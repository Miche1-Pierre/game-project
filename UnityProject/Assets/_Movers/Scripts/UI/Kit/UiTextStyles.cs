using System.Collections.Generic;
using UnityEngine;
using LumaTextStyle = LumaFlow.TextStyle;

namespace Movers
{
    // The game's text sizes as shared LumaFlow TextStyles. The plain ones carry no colour, so
    // the text takes the ink or cream of the panel it sits on (UiSkin); Tinted gives a fixed
    // colour (a red "Destroyed", a green "+$400"), cached per colour so a rebuild reuses it.
    public sealed class UiTextStyles
    {
        public readonly LumaTextStyle Caption, Small, Body, Label, Title, Display;
        public readonly LumaTextStyle CaptionBold, SmallBold, BodyBold, LabelBold, TitleBold;
        public readonly LumaTextStyle Key;        // the letter on a key cap

        readonly Dictionary<Color, LumaTextStyle>[] tinted = new Dictionary<Color, LumaTextStyle>[11];
        readonly LumaTextStyle[] all;

        public UiTextStyles(UiTheme t)
        {
            Caption = new LumaTextStyle(null, t.textCaption, FontStyle.Normal);
            Small = new LumaTextStyle(null, t.textSmall, FontStyle.Normal);
            Body = new LumaTextStyle(null, t.textBody, FontStyle.Normal);
            Label = new LumaTextStyle(null, t.textLabel, FontStyle.Normal);
            Title = new LumaTextStyle(null, t.textTitle, FontStyle.Bold);
            Display = new LumaTextStyle(null, t.textDisplay, FontStyle.Bold);
            CaptionBold = new LumaTextStyle(null, t.textCaption, FontStyle.Bold);
            SmallBold = new LumaTextStyle(null, t.textSmall, FontStyle.Bold);
            BodyBold = new LumaTextStyle(null, t.textBody, FontStyle.Bold);
            LabelBold = new LumaTextStyle(null, t.textLabel, FontStyle.Bold);
            TitleBold = Title;
            Key = new LumaTextStyle(null, t.keyCap * 0.52f, FontStyle.Bold);
            all = new[] { Caption, Small, Body, Label, Title, Display, CaptionBold, SmallBold, BodyBold, LabelBold, Key };
        }

        // The same size and weight as `style`, in a fixed colour.
        public LumaTextStyle Tinted(LumaTextStyle style, Color color)
        {
            int i = System.Array.IndexOf(all, style);
            if (i < 0) return new LumaTextStyle(color, style.FontSize, style.FontStyle);
            var map = tinted[i] ?? (tinted[i] = new Dictionary<Color, LumaTextStyle>());
            if (!map.TryGetValue(color, out var s))
            {
                s = new LumaTextStyle(color, style.FontSize, style.FontStyle);
                map.Add(color, s);
            }
            return s;
        }

        // A one-off size (the speedometer's digits), cached by size.
        readonly Dictionary<int, LumaTextStyle> sized = new Dictionary<int, LumaTextStyle>();
        public LumaTextStyle Sized(float size, bool bold)
        {
            int k = Mathf.RoundToInt(size * 10f) * 2 + (bold ? 1 : 0);
            if (!sized.TryGetValue(k, out var s))
            {
                s = new LumaTextStyle(null, size, bold ? FontStyle.Bold : FontStyle.Normal);
                sized.Add(k, s);
            }
            return s;
        }
    }
}
