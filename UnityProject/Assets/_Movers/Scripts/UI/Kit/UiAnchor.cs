using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // A handle on the element that contains it, so code outside the widget tree can move it
    // every frame without a rebuild: a speech bubble over the grandmother's head, the
    // "deliver here" sign over the truck's board. One anchor per thing and per view, owned by
    // whoever moves it (the HUD's per-player model).
    //
    // Moving writes left/top and display only: struct styles, no allocation.
    public sealed class UiAnchor : UiProbe
    {
        public VisualElement Element { get; private set; }

        bool shown;   // hidden until the first frame places it: no flash at the corner
        Vector2 at = new Vector2(float.NaN, float.NaN);

        protected override void Attached(VisualElement target)
        {
            Element = target;
            target.style.position = Position.Absolute;
            // Centred above the point: the bubble's tail sits on the head.
            target.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-100f));
            at = new Vector2(float.NaN, float.NaN);
            ApplyShown(shown);
        }

        protected override void Detached(VisualElement target)
        {
            if (Element == target) Element = null;
        }

        // Where the anchor point goes, in the coordinates of the container it lives in.
        public void MoveTo(Vector2 local)
        {
            if (Element == null) return;
            if ((local - at).sqrMagnitude < 0.25f) return;
            at = local;
            Element.style.left = local.x;
            Element.style.top = local.y;
        }

        public void Show(bool on)
        {
            if (on == shown) return;
            shown = on;
            ApplyShown(on);
        }

        void ApplyShown(bool on)
        {
            if (Element != null) Element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Size of the anchored box as last laid out (0 before its first layout).
        public Vector2 Size => Element != null ? new Vector2(Element.layout.width, Element.layout.height) : Vector2.zero;
    }
}
