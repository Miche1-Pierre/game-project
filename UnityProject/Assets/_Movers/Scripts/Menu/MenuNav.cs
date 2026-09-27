using UnityEngine;

namespace Movers
{
    public enum MenuDevice { Keyboard, Gamepad }

    // Menu navigation from any keyboard or any gamepad, through the legacy Input Manager
    // (ADR-009 keeps it). Before a game nobody owns a device yet, so the title screen reads the
    // devices themselves rather than a player's CrewInput; gameplay code still never does.
    //
    // UI Toolkit's own navigation is switched off on the menu's panel (MenuPanel): with the
    // legacy axes it would also answer to every pad, and a key would press twice. This class is
    // the one reader, and the menu moves its own selection.
    //
    //   Up / Down      arrows, W / S, Z (AZERTY), left stick, d-pad    (repeats while held)
    //   Left / Right   arrows, A / D, Q (AZERTY), left stick, d-pad    (option values)
    //   Submit         Enter, keypad Enter, Space, pad A
    //   Back           Escape, Backspace, pad B
    //   Tab left/right LB / RB on a pad (the keyboard uses left / right)
    //
    // Reads nothing per frame that allocates.
    public sealed class MenuNav
    {
        public int Vertical { get; private set; }     // +1 up, -1 down, this frame (with repeat)
        public int Horizontal { get; private set; }   // +1 right, -1 left, this frame (with repeat)
        public bool Submit { get; private set; }
        public bool Back { get; private set; }
        public int Tab { get; private set; }          // -1 / +1 this frame
        public bool AnyKey { get; private set; }
        public MenuDevice LastDevice { get; private set; } = MenuDevice.Keyboard;

        public float repeatDelay = 0.38f;
        public float repeatEvery = 0.11f;
        const float StickOn = 0.55f;

        // The pad axes of the first two pads (the Input Manager's J1_LY, J1_DY ...), or none.
        // Checked once: asking for an axis that is not set up throws on every call.
        static bool axesChecked;
        static string[] vAxes = new string[0], hAxes = new string[0];

        // How many pad axes the menu reads (0 = the stick and the d-pad do nothing). Known after
        // the first Update; the Play-mode test fails on 0 when the Input Manager has J1_LY.
        public static int PadAxisCount => vAxes.Length + hAxes.Length;
        public static bool AxesChecked => axesChecked;

        int heldV, heldH;
        float nextV, nextH;
        bool primed;

        // No constructor work on purpose. The owner builds this in a field initializer, which
        // runs inside the MonoBehaviour's constructor, where Unity refuses Input.GetAxisRaw
        // (UnityException, swallowed by Exists): the check would find no axis and the pad
        // would be dead for the whole session. So the axes are checked on the first Update.

        // Forget what is held now: the key that opened a page is not a press on the new one.
        public void Reset()
        {
            primed = false;
            Vertical = Horizontal = Tab = 0;
            Submit = Back = AnyKey = false;
        }

        public void Update()
        {
            if (!axesChecked) CheckAxes();
            float now = Time.unscaledTime;
            bool kbUp = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z);
            bool kbDown = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            bool kbLeft = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q);
            bool kbRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
            float padV = Strongest(vAxes), padH = Strongest(hAxes);

            int v = kbUp ? 1 : kbDown ? -1 : padV > StickOn ? 1 : padV < -StickOn ? -1 : 0;
            int h = kbRight ? 1 : kbLeft ? -1 : padH > StickOn ? 1 : padH < -StickOn ? -1 : 0;

            bool padSubmit = Input.GetKeyDown(KeyCode.JoystickButton0);
            bool padBack = Input.GetKeyDown(KeyCode.JoystickButton1);
            bool padTabL = Input.GetKeyDown(KeyCode.JoystickButton4);
            bool padTabR = Input.GetKeyDown(KeyCode.JoystickButton5);
            bool kbSubmit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
            bool kbBack = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);

            if (!primed)
            {
                // First frame after a Reset: what is held is the baseline, nothing fires, and a
                // direction still held does not repeat on the new page until it is let go.
                primed = true;
                heldV = v; heldH = h;
                nextV = v != 0 ? float.MaxValue : now + repeatDelay;
                nextH = h != 0 ? float.MaxValue : now + repeatDelay;
                Vertical = Horizontal = Tab = 0;
                Submit = Back = AnyKey = false;
                return;
            }

            Vertical = Step(v, ref heldV, ref nextV, now);
            Horizontal = Step(h, ref heldH, ref nextH, now);
            Submit = kbSubmit || padSubmit;
            Back = kbBack || padBack;
            Tab = padTabL ? -1 : padTabR ? 1 : 0;
            AnyKey = Vertical != 0 || Horizontal != 0 || Submit || Back || Tab != 0;

            bool padUsed = padSubmit || padBack || padTabL || padTabR || Mathf.Abs(padV) > StickOn || Mathf.Abs(padH) > StickOn;
            bool keysUsed = kbSubmit || kbBack || kbUp || kbDown || kbLeft || kbRight;
            if (padUsed) LastDevice = MenuDevice.Gamepad;
            else if (keysUsed) LastDevice = MenuDevice.Keyboard;
        }

        // The mouse took over (it moved or clicked): hints and selection follow the keyboard's.
        public void MouseUsed() { LastDevice = MenuDevice.Keyboard; }

        // An edge on press, then a repeat while held.
        int Step(int dir, ref int held, ref float next, float now)
        {
            if (dir == 0) { held = 0; return 0; }
            if (dir != held)
            {
                held = dir;
                next = now + repeatDelay;
                return dir;
            }
            if (now >= next)
            {
                next = now + repeatEvery;
                return dir;
            }
            return 0;
        }

        static float Strongest(string[] axes)
        {
            float best = 0f;
            for (int i = 0; i < axes.Length; i++)
            {
                float a = Input.GetAxisRaw(axes[i]);
                if (Mathf.Abs(a) > Mathf.Abs(best)) best = a;
            }
            return best;
        }

        // GamepadSource's per-pad axes: stick up reads positive on J{n}_LY, as the game uses it.
        // Never the named "Vertical"/"Horizontal" axes: they also answer to W/S/A/D, which would
        // make a key press count twice. Called from Update only, never from a constructor.
        static void CheckAxes()
        {
            axesChecked = true;
            var v = new System.Collections.Generic.List<string>(4);
            var h = new System.Collections.Generic.List<string>(4);
            for (int pad = 1; pad <= 2; pad++)
            {
                string p = "J" + pad + "_";
                if (Exists(p + "LY")) v.Add(p + "LY");
                if (Exists(p + "DY")) v.Add(p + "DY");
                if (Exists(p + "LX")) h.Add(p + "LX");
                if (Exists(p + "DX")) h.Add(p + "DX");
            }
            vAxes = v.ToArray();
            hAxes = h.ToArray();
        }

        static bool Exists(string axis)
        {
            try { Input.GetAxisRaw(axis); return true; }
            catch (System.Exception) { return false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            axesChecked = false;
            vAxes = new string[0];
            hAxes = new string[0];
        }
    }
}
