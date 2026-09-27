using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // The four pockets, bottom right: little cardboard boxes with what is in each (its icon
    // and name) and the key that works it pinned on the corner. The pocket what you hold came
    // out of glows like fresh tape; a pocket with a ticking grenade pulses. Above them, the
    // pockets' own words ("Hands full") and how tipsy the player is.
    public static class PocketsView
    {
        const float SlotW = 78f, SlotH = 84f;
        const float CaptionMargin = 4f;
        static readonly UiPlace Place = UiPlace.BottomRight(18f);

        public static Widget Build(PlayerHudModel p) => UiKit.With(Place, new Column(new Widget[]
        {
            new ReactiveBuilder<string>(p.PocketHint, Hint),
            new ReactiveBuilder<bool>(p.Drunk, on => on ? Drunk(p) : UiKit.Empty),
            new ReactiveBuilder<PocketView>(p.Pockets, v => Bar(v, p)),
        }, 6f, MainAxisAlignment.End, CrossAxisAlignment.End));

        static Widget Hint(string words)
        {
            if (string.IsNullOrEmpty(words)) return UiKit.Empty;
            var th = UiKit.Theme;
            return UiKit.Tag(new Row(new[] { UiKit.Icon(UiSprites.IconWarning, 22f, th.warnInk), UiKit.Label(words, th.Text.BodyBold, false) },
                                     6f, MainAxisAlignment.Start, CrossAxisAlignment.Center), 4f, UiMotion.Shake);
        }

        static Widget Drunk(PlayerHudModel p)
        {
            var th = UiKit.Theme;
            Widget row = new Row(new[]
            {
                UiKit.Icon(UiSprites.IconBeer, 24f, th.accent),
                UiKit.Label(Loc.T("drunk.label"), th.Text.SmallBold, false),
                UiKit.TapeBar(p.DrunkFill, th.warn, 130f),
            }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return UiKit.Skinned(th.Skins.Chip, row, EdgeInsets.Symmetric(10f, 4f), UiMotion.Pop);
        }

        static Widget Bar(in PocketView v, PlayerHudModel p)
        {
            var slots = new List<Widget>(PlayerPockets.SlotCount);
            for (int i = 0; i < PlayerPockets.SlotCount; i++) slots.Add(Slot(v, i, p));
            return new Row(slots, 8f, MainAxisAlignment.End, CrossAxisAlignment.End);
        }

        static Widget Slot(in PocketView v, int i, PlayerHudModel p)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            MovableObject item = v[i];
            bool armed = (v.armed & (1 << i)) != 0;
            bool active = v.active == i;

            UiSkin skin = active ? th.Skins.SlotActive : th.Skins.Slot;
            // Top and bottom from the sprite's content rect (the tape band, the flap), the sides
            // nearly to the edge: the rect's side margins are for blocks of text, and a one-word
            // caption ("Cigarette") needs the whole face of a 78 px box.
            EdgeInsets rect = skin.Padding(3f);
            var pad = new EdgeInsets(CaptionMargin, rect.Top, CaptionMargin, rect.Bottom);
            float inner = SlotW - 2f * CaptionMargin;

            Widget content;
            if (item != null)
            {
                string name = armed ? Loc.T("pocket.ticking") : Loc.Item(item.displayName);
                content = new Column(new[]
                {
                    UiKit.Icon(UiKit.IconOf(item), 34f, armed ? th.bad : (Color?)null),
                    Caption(name, armed ? tx.CaptionBold : tx.Caption, armed ? th.badInk : th.ink, inner),
                }, 2f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
            }
            else if (active && p.member != null && p.member.Held != null)
            {
                // Its contents are in the hands: the box shows what is out, greyed.
                content = new Column(new[]
                {
                    UiKit.Icon(UiKit.IconOf(p.member.Held), 30f, new Color(1f, 1f, 1f, 0.55f)),
                    Caption(Loc.Item(p.member.Held.displayName), tx.Caption, th.inkSoft, inner),
                }, 2f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
            }
            else content = UiKit.Label(Loc.T("pocket.none"), tx.Caption, th.inkSoft, false);

            Widget face = armed
                ? new Stack(new[] { skin.Widget, UiCenter.Instance.Widget, UiMotion.PulseFast.Widget, new Padding(content, pad) })
                : new Stack(new[] { skin.Widget, UiCenter.Instance.Widget, new Padding(content, pad) });
            Widget box = new SizedBox(face, SlotW, SlotH);
            Widget key = p.Device.Value == 2 ? UiKit.Empty : UiKit.Key(InputGlyphs.PocketSlot(p.Source, i), 26f);
            return new Stack(new[] { box, new Positioned(key, left: -6f, top: -10f) });
        }

        // One line cut with an ellipsis, inside the box's content rect: "Vase en porcelaine" is
        // about twice as wide as a pocket (UIART round 3). The icon says what it is, the key
        // hints' card gives the full name once it is in the hands.
        static Widget Caption(string text, TextStyle style, Color color, float width) =>
            ConstrainedBox.AtMost(new Text(text ?? "", UiKit.Theme.Text.Tinted(style, color), false, TextOverflow.Ellipsis, 1), width: width);
    }
}
