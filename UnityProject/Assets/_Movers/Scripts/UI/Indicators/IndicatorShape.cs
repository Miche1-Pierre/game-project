using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // A flat low-poly shape drawn with UI Toolkit's vector painter: what the indicators draw when
    // a sprite of the UIART set is missing, and the coloured disc under the ring sprite (the ring
    // art is a frame; the target's colour is this disc, whatever colour the art is).
    //
    // Drawn only when the element is repainted (a new colour, a new size), never per frame: the
    // markers move with style.translate, which does not repaint.
    public sealed class IndicatorShape : VisualElement
    {
        public enum Shape
        {
            Octagon,      // a token: the target's colour, a dark rim, a lighter top-left facet
            Arrow,        // an arrowhead pointing right (+x), rotated by its parent
            Plank,        // the compass tape: a chamfered wooden board with two nails
            Notch,        // a small triangle pointing down: where the player looks
            Caret,        // a small triangle pointing up (rotated 180 for down): another floor
            IconKey, IconTruck, IconBox, IconGrandma, IconFlag, IconPoliceCar,
        }

        Shape shape;
        Color fill = Color.white, rim = Color.black, light = Color.white;
        float rimWidth = 2f;

        public IndicatorShape(Shape shape)
        {
            this.shape = shape;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        // Colours in one call, repainting only when one really changed.
        public void Set(Color fill, Color rim, float rimWidth)
        {
            if (fill == this.fill && rim == this.rim && Mathf.Approximately(rimWidth, this.rimWidth)) return;
            this.fill = fill;
            this.rim = rim;
            this.rimWidth = rimWidth;
            light = Color.Lerp(fill, Color.white, 0.22f);
            light.a = fill.a;
            MarkDirtyRepaint();
        }

        public void SetShape(Shape s)
        {
            if (s == shape) return;
            shape = s;
            MarkDirtyRepaint();
        }

        public static bool HasFallback(IndicatorIcon icon, out Shape s)
        {
            switch (icon)
            {
                case IndicatorIcon.Key: s = Shape.IconKey; return true;
                case IndicatorIcon.Truck: s = Shape.IconTruck; return true;
                case IndicatorIcon.Box: s = Shape.IconBox; return true;
                case IndicatorIcon.GrandmaCalm:
                case IndicatorIcon.GrandmaAnnoyed:
                case IndicatorIcon.GrandmaAngry:
                case IndicatorIcon.GrandmaFurious: s = Shape.IconGrandma; return true;
                case IndicatorIcon.Flag: s = Shape.IconFlag; return true;
                case IndicatorIcon.PoliceCar: s = Shape.IconPoliceCar; return true;
                default: s = Shape.Octagon; return false;
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 1f || r.height < 1f) return;
            Painter2D p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            switch (shape)
            {
                case Shape.Octagon: Octagon(p, r); break;
                case Shape.Arrow: Arrow(p, r); break;
                case Shape.Plank: Plank(p, r); break;
                case Shape.Notch: Triangle(p, r, true); break;
                case Shape.Caret: Triangle(p, r, false); break;
                case Shape.IconKey: Key(p, r); break;
                case Shape.IconTruck: Truck(p, r); break;
                case Shape.IconBox: Box(p, r); break;
                case Shape.IconGrandma: Grandma(p, r); break;
                case Shape.IconFlag: Flag(p, r); break;
                case Shape.IconPoliceCar: PoliceCar(p, r); break;
            }
        }

        // ---- shapes ----

        static readonly Vector2[] oct = new Vector2[8];

        void Octagon(Painter2D p, Rect r)
        {
            float inset = rimWidth * 0.5f;
            Vector2 c = r.center;
            float rad = Mathf.Min(r.width, r.height) * 0.5f - inset;
            for (int i = 0; i < 8; i++)
            {
                float a = (22.5f + 45f * i) * Mathf.Deg2Rad;
                oct[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            }
            Poly(p, oct, 8);
            p.fillColor = fill;
            p.Fill();
            // The facets facing the light (top left: vertices 4..6 with y down) a shade lighter.
            p.BeginPath();
            p.MoveTo(c);
            p.LineTo(oct[4]);
            p.LineTo(oct[5]);
            p.LineTo(oct[6]);
            p.ClosePath();
            p.fillColor = light;
            p.Fill();
            if (rimWidth > 0f)
            {
                Poly(p, oct, 8);
                p.strokeColor = rim;
                p.lineWidth = rimWidth;
                p.Stroke();
            }
        }

        void Arrow(Painter2D p, Rect r)
        {
            float i = rimWidth * 0.6f;
            var tip = new Vector2(r.xMax - i, r.center.y);
            var top = new Vector2(r.x + i, r.y + i);
            var notch = new Vector2(r.x + r.width * 0.3f, r.center.y);
            var bottom = new Vector2(r.x + i, r.yMax - i);
            p.BeginPath();
            p.MoveTo(tip); p.LineTo(top); p.LineTo(notch); p.LineTo(bottom); p.ClosePath();
            p.fillColor = fill;
            p.Fill();
            // Upper half lit.
            p.BeginPath();
            p.MoveTo(tip); p.LineTo(top); p.LineTo(notch); p.ClosePath();
            p.fillColor = light;
            p.Fill();
            p.BeginPath();
            p.MoveTo(tip); p.LineTo(top); p.LineTo(notch); p.LineTo(bottom); p.ClosePath();
            p.strokeColor = rim;
            p.lineWidth = rimWidth;
            p.Stroke();
        }

        void Plank(Painter2D p, Rect r)
        {
            float i = rimWidth * 0.5f;
            float ch = Mathf.Min(r.height * 0.32f, r.width * 0.1f);
            float x0 = r.x + i, x1 = r.xMax - i, y0 = r.y + i, y1 = r.yMax - i;
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + ch, y0)); p.LineTo(new Vector2(x1 - ch, y0)); p.LineTo(new Vector2(x1, y0 + ch));
            p.LineTo(new Vector2(x1, y1 - ch)); p.LineTo(new Vector2(x1 - ch, y1)); p.LineTo(new Vector2(x0 + ch, y1));
            p.LineTo(new Vector2(x0, y1 - ch)); p.LineTo(new Vector2(x0, y0 + ch)); p.ClosePath();
            p.fillColor = fill;
            p.Fill();
            // The top bevel catches the light: a lighter band along the upper third.
            float band = y0 + (y1 - y0) * 0.36f;
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + ch, y0)); p.LineTo(new Vector2(x1 - ch, y0)); p.LineTo(new Vector2(x1, y0 + ch));
            p.LineTo(new Vector2(x1, band)); p.LineTo(new Vector2(x0, band)); p.LineTo(new Vector2(x0, y0 + ch)); p.ClosePath();
            p.fillColor = light;
            p.Fill();
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + ch, y0)); p.LineTo(new Vector2(x1 - ch, y0)); p.LineTo(new Vector2(x1, y0 + ch));
            p.LineTo(new Vector2(x1, y1 - ch)); p.LineTo(new Vector2(x1 - ch, y1)); p.LineTo(new Vector2(x0 + ch, y1));
            p.LineTo(new Vector2(x0, y1 - ch)); p.LineTo(new Vector2(x0, y0 + ch)); p.ClosePath();
            p.strokeColor = rim;
            p.lineWidth = rimWidth;
            p.Stroke();
            // Two nails, one at each end.
            float nail = Mathf.Max(1.5f, r.height * 0.07f);
            p.fillColor = rim;
            p.BeginPath(); p.Arc(new Vector2(x0 + ch * 1.6f, r.center.y), nail, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(new Vector2(x1 - ch * 1.6f, r.center.y), nail, 0f, 360f); p.Fill();
        }

        void Triangle(Painter2D p, Rect r, bool down)
        {
            float i = rimWidth * 0.6f;
            Vector2 a, b, c;
            if (down) { a = new Vector2(r.x + i, r.y + i); b = new Vector2(r.xMax - i, r.y + i); c = new Vector2(r.center.x, r.yMax - i); }
            else { a = new Vector2(r.x + i, r.yMax - i); b = new Vector2(r.xMax - i, r.yMax - i); c = new Vector2(r.center.x, r.y + i); }
            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.ClosePath();
            p.fillColor = fill;
            p.Fill();
            p.strokeColor = rim;
            p.lineWidth = rimWidth;
            p.Stroke();
        }

        // ---- icons, drawn in fill with a rim, on a unit square mapped onto r ----

        static Vector2 U(Rect r, float x, float y) => new Vector2(r.x + x * r.width, r.y + y * r.height);

        void Key(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height);
            // Bow: a ring on the left.
            p.BeginPath(); p.Arc(U(r, 0.3f, 0.5f), s * 0.2f, 0f, 360f);
            p.strokeColor = fill; p.lineWidth = s * 0.12f; p.Stroke();
            // Shaft and two teeth.
            p.BeginPath();
            p.MoveTo(U(r, 0.48f, 0.5f)); p.LineTo(U(r, 0.9f, 0.5f));
            p.MoveTo(U(r, 0.78f, 0.5f)); p.LineTo(U(r, 0.78f, 0.68f));
            p.MoveTo(U(r, 0.9f, 0.5f)); p.LineTo(U(r, 0.9f, 0.7f));
            p.strokeColor = fill; p.lineWidth = s * 0.11f; p.Stroke();
        }

        void Truck(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height);
            p.fillColor = fill;
            // Box and cab.
            p.BeginPath();
            p.MoveTo(U(r, 0.08f, 0.25f)); p.LineTo(U(r, 0.62f, 0.25f)); p.LineTo(U(r, 0.62f, 0.7f)); p.LineTo(U(r, 0.08f, 0.7f)); p.ClosePath();
            p.Fill();
            p.BeginPath();
            p.MoveTo(U(r, 0.66f, 0.38f)); p.LineTo(U(r, 0.82f, 0.38f)); p.LineTo(U(r, 0.93f, 0.52f)); p.LineTo(U(r, 0.93f, 0.7f));
            p.LineTo(U(r, 0.66f, 0.7f)); p.ClosePath();
            p.Fill();
            // Wheels, cut out in the rim colour.
            p.fillColor = rim;
            p.BeginPath(); p.Arc(U(r, 0.27f, 0.74f), s * 0.1f, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(U(r, 0.77f, 0.74f), s * 0.1f, 0f, 360f); p.Fill();
        }

        void Box(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height);
            p.BeginPath();
            p.MoveTo(U(r, 0.18f, 0.28f)); p.LineTo(U(r, 0.82f, 0.28f)); p.LineTo(U(r, 0.82f, 0.8f)); p.LineTo(U(r, 0.18f, 0.8f)); p.ClosePath();
            p.fillColor = fill; p.Fill();
            // The packing tape across the lid.
            p.BeginPath(); p.MoveTo(U(r, 0.5f, 0.28f)); p.LineTo(U(r, 0.5f, 0.55f));
            p.strokeColor = rim; p.lineWidth = s * 0.09f; p.Stroke();
        }

        void Grandma(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height);
            p.fillColor = fill;
            // Bun, head, shoulders.
            p.BeginPath(); p.Arc(U(r, 0.5f, 0.2f), s * 0.11f, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(U(r, 0.5f, 0.42f), s * 0.18f, 0f, 360f); p.Fill();
            p.BeginPath();
            p.MoveTo(U(r, 0.2f, 0.9f)); p.LineTo(U(r, 0.28f, 0.66f)); p.LineTo(U(r, 0.72f, 0.66f)); p.LineTo(U(r, 0.8f, 0.9f)); p.ClosePath();
            p.Fill();
            // Round glasses.
            p.strokeColor = rim; p.lineWidth = Mathf.Max(1f, s * 0.04f);
            p.BeginPath(); p.Arc(U(r, 0.43f, 0.43f), s * 0.055f, 0f, 360f); p.Stroke();
            p.BeginPath(); p.Arc(U(r, 0.57f, 0.43f), s * 0.055f, 0f, 360f); p.Stroke();
        }

        // The exit: a pole and a pennant.
        void Flag(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height);
            p.BeginPath(); p.MoveTo(U(r, 0.3f, 0.12f)); p.LineTo(U(r, 0.3f, 0.9f));
            p.strokeColor = fill; p.lineWidth = s * 0.09f; p.Stroke();
            p.BeginPath();
            p.MoveTo(U(r, 0.34f, 0.14f)); p.LineTo(U(r, 0.84f, 0.3f)); p.LineTo(U(r, 0.34f, 0.5f)); p.ClosePath();
            p.fillColor = fill; p.Fill();
        }

        // A car seen from the side with a light bar on the roof.
        void PoliceCar(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height);
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(U(r, 0.08f, 0.5f)); p.LineTo(U(r, 0.3f, 0.5f)); p.LineTo(U(r, 0.38f, 0.34f)); p.LineTo(U(r, 0.66f, 0.34f));
            p.LineTo(U(r, 0.74f, 0.5f)); p.LineTo(U(r, 0.92f, 0.52f)); p.LineTo(U(r, 0.92f, 0.7f)); p.LineTo(U(r, 0.08f, 0.7f)); p.ClosePath();
            p.Fill();
            // The light bar.
            p.BeginPath();
            p.MoveTo(U(r, 0.44f, 0.22f)); p.LineTo(U(r, 0.6f, 0.22f)); p.LineTo(U(r, 0.6f, 0.3f)); p.LineTo(U(r, 0.44f, 0.3f)); p.ClosePath();
            p.Fill();
            // Wheels, cut out in the rim colour.
            p.fillColor = rim;
            p.BeginPath(); p.Arc(U(r, 0.27f, 0.72f), s * 0.1f, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(U(r, 0.74f, 0.72f), s * 0.1f, 0f, 360f); p.Fill();
        }

        static void Poly(Painter2D p, Vector2[] pts, int n)
        {
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < n; i++) p.LineTo(pts[i]);
            p.ClosePath();
        }
    }
}
