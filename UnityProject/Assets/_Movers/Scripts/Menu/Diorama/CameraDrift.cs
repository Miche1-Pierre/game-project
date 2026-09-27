using UnityEngine;

namespace Movers
{
    // The title screen's slow camera: it breathes around the pose it was placed at (a few
    // metres of drift, a degree or two of turn, on slow unrelated sines) so the landscape is
    // alive without ever pulling the eye away from the menu. Unscaled time: a paused game
    // coming back to the menu does not freeze it.
    [DisallowMultipleComponent]
    public sealed class CameraDrift : MonoBehaviour
    {
        [Tooltip("Metres of drift on each axis.")]
        public Vector3 amplitude = new Vector3(2.2f, 0.4f, 1.4f);
        [Tooltip("Seconds for one swing on each axis.")]
        public Vector3 period = new Vector3(47f, 31f, 59f);
        [Tooltip("Degrees of turn (yaw, pitch).")]
        public Vector2 turn = new Vector2(1.6f, 0.6f);
        public Vector2 turnPeriod = new Vector2(53f, 37f);

        Vector3 restPosition;
        Quaternion restRotation;
        bool captured;
        float t;

        void Start() => Capture();

        // The pose to breathe around; taken again after something moves the rig (GroundSnap).
        public void Capture()
        {
            restPosition = transform.position;
            restRotation = transform.rotation;
            captured = true;
        }

        void LateUpdate()
        {
            if (!captured) Capture();
            t += Time.unscaledDeltaTime;
            Vector3 d = new Vector3(
                amplitude.x * Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(1f, period.x)),
                amplitude.y * Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(1f, period.y) + 1.3f),
                amplitude.z * Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(1f, period.z) + 2.1f));
            float yaw = turn.x * Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(1f, turnPeriod.x) + 0.7f);
            float pitch = turn.y * Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(1f, turnPeriod.y) + 2.9f);
            transform.SetPositionAndRotation(restPosition + restRotation * d,
                                             Quaternion.Euler(0f, yaw, 0f) * restRotation * Quaternion.Euler(pitch, 0f, 0f));
        }
    }
}
