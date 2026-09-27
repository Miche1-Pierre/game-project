using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // Enlarges the element that contains it as a whole (its boards, text and key glyphs) by one
    // factor: the menus are read from a couch, a few steps from the screen (Pierre, feedback 1).
    // The factor shrinks by itself when the element would not fit its frame, which is the
    // player's view in the HUD (half the screen in split screen, a quarter of its height when
    // stacked) or the whole screen in the title scene.
    //
    // A transform, not a relayout: nothing around the element moves, the mouse still hits the
    // buttons where they are drawn (UI Toolkit picks through transforms), and a rebuild of the
    // content costs nothing here. The fit is redone only when the content or the frame changes
    // size (GeometryChangedEvent); setting a scale never triggers one, so it cannot loop.
    //
    // Shared per placement like every probe; the state of each zoomed element is kept here.
    public sealed class UiZoom : UiProbe
    {
        public const float MenuFactor = 1.3f;
        const float MinFactor = 0.5f;

        // A page centred in a player's view: the pause menu, the controls sheet.
        public static readonly UiZoom Centered = new UiZoom(MenuFactor, 50f, 50f, 40f, 40f);

        readonly float factor;
        readonly float originX, originY;     // percent of the element: the point that stays put
        readonly float reserveX, reserveY;   // panel units of the frame the element may not use
        readonly List<Fit> fits = new List<Fit>(4);

        public UiZoom(float factor, float originXPercent, float originYPercent, float reserveX, float reserveY)
        {
            this.factor = Mathf.Max(MinFactor, factor);
            originX = originXPercent;
            originY = originYPercent;
            this.reserveX = reserveX;
            this.reserveY = reserveY;
        }

        protected override void Attached(VisualElement target)
        {
            // Elements that left the panel without a detach event of their own (their parent was
            // removed first) are dropped here, so no frame keeps a callback for them.
            for (int i = fits.Count - 1; i >= 0; i--)
            {
                if (fits[i].target != target && fits[i].target.panel != null) continue;
                fits[i].Unhook();
                fits.RemoveAt(i);
            }
            fits.Add(new Fit(this, target));
        }

        protected override void Detached(VisualElement target)
        {
            for (int i = fits.Count - 1; i >= 0; i--)
            {
                if (fits[i].target != target) continue;
                fits[i].Unhook();
                fits.RemoveAt(i);
            }
        }

        // The player's view container (HudRoot names them), or the top of the panel.
        static VisualElement FrameOf(VisualElement t)
        {
            VisualElement top = null;
            for (var e = t.parent; e != null; e = e.parent)
            {
                if (e.name != null && e.name.StartsWith(HudRoot.ViewNamePrefix, System.StringComparison.Ordinal)) return e;
                top = e;
            }
            return top;
        }

        sealed class Fit
        {
            public readonly VisualElement target;
            readonly UiZoom zoom;
            readonly VisualElement frame;
            readonly EventCallback<GeometryChangedEvent> onGeometry;
            float applied = -1f;

            public Fit(UiZoom zoom, VisualElement target)
            {
                this.zoom = zoom;
                this.target = target;
                frame = FrameOf(target);
                onGeometry = _ => Apply();
                target.style.transformOrigin = new TransformOrigin(Length.Percent(zoom.originX), Length.Percent(zoom.originY));
                target.RegisterCallback(onGeometry);
                frame?.RegisterCallback(onGeometry);
                Apply();
            }

            public void Unhook()
            {
                target.UnregisterCallback(onGeometry);
                frame?.UnregisterCallback(onGeometry);
            }

            void Apply()
            {
                float k = zoom.factor;
                if (frame != null)
                {
                    Rect box = target.layout, room = frame.layout;
                    if (Valid(box.width) && Valid(room.width)) k = Mathf.Min(k, (room.width - zoom.reserveX) / box.width);
                    if (Valid(box.height) && Valid(room.height)) k = Mathf.Min(k, (room.height - zoom.reserveY) / box.height);
                }
                k = Mathf.Clamp(k, MinFactor, zoom.factor);
                if (Mathf.Abs(k - applied) < 0.002f) return;
                applied = k;
                target.style.scale = new Scale(new Vector3(k, k, 1f));
            }

            static bool Valid(float v) => !float.IsNaN(v) && v >= 1f;
        }
    }
}
