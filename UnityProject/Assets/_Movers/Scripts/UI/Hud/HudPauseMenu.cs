using LumaFlow;
using UnityEngine;

namespace Movers
{
    public enum HudPausePage { Closed, Main, Options, Controls }

    // One player's pause menu (Esc, or Start on a pad), in that player's own view.
    //
    // In split screen the game goes on for the other player: only this player's input is
    // muted (CrewInput.Muted), their menu reads their own device through the mute, the way
    // MutedInputReader does. Once every player on screen with a device has a menu open, nobody
    // is playing: time and sound stop, and they start again as soon as one menu closes. Alone
    // on screen, that is the moment the menu opens.
    //
    // Navigation: up/down (stick, d-pad, arrows or W/S), left/right to change a setting,
    // confirm with Jump or Interact (Space/E, A/X; Enter too on a keyboard), back with Pause or
    // Crouch (Esc, Start/B). The mouse clicks the same buttons.
    //
    // Named Hud* because MENU staged a global pause called PauseMenu: the two are different
    // designs and only one may answer Esc/Start in a build (see Enabled, and INTEGRATION.md).
    public sealed class HudPauseMenu
    {
        public readonly State<int> Version = new State<int>(0);
        public HudPausePage Page { get; private set; } = HudPausePage.Closed;
        public int Selected { get; private set; }
        public bool IsOpen => Page != HudPausePage.Closed;

        readonly CrewMember member;
        bool mutedByUs;
        float repeatAt;
        int heldDir;

        const float Deadzone = 0.55f;
        const float RepeatDelay = 0.38f;
        const float RepeatEvery = 0.11f;

        public static int OpenCount { get; private set; }

        // Off: the HUD offers no pause menu and leaves Esc/Start alone. For a build that keeps
        // a different, global pause instead of this one, so the two never answer the same key.
        public static bool Enabled = true;

        // Time and sound were stopped here because every player on screen is in a menu.
        public static bool WorldPaused { get; private set; }
        static float savedTimeScale = 1f;

        public HudPauseMenu(CrewMember member)
        {
            this.member = member;
        }

        // ---- the rows ----

        public enum MainRow { Resume, Options, Controls, Menu }
        public const int MainRows = 4;

        public enum OptionRow { Master, Music, Sfx, Voice, Ambience, Ui, Sensitivity, InvertY, Language, Layout, Hints, Back }
        public const int OptionRows = 12;

        int RowCount => Page == HudPausePage.Main ? MainRows : Page == HudPausePage.Options ? OptionRows : 1;

        // ---- per frame ----

        public void Tick(bool visible)
        {
            var input = member != null ? member.Input : null;
            if (input == null) return;

            if (!IsOpen)
            {
                if (Enabled && visible && CanOpen() && input.TryConsume(CrewButton.Pause)) Open();
                return;
            }
            if (!visible || Session.IsOver || !Enabled) { Close(true); return; }
            // A partner can leave the screen or lose his device while menus are open.
            SyncWorldPause();

            // Navigation, read through our own mute.
            input.Muted = false;
            Vector2 move = input.Move;
            bool confirm = input.Down(CrewButton.Jump) || input.Down(CrewButton.Interact) ||
                           (HasKeyboard(input) && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)));
            bool back = input.Down(CrewButton.Pause) || input.Down(CrewButton.Crouch);
            // The pad's d-pad arrives as the pocket buttons (GamepadSource): up, right, down, left.
            int pad = input.Down(CrewButton.Pocket1) ? 1 : input.Down(CrewButton.Pocket2) ? 4
                    : input.Down(CrewButton.Pocket3) ? 2 : input.Down(CrewButton.Pocket4) ? 3 : 0;
            input.Muted = true;

            int dir = pad != 0 ? pad : Direction(move);
            if (dir != 0)
            {
                if (dir == 1) Step(-1);
                else if (dir == 2) Step(1);
                else if (dir == 3) Change(-1);
                else Change(1);
            }
            if (confirm) Confirm();
            else if (back) Back();
        }

        bool CanOpen()
        {
            var s = GameSession.Current;
            if (Session.IsOver) return false;
            if (s != null && (s.IntroCardShowing || s.CrewReleasePending)) return false;
            return !member.Input.Muted;
        }

        // 1 up, 2 down, 3 left, 4 right, 0 none. A held direction repeats after a short wait.
        int Direction(Vector2 move)
        {
            int d = 0;
            if (move.y > Deadzone) d = 1;
            else if (move.y < -Deadzone) d = 2;
            else if (move.x < -Deadzone) d = 3;
            else if (move.x > Deadzone) d = 4;
            float now = Time.unscaledTime;
            if (d == 0) { heldDir = 0; return 0; }
            if (d != heldDir)
            {
                heldDir = d;
                repeatAt = now + RepeatDelay;
                return d;
            }
            if (now < repeatAt) return 0;
            repeatAt = now + RepeatEvery;
            return d;
        }

        // ---- actions (also called by the buttons) ----

        public void Open()
        {
            if (IsOpen) return;
            Page = HudPausePage.Main;
            Selected = 0;
            heldDir = 0;
            if (!member.Input.Muted)
            {
                member.Input.Muted = true;
                mutedByUs = true;
            }
            OpenCount++;
            SyncWorldPause();
            if (HasKeyboard(member.Input))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            UiAudio.Open();
            Version.Value++;
        }

        // quiet: torn down (scene change, HUD gone), not closed by the player.
        public void Close(bool quiet = false)
        {
            if (!IsOpen) return;
            Page = HudPausePage.Closed;
            OpenCount = Mathf.Max(0, OpenCount - 1);
            if (mutedByUs)
            {
                mutedByUs = false;
                // The session may have frozen the crew meanwhile (the run ended): it keeps them.
                var s = GameSession.Current;
                bool sessionHolds = Session.IsOver || (s != null && (s.IntroCardShowing || s.CrewReleasePending));
                if (member != null && member.Input != null && !sessionHolds) member.Input.Muted = false;
            }
            SyncWorldPause();
            if (!quiet && member != null && HasKeyboard(member.Input) && !Session.IsOver)
                Cursor.lockState = CursorLockMode.Locked;
            if (!quiet) UiAudio.Close();
            Version.Value++;
        }

        public void ShowPage(HudPausePage page, int select = 0)
        {
            if (!IsOpen) return;
            Page = page;
            Selected = select;
            UiAudio.Click();
            Version.Value++;
        }

        public void Select(int row)
        {
            row = Mathf.Clamp(row, 0, RowCount - 1);
            if (row == Selected) return;
            Selected = row;
            UiAudio.Hover();
            Version.Value++;
        }

        void Step(int delta)
        {
            int n = RowCount;
            Selected = (Selected + delta + n) % n;
            // Online there is no split screen: the Layout row is hidden, and stepped over.
            if (Net.IsOnline && Page == HudPausePage.Options && Selected == (int)OptionRow.Layout)
                Selected = (Selected + delta + n) % n;
            UiAudio.Hover();
            Version.Value++;
        }

        void Confirm()
        {
            switch (Page)
            {
                case HudPausePage.Main: Activate((MainRow)Selected); break;
                case HudPausePage.Options:
                    if (Selected == (int)OptionRow.Back) ShowPage(HudPausePage.Main, (int)MainRow.Options);
                    else Change(1);
                    break;
                default: ShowPage(HudPausePage.Main, (int)MainRow.Controls); break;
            }
        }

        void Back()
        {
            if (Page == HudPausePage.Main) Close();
            else ShowPage(HudPausePage.Main, Page == HudPausePage.Options ? (int)MainRow.Options : (int)MainRow.Controls);
        }

        public void Activate(MainRow row)
        {
            switch (row)
            {
                case MainRow.Resume: UiAudio.Click(); Close(); break;
                case MainRow.Options: ShowPage(HudPausePage.Options); break;
                case MainRow.Controls: ShowPage(HudPausePage.Controls); break;
                case MainRow.Menu:
                    UiAudio.Click();
                    Close(true);
                    // Online: the host's leave ends the run for both, the client's leaves alone
                    // (NETCODE_SLICE 10); NetSession loads the menu.
                    if (Net.IsOnline) NetSession.LeaveToMenu();
                    else SceneFlow.LoadMenu();
                    break;
            }
        }

        // Left/right, or confirm, on an options row.
        public void Change(int delta)
        {
            if (Page != HudPausePage.Options) return;
            switch ((OptionRow)Selected)
            {
                case OptionRow.Master: Volume(AudioChannel.Master, delta); break;
                case OptionRow.Music: Volume(AudioChannel.Music, delta); break;
                case OptionRow.Sfx: Volume(AudioChannel.Sfx, delta); break;
                case OptionRow.Voice: Volume(AudioChannel.Voice, delta); break;
                case OptionRow.Ambience: Volume(AudioChannel.Ambience, delta); break;
                case OptionRow.Ui: Volume(AudioChannel.Ui, delta); break;
                case OptionRow.Sensitivity:
                    GameSettings.LookSensitivity = Mathf.Round((GameSettings.LookSensitivity + 0.1f * delta) * 10f) / 10f;
                    break;
                case OptionRow.InvertY: GameSettings.InvertY = !GameSettings.InvertY; break;
                case OptionRow.Language:
                    GameSettings.Language = GameSettings.Language == Language.French ? Language.English : Language.French;
                    break;
                case OptionRow.Layout:
                    if (Net.IsOnline) return;
                    GameSettings.Layout = GameSettings.Layout == SplitLayout.SideBySide ? SplitLayout.Stacked : SplitLayout.SideBySide;
                    var split = Object.FindAnyObjectByType<SplitScreen>();
                    if (split != null) split.Apply();
                    break;
                case OptionRow.Hints: GameSettings.HintsShown = !GameSettings.HintsShown; break;
                default: return;
            }
            UiAudio.Click();
            Version.Value++;
        }

        static void Volume(AudioChannel channel, int delta)
        {
            float v = Mathf.Round((GameAudio.GetVolume(channel) + 0.1f * delta) * 10f) / 10f;
            GameAudio.SetVolume(channel, Mathf.Clamp01(v));
        }

        // Stops time and sound when every player on screen with a device has a menu open, and
        // gives them back as soon as that is no longer true. Edge-triggered: another system's
        // own use of timeScale is left alone the rest of the time.
        static void SyncWorldPause()
        {
            // Online the world never stops (NETCODE_SLICE 10): a pause that was on is given back.
            if (Net.IsOnline)
            {
                if (!WorldPaused) return;
                WorldPaused = false;
                Time.timeScale = savedTimeScale;
                AudioListener.pause = false;
                return;
            }
            bool all = OpenCount > 0 && OpenCount >= PlayersOnScreen();
            if (all == WorldPaused) return;
            WorldPaused = all;
            if (all)
            {
                savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            else Time.timeScale = savedTimeScale;
            AudioListener.pause = all;
        }

        // Whether this player's menu reads the keyboard (Enter, the cursor). Online the one local
        // player holds the keyboard whatever its source is (keys and pad together).
        static bool HasKeyboard(CrewInput input)
        {
            var src = input != null ? input.Source : null;
            if (src is KeyboardMouseSource) return true;
            return Net.IsOnline && src != null && !(src is NullInputSource) && !(src is RemoteInputSource);
        }

        // Players who could open a menu: a view on screen and a device (not NullInputSource).
        public static int PlayersOnScreen()
        {
            int shown = 0;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m != null && m.View != null && m.View.isActiveAndEnabled && !(m.Input != null && m.Input.Source is NullInputSource)) shown++;
            }
            return shown;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OpenCount = 0;
            Enabled = true;
            WorldPaused = false;
            savedTimeScale = 1f;
        }
    }
}
