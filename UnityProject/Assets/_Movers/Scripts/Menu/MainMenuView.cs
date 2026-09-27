using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // The title screen's widgets, from UICORE's kit: the wooden logo hung in a frame of leafy
    // branches, a paper tag with the tagline, the wooden buttons, a tag under them saying what
    // the selected button does (or whether player 2 has a gamepad), the key hints for the
    // device in hand, and the options board and the controls sheet on their own pages. All on
    // the left of the screen: the living landscape keeps the rest.
    //
    // The column and the key hints are drawn 1.3 times their laid-out size (UiZoom), so the
    // boards, the words and the keys read from the couch. The column's zoom gives way when a
    // page would reach the key hints at the bottom: at 1920x1080 and 1280x720 alike (the panel
    // scales with the screen height), the title page and the options fit at 1.3, the controls
    // sheet a little under it.
    //
    // Rebuilt only when the model's Version changes.
    public static class MainMenuView
    {
        const float Left = 84f;
        const float Top = 48f;
        const float ButtonWidth = 400f;
        const float FooterMargin = 30f;
        const float FooterKey = 34f;
        // The key hints' height once zoomed (a 34 key in a 7 + 7 padded chip, times 1.3), plus
        // their margin and a gap: the part of the screen the column leaves them.
        const float FooterRoom = FooterMargin + (FooterKey + 14f) * UiZoom.MenuFactor + 16f;

        static readonly UiPlace ColumnPlace = new UiPlace(0f, 0f, 0f, 0f, Left, Top);
        static readonly UiPlace FooterPlace = UiPlace.BottomLeft(FooterMargin);
        static readonly UiZoom ColumnZoom = new UiZoom(UiZoom.MenuFactor, 0f, 0f, Left + 40f, Top + FooterRoom);
        static readonly UiZoom FooterZoom = new UiZoom(UiZoom.MenuFactor, 0f, 100f, FooterMargin * 2f, FooterMargin * 2f);
        static readonly UiPlace BuildPlace = UiPlace.BottomRight(26f);
        static readonly UiTransform TagTilt = new UiTransform(-2.2f);
        static readonly UiName ColumnName = new UiName("menu-column");

        public static Widget Build(MainMenuModel m, System.Action<int> activate, System.Action<int, int> change,
                                   System.Action<string> submitCode) =>
            new ReactiveBuilder<int>(m.Version, _ => Screen(m, activate, change, submitCode));

        static Widget Screen(MainMenuModel m, System.Action<int> activate, System.Action<int, int> change, System.Action<string> submitCode)
        {
            Widget page;
            switch (m.Page)
            {
                case MenuPage.Options: page = Options(m, activate, change); break;
                case MenuPage.Controls: page = Controls(m, activate, change); break;
                case MenuPage.Online: page = Online(m, activate); break;
                case MenuPage.Hosting: page = Hosting(m, activate); break;
                case MenuPage.Joining: page = Joining(m, activate, submitCode); break;
                default: page = Title(m, activate); break;
            }
            return new Stack(new[]
            {
                UiPlace.Fill.Widget,
                UiKit.With(ColumnPlace, ColumnName, UiKit.With(ColumnZoom, page)),
                UiKit.With(FooterPlace, UiKit.With(FooterZoom, Footer(m))),
                UiKit.With(BuildPlace, BuildTag()),
            });
        }

        // ---- the title page ----

        static Widget Title(MainMenuModel m, System.Action<int> activate)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            var sk = th.Skins;

            Widget logo = th.HasSprite(UiSprites.LogoTheMovers)
                ? UiKit.Picture(UiSprites.LogoTheMovers, 440f)
                : UiKit.Label("THE MOVERS", tx.Sized(64f, true), false);
            Widget framed = UiKit.Skinned(sk.Branch, new Padding(logo, EdgeInsets.Symmetric(10f, 4f)), sk.Branch.Padding(14f), UiMotion.Hang);
            Widget tagline = UiKit.With(TagTilt, UiKit.Tag(UiKit.Label(Loc.T("menu.tagline"), tx.BodyBold, false), 6f));

            var rows = new List<Widget>(MainMenuModel.TitleRows);
            rows.Add(TitleButton(m, activate, MainMenuModel.TitleRow.Solo, "menu.play1"));
            rows.Add(TitleButton(m, activate, MainMenuModel.TitleRow.Duo, "menu.play2"));
            rows.Add(TitleButton(m, activate, MainMenuModel.TitleRow.Online, "menu.online"));
            rows.Add(TitleButton(m, activate, MainMenuModel.TitleRow.Options, "menu.options"));
            rows.Add(TitleButton(m, activate, MainMenuModel.TitleRow.Controls, "menu.controls"));
            rows.Add(TitleButton(m, activate, MainMenuModel.TitleRow.Quit, "menu.quit"));
            Widget buttons = new Column(rows, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Start);

            return new Column(new[]
            {
                framed,
                UiKit.Pad(tagline, new EdgeInsets(24f, 0f, 0f, 0f)),
                UiKit.Gap(14f),
                UiKit.Pad(buttons, new EdgeInsets(26f, 0f, 0f, 0f)),
                UiKit.Gap(6f),
                UiKit.Pad(Hint(m), new EdgeInsets(26f, 0f, 0f, 0f)),
            }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
        }

        static Widget TitleButton(MainMenuModel m, System.Action<int> activate, MainMenuModel.TitleRow row, string key)
        {
            bool selected = m.ShowSelection && m.Page == MenuPage.Title && m.Selected == (int)row;
            return UiKit.Button(Loc.T(key), () => activate((int)row), selected, !m.Leaving,
                                selected ? SubmitGlyph(m.Device) : default, ButtonWidth);
        }

        // The F1 keyboard swap is a debug key (SliceDebug): only the editor and development
        // builds mention it.
        static string PadNoneKey => UnityEngine.Debug.isDebugBuild ? "menu.padNone" : "menu.padNoneRelease";

        // Under the buttons: what the selected one does; with the mouse, whether player 2 has
        // a gamepad (the one thing worth knowing before "Jouer à deux").
        static Widget Hint(MainMenuModel m)
        {
            var th = UiKit.Theme;
            string text, icon;
            Color tint = th.ink;
            int row = m.ShowSelection ? m.Selected : (int)MainMenuModel.TitleRow.Duo;
            if (m.Message != null) return InfoTag(Loc.T(m.Message), UiSprites.IconWarning, th.warnInk);   // "the host left"
            switch ((MainMenuModel.TitleRow)row)
            {
                case MainMenuModel.TitleRow.Online: text = Loc.T("menu.onlineHint"); icon = UiSprites.IconTruck; break;
                case MainMenuModel.TitleRow.Solo: text = Loc.T("menu.solo"); icon = UiSprites.IconBox; break;
                case MainMenuModel.TitleRow.Duo:
                    text = Loc.T(m.PadConnected ? "menu.padReady" : PadNoneKey);
                    icon = m.PadConnected ? UiSprites.IconCheck : UiSprites.IconWarning;
                    tint = m.PadConnected ? th.goodInk : th.warnInk;
                    break;
                case MainMenuModel.TitleRow.Options: text = Loc.T("menu.optionsHint"); icon = UiSprites.IconKey; break;
                case MainMenuModel.TitleRow.Controls: text = Loc.T("menu.controlsHint"); icon = UiSprites.IconKey; break;
                default: text = Loc.T("menu.quitHint"); icon = UiSprites.IconTruck; break;
            }
            return InfoTag(text, icon, tint);
        }

        // An icon and a line on a paper tag, under the buttons.
        static Widget InfoTag(string text, string icon, Color tint)
        {
            var th = UiKit.Theme;
            Widget line = new Row(new[]
            {
                UiKit.Icon(icon, 26f),
                UiKit.Wrapped(text, tint == th.ink ? th.Text.BodyBold : th.Text.Tinted(th.Text.BodyBold, tint), 330f),
            }, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return new SizedBox(UiKit.Tag(line, 6f, UiMotion.Pop), ButtonWidth);
        }

        // ---- online (NETCODE_SLICE 3.3) ----

        // Héberger, Rejoindre, Retour; the direct host in development builds only, last, as
        // MainMenuModel.OnlineRow orders it.
        static Widget Online(MainMenuModel m, System.Action<int> activate)
        {
            var th = UiKit.Theme;
            var rows = new List<Widget>(6)
            {
                UiKit.TitleSign(Loc.T("net.title").ToUpperInvariant(), th.Text.Title, UiMotion.Drop),
                PageButton(m, activate, (int)MainMenuModel.OnlineRow.Host, "net.host"),
                PageButton(m, activate, (int)MainMenuModel.OnlineRow.Join, "net.join"),
                PageButton(m, activate, (int)MainMenuModel.OnlineRow.Back, "net.back"),
            };
            if (UnityEngine.Debug.isDebugBuild) rows.Add(PageButton(m, activate, (int)MainMenuModel.OnlineRow.HostDirect, "net.hostDirect"));
            string hint;
            switch (m.ShowSelection ? (MainMenuModel.OnlineRow)m.Selected : MainMenuModel.OnlineRow.Host)
            {
                case MainMenuModel.OnlineRow.Join: hint = "net.joinHint"; break;
                case MainMenuModel.OnlineRow.HostDirect: hint = "net.hostDirectHint"; break;
                case MainMenuModel.OnlineRow.Back: hint = "menu.onlineHint"; break;
                default: hint = "net.hostHint"; break;
            }
            rows.Add(InfoTag(Loc.T(hint), UiSprites.IconKey, th.ink));
            return new Column(rows, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
        }

        // The code as key caps (a direct address as one wide key), what is happening, and
        // Copier, Lancer (once player 2 is in), Annuler.
        static Widget Hosting(MainMenuModel m, System.Action<int> activate)
        {
            var th = UiKit.Theme;
            var rows = new List<Widget>(7) { UiKit.TitleSign(Loc.T("net.host").ToUpperInvariant(), th.Text.Title, UiMotion.Drop) };
            if (!string.IsNullOrEmpty(m.JoinCode))
            {
                Widget code = new Column(new[]
                {
                    UiKit.Label(Loc.T("net.code"), th.Text.SmallBold, th.inkSoft, false),
                    CodeCaps(m.JoinCode),
                }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
                rows.Add(new SizedBox(UiKit.Tag(code, 10f, UiMotion.Pop, th.Skins.TagBig), ButtonWidth));
            }
            Widget status = StatusLine(m, true);
            if (status != null) rows.Add(status);
            rows.Add(PageButton(m, activate, (int)MainMenuModel.HostRow.Copy, m.Copied ? "net.copied" : "net.copy",
                                !string.IsNullOrEmpty(m.JoinCode)));
            rows.Add(PageButton(m, activate, (int)MainMenuModel.HostRow.Start, "net.start", m.PeerConnected));
            rows.Add(PageButton(m, activate, (int)MainMenuModel.HostRow.Cancel, "net.cancel"));
            return new Column(rows, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
        }

        // The field (typed, or pasted: "Coller" is the pad's only way), Se connecter, Retour,
        // and what is happening.
        static Widget Joining(MainMenuModel m, System.Action<int> activate, System.Action<string> submitCode)
        {
            var th = UiKit.Theme;
            var s = m.NetStatus;
            bool busy = s == NetStatus.Connecting || s == NetStatus.Lobby || s == NetStatus.Loading;
            bool fieldSelected = m.ShowSelection && m.Selected == (int)MainMenuModel.JoinRow.Field;
            var style = new TextFieldStyle(background: th.cream, foreground: th.ink, border: th.woodDark, placeholder: th.inkSoft,
                                           typography: th.Text.Title, padding: EdgeInsets.Symmetric(12f, 6f));
            Widget field = new TextField(m.Code, placeholder: Loc.T("net.field"), enabled: !busy, style: style,
                                         focusNode: m.CodeFocus, onSubmitted: submitCode);
            var rows = new List<Widget>(7)
            {
                UiKit.TitleSign(Loc.T("net.join").ToUpperInvariant(), th.Text.Title, UiMotion.Drop),
                new SizedBox(UiKit.Tag(field, 8f, null, fieldSelected ? th.Skins.ButtonHover : th.Skins.TagBig), ButtonWidth),
                PageButton(m, activate, (int)MainMenuModel.JoinRow.Paste, "net.paste", !busy),
                PageButton(m, activate, (int)MainMenuModel.JoinRow.Connect, "net.connect", !busy),
                PageButton(m, activate, (int)MainMenuModel.JoinRow.Back, "net.back"),
            };
            Widget status = StatusLine(m, false);
            if (status != null) rows.Add(status);
            return new Column(rows, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
        }

        static Widget PageButton(MainMenuModel m, System.Action<int> activate, int row, string key, bool enabled = true)
        {
            bool selected = m.ShowSelection && m.Selected == row;
            return UiKit.Button(Loc.T(key), () => activate(row), selected, enabled && !m.Leaving,
                                selected ? SubmitGlyph(m.Device) : default, ButtonWidth);
        }

        // "RQ7K2M" as six key caps; "192.168.1.20:7777" as one wide key.
        static Widget CodeCaps(string code)
        {
            if (code.Length != 6) return UiKit.Key(new Glyph(GlyphKind.WideKey, code, UiSprites.KeyWide), 48f);
            var caps = new List<Widget>(6);
            for (int i = 0; i < code.Length; i++) caps.Add(UiKit.Key(new Glyph(GlyphKind.Key, code[i].ToString()), 52f));
            return new Row(caps, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        // The status line of the host and join pages, from NetSession's state; null when idle.
        static Widget StatusLine(MainMenuModel m, bool hosting)
        {
            var th = UiKit.Theme;
            string key = null, icon = UiSprites.IconClock;
            Color tint = th.ink;
            switch (m.NetStatus)
            {
                case NetStatus.Connecting: key = hosting ? "net.creating" : "net.connecting"; break;
                case NetStatus.WaitingForPeer: key = "net.waiting"; break;
                case NetStatus.PeerJoined: key = "net.peerJoined"; icon = UiSprites.IconCheck; tint = th.goodInk; break;
                case NetStatus.Lobby: key = "net.connected"; icon = UiSprites.IconCheck; tint = th.goodInk; break;
                case NetStatus.Loading: key = "net.loading"; break;
                case NetStatus.Failed:
                    key = NetSession.LocKey(m.NetError) ?? "net.failed";
                    icon = UiSprites.IconWarning;
                    tint = th.badInk;
                    break;
            }
            return key != null ? InfoTag(Loc.T(key), icon, tint) : null;
        }

        // ---- options ----

        static Widget Options(MainMenuModel m, System.Action<int> activate, System.Action<int, int> change)
        {
            var th = UiKit.Theme;
            var rows = new List<Widget>(MainMenuModel.OptionRows);
            rows.Add(VolumeRow(m, change, MainMenuModel.OptionRow.Master, "opt.master", AudioChannel.Master, 0));
            rows.Add(VolumeRow(m, change, MainMenuModel.OptionRow.Music, "opt.music", AudioChannel.Music, 1));
            rows.Add(VolumeRow(m, change, MainMenuModel.OptionRow.Sfx, "opt.sfx", AudioChannel.Sfx, 2));
            rows.Add(VolumeRow(m, change, MainMenuModel.OptionRow.Voice, "opt.voice", AudioChannel.Voice, 3));
            rows.Add(VolumeRow(m, change, MainMenuModel.OptionRow.Ambience, "opt.ambience", AudioChannel.Ambience, 4));
            rows.Add(VolumeRow(m, change, MainMenuModel.OptionRow.Ui, "opt.ui", AudioChannel.Ui, 5));
            rows.Add(ValueRow(m, change, MainMenuModel.OptionRow.Sensitivity, "opt.sensitivity", GameSettings.LookSensitivity.ToString("0.0") + " x"));
            rows.Add(ValueRow(m, change, MainMenuModel.OptionRow.InvertY, "opt.invertY", Loc.T(GameSettings.InvertY ? "opt.on" : "opt.off")));
            rows.Add(ValueRow(m, change, MainMenuModel.OptionRow.Language, "opt.language", Loc.T(GameSettings.Language == Language.French ? "lang.fr" : "lang.en")));
            rows.Add(ValueRow(m, change, MainMenuModel.OptionRow.Layout, "opt.layout",
                              Loc.T(GameSettings.Layout == SplitLayout.SideBySide ? "layout.sideBySide" : "layout.stacked")));
            rows.Add(ValueRow(m, change, MainMenuModel.OptionRow.Hints, "opt.hints", Loc.T(GameSettings.HintsShown ? "opt.on" : "opt.off")));

            Widget board = new SizedBox(UiKit.Panel(new Column(rows, 2f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch), th.Skins.Wood, 12f, UiMotion.Pop), 640f);
            bool backSelected = m.ShowSelection && m.Selected == (int)MainMenuModel.OptionRow.Back;
            Widget back = UiKit.Button(Loc.T("pause.back"), () => activate((int)MainMenuModel.OptionRow.Back), backSelected, true, BackGlyph(m.Device), 260f);
            return new Column(new[]
            {
                UiKit.TitleSign(Loc.T("opt.title").ToUpperInvariant(), th.Text.Title, UiMotion.Drop),
                board,
                back,
            }, 12f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        static Widget VolumeRow(MainMenuModel m, System.Action<int, int> change, MainMenuModel.OptionRow row, string key, AudioChannel channel, int fill)
        {
            var th = UiKit.Theme;
            float v = GameAudio.GetVolume(channel);
            m.VolumeFills[fill].Set(v);
            Widget value = new Row(new[]
            {
                UiKit.TapeBar(m.VolumeFills[fill], th.accent, 160f),
                new SizedBox(UiKit.Label(Loc.Int(Mathf.RoundToInt(v * 100f)) + "%", th.Text.SmallBold, false), 52f),
            }, 8f, MainAxisAlignment.End, CrossAxisAlignment.Center);
            return OptionLine(m, change, row, key, value);
        }

        static Widget ValueRow(MainMenuModel m, System.Action<int, int> change, MainMenuModel.OptionRow row, string key, string text) =>
            OptionLine(m, change, row, key, new SizedBox(new Center(UiKit.Label(text, UiKit.Theme.Text.BodyBold, false)), 220f));

        // "Label        < value >": lit when selected, the arrows clickable.
        static Widget OptionLine(MainMenuModel m, System.Action<int, int> change, MainMenuModel.OptionRow row, string key, Widget value)
        {
            var th = UiKit.Theme;
            int index = (int)row;
            bool selected = m.ShowSelection && m.Selected == index;
            Widget line = new Row(new[]
            {
                new Expanded(UiKit.Label(Loc.T(key), selected ? th.Text.BodyBold : th.Text.Body, false)),
                Arrow("<", () => change(index, -1)),
                value,
                Arrow(">", () => change(index, 1)),
            }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            // ClearInk: no surface, ink text without a shadow, readable on the pale board
            // (Clear is cream with a shadow, for text over the scene). The pause menu's options
            // use the same skin, so both option boards read alike.
            return UiKit.Skinned(selected ? th.Skins.ButtonHover : th.Skins.ClearInk, line, EdgeInsets.Symmetric(12f, 5f));
        }

        static Widget Arrow(string glyph, System.Action onPressed) =>
            new WoodButton(glyph, onPressed, false, true, default, 30f, compact: true);

        // ---- controls ----

        static readonly KeyboardMouseSource KeyboardBoard = new KeyboardMouseSource();
        static GamepadSource padBoard;

        static Widget Controls(MainMenuModel m, System.Action<int> activate, System.Action<int, int> change)
        {
            var th = UiKit.Theme;
            bool pad = m.ControlsTab == MenuDevice.Gamepad;
            ICrewInputSource src = pad ? (padBoard ?? (padBoard = new GamepadSource(1))) : (ICrewInputSource)KeyboardBoard;
            bool padHints = m.Device == MenuDevice.Gamepad;
            Widget tabs = new Row(new[]
            {
                UiKit.Button(Loc.T("menu.keyboard"), () => change(0, -1), !pad, true,
                             padHints ? InputGlyphs.ForPad(CrewButton.Rotate) : default, 260f),
                UiKit.Button(Loc.T("menu.gamepad"), () => change(0, 1), pad, true,
                             padHints ? InputGlyphs.ForPad(CrewButton.Grab) : default, 260f),
            }, 12f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
            Widget sheet = UiKit.Panel(HudControlsSheet.Board(src, false), th.Skins.Wood, 12f, UiMotion.Pop);
            Widget back = UiKit.Button(Loc.T("pause.back"), () => activate(0), m.ShowSelection, true, BackGlyph(m.Device), 260f);
            return new Column(new[] { tabs, sheet, back }, 12f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        // ---- the corners ----

        // "[stick] Choisir  [A] Valider  [B] Retour", in the device last used.
        static Widget Footer(MainMenuModel m)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            var parts = new List<Widget>(9)
            {
                UiKit.Key(MoveGlyph(m.Device), FooterKey), UiKit.Label(Loc.T("pause.select"), tx.BodyBold, false),
                UiKit.Gap(10f),
                UiKit.Key(SubmitGlyph(m.Device), FooterKey), UiKit.Label(Loc.T("pause.ok"), tx.BodyBold, false),
            };
            if (m.Page != MenuPage.Title)
            {
                parts.Add(UiKit.Gap(10f));
                parts.Add(UiKit.Key(BackGlyph(m.Device), FooterKey));
                parts.Add(UiKit.Label(Loc.T("pause.back"), tx.BodyBold, false));
            }
            Widget row = new Row(parts, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return UiKit.Skinned(th.Skins.Chip, row, EdgeInsets.Symmetric(14f, 7f));
        }

        static Widget BuildTag()
        {
            var th = UiKit.Theme;
            return new Opacity(UiKit.Skinned(th.Skins.Chip, UiKit.Label(Loc.T("menu.build"), th.Text.CaptionBold, false), EdgeInsets.Symmetric(10f, 4f)), 0.8f);
        }

        // ---- glyphs of the menu keys ----

        public static Glyph SubmitGlyph(MenuDevice d) =>
            d == MenuDevice.Gamepad ? InputGlyphs.ForPad(CrewButton.Jump) : new Glyph(GlyphKind.WideKey, Loc.T("key.enter"), UiSprites.KeyWide);

        public static Glyph BackGlyph(MenuDevice d) =>
            d == MenuDevice.Gamepad ? InputGlyphs.ForPad(CrewButton.Crouch) : new Glyph(GlyphKind.WideKey, Loc.T("key.esc"), UiSprites.KeyWide);

        static Glyph MoveGlyph(MenuDevice d) =>
            d == MenuDevice.Gamepad ? new Glyph(GlyphKind.Sprite, "LS", UiSprites.PadLS) : new Glyph(GlyphKind.WideKey, Loc.T("key.arrows"), UiSprites.KeyWide);
    }
}
