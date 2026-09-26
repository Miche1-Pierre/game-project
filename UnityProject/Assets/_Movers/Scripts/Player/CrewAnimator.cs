using UnityEngine;

namespace Movers
{
    // Moves the crew body with the player, so the other player can read what you are doing:
    // walking, crouching behind the sofa, carrying something. Goes on the player root.
    //
    // It stands the body on the floor (its feet where the capsule's feet are, minus the skin the
    // CharacterController hovers on) and feeds the Animator what the controller built for the
    // slice expects (SLICE_ARCHITECTURE, animation contract):
    //   Speed   float, m/s over the ground
    //   Crouch  bool
    //   layer "Carry", whose weight follows whether the hands are full
    // Each is only written if the controller has it, so the current one-state controller, or a
    // body with no controller at all, is left alone without a warning per frame.
    //
    // Seated in the truck (IsDriving), or with the walking controller switched off for any other
    // reason, the body is not walking: Speed and Crouch go to rest. A switched-off
    // CharacterController keeps reporting the last velocity it moved at, so reading it there
    // would play a run cycle in the driver's seat.
    [DisallowMultipleComponent]
    public sealed class CrewAnimator : MonoBehaviour
    {
        public Animator animator;          // left empty: the one under this player
        public bool standBodyOnFloor = true;
        public float speedDampSeconds = 0.1f;
        public float carryBlendSeconds = 0.2f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int CrouchId = Animator.StringToHash("Crouch");

        PlayerController controller;
        PlayerGrab grab;
        CrewMember member;
        RuntimeAnimatorController bound;
        bool hasSpeed, hasCrouch;
        int carryLayer = -1;
        float carryWeight;

        // The body's local height this component last stood it at, or NaN before it did. Lets
        // a test tell "placed at Play" from "saved there in the scene".
        public float PlacedAtLocalY { get; private set; } = float.NaN;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            grab = GetComponent<PlayerGrab>();
            member = GetComponent<CrewMember>();
        }

        void Start()
        {
            // Added at runtime by the spawner on a scene that did not have one.
            if (member == null) member = GetComponent<CrewMember>();
            Resolve();
            // PlayerController has measured its capsule by now (Awake).
            StandBodyOnFloor();
        }

        // Puts the body's feet on the floor line under the capsule. Done once at Start; public
        // so a test (or a body swapped at runtime) can ask for it again.
        public void StandBodyOnFloor()
        {
            if (!standBodyOnFloor || animator == null || controller == null || animator.transform == transform) return;
            var t = animator.transform;
            var p = t.localPosition;
            float y = controller.FeetHeight;
            t.localPosition = new Vector3(p.x, y, p.z);
            PlacedAtLocalY = y;
        }

        void Resolve()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        void LateUpdate()
        {
            if (animator == null) { Resolve(); if (animator == null) return; }
            if (!animator.isActiveAndEnabled || !animator.isInitialized) return;
            if (animator.runtimeAnimatorController != bound) Bind();
            if (bound == null) return;

            float dt = Time.deltaTime;
            bool walking = controller != null && controller.isActiveAndEnabled && !(member != null && member.IsDriving);
            if (hasSpeed)
            {
                Vector3 v = walking ? controller.Velocity : Vector3.zero;
                v.y = 0f;
                animator.SetFloat(SpeedId, v.magnitude, speedDampSeconds, dt);
            }
            if (hasCrouch) animator.SetBool(CrouchId, walking && controller.crouching);
            if (carryLayer >= 0)
            {
                float target = grab != null && grab.IsCarrying ? 1f : 0f;
                float step = carryBlendSeconds > 0.001f ? dt / carryBlendSeconds : 1f;
                carryWeight = Mathf.MoveTowards(carryWeight, target, step);
                animator.SetLayerWeight(carryLayer, carryWeight);
            }
        }

        // Once per controller: what it offers is read here, never per frame (the parameter list
        // is a fresh array every time it is asked for).
        void Bind()
        {
            bound = animator.runtimeAnimatorController;
            hasSpeed = hasCrouch = false;
            carryLayer = -1;
            if (bound == null) return;

            var ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].nameHash == SpeedId && ps[i].type == AnimatorControllerParameterType.Float) hasSpeed = true;
                if (ps[i].nameHash == CrouchId && ps[i].type == AnimatorControllerParameterType.Bool) hasCrouch = true;
            }
            carryLayer = animator.GetLayerIndex("Carry");
            carryWeight = carryLayer >= 0 ? animator.GetLayerWeight(carryLayer) : 0f;
        }
    }
}
