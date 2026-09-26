using UnityEngine;

namespace Movers
{
    // One gamepad through the legacy Input Manager, mapped onto the same buttons as the
    // keyboard. The pad copies the keyboard's modes instead of adding new ones (ADR-007's
    // modal budget is spent): a held shoulder hands the right stick to the object, the way R
    // hands the mouse to it.
    //
    //   Left stick  move                  Right stick  look
    //   A           jump                  B (hold)     crouch
    //   LS click    sprint (hold)         Start        pause
    //   RB          grab / drop           RT           throw; hold = use (smoke, pin)
    //   LB (hold)   rotate: right stick turns the object, d-pad left/right rolls it
    //   LT (hold)   reach: right stick up/down pushes the object out / pulls it in
    //   X           interact (E)          Y            alt: wear, drink, take off (F)
    //   D-pad up / right / down / left, no shoulder held: pockets 1 / 2 / 3 / 4
    //
    // Axes come from the per-pad entries J{n}_LX .. J{n}_RT in ProjectSettings/InputManager
    // (joystick 1 and 2). Never the named "Horizontal", "Vertical", "Jump" or "Fire" axes:
    // those listen to every pad at once. Button and axis numbers are the Windows XInput ones
    // and were not checked on a real pad (none was connected during the work); see the report.
    public sealed class GamepadSource : ICrewInputSource
    {
        public readonly int joystick;   // 1-based, as Unity numbers joysticks

        // Look speed at full deflection, in mouse-axis units per second, so the head and the
        // rotate sensitivities keep their meaning: 90 x the head's 2 deg/unit = 180 deg/s.
        public float lookSpeed = 90f;
        // Wheel units per second at full deflection (a notch is 0.1): about 1 m/s of reach and
        // 90 deg/s of roll with PlayerGrab's sensitivities.
        public float reachSpeed = 0.3f;
        public float rollSpeed = 0.3f;

        // Triggers are axes. They become buttons with hysteresis so Down and Up land once per
        // pull, like a mouse button, which the modal right button (tap throws, hold uses)
        // depends on.
        const float TriggerOn = 0.5f;
        const float TriggerOff = 0.3f;
        const float DpadOn = 0.5f;

        const int A = 0, B = 1, X = 2, Y = 3, LB = 4, RB = 5, Start = 7, LS = 8;

        readonly string lx, ly, rx, ry, dx, dy, lt, rt;
        readonly bool hasAxes;
        readonly string label;
        bool ltDown, rtDown;

        public string Label => label;

        public GamepadSource(int joystick)
        {
            this.joystick = Mathf.Clamp(joystick, 1, 8);
            string p = "J" + this.joystick + "_";
            lx = p + "LX"; ly = p + "LY"; rx = p + "RX"; ry = p + "RY";
            dx = p + "DX"; dy = p + "DY"; lt = p + "LT"; rt = p + "RT";
            label = "Gamepad " + this.joystick;

            // Without its axis entries the pad still has its buttons. Checked once here, since
            // asking for an axis that is not set up throws on every call.
            hasAxes = AxisExists(lx) && AxisExists(ry) && AxisExists(dx) && AxisExists(rt);
            if (!hasAxes)
                Debug.LogWarning("[GamepadSource] no axis entries " + p + "* in the Input Manager: " + label +
                                 " has buttons only (install the staged ProjectSettings/InputManager.asset).");
        }

        // The first connected pad, 1-based, or 0 when there is none. A pad unplugged leaves an
        // empty name in its place and shifts nothing, so the numbers stay stable. Allocates the
        // name array: call it on a timer, not every frame.
        public static int FirstConnected()
        {
            var names = Input.GetJoystickNames();
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i])) return i + 1;
            return 0;
        }

        public static bool IsConnected(int joystick)
        {
            var names = Input.GetJoystickNames();
            return joystick >= 1 && joystick <= names.Length && !string.IsNullOrEmpty(names[joystick - 1]);
        }

        static bool AxisExists(string axis)
        {
            // Unity answers an unknown axis with an ArgumentException; caught broadly so a
            // different exception type in a later version still reads as "not set up".
            try { Input.GetAxisRaw(axis); return true; }
            catch (System.Exception) { return false; }
        }

        KeyCode Button(int b) => (KeyCode)((int)KeyCode.Joystick1Button0 + (joystick - 1) * 20 + b);

        bool Pressed(int b) => Input.GetKey(Button(b));

        float Axis(string name) => hasAxes ? Input.GetAxisRaw(name) : 0f;

        public void Poll(ref CrewInputFrame f, float dt)
        {
            Vector2 left = Vector2.ClampMagnitude(new Vector2(Axis(lx), Axis(ly)), 1f);
            Vector2 right = Vector2.ClampMagnitude(new Vector2(Axis(rx), Axis(ry)), 1f);
            float padX = Axis(dx), padY = Axis(dy);

            float ltv = Axis(lt), rtv = Axis(rt);
            ltDown = ltDown ? ltv > TriggerOff : ltv > TriggerOn;
            rtDown = rtDown ? rtv > TriggerOff : rtv > TriggerOn;
            bool rotate = Pressed(LB);

            f.move = left;

            // Squared response: the stick's first half is for aiming at a door handle, the
            // second half for turning round.
            Vector2 look = right * right.magnitude * lookSpeed * dt;
            f.scroll = 0f;
            f.rollDelta = 0f;
            if (ltDown)
            {
                // Reach: the stick's up/down goes to the object, the head only turns.
                f.scroll = right.y * reachSpeed * dt;
                look.y = 0f;
            }
            if (rotate) f.rollDelta = padX * rollSpeed * dt;
            f.lookDelta = look;

            uint h = 0;
            if (Pressed(A)) h |= Bit(CrewButton.Jump);
            if (Pressed(LS)) h |= Bit(CrewButton.Sprint);
            if (Pressed(B)) h |= Bit(CrewButton.Crouch);
            if (Pressed(RB)) h |= Bit(CrewButton.Grab);
            if (rtDown) h |= Bit(CrewButton.Throw);
            if (rotate) h |= Bit(CrewButton.Rotate);
            if (ltDown) h |= Bit(CrewButton.Reach);
            if (Pressed(X)) h |= Bit(CrewButton.Interact);
            if (Pressed(Y)) h |= Bit(CrewButton.Alt);
            if (Pressed(Start)) h |= Bit(CrewButton.Pause);

            // The d-pad is the pockets only while no shoulder has claimed it for the object.
            if (!rotate && !ltDown)
            {
                if (padY > DpadOn) h |= Bit(CrewButton.Pocket1);
                else if (padX > DpadOn) h |= Bit(CrewButton.Pocket2);
                else if (padY < -DpadOn) h |= Bit(CrewButton.Pocket3);
                else if (padX < -DpadOn) h |= Bit(CrewButton.Pocket4);
            }
            f.held = h;
        }

        static uint Bit(CrewButton b) => 1u << (int)b;
    }
}
