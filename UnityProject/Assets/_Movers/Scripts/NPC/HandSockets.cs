using UnityEngine;

namespace Movers
{
    // One empty "socket" in the palm of each hand, for props held through an animation (her
    // cup must stay in her palm through Sit_Drink, which a body-frame offset cannot do).
    //
    // Socket convention: local Z runs along the fingers, local Y comes out of the palm. Props
    // are authored once in that frame. The crew rig and the grandmother's rig do not share bone
    // axes (A6_animation.md 3.4), so nothing here is hard-coded in bone space: the finger axis
    // is read from the hand's "_end" child, and the palm normal is the bone's +X for the right
    // hand and -X for the left (measured on both avatars).
    [DisallowMultipleComponent]
    public sealed class HandSockets : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Where the palm centre sits along the hand, as a share of its length (A6: 0.58 to 0.62).")]
        public float palmAlong = 0.6f;
        [Tooltip("Metres from the bone to the palm surface.")]
        public float palmOut = 0.03f;

        public Transform Right { get; private set; }
        public Transform Left { get; private set; }

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            Right = Build(HumanBodyBones.RightHand, 1f, "Socket_R");
            Left = Build(HumanBodyBones.LeftHand, -1f, "Socket_L");
        }

        Transform Build(HumanBodyBones bone, float side, string socketName)
        {
            if (animator == null || !animator.isHuman) return null;
            Transform hand = animator.GetBoneTransform(bone);
            if (hand == null) return null;

            Vector3 finger = Vector3.up;
            float length = 0.15f;
            Transform end = FindEnd(hand);
            if (end != null && end.localPosition.sqrMagnitude > 1e-6f)
            {
                finger = end.localPosition.normalized;
                length = end.localPosition.magnitude;
            }
            else Debug.LogWarning("[HandSockets] " + hand.name + " has no _end child; the fingers are assumed along +Y.");

            Vector3 palm = Vector3.ProjectOnPlane(Vector3.right * side, finger);
            if (palm.sqrMagnitude < 1e-6f) palm = Vector3.ProjectOnPlane(Vector3.forward, finger);
            palm.Normalize();

            var socket = new GameObject(socketName).transform;
            socket.SetParent(hand, false);
            socket.localPosition = finger * (palmAlong * length) + palm * palmOut;
            socket.localRotation = Quaternion.LookRotation(finger, palm);
            return socket;
        }

        static Transform FindEnd(Transform hand)
        {
            for (int i = 0; i < hand.childCount; i++)
            {
                Transform c = hand.GetChild(i);
                if (c.name.EndsWith("_end")) return c;
            }
            return hand.childCount == 1 ? hand.GetChild(0) : null;
        }
    }
}
