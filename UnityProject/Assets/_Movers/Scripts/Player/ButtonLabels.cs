namespace Movers
{
    // What to call a button on screen, for the device the player is holding: "[E] Open" on the
    // keyboard, "[X] Open" on a pad. Constant strings, so a prompt costs nothing to label.
    public static class ButtonLabels
    {
        public static string For(ICrewInputSource source, CrewButton b)
        {
            return LocalDevicesSource.IsPad(source) ? Pad(b) : Keyboard(b);
        }

        public static string For(CrewInput input, CrewButton b)
        {
            return For(input != null ? input.Source : null, b);
        }

        static string Keyboard(CrewButton b)
        {
            switch (b)
            {
                case CrewButton.Jump: return "Space";
                case CrewButton.Sprint: return "Shift";
                case CrewButton.Crouch: return "Ctrl";
                case CrewButton.Grab: return "LMB";
                case CrewButton.Throw: return "RMB";
                case CrewButton.Rotate: return "R";
                case CrewButton.Reach: return "Wheel";
                case CrewButton.Interact: return "E";
                case CrewButton.Alt: return "F";
                case CrewButton.Pocket1: return "1";
                case CrewButton.Pocket2: return "2";
                case CrewButton.Pocket3: return "3";
                case CrewButton.Pocket4: return "4";
                case CrewButton.Pause: return "Esc";
            }
            return "?";
        }

        static string Pad(CrewButton b)
        {
            switch (b)
            {
                case CrewButton.Jump: return "A";
                case CrewButton.Sprint: return "LS";
                case CrewButton.Crouch: return "B";
                case CrewButton.Grab: return "RB";
                case CrewButton.Throw: return "RT";
                case CrewButton.Rotate: return "LB";
                case CrewButton.Reach: return "LT";
                case CrewButton.Interact: return "X";
                case CrewButton.Alt: return "Y";
                case CrewButton.Pocket1: return "Up";
                case CrewButton.Pocket2: return "Right";
                case CrewButton.Pocket3: return "Down";
                case CrewButton.Pocket4: return "Left";
                case CrewButton.Pause: return "Start";
            }
            return "?";
        }
    }
}
