using UnityEngine;

namespace Movers
{
    // Turns the truck's wheel meshes: each one rolls with the WheelCollider nearest to it, and
    // the ones matched to a front collider follow the steering. The model has eight wheels
    // (a twin front axle, twin rear tyres) for four colliders, so the meshes are matched to the
    // colliders by position instead of one to one. Suspension travel is not shown: a few
    // centimetres on a greybox truck.
    [DisallowMultipleComponent]
    public sealed class TruckWheelVisuals : MonoBehaviour
    {
        public TruckVehicle vehicle;
        public Transform[] wheels;          // mesh pivots at the wheel centres (Truck_Body's WheelT1-*)

        Quaternion[] rest;                  // each mesh's rotation in the truck's space, at rest
        WheelCollider[] source;
        bool[] steers;
        float[] angle;

        void Start()
        {
            if (vehicle == null) vehicle = GetComponentInParent<TruckVehicle>();
            if (vehicle == null || wheels == null || vehicle.frontLeft == null || vehicle.frontRight == null
                || vehicle.rearLeft == null || vehicle.rearRight == null)
            {
                enabled = false;
                return;
            }

            Transform root = vehicle.transform;
            var colliders = new[] { vehicle.frontLeft, vehicle.frontRight, vehicle.rearLeft, vehicle.rearRight };
            rest = new Quaternion[wheels.Length];
            source = new WheelCollider[wheels.Length];
            steers = new bool[wheels.Length];
            angle = new float[wheels.Length];

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                rest[i] = Quaternion.Inverse(root.rotation) * wheels[i].rotation;
                Vector3 p = root.InverseTransformPoint(wheels[i].position);
                float best = float.MaxValue;
                for (int c = 0; c < colliders.Length; c++)
                {
                    Vector3 q = root.InverseTransformPoint(colliders[c].transform.position);
                    float d = (p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z);
                    if (d < best) { best = d; source[i] = colliders[c]; }
                }
                steers[i] = source[i] == vehicle.frontLeft || source[i] == vehicle.frontRight;
            }
        }

        void LateUpdate()
        {
            if (vehicle == null) return;
            Transform root = vehicle.transform;
            Quaternion rootRotation = root.rotation;
            float steer = vehicle.SteerAngle;
            float dt = Time.deltaTime;
            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                if (w == null || source[i] == null) continue;
                // rpm is positive rolling forward; a positive turn about the truck's right axis
                // moves the top of the wheel forward.
                angle[i] = Mathf.Repeat(angle[i] + source[i].rpm * 6f * dt, 360f);
                w.rotation = rootRotation
                           * Quaternion.AngleAxis(steers[i] ? steer : 0f, Vector3.up)
                           * Quaternion.AngleAxis(angle[i], Vector3.right)
                           * rest[i];
            }
        }
    }
}
