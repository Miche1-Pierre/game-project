using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    public enum MotionKind
    {
        PopIn,      // grows from 85% with a small overshoot: a toast, a bubble, a card
        DropIn,     // falls from above and settles: a sign hung on a nail
        Pulse,      // breathes forever: the police countdown, a ticking grenade
        Wobble,     // a short shake: something went wrong
        Sway,       // hangs and sways slowly forever: the title logo, a hanging sign
    }

    // A small animation on the element that contains it, played natively: the probe writes
    // the element's scale, translate and rotate from a scheduler, so the widget tree is never
    // rebuilt for it and nothing is allocated per frame. One-shot kinds play once when the
    // element appears; looping kinds run until it leaves the panel.
    //
    // Like every probe, one instance per kind is shared (UiMotion.Pop, ...), so rebuilding a
    // panel does not replay its entrance.
    public sealed class UiMotion : UiProbe
    {
        public static readonly UiMotion Pop = new UiMotion(MotionKind.PopIn, 0.28f);
        public static readonly UiMotion Drop = new UiMotion(MotionKind.DropIn, 0.45f);
        public static readonly UiMotion PulseSlow = new UiMotion(MotionKind.Pulse, 1.1f);
        public static readonly UiMotion PulseFast = new UiMotion(MotionKind.Pulse, 0.45f);
        public static readonly UiMotion Shake = new UiMotion(MotionKind.Wobble, 0.4f);
        public static readonly UiMotion Hang = new UiMotion(MotionKind.Sway, 3.2f);

        public readonly MotionKind kind;
        public readonly float seconds;

        public UiMotion(MotionKind kind, float seconds)
        {
            this.kind = kind;
            this.seconds = Mathf.Max(0.05f, seconds);
        }

        bool Loops => kind == MotionKind.Pulse || kind == MotionKind.Sway;

        protected override void Attached(VisualElement target)
        {
            // One runner per element and motion, kept on the element. Another motion arriving on
            // the same element (a new shake for a new mood) replaces the old runner.
            var run = target.userData as Run;
            if (run == null || run.Motion != this)
            {
                run?.Stop();
                if (target.userData != null && run == null) return;   // the element's userData is someone else's
                run = new Run(this, target);
                target.userData = run;
            }
            run.Start();
        }

        protected override void Detached(VisualElement target)
        {
            if (target.userData is Run run && run.Motion == this) run.Stop();
        }

        sealed class Run
        {
            readonly UiMotion motion;
            readonly VisualElement target;
            IVisualElementScheduledItem item;
            double startedAt;

            public UiMotion Motion => motion;

            public Run(UiMotion motion, VisualElement target)
            {
                this.motion = motion;
                this.target = target;
            }

            public void Start()
            {
                startedAt = Time.realtimeSinceStartupAsDouble;
                target.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(motion.kind == MotionKind.Sway ? 0f : 50f));
                Apply(0f);
                if (item == null) item = target.schedule.Execute(Tick).Every(16);
                else item.Resume();
            }

            public void Stop()
            {
                item?.Pause();
                Apply(1f);
            }

            void Tick()
            {
                float t = (float)((Time.realtimeSinceStartupAsDouble - startedAt) / motion.seconds);
                if (!motion.Loops && t >= 1f)
                {
                    Apply(1f);
                    item.Pause();
                    return;
                }
                Apply(motion.Loops ? t - Mathf.Floor(t) : t);
            }

            // t: 0..1 through the motion (or through one loop).
            void Apply(float t)
            {
                var s = target.style;
                switch (motion.kind)
                {
                    case MotionKind.PopIn:
                    {
                        float k = BackOut(Mathf.Clamp01(t));
                        float sc = Mathf.LerpUnclamped(0.85f, 1f, k);
                        s.scale = new Scale(new Vector3(sc, sc, 1f));
                        s.opacity = Mathf.Clamp01(t * 3f);
                        break;
                    }
                    case MotionKind.DropIn:
                    {
                        float k = BounceOut(Mathf.Clamp01(t));
                        s.translate = new Translate(0f, Mathf.LerpUnclamped(-40f, 0f, k));
                        s.opacity = Mathf.Clamp01(t * 4f);
                        break;
                    }
                    case MotionKind.Pulse:
                    {
                        float sc = 1f + 0.06f * Mathf.Sin(t * Mathf.PI * 2f);
                        s.scale = new Scale(new Vector3(sc, sc, 1f));
                        break;
                    }
                    case MotionKind.Wobble:
                    {
                        float fade = 1f - Mathf.Clamp01(t);
                        s.rotate = new Rotate(new Angle(Mathf.Sin(t * Mathf.PI * 6f) * 4f * fade));
                        break;
                    }
                    case MotionKind.Sway:
                    {
                        s.rotate = new Rotate(new Angle(Mathf.Sin(t * Mathf.PI * 2f) * 1.6f));
                        break;
                    }
                }
            }

            static float BackOut(float t)
            {
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float u = t - 1f;
                return 1f + c3 * u * u * u + c1 * u * u;
            }

            static float BounceOut(float t)
            {
                const float n1 = 7.5625f, d1 = 2.75f;
                if (t < 1f / d1) return n1 * t * t;
                if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                t -= 2.625f / d1;
                return n1 * t * t + 0.984375f;
            }
        }
    }
}
