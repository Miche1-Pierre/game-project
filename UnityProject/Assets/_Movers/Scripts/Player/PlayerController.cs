using UnityEngine;

namespace Movers
{
    // Minimal first-person controller (classic Input Manager, CharacterController).
    // speedMultiplier is driven down by PlayerGrab when carrying something heavy.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cam;
        public float walkSpeed = 4.5f;
        public float mouseSensitivity = 2f;
        public float gravity = -18f;

        [HideInInspector] public float speedMultiplier = 1f;

        CharacterController cc;
        float pitch;
        float vy;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            // look
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, -85f, 85f);
            if (cam != null) cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // move
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = transform.right * h + transform.forward * v;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (cc.isGrounded && vy < 0f) vy = -2f;
            vy += gravity * Time.deltaTime;

            Vector3 vel = dir * walkSpeed * speedMultiplier + Vector3.up * vy;
            cc.Move(vel * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
