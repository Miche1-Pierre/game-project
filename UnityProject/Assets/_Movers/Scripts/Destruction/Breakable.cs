using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Anything that breaks whole: a vase, a chair, a fence, a door leaf, and a wall that has
    // no pre-fractured chunks (walls that have them are DestructibleModules).
    //
    // One health model for all of it. A hit only hurts above a speed set by the material (a
    // plate minds a 3 m/s knock, a wall does not care below 8), and past that the damage grows
    // with the square of the extra speed, scaled by how heavy the other thing is compared to
    // this one (ImpactDamage). At half health it is marked broken (a movable pays half, the
    // existing rule) and goes darker so you can see it; at zero it shatters into physical debris
    // and switches off. It is deactivated, never destroyed, so the contract list and the truck
    // keep valid references and can say "destroyed" instead of throwing.
    //
    // Something being carried is cushioned by the hands holding it (see ImpactDamage.Held):
    // a clumsy carry costs you, a graze does not.
    //
    // stateCap stops it partway down the ladder: the foundation (cellar walls) takes damage and
    // shows it, and never goes past Damaged (ADR-009).
    //
    // Disable the component to make one object indestructible: it then ignores hits and
    // damage, though Shatter() called directly still works. Revive() undoes all of it for one
    // object and ReviveAll() for the whole scene, for the debug reset (F11, DestructionDebug).
    [DisallowMultipleComponent]
    public class Breakable : MonoBehaviour, IDamageable, IStructurePart
    {
        public BreakMaterial material = BreakMaterial.Wood;
        [Tooltip("0 = derived from the material, and from the mass for movables.")]
        public float maxHealth = 0f;
        [Tooltip("A static piece of the house rather than a movable object.")]
        public bool structural = false;
        [Tooltip("Stands in for this object's mass in the impact formula when it has no dynamic Rigidbody (house pieces).")]
        public float referenceMass = 100f;
        [Tooltip("How far down the damage ladder it may go. Damaged = the foundation: it never breaks.")]
        public DestructionState stateCap = DestructionState.Destroyed;
        [Tooltip("A garden piece (hedge, bush, mailbox, post): its destruction is garden damage (DestructionEvents.Garden), never a wall.")]
        public bool isYard;

        public bool IsDestroyed { get; private set; }
        public float Health { get { Init(); return health; } private set { health = value; } }
        public float MaxHealth { get { Init(); return resolvedMax; } }
        public event System.Action<Breakable> Destroyed;

        public BreakMaterial Material => material;
        public DestructionState State => IsDestroyed ? DestructionState.Destroyed
                                       : markedBroken ? DestructionState.Damaged : DestructionState.Intact;
        public bool IsGone => IsDestroyed || !gameObject.activeInHierarchy;
        public Bounds WorldBounds
        {
            get
            {
                GatherOwnRenderers(true);
                Bounds b = ownRenderers.Count > 0 ? ownRenderers[0].bounds : new Bounds(transform.position, Vector3.zero);
                for (int i = 1; i < ownRenderers.Count; i++) b.Encapsulate(ownRenderers[i].bounds);
                ownRenderers.Clear();
                return b;
            }
        }

        // The last hit that landed, for the debug overlay.
        public DamageEvent LastHit { get; private set; }
        public float LastHitTime { get; private set; } = -1f;
        // Its node in the StructureGraph, for a structural piece; -1 otherwise.
        public int GraphNode { get; internal set; } = -1;

        const float HitCooldown = 0.1f;      // one damage event per 0.1 s: a crash is one hit, not five contacts
        const float ArmDelay = 1f;           // objects settling at load time do not count as hits
        const float BrokenTint = 0.55f;
        const float StructuralFill = 0.15f;  // a wall's bounding box is mostly not wall

        MovableObject mo;
        Rigidbody rb;
        bool initialized;
        bool markedBroken;
        float health;
        float resolvedMax;
        float nextHitTime;
        float armedAt;

        static readonly List<MeshRenderer> meshRenderers = new List<MeshRenderer>(32);
        static readonly List<Renderer> ownRenderers = new List<Renderer>(32);
        static MaterialPropertyBlock tintBlock;
        static int colorId = -1;

        // ---- material tables (the data is in DestructionMaterialTable) ----

        // Below this relative speed (m/s) a hit does no damage at all.
        public static float MinImpactSpeed(BreakMaterial m) => DestructionMaterialTable.Get(m).minImpactSpeed;
        // Health before the mass scaling that movables get.
        public static float DefaultHealth(BreakMaterial m) => DestructionMaterialTable.Get(m).durability;
        // kg per cubic metre, only used to give house pieces a plausible debris mass.
        public static float Density(BreakMaterial m) => DestructionMaterialTable.Get(m).density;
        public static ImpactAudio.Kind AudioFor(BreakMaterial m) => DestructionMaterialTable.Get(m).sound;

        // ---- lifecycle ----

        void Awake()
        {
            CacheRefs();
            armedAt = Time.time + ArmDelay;
        }

        void Start() { Init(); }

        void CacheRefs()
        {
            if (mo == null) mo = GetComponent<MovableObject>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        // Health is resolved on first use rather than in Awake: HouseDestruction adds this
        // component and only then sets the material, and AddComponent runs Awake before that.
        void Init()
        {
            if (initialized) return;
            initialized = true;
            CacheRefs();
            resolvedMax = ComputeMaxHealth();
            health = resolvedMax;
        }

        // Sets everything at once and refills health. Meant for setup code right after
        // AddComponent; calling it on a damaged object repairs it.
        public void Configure(BreakMaterial mat, float health, bool isStructural)
        {
            material = mat;
            maxHealth = health;
            structural = isStructural;
            initialized = false;
            markedBroken = false;
            Init();
        }

        float ComputeMaxHealth()
        {
            if (maxHealth > 0f) return maxHealth;
            float h = DefaultHealth(material);
            if (!structural)
            {
                // A heavy wardrobe survives a knock that ends a light one. MovableObject.weight
                // is read rather than the body mass because this can run before its Awake.
                float mass = mo != null ? Mathf.Max(0.1f, mo.weight) : (rb != null ? rb.mass : 0f);
                if (mass > 0f) h *= Mathf.Clamp(0.6f + mass / 40f, 0.6f, 3f);
            }
            return h;
        }

        float MyMass => rb != null && !rb.isKinematic ? rb.mass : referenceMass;
        Rigidbody MyBody => rb != null && !rb.isKinematic ? rb : null;

        // ---- damage ----

        void OnCollisionEnter(Collision c)
        {
            if (!Net.HasAuthority) return;   // online, things break on the host (Props Breakable)
            // Unity sends collision messages to disabled components too: disabled means off.
            if (!enabled || IsDestroyed) return;
            float now = Time.time;
            if (now < armedAt || now < nextHitTime) return;

            Init();
            if (!ImpactDamage.TryMeasure(c, material, MyMass, ImpactDamage.Plain, transform.position, MyBody,
                                         out DamageEvent e))
                return;
            // Only a hit that would hurt pays for the "is somebody carrying me" lookup.
            if (IsHeld() && !ImpactDamage.TryMeasure(c, material, MyMass, ImpactDamage.Held, transform.position, MyBody,
                                                     out e))
                return;

            nextHitTime = now + HitCooldown;
            ApplyDamage(e);
        }

        // Kept for older callers: a plain impact, nobody to blame.
        public void ApplyDamage(float amount, Vector3 point, Vector3 impulse)
        {
            ApplyDamage(new DamageEvent(point, impulse, amount, impulse.magnitude, 0f, DamageType.Impact));
        }

        public DamageResult ApplyDamage(in DamageEvent e)
        {
            var before = State;
            if (!Net.HasAuthority) return DamageResult.None(before);
            if (IsDestroyed || !enabled || !(e.damage > 0f)) return DamageResult.None(before);
            if (IsWorn()) return DamageResult.None(before);
            Init();
            float applied = e.damage * DestructionMaterialTable.Factor(e.type, material);
            if (!(applied > 0f)) return DamageResult.None(before);
            LastHit = e;
            LastHitTime = Time.time;

            health -= applied;
            if (stateCap < DestructionState.Destroyed) health = Mathf.Max(health, 1f);
            if (!markedBroken && health < resolvedMax * 0.5f) MarkBroken(e.instigator);
            if (health <= 0f) Shatter(e);
            return new DamageResult { applied = applied, before = before, after = State, removed = IsDestroyed };
        }

        // Half health: a movable still counts and pays half; anything shows it.
        void MarkBroken(int instigator)
        {
            markedBroken = true;
            Tint(BrokenTint);
            if (Net.IsHost) PropsSync.BreakableState(this, DestructionState.Damaged, default, Vector3.zero);
            if (mo != null)
            {
                mo.broken = true;
                DestructionEvents.PropDamaged(mo, resolvedMax > 0f ? Mathf.Clamp01(health / resolvedMax) : 0f, instigator);
            }
            else if (structural)
            {
                DestructionEvents.Structure(this, transform.position, DestructionState.Damaged, instigator);
            }
        }

        // Multiplies the colour of every material of this object's own renderers, through a
        // property block so the shared materials (and every other vase) stay untouched.
        // 1 puts the material's own colour back.
        void Tint(float factor)
        {
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
            if (colorId < 0) colorId = Shader.PropertyToID("_Color");

            GatherOwnRenderers(true);
            for (int i = 0; i < ownRenderers.Count; i++)
            {
                Renderer r = ownRenderers[i];
                Material[] shared = r.sharedMaterials;
                for (int m = 0; m < shared.Length; m++)
                {
                    Material mat = shared[m];
                    if (mat == null || !mat.HasProperty(colorId)) continue;
                    Color c = mat.GetColor(colorId);
                    r.GetPropertyBlock(tintBlock, m);
                    tintBlock.SetColor(colorId, new Color(c.r * factor, c.g * factor, c.b * factor, c.a));
                    r.SetPropertyBlock(tintBlock, m);
                }
            }
            ownRenderers.Clear();
        }

        // A worn piece (dressing gown, slippers) is part of a body until it comes off: CrewEquip
        // makes it kinematic and parents it to a bone. Letting a blast shatter it there would
        // deactivate something CrewEquip still lists as worn. Its colliders are off while worn,
        // so only a direct ApplyDamage can reach it; this is the guard for that.
        bool IsWorn()
        {
            if (mo == null) return false;
            if (mo.worn) return true;
            return rb != null && rb.isKinematic
                && TryGetComponent(out EquipItem _) && GetComponentInParent<CrewEquip>(true) != null;
        }

        public void Shatter(Vector3 point, Vector3 impulse)
        {
            Shatter(new DamageEvent(point, impulse, 0f, impulse.magnitude, 0f, DamageType.Impact));
        }

        public void Shatter(in DamageEvent e)
        {
            if (IsDestroyed) return;
            // The foundation holds, whatever asks (ADR-009).
            if (stateCap < DestructionState.Destroyed) return;
            Init();
            IsDestroyed = true;
            health = 0f;
            Vector3 point = e.position, impulse = e.ImpulseVector;

            // Children first: a wall takes its door and its windows with it, and each of those
            // breaks with its own material, its own debris and its own sound. Arrays, not the
            // shared buffers, because each child's Shatter uses those buffers itself.
            Breakable[] children = GetComponentsInChildren<Breakable>(false);
            for (int i = 0; i < children.Length; i++)
                if (children[i] != null && children[i] != this) children[i].Shatter(e);
            GlassPane[] panes = GetComponentsInChildren<GlassPane>(false);
            for (int i = 0; i < panes.Length; i++)
                if (panes[i] != null) panes[i].Shatter(e);

            // Online: after the children's own records, before the debris, with the velocity the
            // debris inherits (the client's replica body is kinematic and has none).
            if (Net.IsHost)
                PropsSync.BreakableState(this, DestructionState.Destroyed, e,
                                         !structural && rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero);

            Vector3 at = transform.position;
            var table = DestructionMaterialTable.Current;
            GatherOwnRenderers();
            if (ownRenderers.Count > 0)
            {
                Bounds b = ownRenderers[0].bounds;
                for (int i = 1; i < ownRenderers.Count; i++) b.Encapsulate(ownRenderers[i].bounds);
                at = b.center;

                // Old debris frozen on top of this (a sill, a fence rail) would be left hanging
                // in the air once this is gone. Wake it first so it falls with the rest.
                DebrisManager.WakeInBounds(b);

                int pieces = structural ? StructuralPieces(b) : MovablePieces(b);
                float mass = TotalMass(b);
                // House pieces were standing still; a thrown chair keeps flying as a cloud of chair.
                Vector3 inherit = !structural && rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
                MeshShatter.Shatter(ownRenderers, pieces, mass, inherit, point, impulse,
                                    structural ? table.structureDebrisLifetime : table.propDebrisLifetime, e.instigator);

                float volume = structural ? 1f : Mathf.Clamp(0.35f + b.size.magnitude * 0.35f, 0.35f, 1f);
                ImpactAudio.Play(AudioFor(material), b.center, volume, e.instigator);
                if (structural) DestructionFX.Dust(b.center, b.extents.magnitude);
            }
            ownRenderers.Clear();

            if (mo != null)
            {
                mo.destroyed = true;
                mo.loaded = false;
                mo.broken = true;
                if (Net.HasAuthority) LetGo(mo);
            }

            gameObject.SetActive(false);

            if (mo != null) DestructionEvents.PropDestroyed(mo, e.instigator);
            else if (structural)
            {
                DestructionEvents.Structure(this, at, DestructionState.Destroyed, e.instigator);
                if (DestructionEvents.IsDoor(name)) DestructionEvents.Door(this, at, e.instigator);
            }
            if (GraphNode >= 0 && Net.HasAuthority) StructureGraph.Current?.MarkRemoved(GraphNode, e.instigator);
            Destroyed?.Invoke(this);
        }

        // ---- online client ----

        // The host's transition (Props Breakable), applied without raising anything and without
        // a sound (the host's arrives as Props Sound). Damaged marks it; Destroyed breaks it
        // with the host's hit and inherited velocity (inherit), unless silent (the join
        // snapshot), where it is simply switched off. Never recurses: the children's own
        // records arrive first. Who held it is let go by the replicated item flags.
        public void NetApply(DestructionState state, in DamageEvent e, Vector3 inherit, bool silent)
        {
            if (IsDestroyed) return;
            Init();
            if (state == DestructionState.Damaged)
            {
                if (markedBroken) return;
                markedBroken = true;
                Tint(BrokenTint);
                if (mo != null) mo.broken = true;
                return;
            }
            if (state != DestructionState.Destroyed) return;
            IsDestroyed = true;
            health = 0f;

            if (!silent)
            {
                var table = DestructionMaterialTable.Current;
                GatherOwnRenderers();
                if (ownRenderers.Count > 0)
                {
                    Bounds b = ownRenderers[0].bounds;
                    for (int i = 1; i < ownRenderers.Count; i++) b.Encapsulate(ownRenderers[i].bounds);
                    DebrisManager.WakeInBounds(b);
                    int pieces = structural ? StructuralPieces(b) : MovablePieces(b);
                    MeshShatter.Shatter(ownRenderers, pieces, TotalMass(b), inherit, e.position, e.ImpulseVector,
                                        structural ? table.structureDebrisLifetime : table.propDebrisLifetime, e.instigator);
                    if (structural) DestructionFX.Dust(b.center, b.extents.magnitude);
                }
                ownRenderers.Clear();
            }

            if (mo != null)
            {
                mo.destroyed = true;
                mo.loaded = false;
                mo.broken = true;
            }
            gameObject.SetActive(false);
        }

        // ---- the structure graph ----

        // A structural piece that lost what held it up comes down in pieces.
        float IStructurePart.Release(int node, int part, in DamageEvent cause)
        {
            if (IsDestroyed || stateCap < DestructionState.Destroyed) return 0f;
            float kg = TotalMass(WorldBounds);
            Shatter(cause);
            return IsDestroyed ? kg : 0f;
        }

        bool IStructurePart.SplitForSupport(int node) => false;
        string IStructurePart.Describe(int part) => name + " (" + material + ", " + Health.ToString("0") + "/" + MaxHealth.ToString("0") + ")";

        // ---- revive (debug reset) ----

        // Undoes everything breaking did to this object: full health, not broken, colour back,
        // active again. Children that went down with it come back with it, panes included.
        // The debris already thrown is not collected; it ages out on its own. Position is not
        // touched: putting a movable back where it started is the caller's job.
        public void Revive()
        {
            // Root first (the array is in hierarchy order), so each child is switched on under
            // a parent that is already active again.
            Breakable[] all = GetComponentsInChildren<Breakable>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) all[i].ReviveSelf();
            GlassPane[] panes = GetComponentsInChildren<GlassPane>(true);
            for (int i = 0; i < panes.Length; i++)
                if (panes[i] != null) panes[i].Revive();
        }

        // Every Breakable and every GlassPane in the loaded scenes, inactive ones included,
        // back to how the scene loaded (see Revive). The destruction reset (F11) calls it
        // before it puts the destroyed movables back in place. Returns how many Breakables it
        // had to repair, for a log line.
        public static int ReviveAll()
        {
            int repaired = 0;
            Breakable[] all = Object.FindObjectsByType<Breakable>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Breakable b = all[i];
                if (b == null) continue;
                if (b.IsDestroyed || b.markedBroken || (b.initialized && b.health < b.resolvedMax)) repaired++;
                b.ReviveSelf();
            }
            GlassPane[] panes = Object.FindObjectsByType<GlassPane>(FindObjectsInactive.Include);
            for (int i = 0; i < panes.Length; i++)
                if (panes[i] != null) panes[i].Revive();
            return repaired;
        }

        void ReviveSelf()
        {
            CacheRefs();
            bool wasDestroyed = IsDestroyed;
            bool wasTinted = markedBroken;
            IsDestroyed = false;
            markedBroken = false;
            initialized = false;
            Init();
            nextHitTime = 0f;
            // Put back at its start pose, it may overlap what it lands on for a frame.
            armedAt = Time.time + ArmDelay;
            if (mo != null)
            {
                mo.destroyed = false;
                mo.broken = false;
            }
            // Only what breaking switched off is switched back on: an object that is inactive
            // for another reason (in a pocket, say) stays where it is.
            if (wasDestroyed) gameObject.SetActive(true);
            if (wasTinted) Tint(1f);
        }

        // ---- helpers ----

        // This object's own visible meshes: children that are Breakables, GlassPanes or wall
        // modules of their own are left out, they shatter (or already did) on their own.
        // Hidden ones are only wanted for the tint, which must also reach a piece switched off
        // for now.
        void GatherOwnRenderers(bool includeInactive = false)
        {
            ownRenderers.Clear();
            GetComponentsInChildren(includeInactive, meshRenderers);
            for (int i = 0; i < meshRenderers.Count; i++)
            {
                MeshRenderer mr = meshRenderers[i];
                if (mr == null || (!includeInactive && !mr.enabled)) continue;
                if (!mr.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
                if (!OwnedByThis(mr.transform)) continue;
                ownRenderers.Add(mr);
            }
            meshRenderers.Clear();
        }

        bool OwnedByThis(Transform t)
        {
            Transform me = transform;
            for (; t != null && t != me; t = t.parent)
                if (t.TryGetComponent(out Breakable _) || t.TryGetComponent(out GlassPane _)
                    || t.TryGetComponent(out DestructibleModule _))
                    return false;
            return t == me;
        }

        // About 18 pieces per 9 m2 of the largest face: a full wall module gives ~19,
        // a fence post the minimum.
        static int StructuralPieces(Bounds b)
        {
            Vector3 s = b.size;
            float face = Mathf.Max(s.x * s.y, Mathf.Max(s.y * s.z, s.x * s.z));
            return Mathf.Clamp(Mathf.RoundToInt(18f * face / 9f), 6, 28);
        }

        // A cup gives 5 or 6 pieces, a wardrobe 14.
        static int MovablePieces(Bounds b)
        {
            return Mathf.Clamp(Mathf.RoundToInt(4f + 4f * b.size.magnitude), 4, 14);
        }

        float TotalMass(Bounds b)
        {
            if (!structural) return mo != null ? Mathf.Max(0.1f, mo.weight) : (rb != null ? rb.mass : 5f);
            if (rb != null && !rb.isKinematic) return rb.mass;
            Vector3 s = b.size;
            return Mathf.Clamp(s.x * s.y * s.z * Density(material) * StructuralFill, 10f, 800f);
        }

        // True while a player has this object in their hands. The holder is written by the
        // grab itself (MovableObject.holder); the crew is asked only if it was never set.
        bool IsHeld()
        {
            if (mo == null) return false;
            var h = mo.holder;
            if (h != null) return h.Held == mo;
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != null && crew[i].Held == mo) return true;
            return false;
        }

        // Whoever is carrying it is now carrying nothing. Without this the grab would keep
        // steering a switched-off rigidbody and the player would hold an invisible object.
        static void LetGo(MovableObject target)
        {
            var h = target.holder;
            if (h != null && h.Held == target) h.Release(false);
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                var g = crew[i] != null ? crew[i].Grab : null;
                if (g != null && g.Held == target) g.Release(false);
            }
        }
    }
}
