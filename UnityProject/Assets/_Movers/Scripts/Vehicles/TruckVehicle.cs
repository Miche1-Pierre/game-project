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
    //
    // DEV 2 (03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md 6): the driving numbers come from a
    // TruckTuning asset (90 km/h, a power-limited engine, brakes that stop it in the same distance
    // loaded or empty, steering limited by lateral grip). Without an asset the fields below are
    // the fallback, so an old scene drives as it did.
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class TruckVehicle : MonoBehaviour
    {
        [Tooltip("The driving, ram and feel numbers. Empty: the fields below, today's truck.")]
        public TruckTuning tuning;

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

        [Header("Engine and brakes (fallback when no tuning asset is set)")]
        public float maxSpeedKmh = 40f;             // a speed cap is the first defence against cargo tunnelling
        public float maxReverseKmh = 12f;
        // Nm per rear wheel. Rig, empty: 4.8 m in the first 2 s, 38.7 km/h top (the governor).
        public float motorTorque = 2500f;
        public float reverseTorque = 1800f;         // rig: 11.8 km/h top in reverse
        public float brakeTorque = 3000f;           // per wheel, empty. Rig: 9.2 m to stop from the cap
        public float handbrakeTorque = 6000f;       // rear wheels
        public float handbrakeSidewaysGrip = 0.7f;  // the rear lets go a little: the handbrake swings the tail
        public float coastBrakeTorque = 150f;       // engine braking with the pedal up
        public float holdBrakeTorque = 3000f;       // parked, or stopped with no pedal: it stays put on a slope

        [Header("Steering (fallback when no tuning asset is set)")]
        public float maxSteerSlow = 35f;            // degrees at a standstill
        public float maxSteerFast = 10f;            // degrees at speed, at least
        public float steerRate = 120f;              // degrees per second: the keyboard snaps, the wheels do not

        [Header("Safety and noise")]
        // The map has no edge wall. Below this height the truck has left the world: put it back
        // where it started, with whoever is at the wheel, and let the cargo it lost stay lost.
        public float fallResetY = -10f;
        public float crashNoiseSpeed = 2.5f;        // closing speed (m/s) before a hit makes a sound

        [Header("Crew in the way")]
        public CrewBumper crewBumper = new CrewBumper();

        // The asset, or the fallback built from the fields above.
        public TruckTuning Tuning => tuning != null ? tuning : Fallback();

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
        float wheelbase = 4f;
        float tippedFor;
        TruckTuning fallback;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (cargo == null) cargo = GetComponentInChildren<TruckCargo>(true);
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            wheelsReady = frontLeft != null && frontRight != null && rearLeft != null && rearRight != null;
            if (!wheelsReady)
                Debug.LogWarning("[TruckVehicle] " + name + " is missing a WheelCollider: it will not drive.");

            if (wheelsReady) wheelbase = Mathf.Max(1f, Mathf.Abs(AxleZ(frontLeft, frontRight) - AxleZ(rearLeft, rearRight)));
            ConfigureBody();
            if (wheelsReady) ConfigureWheels();
        }

        void OnDestroy()
        {
            if (fallback != null) Destroy(fallback);
        }

        // Today's truck as a tuning: what the component's own fields say, the resistances it
        // never had at zero, and the new systems (ram, auto-right, feel) at the asset's defaults.
        TruckTuning Fallback()
        {
            if (fallback != null) return fallback;
            fallback = ScriptableObject.CreateInstance<TruckTuning>();
            fallback.name = name + " (fallback tuning)";
            fallback.hideFlags = HideFlags.HideAndDontSave;
            fallback.maxSpeedKmh = maxSpeedKmh;
            fallback.governorBand = 0.15f;
            fallback.motorTorque = motorTorque;
            fallback.maxReverseKmh = maxReverseKmh;
            fallback.reverseTorque = reverseTorque;
            fallback.linearDamping = 0.05f;
            fallback.aeroDrag = 0f;
            fallback.rollingResistance = 0f;
            // The stop the fixed torque gave the empty truck, now whatever the load.
            fallback.brakeDecel = 4f * brakeTorque / Mathf.Max(1f, mass * wheelRadius);
            fallback.handbrakeTorque = handbrakeTorque;
            fallback.handbrakeSidewaysGrip = handbrakeSidewaysGrip;
            fallback.coastBrakeTorque = coastBrakeTorque;
            fallback.holdBrakeTorque = holdBrakeTorque;
            fallback.maxSteerSlow = maxSteerSlow;
            fallback.maxSteerFast = maxSteerFast;
            fallback.steerRate = steerRate;
            fallback.forwardGrip = forwardGrip;
            fallback.sidewaysGrip = sidewaysGrip;
            fallback.substepsAbove = 15;
            fallback.audioTopSpeed = 8f;
            return fallback;
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
            rb.linearDamping = Tuning.linearDamping;
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
            var t = Tuning;
            Setup(frontLeft, t);
            Setup(frontRight, t);
            Setup(rearLeft, t);
            Setup(rearRight, t);

            // More solver substeps at walking pace, where WheelColliders jitter the most, and at
            // speed, where one physics step is half a metre of road.
            frontLeft.ConfigureVehicleSubsteps(t.substepSpeedThreshold, t.substepsBelow, t.substepsAbove);

            rearSideways = rearLeft.sidewaysFriction;
            rearSidewaysHandbrake = rearSideways;
            rearSidewaysHandbrake.stiffness = t.handbrakeSidewaysGrip;
        }

        void Setup(WheelCollider w, TruckTuning t)
        {
            w.radius = wheelRadius;
            w.mass = wheelMass;
            w.center = Vector3.zero;
            w.suspensionDistance = suspensionDistance;
            w.forceAppPointDistance = forceAppPointDistance;
            w.suspensionSpring = new JointSpring { spring = springRate, damper = damperRate, targetPosition = suspensionTarget };
            w.forwardFriction = Curve(0.4f, 1f, 0.8f, 0.5f, t.forwardGrip);
            w.sidewaysFriction = Curve(0.2f, 1f, 0.5f, 0.75f, t.sidewaysGrip);

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

            var t = Tuning;
            ForwardSpeed = Vector3.Dot(rb.linearVelocity, rb.rotation * Vector3.forward);
            if (AutoRight(t)) return;

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

            Steer(move.x, t);
            DriveAndBrake(driving, move.y, handbrake, t);
            Resist(t);
            AntiRoll(frontLeft, frontRight);
            AntiRoll(rearLeft, rearRight);
            crewBumper.Tick(rb, Hull);

            // Parked with nobody at the wheel: the ramp comes back down for the crew.
            if (Driver == null && ramp != null && ramp.IsStowed && rb.linearVelocity.sqrMagnitude < 0.09f)
                ramp.Deploy();
        }

        // The lock that keeps the lateral acceleration under maxLateralAccel at this speed
        // (tan(angle) = a * wheelbase / v^2), clamped between the fast and the slow limits: full
        // lock never slides the truck at speed, and at a crawl it turns as tight as it can.
        void Steer(float input, TruckTuning t)
        {
            float v = Mathf.Abs(ForwardSpeed);
            float limit = v > 0.1f ? Mathf.Atan(t.maxLateralAccel * wheelbase / (v * v)) * Mathf.Rad2Deg : t.maxSteerSlow;
            limit = Mathf.Clamp(limit, Mathf.Min(t.maxSteerFast, t.maxSteerSlow), t.maxSteerSlow);
            SteerAngle = Mathf.MoveTowards(SteerAngle, input * limit, t.steerRate * Time.fixedDeltaTime);
            frontLeft.steerAngle = SteerAngle;
            frontRight.steerAngle = SteerAngle;
        }

        // One pedal, arcade style: forward accelerates, or brakes while rolling back; back brakes
        // while rolling forward, then reverses. Rear-wheel drive.
        //
        // The engine pushes each driven wheel with min(torque / r, half the power / v): a strong
        // launch that tapers as the speed grows, no gears in the physics. The brakes aim at a
        // deceleration, so their torque follows the mass (the truck and its load) and a stop
        // takes the same distance loaded or empty; a wheel slipping past absSlip lets go of the
        // service brake for the step (ABS).
        void DriveAndBrake(bool driving, float pedal, bool handbrake, TruckTuning t)
        {
            float v = ForwardSpeed;
            float motor = 0f, held = 0f, service = 0f;   // held: Nm per wheel; service: share of the full brake

            if (!driving) held = t.holdBrakeTorque;
            else if (pedal > 0.05f)
            {
                if (v < -0.5f) service = pedal;
                else motor = DriveTorque(v, t) * pedal * Governor(v, t.maxSpeedKmh / 3.6f, t.governorBand);
            }
            else if (pedal < -0.05f)
            {
                if (v > 0.5f) service = -pedal;
                else motor = -t.reverseTorque * -pedal * Governor(-v, t.maxReverseKmh / 3.6f, ReverseGovernorBand);
            }
            else held = Mathf.Abs(v) < 0.4f ? t.holdBrakeTorque : t.coastBrakeTorque;

            // Over the cap (downhill, or shoved by something): the engine holds it back.
            float cap = (v >= 0f ? t.maxSpeedKmh : t.maxReverseKmh) / 3.6f;
            if (Mathf.Abs(v) > cap + 0.5f) held = Mathf.Max(held, t.coastBrakeTorque * 4f);

            // The full service brake per wheel for the aimed deceleration, split front and rear.
            float full = t.brakeDecel * BrakingMass() * wheelRadius;
            float front = full * t.brakeBiasFront * 0.5f * service;
            float rear = full * (1f - t.brakeBiasFront) * 0.5f * service;

            bool pulled = driving && handbrake;
            float rearHeld = pulled ? Mathf.Max(held, t.handbrakeTorque) : held;
            rearLeft.motorTorque = motor;
            rearRight.motorTorque = motor;
            frontLeft.brakeTorque = Brake(frontLeft, held, front, t.absSlip);
            frontRight.brakeTorque = Brake(frontRight, held, front, t.absSlip);
            rearLeft.brakeTorque = Brake(rearLeft, rearHeld, rear, t.absSlip);
            rearRight.brakeTorque = Brake(rearRight, rearHeld, rear, t.absSlip);

            if (pulled != handbrakeGrip)
            {
                handbrakeGrip = pulled;
                rearLeft.sidewaysFriction = pulled ? rearSidewaysHandbrake : rearSideways;
                rearRight.sidewaysFriction = pulled ? rearSidewaysHandbrake : rearSideways;
            }
        }

        const float ReverseGovernorBand = 0.15f;

        // Nm on each driven wheel: the torque limit at a launch, the power limit above it.
        float DriveTorque(float v, TruckTuning t)
        {
            if (!(t.enginePowerKw > 0f)) return t.motorTorque;
            float byPower = t.enginePowerKw * 1000f * 0.5f / Mathf.Max(1f, v) * wheelRadius;
            return Mathf.Min(t.motorTorque, byPower);
        }

        // What the brakes stop: the body and what rides loose in the box.
        float BrakingMass() => rb.mass + (cargo != null ? cargo.LoadedKg : 0f);

        // ABS: the service brake lets go of a wheel that slips; the parking, coast and hand
        // brakes do not.
        static float Brake(WheelCollider w, float held, float service, float absSlip)
        {
            if (service > 0f && absSlip > 0f && w.GetGroundHit(out WheelHit hit) && Mathf.Abs(hit.forwardSlip) > absSlip)
                service = 0f;
            return Mathf.Max(held, service);
        }

        // Full drive until the last 'band' share of the cap, then fading to nothing at the cap.
        static float Governor(float speed, float cap, float band)
        {
            return Mathf.Clamp01((cap - speed) / (cap * Mathf.Max(0.01f, band)));
        }

        // Air and rolling resistance along the velocity: the top speed and the coast-down come
        // from here rather than from a big damping.
        void Resist(TruckTuning t)
        {
            Vector3 vel = rb.linearVelocity;
            float speed = vel.magnitude;
            if (speed < 0.05f) return;
            float rolling = t.rollingResistance * rb.mass * -Physics.gravity.y * Mathf.Clamp01(speed / 0.5f);
            float drag = t.aeroDrag * speed * speed;
            rb.AddForce(vel * (-(drag + rolling) / speed), ForceMode.Force);
        }

        // On its side or its roof and nearly still for a while (a grenade, a ditch): set back on
        // its wheels a metre up, facing the way it was heading. Host only, like all driving.
        bool AutoRight(TruckTuning t)
        {
            bool tipped = Vector3.Dot(rb.rotation * Vector3.up, Vector3.up) < t.autoRightUpDot
                          && rb.linearVelocity.sqrMagnitude < 1f;
            tippedFor = tipped ? tippedFor + Time.fixedDeltaTime : 0f;
            if (tippedFor < t.autoRightSeconds) return false;
            tippedFor = 0f;
            Vector3 heading = rb.rotation * Vector3.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f)
            {
                heading = rb.rotation * Vector3.up;   // on its nose or its tail
                heading.y = 0f;
            }
            Quaternion yaw = heading.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(heading.normalized, Vector3.up) : Quaternion.identity;
            PlaceAt(rb.position + Vector3.up, yaw);
            return true;
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
