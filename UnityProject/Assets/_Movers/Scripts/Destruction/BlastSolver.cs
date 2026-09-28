using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Who a blast hurts, and how much. Explosion calls it; it keeps Explosion's order rules:
    //  1. Collect: everything breakable in reach (debris excluded), each at its nearest point,
    //     and what stands between it and the blast, all measured BEFORE anything breaks, so
    //     the rubble of the first wall cannot shield the second;
    //  2. Apply: nearest first, so a wall that gives way lets the blast through to what is
    //     behind it, and a wall that holds still protects the next room.
    //
    // What is new against the old Explosion.CollectTargets/DamageTargets:
    // - walls are measured chunk by chunk: a broken-up wall's chunks are separate targets, and
    //   an intact wall hit hard enough breaks up on the spot and each of its chunks is then
    //   measured from the blast (local damage: a hole where the grenade was);
    // - cover is graded: what gets through a wall depends on its material and on how broken it
    //   already is (DestructionMaterialTable.CoverOf), not one 0.35 for everything standing;
    // - each target gets its own DamageEvent, with the blast's instigator;
    // - the material's blast factor is applied by the receiver (damage is sent raw).
    //
    // DEV 2 (03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md section 4): every number comes from the
    // table; walls and structural pieces follow its structure curve (distance over the cube root
    // of the power); each chunk's event carries the speed it flies out at; the debris budget is
    // told how many chunks to expect before anything breaks.
    public static class BlastSolver
    {
        // Falloff is (1 - d/r) to this power (the table's blastExponent): a little steeper than
        // linear, so the edge of the blast is a nudge and the middle of it is the real thing.
        public static float Exponent => DestructionMaterialTable.Current.blastExponent;
        // Chunks announced for an intact wall the blast will break up: they do not exist until
        // it does. The ram announces as many for one wall hit.
        const int IntactWallChunkGuess = 8;
        // Small loose things this close to the centre never count as cover: they are the bomb
        // itself, or what it was lying in, and they are about to fly.
        const float LooseNearOrigin = 0.35f;
        const float LooseMass = 5f;
        // The cover rays start this far above the blast (see ShieldOrigin).
        const float ShieldRaise = 0.15f;
        const int MaxOverlap = 8192;

        // What stands between the blast and a point.
        public struct Occluder
        {
            public bool solid;                  // something that is not breakable: a floor, a fixture, a roof
            public IDamageable target;          // something breakable (null for a chunk)
            public DestructibleChunk chunk;     // a chunk of a broken-up wall
            public bool IsClear => !solid && target == null && chunk == null;
        }

        struct Target
        {
            public object key;                  // the IDamageable, or the chunk
            public IDamageable target;
            public DestructibleModule module;   // walls, broken up or not
            public int chunk;                   // -1: the whole thing
            public Vector3 point;               // nearest point to the blast, where the damage lands
            public Vector3 center;
            public float distance;
            public Occluder cover;
        }

        sealed class NearestFirst : IComparer<Target>
        {
            public int Compare(Target a, Target b) { return a.distance.CompareTo(b.distance); }
        }

        // The overlap buffer grows instead of dropping colliders: a full buffer loses them in no
        // particular order, and the one lost could be the wall. Past MaxOverlap it warns.
        static Collider[] overlap = new Collider[512];
        static readonly RaycastHit[] rayHits = new RaycastHit[32];
        static readonly List<Target> targets = new List<Target>(256);
        static readonly Dictionary<object, int> targetIndex = new Dictionary<object, int>(256);
        static readonly Dictionary<DestructibleModule, Occluder> moduleCover = new Dictionary<DestructibleModule, Occluder>(32);
        static readonly Dictionary<Collider, IDamageable> owners = new Dictionary<Collider, IDamageable>(1024);
        static readonly NearestFirst nearestFirst = new NearestFirst();
        static readonly List<int> chunkOrder = new List<int>(16);
        static float[] chunkDistance = new float[16];
        static Vector3[] chunkPoint = new Vector3[16];
        static readonly Vector3[] Axes =
            { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static bool warnedFull;

        public static int LastTargetCount { get; private set; }
        // Wall chunks the last blast announced to the debris budget (DebrisManager.ExpectStructure).
        public static int LastExpectedChunks { get; private set; }

        // The blast being applied, while Apply runs: a wall asks WithinBreach whether a chunk it
        // removes was close enough to the centre to come out as rubble (DEV 2, 3.7).
        public static bool Applying { get; private set; }
        static Vector3 applyCenter;
        static float applyBreach;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            targets.Clear();
            targetIndex.Clear();
            moduleCover.Clear();
            owners.Clear();
            warnedFull = false;
            LastTargetCount = 0;
            LastExpectedChunks = 0;
            Applying = false;
        }

        // The collider-to-breakable cache is per scene: HouseDestruction clears it when the
        // house goes (a reload) or is reset.
        public static void ForgetColliders() { owners.Clear(); }

        // ---- 1. what is in reach ----

        public static int Collect(Vector3 c, float radius)
        {
            targets.Clear();
            targetIndex.Clear();
            moduleCover.Clear();

            int n = Physics.OverlapSphereNonAlloc(c, radius, overlap, DestructionLayers.QueryMask, QueryTriggerInteraction.Ignore);
            while (n >= overlap.Length && overlap.Length < MaxOverlap)
            {
                overlap = new Collider[overlap.Length * 2];
                n = Physics.OverlapSphereNonAlloc(c, radius, overlap, DestructionLayers.QueryMask, QueryTriggerInteraction.Ignore);
            }
            if (n >= overlap.Length && !warnedFull)
            {
                warnedFull = true;
                Debug.LogWarning("BlastSolver: " + overlap.Length + " colliders in one blast, some were skipped.");
            }

            for (int i = 0; i < n; i++)
            {
                var col = overlap[i];
                overlap[i] = null;
                if (col == null || IsDebris(col)) continue;

                object key;
                IDamageable target;
                DestructibleModule module;
                int chunkIndex = -1;
                if (DestructibleModule.TryGetChunk(col, out DestructibleChunk chunk))
                {
                    if (!chunk.Attached || chunk.Owner == null || chunk.Owner.IsGone) continue;
                    key = chunk;
                    target = chunk.Owner;
                    module = chunk.Owner;
                    chunkIndex = chunk.Index;
                }
                else
                {
                    target = OwnerOf(col);
                    if (IsGone(target)) continue;
                    key = target;
                    module = target as DestructibleModule;
                    // A broken-up wall is measured through its chunks. A collider that still
                    // finds the wall as its owner is something it carries (a sash frame), and
                    // must not hit the wall a second time.
                    if (module != null && module.IsFractured) continue;
                }

                Vector3 p = ClosestPoint(col, c);
                float d = Vector3.Distance(p, c);

                // One object, several colliders: keep the nearest one.
                if (targetIndex.TryGetValue(key, out int known))
                {
                    var t = targets[known];
                    if (d < t.distance)
                    {
                        t.point = p;
                        t.center = col.bounds.center;
                        t.distance = d;
                        targets[known] = t;
                    }
                    continue;
                }
                targetIndex[key] = targets.Count;
                targets.Add(new Target
                {
                    key = key,
                    target = target,
                    module = module,
                    chunk = chunkIndex,
                    point = p,
                    center = col.bounds.center,
                    distance = d,
                });
            }

            targets.Sort(nearestFirst);
            targetIndex.Clear();
            for (int i = 0; i < targets.Count; i++) targetIndex[targets[i].key] = i;
            LastTargetCount = targets.Count;
            return targets.Count;
        }

        // What stands in front of every target, before anything breaks. One ray per wall, not
        // per chunk: its chunks share their wall's cover.
        public static void MeasureCover(Vector3 eye)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t.module != null && moduleCover.TryGetValue(t.module, out Occluder shared))
                    t.cover = shared;
                else
                {
                    t.cover = FindOccluder(eye, t.point, t.key, t.module, null);
                    if (t.module != null) moduleCover[t.module] = t.cover;
                }
                targets[i] = t;
            }
        }

        // ---- 2. damage, nearest first ----

        // Keeps room in the debris budget for the wall chunks this blast is about to break, so
        // none waits behind a shower of prop shards (DebrisManager.ExpectStructure). Called after
        // the cover is measured and before anything breaks: the chunks of broken-up walls in reach
        // are counted one by one, an intact wall that will break up is a guess.
        public static int ExpectChunks(float radius, float power)
        {
            var table = DestructionMaterialTable.Current;
            int cap = Mathf.Max(0, table.structureReservePerFrame);
            float baseDamage = power * table.blastDamage;
            int n = 0;
            for (int i = 0; i < targets.Count && n < cap; i++)
            {
                var t = targets[i];
                if (t.module == null || IsGone(t)) continue;
                float f = Falloff(t.distance, radius) * DestructionMaterialTable.Focus(t.distance, power);
                if (f <= 0f) continue;
                if (t.chunk >= 0) n++;
                else if (t.module.WouldFracture(baseDamage * f * CoverOf(t.cover), DamageType.Blast)) n += IntactWallChunkGuess;
            }
            n = Mathf.Min(n, cap);
            LastExpectedChunks = n;
            if (n > 0) DebrisManager.ExpectStructure(n);
            return n;
        }

        // True while a blast is being applied and 'point' (the nearest point of a chunk to it)
        // lies within the table's breachRadius of its centre, scaled with the power like the
        // structure curve. The wall reads it to cut such a chunk into rubble.
        public static bool WithinBreach(Vector3 point)
        {
            return Applying && (point - applyCenter).sqrMagnitude <= applyBreach * applyBreach;
        }

        public static void Apply(Vector3 c, float radius, float power, int instigator)
        {
            var table = DestructionMaterialTable.Current;
            float baseDamage = power * table.blastDamage;
            applyCenter = c;
            applyBreach = table.breachRadius * (table.focusScalesWithPower && power > 0f ? Mathf.Pow(power, 1f / 3f) : 1f);
            Applying = true;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (IsGone(t)) continue;   // taken out earlier in this same blast

                    float f = Falloff(t.distance, radius);
                    if (f <= 0f) continue;
                    float cover = CoverOf(t.cover);
                    // A wall, and anything else built into the house (a pillar, a corner, the
                    // chimney, a post), only feels the blast where it lands: the table's structure
                    // curve. Props keep the plain falloff.
                    bool built = t.module != null || (t.target is Breakable br && br.structural);
                    float focus = built ? DestructionMaterialTable.Focus(t.distance, power) : 1f;
                    float damage = baseDamage * f * cover * focus;
                    // How fast a chunk it breaks off flies out: hard at point blank, a hop further away.
                    float eject = table.blastChunkEjectSpeed * focus * cover;
                    var e = new DamageEvent(t.point, Away(c, t.point, t.center), damage,
                                            power * table.blastDamageImpulse * f * cover, radius, DamageType.Blast,
                                            instigator, null, eject);
                    // One object throwing must not cancel the rest of the blast.
                    try
                    {
                        if (t.module != null && t.chunk >= 0) t.module.ApplyToChunk(t.chunk, e);
                        else if (t.module != null && t.module.WouldFracture(damage, DamageType.Blast) && t.module.Fracture())
                            ApplyPerChunk(t.module, c, radius, baseDamage, power, cover, instigator);
                        else t.target.ApplyDamage(e);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            finally
            {
                Applying = false;
            }
        }

        // A wall that just broke up: every chunk measured from the blast, nearest first, each
        // with its own damage and its own eject speed. The nearest point of a chunk is found once
        // and kept: an attached chunk is a non-convex mesh, probed with rays (ClosestPoint).
        static void ApplyPerChunk(DestructibleModule module, Vector3 c, float radius, float baseDamage, float power,
                                  float cover, int instigator)
        {
            var table = DestructionMaterialTable.Current;
            var chunks = module.Chunks;
            chunkOrder.Clear();
            if (chunkDistance.Length < chunks.Count)
            {
                int size = Mathf.NextPowerOfTwo(chunks.Count);
                chunkDistance = new float[size];
                chunkPoint = new Vector3[size];
            }
            for (int i = 0; i < chunks.Count; i++)
            {
                var ch = chunks[i];
                Vector3 p = ch.Collider != null ? ClosestPoint(ch.Collider, c) : ch.Bounds.ClosestPoint(c);
                chunkPoint[i] = p;
                chunkDistance[i] = Vector3.Distance(p, c);
                int k = chunkOrder.Count;
                while (k > 0 && chunkDistance[chunkOrder[k - 1]] > chunkDistance[i]) k--;
                chunkOrder.Insert(k, i);
            }
            for (int o = 0; o < chunkOrder.Count; o++)
            {
                int i = chunkOrder[o];
                var ch = chunks[i];
                if (!ch.Attached || module.IsGone) continue;
                float d = chunkDistance[i];
                float focus = DestructionMaterialTable.Focus(d, power);
                float f = Falloff(d, radius) * focus;
                if (f <= 0f) continue;
                Vector3 p = chunkPoint[i];
                var e = new DamageEvent(p, Away(c, p, ch.Bounds.center), baseDamage * f * cover,
                                        power * table.blastDamageImpulse * f * cover, radius, DamageType.Blast,
                                        instigator, null, table.blastChunkEjectSpeed * focus * cover);
                module.ApplyToChunk(i, e);
            }
            chunkOrder.Clear();
        }

        // Cover only counts if it is still standing. Targets are damaged nearest first and
        // whatever is in front of a target is nearer than it, so by the time a target is hit its
        // cover has already taken its own share and either held, broke, or went.
        public static float CoverOf(in Occluder o)
        {
            if (o.solid) return DestructionMaterialTable.Current.solidCover;
            if (o.chunk != null)
            {
                if (!o.chunk.Attached || o.chunk.Owner == null) return 1f;
                return DestructionMaterialTable.CoverOf(o.chunk.Owner.Material, o.chunk.State);
            }
            if (o.target != null)
            {
                if (IsGone(o.target)) return 1f;
                return DestructionMaterialTable.CoverOf(o.target.Material, o.target.State);
            }
            return 1f;
        }

        // ---- cover rays ----

        // Where the cover rays start: a hand's width above the blast. A grenade lies on the
        // floor, and the nearest point of a sofa or a crate is at the same few centimetres. A
        // ray from one to the other skims the floor, and any lip on the way (a threshold, a
        // plinth, a rug, the seam between two floor pieces) would count as a wall and cut the
        // damage to a third, more or less at random. Raised, the ray clears them.
        // Not raised when something sits right overhead (under a table, against a ceiling): a
        // ray that starts inside a collider does not see it, so that cover would vanish.
        public static Vector3 ShieldOrigin(Vector3 c)
        {
            int n = Physics.RaycastNonAlloc(c, Vector3.up, rayHits, ShieldRaise + 0.05f, DestructionLayers.QueryMask,
                                            QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = rayHits[i];
                var col = h.collider;
                if (col == null || col is CharacterController || IsDebris(col)) continue;
                if (IsLooseNearOrigin(h)) continue;   // the grenade itself
                if (Classify(col).IsClear) continue;  // something already broken
                return c;
            }
            return c + Vector3.up * ShieldRaise;
        }

        // What the blast meets first on its way from 'from' to 'point'. selfKey and selfModule
        // are the target being measured (its own surface, and its own wall, are not cover);
        // selfRoot does the same job for a player.
        public static Occluder FindOccluder(Vector3 from, Vector3 point, object selfKey, DestructibleModule selfModule,
                                            Transform selfRoot)
        {
            Vector3 to = point - from;
            float dist = to.magnitude;
            if (dist < 0.05f) return default;   // the blast is on it

            int n = Physics.RaycastNonAlloc(from, to / dist, rayHits, dist, DestructionLayers.QueryMask,
                                            QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Occluder result = default;
            for (int i = 0; i < n; i++)
            {
                var h = rayHits[i];
                var col = h.collider;
                if (col == null || h.distance >= nearest) continue;
                if (IsLooseNearOrigin(h) || IsDebris(col)) continue;

                if (selfRoot != null && (col.transform == selfRoot || col.transform.IsChildOf(selfRoot)))
                {
                    nearest = h.distance;
                    result = default;
                    continue;
                }
                // A crewmate is not a wall: someone standing between the grenade and a window must
                // not save the window, or another crewmate.
                if (col is CharacterController) continue;

                Occluder o = Classify(col);
                if (o.IsClear) continue;   // a collider left behind by something already broken
                nearest = h.distance;
                bool self = (selfKey != null && (ReferenceEquals(o.chunk, selfKey) || ReferenceEquals(o.target, selfKey)))
                            || (selfModule != null && (ReferenceEquals(o.target, selfModule)
                                                       || (o.chunk != null && o.chunk.Owner == selfModule)));
                result = self ? default : o;
            }
            return result;
        }

        // Loose things never shield each other, they all fly together. Only what is built in
        // (and still standing) does: a wall that held, a floor, the chimney.
        public static bool IsBuildingInTheWay(Vector3 c, Vector3 com, Rigidbody rb)
        {
            Vector3 to = com - c;
            float dist = to.magnitude;
            if (dist < 0.05f) return false;

            int n = Physics.RaycastNonAlloc(c, to / dist, rayHits, dist, DestructionLayers.QueryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = rayHits[i];
                var col = h.collider;
                if (col == null) continue;
                var hb = h.rigidbody;
                if (hb == rb) continue;
                if (hb != null && !hb.isKinematic) continue;
                if (col is CharacterController || IsDebris(col)) continue;   // a crewmate is not a wall
                if (Classify(col).IsClear) continue;
                return true;
            }
            return false;
        }

        // A collider as cover: breakable (and how broken), a chunk, or plain solid. Clear when it
        // belongs to something already gone.
        static Occluder Classify(Collider col)
        {
            if (DestructibleModule.TryGetChunk(col, out DestructibleChunk chunk))
                return chunk.Attached ? new Occluder { chunk = chunk } : default;
            IDamageable d = OwnerOf(col);
            if (d == null) return new Occluder { solid = true };
            if (IsGone(d)) return default;
            return new Occluder { target = d };
        }

        static IDamageable OwnerOf(Collider col)
        {
            if (owners.TryGetValue(col, out IDamageable d))
            {
                if (d is Object o && o == null) { owners.Remove(col); d = null; }
                else return d;
            }
            d = col.GetComponentInParent<IDamageable>();
            owners[col] = d;
            return d;
        }

        static bool IsGone(IDamageable d)
        {
            return d == null || (d is Object o && o == null) || d.IsGone;
        }

        static bool IsGone(in Target t)
        {
            if (t.chunk >= 0) return !(t.key is DestructibleChunk ch) || !ch.Attached || t.module == null || t.module.IsGone;
            return IsGone(t.target);
        }

        // With the Debris layer the queries never see debris. Without it (a scene opened before
        // the layers were added), debris is recognised by its marker.
        static bool IsDebris(Collider col)
        {
            if (DestructionLayers.Debris >= 0) return false;
            if (col.TryGetComponent(out DebrisPiece _)) return true;
            var body = col.attachedRigidbody;
            return body != null && body.TryGetComponent(out DebrisPiece _);
        }

        static bool IsLooseNearOrigin(RaycastHit h)
        {
            var rb = h.rigidbody;
            return h.distance < LooseNearOrigin && rb != null && !rb.isKinematic && rb.mass < LooseMass;
        }

        // ---- geometry ----

        public static float Falloff(float d, float range)
        {
            if (!(range > 0f)) return 0f;
            return Mathf.Pow(Mathf.Clamp01(1f - d / range), Exponent);
        }

        public static Vector3 Away(Vector3 c, Vector3 point, Vector3 center)
        {
            Vector3 v = point - c;
            if (v.sqrMagnitude < 0.0004f) v = center - c;
            if (v.sqrMagnitude < 0.0004f) return Vector3.up;
            return v.normalized;
        }

        // Nearest point of a collider to the blast. Exact where PhysX can answer (box, sphere,
        // capsule, convex mesh: every chunk); the house itself is built from non-convex meshes,
        // which it cannot, and their bounding box is a poor stand-in for anything that is not a
        // flat wall (a sloped roof's box reaches down to the eaves and would put the roof "at" a
        // blast in the room below). So those are probed with a few rays against that one collider.
        public static Vector3 ClosestPoint(Collider col, Vector3 p)
        {
            if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
                return col.ClosestPoint(p);
            var mc = col as MeshCollider;
            if (mc != null)
            {
                if (mc.convex) return col.ClosestPoint(p);
                return ProbeSurface(mc, p);
            }
            return col.bounds.ClosestPoint(p);
        }

        // Rays toward the nearest point of the box, toward its centre and along the six axes
        // (the kit is built square to the world, so one of those is almost always the
        // perpendicular to the wall face). The nearest hit wins; with no hit at all, the box.
        static Vector3 ProbeSurface(MeshCollider mc, Vector3 p)
        {
            Bounds b = mc.bounds;
            Vector3 onBox = b.ClosestPoint(p);
            float reach = Vector3.Distance(p, b.center) + b.extents.magnitude + 0.1f;
            float best = float.MaxValue;
            Vector3 point = onBox;

            Probe(mc, p, onBox - p, reach, ref best, ref point);
            Probe(mc, p, b.center - p, reach, ref best, ref point);
            for (int i = 0; i < Axes.Length; i++) Probe(mc, p, Axes[i], reach, ref best, ref point);
            return point;
        }

        static void Probe(MeshCollider mc, Vector3 p, Vector3 dir, float reach, ref float best, ref Vector3 point)
        {
            if (dir.sqrMagnitude < 1e-6f) return;
            if (mc.Raycast(new Ray(p, dir.normalized), out RaycastHit hit, reach) && hit.distance < best)
            {
                best = hit.distance;
                point = hit.point;
            }
        }
    }
}
