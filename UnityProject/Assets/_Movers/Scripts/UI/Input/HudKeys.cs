using UnityEngine;

namespace Movers
{
    // The HUD's own keys, which are not gameplay and so not CrewButtons (that list is a frozen
    // contract, ADR-009): the controls sheet's toggle, Tab on the keyboard and Back/View
    // (button 6) on a pad. Read from the device of the one player asking, so P1's Tab never
    // opens P2's sheet. A future contract can add CrewButton.Controls and retire this.
    public static class HudKeys
    {
        const int PadBack = 6;

        public static bool ControlsPressed(CrewInput input)
        {
            if (input == null) return false;
            var src = input.Source;
            if (src is KeyboardMouseSource) return Input.GetKeyDown(KeyCode.Tab);
            if (src is GamepadSource pad)
                return Input.GetKeyDown((KeyCode)((int)KeyCode.Joystick1Button0 + (pad.joystick - 1) * 20 + PadBack));
            return false;
        }
    }
}
