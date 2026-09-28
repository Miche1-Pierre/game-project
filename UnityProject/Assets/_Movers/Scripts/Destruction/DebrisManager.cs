using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Movers
{
    // Owns every piece of debris in the scene: how long it lives and how many exist at once.
    //
    // Smashing things is the fun part, but every fragment is a rigidbody, and a grenade in the
    // cellar used to spawn hundreds of them in one frame (117 ms, 1792 pieces at the peak,
    // ADR-008). So the pile is bounded, with every number in DestructionMaterialTable's Debris
    // block (DEV 2 section 5):
    // - at spawn: at most maxSpawnPerFrame new pieces a frame (TryReserve). Whoever breaks
    //   something asks first and gets fewer pieces, or none, when the frame is full. A blast or a
    //   ram that is about to break wall chunks says so first (ExpectStructure): that many slots
    //   (up to structureReservePerFrame) are then kept for the chunks (TryReserveStructure), so
    //   a chunk never waits behind vases. In a frame nobody announced chunks, props get it all;
    // - each piece has a lifetime (structure 60 s, furniture 40 s, glass 20 s, each give or take
    //   lifetimeJitter so a burst does not leave all at once). Expired, it leaves only when no
    //   crew camera sees it or every crew eye is farther than cleanupMinDistance, shrinking to
    //   nothing; at hardLifetimeFactor lifetimes it shrinks regardless. QA saw whole piles pop
    //   out in view: that is what this is for;
    // - at most maxDynamicPieces moving and maxFrozenPieces frozen pieces: past a cap the ones
    //   no crew camera sees go first, then the smallest and farthest, shrinking fast;
    // - at most destroyPerFrame pieces destroyed per frame: the rest wait shrunk and inactive,
    //   so a wall's worth of rubble never costs one frame;
    // - a piece that has been asleep for a few seconds turns kinematic, which takes it out of
    //   the solver entirely, and small or far frozen pieces stop casting shadows.
    //
    // Kinematic means immovable, so freezing is careful about what it leaves behind:
    // - only a piece lying on static ground (a floor, the terrain) or on a frozen piece freezes.
    //   On a table, a sofa or a door it stays a sleeping rigidbody, because that support can be
    //   carried off or swung open, and a frozen piece would stay hanging in the air;
    // - a frozen piece thaws when something moving bumps into it (DebrisPiece), when what it
    //   rests on breaks (WakeInBounds, called by Breakable and GlassPane) or leaves (a frozen
    //   piece under it shrinking away), or when an explosion goes off nearby.
    //
    // Pinned pieces (fallen roof sections) are never aged out or culled, and stay off the Debris layer.
    //
    // Created on demand as a root object named "Debris" the first time something shatters.
    // Debris is local to each machine, online too; the client never freezes (NETCODE_SLICE 11.4).
    [DisallowMultipleComponent]
    public class DebrisManager : MonoBehaviour
    {
        const float GroundProbe = 0.15f;   // how far under its own collider a piece looks for the ground
        const float WakeMargin = 0.5f;     // a piece this close to a breaking object may be resting on it
        const float CullDistance = 10f;    // m: at this distance a piece counts half as visible
        const float InViewScore = 1e6f;    // a piece a crew camera sees is culled after every unseen one
        const float ShadowCheckEvery = 1f; // s between checks of the frozen pieces' shadow rules
        const int MaxViews = 4;

        static DebrisManager instance;
        static bool quitting;
        // Lifetime jitter: its own generator, so debris never moves the gameplay's Random.
        static System.Random jitterRng = new System.Random(12345);

        // In spawn order. The cull picks by visibility, not by position in the list.
        readonly List<DebrisPiece> pieces = new List<DebrisPiece>(768);
        // Cull scratch, reused.
        float[] cullScore = new float[512];
        int[] cullIndex = new int[512];
        readonly List<Vector3> eyes = new List<Vector3>(4);
        readonly Plane[][] viewPlanes = new Plane[MaxViews][];
        int viewCount;
        int viewsFrame = -1;

        // The spawn budget of the current frame (see Roll).
        int budgetFrame = -1;
        int spawnedThisFrame;
        int structureSpawnedThisFrame;
        int structureReserve;
        int releasedThisFrame;
        float nextShadowCheck;

        public int Count => pieces.Count;
        public int DynamicCount { get; private set; }
        public int FrozenCount { get; private set; }
        public int SpawnedThisFrame => budgetFrame == Time.frameCount ? spawnedThisFrame : 0;
        public int SpawnedLastFrame { get; private set; }
        public int ReleasedLastFrame { get; private set; }
        public int StructureReserveThisFrame => budgetFrame == Time.frameCount ? structureReserve : 0;
        // The dynamic cap, as the debug overlay has always shown it.
        public int maxPieces => Mathf.Max(0, Table.maxDynamicPieces);

        static DestructionMaterialTable Table => DestructionMaterialTable.Current;

        // The manager if one exists; never creates it (for the overlay and the tests).
        public static DebrisManager Existing => instance;

        public static DebrisManager Instance
        {
            get
            {
                if (instance != null) return instance;
                // Never build a new root while the game is closing: Unity would report it as
                // an object left behind by the scene.
                if (quitting) return null;
                instance = Object.FindAnyObjectByType<DebrisManager>();
                if (instance == null) instance = new GameObject("Debris").AddComponent<DebrisManager>();
                return instance;
            }
        }

        // The playtest setup can keep statics alive between play sessions (domain reload off),
        // so the flags are reset explicitly rather than trusted.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            quitting = false;
            jitterRng = new System.Random(12345);
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnQuitting() { quitting = true; }

        void Awake()
        {
            if (instance == null) instance = this;
            else if (instance != this) { Destroy(this); return; }
            for (int i = 0; i < MaxViews; i++) viewPlanes[i] = new Plane[6];
        }

        void OnEnable() { Explosion.Detonated += OnDetonated; }
        void OnDisable() { Explosion.Detonated -= OnDetonated; }

        void OnDestroy()
        {
            Explosion.Detonated -= OnDetonated;
            if (instance == this) instance = null;
        }

        // ---- the spawn budget ----

        // The counters belong to one frame; the first call of a new frame starts them over.
        void Roll()
        {
            int frame = Time.frameCount;
            if (budgetFrame == frame) return;
            SpawnedLastFrame = budgetFrame == frame - 1 ? spawnedThisFrame : 0;
            budgetFrame = frame;
            spawnedThisFrame = 0;
            structureSpawnedThisFrame = 0;
            structureReserve = 0;
        }

        // Asks for room for 'wanted' new pieces this frame. granted may be fewer, or 0. Props and
        // glass come here: they never take the slots kept for announced wall chunks.
        public bool TryReserve(int wanted, out int granted)
        {
            Roll();
            int kept = Mathf.Max(0, structureReserve - structureSpawnedThisFrame);
            int room = Mathf.Max(0, Table.maxSpawnPerFrame) - spawnedThisFrame - kept;
            granted = Mathf.Clamp(room, 0, Mathf.Max(0, wanted));
            spawnedThisFrame += granted;
            return granted > 0;
        }

        // A blast or a ram is about to break about this many wall chunks this frame: part of the
        // spawn budget is kept for them (structureReservePerFrame at most, summed over the
        // frame's calls), so a chunk never waits behind props. Nothing is kept in a frame nobody
        // announced chunks.
        public static void ExpectStructure(int chunks)
        {
            if (chunks <= 0) return;
            var m = Instance;
            if (m == null) return;
            m.Roll();
            var t = Table;
            int cap = Mathf.Min(Mathf.Max(0, t.structureReservePerFrame), Mathf.Max(0, t.maxSpawnPerFrame));
            m.structureReserve = Mathf.Min(cap, m.structureReserve + chunks);
        }

        // TryReserve for a wall chunk (or its rubble): may take the slots ExpectStructure kept,
        // and any free slot besides.
        public bool TryReserveStructure(int wanted, out int granted)
        {
            Roll();
            int room = Mathf.Max(0, Table.maxSpawnPerFrame) - spawnedThisFrame;
            granted = Mathf.Clamp(room, 0, Mathf.Max(0, wanted));
            spawnedThisFrame += granted;
            structureSpawnedThisFrame += granted;
            return granted > 0;
        }

        // Gives back slots reserved this frame and not used (a shatter that built fewer pieces).
        internal void Refund(int count, bool structure)
        {
            if (count <= 0 || budgetFrame != Time.frameCount) return;
            spawnedThisFrame = Mathf.Max(0, spawnedThisFrame - count);
            if (structure) structureSpawnedThisFrame = Mathf.Max(0, structureSpawnedThisFrame - count);
        }

        // The same, for callers that have no manager yet: builds it on first use. 0 while the
        // game is closing.
        public static int Reserve(int wanted)
        {
            var m = Instance;
            if (m == null) return 0;
            m.TryReserve(wanted, out int granted);
            return granted;
        }

        // The end of a piece.
        void Release(DebrisPiece p)
        {
            Destroy(p.gameObject);
        }

        // ---- registration ----

        public void Register(DebrisPiece piece, float lifetime)
        {
            if (piece == null) return;
            var t = Table;
            piece.transform.SetParent(transform, true);
            // A fallen roof section is there for good: it keeps its Structure layer, so it blocks
            // sight and paths (the grandmother's senses and NavMesh skip Debris) and shields a
            // later blast like a floor. Short-lived debris goes on Debris, out of every query.
            if (!piece.pinned) DestructionLayers.AssignDebris(piece.gameObject);
            float now = Time.time;
            piece.born = now;
            piece.shrinkStart = -1f;
            piece.awaitingRelease = false;
            piece.sleepTime = 0f;
            if (piece.pinned || float.IsInfinity(lifetime))
            {
                piece.lifetime = float.PositiveInfinity;
                piece.expireAt = piece.hardAt = float.PositiveInfinity;
            }
            else
            {
                float jitter = Mathf.Clamp01(t.lifetimeJitter) * (float)(jitterRng.NextDouble() * 2.0 - 1.0);
                piece.lifetime = Mathf.Max(0.1f, lifetime) * (1f + jitter);
                piece.expireAt = now + piece.lifetime;
                piece.hardAt = now + piece.lifetime * Mathf.Max(1f, t.hardLifetimeFactor);
            }
            piece.baseScale = piece.transform.localScale;
            piece.size = SizeOf(piece);
            piece.rend = piece.GetComponent<Renderer>();
            piece.shadowMode = piece.rend != null ? piece.rend.shadowCastingMode : ShadowCastingMode.On;
            ApplyShadow(piece, false);
            pieces.Add(piece);
        }

        // Half the diagonal of its box. A box collider is read from its own size and scale,
        // which is right the frame it is made (Collider.bounds may lag until the physics sync).
        static float SizeOf(DebrisPiece p)
        {
            if (p.TryGetComponent(out BoxCollider box))
                return Vector3.Scale(box.size, p.transform.lossyScale).magnitude * 0.5f;
            return p.TryGetComponent(out Collider c) ? c.bounds.extents.magnitude : 0.1f;
        }

        // The destruction reset: every piece gone at once. Pinned pieces (roof sections) are
        // let go of here and put back by their owner.
        public void Clear()
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null || p.pinned) continue;
                p.gameObject.SetActive(false);   // out of the physics scene now, destroyed at the end of the frame
                Destroy(p.gameObject);
            }
            pieces.Clear();
        }

        // Thaws every frozen piece in or near 'area'. Called just before something breaks, so
        // the debris resting on it falls instead of floating where it was. Static, and a no-op
        // when no debris exists yet, so breaking the first object does not create the manager
        // just to find nothing to wake.
        public static void WakeInBounds(Bounds area)
        {
            if (instance == null) return;
            area.Expand(WakeMargin * 2f);
            List<DebrisPiece> list = instance.pieces;
            for (int i = 0; i < list.Count; i++)
            {
                DebrisPiece p = list[i];
                if (p == null || p.Shrinking || !p.Frozen) continue;
                if (area.Contains(p.transform.position)) Thaw(p);
            }
        }

        // ---- the frame ----

        void Update()
        {
            var t = Table;
            float now = Time.time;
            float dt = Time.deltaTime;
            int frame = Time.frameCount;
            if (budgetFrame == frame - 1) SpawnedLastFrame = spawnedThisFrame;
            else if (budgetFrame < frame - 1) SpawnedLastFrame = 0;

            GatherEyes();
            int destroyBudget = Mathf.Max(1, t.destroyPerFrame);
            releasedThisFrame = 0;
            float cleanupShrink = Mathf.Max(0.05f, t.cleanupShrinkTime);
            float freezeAfter = Mathf.Max(0f, t.freezeAfterSleep);
            int dynamicCount = 0, frozenCount = 0;

            int write = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null) continue;   // destroyed by something else, just forget it

                if (p.awaitingRelease)
                {
                    if (releasedThisFrame < destroyBudget) { releasedThisFrame++; Release(p); continue; }
                    pieces[write++] = p;
                    continue;
                }

                if (!p.Shrinking && now >= p.expireAt && (now >= p.hardAt || MayLeave(p, t)))
                    BeginShrink(p, now, cleanupShrink);

                if (p.Shrinking)
                {
                    float s = (now - p.shrinkStart) / Mathf.Max(0.01f, p.shrinkDuration);
                    if (s >= 1f)
                    {
                        if (releasedThisFrame < destroyBudget) { releasedThisFrame++; Release(p); continue; }
                        // Over the frame's budget: gone from view and physics now, released later.
                        p.transform.localScale = Vector3.zero;
                        p.gameObject.SetActive(false);
                        p.awaitingRelease = true;
                    }
                    else p.transform.localScale = p.baseScale * (1f - s);
                }
                else if (p.rb != null && !p.rb.isKinematic)
                {
                    if (!p.pinned) dynamicCount++;
                    if (p.rb.IsSleeping())
                    {
                        p.sleepTime += dt;
                        // Never on the online client: a frozen piece is immovable there, and the
                        // replicated props (kinematic) would pass through it (NETCODE_SLICE 11.4).
                        if (p.sleepTime >= freezeAfter && !Net.IsClient)
                        {
                            if (RestsOnSupport(p)) Freeze(p);
                            // On furniture or on moving debris: stay a sleeping rigidbody, and
                            // ask again after another full wait rather than every frame.
                            else p.sleepTime = 0f;
                        }
                    }
                    else p.sleepTime = 0f;
                }
                else if (!p.pinned) frozenCount++;

                pieces[write++] = p;
            }
            if (write < pieces.Count) pieces.RemoveRange(write, pieces.Count - write);

            DynamicCount = dynamicCount;
            FrozenCount = frozenCount;
            ReleasedLastFrame = releasedThisFrame;

            CullOverflow(now, false, dynamicCount - Mathf.Max(0, t.maxDynamicPieces));
            CullOverflow(now, true, frozenCount - Mathf.Max(0, t.maxFrozenPieces));

            if (now >= nextShadowCheck)
            {
                nextShadowCheck = now + ShadowCheckEvery;
                RefreshFrozenShadows(t);
            }
        }

        // An expired piece may leave once no crew camera sees it, or once it is far from every
        // crew eye (a speck at that distance goes unnoticed).
        bool MayLeave(DebrisPiece p, DestructionMaterialTable t)
        {
            if (NearestEye(p.transform.position) > Mathf.Max(0f, t.cleanupMinDistance)) return true;
            return !InAnyView(p);
        }

        // Past a cap, the pieces that matter least to what the crew sees start leaving: the
        // ones no crew camera sees first, then small first and far from every crew eye first.
        // Frozen and moving pieces have separate caps and are culled from their own kind.
        void CullOverflow(float now, bool frozen, int excess)
        {
            if (excess <= 0) return;
            if (cullScore.Length < pieces.Count)
            {
                cullScore = new float[Mathf.NextPowerOfTwo(pieces.Count)];
                cullIndex = new int[cullScore.Length];
            }
            int n = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null || p.pinned || p.Shrinking || p.Frozen != frozen) continue;
                float d = NearestEye(p.transform.position);
                cullScore[n] = p.size * CullDistance / (CullDistance + d) + (InAnyView(p) ? InViewScore : 0f);
                cullIndex[n] = i;
                n++;
            }
            System.Array.Sort(cullScore, cullIndex, 0, n);
            float duration = Mathf.Max(0.05f, Table.overflowShrinkTime);
            for (int k = 0; k < excess && k < n; k++) BeginShrink(pieces[cullIndex[k]], now, duration);
        }

        void GatherEyes()
        {
            eyes.Clear();
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != null) eyes.Add(crew[i].EyePosition);
        }

        float NearestEye(Vector3 p)
        {
            if (eyes.Count == 0) return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < eyes.Count; i++) best = Mathf.Min(best, (eyes[i] - p).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        // Inside the frustum of a crew camera rendering on this machine (the driver's chase view
        // is the same camera). Walls are not tested: a piece behind one counts as seen, which
        // only keeps it a little longer. Planes are computed once per frame, when first needed.
        bool InAnyView(DebrisPiece p)
        {
            if (viewsFrame != Time.frameCount) BuildViews();
            if (viewCount == 0) return false;
            Bounds b = p.rend != null ? p.rend.bounds : new Bounds(p.transform.position, Vector3.one * (p.size * 2f));
            for (int i = 0; i < viewCount; i++)
                if (GeometryUtility.TestPlanesAABB(viewPlanes[i], b)) return true;
            return false;
        }

        void BuildViews()
        {
            viewsFrame = Time.frameCount;
            viewCount = 0;
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count && viewCount < MaxViews; i++)
            {
                Camera cam = crew[i] != null ? crew[i].View : null;
                if (cam == null || !cam.isActiveAndEnabled) continue;
                GeometryUtility.CalculateFrustumPlanes(cam, viewPlanes[viewCount]);
                viewCount++;
            }
        }

        void BeginShrink(DebrisPiece p, float now, float duration)
        {
            p.shrinkStart = now;
            p.shrinkDuration = duration;
            // What rested on a frozen piece must not stay hanging where it was.
            if (p.Frozen && p.rend != null) ThawAbove(p.rend.bounds);
        }

        // The frozen pieces lying on a frozen piece that moves or leaves: their centre is over its
        // footprint and above its top. Neighbours beside it on the floor are left alone, so a
        // thaw climbs a pile (Thaw calls this again) but never spreads across a floor of debris.
        void ThawAbove(Bounds b)
        {
            Vector3 c = b.center, e = b.extents;
            float top = b.max.y - 0.05f;
            float ceiling = b.max.y + 2f;
            float rx = e.x + WakeMargin, rz = e.z + WakeMargin;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece q = pieces[i];
                if (q == null || q.Shrinking || !q.Frozen) continue;
                Vector3 at = q.transform.position;
                if (at.y < top || at.y > ceiling) continue;
                if (Mathf.Abs(at.x - c.x) > rx || Mathf.Abs(at.z - c.z) > rz) continue;
                Thaw(q);
            }
        }

        // Straight down from the middle of the piece. A ray that starts inside a collider does
        // not hit it, so the piece never finds itself. Static means no Rigidbody at all:
        // floors and walls. A frozen piece of debris also holds: it only moves when thawed, and
        // whatever thaws it (a bump, a blast, it leaving) thaws what lies on it too.
        // A player's capsule has no Rigidbody either, but it walks away.
        static bool RestsOnSupport(DebrisPiece p)
        {
            if (!p.TryGetComponent(out Collider own)) return false;
            Bounds b = own.bounds;
            if (!Physics.Raycast(b.center, Vector3.down, out RaycastHit hit, b.extents.y + GroundProbe,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.collider is CharacterController) return false;
            Rigidbody under = hit.rigidbody;
            if (under == null) return true;
            return under.isKinematic && under.TryGetComponent(out DebrisPiece below) && !below.pinned && !below.Shrinking;
        }

        // A kinematic body costs nothing to simulate and still holds up whatever lands on it.
        // Continuous detection is dropped first because kinematic bodies do not support it.
        void Freeze(DebrisPiece p)
        {
            p.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            p.rb.isKinematic = true;
            ApplyShadow(p, NearestEye(p.transform.position) > Table.cleanupMinDistance);
        }

        // Back to a normal rigidbody, awake, with its own collision mode. Also used by
        // DebrisPiece when something moving runs into a frozen piece. What was frozen on top of
        // it thaws too, or it would hang in the air once this one is pushed away.
        internal static void Thaw(DebrisPiece p) => Thaw(p, true);

        // climb false: the caller thaws a whole area anyway (a blast), so the piles in it need
        // no separate climb.
        static void Thaw(DebrisPiece p, bool climb)
        {
            if (p == null || p.rb == null || !p.rb.isKinematic) return;
            p.rb.isKinematic = false;
            p.rb.collisionDetectionMode = p.detectionMode;
            p.rb.WakeUp();
            p.sleepTime = 0f;
            ApplyShadow(p, false);
            if (climb && instance != null && p.rend != null) instance.ThawAbove(p.rend.bounds);
        }

        // Shadows (DEV 2 section 5): a small piece casts none; a frozen piece casts none when it
        // is below frozenNoShadowBelow or far from every crew eye. Pinned roofs keep theirs.
        static void ApplyShadow(DebrisPiece p, bool far)
        {
            if (p.rend == null || p.pinned) return;
            var t = Table;
            bool cast = p.size >= t.smallPieceNoShadow;
            if (cast && p.Frozen && (far || p.size < t.frozenNoShadowBelow)) cast = false;
            ShadowCastingMode mode = cast ? p.shadowMode : ShadowCastingMode.Off;
            if (p.rend.shadowCastingMode != mode) p.rend.shadowCastingMode = mode;
        }

        void RefreshFrozenShadows(DestructionMaterialTable t)
        {
            float far = Mathf.Max(0f, t.cleanupMinDistance);
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null || p.pinned || p.Shrinking || !p.Frozen) continue;
                ApplyShadow(p, NearestEye(p.transform.position) > far);
            }
        }

        // ---- explosions ----

        // The online client's blast (Explosion.PlayCosmetic): Detonated is never raised there, so
        // the picture of a host blast wakes the local debris through this. A no-op when no
        // debris exists yet.
        public static void WakeFromBlast(Vector3 position, float radius, float power)
        {
            if (instance != null) instance.OnDetonated(position, radius, power);
        }

        // Raised by Explosion after its own forces are applied, so the frozen pieces missed
        // them. Wake the ones in range and give them the shove they would have had.
        void OnDetonated(Vector3 position, float radius, float power)
        {
            float r = Mathf.Max(0.01f, radius);
            float r2 = r * r;
            float wake = Table.explosionWakeSpeed;
            // Like the pieces the blast pushed itself: what they crush now is on its owner.
            int instigator = Explosion.CurrentInstigator;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null || p.Shrinking || p.rb == null || !p.rb.isKinematic) continue;

                Vector3 d = p.transform.position - position;
                float sq = d.sqrMagnitude;
                if (sq > r2) continue;

                Thaw(p, false);
                if (instigator != Actors.World) p.instigator = instigator;
                float dist = Mathf.Sqrt(sq);
                Vector3 dir = dist > 0.001f ? d / dist : Vector3.up;
                float falloff = 1f - dist / r;
                p.rb.linearVelocity += (dir + Vector3.up * 0.5f).normalized * (wake * power * falloff);
            }
        }
    }
}
