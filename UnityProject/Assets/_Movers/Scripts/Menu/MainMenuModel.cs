using LumaFlow;
using UnityEngine;

namespace Movers
{
    public enum MenuPage { Title, Options, Controls }

    // What the title screen shows: the page, the selected row, the device the hints are drawn
    // for, whether a gamepad is plugged in. The view rebuilds when Version changes and only
    // then; the model changes on a key, a click, or once a second for the pad check.
    public sealed class MainMenuModel
    {
        public enum TitleRow { Solo, Duo, Options, Controls, Quit }
        public const int TitleRows = 5;

        // Same rows, same order as the in-game options (HudPauseMenu.OptionRow).
        public enum OptionRow { Master, Music, Sfx, Voice, Ambience, Ui, Sensitivity, InvertY, Language, Layout, Hints, Back }
        public const int OptionRows = 12;

        public readonly State<int> Version = new State<int>(0);

        public MenuPage Page { get; private set; } = MenuPage.Title;
        public int Selected { get; private set; }
        // False while the mouse drives: only the button under it is lit, not a second one.
        public bool ShowSelection { get; private set; } = true;
        public MenuDevice Device { get; private set; } = MenuDevice.Keyboard;
        public bool PadConnected { get; private set; }
        public MenuDevice ControlsTab { get; private set; } = MenuDevice.Keyboard;
        public bool Leaving { get; private set; }

        // One fill per volume row, owned here so a rebuild keeps the same tape.
        public readonly UiFill[] VolumeFills = { new UiFill(), new UiFill(), new UiFill(), new UiFill(), new UiFill(), new UiFill() };

        public int RowCount => Page == MenuPage.Title ? TitleRows : Page == MenuPage.Options ? OptionRows : 1;

        public void Touch() => Version.Value++;

        public void ShowPage(MenuPage page, int select)
        {
            Page = page;
            Selected = Mathf.Clamp(select, 0, RowCount - 1);
            if (page == MenuPage.Controls) ControlsTab = Device;
            Touch();
        }

        public bool Select(int row)
        {
            row = Mathf.Clamp(row, 0, RowCount - 1);
            if (row == Selected && ShowSelection) return false;
            Selected = row;
            ShowSelection = true;
            Touch();
            return true;
        }

        public void Step(int delta)
        {
            int n = RowCount;
            ShowSelection = true;
            Selected = (Selected + delta + n) % n;
            Touch();
        }

        public void SetShowSelection(bool on)
        {
            if (ShowSelection == on) return;
            ShowSelection = on;
            Touch();
        }

        public void SetDevice(MenuDevice d)
        {
            if (Device == d) return;
            Device = d;
            Touch();
        }

        public void SetPad(bool connected)
        {
            if (PadConnected == connected) return;
            PadConnected = connected;
            Touch();
        }

        public void SetControlsTab(MenuDevice d)
        {
            if (ControlsTab == d) return;
            ControlsTab = d;
            Touch();
        }

        public void Leave()
        {
            Leaving = true;
            Touch();
        }
    }
}
