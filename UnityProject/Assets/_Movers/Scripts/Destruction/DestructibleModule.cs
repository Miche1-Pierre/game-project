using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A kit wall that breaks where it is hit instead of all at once (ADR-009).
    //
    // At rest it is the kit module exactly as Pierre built it: one mesh, one collider, one node
    // in the structure graph, no cost. The first real hit swaps it for its pre-fractured chunk
    // set (8 to 15 pieces cut in Blender, PKF_<Module>_vN): the chunks are instantiated in place
    // under the module, the intact mesh and its collider are hidden, and from then on damage
    // lands per chunk, by distance. What the wall carries stays: its window panes, casements,
    // door leaf and hinges are children and are left alone, and break only when no chunk is
    // left around them.
    //
    // Each chunk walks down the same ladder as everything else: Intact, Damaged (tinted, at 60 %
    // health), Fractured (darker, at 25 %: still in place, holds what is above it, no longer
    // holds anything beside it), then gone: it breaks off as a real piece of debris. The wall is
    // Damaged once a chunk is hurt, Fractured once one is gone, and Destroyed (collapsed) when
    // under a third of it is left, at which point the rest comes down. Hits too small to break
    // the wall up are stored on the intact wall (tinted once Damaged) and add up: worn down to
    // the fracture share, it breaks up anyway (DEV 2, 3.3).
    //
    // What hangs in the wall goes when no chunk is left around it: a pane breaks, a door leaf
    // shatters, and a window sash frame (INTERACTION hangs it on a hinge under the module)
    // drops as a few pieces of debris. A window with no wall around it does not float.
    //
    // The structure graph decides what falls for lack of support; this class only does what it
    // is told (Release) and tells the graph what it lost (MarkRemoved, MarkWeakened).
    [DisallowMultipleComponent]
    public sealed class DestructibleModule : MonoBehaviour, IDamageable, IStructurePart
    {
        const float HitCooldown = 0.1f;
        const float ArmDelay = 1f;
        const float Touch = 0.03f;          // m: a chunk this close to a face of the wall reaches it
        const float HeldMargin = 0.16f;     // m: a pane or a leaf still touching a chunk is held
        const float DamagedTint = 0.8f, FracturedTint = 0.6f;
        const float SpreadExponent = 1.3f;  // an impact's share falls as (1 - d / spread) to this power
        const float MinFeedbackSize = 0.3f; // m: the dust of a point hit
        const int MaxRubble = 15;           // rubble pieces per chunk at most (4 bits in ChunkDetached)

        public static readonly List<DestructibleModule> All = new List<DestructibleModule>();
        static readonly Dictionary<Collider, DestructibleChunk> byCollider = new Dictionary<Collider, DestructibleChunk>();
        public static int FracturedCount { get; private set; }
        public static float LastFractureMs { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            byCollider.Clear();
            FracturedCount = 0;
            LastFractureMs = 0f;
        }

        // The chunk behind a collider, for the blast and the crosshair: a chunk is not a
        // component, so GetComponentInParent cannot find it.
        public static bool TryGetChunk(Collider c, out DestructibleChunk chunk)
        {
            chunk = null;
            return c != null && byCollider.TryGetValue(c, out chunk) && chunk != null;
        }

        public DestructibleModuleCatalog.Entry Spec { get; private set; }
        public string ModuleName => Spec != null ? Spec.module : name;
        public bool IsFractured { get; private set; }
        public bool HasChunkSet => variant != null && variant.prefab != null;
        // The chunk set it breaks into, for the collider pre-cook (HouseDestruction).
        internal GameObject ChunkSetPrefab => variant != null ? variant.prefab : null;
        public IReadOnlyList<DestructibleChunk> Chunks => chunks;
        public int AttachedCount => attached;
        public int RemovedCount => removedCount;   // broken off by damage
        public int FallenCount => fallenCount;     // came down for lack of support, or with the collapse
        public int GraphNode { get; private set; } = -1;
        public bool IsAnchor { get; private set; }
        public DestructionState State { get; private set; }
        public event System.Action<DestructibleModule, DestructionState> StateChanged;

        public BreakMaterial Material => Spec != null ? Spec.material : BreakMaterial.Plaster;
        public float MaxHealth => Spec != null ? Mathf.Max(1f, Spec.health) : 1f;
        public bool IsGone => State == DestructionState.Destroyed;
        public Bounds WorldBounds => intactBounds;

        // The whole wall's health: its own before it breaks up, the share of chunk health left after.
        public float Health
        {
            get
            {
                if (!IsFractured) return health;
                float left = 0f, max = 0f;
                for (int i = 0; i < chunks.Count; i++)
                {
                    max += chunks[i].MaxHealth;
                    if (chunks[i].Attached) left += Mathf.Max(0f, chunks[i].Health);
                }
                return max > 0f ? MaxHealth * left / max : 0f;
            }
        }

        // The last hit that landed, for the debug overlay.
        public DamageEvent LastHit { get; private set; }
        public float LastHitTime { get; private set; } = -1f;
        public int LastHitChunk { get; private set; } = -1;

        DestructibleModuleCatalog.ChunkVariant variant;
        MeshRenderer body;
        MeshCollider bodyCollider;
        Bounds intactBounds;
        float health;
        float nextHitTime;
        float armedAt;
        GameObject chunkRoot;
        readonly List<DestructibleChunk> chunks = new List<DestructibleChunk>(16);
        int attached, removedCount, fallenCount;
        GlassPane[] panes;
        Breakable[] leaves;
        // Frames with no health of their own (sashes), and the ones that already dropped, so
        // the reset can put them back.
        readonly List<MeshRenderer> fixtures = new List<MeshRenderer>(4);
        readonly List<GameObject> droppedFixtures = new List<GameObject>(4);

        // A sash frame, as the wood it is: its debris mass is its box times this share of the
        // wood density (most of a frame's box is the opening).
        const float FixtureFill = 0.15f;
        const int FixturePieces = 5;

        static readonly List<MeshFilter> filterScratch = new List<MeshFilter>(32);
        static readonly List<MeshRenderer> rendererScratch = new List<MeshRenderer>(32);
        static readonly List<Renderer> single = new List<Renderer>(1);
        static readonly List<int> dataIndexScratch = new List<int>(32);
        static readonly List<float> volumeScratch = new List<float>(32);
        static readonly List<StructureGraph.ChunkSpec> specScratch = new List<StructureGraph.ChunkSpec>(32);
        static readonly List<Vector2Int> pairScratch = new List<Vector2Int>(64);
        static readonly List<float> areaScratch = new List<float>(64);
        static readonly List<int> nodeScratch = new List<int>(32);
        static readonly List<Material> materialScratch = new List<Material>(4);
        static MaterialPropertyBlock tintBlock;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }

        // A scene reload destroys the house without a domain reload: the static registries must
        // not keep this wall or count it.
        void OnDestroy()
        {
            All.Remove(this);
            for (int i = 0; i < chunks.Count; i++)
                if (chunks[i].Collider != null) byCollider.Remove(chunks[i].Collider);
            if (IsFractured) FracturedCount = Mathf.Max(0, FracturedCount - 1);
        }

        // ---- setup (HouseDestruction) ----

        internal void Bind(DestructibleModuleCatalog.Entry spec, DestructibleModuleCatalog.ChunkVariant chunkSet, bool anchor)
        {
            Spec = spec;
            variant = chunkSet;
            IsAnchor = anchor;
            health = MaxHealth;
            State = DestructionState.Intact;
            body = GetComponent<MeshRenderer>();
            bodyCollider = GetComponent<MeshCollider>();
            intactBounds = body != null ? body.bounds
                         : bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
            armedAt = Time.time + ArmDelay;
        }

        internal void SetGraphNode(int node) { GraphNode = node; }

        // ---- damage ----

        // A hit on the intact wall's own collider.
        void OnCollisionEnter(Collision c)
        {
            if (!Net.HasAuthority) return;   // online, walls break on the host (Structure records)
            if (!enabled || IsFractured || IsGone || !Armed()) return;
            if (!ImpactDamage.TryMeasure(c, Material, StructureMass, ImpactDamage.Plain, transform.position, null, out DamageEvent e))
                return;
            nextHitTime = Time.time + HitCooldown;
            ApplyHit(e, -1);
        }

        // A hit on one chunk, passed on by its ChunkCollisionRelay. It spreads like any other hit,
        // centred on that chunk. A chunk this wall launched does nothing to the chunks still in
        // it (DEV 2, 3.14): a breach does not knock the rest of its own wall down.
        internal void OnChunkCollision(int index, Collision c)
        {
            if (!Net.HasAuthority) return;
            if (!enabled || IsGone || index < 0 || index >= chunks.Count || !chunks[index].Attached || !Armed()) return;
            if (IsOwnDebris(c)) return;
            if (!ImpactDamage.TryMeasure(c, Material, StructureMass, ImpactDamage.Plain, chunks[index].Bounds.center, null,
                                         out DamageEvent e))
                return;
            nextHitTime = Time.time + HitCooldown;
            ApplyHit(e, index);
        }

        // The wall's resisting mass in the impact formula (ImpactDamage.Evaluate), as for every
        // built piece.
        static float StructureMass => DestructionMaterialTable.Current.structureReferenceMass;

        bool IsOwnDebris(Collision c)
        {
            DebrisPiece piece = null;
            if (c.collider != null) c.collider.TryGetComponent(out piece);
            if (piece == null && c.rigidbody != null) c.rigidbody.TryGetComponent(out piece);
            return piece != null && piece.structureChunk && ReferenceEquals(piece.structureSource, this);
        }

        // One damage event per 0.1 s, as for Breakable: a crash is one hit, not five contacts.
        // Only a contact that hurts starts the cooldown, so a graze cannot mask the real hit
        // that follows it.
        bool Armed()
        {
            float now = Time.time;
            return now >= armedAt && now >= nextHitTime;
        }

        // The hit that would break it up for real: a small share of one chunk's health. Below
        // it the wall keeps its intact mesh and just remembers the damage.
        public float FractureThreshold =>
            (Spec != null ? Spec.ChunkHealth : MaxHealth) * DestructionMaterialTable.Current.fractureAtShare;

        // The damage stored on the intact wall reached the share that a single hit would need
        // to break it up (fractureOnAccumulated).
        bool WornThrough()
        {
            var table = DestructionMaterialTable.Current;
            return table.fractureOnAccumulated && HasChunkSet && health <= MaxHealth * (1f - table.fractureAtShare);
        }

        // Would this much raw damage (before the material) break the wall up?
        public bool WouldFracture(float rawDamage, DamageType type)
        {
            return !IsFractured && HasChunkSet && rawDamage * DestructionMaterialTable.Factor(type, Material) >= FractureThreshold;
        }

        // Any hit, from anything. An intact wall stores a hit too small to break it up; a broken-up
        // wall spreads it over the chunks around where it lands (DEV 2, 3.5): an impact over its
        // spread (e.radius, from the impact energy) with weights (1 - d / spread)^1.3, a blast
        // routed here over a quarter of its radius (0.5 to 1.5 m), a point hit (no radius) on one
        // chunk. (The blast does better: BlastSolver measures every chunk from the blast itself
        // and calls ApplyToChunk.)
        public DamageResult ApplyDamage(in DamageEvent e) => ApplyHit(e, -1);

        // hitChunk: the chunk a collision landed on, -1 when not known (the nearest one then).
        DamageResult ApplyHit(in DamageEvent e, int hitChunk)
        {
            var before = State;
            if (IsGone || !enabled || !(e.damage > 0f)) return DamageResult.None(before);
            if (!(e.damage * DestructionMaterialTable.Factor(e.type, Material) > 0f)) return DamageResult.None(before);

            var result = !IsFractured && (!WouldFracture(e.damage, e.type) || !Fracture())
                ? WearIntact(e, before)
                : SpreadOverChunks(e, hitChunk, before);
            // What the hit looks and sounds like, by its energy (3.9). A blast has its own.
            if (e.type != DamageType.Blast && result.applied > 0f)
                ImpactFeedback.Hit(e.position, HitEnergy(e), Mathf.Max(MinFeedbackSize, e.radius),
                                   DestructionMaterialTable.Get(Material).sound, e.instigator, result.removed);
            return result;
        }

        // The energy of an impact, for its feedback. ImpactDamage.Evaluate's spread grows with the
        // cube root of the energy, so the spread gives it back (exact under impactMaxSpread); an
        // event with no spread falls back to its impulse times its speed.
        static float HitEnergy(in DamageEvent e)
        {
            var t = DestructionMaterialTable.Current;
            if (e.radius > 0f && t.impactSpreadPerCubeRootKJ > 0f)
            {
                float k = e.radius / t.impactSpreadPerCubeRootKJ;
                return 1000f * k * k * k;
            }
            return Mathf.Max(0f, e.impulse * e.speed);
        }

        // A hit too small to break the wall up is remembered on the intact wall. Small hits add up:
        // worn down far enough, the wall breaks up into chunks that already carry the wear
        // (Fracture shares the health left between them).
        DamageResult WearIntact(in DamageEvent e, DestructionState before)
        {
            LastHit = e;
            LastHitTime = Time.time;
            LastHitChunk = -1;
            float had = health;
            health = Mathf.Max(1f, health - e.damage * DestructionMaterialTable.Factor(e.type, Material));
            if (WornThrough()) Fracture();
            Refresh(e);
            return new DamageResult { applied = had - health, before = before, after = State };
        }

        DamageResult SpreadOverChunks(in DamageEvent e, int hitChunk, DestructionState before)
        {
            var result = new DamageResult { before = before };
            float spread = e.type != DamageType.Blast ? e.radius
                         : e.radius > 0.01f ? Mathf.Clamp(e.radius * 0.25f, 0.5f, 1.5f) : 0f;
            if (spread > 0.01f)
            {
                for (int i = 0; i < chunks.Count && !IsGone; i++)
                {
                    var c = chunks[i];
                    if (!c.Attached) continue;
                    float d = Mathf.Sqrt(c.Bounds.SqrDistance(e.position));
                    float w = Mathf.Pow(Mathf.Clamp01(1f - d / spread), SpreadExponent);
                    if (w <= 0f) continue;
                    var r = HitChunk(i, e.With(e.damage * w, e.impulse * w, e.position, e.direction), true);
                    result.applied += r.applied;
                    result.removed |= r.removed;
                }
                if (hitChunk >= 0 && hitChunk < chunks.Count) LastHitChunk = hitChunk;
            }
            else
            {
                int target = hitChunk >= 0 && hitChunk < chunks.Count && chunks[hitChunk].Attached ? hitChunk : NearestChunk(e.position);
                if (target >= 0)
                {
                    var r = HitChunk(target, e, true);
                    result.applied = r.applied;
                    result.removed = r.removed;
                }
            }
            result.after = State;
            return result;
        }

        public int NearestChunk(Vector3 p)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (!chunks[i].Attached) continue;
                float d = chunks[i].Bounds.SqrDistance(p);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // Damage to one chunk. e.damage is before the material, like everywhere else. applied is
        // the health it really lost (capped at what it had), plus what it passed on.
        public DamageResult ApplyToChunk(int index, in DamageEvent e) => HitChunk(index, e, true);

        // propagate: a chunk broken off with damage to spare passes a share of it on (3.4). What it
        // passes on goes no further.
        DamageResult HitChunk(int index, in DamageEvent e, bool propagate)
        {
            var before = State;
            if (IsGone || index < 0 || index >= chunks.Count) return DamageResult.None(before);
            var c = chunks[index];
            if (!c.Attached || !(e.damage > 0f)) return DamageResult.None(before);
            float dealt = e.damage * DestructionMaterialTable.Factor(e.type, Material);
            if (!(dealt > 0f)) return DamageResult.None(before);
            LastHit = e;
            LastHitTime = Time.time;
            LastHitChunk = index;

            float had = Mathf.Max(0f, c.Health);
            c.Health -= dealt;
            if (Spec != null && Spec.stateCap < DestructionState.Destroyed) c.Health = Mathf.Max(1f, c.Health);
            float applied = had - Mathf.Max(0f, c.Health);
            bool removed = false;
            if (c.Health <= 0f)
            {
                float overkill = -c.Health;
                RemoveChunk(c, e, false, overkill);
                removed = true;
                if (propagate) applied += PassOn(c, e, overkill);
            }
            else UpdateChunkLook(c, e.instigator);
            Refresh(e);
            return new DamageResult { applied = applied, before = before, after = State, removed = removed };
        }

        // A chunk broken off with damage to spare hands overflowShare of the overkill to the chunks
        // it shared a cut face with, by the area of that face: a hard hit tears the edge of the
        // hole instead of stopping at a clean line. Same type, instigator and speed.
        float PassOn(DestructibleChunk from, in DamageEvent e, float overkill)
        {
            float share = DestructionMaterialTable.Current.overflowShare;
            float factor = DestructionMaterialTable.Factor(e.type, Material);
            if (!(share > 0f) || !(overkill > 0f) || !(factor > 0f)) return 0f;
            int[] nb = from.neighbours;
            float[] area = from.neighbourArea;
            float total = 0f;
            for (int k = 0; k < nb.Length; k++)
                if (chunks[nb[k]].Attached) total += area[k];
            if (!(total > 0f)) return 0f;
            float raw = share * overkill / factor;   // back to raw damage: the neighbour applies the material again
            float applied = 0f;
            for (int k = 0; k < nb.Length && !IsGone; k++)
            {
                var n = chunks[nb[k]];
                if (!n.Attached || !(area[k] > 0f)) continue;
                float w = area[k] / total;
                Vector3 at = n.Bounds.ClosestPoint(e.position);
                applied += HitChunk(nb[k], e.With(raw * w, e.impulse * share * w, at, e.direction), false).applied;
            }
            return applied;
        }

        void UpdateChunkLook(DestructibleChunk c, int instigator)
        {
            var table = DestructionMaterialTable.Current;
            float r = c.MaxHealth > 0f ? c.Health / c.MaxHealth : 0f;
            var s = r <= table.chunkFracturedAt ? DestructionState.Fractured
                  : r <= table.chunkDamagedAt ? DestructionState.Damaged : DestructionState.Intact;
            if (s == c.State) return;
            c.State = s;
            // Only once broken up: the looks set inside Fracture follow from its record.
            if (Net.IsHost && IsFractured) StructureSync.ChunkLook(this, c.Index, s);
            Tint(c.Renderer, s == DestructionState.Fractured ? FracturedTint : s == DestructionState.Damaged ? DamagedTint : 1f);
            if (s == DestructionState.Fractured && c.Node >= 0) StructureGraph.Current?.MarkWeakened(c.Node, instigator);
        }

        // Out of the wall: broken off by damage (launched by the hit) or fallen (drops). overkill:
        // the damage past its health, which decides whether a broken-off chunk comes out as rubble.
        void RemoveChunk(DestructibleChunk c, in DamageEvent e, bool fell, float overkill = 0f)
        {
            if (!c.Attached) return;
            Bounds b = c.Bounds;
            c.Attached = false;
            attached--;
            if (fell) fallenCount++;
            else removedCount++;
            if (c.Collider != null) byCollider.Remove(c.Collider);
            if (c.Node >= 0) StructureGraph.Current?.MarkRemoved(c.Node, e.instigator);

            var sound = DestructionMaterialTable.Get(Material).sound;
            if (fell)
            {
                Detach(c, Vector3.down * 0.5f + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 1.5f,
                       e.instigator, true, 0);
                DestructionFX.Dust(b.center, b.extents.magnitude);
                ImpactAudio.Play(sound, b.center, 0.6f, e.instigator);
            }
            else
            {
                Vector3 v = LaunchVelocity(c, e);
                Detach(c, v, Spin(), e.instigator, false, RubblePieces(c, e, overkill));
                // Dust and sound by the energy the chunk flies off with; the client plays its own
                // from ChunkDetached (NetDetach), so this one is not sent.
                ImpactFeedback.Hit(b.center, 0.5f * PhysicsMass(c.Mass) * v.sqrMagnitude, b.extents.magnitude, sound,
                                   e.instigator, true);
            }
            DebrisManager.WakeInBounds(b);
            CheckAttachments(e.instigator);
        }

        // How a chunk leaves the wall (DEV 2, 3.6): along the hit at the striker's speed (for a
        // blast, its eject speed at this chunk), all of it for a blast and impactChunkLaunchShare
        // of it for an impact, a heavy chunk slower and a light one faster, with a little lift.
        // An event with no speed (an older caller) falls back to its impulse over the mass.
        static Vector3 LaunchVelocity(DestructibleChunk c, in DamageEvent e)
        {
            if (!(e.speed > 0f))
                return Vector3.ClampMagnitude(e.ImpulseVector / Mathf.Max(1f, c.Mass), 8f) + e.direction * 0.5f;
            var t = DestructionMaterialTable.Current;
            float share = e.type == DamageType.Blast ? 1f : t.impactChunkLaunchShare;
            float lo = Mathf.Min(t.chunkLaunchMassFactor.x, t.chunkLaunchMassFactor.y);
            float hi = Mathf.Max(t.chunkLaunchMassFactor.x, t.chunkLaunchMassFactor.y);
            float byMass = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, t.chunkLaunchReferenceMass) / PhysicsMass(c.Mass)), lo, hi);
            return e.direction * (e.speed * share * byMass) + Vector3.up * (t.chunkUpBias * e.speed);
        }

        static Vector3 Spin()
        {
            Vector2 s = DestructionMaterialTable.Current.chunkSpin;
            return Random.onUnitSphere * Random.Range(Mathf.Min(s.x, s.y), Mathf.Max(s.x, s.y));
        }

        // A chunk's Rigidbody mass, on both machines: capped at debrisPhysicsMassCap, so a 400 kg
        // slab does not bulldoze the furniture it lands on. Its real mass still counts for the
        // collapse events and the crush blame.
        static float PhysicsMass(float kg)
        {
            float cap = DestructionMaterialTable.Current.debrisPhysicsMassCap;
            return Mathf.Max(1f, cap > 0f ? Mathf.Min(kg, cap) : kg);
        }

        // Big hits break a chunk into rubble (3.7): an overkill of at least rubbleOverkill times its
        // health, or a blast so close that the chunk flies out at least as fast as it would at
        // breachRadius (the eject speed follows the falloff curve, so this is "within breachRadius
        // of the blast" without knowing the blast's centre). More overkill, more pieces. 0: whole.
        static int RubblePieces(DestructibleChunk c, in DamageEvent e, float overkill)
        {
            var t = DestructionMaterialTable.Current;
            float ratio = c.MaxHealth > 0f ? overkill / c.MaxHealth : 0f;
            bool big = t.rubbleOverkill > 0f && ratio >= t.rubbleOverkill;
            bool breach = e.type == DamageType.Blast && e.speed > 0f && t.breachRadius > 0f && t.blastChunkEjectSpeed > 0f
                          && e.speed >= t.blastChunkEjectSpeed * DestructionMaterialTable.Focus(t.breachRadius) - 0.01f;
            if (!big && !breach) return 0;
            int lo = Mathf.Clamp(Mathf.Min(t.rubblePieces.x, t.rubblePieces.y), 2, MaxRubble);
            int hi = Mathf.Clamp(Mathf.Max(t.rubblePieces.x, t.rubblePieces.y), lo, MaxRubble);
            float more = t.rubbleOverkill > 0f ? Mathf.Clamp01((ratio - t.rubbleOverkill) / (2f * t.rubbleOverkill)) : 1f;
            return Mathf.RoundToInt(Mathf.Lerp(lo, hi, more));
        }

        // The chunk object becomes debris: rubble for a big hit when the budget allows it, else the
        // whole slab as one body. Never nothing (3.8): when the frame's budget is spent the slab
        // waits, collider off and still drawn, and is retried on the next frames, then forced.
        // Online, each exit tells the client when it really happens, with the velocities the host
        // gave it and the rubble count (fell: it came down for lack of support).
        void Detach(DestructibleChunk c, Vector3 velocity, Vector3 angular, int instigator, bool fell, int rubble)
        {
            if (c.Transform == null)
            {
                if (Net.IsHost) StructureSync.ChunkDetached(this, c.Index, fell, true, velocity, Vector3.zero, 0);
                return;
            }
            if (c.Transform.TryGetComponent(out ChunkCollisionRelay relay)) relay.owner = null;
            if (rubble > 0 && Shatter(c, velocity, instigator, rubble))
            {
                if (Net.IsHost) StructureSync.ChunkDetached(this, c.Index, fell, false, velocity, angular, rubble);
                return;
            }
            var manager = DebrisManager.Instance;
            if (manager != null && manager.TryReserveStructure(1, out _))
            {
                MakeDebris(c, velocity, angular, instigator, fell);
                if (Net.IsHost) StructureSync.ChunkDetached(this, c.Index, fell, false, velocity, angular, 0);
                return;
            }
            Defer(c, velocity, angular, instigator, fell);
        }

        // The chunk cut into rubble, every piece flying with it. False when the budget gave no
        // piece: the caller then lets the whole slab go.
        bool Shatter(DestructibleChunk c, Vector3 velocity, int instigator, int pieces)
        {
            if (c.Renderer == null) return false;
            Bounds b = c.Bounds;
            var made = MeshShatter.ShatterChunk(c.Renderer, pieces, PhysicsMass(c.Mass), velocity, b.center,
                                                DestructionMaterialTable.Current.structureDebrisLifetime, instigator);
            if (made == null || made.Count == 0) return false;
            for (int i = 0; i < made.Count; i++)
            {
                if (made[i] == null || !made[i].TryGetComponent(out DebrisPiece piece)) continue;
                piece.material = Material;
                MarkWallDebris(piece, false);
            }
            // The rubble took its place in this same frame: the slab itself goes.
            if (c.Collider != null) c.Collider.enabled = false;
            c.Renderer.enabled = false;
            Destroy(c.Transform.gameObject);
            return true;
        }

        void MakeDebris(DestructibleChunk c, Vector3 velocity, Vector3 angular, int instigator, bool fell)
        {
            GameObject go = c.Transform.gameObject;
            if (c.Collider != null) c.Collider.enabled = true;
            // A dynamic body needs a convex collider (see MakeCollider); its hull was cooked at load
            // (HouseDestruction), so this does not cook one now.
            if (go.TryGetComponent(out MeshCollider mc) && !mc.convex) mc.convex = true;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = PhysicsMass(c.Mass);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxDepenetrationVelocity = 3f;
            rb.linearVelocity = velocity;
            rb.angularVelocity = angular;
            var piece = go.AddComponent<DebrisPiece>();
            piece.Init(rb, null);
            piece.instigator = instigator;
            piece.material = Material;
            MarkWallDebris(piece, fell);
            var manager = DebrisManager.Instance;
            if (manager != null) manager.Register(piece, DestructionMaterialTable.Current.structureDebrisLifetime);
        }

        // A piece of this wall: it never hurts the chunks still in it (3.14), strikes other built
        // pieces softly (ImpactDamage), and the blast that launched it does not push it a second
        // time within structureLaunchGrace (a fallen chunk was not launched: any blast pushes it).
        void MarkWallDebris(DebrisPiece piece, bool fell)
        {
            piece.structureChunk = true;
            piece.structureSource = this;
            piece.launchedAt = fell ? float.NegativeInfinity : Time.time;
        }

        // ---- deferred detaches (3.8) ----

        struct PendingDetach
        {
            public int chunk;
            public Vector3 velocity, angular;
            public int instigator;
            public bool fell;
            public int tries;
            public int frame;
        }

        const int DetachRetries = 3;
        readonly List<PendingDetach> pending = new List<PendingDetach>(4);

        // No budget this frame: the chunk leaves the wall for physics at once (collider off, so
        // nothing hits a chunk that is gone) and stays drawn until it can fall.
        void Defer(DestructibleChunk c, Vector3 velocity, Vector3 angular, int instigator, bool fell)
        {
            if (c.Collider != null) c.Collider.enabled = false;
            pending.Add(new PendingDetach
            {
                chunk = c.Index, velocity = velocity, angular = angular, instigator = instigator, fell = fell,
                tries = 0, frame = Time.frameCount,
            });
        }

        void Update()
        {
            if (pending.Count > 0) RetryDetaches();
        }

        // Once per frame after the one it was refused in, with the same velocity; the third retry
        // is always granted. On the host the record goes out now, when the chunk really falls.
        void RetryDetaches()
        {
            int frame = Time.frameCount;
            var manager = DebrisManager.Instance;
            for (int i = 0; i < pending.Count; i++)
            {
                var p = pending[i];
                if (p.frame >= frame) continue;
                p.tries++;
                p.frame = frame;
                var c = p.chunk >= 0 && p.chunk < chunks.Count ? chunks[p.chunk] : null;
                if (c == null || c.Transform == null)
                {
                    pending.RemoveAt(i--);
                    continue;
                }
                bool granted = manager != null && manager.TryReserveStructure(1, out _);
                if (!granted && p.tries < DetachRetries)
                {
                    pending[i] = p;
                    continue;
                }
                pending.RemoveAt(i--);
                MakeDebris(c, p.velocity, p.angular, p.instigator, p.fell);
                if (Net.IsHost) StructureSync.ChunkDetached(this, c.Index, p.fell, false, p.velocity, p.angular, 0);
            }
        }

        // Panes, casements and door leaves hang in the wall. Once no chunk touches one, it
        // breaks: a window with no wall around it does not float.
        void CheckAttachments(int instigator)
        {
            if (panes != null)
                for (int i = 0; i < panes.Length; i++)
                {
                    var p = panes[i];
                    if (p == null || p.IsBroken || !p.gameObject.activeInHierarchy) continue;
                    Bounds b = p.WorldBounds;
                    if (HeldByChunks(b)) continue;
                    p.Shatter(new DamageEvent(b.center, Vector3.down, 0f, 0f, 0f, DamageType.Fall, instigator));
                }
            if (leaves != null)
                for (int i = 0; i < leaves.Length; i++)
                {
                    var l = leaves[i];
                    if (l == null || l.IsDestroyed || !l.gameObject.activeInHierarchy) continue;
                    Bounds b = l.WorldBounds;
                    if (HeldByChunks(b)) continue;
                    l.Shatter(new DamageEvent(b.center, Vector3.down, 0f, 0f, 0f, DamageType.Fall, instigator));
                }
            for (int i = 0; i < fixtures.Count; i++)
            {
                var r = fixtures[i];
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (HeldByChunks(r.bounds)) continue;
                DropFixture(r, instigator);
            }
        }

        // A sash frame with no wall left around it: its glass goes first (each pane says so
        // itself), then the frame falls as a few pieces of wood and switches off, the way a
        // Breakable goes. HingedPanel sees an inactive panel and stops offering it.
        void DropFixture(MeshRenderer r, int instigator)
        {
            Bounds b = r.bounds;
            var fall = new DamageEvent(b.center, Vector3.down, 0f, 0f, 0f, DamageType.Fall, instigator);
            GlassPane[] own = r.GetComponentsInChildren<GlassPane>(false);
            for (int k = 0; k < own.Length; k++)
                if (own[k] != null && !own[k].IsBroken) own[k].Shatter(fall);
            if (r.enabled)
            {
                var wood = DestructionMaterialTable.Get(BreakMaterial.Wood);
                float kg = Mathf.Clamp(b.size.x * b.size.y * b.size.z * wood.density * FixtureFill, 2f, 60f);
                single.Clear();
                single.Add(r);
                MeshShatter.Shatter(single, FixturePieces, kg, Vector3.zero, b.center, Vector3.down,
                                    DestructionMaterialTable.Current.propDebrisLifetime, instigator);
                single.Clear();
                ImpactAudio.Play(wood.sound, b.center, 0.6f, instigator);
            }
            droppedFixtures.Add(r.gameObject);
            r.gameObject.SetActive(false);
        }

        // Panes, leaves and frames still showing with no chunk around them. Always 0 unless
        // something is wrong: for the tests and the overlay.
        public int LooseAttachmentCount()
        {
            if (!IsFractured) return 0;
            int n = 0;
            if (panes != null)
                for (int i = 0; i < panes.Length; i++)
                    if (panes[i] != null && !panes[i].IsBroken && panes[i].gameObject.activeInHierarchy && !HeldByChunks(panes[i].WorldBounds)) n++;
            if (leaves != null)
                for (int i = 0; i < leaves.Length; i++)
                    if (leaves[i] != null && !leaves[i].IsDestroyed && leaves[i].gameObject.activeInHierarchy && !HeldByChunks(leaves[i].WorldBounds)) n++;
            for (int i = 0; i < fixtures.Count; i++)
                if (fixtures[i] != null && fixtures[i].gameObject.activeInHierarchy && !HeldByChunks(fixtures[i].bounds)) n++;
            return n;
        }

        // What the wall carries that has no health of its own: every mesh under the module that
        // is not a chunk, not a pane and not part of a door leaf (which shatters on its own).
        void CollectFixtures()
        {
            fixtures.Clear();
            GetComponentsInChildren(true, rendererScratch);
            Transform chunks = chunkRoot != null ? chunkRoot.transform : null;
            for (int i = 0; i < rendererScratch.Count; i++)
            {
                var r = rendererScratch[i];
                if (r == null || r == body) continue;
                if (chunks != null && r.transform.IsChildOf(chunks)) continue;
                if (r.TryGetComponent(out GlassPane _)) continue;
                if (r.GetComponentInParent<Breakable>(true) != null) continue;
                if (!r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
                fixtures.Add(r);
            }
            rendererScratch.Clear();
        }

        bool HeldByChunks(Bounds b)
        {
            b.Expand(HeldMargin * 2f);
            for (int i = 0; i < chunks.Count; i++)
                if (chunks[i].Attached && chunks[i].Bounds.Intersects(b)) return true;
            return false;
        }

        // The wall's own state from its chunks; says so when it changes. When too little is left
        // it collapses: the rest comes down, and the kg that fell is returned. The collapse queue
        // passes announceCollapse false and reports that mass itself, in its one
        // StructureCollapsed for this wall, so the wall is never counted twice.
        float Refresh(in DamageEvent e, bool announceCollapse = true)
        {
            DestructionState s;
            if (!IsFractured) s = health < MaxHealth - 0.01f ? DestructionState.Damaged : DestructionState.Intact;
            else if (chunks.Count == 0 || attached < chunks.Count * DestructionMaterialTable.Current.collapseBelowShare)
                s = DestructionState.Destroyed;
            else if (removedCount + fallenCount > 0) s = DestructionState.Fractured;
            else s = AnyChunkHurt() ? DestructionState.Damaged : DestructionState.Intact;
            if (Spec != null && s > Spec.stateCap) s = Spec.stateCap;
            if (s == State) return 0f;
            State = s;
            if (!IsFractured) TintBody();
            if (Net.IsHost) StructureSync.ModuleState(this, s);
            Vector3 at = intactBounds.center;
            float kg = 0f;
            if (s == DestructionState.Destroyed)
            {
                kg = FallRemaining(e.instigator);
                if (announceCollapse && kg > 0f) DestructionEvents.Collapsed(this, at, kg, e.instigator);
            }
            try { StateChanged?.Invoke(this, s); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            DestructionEvents.Structure(this, at, s, e.instigator);
            if (s == DestructionState.Destroyed && DestructionEvents.IsDoor(name)) DestructionEvents.Door(this, at, e.instigator);
            return kg;
        }

        // The intact wall shows its stored damage, like a chunk does: repeated small hits read.
        void TintBody()
        {
            Tint(body, State == DestructionState.Damaged ? DamagedTint : 1f);
        }

        bool AnyChunkHurt()
        {
            for (int i = 0; i < chunks.Count; i++)
                if (chunks[i].Attached && chunks[i].Health < chunks[i].MaxHealth) return true;
            return false;
        }

        // Too little left to stand: everything still attached comes down. Returns the kg that fell.
        float FallRemaining(int instigator)
        {
            var cause = new DamageEvent(intactBounds.center, Vector3.down, 0f, 0f, 0f, DamageType.Fall, instigator);
            float kg = 0f;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (!chunks[i].Attached) continue;
                kg += chunks[i].Mass;
                RemoveChunk(chunks[i], cause, true);
            }
            CheckAttachments(instigator);
            return kg;
        }

        // ---- breaking up ----

        // Swaps the intact wall for its chunk set. Lazy and idempotent. forCollapse: the whole
        // wall is about to fall, the graph needs no chunk nodes.
        public bool Fracture() => Fracture(false);

        bool Fracture(bool forCollapse)
        {
            if (IsFractured || !HasChunkSet) return false;
            double t0 = Time.realtimeSinceStartupAsDouble;
            var data = ChunkSetData.Get(variant.graph);

            chunkRoot = Instantiate(variant.prefab, transform, false);
            chunkRoot.name = "Chunks";
            chunkRoot.transform.localPosition = Vector3.zero;
            chunkRoot.transform.localRotation = Quaternion.identity;
            chunkRoot.transform.localScale = Vector3.one;

            // Which meshes are chunks. Glass shards in the set are dropped: the GlassPanes the
            // house already split off are the glass.
            filterScratch.Clear();
            dataIndexScratch.Clear();
            volumeScratch.Clear();
            chunkRoot.GetComponentsInChildren(true, filterScratch);
            float totalVolume = 0f;
            for (int i = filterScratch.Count - 1; i >= 0; i--)
            {
                var mf = filterScratch[i];
                int di = data != null ? data.IndexOf(mf.name) : -1;
                bool glass = di >= 0 ? data.chunks[di].glass : IsGlassOnly(mf);
                if (mf.sharedMesh == null || glass)
                {
                    mf.gameObject.SetActive(false);
                    filterScratch.RemoveAt(i);
                }
            }
            for (int i = 0; i < filterScratch.Count; i++)
            {
                var mf = filterScratch[i];
                int di = data != null ? data.IndexOf(mf.name) : -1;
                float vol = di >= 0 && data.chunks[di].volume > 0f ? data.chunks[di].volume : BoxVolume(mf) * 0.8f;
                dataIndexScratch.Add(di);
                volumeScratch.Add(vol);
                totalVolume += vol;
            }

            int n = filterScratch.Count;
            if (n == 0)
            {
                Destroy(chunkRoot);
                chunkRoot = null;
                Debug.LogWarning("[Destruction] " + name + ": chunk set " + variant.prefab.name + " has no chunk meshes; the wall stays whole.");
                variant = null;
                return false;
            }

            var table = DestructionMaterialTable.Current;
            float density = DestructionMaterialTable.Get(Material).density;
            float baseHealth = Spec != null ? Spec.ChunkHealth : MaxHealth;
            float left = Mathf.Clamp01(health / MaxHealth);
            int structure = DestructionLayers.Structure;
            bool longX = intactBounds.size.x >= intactBounds.size.z;
            float hpMin = Mathf.Max(0.05f, Mathf.Min(table.chunkHealthClamp.x, table.chunkHealthClamp.y));
            float hpMax = Mathf.Max(hpMin, Mathf.Max(table.chunkHealthClamp.x, table.chunkHealthClamp.y));

            for (int k = 0; k < n; k++)
            {
                var mf = filterScratch[k];
                GameObject go = mf.gameObject;
                DestructionLayers.Assign(go, structure);
                var r = go.GetComponent<MeshRenderer>();
                if (r != null && body != null)
                {
                    r.shadowCastingMode = body.shadowCastingMode;
                    r.receiveShadows = body.receiveShadows;
                    r.lightProbeUsage = body.lightProbeUsage;
                    r.reflectionProbeUsage = body.reflectionProbeUsage;
                }
                Collider col = MakeCollider(go, mf.sharedMesh);

                int di = dataIndexScratch[k];
                float share = di >= 0 && data.chunks[di].massShare > 0f ? data.chunks[di].massShare
                            : totalVolume > 0f ? volumeScratch[k] / totalVolume : 1f / n;
                // Bigger chunks take more, within chunkHealthClamp of the wall's chunkHealth.
                float maxHp = baseHealth * Mathf.Clamp(share * n, hpMin, hpMax);
                var chunk = new DestructibleChunk(this, chunks.Count, go.transform, col, r,
                                                  Mathf.Max(5f, volumeScratch[k] * density), maxHp);
                chunk.Health = maxHp * left;

                Bounds cb = chunk.Bounds;
                if (di >= 0 && data.chunks[di].anchorsKnown)
                {
                    var d = data.chunks[di];
                    chunk.bottom = d.bottom;
                    chunk.top = d.top;
                    chunk.sideNeg = d.sideNeg;
                    chunk.sidePos = d.sidePos;
                }
                else
                {
                    chunk.bottom = cb.min.y - intactBounds.min.y < Touch;
                    chunk.top = intactBounds.max.y - cb.max.y < Touch;
                    chunk.sideNeg = longX ? cb.min.x - intactBounds.min.x < Touch : cb.min.z - intactBounds.min.z < Touch;
                    chunk.sidePos = longX ? intactBounds.max.x - cb.max.x < Touch : intactBounds.max.z - cb.max.z < Touch;
                }

                var relay = go.AddComponent<ChunkCollisionRelay>();
                relay.owner = this;
                relay.index = chunk.Index;
                if (col != null) byCollider[col] = chunk;
                chunks.Add(chunk);
                if (left < 1f) UpdateChunkLook(chunk, LastHit.instigator);
            }
            attached = chunks.Count;
            BuildNeighbours(data);

            if (body != null) body.enabled = false;
            if (bodyCollider != null) bodyCollider.enabled = false;
            IsFractured = true;
            if (Net.IsHost) StructureSync.ModuleFractured(this, left);
            FracturedCount++;
            panes = GetComponentsInChildren<GlassPane>(true);
            leaves = GetComponentsInChildren<Breakable>(true);
            CollectFixtures();

            var graph = StructureGraph.Current;
            if (!forCollapse && graph != null && GraphNode >= 0 && graph.IsLive(GraphNode))
            {
                BuildSpecs(data);
                graph.ReplaceNode(GraphNode, this, specScratch, pairScratch, nodeScratch);
                for (int k = 0; k < nodeScratch.Count && k < chunks.Count; k++)
                {
                    chunks[k].Node = nodeScratch[k];
                    // A wall worn down before it broke up comes apart into chunks that are
                    // already Fractured (UpdateChunkLook ran before they had nodes): they hold
                    // what is above them and nothing beside them, from the first moment.
                    if (chunks[k].State == DestructionState.Fractured) graph.MarkWeakened(nodeScratch[k], LastHit.instigator);
                }
            }
            filterScratch.Clear();
            LastFractureMs = (float)((Time.realtimeSinceStartupAsDouble - t0) * 1000.0);
            return true;
        }

        // The chunk's exact shape while it is still part of the wall. A chunk next to a window
        // or a doorway can be L-shaped, and its convex hull would fill a corner of the opening:
        // the fracture check measured up to 0.16 m2 of window blocked that way. An attached chunk
        // has no Rigidbody, so a non-convex MeshCollider is allowed; Detach makes it convex just
        // before it becomes a falling body, where a hull no longer matters. A box when the mesh
        // cannot be read.
        static Collider MakeCollider(GameObject go, Mesh mesh)
        {
            if (mesh.isReadable)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
                return mc;
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.04f);
            return box;
        }

        void BuildSpecs(ChunkSetData data)
        {
            specScratch.Clear();
            pairScratch.Clear();
            for (int k = 0; k < chunks.Count; k++)
            {
                var c = chunks[k];
                specScratch.Add(new StructureGraph.ChunkSpec
                {
                    bounds = c.Bounds,
                    anchor = IsAnchor && c.bottom,
                    bottom = c.bottom,
                    top = c.top,
                    border = c.Border,
                    side = c.sideNeg || c.sidePos,
                });
            }
            if (data != null && data.neighbours.Count > 0)
            {
                // Sidecar indices count every chunk, glass included; ours skip the glass.
                for (int p = 0; p < data.neighbours.Count; p++)
                {
                    int a = ChunkOfData(data.neighbours[p].x), b = ChunkOfData(data.neighbours[p].y);
                    if (a >= 0 && b >= 0) pairScratch.Add(new Vector2Int(a, b));
                }
                return;
            }
            for (int a = 0; a < chunks.Count; a++)
            {
                Bounds ba = specScratch[a].bounds;
                ba.Expand(0.04f);
                for (int b = a + 1; b < chunks.Count; b++)
                    if (ba.Intersects(specScratch[b].bounds)) pairScratch.Add(new Vector2Int(a, b));
            }
        }

        // Each chunk's cut-face neighbours and the area of each face, for the propagation: from the
        // sidecar when there is one (pairs that only touch, a frame against the plaster, share no
        // face and pass nothing on), else from the boxes, every touching pair counting the same.
        void BuildNeighbours(ChunkSetData data)
        {
            pairScratch.Clear();
            areaScratch.Clear();
            if (data != null && data.neighbours.Count > 0)
            {
                for (int p = 0; p < data.neighbours.Count; p++)
                {
                    float area = p < data.neighbourArea.Count ? data.neighbourArea[p] : 1f;
                    int a = ChunkOfData(data.neighbours[p].x), b = ChunkOfData(data.neighbours[p].y);
                    if (a < 0 || b < 0 || !(area > 0f)) continue;
                    pairScratch.Add(new Vector2Int(a, b));
                    areaScratch.Add(area);
                }
            }
            else
            {
                for (int a = 0; a < chunks.Count; a++)
                {
                    Bounds ba = chunks[a].Bounds;
                    ba.Expand(0.04f);
                    for (int b = a + 1; b < chunks.Count; b++)
                    {
                        if (!ba.Intersects(chunks[b].Bounds)) continue;
                        pairScratch.Add(new Vector2Int(a, b));
                        areaScratch.Add(1f);
                    }
                }
            }
            for (int k = 0; k < chunks.Count; k++)
            {
                int count = 0;
                for (int p = 0; p < pairScratch.Count; p++)
                    if (pairScratch[p].x == k || pairScratch[p].y == k) count++;
                var nb = new int[count];
                var area = new float[count];
                int j = 0;
                for (int p = 0; p < pairScratch.Count; p++)
                {
                    var pair = pairScratch[p];
                    if (pair.x != k && pair.y != k) continue;
                    nb[j] = pair.x == k ? pair.y : pair.x;
                    area[j] = areaScratch[p];
                    j++;
                }
                chunks[k].neighbours = nb;
                chunks[k].neighbourArea = area;
            }
            pairScratch.Clear();
            areaScratch.Clear();
        }

        int ChunkOfData(int dataIndex)
        {
            for (int k = 0; k < dataIndexScratch.Count && k < chunks.Count; k++)
                if (dataIndexScratch[k] == dataIndex) return k;
            return -1;
        }

        static float BoxVolume(MeshFilter mf)
        {
            Vector3 s = mf.sharedMesh.bounds.size;
            Vector3 l = mf.transform.lossyScale;
            return Mathf.Abs(s.x * l.x * s.y * l.y * s.z * l.z);
        }

        static bool IsGlassOnly(MeshFilter mf)
        {
            if (!mf.TryGetComponent(out MeshRenderer r)) return false;
            materialScratch.Clear();
            r.GetSharedMaterials(materialScratch);
            bool any = false;
            for (int i = 0; i < materialScratch.Count; i++)
            {
                var m = materialScratch[i];
                if (m == null) continue;
                if (m.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) < 0) { materialScratch.Clear(); return false; }
                any = true;
            }
            materialScratch.Clear();
            return any;
        }

        static void Tint(Renderer r, float factor)
        {
            if (r == null) return;
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
            materialScratch.Clear();
            r.GetSharedMaterials(materialScratch);
            for (int m = 0; m < materialScratch.Count; m++)
            {
                Material mat = materialScratch[m];
                if (mat == null || !mat.HasProperty(ColorId)) continue;
                Color c = mat.GetColor(ColorId);
                r.GetPropertyBlock(tintBlock, m);
                tintBlock.SetColor(ColorId, new Color(c.r * factor, c.g * factor, c.b * factor, c.a));
                r.SetPropertyBlock(tintBlock, m);
            }
            materialScratch.Clear();
        }

        // ---- the structure graph ----

        float IStructurePart.Release(int node, int part, in DamageEvent cause)
        {
            if (IsGone) return 0f;
            if (node == GraphNode && !IsFractured) return CollapseWhole(cause);
            if (part < 0 || part >= chunks.Count || !chunks[part].Attached) return 0f;
            var c = chunks[part];
            RemoveChunk(c, cause, true);
            // If that was one chunk too many, the rest of the wall came down with it: its mass
            // goes into the queue's report instead of a second StructureCollapsed.
            return c.Mass + Refresh(cause, false);
        }

        // The whole wall lost its support before it ever broke up: it comes down in its chunks.
        float CollapseWhole(in DamageEvent cause)
        {
            if (!Fracture(true))
            {
                StructureGraph.Current?.MarkRemoved(GraphNode, cause.instigator);
                return 0f;
            }
            StructureGraph.Current?.MarkRemoved(GraphNode, cause.instigator);
            float kg = 0f;
            for (int i = 0; i < chunks.Count; i++) kg += chunks[i].Mass;
            State = DestructionState.Destroyed;
            if (Net.IsHost) StructureSync.ModuleState(this, State);
            FallRemaining(cause.instigator);   // the collapse queue reports the fall itself
            Vector3 at = intactBounds.center;
            try { StateChanged?.Invoke(this, State); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            DestructionEvents.Structure(this, at, State, cause.instigator);
            if (DestructionEvents.IsDoor(name)) DestructionEvents.Door(this, at, cause.instigator);
            return kg;
        }

        bool IStructurePart.SplitForSupport(int node)
        {
            if (node != GraphNode || IsFractured || !HasChunkSet) return false;
            return Fracture(false);
        }

        string IStructurePart.Describe(int part)
        {
            if (part < 0 || part >= chunks.Count) return name + " (" + ModuleName + ")";
            var c = chunks[part];
            return name + " chunk " + part + " (" + c.Health.ToString("0") + "/" + c.MaxHealth.ToString("0") + ", " + c.State + ")";
        }

        // ---- online client (NETCODE_SLICE 11.4) ----
        //
        // The host's structure records, applied as bookkeeping and picture only: no graph (it is
        // not installed on the client), no Refresh, no events, no damage. Sounds come from the
        // host's own Props Sound records, except a broken-off chunk's (NetDetach plays it from the
        // speed it was given). Silent is the join snapshot: no dust, no debris.

        // The wall breaks up into its chunks, with the host's share of health left, so every
        // chunk starts with the host's look.
        public void NetFracture(float healthLeft01)
        {
            if (IsFractured || !HasChunkSet) return;
            health = Mathf.Clamp01(healthLeft01) * MaxHealth;
            Fracture(false);
        }

        // One chunk out of the wall, as on the host (3.8): rubble when the host cut it (rubble > 0)
        // and this machine's budget allows it, else the whole slab as a body with the host's
        // velocities (its mass capped like the host's), deferred like the host's when the frame's
        // budget is spent. Never hidden while it is live here. The window frames with nothing left
        // around them drop here too; the panes and door leaves have their own records.
        public void NetDetach(int index, bool fell, bool vanished, Vector3 velocity, Vector3 angular, bool silent,
                              int rubble = 0)
        {
            if (!IsFractured || index < 0 || index >= chunks.Count) return;
            var c = chunks[index];
            if (!c.Attached) return;
            Bounds b = c.Bounds;
            c.Attached = false;
            attached--;
            if (fell) fallenCount++;
            else removedCount++;
            if (c.Collider != null) byCollider.Remove(c.Collider);

            if (c.Transform != null)
            {
                GameObject go = c.Transform.gameObject;
                if (go.TryGetComponent(out ChunkCollisionRelay relay)) relay.owner = null;
                // The join snapshot: gone before this machine joined, never a live chunk here. (A
                // chunk the host had no object for still falls here: nothing live vanishes.)
                if (silent) go.SetActive(false);
                else if (!(rubble > 0 && Shatter(c, velocity, Actors.World, rubble)))
                {
                    var manager = DebrisManager.Instance;
                    if (manager != null && manager.TryReserveStructure(1, out _)) MakeDebris(c, velocity, angular, Actors.World, fell);
                    else Defer(c, velocity, angular, Actors.World, fell);
                }
            }
            if (!silent)
            {
                if (fell) DestructionFX.Dust(b.center, b.extents.magnitude);   // its sound is the host's (Props Sound)
                else LaunchFeedback(b, velocity, c.Mass);
                DebrisManager.WakeInBounds(b);
            }
            NetDropLooseFixtures(silent);
        }

        // The client's dust and sound for a chunk broken off, scaled by the speed the host gave it:
        // the host's own ImpactFeedback for the removal is not sent (3.9), so this is the only puff.
        void LaunchFeedback(Bounds b, Vector3 velocity, float mass)
        {
            float strength = ImpactFeedback.Strength01(0.5f * PhysicsMass(mass) * velocity.sqrMagnitude);
            DestructionFX.Dust(b.center, b.extents.magnitude * (1f + strength));
            ImpactAudio.PlayFromNet(DestructionMaterialTable.Get(Material).sound, b.center, Mathf.Lerp(0.3f, 1f, strength));
        }

        // DropFixture's picture, for the client: the frame's own panes break here first (the
        // host breaks them too, and their records then find them broken), then the frame falls.
        void NetDropLooseFixtures(bool silent)
        {
            for (int i = 0; i < fixtures.Count; i++)
            {
                var r = fixtures[i];
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (HeldByChunks(r.bounds)) continue;
                Bounds b = r.bounds;
                if (!silent)
                {
                    var fall = new DamageEvent(b.center, Vector3.down, 0f, 0f, 0f, DamageType.Fall, Actors.World);
                    GlassPane[] own = r.GetComponentsInChildren<GlassPane>(false);
                    for (int k = 0; k < own.Length; k++)
                        if (own[k] != null && !own[k].IsBroken) own[k].NetApply(DestructionState.Destroyed, fall, false);
                    if (r.enabled)
                    {
                        var wood = DestructionMaterialTable.Get(BreakMaterial.Wood);
                        float kg = Mathf.Clamp(b.size.x * b.size.y * b.size.z * wood.density * FixtureFill, 2f, 60f);
                        single.Clear();
                        single.Add(r);
                        MeshShatter.Shatter(single, FixturePieces, kg, Vector3.zero, b.center, Vector3.down,
                                            DestructionMaterialTable.Current.propDebrisLifetime, Actors.World);
                        single.Clear();
                    }
                }
                droppedFixtures.Add(r.gameObject);
                r.gameObject.SetActive(false);
            }
        }

        public void NetChunkLook(int index, DestructionState s)
        {
            if (!IsFractured || index < 0 || index >= chunks.Count) return;
            var c = chunks[index];
            if (c.State == s) return;
            c.State = s;
            Tint(c.Renderer, s == DestructionState.Fractured ? FracturedTint : s == DestructionState.Damaged ? DamagedTint : 1f);
        }

        public void NetState(DestructionState s)
        {
            State = s;
            if (!IsFractured) TintBody();
        }

        // The join snapshot of a fractured wall: its chunks, their looks, which are gone (hidden,
        // no debris) and which frames dropped. Chunks past the 32nd are left as they are (the
        // re-fractured sets have at most 28).
        public void NetApplySnapshot(float healthLeft01, DestructionState state, ulong attachedMask, ulong looks, byte fixturesMask)
        {
            NetFracture(healthLeft01);
            if (IsFractured)
            {
                for (int i = 0; i < chunks.Count && i < SnapshotChunks; i++)
                {
                    NetChunkLook(i, (DestructionState)((looks >> (i * 2)) & 3ul));
                    if ((attachedMask & (1ul << i)) == 0) NetDetach(i, false, true, Vector3.zero, Vector3.zero, true);
                }
                for (int k = 0; k < fixtures.Count && k < SnapshotFixtures; k++)
                {
                    var r = fixtures[k];
                    if ((fixturesMask & (1 << k)) == 0 || r == null || !r.gameObject.activeSelf) continue;
                    droppedFixtures.Add(r.gameObject);
                    r.gameObject.SetActive(false);
                }
            }
            State = state;
        }

        const int SnapshotChunks = 32, SnapshotFixtures = 8;

        // Host: what NetApplySnapshot needs, for a fractured wall.
        internal void NetSnapshot(out float healthLeft01, out ulong attachedMask, out ulong looks, out byte fixturesMask)
        {
            healthLeft01 = Mathf.Clamp01(health / MaxHealth);
            attachedMask = 0ul;
            looks = 0ul;
            for (int i = 0; i < chunks.Count && i < SnapshotChunks; i++)
            {
                if (chunks[i].Attached) attachedMask |= 1ul << i;
                looks |= ((ulong)chunks[i].State & 3ul) << (i * 2);
            }
            fixturesMask = 0;
            for (int k = 0; k < fixtures.Count && k < SnapshotFixtures; k++)
                if (fixtures[k] != null && droppedFixtures.Contains(fixtures[k].gameObject)) fixturesMask |= (byte)(1 << k);
        }

        // ---- debug and tests ----

        // Takes every chunk out, as debris. For the debug tools and the tests: the "this wall
        // is gone" case without having to aim ten grenades.
        public void Demolish(in DamageEvent e)
        {
            if (IsGone) return;
            if (Spec != null && Spec.stateCap < DestructionState.Destroyed) return;
            if (!IsFractured && !Fracture())
            {
                health = 1f;
                Refresh(e);
                return;
            }
            for (int i = 0; i < chunks.Count; i++)
                if (chunks[i].Attached) RemoveChunk(chunks[i], e, false);
            Refresh(e);
        }

        // Back to the intact kit module (the destruction reset, F11). The chunks already thrown
        // are destroyed; the graph is rebuilt by HouseDestruction afterwards.
        public void Revive()
        {
            pending.Clear();
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                if (c.Collider != null) byCollider.Remove(c.Collider);
                if (!c.Attached && c.Transform != null)
                {
                    c.Transform.gameObject.SetActive(false);
                    Destroy(c.Transform.gameObject);
                }
            }
            chunks.Clear();
            if (chunkRoot != null)
            {
                chunkRoot.SetActive(false);
                Destroy(chunkRoot);
                chunkRoot = null;
            }
            if (IsFractured) FracturedCount = Mathf.Max(0, FracturedCount - 1);
            IsFractured = false;
            attached = removedCount = fallenCount = 0;
            panes = null;
            leaves = null;
            // Frames back in the wall; their panes come back with Breakable.ReviveAll, which the
            // reset runs after this.
            for (int i = 0; i < droppedFixtures.Count; i++)
                if (droppedFixtures[i] != null) droppedFixtures[i].SetActive(true);
            droppedFixtures.Clear();
            fixtures.Clear();
            if (body != null) body.enabled = true;
            if (bodyCollider != null) bodyCollider.enabled = true;
            health = MaxHealth;
            State = DestructionState.Intact;
            TintBody();
            GraphNode = -1;
            LastHit = default;
            LastHitTime = -1f;
            LastHitChunk = -1;
            armedAt = Time.time + ArmDelay;
        }
    }
}
