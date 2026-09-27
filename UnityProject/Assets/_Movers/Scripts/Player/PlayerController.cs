using UnityEngine;

namespace Movers
{
    // Minimal first-person controller (CharacterController), driven by this player's CrewInput.
    // speedMultiplier, jumpMultiplier and lookLocked are all driven by PlayerGrab:
    // carrying something heavy slows you down and flattens your jump, and holding the
    // rotate button hands the look to the held object instead of your head.
    // AddImpulse is the one way in for anything outside the player: an explosion shoves you
    // through it, and the shove fades on its own instead of taking your controls away.
    //
    // It never reads UnityEngine.Input. The keyboard, a gamepad or a test script all arrive
    // through CrewInput, so two of these on one machine answer to two different people. The
    // cursor is not a player's business either: CursorLock owns it.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cam;
        public float walkSpeed = 4.5f;
        public float mouseSensitivity = 2f;   // degrees per unit of look delta (mouse-axis units)
        public float gravity = -18f;

        [Header("Eyes")]
        // Metres from the floor to the eyes. 0 keeps the camera where the scene put it (the
        // tutorial, where the carry was validated). Measured from the floor under the feet, not
        // from the capsule bottom: a CharacterController hovers skinWidth above the ground
        // (measured: exactly 0.08 m with the house player's settings), and the body stands on
        // the floor, so the eyes have to be counted from there too.
        public float eyeHeight = 0f;

        [Header("Jump")]
        // Sized to climb into the truck bed and step over a dropped crate, not to vault a wall.
        // Out of scope: double jump.
        public float jumpHeight = 1.1f;
        // Grace window after walking off an edge. Without it a CharacterController
        // eats jumps on ramps and doorsteps, which is a frustrating failure, not a funny one.
        public float coyoteTime = 0.12f;

        [Header("Sprint and crouch")]
        // Both are hold, not toggle: a mover sprints across the garden and ducks under a
        // shelf, they do not live in either state. Crouch wins over sprint, and no stamina:
        // a meter to watch is a system nobody asked for. Which key or button it is belongs to
        // the input source (KeyboardMouseSource: Shift and Ctrl, as before).
        public float sprintMultiplier = 1.6f;
        public float crouchSpeedMultiplier = 0.45f;
        public float crouchHeight = 1.0f;
        public float stanceSpeed = 8f;     // metres of capsule height per second, so the camera does not snap

        [HideInInspector] public float speedMultiplier = 1f;
        [HideInInspector] public float jumpMultiplier = 1f;
        [HideInInspector] public bool lookLocked = false;
        [HideInInspector] public bool crouching = false;

        // Driven by Drunkenness, the same way the two multipliers above are driven by
        // PlayerGrab. Degrees: lookSway is (pitch, yaw, roll) added to the camera, moveDrift
        // turns the direction you actually walk away from the one you asked for. Both are
        // zero while sober, and nothing else writes them.
        [HideInInspector] public Vector3 lookSway = Vector3.zero;
        [HideInInspector] public float moveDrift = 0f;

        [Header("Knockback")]
        // How fast a shove from AddImpulse dies out, per second. On the ground your feet catch
        // you after a stumble; in the air nothing does, so the same blast carries you several
        // metres when it lifts you and about two when it does not.
        public float knockbackGroundDecay = 5f;
        public float knockbackAirDecay = 1.2f;
        // Five grenades going off together should throw you across the room, not across the map.
        public float maxKnockbackSpeed = 16f;

        CharacterController cc;
        CrewInput input;
        float pitch;
        float vy;
        float lastGroundedTime = -999f;
        Vector3 knockback;    // horizontal m/s added on top of the walk, decays by itself

        // stance geometry, captured once so the crouch math follows whatever the scene authored
        float standHeight;
        Vector3 standCenter;
        float feetOffset;     // capsule bottom relative to the transform, kept constant while crouching
        float headTopGap;     // distance from the eye to the top of the capsule

        // Where the floor is, in this transform's space: the capsule bottom minus the skin the
        // CharacterController hovers on. The crew body stands here (CrewAnimator).
        public float FeetHeight => feetOffset - (cc != null ? cc.skinWidth : 0f);

        // Metres per second from the last Move, for the animation driver. Zero while the capsule
        // is switched off (the truck seat does that): it would otherwise keep the last walk.
        // A puppet (online, a body the other machine drives) has no Move of its own: it reports
        // the velocity that came with its pose.
        public Vector3 Velocity => IsNetPuppet ? netVelocity : (cc != null && cc.enabled ? cc.velocity : Vector3.zero);

        // Online co-op (NETCODE_SLICE 9.3, 9.4). A puppet reads the values that came with its
        // pose; offline and driven bodies read their own.
        public bool Grounded => IsNetPuppet ? netGrounded : cc != null && cc.isGrounded;
        public bool NetThrowHeld => IsNetPuppet ? netThrowHeld : input != null && input.Held(CrewButton.Throw);

        // Online, a body the other machine drives: posed by NetPlayerDriver, never moved here.
        // Always false offline.
        bool IsNetPuppet => Net.IsOnline && !Net.Drives(Member);
        CrewMember Member => member != null ? member : (member = GetComponent<CrewMember>());
        CrewMember member;
        Vector3 netVelocity;
        bool netGrounded, netThrowHeld;

        // The pose the driving machine sent (9.3). The transform only: the capsule is never
        // switched off and on (that would rebuild its shape, lose its ignore pairs and re-fire
        // every collision enter), and nothing calls Move. The stance follows the sent height,
        // around the same fixed feet UpdateStance keeps.
        public void ApplyNetPose(in NetPose pose)
        {
            transform.SetPositionAndRotation(pose.position, Quaternion.Euler(0f, pose.yaw, 0f));
            if (cam != null) cam.localRotation = pose.camLocalRotation;
            float x = pose.camLocalRotation.eulerAngles.x;
            pitch = Mathf.Clamp(x > 180f ? x - 360f : x, -85f, 85f);
            crouching = pose.crouching;
            if (cc != null && pose.height > 0.1f && !Mathf.Approximately(cc.height, pose.height))
            {
                cc.height = pose.height;
                cc.center = new Vector3(standCenter.x, feetOffset + cc.height * 0.5f, standCenter.z);
                if (cam != null)
                {
                    var p = cam.localPosition;
                    p.y = feetOffset + (cc.height - headTopGap);
                    cam.localPosition = p;
                }
            }
            netVelocity = pose.velocity;
            netGrounded = pose.grounded;
            netThrowHeld = pose.throwHeld;
        }

        // This body's pose as the net sends it (9.2, 9.4): sampled by NetPlayerDriver in its
        // LateUpdate, before any render-only offset.
        public NetPose NetPoseNow()
        {
            return new NetPose
            {
                position = transform.position,
                yaw = transform.eulerAngles.y,
                camLocalRotation = cam != null ? cam.localRotation : Quaternion.identity,
                height = cc != null ? cc.height : 0f,
                crouching = crouching,
                grounded = cc != null && cc.isGrounded,
                throwHeld = input != null && input.Held(CrewButton.Throw),
                velocity = cc != null && cc.enabled ? cc.velocity : Vector3.zero,
            };
        }

        public CrewInput Input => input;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            input = CrewSetup.InputOf(gameObject);

            standHeight = cc.height;
            standCenter = cc.center;
            feetOffset = standCenter.y - standHeight * 0.5f;

            if (eyeHeight > 0f && cam != null)
            {
                var p = cam.localPosition;
                p.y = FeetHeight + eyeHeight;
                cam.localPosition = p;
            }

            float standEyeAboveFeet = (cam != null ? cam.localPosition.y : 0f) - feetOffset;
            headTopGap = standHeight - standEyeAboveFeet;
        }

        void Start()
        {
            // A scene nobody set up for the crew (an older test map) still plays: its player is
            // put on the roster and takes the keyboard, instead of standing there deaf.
            if (CrewSpawner.Active == null) CrewSpawner.CreateDefault();
        }

        void Update()
        {
            // Online, a body the other machine drives is posed by NetPlayerDriver. The component
            // stays enabled: Explosion, CrewAnimator and VehicleSeat test isActiveAndEnabled.
            if (Net.IsOnline && !Net.Drives(Member)) return;

            // look (suspended while PlayerGrab is using the look to turn a held object)
            if (!lookLocked)
            {
                Vector2 look = GameSettings.ApplyLook(input.LookDelta);   // the player's sensitivity and invert Y
                float mx = look.x * mouseSensitivity;
                float my = look.y * mouseSensitivity;
                transform.Rotate(0f, mx, 0f);
                pitch = Mathf.Clamp(pitch - my, -85f, 85f);
            }
            // Applied every frame, not only when the look is free, so a drunk head keeps
            // wandering while you are busy turning a sofa. Zero sway reproduces the old line
            // exactly.
            if (cam != null) cam.localRotation = Quaternion.Euler(pitch + lookSway.x, lookSway.y, lookSway.z);

            UpdateStance();

            // move
            Vector2 move = input.Move;
            Vector3 dir = transform.right * move.x + transform.forward * move.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            // You do not walk where you point any more. Rotating the direction rather than
            // nudging the input keeps full speed: drunk is crooked, not slow.
            if (moveDrift != 0f) dir = Quaternion.Euler(0f, moveDrift, 0f) * dir;

            bool sprinting = input.Held(CrewButton.Sprint) && !crouching;
            float stance = crouching ? crouchSpeedMultiplier : (sprinting ? sprintMultiplier : 1f);

            if (cc.isGrounded)
            {
                lastGroundedTime = Time.time;
                if (vy < 0f) vy = -2f;
            }

            // jump: v = sqrt(2 * g * h). A loaded crew jumps lower, it never stops jumping,
            // because a fridge that refuses to leave the ground is a rule, and a fridge that
            // barely hops is a joke. Crouched, you do not jump at all: let go of crouch first.
            if (input.Down(CrewButton.Jump) && !crouching && Time.time - lastGroundedTime <= coyoteTime)
            {
                float h2 = Mathf.Max(0.05f, jumpHeight * jumpMultiplier);
                vy = Mathf.Sqrt(2f * -gravity * h2);
                lastGroundedTime = -999f;   // one jump per contact with the ground
            }

            vy += gravity * Time.deltaTime;

            Vector3 vel = dir * walkSpeed * speedMultiplier * stance + Vector3.up * vy;
            // A blast rides on top of whatever you were doing rather than replacing it: you can
            // still steer while you fly, you just cannot stop. Zero knockback leaves the line
            // above exactly as it was.
            if (knockback != Vector3.zero)
            {
                vel += knockback;
                float decay = cc.isGrounded ? knockbackGroundDecay : knockbackAirDecay;
                knockback *= Mathf.Exp(-decay * Time.deltaTime);
                if (knockback.sqrMagnitude < 0.0025f) knockback = Vector3.zero;
            }
            cc.Move(vel * Time.deltaTime);
        }

        // A shove from outside the player, as a change of velocity in m/s (Explosion today).
        // The horizontal part is added to the walk and fades by itself. The upward part throws
        // you the way a jump does: it sets the climb rather than adding to it, so two blasts in
        // the same instant, or a blast under a player who is already jumping, do not stack into
        // a rocket. The grounded check in Update only resets a falling vy, so the lift survives
        // the frame it lands in. Both parts are capped at maxKnockbackSpeed.
        public void AddImpulse(Vector3 velocityChange)
        {
            // A NaN or an infinity from any caller would put the player's position at NaN, and
            // nothing brings him back from there. Ignore the shove instead.
            if (!IsFinite(velocityChange.x) || !IsFinite(velocityChange.y) || !IsFinite(velocityChange.z)) return;
            // Online, the host does not move a body the client drives: the shove goes to it (9.6).
            if (Net.IsHost && !Net.Drives(Member)) { PlayerSync.SendImpulse(Member.index, velocityChange); return; }

            knockback += new Vector3(velocityChange.x, 0f, velocityChange.z);
            knockback = Vector3.ClampMagnitude(knockback, maxKnockbackSpeed);

            float y = Mathf.Clamp(velocityChange.y, -maxKnockbackSpeed, maxKnockbackSpeed);
            if (y > 0f) vy = Mathf.Max(vy, y);
            else vy += y;
        }

        // Puts the player somewhere else at once (the debug "bring the other player here").
        // The CharacterController keeps its own copy of the position, so it is switched off
        // for the jump; the fall and any shove in progress are dropped with it.
        public void Teleport(Vector3 position, float yaw)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = was;
            vy = 0f;
            knockback = Vector3.zero;
        }

        // float.IsFinite is missing from some of Unity's API profiles; this works in all of them.
        static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        // Grows and shrinks the capsule around fixed feet, and rides the camera down with it.
        void UpdateStance()
        {
            bool wantCrouch = input.Held(CrewButton.Crouch);
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
