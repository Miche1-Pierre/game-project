using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // One marker token, retained: built once, then only moved and restyled when something
    // changes. The same widget is the small token on the compass tape, the ring over a target in
    // view and the token at the edge of the view with its arrow.
    //
    //   disc     an octagon in the target's colour (IndicatorShape), under
    //   ring     indicator_ring, a frame (or the ring itself tinted, IndicatorSettings.ringIsFrame)
    //   icon     the grandmother by mood, the truck, the box, or the other player's number
    //   badge    a gold key: the grandmother still has the house keys (the intro)
    //   caret    up or down: the target is on another floor
    //   pointer  indicator_arrow outside the ring, turned towards the target
    //   label    the distance, on the side away from the arrow
    //
    // Per frame only style.translate, style.rotate, style.scale and opacity change, which move
    // and blend the element without a layout or a repaint; every setter skips unchanged values.
    public sealed class IndicatorToken
    {
        public readonly VisualElement root;

        readonly IndicatorArt art;
        readonly IndicatorSettings s;
        readonly IndicatorShape disc;
        readonly VisualElement ring;
        readonly VisualElement iconPicture;
        readonly IndicatorShape iconShape;
        readonly Label number;
        readonly VisualElement badge;
        readonly IndicatorShape caret;
        readonly VisualElement pointerPivot;
        readonly VisualElement pointerHead;
        readonly IndicatorShape pointerShape;
        readonly Label label;

        float size = -1f, k = -1f, labelFont = 12f;
        Color color = new Color(0f, 0f, 0f, 0f);
        IndicatorIcon icon = (IndicatorIcon)(-1);
        int numberIndex = -99;
        int caretDir = -99;
        bool badgeOn = true, pointerOn = true, visible = true;
        float pointerAngle = float.NaN, labelAngle = float.NaN, opacity = -1f, scale = -1f;
        Vector2 position = new Vector2(float.NaN, float.NaN);
        string labelText;

        public Vector2 Center => position;
        public bool Visible => visible;

        const float ArrowTuck = 0.35f;   // share of the arrow head tucked under the token's rim
        const float CaretClearance = 0.3f;   // extra label reach, in token sizes, while a caret shows

        // The arrow head's size for a token this size.
        public static float ArrowSize(IndicatorSettings s, float size) => Mathf.Max(6f, s.arrowSize * size / Mathf.Max(1f, s.edgeTokenSize));

        // How far a token this size reaches from its centre, arrow included: what must stay clear
        // of a HUD panel (the tape's parked ends, the edge tokens' keep-out margin).
        public static float Reach(IndicatorSettings s, float size) => size * 0.5f + (1f - ArrowTuck) * ArrowSize(s, size);

        public IndicatorToken(string name, IndicatorArt art, IndicatorSettings settings, bool withLabel)
        {
            this.art = art;
            s = settings;
            root = IndicatorUi.Box(name);
            root.usageHints = UsageHints.DynamicTransform;

            // The pointer first, so the token covers the arrow's root where they meet.
            pointerPivot = IndicatorUi.Fill(IndicatorUi.Box("pointer"));
            pointerPivot.usageHints = UsageHints.DynamicTransform;
            pointerPivot.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);
            if (art != null && art.arrow != null)
            {
                pointerHead = IndicatorUi.Picture("arrow", art.arrow, 1f);
                // Turned once so that it points right, like the drawn arrow; the pivot turns both.
                if (Mathf.Abs(art.arrowPointsTo) > 0.01f)
                {
                    pointerHead.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);
                    pointerHead.style.rotate = new Rotate(new Angle(-art.arrowPointsTo, AngleUnit.Degree));
                }
            }
            else
            {
                pointerShape = new IndicatorShape(IndicatorShape.Shape.Arrow) { name = "arrow" };
                pointerShape.style.position = Position.Absolute;
                pointerHead = pointerShape;
            }
            pointerPivot.Add(pointerHead);
            root.Add(pointerPivot);

            if (s.ringIsFrame || art == null || art.ring == null)
            {
                disc = IndicatorUi.Fill(new IndicatorShape(IndicatorShape.Shape.Octagon) { name = "disc" });
                root.Add(disc);
            }
            if (art != null && art.ring != null)
            {
                ring = IndicatorUi.Fill(IndicatorUi.Picture("ring", art.ring, 1f));
                root.Add(ring);
            }

            iconPicture = IndicatorUi.Box("icon");
            iconShape = new IndicatorShape(IndicatorShape.Shape.IconTruck) { name = "icon-shape" };
            iconShape.style.position = Position.Absolute;
            number = IndicatorUi.Fill(IndicatorUi.Text("number", art != null ? art.bold : null, s.cream, s.woodDark));
            root.Add(iconPicture);
            root.Add(iconShape);
            root.Add(number);

            badge = IndicatorUi.Box("badge");
            var badgeDisc = IndicatorUi.Fill(new IndicatorShape(IndicatorShape.Shape.Octagon) { name = "badge-disc" });
            badgeDisc.Set(s.keys, s.woodDark, 2f);
            badge.Add(badgeDisc);
            if (art != null && art.key != null)
            {
                var keyIcon = IndicatorUi.Picture("badge-key", art.key, 1f);
                keyIcon.style.left = Length.Percent(15f); keyIcon.style.top = Length.Percent(15f);
                keyIcon.style.width = Length.Percent(70f); keyIcon.style.height = Length.Percent(70f);
                badge.Add(keyIcon);
            }
            else
            {
                var keyIcon = new IndicatorShape(IndicatorShape.Shape.IconKey) { name = "badge-key" };
                keyIcon.style.position = Position.Absolute;
                keyIcon.style.left = Length.Percent(12f); keyIcon.style.top = Length.Percent(12f);
                keyIcon.style.width = Length.Percent(76f); keyIcon.style.height = Length.Percent(76f);
                keyIcon.Set(s.woodDark, s.keys, 0f);
                badge.Add(keyIcon);
            }
            root.Add(badge);

            caret = new IndicatorShape(IndicatorShape.Shape.Caret) { name = "caret" };
            caret.style.position = Position.Absolute;
            caret.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);
            root.Add(caret);

            if (withLabel)
            {
                label = IndicatorUi.Text("distance", art != null ? art.semiBold : null, s.cream, s.woodDark);
                label.usageHints = UsageHints.DynamicTransform;
                root.Add(label);
            }

            SetBadge(false);
            SetPointer(false, 0f);
            SetCaret(0);
        }

        // Sizes of every part: on creation and when the view's scale changes, never per frame.
        public void SetSize(float tokenSize, float viewScale)
        {
            if (Mathf.Approximately(tokenSize, size) && Mathf.Approximately(viewScale, k)) return;
            size = tokenSize;
            k = viewScale;
            IndicatorUi.Size(root, size, size);
            if (ring != null) ring.style.unitySliceScale = SliceScale();

            float iconSize = size * 0.64f;
            IndicatorUi.Place(iconPicture, size * 0.5f, size * 0.5f, iconSize, iconSize);
            IndicatorUi.Place(iconShape, size * 0.5f, size * 0.5f, iconSize, iconSize);
            number.style.fontSize = Mathf.Max(9f, size * 0.5f * s.numberFontSize / 18f);
            number.style.unityTextOutlineWidth = Mathf.Max(0.5f, 1.2f * k);

            float b = size * 0.5f;
            IndicatorUi.Place(badge, size * 0.86f, size * 0.14f, b, b);

            float c = size * 0.36f;
            IndicatorUi.Place(caret, size * 0.98f, size * 0.82f, c, c);
            caret.Set(s.cream, s.woodDark, Mathf.Max(1f, 1.4f * k));

            float a = ArrowSize(s, size);
            // The head starts just inside the ring's right edge and points out of it.
            pointerHead.style.left = size - a * ArrowTuck;
            pointerHead.style.top = (size - a) * 0.5f;
            IndicatorUi.Size(pointerHead, a, a);
            if (label != null)
            {
                float fs = labelFont = Mathf.Max(9f, s.distanceFontSize * k);
                label.style.fontSize = fs;
                label.style.unityTextOutlineWidth = Mathf.Max(0.5f, 1.3f * k);
                IndicatorUi.Size(label, size * 3f, fs * 1.4f);
                labelAngle = float.NaN;   // the offset depends on the size: place it again
            }
            ApplyLookColours();
        }

        // What the token shows: on a change of target state only (mood, readiness, the keys).
        public void SetLook(Color c, IndicatorIcon ic, int partnerIndex)
        {
            c = IndicatorUi.Opaque(c);
            bool colourChanged = c != color;
            color = c;
            if (ic != icon || partnerIndex != numberIndex)
            {
                icon = ic;
                numberIndex = partnerIndex;
                Sprite sp = art != null ? art.Icon(ic) : null;
                bool showNumber = ic == IndicatorIcon.Number;
                bool showPicture = !showNumber && sp != null;
                bool showShape = !showNumber && sp == null && IndicatorShape.HasFallback(ic, out IndicatorShape.Shape shape);
                if (showShape)
                {
                    IndicatorShape.HasFallback(ic, out shape);
                    iconShape.SetShape(shape);
                }
                IndicatorUi.SetPicture(iconPicture, showPicture ? sp : null, 1f);
                iconPicture.style.display = showPicture ? DisplayStyle.Flex : DisplayStyle.None;
                iconShape.style.display = showShape ? DisplayStyle.Flex : DisplayStyle.None;
                number.style.display = showNumber ? DisplayStyle.Flex : DisplayStyle.None;
                if (showNumber) number.text = IndicatorText.PlayerNumber(partnerIndex);
                colourChanged = true;
            }
            if (colourChanged) ApplyLookColours();
        }

        void ApplyLookColours()
        {
            if (size <= 0f) return;
            float rim = Rim;
            if (disc != null) disc.Set(color, s.woodDark, rim);
            if (ring != null) ring.style.unityBackgroundImageTintColor = disc != null ? Color.white : color;
            Color ink = IndicatorUi.InkOn(color, s);
            iconShape.Set(ink, color, 0f);
            number.style.color = ink;
            number.style.unityTextOutlineColor = ink == s.cream ? s.woodDark : s.cream;
            if (pointerShape != null) pointerShape.Set(color, s.woodDark, rim * 0.8f);
            else if (s.tintArrow) pointerHead.style.unityBackgroundImageTintColor = color;
        }

        // The dark outline of every part, in proportion to the token.
        float Rim => Mathf.Max(1.2f, size * 0.06f);

        float SliceScale() => art != null && art.artDensity > 0f ? k / art.artDensity : 0.5f;

        public void SetBadge(bool on)
        {
            if (on == badgeOn) return;
            badgeOn = on;
            badge.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // +1 up (a floor above), -1 down, 0 none.
        public void SetCaret(int dir)
        {
            if (dir == caretDir) return;
            caretDir = dir;
            caret.style.display = dir == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            caret.style.rotate = new Rotate(new Angle(dir < 0 ? 180f : 0f, AngleUnit.Degree));
            labelAngle = float.NaN;   // the label keeps clear of the caret: place it again
        }

        // The arrow outside the ring, turned to screen degrees (0 right, 90 down).
        public void SetPointer(bool on, float angle)
        {
            if (on != pointerOn)
            {
                pointerOn = on;
                pointerPivot.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!on || Mathf.Abs(Mathf.DeltaAngle(angle, pointerAngle)) < 0.5f) return;
            pointerAngle = angle;
            pointerPivot.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
        }

        // The distance, placed on the side of the token away from the arrow (below when there is
        // none), so the two never overlap whichever way the target lies.
        public void SetLabel(string text, float awayFromAngle, bool hasPointer)
        {
            if (label == null) return;
            if (!ReferenceEquals(text, labelText))
            {
                labelText = text;
                label.text = text;
                label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            float angle = hasPointer ? awayFromAngle + 180f : 90f;
            if (Mathf.Abs(Mathf.DeltaAngle(angle, labelAngle)) < 2f) return;
            labelAngle = angle;
            float rad = angle * Mathf.Deg2Rad;
            float fs = labelFont;
            // Far enough out that a label sitting sideways clears the token too, and the floor
            // caret on the token's lower right (a token pointing up-left puts its label there).
            float reach = size * 0.5f + fs * (0.75f + 0.9f * Mathf.Abs(Mathf.Cos(rad))) + (caretDir != 0 ? size * CaretClearance : 0f);
            var c = new Vector2(size * 0.5f + Mathf.Cos(rad) * reach, size * 0.5f + Mathf.Sin(rad) * reach);
            label.style.translate = new Translate(c.x - size * 1.5f, c.y - fs * 0.7f);
        }

        // Centre of the token in its parent's space.
        public void SetPosition(Vector2 center)
        {
            if (Mathf.Abs(center.x - position.x) < 0.1f && Mathf.Abs(center.y - position.y) < 0.1f) return;
            position = center;
            root.style.translate = new Translate(center.x - size * 0.5f, center.y - size * 0.5f);
        }

        public void SetScale(float s)
        {
            if (Mathf.Abs(s - scale) < 0.004f) return;
            scale = s;
            root.style.scale = new Scale(new Vector2(s, s));
        }

        public void SetOpacity(float a)
        {
            a = Mathf.Clamp01(a);
            if (Mathf.Abs(a - opacity) < 0.01f) return;
            opacity = a;
            root.style.opacity = a;
        }

        public void SetVisible(bool on)
        {
            if (on == visible) return;
            visible = on;
            root.style.visibility = on ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
