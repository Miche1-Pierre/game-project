using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // The filled part of a bar (a tape measure pulled out to the value). Set it every frame
    // if you like: it writes the element's width only when the value moved, and never
    // rebuilds the widget tree. One fill per bar, owned by the model that feeds it.
    public sealed class UiFill : UiProbe
    {
        VisualElement element;
        float value = -1f;
        float shownValue = -2f;

        public float Value => Mathf.Max(0f, value);

        protected override void Attached(VisualElement target)
        {
            element = target;
            shownValue = -2f;
            Write();
        }

        protected override void Detached(VisualElement target)
        {
            if (element == target) element = null;
        }

        public void Set(float value01)
        {
            value = Mathf.Clamp01(value01);
            Write();
        }

        void Write()
        {
            if (element == null || value < 0f) return;
            // A quarter of a percent is below a pixel on any bar the HUD draws.
            if (Mathf.Abs(value - shownValue) < 0.0025f) return;
            shownValue = value;
            element.style.width = Length.Percent(value * 100f);
        }
    }
}
