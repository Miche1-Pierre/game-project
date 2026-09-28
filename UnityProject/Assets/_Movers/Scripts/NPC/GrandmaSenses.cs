using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // What the grandmother notices, turned into Stimulus values for the brain. Legible by rule,
    // so players can learn to play around her:
    //
    // - Vision: a 110 degree cone, 14 m, and a clear line from her head to a player's eyes or
    //   chest. Intact glass does not block it: she sees you through the veranda.
    // - Hearing: world events within WorldEvents.HearingRadius(loudness), halved for each wall
    //   or floor in between. Explosions are heard through walls, and at full range (her
    //   deafness does not apply to them, MoodCosts.explosionHearing).
    // - Touch: a player pushing into her, or a thrown object hitting her.
    // - The clock: whether the house is emptying as fast as the time runs out.
    //
    // Theft: she witnesses it when she sees a player pocket or load one of her things that is
    // not on the list, or carry one in her sight for more than carryWitnessSeconds.
    [DisallowMultipleComponent]
    public sealed class GrandmaSenses : MonoBehaviour
    {
        [Header("Vision")]
        public float visionAngle = 110f;
        public float visionRange = 14f;
        [Tooltip("Seconds between two looks at the crew. The carry timers advance by the real time between looks.")]
        public float visionInterval = 0.1f;

        [Header("Hearing")]
        [Tooltip("Hearing radius factor per wall or floor in between.")]
        [Range(0f, 1f)] public float wallDamping = 0.5f;
        public int maxWalls = 3;
        public bool explosionsIgnoreWalls = true;

        [Header("Touch")]
        [Tooltip("Seconds of a player walking into her before it counts as a bump.")]
        public float pushSeconds = 0.25f;
        [Tooltip("Relative speed at which a loose object hitting her counts, m/s.")]
        public float hitSpeed = 2.5f;

        public GrandmaMover mover;
        public GrandmaMood mood;
        public Animator animator;

        public event Action<Stimulus> Perceived;

        static readonly MoodCosts DefaultCosts = new MoodCosts();
        MoodCosts Costs => mood != null ? mood.Costs : DefaultCosts;

        // Set by the brain while her AI is off or the run is over: she perceives nothing, so
        // nothing is marked witnessed behind the brain's back.
        [System.NonSerialized] public bool asleep;

        const int MaxPlayers = 4;
        readonly bool[] seen = new bool[MaxPlayers];
        readonly float[] carrySeen = new float[MaxPlayers];
        readonly float[] carryCharge = new float[MaxPlayers];
        readonly float[] pushTime = new float[MaxPlayers];
        readonly CharacterController[] crewBodies = new CharacterController[MaxPlayers];
        readonly HashSet<MovableObject> witnessed = new HashSet<MovableObject>();
        readonly List<MovableObject> required = new List<MovableObject>();

        static readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly float[] distances = new float[32];
        static readonly Collider[] touching = new Collider[16];

        Transform head;
        Action<WorldEvent> onWorldEvent;
        float nextLook;
        float lastLookTime = -1f;
        float nextScheduleCheck = -1f;
        bool listRead;
        int layerMask = ~0;
        MovableObject lastHitBy;
        float lastHitTime = -99f;

        // For the debug overlay.
        public Vector3 LastNoisePosition { get; private set; }
        public float LastNoiseRadius { get; private set; }
        public float LastNoiseTime { get; private set; } = -99f;
        public bool LastNoiseHeard { get; private set; }
        public int WitnessedCount => witnessed.Count;

        public bool Sees(int player) => player >= 0 && player < MaxPlayers && seen[player];
        public bool HasWitnessed(MovableObject item) => item != null && witnessed.Contains(item);

        public Vector3 HeadPosition => head != null ? head.position : transform.position + Vector3.up * 1.45f;

        void Awake()
        {
            if (mover == null) mover = GetComponent<GrandmaMover>();
            if (mood == null) mood = GetComponent<GrandmaMood>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
            onWorldEvent = OnWorldEvent;
            int debris = LayerMask.NameToLayer("Debris");
            if (debris >= 0) layerMask &= ~(1 << debris);
        }

        void OnEnable() { WorldEvents.Subscribe(onWorldEvent); }
        void OnDisable() { WorldEvents.Unsubscribe(onWorldEvent); }

        Vector3 Forward => mover != null ? mover.Forward : transform.forward;

        // ---------------------------------------------------------------- vision

        void Update()
        {
            if (!Net.HasAuthority) return;   // online client: the host perceives for her
            if (asleep) { lastLookTime = -1f; return; }
            if (Time.time >= nextLook)
            {
                // A look lands on the first frame after nextLook, so the real gap is longer
                // than the interval (at 30 fps, 0.13 s rather than 0.1). The carry rules ("2 s
                // in sight", "every 5 s") are in game seconds, so they advance by the real gap.
                // Capped: a hitch or a long sleep must not count as time spent watching.
                float gap = lastLookTime < 0f ? visionInterval : Mathf.Min(Time.time - lastLookTime, 0.5f);
                lastLookTime = Time.time;
                nextLook = Time.time + visionInterval;
                LookAtCrew(gap);
            }
            CheckSchedule();
        }

        void LookAtCrew(float elapsed)
        {
            var costs = mood != null ? mood.Costs : null;
            for (int p = 0; p < MaxPlayers; p++)
            {
                CrewMember m = CrewRoster.Get(p);
                bool sees = m != null && m.isActiveAndEnabled && CanSee(m);
                seen[p] = sees;

                MovableObject held = sees ? m.Held : null;
                if (held == null || !held.IsTheftTarget)
                {
                    carrySeen[p] = 0f;
                    carryCharge[p] = 0f;
                    continue;
                }

                carrySeen[p] += elapsed;
                carryCharge[p] += elapsed;
                float witnessAfter = costs != null ? costs.carryWitnessSeconds : 2f;
                float chargeEvery = costs != null ? Mathf.Max(0.5f, costs.carryingInterval) : 5f;
                if (carrySeen[p] >= witnessAfter && !witnessed.Contains(held))
                    Witness(held, p, held.transform.position);
                if (carryCharge[p] >= chargeEvery)
                {
                    carryCharge[p] -= chargeEvery;
                    var s = new Stimulus(StimulusKind.CarryingSeen, m.Position, p) { seen = true, item = held };
                    Emit(s);
                }
            }
        }

        // Cone, range, then a clear line to the eyes or the chest.
        public bool CanSee(CrewMember m)
        {
            if (m == null) return false;
            Transform ignore = m.Held != null ? m.Held.transform : null;
            Vector3 eyes = m.EyePosition;
            Vector3 chest = ChestOf(m);
            return (InCone(eyes) && ClearLine(HeadPosition, eyes, m.transform, ignore))
                || (InCone(chest) && ClearLine(HeadPosition, chest, m.transform, ignore));
        }

        public bool CanSeePlayer(int player) => CanSee(CrewRoster.Get(player));

        public bool CanSeePoint(Vector3 point, Transform target = null)
        {
            return InCone(point) && ClearLine(HeadPosition, point, target, null);
        }

        Vector3 ChestOf(CrewMember m)
        {
            CharacterController cc = BodyOf(m);
            if (cc != null && cc.enabled) return cc.bounds.center + Vector3.up * 0.2f;
            return m.EyePosition - Vector3.up * 0.5f;
        }

        CharacterController BodyOf(CrewMember m)
        {
            int i = m.index;
            if (i < 0 || i >= MaxPlayers) return m.GetComponent<CharacterController>();
            if (crewBodies[i] == null || crewBodies[i].transform != m.transform) crewBodies[i] = m.GetComponent<CharacterController>();
            return crewBodies[i];
        }

        bool InCone(Vector3 point)
        {
            Vector3 d = point - HeadPosition;
            if (d.sqrMagnitude > visionRange * visionRange) return false;
            return Vector3.Angle(Forward, d) <= visionAngle * 0.5f;
        }

        // Nothing solid between: her own body, the target and what it carries, triggers and
        // intact glass are see-through.
        bool ClearLine(Vector3 from, Vector3 to, Transform target, Transform alsoIgnore)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(from, d / dist, hits, dist - 0.05f, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                Transform t = c.transform;
                if (t.IsChildOf(transform)) continue;
                if (target != null && t.IsChildOf(target)) continue;
                if (alsoIgnore != null && t.IsChildOf(alsoIgnore)) continue;
                GlassPane pane = c.GetComponentInParent<GlassPane>();
                if (pane != null && !pane.IsBroken) continue;
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- hearing

        void OnWorldEvent(WorldEvent e)
        {
            if (!Net.HasAuthority || asleep || !isActiveAndEnabled || e.instigator == Actors.Grandma) return;
            MovableObject item = e.Item;
            switch (e.type)
            {
                case WorldEventType.LoudNoise:
                    // Her own door clicking shut behind her is not news.
                    if (mover != null && mover.RecentlyOperatedDoorNear(e.position, 2.5f, 3f)) return;
                    if (Hears(e.position, e.loudness, false))
                        Emit(new Stimulus(StimulusKind.SmallNoise, e.position, e.instigator) { seen = CanSeePoint(e.position) });
                    break;

                case WorldEventType.Explosion:
                    if (Hears(e.position, Mathf.Max(1f, e.loudness), explosionsIgnoreWalls, Costs.explosionHearing))
                        Emit(new Stimulus(StimulusKind.Explosion, e.position, e.instigator) { seen = CanSeePoint(e.position), inside = IsIndoors(e.position) });
                    break;

                case WorldEventType.ObjectDestroyed:
                    // A piece on the list also raises ContractObjectDestroyed: priced there, once.
                    if (item != null && item.requiredForContract) return;
                    Perceive(StimulusKind.ObjectDestroyed, e, Costs.objectDestroyedLoudness, item, item != null && item.fragile);
                    break;
                case WorldEventType.ObjectDamaged:
                    // Same rule: a piece on the list is ContractObjectDamaged.
                    if (item == null || item.requiredForContract) return;
                    Perceive(StimulusKind.ObjectDamaged, e, Costs.objectDamagedLoudness, item, false);
                    break;
                case WorldEventType.ContractObjectDamaged: Perceive(StimulusKind.ContractDamaged, e, Costs.contractDamagedLoudness, item, false); break;
                case WorldEventType.ContractObjectDestroyed: Perceive(StimulusKind.ContractDestroyed, e, Costs.contractDestroyedLoudness, item, false); break;
                case WorldEventType.WindowBroken: Perceive(StimulusKind.WindowBroken, e, Costs.windowBrokenLoudness, null, false); break;
                case WorldEventType.DoorBroken: Perceive(StimulusKind.DoorBroken, e, Costs.doorBrokenLoudness, null, false); break;

                // A wall broken through (a crack is not news), or part of the house coming down.
                case WorldEventType.StructureDamaged:
                    if (e.magnitude >= (float)DestructionState.Destroyed)
                        Perceive(StimulusKind.StructureBroken, e, Costs.structureBrokenLoudness, null, false);
                    break;
                case WorldEventType.StructureCollapsed:
                    Perceive(StimulusKind.StructureBroken, e, Costs.structureBrokenLoudness, null, false, true);
                    break;
                case WorldEventType.GardenDamaged:
                    Perceive(StimulusKind.GardenBroken, e, Costs.gardenBrokenLoudness, null, false);
                    break;

                case WorldEventType.PlayerSmoking:
                    if (Actors.IsPlayer(e.instigator) && CanSeePlayer(e.instigator))
                        Emit(new Stimulus(StimulusKind.Smoking, e.position, e.instigator) { seen = true });
                    break;
                case WorldEventType.PlayerDrinking:
                    if (Actors.IsPlayer(e.instigator) && CanSeePlayer(e.instigator))
                        Emit(new Stimulus(StimulusKind.Drinking, e.position, e.instigator) { seen = true });
                    break;

                case WorldEventType.ItemPocketed:
                    if (item != null && item.IsTheftTarget && Actors.IsPlayer(e.instigator) && CanSeePlayer(e.instigator))
                        Witness(item, e.instigator, e.position);
                    break;
                case WorldEventType.CargoLoaded:
                    if (item != null && item.IsTheftTarget
                        && (CanSeePoint(e.position, item.transform) || (Actors.IsPlayer(e.instigator) && CanSeePlayer(e.instigator))))
                        Witness(item, e.instigator, e.position);
                    break;
            }
        }

        // Breakage: seen, or heard with a default loudness when the raiser gave none. What broke
        // does not hide itself: a wall is seen breaking through its own colliders.
        void Perceive(StimulusKind kind, in WorldEvent e, float defaultLoudness, MovableObject item, bool fragile, bool collapse = false)
        {
            Transform broken = item != null ? item.transform : e.subject is Component c && c != null ? c.transform : null;
            bool sawIt = CanSeePoint(e.position, broken);
            float loudness = e.loudness > 0f ? e.loudness : defaultLoudness;
            if (!sawIt && !Hears(e.position, loudness, false)) return;
            Emit(new Stimulus(kind, e.position, e.instigator) { seen = sawIt, item = item, fragile = fragile, value = e.value, collapse = collapse });
        }

        void Witness(MovableObject item, int thief, Vector3 at)
        {
            if (item == null || !witnessed.Add(item)) return;
            Emit(new Stimulus(StimulusKind.TheftWitnessed, at, thief) { seen = true, item = item, value = item.contractValue });
        }

        public bool Hears(Vector3 source, float loudness, bool ignoreWalls) => Hears(source, loudness, ignoreWalls, Costs.hearing);

        // hearing: the share of the normal radius she hears at (MoodCosts.hearing, or
        // explosionHearing for a blast).
        public bool Hears(Vector3 source, float loudness, bool ignoreWalls, float hearing)
        {
            float radius = WorldEvents.HearingRadius(loudness) * hearing;
            Vector3 ear = HeadPosition;
            float dist = Vector3.Distance(source, ear);
            float effective = radius;
            if (dist <= radius && !ignoreWalls)
                effective = radius * Mathf.Pow(wallDamping, CountBarriers(source + Vector3.up * 0.1f, ear));
            LastNoisePosition = source;
            LastNoiseRadius = effective;
            LastNoiseTime = Time.time;
            LastNoiseHeard = dist <= effective;
            return LastNoiseHeard;
        }

        // Walls and floors between two points: big static solids (and shut door leaves), each
        // counted once even when a wall is made of several colliders a few centimetres apart.
        // A kitchen counter in the way is not a wall.
        int CountBarriers(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.1f) return 0;
            int n = Physics.RaycastNonAlloc(from, d / dist, hits, dist, layerMask, QueryTriggerInteraction.Ignore);
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c is CharacterController) continue;
                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;   // loose things do not stop sound
                if (!IsWallSized(c.bounds.size)) continue;
                distances[count++] = hits[i].distance;
            }
            // Sort, then merge hits closer than a wall's thickness.
            for (int i = 1; i < count; i++)
            {
                float v = distances[i];
                int j = i - 1;
                while (j >= 0 && distances[j] > v) { distances[j + 1] = distances[j]; j--; }
                distances[j + 1] = v;
            }
            int walls = 0;
            float last = -1f;
            for (int i = 0; i < count; i++)
            {
                if (last < 0f || distances[i] - last > 0.35f) walls++;
                last = distances[i];
            }
            return Mathf.Min(walls, maxWalls);
        }

        // At least 1.8 m one way and 1 m another: a wall module, a floor, a door leaf.
        static bool IsWallSized(Vector3 size)
        {
            float a = size.x, b = size.y, c = size.z;
            float max = Mathf.Max(a, Mathf.Max(b, c));
            float min = Mathf.Min(a, Mathf.Min(b, c));
            float mid = a + b + c - max - min;
            return max >= 1.8f && mid >= 1f;
        }

        // Under a roof or a ceiling.
        bool IsIndoors(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.3f, Vector3.up, hits, 15f, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                Rigidbody rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- touch

        void FixedUpdate()
        {
            if (!Net.HasAuthority || asleep) return;
            FeelPlayers();
            FeelHits();
        }

        // A player walking into her: in contact and still pushing, for a moment.
        void FeelPlayers()
        {
            Vector3 me = transform.position;
            float myRadius = mover != null ? mover.radius : 0.28f;
            for (int p = 0; p < MaxPlayers; p++)
            {
                CrewMember m = CrewRoster.Get(p);
                if (m == null || m.Input == null) { pushTime[p] = 0f; continue; }
                CharacterController cc = BodyOf(m);
                float theirRadius = cc != null ? cc.radius : 0.35f;
                Vector3 toMe = me - m.Position;
                float dy = toMe.y;
                toMe.y = 0f;
                float gap = toMe.magnitude;
                bool contact = gap < myRadius + theirRadius + 0.12f && Mathf.Abs(dy) < 1.6f;

                Vector2 move = m.Input.Move;
                Vector3 fwd = m.LookDirection;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3 wish = right * move.x + fwd * move.y;
                bool pushing = contact && gap > 1e-3f && wish.sqrMagnitude > 0.25f && Vector3.Dot(wish.normalized, toMe / gap) > 0.5f;

                if (!pushing) { pushTime[p] = Mathf.Min(0f, pushTime[p] + Time.fixedDeltaTime); continue; }
                pushTime[p] += Time.fixedDeltaTime;
                if (pushTime[p] >= pushSeconds)
                {
                    pushTime[p] = -1.5f;   // one bump per shove, not one per physics step
                    Emit(new Stimulus(StimulusKind.Bumped, m.Position, p) { seen = true });
                }
            }
        }

        // Something thrown (or knocked) into her.
        void FeelHits()
        {
            float r = (mover != null ? mover.radius : 0.28f) + 0.12f;
            float h = mover != null ? mover.height : 1.58f;
            Vector3 bottom = transform.position + Vector3.up * r;
            Vector3 top = transform.position + Vector3.up * Mathf.Max(r, h - r);
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, r, touching, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = touching[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (!rb.TryGetComponent(out MovableObject mo) || mo.holder != null) continue;
                Vector3 v = rb.linearVelocity;
                if (v.sqrMagnitude < hitSpeed * hitSpeed) continue;
                Vector3 toMe = transform.position + Vector3.up * (h * 0.5f) - rb.worldCenterOfMass;
                if (Vector3.Dot(v, toMe) <= 0f) continue;
                if (mo == lastHitBy && Time.time - lastHitTime < 1f) continue;
                lastHitBy = mo;
                lastHitTime = Time.time;
                Emit(new Stimulus(StimulusKind.Bumped, rb.position, mo.RecentHandler(2f)) { seen = true, item = mo });
            }
        }

        // ---------------------------------------------------------------- the clock

        // Once a minute: is the loaded share of the list keeping up with the time used?
        void CheckSchedule()
        {
            if (!Session.IsRunning || Session.TimeLimit <= 0f || mood == null) return;
            if (!listRead)
            {
                // The moving list, read once the run has started (the contract may mark its
                // pieces at load). Destroyed and loaded pieces count as done.
                listRead = true;
                var all = FindObjectsByType<MovableObject>(FindObjectsInactive.Include);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].requiredForContract) required.Add(all[i]);
            }
            if (required.Count == 0) return;
            float interval = Mathf.Max(5f, mood.Costs.scheduleInterval);
            if (nextScheduleCheck < 0f) { nextScheduleCheck = Time.time + interval; return; }
            if (Time.time < nextScheduleCheck) return;
            nextScheduleCheck = Time.time + interval;

            int done = 0;
            for (int i = 0; i < required.Count; i++)
            {
                MovableObject mo = required[i];
                if (mo != null && (mo.loaded || mo.destroyed)) done++;
            }
            float progress = (float)done / required.Count;
            float elapsed = 1f - Mathf.Clamp01(Session.TimeLeft / Session.TimeLimit);
            if (elapsed - progress > mood.Costs.scheduleGrace)
                Emit(new Stimulus(StimulusKind.BehindSchedule, transform.position, Actors.World));
        }

        void Emit(in Stimulus s)
        {
            try { Perceived?.Invoke(s); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
