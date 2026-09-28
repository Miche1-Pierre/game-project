using UnityEngine;

namespace Movers
{
    // The moving truck, driven (ADR-009 point 7). Arcade handling on real physics: one rigid body
    // for the whole truck, four WheelColliders, and the cargo riding loose in the box. There is
    // no load button and no inventory: what is still in the box after the braking is what gets
    // delivered.
    //
    // The driver's CrewInput steers it (VehicleSeat hands it over): Move y is throttle, brake and
    // reverse, Move x steers, Jump is the handbrake. With nobody at the wheel the engine idles and
    // the parking brake holds, so a crew member leaning on the bumper does not roll 3.5 t of truck
    // down the street.
    //
    // The root sits on the ground: its local y = 0 is the road under the wheels, and local +z is
    // the way the cab faces. The integration recipe (INTEGRATION.md, TRUCK) builds it that way.
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class TruckVehicle : MonoBehaviour
    {
        [Header("Wiring")]
        public WheelCollider frontLeft;
        public WheelCollider frontRight;
        public WheelCollider rearLeft;
        public WheelCollider rearRight;
        public TruckRamp ramp;              // slides in under the bed while someone drives
        public TruckCargo cargo;            // found in the children when empty

        [Header("Body")]
        public float mass = 3500f;
        // Low on purpose. A real box truck carries its weight about a metre up and tips over in a
        // hard turn; this one must not (rig: 0.3 degrees of roll at full lock at the speed cap).
        public float centerOfMassHeight = 0.6f;
        public float centerOfMassForward = 0f;      // metres ahead of the midpoint between the axles
        // A bumper pushed into a wall backs out of it slowly instead of being fired out.
        public float maxDepenetrationSpeed = 3f;

        [Header("Wheels and suspension")]
        public float wheelRadius = 0.552f;          // the Truck_Body wheel meshes
        public float wheelMass = 50f;
        public float suspensionDistance = 0.3f;
        // Per wheel. Unity sizes each wheel's sprung mass from the body, so empty the truck rests at
        // suspensionTarget; 2.5 t of cargo on top sinks it about 6 cm more, short of the stop.
        public float springRate = 110000f;
        public float damperRate = 10000f;
        [Range(0f, 1f)] public float suspensionTarget = 0.5f;
        // Tyre forces act this far above the contact patch. Close to the centre of mass height the
        // tyres hardly roll the body in a turn: the free half of the anti-roll.
        public float forceAppPointDistance = 0.45f;
        // The other half: N per unit of travel difference between the left and right wheel.
        public float antiRollForce = 15000f;
        public float forwardGrip = 1.5f;
        public float sidewaysGrip = 1.6f;

        [Header("Engine and brakes")]
        public float maxSpeedKmh = 40f;             // a speed cap is the first defence against cargo tunnelling
        public float maxReverseKmh = 12f;
        // Nm per rear wheel. Rig, empty: 4.8 m in the first 2 s, 38.7 km/h top (the governor).
        public float motorTorque = 2500f;
        public float reverseTorque = 1800f;         // rig: 11.8 km/h top in reverse
        public float brakeTorque = 3000f;           // per wheel. Rig: 9.2 m to stop from the cap
        public float handbrakeTorque = 6000f;       // rear wheels
        public float handbrakeSidewaysGrip = 0.7f;  // the rear lets go a little: the handbrake swings the tail
        public float coastBrakeTorque = 150f;       // engine braking with the pedal up
        public float holdBrakeTorque = 3000f;       // parked, or stopped with no pedal: it stays put on a slope

        [Header("Steering")]
        public float maxSteerSlow = 35f;            // degrees at a standstill
        public float maxSteerFast = 10f;            // degrees at the speed cap
        public float steerRate = 120f;              // degrees per second: the keyboard snaps, the wheels do not

        [Header("Safety and noise")]
        // The map has no edge wall. Below this height the truck has left the world: put it back
        // where it started, with whoever is at the wheel, and let the cargo it lost stay lost.
        public float fallResetY = -10f;
        public float crashNoiseSpeed = 2.5f;        // closing speed (m/s) before a hit makes a sound

        [Header("Crew in the way")]
        public CrewBumper crewBumper = new CrewBumper();

        public CrewMember Driver { get; private set; }
        public int DriverActor => Driver != null ? Driver.index : Actors.World;
        public Rigidbody Body => rb;
        public float ForwardSpeed { get; private set; }     // m/s along the cab direction, negative in reverse
        public float SpeedKmh => Velocity.magnitude * 3.6f;
        // Online co-op (NETCODE_SLICE 11.7): on the client the body is kinematic and these are
        // the host's values (TruckSync State); elsewhere the simulation's.
        public Vector3 Velocity => !Net.HasAuthority ? replicaVelocity : rb != null ? rb.linearVelocity : Vector3.zero;
        public float Pedal => pedalNow;          // -1..1, the pedal of the last physics step
        public bool Handbrake => handbrakeNow;
        public float SteerAngle { get; private set; }
        // Driving off with the ramp down would drag a 5 m plank along the road.
        public bool CanDrive => ramp == null || ramp.IsStowed;

        // The truck's own solid shape (bed, walls, roof, chassis, cab) as one box in the truck's
        // space: not the ramp (its own body), the wheels or the cargo trigger. The seat looks for
        // exit spots round it, the crew bumper for whoever is about to be hit.
        public Bounds Hull
        {
            get
            {
                if (!hullMeasured) MeasureHull();
                return hull;
            }
        }

        Rigidbody rb;
        Bounds hull;
        bool hullMeasured;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        WheelFrictionCurve rearSideways, rearSidewaysHandbrake;
        bool handbrakeGrip;
        float nextNoiseTime;
        bool wheelsReady;
        float pedalNow;
        bool handbrakeNow;
        Vector3 replicaVelocity;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (cargo == null) cargo = GetComponentInChildren<TruckCargo>(true);
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            wheelsReady = frontLeft != null && frontRight != null && rearLeft != null && rearRight != null;
            if (!wheelsReady)
                Debug.LogWarning("[TruckVehicle] " + name + " is missing a WheelCollider: it will not drive.");

            ConfigureBody();
            if (wheelsReady) ConfigureWheels();
        }

        void Start()
        {
            // Here rather than lazily in a physics step: parked, the colliders sit where they
            // were authored.
            if (!hullMeasured) MeasureHull();
        }

        void MeasureHull()
        {
            if (rb == null) return;     // before Awake: stay unmeasured and try again later
            hullMeasured = true;
            hull = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                // A switched-off collider reports empty bounds at the world origin.
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (c.isTrigger || c is WheelCollider || c.attachedRigidbody != rb) continue;
                // A box's own corners, so the result is exact whatever way the truck faces. Any
                // other shape falls back on its world bounds.
                var b = c as BoxCollider;
                Bounds wb = c.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    Vector3 corner = b != null
                        ? b.transform.TransformPoint(b.center + Vector3.Scale(b.size * 0.5f, sign))
                        : wb.center + Vector3.Scale(wb.extents, sign);
                    Vector3 local = transform.InverseTransformPoint(corner);
                    if (first) { hull = new Bounds(local, Vector3.zero); first = false; }
                    else hull.Encapsulate(local);
                }
            }
        }

        void ConfigureBody()
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;   // the chase camera and the driver ride on it
            // Continuous on the truck lets the light cargo, switched to ContinuousDynamic while the
            // truck moves (TruckCargo), sweep against the box walls instead of passing through them.
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.maxDepenetrationVelocity = maxDepenetrationSpeed;
            float midZ = wheelsReady ? (AxleZ(frontLeft, frontRight) + AxleZ(rearLeft, rearRight)) * 0.5f : 0f;
            rb.centerOfMass = new Vector3(0f, centerOfMassHeight, midZ + centerOfMassForward);
        }

        void ConfigureWheels()
        {
            Setup(frontLeft);
            Setup(frontRight);
            Setup(rearLeft);
            Setup(rearRight);

            // More solver substeps at walking pace, where WheelColliders jitter the most.
            frontLeft.ConfigureVehicleSubsteps(5f, 12, 15);

            rearSideways = rearLeft.sidewaysFriction;
            rearSidewaysHandbrake = rearSideways;
            rearSidewaysHandbrake.stiffness = handbrakeSidewaysGrip;
        }

        void Setup(WheelCollider w)
        {
            w.radius = wheelRadius;
            w.mass = wheelMass;
            w.center = Vector3.zero;
            w.suspensionDistance = suspensionDistance;
            w.forceAppPointDistance = forceAppPointDistance;
            w.suspensionSpring = new JointSpring { spring = springRate, damper = damperRate, targetPosition = suspensionTarget };
            w.forwardFriction = Curve(0.4f, 1f, 0.8f, 0.5f, forwardGrip);
            w.sidewaysFriction = Curve(0.2f, 1f, 0.5f, 0.75f, sidewaysGrip);

            // Hang the wheel so the truck rests at its authored height whatever the suspension numbers.
            // The spring carries the wheel's share of the body at suspensionTarget, so the wheel
            // centre sits wheelRadius above the root when the spring is that far in. Measured on a
            // rig built from this truck's own colliders in an editor preview scene: springs at 49 %
            // of their travel, the root 3 mm below its authored height. Only checked at the default
            // target of 0.5, where Unity's two readings of targetPosition agree.
            Vector3 local = transform.InverseTransformPoint(w.transform.position);
            local.y = wheelRadius + suspensionDistance * (1f - suspensionTarget);
            w.transform.SetPositionAndRotation(transform.TransformPoint(local), transform.rotation);
        }

        static WheelFrictionCurve Curve(float extremumSlip, float extremumValue, float asymptoteSlip, float asymptoteValue, float stiffness)
        {
            return new WheelFrictionCurve
            {
                extremumSlip = extremumSlip,
                extremumValue = extremumValue,
                asymptoteSlip = asymptoteSlip,
                asymptoteValue = asymptoteValue,
                stiffness = stiffness
            };
        }

        float AxleZ(WheelCollider a, WheelCollider b)
        {
            return (transform.InverseTransformPoint(a.transform.position).z + transform.InverseTransformPoint(b.transform.position).z) * 0.5f;
        }

        // On board for the police flee (EscapeMission): seated in one of its seats, or standing in
        // the cargo box while the truck is nearly stopped.
        public bool IsAboard(CrewMember m) => m != null && Driver == m;

        // In the driving seat. CrewMember.IsDriving means "seated in the truck" (driver or passenger).
        public static bool IsAtWheel(CrewMember m) => m != null && m.IsDriving;

        // VehicleSeat calls this when someone sits down (member) or gets out (null).
        public void SetDriver(CrewMember member)
        {
            Driver = member;
            if (rb != null) rb.WakeUp();
            // The ramp goes up at once. It comes back down when the truck is empty and still (FixedUpdate).
            if (member != null && ramp != null && Net.HasAuthority) ramp.Stow();   // the client's ramp follows TruckSync
        }

        // A teleport, for the fall guard and the tests. Loose cargo stays where it was: it is not
        // parented, which is the whole point of physical cargo.
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            SteerAngle = 0f;
            rb.WakeUp();
            NetTransforms.Snap(gameObject);   // online host: the next sample teleports
        }

        // Online client: the host's driving state, for the wheels, the sound and the HUD.
        public void ApplyReplica(float forwardSpeed, float steer, float pedal, bool handbrake, Vector3 velocity)
        {
            if (Net.HasAuthority) return;
            ForwardSpeed = forwardSpeed;
            SteerAngle = steer;
            pedalNow = Mathf.Clamp(pedal, -1f, 1f);
            handbrakeNow = handbrake;
            replicaVelocity = velocity;
        }

        void FixedUpdate()
        {
            if (!Net.HasAuthority) return;   // the client's truck is kinematic, posed by the host
            if (rb.position.y < fallResetY)
            {
                PlaceAt(spawnPosition, spawnRotation);
                return;
            }
            if (!wheelsReady) return;

            ForwardSpeed = Vector3.Dot(rb.linearVelocity, rb.rotation * Vector3.forward);

            // Driver is a UnityEngine.Object: a destroyed crew member reads as nobody at the wheel.
            bool driving = Driver != null && CanDrive;
            Vector2 move = Vector2.zero;
            bool handbrake = false;
            if (driving && Driver.Input != null)
            {
                move = Driver.Input.Move;
                handbrake = Driver.Input.Held(CrewButton.Jump);
            }
            pedalNow = move.y;
            handbrakeNow = handbrake;

            // A body that went to sleep while parked does not wake for wheel torque on its own.
            if (driving && (Mathf.Abs(move.y) > 0.05f || Mathf.Abs(move.x) > 0.05f) && rb.IsSleeping()) rb.WakeUp();

            Steer(move.x);
            DriveAndBrake(driving, move.y, handbrake);
            AntiRoll(frontLeft, frontRight);
            AntiRoll(rearLeft, rearRight);
            crewBumper.Tick(rb, Hull);

            // Parked with nobody at the wheel: the ramp comes back down for the crew.
            if (Driver == null && ramp != null && ramp.IsStowed && rb.linearVelocity.sqrMagnitude < 0.09f)
                ramp.Deploy();
        }

        void Steer(float input)
        {
            float speed01 = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / (maxSpeedKmh / 3.6f));
            float limit = Mathf.Lerp(maxSteerSlow, maxSteerFast, speed01);
            SteerAngle = Mathf.MoveTowards(SteerAngle, input * limit, steerRate * Time.fixedDeltaTime);
            frontLeft.steerAngle = SteerAngle;
            frontRight.steerAngle = SteerAngle;
        }

        // One pedal, arcade style: forward accelerates, or brakes while rolling back; back brakes
        // while rolling forward, then reverses. Rear-wheel drive.
        void DriveAndBrake(bool driving, float pedal, bool handbrake)
        {
            float v = ForwardSpeed;
            float motor = 0f, brake = 0f;

            if (!driving) brake = holdBrakeTorque;
            else if (pedal > 0.05f)
            {
                if (v < -0.5f) brake = brakeTorque * pedal;
                else motor = motorTorque * pedal * Governor(v, maxSpeedKmh / 3.6f);
            }
            else if (pedal < -0.05f)
            {
                if (v > 0.5f) brake = brakeTorque * -pedal;
                else motor = -reverseTorque * -pedal * Governor(-v, maxReverseKmh / 3.6f);
            }
            else brake = Mathf.Abs(v) < 0.4f ? holdBrakeTorque : coastBrakeTorque;

            // Over the cap (downhill, or shoved by something): the engine holds it back.
            float cap = (v >= 0f ? maxSpeedKmh : maxReverseKmh) / 3.6f;
            if (Mathf.Abs(v) > cap + 0.5f) brake = Mathf.Max(brake, coastBrakeTorque * 4f);

            bool pulled = driving && handbrake;
            rearLeft.motorTorque = motor;
            rearRight.motorTorque = motor;
            frontLeft.brakeTorque = brake;
            frontRight.brakeTorque = brake;
            float rearBrake = pulled ? Mathf.Max(brake, handbrakeTorque) : brake;
            rearLeft.brakeTorque = rearBrake;
            rearRight.brakeTorque = rearBrake;

            if (pulled != handbrakeGrip)
            {
                handbrakeGrip = pulled;
                rearLeft.sidewaysFriction = pulled ? rearSidewaysHandbrake : rearSideways;
                rearRight.sidewaysFriction = pulled ? rearSidewaysHandbrake : rearSideways;
            }
        }

        // Full torque until 85 % of the cap, then fading to nothing at the cap.
        static float Governor(float speed, float cap)
        {
            return Mathf.Clamp01((cap - speed) / (cap * 0.15f));
        }

        // An anti-roll bar: the wheel whose spring is more extended is pulled down, the other one
        // pushed up, in proportion to the difference.
        void AntiRoll(WheelCollider left, WheelCollider right)
        {
            WheelHit hit;
            float travelL = 1f, travelR = 1f;
            bool groundedL = left.GetGroundHit(out hit);
            if (groundedL) travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;
            bool groundedR = right.GetGroundHit(out hit);
            if (groundedR) travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;

            float force = (travelL - travelR) * antiRollForce;
            if (groundedL) rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (groundedR) rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }

        // The truck hitting the world makes a sound, and ImpactAudio turns every sound into a
        // LoudNoise event: backing into the grandmother's car is heard in the kitchen. Cargo
        // knocking about is left to the cargo's own sounds.
        //
        // The noise names the driver (World when nobody drives), through DESTRUCTION's
        // ImpactAudio.Play overload with an instigator.
        void OnCollisionEnter(Collision c)
        {
            if (!Net.HasAuthority) return;
            if (c.contactCount == 0 || Time.time < nextNoiseTime) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < crashNoiseSpeed) return;
            var other = c.rigidbody;
            if (other != null && other.TryGetComponent(out MovableObject _)) return;
            // A crew member the bumper could not shove clear (pinned against a wall): the truck
            // stops against him, and that is not a crash.
            if (other == null && c.collider is CharacterController && CrewRoster.Owner(c.collider.transform) != null) return;

            nextNoiseTime = Time.time + 0.3f;
            ImpactAudio.Play(speed > 6f ? ImpactAudio.Kind.Crunch : ImpactAudio.Kind.Thud,
                             c.GetContact(0).point, Mathf.Clamp01(speed / 10f), DriverActor);
        }
    }
}
