using UnityEngine;

namespace Movers
{
    // Every number of the truck's driving, ram and feel in one asset (ADR-013, 03_TECHNICAL/
    // DEV2_DESTRUCTION_GAMEPLAY.md 6.1), assigned on TruckVehicle. Without an asset the component's
    // own fields are the fallback, so a scene never breaks; with it, the asset wins. The code
    // defaults are the DEV 2 tuning (90 km/h, a power-limited engine), first guesses to confirm
    // in Play (ADR-009).
    [CreateAssetMenu(menuName = "Movers/Truck Tuning", fileName = "TruckTuning")]
    public sealed class TruckTuning : ScriptableObject
    {
        [Header("Engine")]
        [Tooltip("km/h. The governor fades the drive over the last governorBand share below it.")]
        public float maxSpeedKmh = 90f;
        [Range(0f, 1f)] public float governorBand = 0.05f;
        [Tooltip("Nm per rear wheel: the launch, until the power limit takes over.")]
        public float motorTorque = 5200f;
        [Tooltip("kW for the two driven wheels: the drive force tapers as 1 / speed above the torque limit.")]
        public float enginePowerKw = 260f;
        public float maxReverseKmh = 18f;
        public float reverseTorque = 3500f;

        [Header("Resistance")]
        public float linearDamping = 0.01f;
        [Tooltip("N per (m/s)^2 along the velocity.")]
        public float aeroDrag = 5f;
        [Tooltip("Share of the weight, along the velocity.")]
        public float rollingResistance = 0.012f;

        [Header("Brakes")]
        [Tooltip("m/s^2 of deceleration the brakes aim for, whatever the load.")]
        public float brakeDecel = 7.5f;
        [Range(0f, 1f)] public float brakeBiasFront = 0.6f;
        [Tooltip("A wheel slipping more than this has its brake released for the step (ABS).")]
        public float absSlip = 0.4f;
        public float handbrakeTorque = 6000f;
        public float handbrakeSidewaysGrip = 0.7f;
        public float coastBrakeTorque = 300f;
        public float holdBrakeTorque = 3000f;

        [Header("Steering and grip")]
        [Tooltip("m/s^2. The steering limit keeps the lateral acceleration under this: no slide at full lock.")]
        public float maxLateralAccel = 6.5f;
        public float maxSteerSlow = 35f;
        public float maxSteerFast = 2.5f;
        public float steerRate = 120f;
        public float forwardGrip = 1.5f;
        public float sidewaysGrip = 1.3f;

        [Header("Wheel substeps")]
        public float substepSpeedThreshold = 5f;
        public int substepsBelow = 12;
        public int substepsAbove = 20;

        [Header("Recovery")]
        [Tooltip("Tilted so its up vector's height is under this, and nearly still for autoRightSeconds: set back on its wheels.")]
        public float autoRightUpDot = 0.3f;
        public float autoRightSeconds = 2f;

        [Header("Cargo and crew")]
        [Tooltip("m/s. Faster than this, riding cargo uses continuous collision detection.")]
        public float cargoCcdSpeed = 12f;
        [Tooltip("m/s. Standing in the cargo box counts as aboard only while the truck is slower than this.")]
        public float cargoAboardMaxSpeed = 2f;
        [Tooltip("m/s. A truck hitting the grandmother at least this fast runs her over; slower is a bump.")]
        public float runOverMinSpeed = 3f;

        [Header("Ram")]
        [Tooltip("m/s. The ram sweep runs above this speed.")]
        public float ramMinSpeed = 3f;
        [Tooltip("m. The sweep starts this far behind the hull and reaches this much further.")]
        public float ramSweepExtra = 0.3f;
        [Tooltip("m. Depth of the slab on the front face that catches what already touches the bumper.")]
        public float ramFrontSlab = 0.3f;
        [Tooltip("J of the truck's energy each HP it removes costs.")]
        public float ramJoulesPerHp = 80f;
        [Tooltip("s before the same collider can be rammed again.")]
        public float ramCooldown = 0.3f;

        [Header("Fling")]
        [Tooltip("kg. Lighter loose bodies in the path are flung out of it.")]
        public float flingMassKg = 80f;
        public float flingUpShare = 0.3f;
        public float flingLateralShare = 0.35f;
        [Tooltip("m/s at most.")]
        public float flingMaxSpeed = 35f;
        [Tooltip("s a flung body's colliders are left alone by the ram and by their own collision damage.")]
        public float flingHandledSeconds = 1f;

        [Header("Feel")]
        [Tooltip("m/s at which the engine sound and the road rumble peak.")]
        public float audioTopSpeed = 25f;
        [Tooltip("Virtual gears of the engine sound.")]
        public int gearBands = 4;
        [Tooltip("m the chase camera backs off at top speed.")]
        public float cameraExtraDistance = 2f;
        [Tooltip("Degrees of field of view added at top speed.")]
        public float cameraExtraFov = 8f;
        [Tooltip("J. A crash over this shakes the camera.")]
        public float crashShakeEnergy = 50000f;
    }
}
