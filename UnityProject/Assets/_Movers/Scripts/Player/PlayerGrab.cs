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
        public float followStrength = 10f;   // lowered from 14: more lag reads as more weight
        public float maxSpeed = 9f;
        public float throwForce = 6f;
        public float maxSoloWeight = 60f; // above this the object gets very sluggish (needs 2 players, later)

        [Header("Weight feel")]
        // Playtest 2026-09-17: the carry read as slightly too floaty. The cause is that
        // FixedUpdate assigns linearVelocity outright, which cancels gravity and inertia, so
        // the object hangs at eye level with no mass. Rather than rewrite the control model
        // (the carry itself was judged correct), heavy objects now hang lower and lag more.
        public float sagPerKg = 0.006f;        // hold point drops this much per kg
        public float maxSag = 0.7f;            // a fridge ends up near the floor, not through it
        public float carriedLinearDamping = 6f;
        public float carriedAngularDamping = 2.5f;  // was 6, which froze all sway

        [Header("Rotate the held object")]
        // GREYBOX_SPEC names one emergent problem: the sofa is wider than the interior door.
        // With no way to turn it, that problem has exactly one answer (walk around), which is
        // not a problem, it is a wall. Holding rotateKey hands the mouse to the object.
        public KeyCode rotateKey = KeyCode.R;
        public float rotateSensitivity = 3.5f;   // degrees per unit of mouse delta
        public float rollSensitivity = 300f;     // a scroll notch is about 0.1, so this is ~30 deg per notch
        // No 90 degree snap key on purpose. The obvious pair, Q and E, cannot be used: E is
        // DELIVER in ContractManager, so a quarter turn at the truck could settle the contract
        // by accident. The free turn below is enough to clear a door. Revisit if it reads fiddly.
        public float rotateStrength = 14f;       // how hard the object is driven to its target orientation
        public float maxAngularSpeed = 6f;       // rad/s, before the weight factor
        // false restores the pre-rotation carry: the object just hangs and sways freely.
        // Kept as a switch because the free-hanging carry is what playtest 001 validated.
        public bool holdOrientation = true;

        MovableObject held;
        float savedLinearDamping;
        float savedAngularDamping;
        float savedMaxAngularVelocity;

        // The held object orientation, expressed relative to the player yaw. Turning your body
        // turns the object with you (a sofa held sideways stays sideways as you walk to the
        // door); looking up and down does not, because tipping cargo with the camera is not
        // how carrying reads.
        Quaternion heldLocalRotation = Quaternion.identity;
        bool rotating;

        Quaternion PlayerYaw => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (held == null) TryGrab();
                else Release(false);
            }
            if (Input.GetMouseButtonDown(1) && held != null) Release(true);

            HandleRotateInput();

            // carrying something heavy slows you down and flattens your jump
            controller.speedMultiplier = held == null
                ? 1f
                : Mathf.Clamp(1f - held.weight / 120f, 0.35f, 1f);
            controller.jumpMultiplier = held == null
                ? 1f
                : Mathf.Clamp(1f - held.weight / 90f, 0.25f, 1f);
        }

        void HandleRotateInput()
        {
            rotating = held != null && holdOrientation && Input.GetKey(rotateKey);
            controller.lookLocked = rotating;
            if (!rotating) return;

            // Axes are those of the player-yaw frame heldLocalRotation lives in:
            // up = yaw it, right = tip it away from you, forward = roll it like a barrel.
            float mx = Input.GetAxis("Mouse X") * rotateSensitivity;
            float my = Input.GetAxis("Mouse Y") * rotateSensitivity;
            float scroll = Input.GetAxis("Mouse ScrollWheel") * rollSensitivity;

            if (Mathf.Abs(mx) > 0f) Turn(mx, Vector3.up);
            if (Mathf.Abs(my) > 0f) Turn(my, Vector3.right);
            if (Mathf.Abs(scroll) > 0f) Turn(scroll, Vector3.forward);
        }

        void Turn(float degrees, Vector3 axis)
        {
            heldLocalRotation = Quaternion.AngleAxis(degrees, axis) * heldLocalRotation;
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
                    savedMaxAngularVelocity = held.rb.maxAngularVelocity;
                    held.rb.useGravity = true;
                    held.rb.linearDamping = carriedLinearDamping;
                    held.rb.angularDamping = carriedAngularDamping;
                    // the default cap would silently eat the turn on the fastest objects
                    held.rb.maxAngularVelocity = Mathf.Max(savedMaxAngularVelocity, maxAngularSpeed);
                    // pick the object up as it lies, do not snap it to a pose
                    heldLocalRotation = Quaternion.Inverse(PlayerYaw) * held.rb.rotation;
                }
            }
        }

        void Release(bool thrown)
        {
            if (held == null) return;
            held.rb.linearDamping = savedLinearDamping;
            held.rb.angularDamping = savedAngularDamping;
            held.rb.maxAngularVelocity = savedMaxAngularVelocity;
            if (thrown) held.rb.AddForce(cam.forward * throwForce, ForceMode.VelocityChange);
            held = null;
            rotating = false;
            if (controller != null) controller.lookLocked = false;
        }

        void OnDisable()
        {
            // never strand the camera in rotate mode
            if (controller != null) controller.lookLocked = false;
        }

        void FixedUpdate()
        {
            if (held == null) return;
            float weightFactor = Mathf.Clamp(maxSoloWeight / Mathf.Max(held.weight, 1f), 0.2f, 1f);

            Vector3 target = cam.position + cam.forward * holdDistance;
            // Heavy things hang lower. You cannot hold a fridge at eye level, and a settled
            // offset reads as weight without the object sinking through the floor.
            target.y -= Mathf.Min(held.weight * sagPerKg, maxSag);
            Vector3 toTarget = target - held.rb.worldCenterOfMass;
            Vector3 desired = Vector3.ClampMagnitude(toTarget * followStrength * weightFactor, maxSpeed);
            held.rb.linearVelocity = desired;

            if (holdOrientation) DriveRotation(weightFactor);
        }

        // Same control model as the carry: steer the angular velocity straight at the target
        // orientation rather than apply torque. Heavy objects turn slower, for the same reason
        // they lag behind the hold point.
        void DriveRotation(float weightFactor)
        {
            Quaternion delta = (PlayerYaw * heldLocalRotation) * Quaternion.Inverse(held.rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            bool usable = axis.sqrMagnitude > 0.0001f
                          && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x)
                          && Mathf.Abs(angle) > 0.05f;
            if (!usable)
            {
                held.rb.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 spin = axis.normalized * (angle * Mathf.Deg2Rad * rotateStrength * weightFactor);
            held.rb.angularVelocity = Vector3.ClampMagnitude(spin, maxAngularSpeed * weightFactor);
        }
    }
}
