using UnityEngine;

namespace Movers
{
    // Every button a crew member can press. Names are actions, not keys: the keyboard and a
    // gamepad map onto the same list (ADR-009, 03_TECHNICAL/SLICE_ARCHITECTURE.md).
    public enum CrewButton
    {
        Jump, Sprint, Crouch,
        Grab,       // LMB: grab / drop
        Throw,      // RMB: throw; hold = use a HeldUsable (smoke, pin a grenade)
        Rotate,     // R held: the look goes to the held object
        Reach,      // gamepad only: held, the right stick pushes the held object out or in
        Interact,   // E: open, close, talk, deliver, drive
        Alt,        // F: wear, drink, take off
        Pocket1, Pocket2, Pocket3, Pocket4,
        Pause,
        Count
    }

    // One frame of one player's input.
    public struct CrewInputFrame
    {
        public Vector2 move;        // -1..1, x strafe, y forward (GetAxisRaw semantics: keys snap)
        public Vector2 lookDelta;   // mouse-axis units this frame; consumers apply their own sensitivity
        public float scroll;        // reach request, mouse-wheel units (0.1 per notch)
        public float rollDelta;     // roll request while Rotate is held, wheel units
        public uint held;           // bit per CrewButton
    }

    public interface ICrewInputSource
    {
        string Label { get; }                           // "Keyboard", "Gamepad 1", "None"
        void Poll(ref CrewInputFrame frame, float dt);  // fills move, lookDelta, scroll, rollDelta, held
    }

    // One per player, on the player root, before every consumer. Nothing in gameplay reads
    // UnityEngine.Input except the input sources and the debug dispatcher (SliceDebug).
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class CrewInput : MonoBehaviour
    {
        ICrewInputSource source;
        CrewInputFrame frame;
        uint prevHeld, down, up, consumed;
        int consumedFrame = -1;

        public ICrewInputSource Source => source;
        public string SourceLabel => source != null ? source.Label : "None";

        // Frozen: no movement, no buttons (end screen, intro card, sitting in the truck is
        // handled by the truck itself). The source keeps polling so nothing fires on unmute.
        public bool Muted { get; set; }

        public Vector2 Move => Muted ? Vector2.zero : frame.move;
        public Vector2 LookDelta => Muted ? Vector2.zero : frame.lookDelta;
        public float Scroll => Muted ? 0f : frame.scroll;
        public float RollDelta => Muted ? 0f : frame.rollDelta;

        // This poll's frame, unmuted: what the online client sends to the host (NETCODE_SLICE 9.2).
        public CrewInputFrame RawFrame => frame;

        public bool Held(CrewButton b) => !Muted && (frame.held & Bit(b)) != 0;
        public bool Down(CrewButton b) => !Muted && (down & Bit(b)) != 0;
        public bool Up(CrewButton b) => !Muted && (up & Bit(b)) != 0;

        // True once per press, per player: the first system to act on a press takes it, and
        // the others see it as not pressed this frame. Replaces the old KeyCode claims.
        public bool TryConsume(CrewButton b)
        {
            if (consumedFrame != Time.frameCount) { consumed = 0; consumedFrame = Time.frameCount; }
            uint bit = Bit(b);
            if (!Down(b) || (consumed & bit) != 0) return false;
            consumed |= bit;
            return true;
        }

        public bool IsConsumed(CrewButton b)
        {
            return consumedFrame == Time.frameCount && (consumed & Bit(b)) != 0;
        }

        // Swapping sources clears every held bit, so a swap in the middle of a hold never fires
        // a phantom Up (which would throw an armed grenade).
        public void SetSource(ICrewInputSource newSource)
        {
            source = newSource;
            frame = default;
            prevHeld = down = up = 0;
            ignoreNextHeld = true;
        }

        bool ignoreNextHeld;

        // Takes the next poll's held bits as the baseline, so no Down or Up fires from a gap in
        // the input (the online pause, 9.2). The same path as SetSource, without the swap.
        public void ResyncHeld()
        {
            ignoreNextHeld = true;
        }

        static uint Bit(CrewButton b) => 1u << (int)b;

        void Awake()
        {
            if (source == null) source = new NullInputSource();
        }

        void Update()
        {
            var f = new CrewInputFrame();
            if (source != null) source.Poll(ref f, Time.deltaTime);
            frame = f;
            if (ignoreNextHeld)
            {
                // After a swap, only presses that start from now count.
                prevHeld = frame.held;
                ignoreNextHeld = false;
            }
            down = frame.held & ~prevHeld;
            up = prevHeld & ~frame.held;
            prevHeld = frame.held;
        }
    }

    // The keyboard and mouse, reproducing the numbers the carry was tuned with: mouse axes as
    // the Input Manager scales them, the wheel at 0.1 per notch, keys snapping like GetAxisRaw.
    // It never reads the named axes "Horizontal", "Vertical", "Jump" or "Fire1-3": in this
    // project those carry joystick entries for every pad, which would leak a gamepad into P1.
    public sealed class KeyboardMouseSource : ICrewInputSource
    {
        public string Label => "Keyboard";

        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode crouchKey = KeyCode.LeftControl;
        public KeyCode rotateKey = KeyCode.R;
        public KeyCode interactKey = KeyCode.E;
        public KeyCode altKey = KeyCode.F;
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode pauseKey = KeyCode.Escape;

        public void Poll(ref CrewInputFrame f, float dt)
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            f.move = new Vector2(x, y);

            f.lookDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            f.scroll = wheel;
            f.rollDelta = wheel;   // same wheel: consumers pick one by whether Rotate is held

            uint h = 0;
            if (Input.GetKey(jumpKey)) h |= B(CrewButton.Jump);
            if (Input.GetKey(sprintKey)) h |= B(CrewButton.Sprint);
            if (Input.GetKey(crouchKey)) h |= B(CrewButton.Crouch);
            if (Input.GetMouseButton(0)) h |= B(CrewButton.Grab);
            if (Input.GetMouseButton(1)) h |= B(CrewButton.Throw);
            if (Input.GetKey(rotateKey)) h |= B(CrewButton.Rotate);
            if (Input.GetKey(interactKey)) h |= B(CrewButton.Interact);
            if (Input.GetKey(altKey)) h |= B(CrewButton.Alt);
            if (Input.GetKey(KeyCode.Alpha1)) h |= B(CrewButton.Pocket1);
            if (Input.GetKey(KeyCode.Alpha2)) h |= B(CrewButton.Pocket2);
            if (Input.GetKey(KeyCode.Alpha3)) h |= B(CrewButton.Pocket3);
            if (Input.GetKey(KeyCode.Alpha4)) h |= B(CrewButton.Pocket4);
            if (Input.GetKey(pauseKey)) h |= B(CrewButton.Pause);
            f.held = h;
        }

        static uint B(CrewButton b) => 1u << (int)b;
    }

    // A player nobody drives: stands still.
    public sealed class NullInputSource : ICrewInputSource
    {
        public string Label => "None";
        public void Poll(ref CrewInputFrame f, float dt) { f = default; }
    }
}
