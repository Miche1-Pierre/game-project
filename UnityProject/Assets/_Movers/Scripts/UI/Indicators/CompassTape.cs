using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // The compass tape at the top of one player's view: a wooden plank (compass_tape_bg), a tick
    // every 15 degrees (compass_tick), N E S O (W in English) in Fredoka, a notch in the middle
    // where the player looks, and the targets' tokens riding along it (added by the view).
    //
    // Retained: the 24 ticks and 4 letters are made once, one per world heading, and slide with
    // style.translate as the player turns; the lane clips them at the plank's ends, where they
    // also fade. Nothing is created or laid out per frame.
    public sealed class CompassTape
    {
        public readonly VisualElement root;

        // The lane (where ticks and tokens ride) stops this share of the plank's height short of
        // each end; a token parked at the end of the lane sits that far inside the plank.
        public const float LaneInsetShare = 0.45f;

        const int TickCount = 24;          // one per 15 degrees of world heading
        const float FadeDegrees = 22f;     // ticks fade out over the last degrees of the arc

        readonly IndicatorSettings s;
        readonly IndicatorArt art;
        readonly VisualElement background;
        readonly IndicatorShape plank;
        readonly VisualElement lane;
        readonly VisualElement[] ticks = new VisualElement[TickCount];
        readonly Label[] letters = new Label[4];
        readonly IndicatorShape notch;

        readonly float[] tickX = new float[TickCount];
        readonly float[] tickAlpha = new float[TickCount];
        readonly float[] tickW = new float[TickCount];
        readonly float[] tickH = new float[TickCount];

        float width = -1f, height, k = -1f, laneX, laneWidth, perDegree, arc;
        float nextLetters;
        Rect placed = new Rect(float.NaN, float.NaN, 0f, 0f);

        public float Width => width;
        public float Height => height;
        public float Arc => arc;

        public CompassTape(IndicatorArt art, IndicatorSettings settings)
        {
            this.art = art;
            s = settings;
            root = IndicatorUi.Box("compass-tape");

            if (art != null && art.tapeBackground != null)
            {
                background = IndicatorUi.Fill(IndicatorUi.Picture("plank", art.tapeBackground, 0.5f));
                root.Add(background);
            }
            else
            {
                plank = IndicatorUi.Fill(new IndicatorShape(IndicatorShape.Shape.Plank) { name = "plank" });
                plank.Set(s.wood, s.woodDark, 2f);
                root.Add(plank);
            }

            lane = IndicatorUi.Box("lane");
            lane.style.overflow = Overflow.Hidden;
            root.Add(lane);

            Font font = art != null ? art.semiBold : null;
            for (int i = 0; i < TickCount; i++)
            {
                if (i % 6 == 0)
                {
                    int q = i / 6;
                    var l = IndicatorUi.Text("letter-" + q, font, q == 0 ? s.north : s.cream, s.woodDark);
                    l.usageHints = UsageHints.DynamicTransform;
                    letters[q] = l;
                    ticks[i] = l;
                }
                else
                {
                    VisualElement t = art != null && art.tick != null
                        ? IndicatorUi.Picture("tick-" + i, art.tick, 1f)
                        : IndicatorUi.Box("tick-" + i);
                    if (art == null || art.tick == null)
                    {
                        Color c = s.cream;
                        c.a = i % 3 == 0 ? 0.85f : 0.55f;
                        t.style.backgroundColor = c;
                    }
                    t.usageHints = UsageHints.DynamicTransform;
                    ticks[i] = t;
                }
                lane.Add(ticks[i]);
                tickX[i] = float.NaN;
                tickAlpha[i] = -1f;
            }

            notch = new IndicatorShape(IndicatorShape.Shape.Notch) { name = "notch" };
            notch.style.position = Position.Absolute;
            root.Add(notch);
            RefreshLetters(force: true);
        }

        // The plank's box in its view (view-local panel units) and the view's scale. On a layout
        // change only (a new split, a new resolution).
        public void Layout(Rect rect, float viewScale)
        {
            if (rect == placed && Mathf.Approximately(viewScale, k)) return;
            placed = rect;
            width = rect.width;
            height = rect.height;
            k = viewScale;
            root.style.left = rect.x;
            root.style.top = rect.y;
            IndicatorUi.Size(root, width, height);
            if (background != null) background.style.unitySliceScale = art != null && art.artDensity > 0f ? k / art.artDensity : 0.5f;
            if (plank != null) plank.Set(s.wood, s.woodDark, Mathf.Max(1.5f, 2.2f * k));

            // The lane stops short of the plank's chamfered ends.
            float inset = height * LaneInsetShare;
            laneX = inset;
            laneWidth = Mathf.Max(10f, width - 2f * inset);
            lane.style.left = laneX;
            lane.style.top = 0f;
            IndicatorUi.Size(lane, laneWidth, height);
            arc = Mathf.Clamp(s.tapeArcDegrees, 30f, 360f);
            perDegree = laneWidth / arc;

            float letterSize = Mathf.Max(9f, s.letterFontSize * k * Mathf.Clamp(height / (s.tapeHeight * k), 0.7f, 1.3f));
            for (int i = 0; i < TickCount; i++)
            {
                VisualElement t = ticks[i];
                if (i % 6 == 0)
                {
                    tickW[i] = height * 1.6f;
                    tickH[i] = height;
                    t.style.fontSize = letterSize;
                    t.style.unityTextOutlineWidth = Mathf.Max(0.5f, 1.3f * k);
                }
                else
                {
                    bool major = i % 3 == 0;   // every 45 degrees
                    tickW[i] = Mathf.Max(1.5f, (art != null && art.tick != null ? 4f : 2f) * k);
                    tickH[i] = height * (major ? 0.42f : 0.26f);
                }
                IndicatorUi.Size(t, tickW[i], tickH[i]);
                tickX[i] = float.NaN;
            }

            float n = height * 0.5f;
            notch.style.left = width * 0.5f - n * 0.5f;
            notch.style.top = -n * 0.42f;
            IndicatorUi.Size(notch, n, n * 0.8f);
            notch.Set(s.cream, s.woodDark, Mathf.Max(1.2f, 1.6f * k));
        }

        // Where a token sits for a target this many degrees right (+) or left (-) of the view,
        // in the tape's space. Past the end of the arc it is parked at the end: it is behind.
        public float TokenX(float degrees, out bool clamped)
        {
            float half = arc * 0.5f;
            clamped = Mathf.Abs(degrees) > half;
            float d = clamped ? Mathf.Sign(degrees) * half : degrees;
            return laneX + laneWidth * 0.5f + d * perDegree;
        }

        public float CenterY => height * 0.5f;

        // Slides the ticks and letters under the player's heading. Per frame; no allocation.
        public void Tick(float heading)
        {
            if (width <= 0f) return;
            // Unscaled: the language is changed from the pause menu, where the game clock stands still.
            if (Time.unscaledTime >= nextLetters) RefreshLetters(force: false);
            float half = arc * 0.5f;
            float mid = laneWidth * 0.5f;
            for (int i = 0; i < TickCount; i++)
            {
                float d = Mathf.DeltaAngle(heading, i * 15f);
                float x = mid + d * perDegree;
                float a = Mathf.Clamp01((half - Mathf.Abs(d)) / FadeDegrees);
                if (Mathf.Abs(x - tickX[i]) >= 0.1f)
                {
                    tickX[i] = x;
                    // Ticks stand on the lower part of the plank; letters fill its height.
                    float y = i % 6 == 0 ? 0f : height * 0.78f - tickH[i];
                    ticks[i].style.translate = new Translate(x - tickW[i] * 0.5f, y);
                }
                if (Mathf.Abs(a - tickAlpha[i]) >= 0.02f)
                {
                    tickAlpha[i] = a;
                    ticks[i].style.opacity = a;
                }
            }
        }

        // The letters come from the game's table (the language can change in the options). Once
        // a second is plenty and costs nothing when nothing changed.
        void RefreshLetters(bool force)
        {
            nextLetters = Time.unscaledTime + 1f;
            for (int q = 0; q < 4; q++)
            {
                string t = IndicatorText.Cardinal(q);
                if (force || !ReferenceEquals(t, letters[q].text)) letters[q].text = t;
            }
        }
    }
}
