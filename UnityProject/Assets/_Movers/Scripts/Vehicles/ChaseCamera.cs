using UnityEngine;

namespace Movers
{
    // The driver's view: behind and above the truck, swinging round after it with a little lag,
    // orbited by the driver's look input (mouse or right stick). Let go of the look while driving
    // and it drifts back behind the truck. A wall or a tree between the truck and the camera
    // pulls the camera in; loose objects (a flying chair) do not.
    //
    // Plain data plus one method, owned by VehicleSeat: it moves the driver's own camera, which
    // VehicleSeat gives back to the head when the driver gets out.
    [System.Serializable]
    public sealed class ChaseCamera
    {
        public float distance = 10f;
        public float pivotHeight = 2.4f;        // above the truck's ground point: about the middle of the box
        public float basePitch = 14f;           // degrees looking down
        public float minPitch = -5f;
        public float maxPitch = 60f;
        public float orbitSensitivity = 3f;     // degrees per look unit (mouse-axis units, as CrewInput gives them)
        public float followLag = 0.25f;         // seconds for the view to swing in behind the truck
        public float recenterDelay = 1.5f;      // seconds without look input before it drifts back
        public float recenterSpeed = 90f;       // degrees per second
        public float recenterMinSpeed = 2f;     // m/s: parked, it stays where the driver put it
        public float collisionRadius = 0.3f;

        [System.NonSerialized] float yaw, yawVelocity, orbitYaw, orbitPitch, lastLookTime;
        [System.NonSerialized] RaycastHit[] hits;     // made on first use: the serializer may skip initialisers

        // Where the camera looks, as a heading in degrees: the driver faces this way getting out.
        public float Yaw => yaw + orbitYaw;

        public void Begin(Transform truck)
        {
            yaw = Heading(truck.forward);
            yawVelocity = 0f;
            orbitYaw = 0f;
            orbitPitch = 0f;
            lastLookTime = -99f;
        }

        public void Tick(Transform cam, Transform truck, Vector3 truckVelocity, Vector2 look, float dt)
        {
            if (cam == null || truck == null) return;

            if (look.sqrMagnitude > 1e-6f) lastLookTime = Time.time;
            orbitYaw = Mathf.DeltaAngle(0f, orbitYaw + look.x * orbitSensitivity);
            orbitPitch = Mathf.Clamp(orbitPitch - look.y * orbitSensitivity, minPitch - basePitch, maxPitch - basePitch);
            if (Time.time - lastLookTime > recenterDelay && truckVelocity.sqrMagnitude > recenterMinSpeed * recenterMinSpeed)
            {
                orbitYaw = Mathf.MoveTowardsAngle(orbitYaw, 0f, recenterSpeed * dt);
                orbitPitch = Mathf.MoveTowards(orbitPitch, 0f, recenterSpeed * 0.5f * dt);
            }

            yaw = Mathf.SmoothDampAngle(yaw, Heading(truck.forward), ref yawVelocity, followLag, Mathf.Infinity, dt);
            Quaternion rotation = Quaternion.Euler(basePitch + orbitPitch, yaw + orbitYaw, 0f);
            Vector3 pivot = truck.position + Vector3.up * pivotHeight;
            Vector3 back = rotation * Vector3.back;

            float reach = distance;
            if (hits == null) hits = new RaycastHit[16];
            int n = Physics.SphereCastNonAlloc(pivot, collisionRadius, back, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || hits[i].distance <= 0f) continue;       // started inside it: the truck's own box
                if (c.transform.IsChildOf(truck)) continue;              // the truck, its ramp, its driver
                var body = c.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;         // loose objects never push the view
                // Online client: loose objects are kinematic replicas; registered bodies count as loose.
                if (body != null && NetIds.IdOf(body.gameObject) != 0) continue;
                if (hits[i].distance < reach) reach = hits[i].distance;
            }

            cam.SetPositionAndRotation(pivot + back * Mathf.Max(0.5f, reach - 0.1f), rotation);
        }

        static float Heading(Vector3 forward)
        {
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }
    }
}
