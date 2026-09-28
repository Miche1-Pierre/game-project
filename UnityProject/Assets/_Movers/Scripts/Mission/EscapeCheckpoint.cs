using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The exit of a police flee (ADR-013): a scene marker, placed per map. The truck inside it with
    // crew aboard is a success (EscapeMission). The BoxCollider only gives the size and the gizmo:
    // it is kept disabled, on the Ignore Raycast layer, so no query or ray ever hits it, and the
    // test is an oriented box in local space, not a world AABB. One per scene is active.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EscapeCheckpoint : MonoBehaviour
    {
        static readonly List<EscapeCheckpoint> enabledOnes = new List<EscapeCheckpoint>();

        BoxCollider box;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { enabledOnes.Clear(); }

        // The enabled one in the game scene, null when none.
        public static EscapeCheckpoint Active => enabledOnes.Count > 0 ? enabledOnes[enabledOnes.Count - 1] : null;

        // World centre of its box.
        public Vector3 Center => transform.TransformPoint(Box != null ? Box.center : Vector3.zero);
        // Where the HUD marker points: 2 m above the centre.
        public Vector3 MarkerPosition => Center + Vector3.up * 2f;

        BoxCollider Box
        {
            get
            {
                if (box == null) box = GetComponent<BoxCollider>();
                return box;
            }
        }

        void Awake()
        {
            if (Box != null) Box.enabled = false;
        }

        void OnEnable()
        {
            if (!enabledOnes.Contains(this)) enabledOnes.Add(this);
        }

        void OnDisable()
        {
            enabledOnes.Remove(this);
        }

        // Oriented box test in the marker's local space.
        public bool Contains(Vector3 worldPoint)
        {
            var b = Box;
            if (b == null) return false;
            Vector3 local = transform.InverseTransformPoint(worldPoint) - b.center;
            Vector3 half = b.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }
}
