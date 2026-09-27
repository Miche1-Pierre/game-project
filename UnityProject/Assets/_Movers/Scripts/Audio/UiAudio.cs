using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // The interface's sounds, for the UI code to call. Wood and cardboard like the rest of the
    // game, taps on a crate rather than glassy beeps: a soft tick on hover, a knock on click,
    // a box flap opening and closing, a kalimba note for a toast, a clunk for "no", coins.
    //
    // Call them on the event (the pointer enters, the button is pressed, the toast appears).
    // They are also cheap and safe to call every frame, as the contract asks: each sound
    // remembers when it was last *called*, and plays only after a quiet gap. So a caller that
    // fires Hover every frame while the pointer sits on a button gets one tick when the stream
    // starts, not a buzz; the next tick needs the calls to stop for the gap first. A call in
    // the same frame as the last one, or the next, is always the same stream: at a low frame
    // rate (a loading hitch, 10 fps) the frames are longer than the gap, and the gap alone
    // would let every call through.
    //
    // Tick and Slider are the exception: they are meant to repeat (a countdown, a slider being
    // dragged), so they are rate-limited from the last time they *played* instead, a few per
    // second at most while the calls keep coming.
    //
    // Silent outside Play mode (UI Builder previews) and until the clips exist (the first
    // frame of the game).
    public static class UiAudio
    {
        enum Sound { Hover, Click, Open, Close, Error, Toast, Money, Confirm, Back, Tick, Slider, Count }

        static readonly float[] lastCalled = new float[(int)Sound.Count];
        static readonly int[] lastCalledFrame = new int[(int)Sound.Count];
        static readonly float[] lastPlayed = new float[(int)Sound.Count];
        static readonly float[] Gap = { 0.06f, 0.04f, 0.12f, 0.12f, 0.15f, 0.25f, 0.2f, 0.08f, 0.08f, 0.03f, 0.045f };

        const float Never = -99f;
        const int NeverFrame = -100;

        public static void Hover() { Play(Sound.Hover, SfxKind.UiHover, 0.35f, 0.04f); }
        public static void Click() { Play(Sound.Click, SfxKind.UiClick, 0.6f, 0.03f); }
        public static void Open() { Play(Sound.Open, SfxKind.UiOpen, 0.55f, 0f); }
        public static void Close() { Play(Sound.Close, SfxKind.UiClose, 0.5f, 0f); }
        public static void Error() { Play(Sound.Error, SfxKind.UiDenied, 0.6f, 0f); }
        public static void Toast() { Play(Sound.Toast, SfxKind.UiToast, 0.45f, 0f); }
        public static void Money() { Play(Sound.Money, SfxKind.UiCoins, 0.55f, 0.03f); }

        // Beyond the contract, for screens that want them.
        public static void Confirm() { Play(Sound.Confirm, SfxKind.UiConfirm, 0.55f, 0f); }
        public static void Back() { Play(Sound.Back, SfxKind.UiBack, 0.5f, 0f); }
        public static void Tick() { Play(Sound.Tick, SfxKind.UiTick, 0.4f, 0.05f); }
        public static void Slider() { Play(Sound.Slider, SfxKind.UiSlider, 0.35f, 0.08f); }

        static bool Repeats(Sound s) => s == Sound.Tick || s == Sound.Slider;

        static void Play(Sound s, SfxKind kind, float volume, float pitchJitter)
        {
            if (!Application.isPlaying) return;
            float now = Time.unscaledTime;
            int frame = Time.frameCount;
            int i = (int)s;
            bool repeats = Repeats(s);
            float since = now - (repeats ? lastPlayed[i] : lastCalled[i]);
            bool stream = !repeats && frame - lastCalledFrame[i] <= 1;
            lastCalled[i] = now;
            lastCalledFrame[i] = frame;
            if (stream || since < Gap[i]) return;
            lastPlayed[i] = now;
            float pitch = pitchJitter > 0f ? 1f + Random.Range(-pitchJitter, pitchJitter) : 1f;
            AudioDirector.Play2D(kind, SoundPreset.Ui, volume, pitch);
        }

        // Every Play session starts with nothing heard yet (with domain reload off the arrays
        // keep the last session's times, and zeros would swallow the first quarter second).
        // Play() does nothing outside Play mode, so this is always run before the first sound.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < lastCalled.Length; i++)
            {
                lastCalled[i] = lastPlayed[i] = Never;
                lastCalledFrame[i] = NeverFrame;
            }
        }
    }
}
