using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // One player's pause menu, over their own view only: the game behind is dimmed, a sign
    // with a leafy branch says whose pause it is, and wooden buttons (or the options board, or
    // the controls sheet) sit under it. The selected row is lit for the pad and the keys; the
    // mouse clicks the same buttons. Every page is drawn 1.3 times its size (UiZoom), less when
    // the view is too small for that (a stacked split view): it is read from the couch.
    public static class PauseView
    {
        static readonly UiPlace Place = UiPlace.Center();
        const float FooterKey = 32f;

        public static Widget Build(PlayerHudModel p) =>
            new ReactiveBuilder<int>(p.pause.Version, _ => p.pause.IsOpen ? Overlay(p) : UiKit.Empty);

        static Widget Overlay(PlayerHudModel p)
        {
            var th = UiKit.Theme;
            Widget page;
            switch (p.pause.Page)
            {
                case HudPausePage.Options: page = Options(p); break;
                case HudPausePage.Controls: page = Controls(p); break;
                default: page = Main(p); break;
            }
            return new Stack(new[] { UiPlace.Fill.Widget, th.Skins.Scrim.Widget, UiKit.With(Place, UiKit.With(UiZoom.Centered, page)) });
        }

        static Widget Title(PlayerHudModel p, string words)
        {
            var th = UiKit.Theme;
            Widget badge = UiKit.Chip(Loc.F("pause.player", p.Index + 1), p.member != null ? p.member.color : th.accent);
            Widget sign = UiKit.TitleSign(words.ToUpperInvariant(), th.Text.Title, UiMotion.Drop);
            return new Column(new[] { sign, badge }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        // ---- main page ----

        static Widget Main(PlayerHudModel p)
        {
            var menu = p.pause;
            var src = p.Source;
            var rows = new List<Widget>(HudPauseMenu.MainRows + 2) { Title(p, Loc.T("pause.title")) };
            rows.Add(Button(menu, HudPauseMenu.MainRow.Resume, Loc.T("pause.resume"), InputGlyphs.For(src, CrewButton.Pause)));
            rows.Add(Button(menu, HudPauseMenu.MainRow.Options, Loc.T("pause.options"), default));
            rows.Add(Button(menu, HudPauseMenu.MainRow.Controls, Loc.T("pause.controls"), default));
            rows.Add(Button(menu, HudPauseMenu.MainRow.Menu, Loc.T("pause.menu"), default));
            rows.Add(Footer(src));
            return new Column(rows, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        static Widget Button(HudPauseMenu menu, HudPauseMenu.MainRow row, string label, Glyph glyph) =>
            UiKit.Button(label, () => menu.Activate(row), menu.Selected == (int)row, true, glyph, 300f);

        // "[stick] Choose  [A] OK  [B] Back", in this player's device.
        static Widget Footer(ICrewInputSource src)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            Widget row = new Row(new[]
            {
                UiKit.Key(InputGlyphs.Move(src), FooterKey), UiKit.Label(Loc.T("pause.select"), tx.BodyBold, false),
                UiKit.Gap(8f),
                UiKit.Key(InputGlyphs.For(src, CrewButton.Jump), FooterKey), UiKit.Label(Loc.T("pause.ok"), tx.BodyBold, false),
                UiKit.Gap(8f),
                UiKit.Key(InputGlyphs.For(src, CrewButton.Pause), FooterKey), UiKit.Label(Loc.T("pause.back"), tx.BodyBold, false),
            }, 6f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
            return UiKit.Skinned(th.Skins.Chip, row, EdgeInsets.Symmetric(12f, 6f));
        }

        // ---- options ----

        static Widget Options(PlayerHudModel p)
        {
            var menu = p.pause;
            var rows = new List<Widget>(HudPauseMenu.OptionRows);
            rows.Add(VolumeRow(menu, HudPauseMenu.OptionRow.Master, "opt.master", AudioChannel.Master));
            rows.Add(VolumeRow(menu, HudPauseMenu.OptionRow.Music, "opt.music", AudioChannel.Music));
            rows.Add(VolumeRow(menu, HudPauseMenu.OptionRow.Sfx, "opt.sfx", AudioChannel.Sfx));
            rows.Add(VolumeRow(menu, HudPauseMenu.OptionRow.Voice, "opt.voice", AudioChannel.Voice));
            rows.Add(VolumeRow(menu, HudPauseMenu.OptionRow.Ambience, "opt.ambience", AudioChannel.Ambience));
            rows.Add(VolumeRow(menu, HudPauseMenu.OptionRow.Ui, "opt.ui", AudioChannel.Ui));
            rows.Add(ValueRow(menu, HudPauseMenu.OptionRow.Sensitivity, "opt.sensitivity",
                              GameSettings.LookSensitivity.ToString("0.0") + " x"));
            rows.Add(ValueRow(menu, HudPauseMenu.OptionRow.InvertY, "opt.invertY", Loc.T(GameSettings.InvertY ? "opt.on" : "opt.off")));
            rows.Add(ValueRow(menu, HudPauseMenu.OptionRow.Language, "opt.language", Loc.T(GameSettings.Language == Language.French ? "lang.fr" : "lang.en")));
            rows.Add(ValueRow(menu, HudPauseMenu.OptionRow.Layout, "opt.layout",
                              Loc.T(GameSettings.Layout == SplitLayout.SideBySide ? "layout.sideBySide" : "layout.stacked")));
            rows.Add(ValueRow(menu, HudPauseMenu.OptionRow.Hints, "opt.hints", Loc.T(GameSettings.HintsShown ? "opt.on" : "opt.off")));

            var th = UiKit.Theme;
            Widget board = new SizedBox(UiKit.Panel(new Column(rows, 2f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch), th.Skins.Wood, 10f), 600f);
            Widget back = UiKit.Button(Loc.T("pause.back"), () => menu.ShowPage(HudPausePage.Main, (int)HudPauseMenu.MainRow.Options),
                                       menu.Selected == (int)HudPauseMenu.OptionRow.Back, true, InputGlyphs.For(p.Source, CrewButton.Pause), 240f);
            return new Column(new[] { Title(p, Loc.T("opt.title")), board, back, Footer(p.Source) }, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        static Widget VolumeRow(HudPauseMenu menu, HudPauseMenu.OptionRow row, string key, AudioChannel channel)
        {
            float v = GameAudio.GetVolume(channel);
            var th = UiKit.Theme;
            Widget value = new Row(new[]
            {
                StaticBar(v, 150f, 14f),
                new SizedBox(UiKit.Label(Loc.Int(Mathf.RoundToInt(v * 100f)) + "%", th.Text.SmallBold, false), 52f),
            }, 8f, MainAxisAlignment.End, CrossAxisAlignment.Center);
            return OptionLine(menu, row, key, value);
        }

        static Widget ValueRow(HudPauseMenu menu, HudPauseMenu.OptionRow row, string key, string text) =>
            OptionLine(menu, row, key, new SizedBox(new Center(UiKit.Label(text, UiKit.Theme.Text.BodyBold, false)), 210f));

        // "Label        < value >", lit when selected, arrows clickable.
        static Widget OptionLine(HudPauseMenu menu, HudPauseMenu.OptionRow row, string key, Widget value)
        {
            var th = UiKit.Theme;
            int index = (int)row;
            bool selected = menu.Selected == index;
            Widget line = new Row(new[]
            {
                new Expanded(UiKit.Label(Loc.T(key), selected ? th.Text.BodyBold : th.Text.Body, false)),
                Arrow("<", () => { menu.Select(index); menu.Change(-1); }),
                value,
                Arrow(">", () => { menu.Select(index); menu.Change(1); }),
            }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            // Unselected rows sit on the pale board, so their words are ink (cream vanished there).
            UiSkin skin = selected ? th.Skins.ButtonHover : th.Skins.ClearInk;
            return UiKit.Skinned(skin, line, EdgeInsets.Symmetric(12f, 4f));
        }

        static Widget Arrow(string glyph, System.Action onPressed) =>
            new WoodButton(glyph, onPressed, false, true, default, 30f, compact: true);

        // A tape bar whose fill is drawn from a value (the options rebuild when they change).
        static Widget StaticBar(float v, float width, float height)
        {
            var th = UiKit.Theme;
            float inner = width - 6f;
            Widget fill = new SizedBox(UiKit.Skinned(th.Skins.TapeFill(th.accent), UiKit.Empty), Mathf.Max(0f, inner * Mathf.Clamp01(v)), height - 6f);
            Widget track = new Row(new[] { fill }, 0f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return new SizedBox(UiKit.Skinned(th.Skins.TapeBg, track, EdgeInsets.All(3f)), width, height);
        }

        // ---- controls ----

        static Widget Controls(PlayerHudModel p)
        {
            var th = UiKit.Theme;
            var menu = p.pause;
            Widget sheet = UiKit.Panel(HudControlsSheet.Board(p.Source, false), th.Skins.Wood, 12f);
            Widget back = UiKit.Button(Loc.T("pause.back"), () => menu.ShowPage(HudPausePage.Main, (int)HudPauseMenu.MainRow.Controls),
                                       true, true, InputGlyphs.For(p.Source, CrewButton.Pause), 240f);
            return new Column(new[] { sheet, back }, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }
    }
}
