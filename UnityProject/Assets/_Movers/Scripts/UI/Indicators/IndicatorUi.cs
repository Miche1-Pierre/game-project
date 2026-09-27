using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // The small UI Toolkit helpers every indicator widget uses. Everything is absolutely placed
    // and ignores the pointer: the markers are an overlay, never something to click, and must not
    // eat a click meant for a menu underneath.
    public static class IndicatorUi
    {
        public static VisualElement Box(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0f;
            e.style.top = 0f;
            return e;
        }

        // A child that covers its parent exactly.
        public static T Fill<T>(T e) where T : VisualElement
        {
            e.pickingMode = PickingMode.Ignore;
            e.style.position = Position.Absolute;
            e.style.left = 0f; e.style.top = 0f; e.style.right = 0f; e.style.bottom = 0f;
            return e;
        }

        // A sprite as a background. Sprites with a border (9-slice) are sliced with it, scaled
        // from the 2x art to the screen.
        public static VisualElement Picture(string name, Sprite sprite, float sliceScale)
        {
            var e = Box(name);
            SetPicture(e, sprite, sliceScale);
            return e;
        }

        public static void SetPicture(VisualElement e, Sprite sprite, float sliceScale)
        {
            if (sprite == null)
            {
                e.style.backgroundImage = StyleKeyword.None;
                return;
            }
            e.style.backgroundImage = new StyleBackground(sprite);
            Vector4 b = sprite.border;   // left, bottom, right, top
            if (b.sqrMagnitude > 0f)
            {
                e.style.unitySliceLeft = Mathf.RoundToInt(b.x);
                e.style.unitySliceBottom = Mathf.RoundToInt(b.y);
                e.style.unitySliceRight = Mathf.RoundToInt(b.z);
                e.style.unitySliceTop = Mathf.RoundToInt(b.w);
                e.style.unitySliceScale = sliceScale;
            }
        }

        // A one-line label in Fredoka (or the theme's font when the font is missing), cream on a
        // dark outline so it reads over a bright sky and a dark cellar alike.
        public static Label Text(string name, Font font, Color color, Color outline)
        {
            var l = new Label { name = name, pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.left = 0f;
            l.style.top = 0f;
            l.style.marginLeft = 0f; l.style.marginRight = 0f; l.style.marginTop = 0f; l.style.marginBottom = 0f;
            l.style.paddingLeft = 0f; l.style.paddingRight = 0f; l.style.paddingTop = 0f; l.style.paddingBottom = 0f;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.color = color;
            l.style.unityTextOutlineColor = outline;
            if (font != null) l.style.unityFontDefinition = new StyleFontDefinition(font);
            return l;
        }

        public static void Size(VisualElement e, float w, float h)
        {
            e.style.width = w;
            e.style.height = h;
        }

        // Centred at (x, y) inside its parent, by position, for elements laid out once (not per frame).
        public static void Place(VisualElement e, float x, float y, float w, float h)
        {
            e.style.left = x - w * 0.5f;
            e.style.top = y - h * 0.5f;
            e.style.width = w;
            e.style.height = h;
        }

        // Dark icon on a light colour, cream on a dark one.
        public static Color InkOn(Color c, IndicatorSettings s)
        {
            float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            return luma > 0.6f ? s.woodDark : s.cream;
        }

        public static Color Opaque(Color c) { c.a = 1f; return c; }
    }
}
