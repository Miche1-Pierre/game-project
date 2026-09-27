using LumaFlow;
using UnityEngine;

namespace Movers
{
    public enum MenuPage { Title, Options, Controls, Online, Hosting, Joining }

    // What the title screen shows: the page, the selected row, the device the hints are drawn
    // for, whether a gamepad is plugged in. The view rebuilds when Version changes and only
    // then; the model changes on a key, a click, or once a second for the pad check.
    public sealed class MainMenuModel
    {
        public enum TitleRow { Solo, Duo, Online, Options, Controls, Quit }
        public const int TitleRows = 6;

        // Same rows, same order as the in-game options (HudPauseMenu.OptionRow).
        public enum OptionRow { Master, Music, Sfx, Voice, Ambience, Ui, Sensitivity, InvertY, Language, Layout, Hints, Back }
        public const int OptionRows = 12;

        // The online pages (NETCODE_SLICE 3.3). Direct host is last: a development build option.
        public enum OnlineRow { Host, Join, Back, HostDirect }
        public static int OnlineRows => Debug.isDebugBuild ? 4 : 3;
        public enum HostRow { Copy, Start, Cancel }
        public const int HostRows = 3;
        public enum JoinRow { Field, Paste, Connect, Back }
        public const int JoinRows = 4;

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

        // ---- online ----

        // The join page's field: its text and its focus live here, so a rebuild keeps both.
        public readonly State<string> Code = new State<string>("");
        public readonly FocusNode CodeFocus = new FocusNode();
        // NetSession as last shown (polled by MainMenu; the getters never create a session).
        public NetStatus NetStatus { get; private set; }
        public NetError NetError { get; private set; }
        public string JoinCode { get; private set; }
        public bool PeerConnected { get; private set; }
        public bool Copied { get; private set; }
        // A Loc key shown once on the title page (NetSession.TakeMenuMessage: "the host left").
        public string Message { get; private set; }

        public int RowCount
        {
            get
            {
                switch (Page)
                {
                    case MenuPage.Title: return TitleRows;
                    case MenuPage.Options: return OptionRows;
                    case MenuPage.Online: return OnlineRows;
                    case MenuPage.Hosting: return HostRows;
                    case MenuPage.Joining: return JoinRows;
                    default: return 1;
                }
            }
        }

        public void Touch() => Version.Value++;

        public void ShowPage(MenuPage page, int select)
        {
            Page = page;
            Selected = Mathf.Clamp(select, 0, RowCount - 1);
            if (page == MenuPage.Controls) ControlsTab = Device;
            if (page != MenuPage.Title) Message = null;
            Copied = false;
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

        // ---- online ----

        public void SetNet(NetStatus status, NetError error, string joinCode, bool peerConnected)
        {
            if (status == NetStatus && error == NetError && joinCode == JoinCode && peerConnected == PeerConnected) return;
            NetStatus = status;
            NetError = error;
            JoinCode = joinCode;
            PeerConnected = peerConnected;
            Touch();
        }

        public void SetCopied()
        {
            if (Copied) return;
            Copied = true;
            Touch();
        }

        public void SetMessage(string locKey)
        {
            if (Message == locKey) return;
            Message = locKey;
            Touch();
        }
    }
}
