using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Owns every piece of debris in the scene: how long it lives and how many exist at once.
    //
    // Smashing things is the fun part, but every fragment is a rigidbody, and a grenade in the
    // cellar used to spawn hundreds of them in one frame (117 ms, 1792 pieces at the peak,
    // ADR-008). So the pile is bounded four ways:
    // - at spawn: at most maxSpawnPerFrame new pieces a frame (TryReserve). Whoever breaks
    //   something asks first and gets fewer pieces, or none, when the frame is full. The blast
    //   breaks things nearest first, so the nearest get their debris;
    // - each piece has a lifetime (structure 25 s, furniture 18 s, glass 8 s), then shrinks
    //   to nothing over a second rather than popping out of existence;
    // - past maxPieces the least visible go first, shrinking fast: the smallest, farthest from
    //   any crew camera. (It used to be the oldest, which inside one burst meant the nearest
    //   wall, the very thing the player was looking at.);
    // - a piece that has been asleep for a few seconds turns kinematic, which takes it out of
    //   the solver entirely.
    //
    // Kinematic means immovable, so freezing is careful about what it leaves behind:
    // - only a piece lying on static ground (a floor, the terrain) freezes. On a table, a sofa
    //   or a door it stays a sleeping rigidbody, because that support can be carried off or
    //   swung open, and a frozen piece would stay hanging in the air;
    // - a frozen piece thaws when something moving bumps into it (DebrisPiece), when what it
    //   rests on breaks (WakeInBounds, called by Breakable and GlassPane), or when an
    //   explosion goes off nearby.
    //
    // Pinned pieces (fallen roof sections) are never aged out or culled, and stay off the Debris layer.
    //
    // Created on demand as a root object named "Debris" the first time something shatters.
    // Placing one in the scene by hand is allowed, and is how you would tune the numbers.
    [DisallowMultipleComponent]
    public class DebrisManager : MonoBehaviour
    {
        [Tooltip("Cap on live pieces. Past it the smallest, farthest ones shrink away first.")]
        public int maxPieces = 450;
        [Tooltip("New pieces allowed per frame, all sources together. Past it things break with fewer pieces, or just dust.")]
        // 90, not 150: each piece costs about 0.13 ms to build, and 150 made a grenade frame
        // 27 ms warm (measured in the E6 tests, 2026-09-26); 90 keeps it near 12 ms. The
        // pieces that do not fit simply vanish in the dust.
        public int maxSpawnPerFrame = 90;
        [Tooltip("Seconds a piece takes to shrink away when its lifetime is over.")]
        public float shrinkTime = 1f;
        [Tooltip("Seconds a piece takes to shrink away when it is culled for the budget.")]
        public float overflowShrinkTime = 0.25f;
        [Tooltip("Seconds asleep on static ground before a piece turns kinematic to save physics.")]
        public float freezeAfterSleep = 3f;
        [Tooltip("Speed given to a frozen piece woken by an explosion, at the centre, per unit of power.")]
        public float explosionWakeSpeed = 8f;

        const float GroundProbe = 0.15f;   // how far under its own collider a piece looks for the ground
        const float WakeMargin = 0.5f;     // a piece this close to a breaking object may be resting on it
        const float CullDistance = 10f;    // m: at this distance a piece counts half as visible

        static DebrisManager instance;
        static bool quitting;

        // In spawn order. The cull picks by visibility, not by position in the list.
        readonly List<DebrisPiece> pieces = new List<DebrisPiece>(512);
        // Cull scratch, reused.
        float[] cullScore = new float[512];
        int[] cullIndex = new int[512];
        readonly List<Vector3> eyes = new List<Vector3>(4);

        int spawnFrame = -1;
        int spawnedThisFrame;

        public int Count => pieces.Count;
        public int SpawnedThisFrame => spawnFrame == Time.frameCount ? spawnedThisFrame : 0;
        public int SpawnedLastFrame { get; private set; }

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
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnQuitting() { quitting = true; }

        void Awake()
        {
            if (instance == null) instance = this;
            else if (instance != this) Destroy(this);
        }

        void OnEnable() { Explosion.Detonated += OnDetonated; }
        void OnDisable() { Explosion.Detonated -= OnDetonated; }

        void OnDestroy()
        {
            Explosion.Detonated -= OnDetonated;
            if (instance == this) instance = null;
        }

        // ---- the spawn budget ----

        // Asks for room for 'wanted' new pieces this frame. granted may be fewer, or 0.
        public bool TryReserve(int wanted, out int granted)
        {
            int frame = Time.frameCount;
            if (spawnFrame != frame)
            {
                SpawnedLastFrame = spawnFrame == frame - 1 ? spawnedThisFrame : 0;
                spawnFrame = frame;
                spawnedThisFrame = 0;
            }
            granted = Mathf.Clamp(Mathf.Max(0, maxSpawnPerFrame) - spawnedThisFrame, 0, Mathf.Max(0, wanted));
            spawnedThisFrame += granted;
            return granted > 0;
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

        public void Register(DebrisPiece piece, float lifetime)
        {
            if (piece == null) return;
            piece.transform.SetParent(transform, true);
            // A fallen roof section is there for good: it keeps its Structure layer, so it blocks
            // sight and paths (the grandmother's senses and NavMesh skip Debris) and shields a
            // later blast like a floor. Short-lived debris goes on Debris, out of every query.
            if (!piece.pinned) DestructionLayers.AssignDebris(piece.gameObject);
            piece.born = Time.time;
            piece.lifetime = piece.pinned ? float.PositiveInfinity : Mathf.Max(0.1f, lifetime);
            piece.baseScale = piece.transform.localScale;
            piece.size = piece.TryGetComponent(out Collider c) ? c.bounds.extents.magnitude : 0.1f;
            pieces.Add(piece);
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

        void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            int frame = Time.frameCount;
            if (spawnFrame == frame - 1) SpawnedLastFrame = spawnedThisFrame;
            else if (spawnFrame < frame - 1) SpawnedLastFrame = 0;

            CullOverflow(now);

            int write = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null) continue;   // destroyed by something else, just forget it

                if (!p.Shrinking && now - p.born >= p.lifetime) BeginShrink(p, now, shrinkTime);

                if (p.Shrinking)
                {
                    float t = (now - p.shrinkStart) / Mathf.Max(0.01f, p.shrinkDuration);
                    if (t >= 1f)
                    {
                        Destroy(p.gameObject);
                        continue;
                    }
                    p.transform.localScale = p.baseScale * (1f - t);
                }
                else if (p.rb != null && !p.rb.isKinematic)
                {
                    if (p.rb.IsSleeping())
                    {
                        p.sleepTime += dt;
                        // Never on the online client: a frozen piece is immovable there, and the
                        // replicated props (kinematic) would pass through it (NETCODE_SLICE 11.4).
                        if (p.sleepTime >= freezeAfterSleep && !Net.IsClient)
                        {
                            if (RestsOnStaticGround(p)) Freeze(p);
                            // On furniture or on other debris: stay a sleeping rigidbody, and
                            // ask again after another full wait rather than every frame.
                            else p.sleepTime = 0f;
                        }
                    }
                    else p.sleepTime = 0f;
                }

                pieces[write++] = p;
            }
            if (write < pieces.Count) pieces.RemoveRange(write, pieces.Count - write);
        }

        // Past the cap, the pieces that matter least to what the crew sees start leaving:
        // small first, far from every crew camera first.
        void CullOverflow(float now)
        {
            // Pieces already shrinking are on their way out and do not count.
            int candidates = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p != null && !p.pinned && !p.Shrinking) candidates++;
            }
            int excess = candidates - Mathf.Max(0, maxPieces);
            if (excess <= 0) return;

            eyes.Clear();
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != null) eyes.Add(crew[i].EyePosition);

            if (cullScore.Length < pieces.Count)
            {
                cullScore = new float[Mathf.NextPowerOfTwo(pieces.Count)];
                cullIndex = new int[cullScore.Length];
            }
            int n = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null || p.pinned || p.Shrinking) continue;
                float d = NearestEye(p.transform.position);
                cullScore[n] = p.size * CullDistance / (CullDistance + d);
                cullIndex[n] = i;
                n++;
            }
            System.Array.Sort(cullScore, cullIndex, 0, n);
            for (int k = 0; k < excess && k < n; k++) BeginShrink(pieces[cullIndex[k]], now, overflowShrinkTime);
        }

        float NearestEye(Vector3 p)
        {
            if (eyes.Count == 0) return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < eyes.Count; i++) best = Mathf.Min(best, (eyes[i] - p).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        static void BeginShrink(DebrisPiece p, float now, float duration)
        {
            p.shrinkStart = now;
            p.shrinkDuration = duration;
        }

        // Straight down from the middle of the piece. A ray that starts inside a collider does
        // not hit it, so the piece never finds itself. Static means no Rigidbody at all:
        // floors and walls. A player's capsule has no Rigidbody either, but it walks away.
        static bool RestsOnStaticGround(DebrisPiece p)
        {
            if (!p.TryGetComponent(out Collider own)) return false;
            Bounds b = own.bounds;
            if (!Physics.Raycast(b.center, Vector3.down, out RaycastHit hit, b.extents.y + GroundProbe,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.collider is CharacterController) return false;
            return hit.rigidbody == null;
        }

        // A kinematic body costs nothing to simulate and still holds up whatever lands on it.
        // Continuous detection is dropped first because kinematic bodies do not support it.
        static void Freeze(DebrisPiece p)
        {
            p.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            p.rb.isKinematic = true;
        }

        // Back to a normal rigidbody, awake, with its own collision mode. Also used by
        // DebrisPiece when something moving runs into a frozen piece.
        internal static void Thaw(DebrisPiece p)
        {
            if (p == null || p.rb == null || !p.rb.isKinematic) return;
            p.rb.isKinematic = false;
            p.rb.collisionDetectionMode = p.detectionMode;
            p.rb.WakeUp();
            p.sleepTime = 0f;
        }

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
            // Like the pieces the blast pushed itself: what they crush now is on its owner.
            int instigator = Explosion.CurrentInstigator;
            for (int i = 0; i < pieces.Count; i++)
            {
                DebrisPiece p = pieces[i];
                if (p == null || p.Shrinking || p.rb == null || !p.rb.isKinematic) continue;

                Vector3 d = p.transform.position - position;
                float sq = d.sqrMagnitude;
                if (sq > r2) continue;

                Thaw(p);
                if (instigator != Actors.World) p.instigator = instigator;
                float dist = Mathf.Sqrt(sq);
                Vector3 dir = dist > 0.001f ? d / dist : Vector3.up;
                float falloff = 1f - dist / r;
                p.rb.linearVelocity += (dir + Vector3.up * 0.5f).normalized * (explosionWakeSpeed * power * falloff);
            }
        }
    }
}
