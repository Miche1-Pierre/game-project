using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // Where UICORE's HUD sits in the corners of each view: the contract card top left, the
    // grandmother's panel and her toasts top right, the controls chip bottom left, the pockets
    // bottom right. The HUD's panel draws over the markers' (order 10 over -5), so a marker under
    // one of them is lost: the compass tape stops short of the top ones and the edge tokens slide
    // round all of them (CrewIndicatorView, IndicatorPlacement.Keepout).
    //
    // Measured, not guessed. The contract card grows with the contract (about 450 px high with
    // Map01's twelve rows), the toasts come and go, and the HUD scales a narrow view down: a fixed
    // share of the view, the first layout's rule, was wrong in each of those cases.
    //
    // How: every region of UICORE's HUD is placed by a UiPlace probe (UICORE, UI/Kit/UiPlace.cs),
    // an invisible child whose userData is the probe and which anchors its parent at a point of
    // the view given in percent. A region anchored at 0% or 100% both across and down is a corner
    // panel, and its worldBound is where it really is, whatever its rows, font or scale. The walk
    // stops at every placed region, so it only visits the HUD's skeleton, and it runs twice a
    // second (or when the HUD changed); reading the rects each frame allocates nothing.
    //
    // The only file of the indicators that knows how UICORE builds its HUD. If UICORE exposes the
    // rects itself (INTEGRATION.md, request 4), that query replaces the walk.
    public sealed class HudCorners
    {
        public const int Max = 16;          // corner regions over every view: 4 per view today
        const int MaxDepth = 32;
        const float RescanSeconds = 0.5f;
        const float EdgePercent = 0.5f;     // how close to 0% or 100% an anchor must be

        readonly VisualElement[] regions = new VisualElement[Max];
        int count;
        HudRoot hud;
        UIDocument document;
        float nextScan = float.NegativeInfinity;
        Vector2 panelOrigin;                // HUD panel units at the screen's top left
        Vector2 pixelsPerUnit = Vector2.one;
        int screenW = -1, screenH = -1;

        public int RegionCount => count;

        // Once per frame, before the views collect. Rescans when the HUD changed (a reload), when
        // a region left its panel (a rebuild), and twice a second in case a region appeared.
        public void Update(float now)
        {
            HudRoot active = HudRoot.Active;
            if (active != hud)
            {
                hud = active;
                document = hud != null ? hud.GetComponent<UIDocument>() : null;
                nextScan = float.NegativeInfinity;
            }
            if (hud == null || document == null || !hud.isActiveAndEnabled)
            {
                DropRegions();
                return;
            }
            if (now >= nextScan || AnyDetached())
            {
                nextScan = now + RescanSeconds;
                Scan();
            }
            if (count > 0 && (Screen.width != screenW || Screen.height != screenH)) MeasurePanel();
        }

        // The next Update walks the HUD again, whatever the clock says. For the allocation test,
        // which runs many ticks inside one frame and would otherwise never see a rescan.
        public void Invalidate() { nextScan = float.NegativeInfinity; }

        public void Clear()
        {
            DropRegions();
            hud = null;
            document = null;
            nextScan = float.NegativeInfinity;
        }

        // The corner regions drawn inside this view, in OnGUI pixels, into the array. Empty
        // regions (no toast right now, no contract yet) are left out. Returns how many.
        public int Collect(Rect view, Rect[] into)
        {
            int n = 0;
            for (int i = 0; i < count && n < into.Length; i++)
            {
                VisualElement e = regions[i];
                if (e == null || e.panel == null) continue;
                Rect wb = e.worldBound;
                if (float.IsNaN(wb.x) || float.IsNaN(wb.width) || wb.width < 2f || wb.height < 2f) continue;
                var r = new Rect((wb.x - panelOrigin.x) * pixelsPerUnit.x, (wb.y - panelOrigin.y) * pixelsPerUnit.y,
                                 wb.width * pixelsPerUnit.x, wb.height * pixelsPerUnit.y);
                if (!view.Contains(r.center)) continue;
                // As big as the view, it is an overlay (a menu), not a corner panel: keeping out
                // of it would hide every marker.
                if (r.width > view.width * 0.7f || r.height > view.height * 0.8f) continue;
                into[n++] = r;
            }
            return n;
        }

        bool AnyDetached()
        {
            for (int i = 0; i < count; i++) if (regions[i] == null || regions[i].panel == null) return true;
            return false;
        }

        // Forgets the elements (a HUD that went away must not be kept alive through them).
        void DropRegions()
        {
            for (int i = 0; i < count; i++) regions[i] = null;
            count = 0;
        }

        void Scan()
        {
            DropRegions();
            VisualElement root = document.rootVisualElement;
            if (root == null || root.panel == null) return;
            Walk(root, 0);
            if (count > 0) MeasurePanel();
        }

        // Depth first through the hierarchy (not contentContainer: every real child), stopping at
        // each placed region. Index access only: no enumerator, no query, no allocation.
        void Walk(VisualElement e, int depth)
        {
            if (depth > MaxDepth || count >= Max) return;
            var children = e.hierarchy;
            int n = children.childCount;
            for (int i = 0; i < n; i++)
            {
                if (!(children[i].userData is UiPlace)) continue;
                if (IsCorner(e)) regions[count++] = e;
                return;   // a placed region: its content holds no other region of the HUD
            }
            for (int i = 0; i < n && count < Max; i++) Walk(children[i], depth + 1);
        }

        // UiPlace anchors with left and top in percent; the full-cover overlays use pixels.
        static bool IsCorner(VisualElement e)
        {
            StyleLength left = e.style.left, top = e.style.top;
            if (left.keyword != StyleKeyword.Undefined || top.keyword != StyleKeyword.Undefined) return false;
            Length x = left.value, y = top.value;
            if (x.unit != LengthUnit.Percent || y.unit != LengthUnit.Percent) return false;
            return AtEdge(x.value) && AtEdge(y.value);
        }

        static bool AtEdge(float percent) => percent <= EdgePercent || percent >= 100f - EdgePercent;

        // The HUD panel scales with the screen: its units to OnGUI pixels, from two screen points.
        void MeasurePanel()
        {
            screenW = Screen.width;
            screenH = Screen.height;
            VisualElement root = document != null ? document.rootVisualElement : null;
            IPanel panel = root != null ? root.panel : null;
            if (panel == null) return;
            Vector2 a = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 b = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenW, screenH));
            float w = b.x - a.x, h = b.y - a.y;
            if (w < 1e-3f || h < 1e-3f) return;
            panelOrigin = a;
            pixelsPerUnit = new Vector2(screenW / w, screenH / h);
        }
    }
}
