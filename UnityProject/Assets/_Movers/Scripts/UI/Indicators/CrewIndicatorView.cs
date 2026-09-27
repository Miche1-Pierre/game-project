using System.Collections.Generic;
using TargetIndicators;
using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // One player's view: its compass tape, and a token per target in view (a ring) or out of it
    // (an edge arrow), all inside that player's viewport, so two views side by side never draw
    // into each other.
    //
    // The Target Indicators package gives each view its compass: one TargetIndicatorManager per
    // player, BoundaryType.CompassTape, tracking every target but the player himself. Its forward
    // reference is a helper turned to the view's heading only (CompassForwardReferenceOverride),
    // so looking at your feet does not spin the tape. The manager is disabled and pumped here,
    // after every camera has moved, so the tape and the rings agree within the frame.
    //
    // UICORE's HUD draws over the markers, so they keep out from under its corner panels, as
    // measured each frame (HudCorners): the tape is only as wide as the room between the contract
    // card and the grandmother's panel, and the edge tokens slide round every corner panel.
    public sealed class CrewIndicatorView
    {
        public readonly CrewMember owner;

        const float PulseAmplitude = 0.12f;   // a beating token grows by up to this share
        const int HangingTokens = 3;          // parked tape tokens that may hang at one end

        sealed class Slot
        {
            public IndicatorTarget target;
            public TargetIndicatorId id;
            public bool tracked;
            public IndicatorToken tapeToken;
            public IndicatorToken viewToken;
            public IndicatorSnapshot snap;
        }

        readonly IndicatorArt art;
        readonly IndicatorSettings s;
        readonly TargetIndicatorManager compass;
        readonly Transform heading;
        readonly VisualElement container;
        readonly CompassTape tape;
        readonly List<Slot> slots = new List<Slot>(8);

        // The HUD's corner panels in this view (OnGUI pixels), and the same grown by a token's
        // reach: where no token centre goes. Filled each frame, never reallocated.
        readonly Rect[] hudRects = new Rect[HudCorners.Max];
        readonly Rect[] keepRects = new Rect[HudCorners.Max];
        int hudCount;
        IndicatorPlacement.Keepout keep;

        Rect gui = new Rect(float.NaN, float.NaN, 0f, 0f);   // the viewport in OnGUI pixels
        Rect tapeGui;
        Vector2 panelOrigin;
        float toPanel = 1f, k = 1f, tapeTokenPanel = 26f;
        float tapeTop, tapeHeight, tapeBandBottom, tapeOverhang, tapeRoom = -1f, keepMargin;
        int parkedLeft, parkedRight;
        int syncedVersion = -1;
        bool shown = true, laidOutOnPanel;
        IndicatorPlacement.Ellipse ellipse;

        public Rect ViewRect => gui;
        public Rect TapeRect => tapeGui;
        public bool Shown => shown;

        public CrewIndicatorView(CrewMember owner, Transform parent, VisualElement layer, IndicatorArt art, IndicatorSettings settings)
        {
            this.owner = owner;
            this.art = art;
            s = settings;

            // Built inactive so the manager's Awake finds its camera already set: it warns when
            // it cannot find one by itself.
            var go = new GameObject("Compass " + owner.DisplayName);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            heading = new GameObject("Heading").transform;
            heading.SetParent(go.transform, false);
            compass = go.AddComponent<TargetIndicatorManager>();
            compass.Camera = owner.View;
            compass.BoundaryType = BoundaryType.CompassTape;
            compass.CompassForwardReferenceOverride = heading;
            compass.CalculateLookAtDot = false;
            compass.enabled = false;
            go.SetActive(true);

            container = IndicatorUi.Box("view-" + owner.DisplayName);
            container.style.overflow = Overflow.Hidden;   // a marker never spills into the other view
            tape = new CompassTape(art, s);
            container.Add(tape.root);
            layer.Add(container);
            keep.rects = keepRects;
        }

        // ---- targets ----

        // Adds and removes tokens when the target list changed. Allocates, but only then.
        public void Sync(IndicatorTargets targets)
        {
            if (targets.Version == syncedVersion) return;
            syncedVersion = targets.Version;
            var all = targets.All;

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                Slot slot = slots[i];
                bool keep = false;
                for (int j = 0; j < all.Count && !keep; j++) keep = all[j] == slot.target;
                if (keep) continue;
                Drop(slot);
                slots.RemoveAt(i);
            }

            for (int j = 0; j < all.Count; j++)
            {
                IndicatorTarget t = all[j];
                if (t.member == owner || Has(t)) continue;
                var slot = new Slot { target = t };
                slot.tapeToken = new IndicatorToken("tape-" + t.kind, art, s, withLabel: false);
                slot.viewToken = new IndicatorToken("marker-" + t.kind, art, s, withLabel: true);
                tape.root.Add(slot.tapeToken.root);
                container.Add(slot.viewToken.root);
                slot.tapeToken.SetVisible(false);
                slot.viewToken.SetVisible(false);
                if (t.root != null && compass.TryAddTarget(t.root, out TargetIndicator added))
                {
                    slot.id = added.Id;
                    slot.tracked = true;
                }
                slots.Add(slot);
            }

            // Drawing order follows the target list (board, grandmother, crew): the crew on top.
            for (int j = 0; j < all.Count; j++)
            {
                Slot slot = Find(all[j]);
                if (slot == null) continue;
                slot.tapeToken.root.BringToFront();
                slot.viewToken.root.BringToFront();
            }
            gui = new Rect(float.NaN, float.NaN, 0f, 0f);   // size the new tokens
        }

        bool Has(IndicatorTarget t) => Find(t) != null;

        Slot Find(IndicatorTarget t)
        {
            for (int i = 0; i < slots.Count; i++) if (slots[i].target == t) return slots[i];
            return null;
        }

        void Drop(Slot slot)
        {
            if (slot.tracked && compass != null) compass.TryRemoveTarget(slot.id);
            slot.tapeToken.root.RemoveFromHierarchy();
            slot.viewToken.root.RemoveFromHierarchy();
        }

        public void Dispose()
        {
            for (int i = 0; i < slots.Count; i++) Drop(slots[i]);
            slots.Clear();
            container.RemoveFromHierarchy();
            if (compass != null) Object.Destroy(compass.gameObject);
        }

        // ---- per frame ----

        // hud: the HUD's measured corner panels, or null to ignore the HUD.
        public void Tick(HudCorners hud, bool markersOn, float time)
        {
            Camera cam = owner != null ? owner.View : null;
            if (!CrewView.TryGetRect(cam, out Rect view))
            {
                SetShown(false);   // a solo layout (F2): this view is not on screen
                return;
            }
            SetShown(true);
            if (view != gui || (!laidOutOnPanel && container.panel != null)) Layout(view);
            KeepClearOf(hud);

            var frame = IndicatorPlacement.Frame.Of(cam, gui);
            heading.SetPositionAndRotation(frame.position, Quaternion.LookRotation(frame.FlatForward, Vector3.up));
            if (compass.Camera != cam) compass.Camera = cam;
            compass.GetChanges();
            if (s.showTape) tape.Tick(frame.Heading);

            Vector3 eyes = owner.EyePosition;
            parkedLeft = parkedRight = 0;
            bool driving = owner.IsDriving;
            bool carrying = owner.Held != null;
            for (int i = 0; i < slots.Count; i++)
                TickSlot(slots[i], frame, eyes, driving, carrying, markersOn, time);
        }

        void TickSlot(Slot slot, in IndicatorPlacement.Frame frame, Vector3 eyes, bool driving, bool carrying, bool markersOn, float time)
        {
            IndicatorTarget t = slot.target;
            ref IndicatorSnapshot snap = ref slot.snap;
            snap.kind = t.kind;
            snap.partnerIndex = t.kind == IndicatorKind.Partner ? t.number : -1;
            // The board rides on the truck: its driver needs no arrow to it.
            bool visible = t.alive && !(t.kind == IndicatorKind.Deliver && driving);
            if (!visible)
            {
                slot.tapeToken.SetVisible(false);
                slot.viewToken.SetVisible(false);
                snap.onTape = false;
                snap.mode = MarkerMode.Hidden;
                return;
            }

            float distance = Vector3.Distance(eyes, t.anchor);
            float rise = t.level - eyes.y;
            int caret = Mathf.Abs(rise) > s.floorCaretHeight ? (rise > 0f ? 1 : -1) : 0;
            float pulse = t.pulse > 0f ? 1f + PulseAmplitude * t.pulse *(0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f * 1.3f)) : 1f;
            snap.distance = distance;
            snap.color = t.color;
            snap.keysBadge = t.keys;

            // The tape: the package's compass pose, x in 0..1 over the full turn, 0.5 ahead.
            TargetIndicator ti = default;
            bool onTape = s.showTape && t.onTape && slot.tracked && compass.TryGetTargetIndicator(slot.id, out ti);
            snap.onTape = onTape;
            if (onTape)
            {
                float degrees = (ti.ScreenPose.position.x - 0.5f) * 360f;
                float x = tape.TokenX(degrees, out bool clamped);
                // Behind the player: parked at the end of the plank. Several parked at one end
                // hang one under the other, like tags on a nail, instead of hiding each other.
                float y = tape.CenterY;
                if (clamped) y += (degrees > 0f ? parkedRight++ : parkedLeft++) * tapeTokenPanel * 0.66f;
                IndicatorToken tok = slot.tapeToken;
                tok.SetLook(t.color, t.icon, t.number);
                tok.SetBadge(t.keys);
                tok.SetCaret(caret);
                tok.SetPointer(clamped, degrees > 0f ? 0f : 180f);
                tok.SetPosition(new Vector2(x, y));
                tok.SetOpacity(clamped ? 0.8f : 1f);
                tok.SetScale(pulse);
                tok.SetVisible(true);
                snap.tapeDegrees = degrees;
                snap.tapeClamped = clamped;
                snap.tapePosition = new Vector2(tapeGui.x + x / toPanel, tapeGui.y + y / toPanel);
            }
            else slot.tapeToken.SetVisible(false);

            // In the view: a ring over it, or a token at the edge pointing to it, never under the HUD.
            IndicatorToken v = slot.viewToken;
            IndicatorPlacement.Result place = IndicatorPlacement.Place(frame, t.anchor, ellipse, keep);
            bool allowed = markersOn && t.markers;
            MarkerMode mode = MarkerMode.Hidden;
            if (allowed && (place.onScreen || place.underHud))
            {
                // In view: the ring's rules. Under a HUD panel the ring would be drawn and never
                // seen, so the same ring becomes an edge token beside the panel.
                float closest = Mathf.Max(t.kind == IndicatorKind.Grandma ? s.grandmaRingMinDistance : s.ringMinDistance, t.noRingWithin);
                bool deliverRing = t.kind != IndicatorKind.Deliver || (t.ready && s.deliverRingInView);
                if (distance >= closest && distance <= s.ringMaxDistance && deliverRing)
                    mode = place.onScreen ? MarkerMode.Ring : MarkerMode.Edge;
            }
            else if (allowed && distance >= s.edgeMinDistance
                     && (t.kind != IndicatorKind.Deliver || t.ready || (s.deliverArrowWhenCarrying && carrying)))
                mode = MarkerMode.Edge;
            snap.mode = mode;
            snap.aroundHud = mode == MarkerMode.Edge && (place.moved || place.underHud);
            if (mode == MarkerMode.Hidden)
            {
                v.SetVisible(false);
                return;
            }

            v.SetLook(t.color, t.icon, t.number);
            v.SetBadge(t.keys);
            v.SetCaret(caret);
            v.SetLabel(IndicatorText.Metres(distance), mode == MarkerMode.Ring ? 90f : place.angle, true);
            Vector2 at;
            if (mode == MarkerMode.Ring)
            {
                float size = s.ringTokenSize * k;
                // Over the target, its arrow pointing down at it; never above the top of the view.
                at = place.gui + new Vector2(0f, -size * 0.95f);
                at.y = Mathf.Max(at.y, gui.y + size * 0.6f);
                v.SetPointer(true, 90f);
                // Fades out near the crosshair, so it never covers what the player aims at.
                float fromCentre = (place.gui - gui.center).magnitude / Mathf.Max(1f, gui.height);
                float fade = s.ringCrosshairFade <= 0f ? 1f : Mathf.InverseLerp(s.ringCrosshairFade * 0.5f, s.ringCrosshairFade, fromCentre);
                v.SetOpacity(fade);
                snap.arrowAngle = 90f;
            }
            else
            {
                at = place.edge;
                v.SetPointer(true, place.angle);
                v.SetOpacity(1f);
                snap.arrowAngle = place.angle;
            }
            // One size for both, laid out once: a ring is the edge token scaled down, which is a
            // transform, so a target crossing the edge of the view does not re-layout anything.
            float ringScale = mode == MarkerMode.Ring ? s.ringTokenSize / Mathf.Max(1f, s.edgeTokenSize) : 1f;
            v.SetPosition((at - gui.min) * toPanel);
            v.SetScale(pulse * ringScale);
            v.SetVisible(true);
            snap.position = at;
        }

        // ---- layout: on a new viewport only ----

        void Layout(Rect view)
        {
            gui = view;
            IPanel panel = container.panel;
            laidOutOnPanel = panel != null;
            Vector2 min = view.min, max = view.max;
            if (panel != null)
            {
                min = RuntimePanelUtils.ScreenToPanel(panel, view.min);
                max = RuntimePanelUtils.ScreenToPanel(panel, view.max);
            }
            panelOrigin = min;
            toPanel = view.width > 0f ? (max.x - min.x) / view.width : 1f;
            if (toPanel <= 0f || float.IsNaN(toPanel)) toPanel = 1f;
            k = Mathf.Clamp(view.height / 1080f, 0.6f, 1.6f);

            container.style.left = min.x;
            container.style.top = min.y;
            IndicatorUi.Size(container, max.x - min.x, max.y - min.y);

            // The tape: top centre. Its height and top are fixed here; its width follows the room
            // the HUD's top panels leave (LayoutTape, from KeepClearOf, right after).
            tapeTop = view.y + s.tapeTop * k;
            tapeHeight = s.tapeHeight * k;
            float tapeToken = s.tapeTokenSize * k;
            // Parked tokens hang under the tape's ends, and their arrows reach past them.
            tapeBandBottom = tapeTop + tapeHeight * 0.5f + (HangingTokens - 1) * tapeToken * 0.66f + tapeToken * 0.5f * (1f + PulseAmplitude);
            tapeOverhang = Mathf.Max(0f, IndicatorToken.Reach(s, tapeToken) * (1f + PulseAmplitude) - tapeHeight * CompassTape.LaneInsetShare);
            tape.root.style.display = s.showTape ? DisplayStyle.Flex : DisplayStyle.None;
            LayoutTape(view.width * 0.5f);

            // The edge ellipse keeps clear of the tape and the bottom band; the HUD's corner
            // panels are kept clear per frame (KeepClearOf), since they change size.
            float half = s.edgeTokenSize * k * 0.5f;
            float top = Mathf.Max(view.height * s.edgeTopInset, (tapeTop + tapeHeight - view.y) + half + 8f * k);
            float bottom = view.height * s.edgeBottomInset + half;
            float side = view.width * s.edgeSideInset + half;
            ellipse = IndicatorPlacement.EdgeEllipse(view, top, bottom, side);
            keepMargin = IndicatorToken.Reach(s, s.edgeTokenSize * k) * (1f + PulseAmplitude) + s.hudGap * k;

            tapeToken = tapeTokenPanel = s.tapeTokenSize * k * toPanel;
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].tapeToken.SetSize(tapeToken, k * toPanel);
                slots[i].viewToken.SetSize(s.edgeTokenSize * k * toPanel, k * toPanel);
            }
        }

        // ---- the HUD: per frame, laid out again only when its room changed ----

        // Reads where the HUD's corner panels are in this view, grows them into the keep-out the
        // edge tokens slide round, and narrows the tape to the room left between the top ones.
        void KeepClearOf(HudCorners hud)
        {
            hudCount = hud != null ? hud.Collect(gui, hudRects) : 0;
            for (int i = 0; i < hudCount; i++)
            {
                Rect r = hudRects[i];
                keepRects[i] = new Rect(r.x - keepMargin, r.y - keepMargin, r.width + 2f * keepMargin, r.height + 2f * keepMargin);
            }
            keep.count = hudCount;
            if (!s.showTape) return;
            float room = IndicatorPlacement.TapeHalfRoom(gui, gui.y, tapeBandBottom, hudRects, hudCount);
            if (Mathf.Abs(room - tapeRoom) > 0.5f) LayoutTape(room);
        }

        // The tape's width: the preferred share of the view, but never reaching under the HUD's
        // top panels (with room for a parked token's arrow past each end, and a gap), down to a
        // floor below which a very narrow view lets its ends go under them.
        void LayoutTape(float room)
        {
            tapeRoom = room;
            Rect view = gui;
            float cap = view.width - 24f * k;
            float w = Mathf.Min(Mathf.Clamp(view.width * s.tapeWidthShare, s.tapeMinWidth * k, s.tapeMaxWidth * k), cap);
            float fit = 2f * (room - s.hudGap * k - tapeOverhang);
            w = Mathf.Max(Mathf.Min(w, fit), Mathf.Min(s.tapeFloorWidth * k, cap));
            tapeGui = new Rect(view.center.x - w * 0.5f, tapeTop, w, tapeHeight);
            tape.Layout(new Rect((tapeGui.x - view.x) * toPanel, (tapeGui.y - view.y) * toPanel, w * toPanel, tapeHeight * toPanel), k * toPanel);
        }

        void SetShown(bool on)
        {
            if (on == shown) return;
            shown = on;
            container.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on)
                for (int i = 0; i < slots.Count; i++) slots[i].snap.mode = MarkerMode.Hidden;
        }

        // ---- for tests and debug ----

        // The HUD's corner panels this view keeps clear of, as last measured (OnGUI pixels).
        public int HudRects(List<Rect> into)
        {
            for (int i = 0; i < hudCount; i++) into.Add(hudRects[i]);
            return hudCount;
        }

        public int Snapshots(List<IndicatorSnapshot> into)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                IndicatorSnapshot snap = slot.snap;
                snap.drawnPosition = Drawn(slot.viewToken);
                snap.drawnTapePosition = Drawn(slot.tapeToken);
                into.Add(snap);
            }
            return slots.Count;
        }

        // Where the element really is on screen after the last layout, in OnGUI pixels.
        Vector2 Drawn(IndicatorToken token)
        {
            if (!token.Visible || token.root.panel == null) return new Vector2(float.NaN, float.NaN);
            Rect wb = token.root.worldBound;
            if (float.IsNaN(wb.x)) return new Vector2(float.NaN, float.NaN);
            return gui.min + (wb.center - panelOrigin) / toPanel;
        }
    }
}
