using UnityEngine;

namespace Movers
{
    // The moving truck of the title screen, driving the country road on a loop: from one end to
    // the other (`reverse`: from the far end over the hills towards the camera), a short wait
    // out of sight, and again.
    // Pure animation (no physics, no TruckVehicle): it follows the road by distance, slows in
    // the bends, pitches with the slope, bobs a little on its springs and turns its wheels at
    // the speed it drives.
    //
    // Its visual is the box truck of Map01's MovingTruck (Truck_1_Blue cab and the box), built
    // under this object by the scene setup with the truck's front along +Z.
    [DisallowMultipleComponent]
    public sealed class TruckOnRoad : MonoBehaviour
    {
        public RoadPath road;
        [Tooltip("Cruising speed, m/s (8 is about 30 km/h: a loaded truck on a country road).")]
        public float speed = 8f;
        [Tooltip("Slowest speed in the tightest bend, as a share of the cruising speed.")]
        [Range(0.2f, 1f)] public float bendSlowdown = 0.55f;
        [Tooltip("Metres to the right of the centre line, in its own direction: it keeps to its lane.")]
        public float lane = 1.25f;
        [Tooltip("Drive the road from its last point to its first.")]
        public bool reverse;
        [Tooltip("Distance between the front and rear axles, for the pitch.")]
        public float wheelbase = 4f;
        [Tooltip("Where the first lap starts, metres driven: the first pass comes soon after the menu opens. Later laps start at the far end.")]
        public float startDistance = 0f;
        [Tooltip("Where it leaves the road's far end and waits, hidden, before starting again.")]
        public float endMargin = 2f;
        public float waitAtEnd = 3f;
        [Tooltip("Wheel meshes to turn (the cab's WheelT1-*).")]
        public Transform[] wheels = new Transform[0];
        public float wheelRadius = 0.5f;
        [Tooltip("The body that bobs (the cab and box), a child of this object; empty: nothing bobs.")]
        public Transform body;
        public float bob = 0.03f;

        public float Distance { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float WheelAngle => wheelAngle;
        public int Laps { get; private set; }

        Quaternion[] rest;
        float wheelAngle;
        float waiting;
        Vector3 bodyRest;
        float bobPhase;

        void Start()
        {
            Distance = startDistance;
            rest = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) rest[i] = Quaternion.Inverse(transform.rotation) * wheels[i].rotation;
            if (body != null) bodyRest = body.localPosition;
            Place();
        }

        void Update()
        {
            if (road == null || !road.Ready) return;
            float dt = Time.deltaTime;

            if (waiting > 0f)
            {
                waiting -= dt;
                CurrentSpeed = 0f;
                if (waiting <= 0f)
                {
                    Distance = 0f;
                    Laps++;
                    Place();
                }
                return;
            }

            // Slower where the road turns: compare the heading a few metres ahead and behind.
            road.Sample(At(Distance) - 6f, 0f, out _, out Vector3 behind);
            road.Sample(At(Distance) + 6f, 0f, out _, out Vector3 ahead);
            float bend = Vector3.Angle(Flat(behind), Flat(ahead));
            float target = speed * Mathf.Lerp(1f, bendSlowdown, Mathf.Clamp01(bend / 50f));
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, target, 2.5f * dt);

            Distance += CurrentSpeed * dt;
            if (Distance >= road.Length - endMargin)
            {
                waiting = waitAtEnd;
                return;
            }
            Place();

            // Wheels: the arc driven over the radius.
            wheelAngle = Mathf.Repeat(wheelAngle + CurrentSpeed * dt / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg, 360f);
            Quaternion roll = Quaternion.AngleAxis(wheelAngle, Vector3.right);
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].rotation = transform.rotation * roll * rest[i];

            if (body != null)
            {
                bobPhase += dt * (1.3f + CurrentSpeed * 0.25f);
                body.localPosition = bodyRest + Vector3.up * (Mathf.Sin(bobPhase * 2.1f) * 0.6f + Mathf.Sin(bobPhase * 3.7f) * 0.4f) * bob;
            }
        }

        // Puts the truck at a point of its lap at once (a test, a capture).
        public void JumpTo(float driven)
        {
            waiting = 0f;
            Distance = Mathf.Max(0f, driven);
            CurrentSpeed = speed;
            Place();
        }

        // Where along the road the truck is, in the road's own direction.
        float At(float driven) => reverse ? road.Length - driven : driven;

        // On the road at the current distance, level with its two axles.
        void Place()
        {
            if (road == null || !road.Ready) return;
            float side = reverse ? -lane : lane;
            road.Sample(At(Distance + wheelbase * 0.5f), side, out Vector3 front, out _);
            road.Sample(At(Distance - wheelbase * 0.5f), side, out Vector3 rear, out _);
            Vector3 fwd = front - rear;
            if (fwd.sqrMagnitude < 1e-4f) return;
            transform.SetPositionAndRotation((front + rear) * 0.5f, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
