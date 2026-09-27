using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // Every control of the game for one device, on a wooden board in two columns: on foot,
    // the hands, at the wheel. Shown by the controls key (Tab, or Back on a pad) and by the
    // pause menu's "Controls". The main menu can show it too (MENU): it only needs a source.
    // Whoever shows it zooms it (UiZoom): the sheet itself is laid at its design size.
    public static class HudControlsSheet
    {
        const float Key = 32f;
        const float PocketKey = 28f;
        static readonly UiPlace Place = UiPlace.Center();

        // The toggleable overlay in a player's view.
        public static Widget Overlay(PlayerHudModel p) =>
            UiKit.With(Place, UiKit.With(UiZoom.Centered, new ReactiveBuilder<bool>(p.ControlsOpen, open => open && !p.pause.IsOpen
                ? UiKit.Panel(Board(p.Source, true), UiKit.Theme.Skins.Wood, 12f, UiMotion.Pop)
                : UiKit.Empty)));

        // The sheet itself, for any device. withToggle adds the line that closes it.
        public static Widget Board(ICrewInputSource src, bool withToggle)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            Widget left = Section(Loc.T("ctrl.onFoot"), new[]
            {
                Line(InputGlyphs.Move(src), Loc.T("verb.move")),
                Line(InputGlyphs.Look(src), Loc.T("verb.look")),
                Line(InputGlyphs.For(src, CrewButton.Jump), Loc.T("verb.jump")),
                Line(InputGlyphs.For(src, CrewButton.Sprint), Loc.T("verb.sprint"), Gesture.Hold),
                Line(InputGlyphs.For(src, CrewButton.Crouch), Loc.T("verb.crouch"), Gesture.Hold),
                Line(InputGlyphs.For(src, CrewButton.Interact), Loc.T("ctrl.interact")),
                Line(InputGlyphs.For(src, CrewButton.Pause), Loc.T("verb.pause")),
            });
            Widget hands = Section(Loc.T("ctrl.hands"), new[]
            {
                Line(InputGlyphs.For(src, CrewButton.Grab), Loc.T("ctrl.grab")),
                Line(InputGlyphs.For(src, CrewButton.Throw), Loc.T("ctrl.throw")),
                Line(InputGlyphs.For(src, CrewButton.Rotate), Loc.T("ctrl.rotate")),
                Line(InputGlyphs.For(src, CrewButton.Reach), Loc.T("ctrl.reach"), Gesture.Press, InputGlyphs.ReachStick(src)),
                Line(InputGlyphs.For(src, CrewButton.Alt), Loc.T("ctrl.alt")),
                Pockets(src),
            });
            Widget truck = Section(Loc.T("ctrl.truck"), new[]
            {
                Line(InputGlyphs.Move(src), Loc.T("verb.steer")),
                Line(InputGlyphs.For(src, CrewButton.Jump), Loc.T("verb.handbrake"), Gesture.Hold),
                Line(InputGlyphs.For(src, CrewButton.Interact), Loc.T("ctrl.getOut")),
            });

            var columns = new Row(new[]
            {
                new SizedBox(new Column(new[] { left, truck }, 14f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch), 330f),
                new SizedBox(hands, 360f),
            }, 24f, MainAxisAlignment.Start, CrossAxisAlignment.Start);

            var parts = new List<Widget>(3) { UiKit.TitleSign(Loc.T("ctrl.title").ToUpperInvariant(), tx.Title), columns };
            if (withToggle)
                parts.Add(new Opacity(Line(InputGlyphs.ControlsToggle(src), Loc.T("ctrl.toggle")), 0.8f));
            return new Column(parts, 14f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        static Widget Section(string title, Widget[] lines)
        {
            var th = UiKit.Theme;
            var all = new Widget[lines.Length + 1];
            all[0] = UiKit.Label(title.ToUpperInvariant(), th.Text.SmallBold, th.warnInk, false);
            for (int i = 0; i < lines.Length; i++) all[i + 1] = lines[i];
            return new Column(all, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch);
        }

        static Widget Line(Glyph g, string words, Gesture gesture = Gesture.Press, Glyph g2 = default)
        {
            var th = UiKit.Theme;
            var parts = new List<Widget>(5) { new SizedBox(new Row(new[] { UiKit.Key(g, Key) }, 0f, MainAxisAlignment.End, CrossAxisAlignment.Center), 96f) };
            if (!g2.IsNone)
            {
                parts.Add(UiKit.Label("+", th.Text.SmallBold, false));
                parts.Add(UiKit.Key(g2, Key));
            }
            parts.Add(new Flexible(UiKit.Label(words, th.Text.Body)));
            string gw = HintSet.GestureWord(gesture);
            if (gw != null) parts.Add(UiKit.Chip(gw));
            return new Row(parts, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        static Widget Pockets(ICrewInputSource src)
        {
            var th = UiKit.Theme;
            Widget keys = new Row(new Widget[]
            {
                UiKit.Key(InputGlyphs.PocketSlot(src, 0), PocketKey), UiKit.Key(InputGlyphs.PocketSlot(src, 1), PocketKey),
                UiKit.Key(InputGlyphs.PocketSlot(src, 2), PocketKey), UiKit.Key(InputGlyphs.PocketSlot(src, 3), PocketKey),
            }, 3f, MainAxisAlignment.End, CrossAxisAlignment.Center);
            return new Row(new Widget[] { new SizedBox(keys, 132f), new Flexible(UiKit.Label(Loc.T("ctrl.pockets"), th.Text.Body)) },
                           10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }
    }
}
