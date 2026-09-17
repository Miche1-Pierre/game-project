using UnityEngine;

namespace Movers
{
    // Physics grab: the object stays a rigidbody and is dragged toward a hold point.
    // Heavy objects lag and resist (weight matters). This unwieldy feel IS the game.
    public class PlayerGrab : MonoBehaviour
    {
        public Transform cam;
        public PlayerController controller;

        public float grabRange = 3f;
        public float holdDistance = 2.2f;
        public float followStrength = 14f;
        public float maxSpeed = 9f;
        public float throwForce = 6f;
        public float maxSoloWeight = 60f; // above this the object gets very sluggish (needs 2 players, later)

        MovableObject held;
        float savedLinearDamping;
        float savedAngularDamping;

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (held == null) TryGrab();
                else Release(false);
            }
            if (Input.GetMouseButtonDown(1) && held != null) Release(true);

            // carrying something heavy slows you down
            controller.speedMultiplier = held == null
                ? 1f
                : Mathf.Clamp(1f - held.weight / 120f, 0.35f, 1f);
        }

        void TryGrab()
        {
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, grabRange))
            {
                var mo = hit.collider.GetComponentInParent<MovableObject>();
                if (mo != null)
                {
                    held = mo;
                    savedLinearDamping = held.rb.linearDamping;
                    savedAngularDamping = held.rb.angularDamping;
                    held.rb.useGravity = true;
                    held.rb.linearDamping = 6f;
                    held.rb.angularDamping = 6f;
                }
            }
        }

        void Release(bool thrown)
        {
            if (held == null) return;
            held.rb.linearDamping = savedLinearDamping;
            held.rb.angularDamping = savedAngularDamping;
            if (thrown) held.rb.AddForce(cam.forward * throwForce, ForceMode.VelocityChange);
            held = null;
        }

        void FixedUpdate()
        {
            if (held == null) return;
            Vector3 target = cam.position + cam.forward * holdDistance;
            Vector3 toTarget = target - held.rb.worldCenterOfMass;
            float weightFactor = Mathf.Clamp(maxSoloWeight / Mathf.Max(held.weight, 1f), 0.2f, 1f);
            Vector3 desired = Vector3.ClampMagnitude(toTarget * followStrength * weightFactor, maxSpeed);
            held.rb.linearVelocity = desired;
        }
    }
}
