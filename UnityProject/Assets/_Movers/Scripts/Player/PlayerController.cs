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
        // not to vault a wall. Out of scope: double jump.
        public float jumpHeight = 1.1f;
        // Grace window after walking off an edge. Without it a CharacterController
        // eats jumps on ramps and doorsteps, which is a frustrating failure, not a funny one.
        public float coyoteTime = 0.12f;

        [Header("Sprint and crouch")]
        // Both are hold, not toggle: a mover sprints across the garden and ducks under a
        // shelf, they do not live in either state. Crouch wins over sprint, and no stamina:
        // a meter to watch is a system nobody asked for.
        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode crouchKey = KeyCode.LeftControl;
        public float sprintMultiplier = 1.6f;
        public float crouchSpeedMultiplier = 0.45f;
        public float crouchHeight = 1.0f;
        public float stanceSpeed = 8f;     // metres of capsule height per second, so the camera does not snap

        [HideInInspector] public float speedMultiplier = 1f;
        [HideInInspector] public float jumpMultiplier = 1f;
        [HideInInspector] public bool lookLocked = false;
        [HideInInspector] public bool crouching = false;

        CharacterController cc;
        float pitch;
        float vy;
        float lastGroundedTime = -999f;

        // stance geometry, captured once so the crouch math follows whatever the scene authored
        float standHeight;
        Vector3 standCenter;
        float feetOffset;     // feet relative to the transform, kept constant while crouching
        float headTopGap;     // distance from the eye to the top of the capsule

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;

            standHeight = cc.height;
            standCenter = cc.center;
            feetOffset = standCenter.y - standHeight * 0.5f;
            float standEyeAboveFeet = (cam != null ? cam.localPosition.y : 0f) - feetOffset;
            headTopGap = standHeight - standEyeAboveFeet;
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

            UpdateStance();

            // move
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = transform.right * h + transform.forward * v;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            bool sprinting = Input.GetKey(sprintKey) && !crouching;
            float stance = crouching ? crouchSpeedMultiplier : (sprinting ? sprintMultiplier : 1f);

            if (cc.isGrounded)
            {
                lastGroundedTime = Time.time;
                if (vy < 0f) vy = -2f;
            }

            // jump: v = sqrt(2 * g * h). A loaded crew jumps lower, it never stops jumping,
            // because a fridge that refuses to leave the ground is a rule, and a fridge that
            // barely hops is a joke. Crouched, you do not jump at all: let go of crouch first.
            if (Input.GetButtonDown("Jump") && !crouching && Time.time - lastGroundedTime <= coyoteTime)
            {
                float h2 = Mathf.Max(0.05f, jumpHeight * jumpMultiplier);
                vy = Mathf.Sqrt(2f * -gravity * h2);
                lastGroundedTime = -999f;   // one jump per contact with the ground
            }

            vy += gravity * Time.deltaTime;

            Vector3 vel = dir * walkSpeed * speedMultiplier * stance + Vector3.up * vy;
            cc.Move(vel * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
        }

        // Grows and shrinks the capsule around fixed feet, and rides the camera down with it.
        void UpdateStance()
        {
            bool wantCrouch = Input.GetKey(crouchKey);
            if (!wantCrouch && crouching && !HasHeadroom()) wantCrouch = true;   // something overhead, stay down
            crouching = wantCrouch;

            float target = crouching ? crouchHeight : standHeight;
            if (!Mathf.Approximately(cc.height, target))
            {
                cc.height = Mathf.MoveTowards(cc.height, target, stanceSpeed * Time.deltaTime);
                cc.center = new Vector3(standCenter.x, feetOffset + cc.height * 0.5f, standCenter.z);
                if (cam != null)
                {
                    var p = cam.localPosition;
                    p.y = feetOffset + (cc.height - headTopGap);
                    cam.localPosition = p;
                }
            }
        }

        // Is there room to stand back up? Cast the head sphere upward by the height we would
        // regain, and ignore our own capsule, which the cast starts inside.
        bool HasHeadroom()
        {
            float grow = standHeight - cc.height;
            if (grow <= 0.01f) return true;

            Vector3 headSphere = transform.position + cc.center + Vector3.up * (cc.height * 0.5f - cc.radius);
            var hits = Physics.SphereCastAll(headSphere, cc.radius * 0.95f, Vector3.up, grow + 0.05f,
                                             ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
                if (hit.collider != (Collider)cc) return false;
            return true;
        }
    }
}
