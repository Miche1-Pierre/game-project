using UnityEngine;

namespace Movers
{
    // A blast that really throws you tips your view over onto the floor and stands it back up:
    // about a second, then you are looking where you were. Goes on the player root.
    //
    // Only the picture falls (ViewOffset): the player never loses control, the capsule keeps
    // flying where the blast shoved it, and you can walk out of the tumble while it plays. The
    // body the other player sees does the real fall (CrewAnimator, Crew_KnockedDown then
    // Crew_GetUp). A light shove (under heavySpeed) only gets the camera shake it already had:
    // funny failure, not a punishment you have to sit through (CLAUDE.md 4).
    //
    // The view falls the way the blast pushed you: away from it. Explosion raises
    // PlayerKnockedDown with the victim's centre, and its own Explosion event with the blast's,
    // in the same frame and in either order, so the direction is worked out in LateUpdate.
    [DisallowMultipleComponent]
    public sealed class KnockdownTumble : MonoBehaviour
    {
        [Tooltip("Shove speed (m/s, PlayerKnockedDown's magnitude) from which the view tumbles. A grenade at your feet is about 11, the edge of its reach 2.")]
        public float heavySpeed = 5.5f;
        public float fallSeconds = 0.32f;
        public float lieSeconds = 0.28f;
        public float riseSeconds = 0.5f;
        [Tooltip("Where the eyes end up, metres above the feet.")]
        public float floorEyeHeight = 0.3f;
        public float sideShift = 0.15f;         // metres the head lands off the capsule's axis
        public float tipDegrees = 72f;          // how far the view tips over, for the heaviest shove
        public float extraRollDegrees = 14f;    // a twist on top, so a fall straight back is not just a look up

        CrewMember member;
        PlayerController controller;

        bool pending;
        float pendingSpeed;
        Vector3 pendingCentre;

        float clock = -1f;         // seconds into the tumble, negative when there is none
        float strength;            // 0..1 from the shove speed
        Vector3 fallDir;           // in the player's space, flat
        float rollSign = 1f;

        public bool IsTumbling => clock >= 0f;
        public float Duration => fallSeconds + lieSeconds + riseSeconds;
        // What the last frame asked of the view (for the tests: the offset itself is gone again
        // by the time anything outside a render could look at the camera).
        public Vector3 LastOffset { get; private set; }
        public float LastTipDegrees { get; private set; }
        // The whole of the latest tumble, kept after it ends until the next one begins: when it
        // began (Time.time, negative before the first), how far down the eyes went and how far
        // over the view tipped. A test that looks a little late still sees what happened.
        public float StartedAt { get; private set; } = -1f;
        public float MaxDrop { get; private set; }
        public float MaxTipDegrees { get; private set; }

        void Awake()
        {
            member = GetComponent<CrewMember>();
            controller = GetComponent<PlayerController>();
        }

        void OnEnable() { WorldEvents.Subscribe(OnWorldEvent); }

        void OnDisable()
        {
            WorldEvents.Unsubscribe(OnWorldEvent);
            clock = -1f;
            pending = false;
        }

        void OnWorldEvent(WorldEvent e)
        {
            if (e.type != WorldEventType.PlayerKnockedDown) return;
            if (member == null) member = GetComponent<CrewMember>();
            if (e.subject == null || !ReferenceEquals(e.subject, member)) return;
            if (e.magnitude < heavySpeed) return;
            Knock(e.position, e.magnitude);
        }

        // Tumbles the view as if a blast had thrown this player at `speed` m/s. Public for the
        // tests and a debug key; the game calls it through PlayerKnockedDown.
        public void Knock(Vector3 victimCentre, float speed)
        {
            pending = true;
            pendingCentre = victimCentre;
            pendingSpeed = speed;
        }

        void LateUpdate()
        {
            if (pending) Begin();
            if (clock < 0f) return;

            clock += Time.deltaTime;
            if (clock >= Duration) { clock = -1f; LastOffset = Vector3.zero; LastTipDegrees = 0f; return; }

            if (member == null || member.IsDriving || member.View == null || controller == null) return;
            var cam = member.View;
            if (cam.transform.parent != transform) return;   // the eyes are somewhere else (a chase camera)

            Evaluate(clock, out float down, out float tip, out float bounce);
            // How far the eyes have to go to reach the floor, from wherever the crouch has them.
            float drop = Mathf.Max(0f, cam.transform.localPosition.y - (controller.FeetHeight + floorEyeHeight));
            Vector3 position = Vector3.down * (drop * down) + fallDir * (sideShift * down) + Vector3.up * bounce;

            Vector3 axis = Vector3.Cross(Vector3.up, fallDir);
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;
            Quaternion over = Quaternion.AngleAxis(tipDegrees * strength * tip, axis.normalized);
            Quaternion twist = Quaternion.AngleAxis(extraRollDegrees * rollSign * tip, Vector3.forward);

            ViewOffset.For(cam).Add(position, over, twist);
            LastOffset = position;
            LastTipDegrees = tipDegrees * strength * tip;
            if (-position.y > MaxDrop) MaxDrop = -position.y;
            if (LastTipDegrees > MaxTipDegrees) MaxTipDegrees = LastTipDegrees;
        }

        void Begin()
        {
            pending = false;
            strength = Mathf.InverseLerp(heavySpeed * 0.8f, 11f, pendingSpeed);
            strength = Mathf.Lerp(0.7f, 1f, strength);

            // Away from the blast, in the player's own space. The newest Explosion event of the
            // last moment is the blast that did it; none (a debug knock) falls backwards.
            Vector3 away = -transform.forward;
            for (int i = 0; i < WorldEvents.RecentCount && i < 16; i++)
            {
                var e = WorldEvents.GetRecent(i);
                if (Time.time - e.time > 0.25f) break;
                if (e.type != WorldEventType.Explosion) continue;
                Vector3 d = pendingCentre - e.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) away = d.normalized;
                break;
            }
            fallDir = transform.InverseTransformDirection(away);
            fallDir.y = 0f;
            fallDir = fallDir.sqrMagnitude > 1e-6f ? fallDir.normalized : Vector3.back;
            rollSign = Random.value < 0.5f ? -1f : 1f;
            clock = 0f;
            StartedAt = Time.time;
            MaxDrop = 0f;
            MaxTipDegrees = 0f;
        }

        // down 0..1 (how far to the floor), tip 0..1 (how far over), bounce in metres.
        void Evaluate(float t, out float down, out float tip, out float bounce)
        {
            bounce = 0f;
            if (t < fallSeconds)
            {
                float u = t / fallSeconds;
                down = u * u;                                   // falling, not sliding
                tip = Mathf.SmoothStep(0f, 1f, u);
                return;
            }
            t -= fallSeconds;
            if (t < lieSeconds)
            {
                down = 1f;
                tip = 1f;
                // The head bounces once off the floor.
                float u = t / lieSeconds;
                bounce = 0.035f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 2.2f)) * (1f - u);
                return;
            }
            t -= lieSeconds;
            float r = Mathf.Clamp01(t / riseSeconds);
            // Up with a little overshoot on the tip, the stagger of someone who just got up.
            float e = Mathf.SmoothStep(0f, 1f, r);
            down = 1f - e;
            tip = (1f - e) - 0.08f * Mathf.Sin(Mathf.PI * r);
        }
    }
}
