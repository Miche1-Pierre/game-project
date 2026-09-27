using LumaFlow;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Movers
{
    // How a box looks: a 9-slice sprite from UIART (a wooden board, a cardboard flap, a paper
    // tag), or, while that sprite does not exist, a flat colour with a border and rounded
    // corners in the same tones. Also the text inside it: its colour, face and shadow, which
    // every Text below inherits (ink on the pale board and paper, cream on walnut).
    //
    // UIART's art is drawn at 2x with thick borders; each skin chooses the slice scale it is
    // shown at (a key cap at a quarter, a menu board at almost a half), and its padding comes
    // from the sprite's own content rect (sprites.json) at that scale, so text always sits
    // inside the frame, whatever scale.
    //
    // Skins are made once (UiSkins holds the game's set) and shared by every panel of that
    // kind, which is what lets LumaFlow keep their native elements across rebuilds.
    public sealed class UiSkin : UiProbe
    {
        public readonly string sprite;
        public readonly float sliceScale;     // 0: the theme's default (0.5, the 2x density)
        public readonly Color tint;
        public readonly Color fill;           // flat fallback
        public readonly Color border;
        public readonly float borderWidth;
        public readonly float radius;
        public readonly float fallbackPadding;
        public readonly Color? text;
        public readonly UiWeight? weight;
        public readonly bool shadow;
        public readonly bool tiled;           // a seamless texture repeated (bg_paper)
        public readonly bool tiledCenter;     // a 9-slice whose centre repeats (the tape's ticks)

        public UiSkin(string sprite, Color fill, Color border, float borderWidth = 3f, float radius = 10f,
                      Color? text = null, UiWeight? weight = null, bool shadow = false, Color? tint = null,
                      float sliceScale = 0f, float fallbackPadding = 10f, bool tiled = false, bool tiledCenter = false)
        {
            this.sprite = sprite;
            this.sliceScale = sliceScale;
            this.fill = fill;
            this.border = border;
            this.borderWidth = borderWidth;
            this.radius = radius;
            this.fallbackPadding = fallbackPadding;
            this.text = text;
            this.weight = weight;
            this.shadow = shadow;
            this.tint = tint ?? Color.white;
            this.tiled = tiled;
            this.tiledCenter = tiledCenter;
        }

        float Scale(UiTheme theme) => sliceScale > 0f ? sliceScale : theme.spriteSliceScale;

        // Where the content goes: the sprite's content rect at this skin's scale, at least
        // `min` on every side; the flat fallback's padding while the sprite is missing.
        public EdgeInsets Padding(float min = 0f)
        {
            var theme = UiTheme.Current;
            if (theme.Sprite(sprite) != null && !tiled)
            {
                Vector4 c = theme.Content(sprite);   // left, bottom, right, top, in texels
                float k = Scale(theme);
                return new EdgeInsets(Mathf.Max(min, c.x * k), Mathf.Max(min, c.w * k), Mathf.Max(min, c.z * k), Mathf.Max(min, c.y * k));
            }
            float p = Mathf.Max(min, fallbackPadding);
            return new EdgeInsets(p + 2f, p, p + 2f, p);
        }

        protected override void Attached(VisualElement t) => Apply(t);

        public void Apply(VisualElement t)
        {
            var theme = UiTheme.Current;
            var s = t.style;
            Sprite image = theme.Sprite(sprite);
            if (image != null)
            {
                s.backgroundImage = new StyleBackground(image);
                s.unityBackgroundImageTintColor = tint;
                float k = Scale(theme);
                if (tiled)
                {
                    var r = image.rect;
                    s.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                    s.backgroundSize = new BackgroundSize(new Length(r.width * k), new Length(r.height * k));
                    s.backgroundColor = fill;
                    SetBorder(s, border, borderWidth, radius);
                }
                else
                {
                    Vector4 b = image.border;   // left, bottom, right, top
                    s.unitySliceLeft = Mathf.RoundToInt(b.x);
                    s.unitySliceBottom = Mathf.RoundToInt(b.y);
                    s.unitySliceRight = Mathf.RoundToInt(b.z);
                    s.unitySliceTop = Mathf.RoundToInt(b.w);
                    s.unitySliceScale = k;
                    s.unitySliceType = tiledCenter ? SliceType.Tiled : SliceType.Sliced;
                    s.backgroundColor = Color.clear;
                    SetBorder(s, Color.clear, 0f, 0f);
                }
            }
            else
            {
                s.backgroundImage = StyleKeyword.Null;
                s.backgroundColor = fill;
                SetBorder(s, border, borderWidth, radius);
            }

            if (text.HasValue) s.color = text.Value;
            if (weight.HasValue)
            {
                FontAsset face = theme.FontFor(weight.Value);
                if (face != null) s.unityFontDefinition = FontDefinition.FromSDFFont(face);
            }
            // Always written, so a paper tag inside a walnut panel drops the panel's shadow.
            s.textShadow = shadow
                ? new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 0f, color = theme.shadow }
                : new TextShadow { offset = Vector2.zero, blurRadius = 0f, color = Color.clear };
        }

        static void SetBorder(IStyle s, Color c, float width, float r)
        {
            s.borderTopWidth = width;
            s.borderBottomWidth = width + (width > 0f ? 1f : 0f);   // a thicker bottom edge: a board, not a sticker
            s.borderLeftWidth = width;
            s.borderRightWidth = width;
            s.borderTopColor = c;
            s.borderBottomColor = c;
            s.borderLeftColor = c;
            s.borderRightColor = c;
            s.borderTopLeftRadius = r;
            s.borderTopRightRadius = r;
            s.borderBottomLeftRadius = r;
            s.borderBottomRightRadius = r;
        }
    }
}
