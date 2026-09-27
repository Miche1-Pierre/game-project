using System;
using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using UnityEngine.UIElements;
using Image = LumaFlow.Image;
using Column = LumaFlow.Column;
using TextOverflow = LumaFlow.TextOverflow;
using Text = LumaFlow.Text;
using LumaTextStyle = LumaFlow.TextStyle;

namespace Movers
{
    // The game's component library on LumaFlow: surfaces, text, icons, key glyphs, hint rows,
    // the tape-measure bar, toasts, the paper speech bubble, cards. Every screen (the HUD, the
    // pause menu, the main menu) builds from these, so the whole game looks like one thing.
    //
    // Everything here returns a fresh widget description (that is what LumaFlow widgets are)
    // but reuses the shared skins, styles and probes, so a rebuild keeps the native elements.
    // Build only when what is shown changes.
    public static class UiKit
    {
        public static UiTheme Theme => UiTheme.Current;
        static UiSkins Skins => UiTheme.Current.Skins;
        static UiTextStyles Tx => UiTheme.Current.Text;

        // ---- structure ----

        // An empty leaf, for a bar's fill or a spacer of a fixed size.
        static VisualElement MakeEmpty() => new VisualElement { pickingMode = PickingMode.Ignore };
        public static Widget Empty => new Native(MakeEmpty);

        public static Widget Gap(float size) => new SizedBox(Empty, size, size);

        public static Widget Pad(Widget child, EdgeInsets padding) => new Padding(child, padding);

        // A child on a surface. Probes first (they are zero-size and absolute), content last.
        public static Widget Skinned(UiSkin skin, Widget child, EdgeInsets? padding = null, UiProbe extra = null)
        {
            Widget content = padding.HasValue ? new Padding(child, padding.Value) : child;
            return extra == null
                ? new Stack(new[] { skin.Widget, content })
                : new Stack(new[] { skin.Widget, extra.Widget, content });
        }

        // A child with a probe and no surface (a placement, a motion).
        public static Widget With(UiProbe probe, Widget child) => new Stack(new[] { probe.Widget, child });

        public static Widget With(UiProbe a, UiProbe b, Widget child) => new Stack(new[] { a.Widget, b.Widget, child });

        public static Widget Row(float gap, params Widget[] children) =>
            new Row(children, gap, MainAxisAlignment.Start, CrossAxisAlignment.Center);

        public static Widget Column(float gap, params Widget[] children) =>
            new Column(children, gap, MainAxisAlignment.Start, CrossAxisAlignment.Stretch);

        public static Widget Centered(float gap, params Widget[] children) =>
            new Column(children, gap, MainAxisAlignment.Start, CrossAxisAlignment.Center);

        // ---- surfaces ----
        // The padding is the skin's own content rect (UIART's sprites.json) at its scale, at
        // least `padding`: text never runs over the battens or the tape.

        public static Widget Panel(Widget child, UiSkin skin = null, float padding = 10f, UiProbe motion = null)
        {
            var sk = skin ?? Skins.Wood;
            return Skinned(sk, child, sk.Padding(padding), motion);
        }

        public static Widget Card(Widget child, float padding = 8f, UiSkin skin = null, UiProbe motion = null)
        {
            var sk = skin ?? Skins.Cardboard;
            return Skinned(sk, child, sk.Padding(padding), motion);
        }

        // A paper luggage label: the key hints, the toasts, the grandmother's words.
        public static Widget Tag(Widget child, float padding = 6f, UiProbe motion = null, UiSkin skin = null)
        {
            var sk = skin ?? Skins.Tag;
            return Skinned(sk, child, sk.Padding(padding), motion);
        }

        // A small walnut sign: the banner, "Deliver here", the intro's call to action.
        public static Widget Sign(string title, LumaTextStyle style = null, UiProbe motion = null) =>
            Skinned(Skins.WoodDark, Label(title, style ?? Tx.LabelBold, wrap: false), Skins.WoodDark.Padding(8f), motion);

        // A title: a walnut plate framed by lashed branches (the frame is transparent inside).
        public static Widget TitleSign(string title, LumaTextStyle style = null, UiProbe motion = null)
        {
            Widget plate = Skinned(Skins.Plate, Label(title, style ?? Tx.Title, wrap: false), EdgeInsets.Symmetric(22f, 6f));
            return Skinned(Skins.Branch, plate, Skins.Branch.Padding(4f), motion);
        }

        // ---- text ----

        public static Widget Label(string text, LumaTextStyle style = null, bool wrap = true, int? maxLines = null) =>
            new Text(text ?? "", style ?? Tx.Body, wrap, maxLines.HasValue ? TextOverflow.Ellipsis : TextOverflow.Clip, maxLines);

        public static Widget Label(string text, LumaTextStyle style, Color color, bool wrap = true) =>
            new Text(text ?? "", Tx.Tinted(style, color), wrap);

        // Text that wraps at `maxWidth`. In a row, a wrapping label must carry its own width
        // limit: UI Toolkit measures a shrunk flex item's text at its unshrunk width (one long
        // line, then clipped), so the limit goes on the text's own box instead.
        public static Widget Wrapped(string text, LumaTextStyle style, float maxWidth) =>
            ConstrainedBox.AtMost(Label(text, style), width: maxWidth);

        // Text that follows a State<string> without a rebuild: the clock, the money.
        public static Widget Bound(State<string> text, LumaTextStyle style = null) =>
            new Text(text, style ?? Tx.Body, false);

        public static Widget Bound(State<string> text, LumaTextStyle style, Color color) =>
            new Text(text, Tx.Tinted(style, color), false);

        // ---- icons ----

        // A UIART icon by its fixed name; a built-in vector icon or a letter while it is missing.
        public static Widget Icon(string sprite, float size, Color? tint = null)
        {
            Sprite s = Theme.Sprite(sprite);
            if (s != null) return new Image(s, size, size, ImageFit.Contain, tint);
            if (FallbackIcons.TryGetValue(sprite ?? "", out IconData icon))
                return new LumaFlow.Icon(icon, size * 0.8f, tint ?? Theme.cream);
            string letter = FallbackLetter(sprite);
            return new SizedBox(new Center(Label(letter, Tx.Sized(size * 0.62f, true), tint ?? Theme.accent, false)), size, size);
        }

        static readonly Dictionary<string, IconData> FallbackIcons = new Dictionary<string, IconData>
        {
            { UiSprites.IconClock, LumaIcons.Clock }, { UiSprites.IconBox, LumaIcons.Package },
            { UiSprites.IconCrate, LumaIcons.Package }, { UiSprites.IconKey, LumaIcons.Key },
            { UiSprites.IconWarning, LumaIcons.Warning }, { UiSprites.IconCheck, LumaIcons.Check },
            { UiSprites.IconCross, LumaIcons.Close }, { UiSprites.IconTruck, LumaIcons.Home },
            { UiSprites.IconPocket, LumaIcons.Archive },
        };

        static string FallbackLetter(string sprite)
        {
            switch (sprite)
            {
                case UiSprites.IconMoney: return "$";
                case UiSprites.IconCigarette: return "~";
                case UiSprites.IconBeer: return "B";
                case UiSprites.IconGrenade: return "G";
                case UiSprites.IconGrandmaCalm: return ":)";
                case UiSprites.IconGrandmaAnnoyed: return ":|";
                case UiSprites.IconGrandmaAngry: return ":(";
                case UiSprites.IconGrandmaFurious: return ">:(";
            }
            return "?";
        }

        // A sprite at its own proportions, `width` wide (the title logo). Nothing when missing.
        public static Widget Picture(string sprite, float width)
        {
            Sprite s = Theme.Sprite(sprite);
            if (s == null) return Empty;
            float h = s.rect.width > 0f ? width * s.rect.height / s.rect.width : width;
            return new Image(s, width, h, ImageFit.Contain);
        }

        // The icon an object is shown with in a pocket or on a card.
        public static string IconOf(MovableObject item)
        {
            if (item == null) return UiSprites.IconPocket;
            if (item.TryGetComponent(out GrenadeItem _)) return UiSprites.IconGrenade;
            if (item.TryGetComponent(out CigaretteItem _)) return UiSprites.IconCigarette;
            if (item.TryGetComponent(out BeerItem _)) return UiSprites.IconBeer;
            if (item.TryGetComponent(out KeyItem _)) return UiSprites.IconKey;
            return item.requiredForContract ? UiSprites.IconBox : UiSprites.IconMoney;
        }

        // ---- key glyphs ----

        // One key, mouse button or pad button, `height` tall.
        public static Widget Key(in Glyph g, float height)
        {
            switch (g.kind)
            {
                case GlyphKind.None:
                    return new SizedBox(Empty, 0f, height);
                case GlyphKind.Sprite:
                {
                    Sprite s = Theme.Sprite(g.sprite);
                    if (s != null)
                    {
                        // The glyph art keeps a transparent margin (16 of 128 texels) for its
                        // outline and shadow: drawn a little larger, it sits level with a key cap.
                        float h = height * 1.25f;
                        float w = s.rect.height > 0f ? h * s.rect.width / s.rect.height : h;
                        return new SizedBox(new Center(new Image(s, w, h, ImageFit.Contain)), w * 0.86f, height);
                    }
                    return Cap(Skins.KeyWide, g.text, height, true);
                }
                case GlyphKind.WideKey:
                    return Cap(Skins.KeyWide, g.text, height, true);
                default:
                    return Cap(Skins.KeyCap, g.text, height, false);
            }
        }

        // A blank key cap with its letter or word in the skin's face (Fredoka SemiBold, ink):
        // on the cap's top face, which the content rect puts a little above the middle.
        static Widget Cap(UiSkin skin, string text, float height, bool wide) =>
            KeyFace(skin, text, height, Tx.Sized(Mathf.Max(11f, height * (wide ? 0.42f : 0.5f)), false), wide);

        public static Widget KeyFace(UiSkin skin, string text, float height, LumaTextStyle style, bool wide = false)
        {
            EdgeInsets pad = skin.Padding(0f);
            float side = wide ? Mathf.Max(pad.Left, height * 0.28f) : pad.Left;
            Widget label = new Padding(new Text(text ?? "?", style, false), new EdgeInsets(side, pad.Top, side, pad.Bottom));
            Widget face = new Stack(new[] { skin.Widget, UiCenter.Instance.Widget, label });
            return new ConstrainedBox(new SizedBox(face, null, height), new BoxConstraints(minWidth: height));
        }

        // "[E] Ouvrir", with the second key of a chord ("LT + RS") and the gesture ("hold").
        public static Widget HintRow(in HintLine line, float keyHeight, LumaTextStyle style = null)
        {
            var parts = new List<Widget>(5) { Key(line.glyph, keyHeight) };
            if (!line.glyph2.IsNone)
            {
                parts.Add(Label("+", Tx.SmallBold, false));
                parts.Add(Key(line.glyph2, keyHeight));
            }
            string words = HintSet.Words(line);
            parts.Add(line.warn
                ? Label(words, style ?? Tx.BodyBold, Theme.badInk, false)   // hint rows sit on paper
                : Label(words, style ?? Tx.BodyBold, false));
            string gesture = HintSet.GestureWord(line.gesture);
            if (gesture != null) parts.Add(Chip(gesture));
            return new Row(parts, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        // A small pill of text: "hold", "P2", "x2".
        public static Widget Chip(string text, Color? color = null)
        {
            UiSkin skin = color.HasValue ? Skins.ChipIn(color.Value) : Skins.Chip;
            return Skinned(skin, Label(text, Tx.CaptionBold, false), EdgeInsets.Symmetric(8f, 2f));
        }

        // ---- the tape-measure bar ----

        // A tape measure pulled out to `fill`'s value. The fill is a probe owned by the caller's
        // model: setting it moves the tape without rebuilding anything.
        // The case (bar_tape_bg) is shown at its drawn height, UiSkins.TapeHeight; the tape
        // runs in its slot (the content rect), from the case on the left. `warn` reddens it.
        public static Widget TapeBar(UiFill fill, Color color, float width, bool warn = false)
        {
            float h = UiSkins.TapeHeight;
            EdgeInsets slot = Skins.TapeBg.Padding(0f);
            float inner = Mathf.Max(4f, h - slot.Top - slot.Bottom);
            Widget tape = new Stack(new[] { Skins.TapeFill(color, warn).Widget, fill.Widget, new SizedBox(Empty, null, inner) });
            return new SizedBox(Skinned(Skins.TapeBg, tape, slot), width, h);
        }

        // ---- toasts, speech, buttons ----

        // A line on a paper tag with a coloured icon: "P2 pocketed the watch ($120)".
        public static Widget Toast(string text, string icon, Color accent, float maxWidth)
        {
            var tag = Skins.Tag;
            EdgeInsets pad = tag.Padding(4f);
            float textWidth = Mathf.Max(80f, maxWidth - pad.Left - pad.Right - 36f);
            Widget row = new Row(new[]
            {
                Icon(icon, 26f, accent),
                Wrapped(text, Tx.BodyBold, textWidth),
            }, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return Skinned(tag, row, pad, UiMotion.Pop);
        }

        static readonly UiTransform TailTurn = new UiTransform(45f);
        static readonly UiPlace TailPlace = new UiPlace(50f, 100f, 50f, 50f, 0f, -2f);

        // The grandmother's words on a paper tag with a little tail pointing at her. The tail
        // is drawn first, so the tag covers its upper half.
        public static Widget Speech(string text, float maxWidth)
        {
            Widget tail = new Stack(new[] { Skins.TagTail.Widget, TailPlace.Widget, TailTurn.Widget, new SizedBox(Empty, 18f, 18f) });
            Widget bubble = ConstrainedBox.AtMost(Tag(Label(text, Tx.LabelBold), 8f, null, Skins.TagBig), width: maxWidth);
            return new Stack(new[] { tail, bubble });
        }

        // A wooden button with hover and press, a click sound, and a key glyph when it has one.
        public static Widget Button(string label, Action onPressed, bool selected = false, bool enabled = true,
                                    Glyph glyph = default, float minWidth = 220f) =>
            new WoodButton(label, onPressed, selected, enabled, glyph, minWidth);
    }
}
