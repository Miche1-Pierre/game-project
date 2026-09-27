using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // Puts the element that contains it at a point of its parent given in percent, with a
    // pixel offset and a pivot: "50%, 50% plus 28 px right, 24 px down" is just below and
    // right of the crosshair whatever the size of the view. LumaFlow's Positioned only takes
    // pixels, which would need a rebuild every time a split view changes size; this needs
    // none. Shared per placement like every probe.
    public sealed class UiPlace : UiProbe
    {
        readonly float x, y;        // anchor in the parent, percent
        readonly float px, py;      // pivot of this element, percent of its own size
        readonly float dx, dy;      // offset, panel units
        readonly bool fill;

        // Covers the whole parent: a scrim, an overlay that holds a centred card.
        public static readonly UiPlace Fill = new UiPlace();

        UiPlace() { fill = true; }

        public UiPlace(float xPercent, float yPercent, float pivotXPercent, float pivotYPercent, float offsetX = 0f, float offsetY = 0f)
        {
            x = xPercent; y = yPercent; px = pivotXPercent; py = pivotYPercent; dx = offsetX; dy = offsetY;
        }

        // Corners and edges, with a margin from the edge of the view.
        public static UiPlace TopLeft(float margin) => new UiPlace(0f, 0f, 0f, 0f, margin, margin);
        public static UiPlace TopRight(float margin) => new UiPlace(100f, 0f, 100f, 0f, -margin, margin);
        public static UiPlace TopCenter(float margin) => new UiPlace(50f, 0f, 50f, 0f, 0f, margin);
        public static UiPlace BottomLeft(float margin) => new UiPlace(0f, 100f, 0f, 100f, margin, -margin);
        public static UiPlace BottomRight(float margin) => new UiPlace(100f, 100f, 100f, 100f, -margin, -margin);
        public static UiPlace BottomCenter(float margin) => new UiPlace(50f, 100f, 50f, 100f, 0f, -margin);
        public static UiPlace Center(float offsetX = 0f, float offsetY = 0f) => new UiPlace(50f, 50f, 50f, 50f, offsetX, offsetY);

        protected override void Attached(VisualElement t)
        {
            var s = t.style;
            s.position = Position.Absolute;
            if (fill)
            {
                s.left = 0f;
                s.top = 0f;
                s.right = 0f;
                s.bottom = 0f;
                return;
            }
            s.left = Length.Percent(x);
            s.top = Length.Percent(y);
            s.translate = new Translate(Length.Percent(-px), Length.Percent(-py));
            s.marginLeft = dx;
            s.marginTop = dy;
        }
    }

    // Centres the content of the element that contains it, both ways: a key cap's letter, an
    // empty pocket's word. LumaFlow's Center is a child of the box and does not fill it.
    public sealed class UiCenter : UiProbe
    {
        public static readonly UiCenter Instance = new UiCenter();

        protected override void Attached(VisualElement t)
        {
            t.style.justifyContent = Justify.Center;
            t.style.alignItems = Align.Center;
        }
    }

    // Names the element that contains it, so a test (or the UI Toolkit debugger) can find a
    // region of the HUD: "hud-hints" is the key hints panel. One shared probe per name.
    public sealed class UiName : UiProbe
    {
        readonly string name;

        public UiName(string name) { this.name = name; }

        protected override void Attached(VisualElement t) { t.name = name; }
    }

    // A fixed transform on the element that contains it: the speech bubble's tail turned 45
    // degrees, a sign hung slightly crooked.
    public sealed class UiTransform : UiProbe
    {
        readonly float degrees, scale, offsetY;

        public UiTransform(float degrees, float scale = 1f, float offsetY = 0f)
        {
            this.degrees = degrees;
            this.scale = scale;
            this.offsetY = offsetY;
        }

        protected override void Attached(VisualElement t)
        {
            t.style.rotate = new Rotate(new Angle(degrees));
            if (!Mathf.Approximately(scale, 1f)) t.style.scale = new Scale(new Vector3(scale, scale, 1f));
            if (offsetY != 0f) t.style.translate = new Translate(0f, offsetY);
        }
    }
}
