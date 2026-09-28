using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The police flee of a map (ADR-013, DEV2 section 8), on _Systems. Its presence is what turns
    // the grandmother's call from a failure into a flee (GameSession.HasEscape). Everything it
    // needs comes from scene markers (MissionRoute, PoliceSpawn, EscapeCheckpoint, the cars), so
    // a new map only places them.
    //
    // Host only (Net.HasAuthority); the client sees the result through the Session record, the
    // transform stream (the cars) and the relayed events. From the call on:
    //   dispatch      the lead car leaves early enough to reach the arrival point when the
    //                 countdown ends, the others `stagger` seconds after it
    //   targets       every 0.25 s once they are here: the truck if crew is aboard or it moves,
    //                 else the nearest crew member on foot; the second car takes the other one
    //   arrests       a member on foot touched by a moving car, or 2 s in the officer zone of a
    //                 stopped one (pending meanwhile: the HUD's "CAUGHT" ring)
    //   interception  a car by the slow truck for interceptSeconds in a row (warned from the start)
    //   the exit      the truck's centre of mass in the checkpoint with free crew aboard
    // GameSession owns the phases, the flee timer, the settlement and every Session field.
    [DisallowMultipleComponent]
    public sealed class EscapeMission : MonoBehaviour
    {
        [Tooltip("Seconds from the call to the lead car's arrival on this map. Negative: GameLoopNumbers.policeCountdown.")]
        public float policeCountdown = -1f;
        [Tooltip("Seconds of escape from the arrival on this map. Negative: GameLoopNumbers.fleeTimeLimit.")]
        public float fleeTimeLimit = -1f;

        [Header("Markers (found in the scene when empty)")]
        public MissionRoute route;
        public PoliceSpawn spawn;
        public EscapeCheckpoint checkpoint;
        public TruckVehicle truck;

        const float RetargetInterval = 0.25f;
        const float ParkSpacing = 9f;          // metres between the cars parked at the arrival
        const float TruckMovingSpeed = 2f;     // m/s: a moving truck is chased even with nobody seen aboard
        const float BodyCentreHeight = 0.9f;   // a member's capsule centre above their feet

        readonly List<PoliceCar> cars = new List<PoliceCar>(4);
        readonly float[] zoneFor = new float[8];
        float leadDepartsAt = -1f;
        int dispatched;
        float nextRetarget;
        float interceptFor;

        public TruckVehicle Truck => truck;
        public MissionRoute Route => route;
        public EscapeCheckpoint Checkpoint => checkpoint != null ? checkpoint : EscapeCheckpoint.Active;
        // The car whose arrival starts the escape time.
        public Rigidbody LeadCarBody
        {
            get
            {
                ResolveCars();
                return cars.Count > 0 && cars[0] != null ? cars[0].Body : null;
            }
        }

        public float Countdown(GameLoopNumbers n) => policeCountdown >= 0f ? policeCountdown : n.policeCountdown;
        public float FleeTime(GameLoopNumbers n) => fleeTimeLimit >= 0f ? fleeTimeLimit : n.fleeTimeLimit;

        void Awake()
        {
            var scene = gameObject.scene;
            if (route == null) route = SceneLookup.Find<MissionRoute>(scene);
            if (spawn == null) spawn = SceneLookup.Find<PoliceSpawn>(scene);
            if (checkpoint == null) checkpoint = SceneLookup.Find<EscapeCheckpoint>(scene);
            if (truck == null) truck = SceneLookup.Find<TruckVehicle>(scene);
        }

        void Update()
        {
            if (!Net.HasAuthority) return;
            var s = GameSession.Current;
            if (s == null || s.Mission != this || Session.IsOver || Session.Phase == MissionPhase.Job) return;
            var n = s.Numbers;
            float dt = Time.deltaTime;

            Dispatch(s, n);
            if (Session.Phase == MissionPhase.PoliceHere && Time.time >= nextRetarget)
            {
                nextRetarget = Time.time + RetargetInterval;
                Retarget();
            }
            CheckArrests(s, n, dt);
            if (Session.IsOver) return;
            CheckInterception(s, n, dt);
            if (Session.IsOver) return;
            CheckExit(s, n);
        }

        // ---- the cars ----

        void ResolveCars()
        {
            if (cars.Count > 0) return;
            if (spawn != null)
            {
                var list = spawn.Cars;
                for (int i = 0; i < list.Count; i++) if (list[i] != null) cars.Add(list[i]);
            }
            else
            {
                var list = PoliceCar.All;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].gameObject.scene == gameObject.scene) cars.Add(list[i]);
            }
        }

        int CarCount(GameLoopNumbers n) => Mathf.Min(cars.Count, Mathf.Max(0, n.policeCarCount));

        // The lead car leaves when the route at cruise speed (plus the time to reach it) is all
        // the countdown has left; the others follow `stagger` apart. Once the police are due,
        // anyone still parked goes at once.
        void Dispatch(GameSession s, GameLoopNumbers n)
        {
            ResolveCars();
            if (route == null) return;
            int count = CarCount(n);
            if (dispatched >= count) return;
            float arrivalS = route.ArrivalDistance;
            if (leadDepartsAt < 0f)
            {
                float cruise = Mathf.Max(1f, n.policeCruiseKmh / 3.6f);
                float leadS = cars[0] != null ? route.ProgressOf(cars[0].transform.position, -1f) : 0f;
                float travel = Mathf.Abs(arrivalS - leadS) / cruise + cruise / (2f * Mathf.Max(0.1f, n.policeAccel));
                leadDepartsAt = Time.time + Mathf.Max(0f, s.PoliceIn - travel);
            }
            float stagger = spawn != null ? spawn.Stagger(n) : n.policeStagger;
            while (dispatched < count &&
                   (Time.time >= leadDepartsAt + dispatched * stagger || Session.Phase == MissionPhase.PoliceHere))
            {
                var car = cars[dispatched];
                if (car != null) car.Dispatch(route, Mathf.Max(0f, arrivalS - dispatched * ParkSpacing), car.lane);
                dispatched++;
            }
        }

        // The truck if crew is aboard or it moves, else the nearest member on foot. The second
        // car (and any after it) prefers whatever the lead car is not chasing.
        void Retarget()
        {
            bool truckWanted = truck != null && (TruckMoving || AnyAboard());
            CrewMember leadMember = null;
            bool leadOnTruck = false;
            for (int i = 0; i < dispatched && i < cars.Count; i++)
            {
                var car = cars[i];
                if (car == null) continue;
                CrewMember member = null;
                bool onTruck;
                if (i == 0)
                {
                    onTruck = truckWanted;
                    if (!onTruck) member = NearestOnFoot(car.Center, null);
                    if (!onTruck && member == null) onTruck = truck != null;
                    leadOnTruck = onTruck;
                    leadMember = member;
                }
                else if (leadOnTruck)
                {
                    member = NearestOnFoot(car.Center, null);
                    onTruck = member == null;
                }
                else
                {
                    onTruck = truckWanted;
                    if (!onTruck)
                    {
                        member = NearestOnFoot(car.Center, leadMember);
                        if (member == null) member = leadMember;
                    }
                }
                if (onTruck && truck != null) car.Chase(truck);
                else if (member != null) car.Chase(member);
                else car.ClearTarget();
            }
        }

        bool TruckMoving
        {
            get
            {
                if (truck == null) return false;
                Vector3 v = truck.Velocity;
                v.y = 0f;
                return v.magnitude > TruckMovingSpeed;
            }
        }

        bool AnyAboard()
        {
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
                if (Free(all[i]) && truck.IsAboard(all[i])) return true;
            return false;
        }

        CrewMember NearestOnFoot(Vector3 from, CrewMember except)
        {
            CrewMember best = null;
            float bestSq = float.MaxValue;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == except || !Free(m) || OnBoard(m)) continue;
                float d = (m.Position - from).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = m; }
            }
            return best;
        }

        // ---- arrests, interception, the exit ----

        void CheckArrests(GameSession s, GameLoopNumbers n, float dt)
        {
            byte pending = 0;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (!Free(m)) continue;
                int k = m.index;
                if (OnBoard(m)) { zoneFor[k] = 0f; continue; }
                Vector3 p = m.Position + Vector3.up * BodyCentreHeight;
                bool struck = false, inZone = false;
                for (int c = 0; c < dispatched && c < cars.Count; c++)
                {
                    var car = cars[c];
                    if (car == null) continue;
                    if (car.Speed > n.arrestCarMinSpeed && car.DistanceTo(p) <= n.arrestRadius) struck = true;
                    if (car.IsStopped && FlatDistance(car.Center, p) <= n.officerZoneRadius) inZone = true;
                }
                if (!struck && inZone)
                {
                    zoneFor[k] += dt;
                    if (zoneFor[k] < n.officerZoneSeconds) { pending |= (byte)(1 << k); continue; }
                }
                else if (!struck) { zoneFor[k] = 0f; continue; }
                zoneFor[k] = 0f;
                s.Arrest(m);
                if (Session.IsOver) return;
            }
            s.SetArrestPending(pending);
        }

        void CheckInterception(GameSession s, GameLoopNumbers n, float dt)
        {
            bool blocked = false;
            if (Session.Phase == MissionPhase.PoliceHere && truck != null && truck.SpeedKmh < n.interceptTruckMaxKmh)
            {
                for (int c = 0; c < dispatched && c < cars.Count; c++)
                {
                    var car = cars[c];
                    if (car != null && !car.IsStunned && HullDistance(car) <= n.interceptRadius) { blocked = true; break; }
                }
            }
            if (!blocked)
            {
                interceptFor = 0f;
                s.SetInterceptWarning(-1f);
                return;
            }
            interceptFor += dt;
            float left = n.interceptSeconds - interceptFor;
            if (left <= 0f) { s.FailIntercepted(false); return; }
            s.SetInterceptWarning(left);   // sent on the first second only (the edge)
        }

        void CheckExit(GameSession s, GameLoopNumbers n)
        {
            var cp = Checkpoint;
            if (cp == null || truck == null) return;
            Vector3 at = truck.Body != null ? truck.Body.worldCenterOfMass : truck.transform.position;
            if (!cp.Contains(at)) return;
            int aboard = 0, count = 0;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (!Free(m) || !truck.IsAboard(m)) continue;
                aboard |= 1 << m.index;
                count++;
            }
            if (count < Mathf.Max(1, n.minCrewAboard)) return;
            s.CompleteEscape(truck.DriverActor, at, aboard);
        }

        // Metres from a car's box to the truck's hull.
        float HullDistance(PoliceCar car)
        {
            Transform t = truck.transform;
            Bounds hull = truck.Hull;
            Vector3 local = t.InverseTransformPoint(car.Center);
            Vector3 closest = t.TransformPoint(new Vector3(Mathf.Clamp(local.x, hull.min.x, hull.max.x),
                                                           Mathf.Clamp(local.y, hull.min.y, hull.max.y),
                                                           Mathf.Clamp(local.z, hull.min.z, hull.max.z)));
            return car.DistanceTo(closest);
        }

        // A crew member of this scene, not arrested.
        bool Free(CrewMember m) =>
            m != null && m.index >= 0 && m.index < 8 && m.gameObject.scene == gameObject.scene && !Session.IsArrested(m.index);

        // In a seat, or in the cargo box of a stopped truck (TruckVehicle.IsAboard).
        bool OnBoard(CrewMember m) => m.IsDriving || (truck != null && truck.IsAboard(m));

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
