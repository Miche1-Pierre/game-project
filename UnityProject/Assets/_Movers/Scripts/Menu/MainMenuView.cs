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
    // the left third of the screen: the living landscape keeps the rest.
    //
    // Rebuilt only when the model's Version changes.
    public static class MainMenuView
    {
        const float Left = 84f;
        const float Top = 48f;
        const float ButtonWidth = 400f;

        static readonly UiPlace ColumnPlace = new UiPlace(0f, 0f, 0f, 0f, Left, Top);
        static readonly UiPlace FooterPlace = UiPlace.BottomLeft(30f);
        static readonly UiPlace BuildPlace = UiPlace.BottomRight(26f);
        static readonly UiTransform TagTilt = new UiTransform(-2.2f);
        static readonly UiName ColumnName = new UiName("menu-column");

        public static Widget Build(MainMenuModel m, System.Action<int> activate, System.Action<int, int> change) =>
            new ReactiveBuilder<int>(m.Version, _ => Screen(m, activate, change));

        static Widget Screen(MainMenuModel m, System.Action<int> activate, System.Action<int, int> change)
        {
            Widget page;
            switch (m.Page)
            {
                case MenuPage.Options: page = Options(m, activate, change); break;
                case MenuPage.Controls: page = Controls(m, activate, change); break;
                default: page = Title(m, activate); break;
            }
            return new Stack(new[]
            {
                UiPlace.Fill.Widget,
                UiKit.With(ColumnPlace, ColumnName, page),
                UiKit.With(FooterPlace, Footer(m)),
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
            switch ((MainMenuModel.TitleRow)row)
            {
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
            Widget line = new Row(new[]
            {
                UiKit.Icon(icon, 26f),
                UiKit.Wrapped(text, tint == th.ink ? th.Text.BodyBold : th.Text.Tinted(th.Text.BodyBold, tint), 330f),
            }, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return new SizedBox(UiKit.Tag(line, 6f, UiMotion.Pop), ButtonWidth);
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
                UiKit.Key(MoveGlyph(m.Device), 28f), UiKit.Label(Loc.T("pause.select"), tx.SmallBold, false),
                UiKit.Gap(8f),
                UiKit.Key(SubmitGlyph(m.Device), 28f), UiKit.Label(Loc.T("pause.ok"), tx.SmallBold, false),
            };
            if (m.Page != MenuPage.Title)
            {
                parts.Add(UiKit.Gap(8f));
                parts.Add(UiKit.Key(BackGlyph(m.Device), 28f));
                parts.Add(UiKit.Label(Loc.T("pause.back"), tx.SmallBold, false));
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
