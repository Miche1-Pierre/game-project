using UnityEngine;

namespace Movers
{
    // Minimal first-person controller (classic Input Manager, CharacterController).
    // speedMultiplier, jumpMultiplier and lookLocked are all driven by PlayerGrab:
    // carrying something heavy slows you down and flattens your jump, and holding the
    // rotate key hands the mouse to the held object instead of your head.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cam;
        public float walkSpeed = 4.5f;
        public float mouseSensitivity = 2f;
        public float gravity = -18f;

        [Header("Jump")]
        // Space. Sized to climb into the truck bed and step over a dropped crate,
        // not to vault a wall. Out of scope: double jump, sprint, crouch.
        public float jumpHeight = 1.1f;
        // Grace window after walking off an edge. Without it a CharacterController
        // eats jumps on ramps and doorsteps, which is a frustrating failure, not a funny one.
        public float coyoteTime = 0.12f;

        [HideInInspector] public float speedMultiplier = 1f;
        [HideInInspector] public float jumpMultiplier = 1f;
        [HideInInspector] public bool lookLocked = false;

        CharacterController cc;
        float pitch;
        float vy;
        float lastGroundedTime = -999f;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            // look (suspended while PlayerGrab is using the mouse to turn a held object)
            if (!lookLocked)
            {
                float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
                float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
                transform.Rotate(0f, mx, 0f);
                pitch = Mathf.Clamp(pitch - my, -85f, 85f);
                if (cam != null) cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            // move
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = transform.right * h + transform.forward * v;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (cc.isGrounded)
            {
                lastGroundedTime = Time.time;
                if (vy < 0f) vy = -2f;
            }

            // jump: v = sqrt(2 * g * h). A loaded crew jumps lower, it never stops jumping,
            // because a fridge that refuses to leave the ground is a rule, and a fridge that
            // barely hops is a joke.
            if (Input.GetButtonDown("Jump") && Time.time - lastGroundedTime <= coyoteTime)
            {
                float h2 = Mathf.Max(0.05f, jumpHeight * jumpMultiplier);
                vy = Mathf.Sqrt(2f * -gravity * h2);
                lastGroundedTime = -999f;   // one jump per contact with the ground
            }

            vy += gravity * Time.deltaTime;

            Vector3 vel = dir * walkSpeed * speedMultiplier + Vector3.up * vy;
            cc.Move(vel * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
