using System;
using System.Collections.Generic;

namespace Movers.AudioSynth
{
    // A tiny formant voice: a buzzing "glottis" (a band-limited sawtooth, tilted warm) and a
    // breath noise, shaped by three moving resonances (the formants that make an "a" an "a"),
    // plus hiss for s / sh / f and small bursts for p / t / k.
    //
    // It does not try to be intelligible. It is the Animal Crossing idea: every syllable of the
    // written line becomes one quick, pitched syllable of babble, so the rhythm, the length and
    // the melody of the sentence are there (a question rises, an exclamation jumps, "..."
    // trails off, CAPITALS shout) while the words stay on the speech bubble. What she means is
    // read; how she feels is heard.

    public enum Phone : byte
    {
        Silence, A, E, I, O, U, Y, Schwa,
        M, N, L, R, W, H,
        S, Z, SH, J, F, V,
        P, B, T, D, K, G,
        Click,
    }

    public enum VoiceMood { Calm, Annoyed, Angry, Police }

    public struct Seg
    {
        public Phone phone;
        public float seconds;
        public float pitchFrom, pitchTo;   // multiples of the voice's base pitch
        public float amp;
        public float press;                // 0 relaxed .. 1 strained (brighter, buzzier)
        public float rough;                // 0 clean .. 1 gravel (a burp, a growl)

        public Seg(Phone phone, float seconds, float pitchFrom, float pitchTo, float amp = 1f, float press = 0f, float rough = 0f)
        {
            this.phone = phone;
            this.seconds = seconds;
            this.pitchFrom = pitchFrom;
            this.pitchTo = pitchTo;
            this.amp = amp;
            this.press = press;
            this.rough = rough;
        }
    }

    public sealed class VoiceProfile
    {
        public int rate = 32000;
        public float f0 = 205f;              // base pitch, Hz
        public float formantScale = 1f;      // 1 = an adult woman, about 0.86 = a man
        public float bandwidthScale = 1.3f;  // wider formants: older, softer, breathier
        public float breath = 0.22f;         // aspiration mixed into every voiced sound
        public float tiltHz = 1100f;         // the glottal buzz is low-passed here: lower is warmer
        public float tremorHz = 5.6f;        // an old voice trembles a little...
        public float tremorPitch = 0.025f;   // ...in pitch
        public float tremorAmp = 0.12f;      // ...and in loudness
        public float jitter = 0.012f;        // period to period pitch wobble
        public float shimmer = 0.06f;        // period to period loudness wobble
        public float brightnessHz = 5200f;   // final gentle low-pass
        public float speed = 1f;             // multiplies every duration (lower is faster)

        // The grandmother: an old, warm, slightly quavering voice, pitched a little under a
        // young woman's. Sweet when calm; the moods push her up and sharpen her.
        public static VoiceProfile Grandma() => new VoiceProfile();

        // The crew only grunts: lifting, throwing, falling, a satisfied "ahh", a burp. Each
        // player gets a different pitch, so you can tell who just dropped the fridge.
        public static VoiceProfile Crew(int index)
        {
            float[] pitches = { 118f, 148f, 104f, 132f };
            return new VoiceProfile
            {
                f0 = pitches[Math.Abs(index) % pitches.Length],
                formantScale = 0.86f,
                bandwidthScale = 1.15f,
                breath = 0.3f,
                tiltHz = 900f,
                tremorPitch = 0.004f,
                tremorAmp = 0.03f,
                jitter = 0.018f,
                shimmer = 0.08f,
                brightnessHz = 4800f,
            };
        }

        // The voice on her television: a brisk presenter, filtered later to sound like a set.
        public static VoiceProfile Presenter() => new VoiceProfile
        {
            f0 = 128f,
            formantScale = 0.9f,
            bandwidthScale = 1.0f,
            breath = 0.12f,
            tiltHz = 1500f,
            tremorPitch = 0.003f,
            tremorAmp = 0.02f,
            jitter = 0.008f,
            shimmer = 0.04f,
            speed = 0.8f,
        };
    }

    public static class VoiceSynth
    {
        // Formant targets (Hz) and relative gains of F1..F3, for a woman's voice.
        struct Shape
        {
            public float f1, f2, f3, g1, g2, g3, voice, breath, fricHz, fricQ, fric, burstHz;
            public bool nasal, plosive, vowel;
        }

        static Shape V(float f1, float f2, float f3) => new Shape { f1 = f1, f2 = f2, f3 = f3, g1 = 1f, g2 = 0.6f, g3 = 0.3f, voice = 1f, vowel = true };

        static Shape ShapeOf(Phone p, Shape next)
        {
            switch (p)
            {
                case Phone.A: return V(850f, 1400f, 2800f);
                case Phone.E: return V(560f, 1950f, 2800f);
                case Phone.I: return V(380f, 2550f, 3200f);
                case Phone.O: return V(520f, 960f, 2750f);
                case Phone.U: return V(390f, 860f, 2650f);
                case Phone.Y: return V(370f, 1850f, 2550f);
                case Phone.Schwa: return V(620f, 1500f, 2700f);
                case Phone.M: return new Shape { f1 = 280f, f2 = 1150f, f3 = 2500f, g1 = 1f, g2 = 0.15f, g3 = 0.06f, voice = 0.75f, nasal = true };
                case Phone.N: return new Shape { f1 = 290f, f2 = 1600f, f3 = 2600f, g1 = 1f, g2 = 0.18f, g3 = 0.08f, voice = 0.75f, nasal = true };
                case Phone.L: return new Shape { f1 = 400f, f2 = 1150f, f3 = 2750f, g1 = 1f, g2 = 0.45f, g3 = 0.25f, voice = 0.8f };
                case Phone.R: return new Shape { f1 = 520f, f2 = 1300f, f3 = 2300f, g1 = 1f, g2 = 0.5f, g3 = 0.2f, voice = 0.7f, breath = 0.25f };
                case Phone.W: return new Shape { f1 = 330f, f2 = 740f, f3 = 2500f, g1 = 1f, g2 = 0.5f, g3 = 0.2f, voice = 0.85f };
                case Phone.H:
                {
                    // Breath through the shape of the vowel that follows: "ha" and "hi" differ.
                    var h = next;
                    h.voice = 0f;
                    h.breath = 1.1f;
                    h.vowel = false;
                    return h;
                }
                case Phone.S: return new Shape { f1 = next.f1, f2 = next.f2, f3 = next.f3, fricHz = 6200f, fricQ = 2.2f, fric = 0.55f };
                case Phone.Z: return new Shape { f1 = 300f, f2 = 1600f, f3 = 2600f, g1 = 1f, g2 = 0.2f, g3 = 0.1f, voice = 0.45f, fricHz = 6000f, fricQ = 2f, fric = 0.35f };
                case Phone.SH: return new Shape { f1 = next.f1, f2 = next.f2, f3 = next.f3, fricHz = 3000f, fricQ = 1.6f, fric = 0.6f };
                case Phone.J: return new Shape { f1 = 300f, f2 = 1700f, f3 = 2500f, g1 = 1f, g2 = 0.2f, g3 = 0.1f, voice = 0.45f, fricHz = 2900f, fricQ = 1.6f, fric = 0.4f };
                case Phone.F: return new Shape { f1 = next.f1, f2 = next.f2, f3 = next.f3, fricHz = 5200f, fricQ = 0.7f, fric = 0.22f };
                case Phone.V: return new Shape { f1 = 300f, f2 = 1400f, f3 = 2500f, g1 = 1f, g2 = 0.2f, g3 = 0.1f, voice = 0.5f, fricHz = 5000f, fricQ = 0.7f, fric = 0.15f };
                case Phone.P: return Stop(next, 900f, false);
                case Phone.B: return Stop(next, 900f, true);
                case Phone.T: return Stop(next, 4200f, false);
                case Phone.D: return Stop(next, 4000f, true);
                case Phone.K: return Stop(next, 2100f, false);
                case Phone.G: return Stop(next, 2000f, true);
                case Phone.Click: return new Shape { f1 = next.f1, f2 = next.f2, f3 = next.f3, plosive = true, burstHz = 3300f };
                default: { var s = next; s.voice = 0f; s.breath = 0f; s.fric = 0f; s.vowel = false; return s; }
            }
        }

        static Shape Stop(Shape next, float burstHz, bool voiced)
        {
            var s = next;
            s.vowel = false;
            s.voice = voiced ? 0.25f : 0f;
            s.breath = 0f;
            s.fric = 0f;
            s.plosive = true;
            s.burstHz = burstHz;
            return s;
        }

        static readonly Shape Neutral = V(620f, 1500f, 2700f);

        // Renders a list of segments to samples at the profile's rate, peak about 0.9.
        public static float[] Render(IReadOnlyList<Seg> segs, VoiceProfile v, uint seed)
        {
            int rate = v.rate;
            var rng = new Rng(seed);

            // Shapes, with each consonant knowing the vowel after it (coarticulation).
            int n = segs.Count;
            var shapes = new Shape[n];
            Shape following = Neutral;
            for (int i = n - 1; i >= 0; i--)
            {
                shapes[i] = ShapeOf(segs[i].phone, following);
                if (shapes[i].vowel || segs[i].phone == Phone.M || segs[i].phone == Phone.N || segs[i].phone == Phone.L) following = shapes[i];
            }

            float total = 0.06f;   // a breath of tail for the last release
            for (int i = 0; i < n; i++) total += segs[i].seconds * v.speed;
            var d = new float[Math.Max(1, (int)(total * rate))];

            var f1 = new Svf(); var f2 = new Svf(); var f3 = new Svf();
            var tilt = new OnePole(); tilt.Set(v.tiltHz, rate);
            var nasalLp = new OnePole(); nasalLp.Set(450f, rate);
            var bright = new OnePole(); bright.Set(v.brightnessHz, rate);
            var fric = new Biquad();
            var burst = new Biquad();
            var dcIn = 0f; var dcOut = 0f;

            // Smoothed parameters: formants glide (15 ms), loudness moves faster (6 ms).
            float kF = 1f - MathF.Exp(-1f / (0.015f * rate));
            float kA = 1f - MathF.Exp(-1f / (0.006f * rate));
            Shape first = n > 0 ? shapes[0] : Neutral;
            float cf1 = first.f1, cf2 = first.f2, cf3 = first.f3;
            float cg1 = 1f, cg2 = 0.6f, cg3 = 0.3f;
            float cVoice = 0f, cBreath = 0f, cFric = 0f, cNasal = 0f, cPress = 0f;
            float phase = 0f, periodScale = 1f, periodAmp = 1f;
            float tremorPhase = rng.Value * 6.28f;
            bool oddPeriod = false;
            int pos = 0;
            int coefCountdown = 0;

            for (int si = 0; si < n; si++)
            {
                Seg seg = segs[si];
                Shape sh = shapes[si];
                int len = Math.Max(1, (int)(seg.seconds * v.speed * rate));
                float fs = v.formantScale;
                float bwScale = v.bandwidthScale;
                if (sh.fric > 0f) fric.SetBandPass(sh.fricHz, sh.fricQ, rate);
                if (sh.plosive) burst.SetBandPass(sh.burstHz, 1.2f, rate);
                int burstAt = (int)(len * (seg.phone == Phone.Click ? 0.1f : 0.62f));
                float segBreath = sh.breath + v.breath * (sh.voice > 0f ? 1f : 0f);

                for (int i = 0; i < len && pos < d.Length; i++, pos++)
                {
                    float x = i / (float)len;
                    float t = pos / (float)rate;

                    // Targets, with a short fade at the segment edges handled by the smoothing.
                    float tv = sh.voice * seg.amp;
                    float tb = segBreath * seg.amp;
                    float tf = sh.fric * seg.amp;
                    if (sh.plosive)
                    {
                        // Closure: silence (or a murmur for b/d/g), then the burst. The next
                        // vowel's own smoothing brings the voice back in after it.
                        tf = 0f;
                        if (i >= burstAt) tv = 0f;
                    }
                    cf1 += kF * (sh.f1 * fs - cf1);
                    cf2 += kF * (sh.f2 * fs - cf2);
                    cf3 += kF * (sh.f3 * fs - cf3);
                    cg1 += kF * (sh.g1 - cg1);
                    cg2 += kF * (sh.g2 - cg2);
                    cg3 += kF * (sh.g3 - cg3);
                    cVoice += kA * (tv - cVoice);
                    cBreath += kA * (tb - cBreath);
                    cFric += kA * (tf - cFric);
                    cNasal += kF * ((sh.nasal ? 1f : 0f) - cNasal);
                    cPress += kF * (seg.press - cPress);

                    if (--coefCountdown <= 0)
                    {
                        coefCountdown = 32;
                        f1.Set(cf1, cf1 / (90f * bwScale), rate);
                        f2.Set(cf2, cf2 / (120f * bwScale), rate);
                        f3.Set(cf3, cf3 / (170f * bwScale), rate);
                    }

                    // Pitch: the segment's glide, the tremor of an old voice, and per-period jitter.
                    float glide = seg.pitchFrom + (seg.pitchTo - seg.pitchFrom) * Env.Smooth(x);
                    tremorPhase += 2f * MathF.PI * v.tremorHz / rate;
                    float tremor = MathF.Sin(tremorPhase);
                    float hz = v.f0 * glide * (1f + v.tremorPitch * tremor) * periodScale;
                    float dt = hz / rate;
                    phase += dt;
                    if (phase >= 1f)
                    {
                        phase -= 1f;
                        float rough = seg.rough;
                        periodScale = 1f + (v.jitter + rough * 0.25f) * rng.Bipolar;
                        periodAmp = 1f + (v.shimmer + rough * 0.5f) * rng.Bipolar;
                        oddPeriod = !oddPeriod;
                        if (rough > 0f && oddPeriod) periodAmp *= 1f - rough * 0.7f;   // subharmonic gravel
                    }
                    float saw = 2f * phase - 1f - PolyBlep(phase, dt);
                    float warm = tilt.Low(saw) * 2.2f;
                    float src = (warm * (1f - cPress * 0.6f) + saw * cPress * 0.45f) * periodAmp;
                    float noise = rng.Bipolar;
                    float loud = 1f + v.tremorAmp * tremor;

                    float excite = src * cVoice * loud + noise * cBreath * 0.35f;
                    float y = cg1 * f1.Band(excite) + cg2 * f2.Band(excite) + cg3 * f3.Band(excite);
                    // Nasals: the mouth is shut, the sound is a low hum through the nose.
                    if (cNasal > 0.01f) y = y * (1f - 0.6f * cNasal) + nasalLp.Low(src * cVoice) * 0.9f * cNasal;
                    if (cFric > 0.001f) y += fric.Process(noise) * cFric;
                    if (sh.plosive && i >= burstAt)
                    {
                        float bt = (i - burstAt) / (float)rate;
                        y += burst.Process(noise) * Env.AD(bt, 0.0006f, 0.009f) * 0.9f * seg.amp;
                    }
                    d[pos] = y;
                }
            }

            // Warm top end, DC blocked, gently saturated when strained, then normalised.
            for (int i = 0; i < d.Length; i++)
            {
                float y = bright.Low(d[i]);
                float dc = y - dcIn + 0.995f * dcOut;
                dcIn = y;
                dcOut = dc;
                d[i] = dc;
            }
            Buf.Finish(d, 1f, rate, 0.02f);
            for (int i = 0; i < d.Length; i++) d[i] = Buf.SoftClip(d[i] * 1.2f) * 0.9f / MathF.Tanh(1.2f);
            Buf.FadeIn(d, 0.003f, rate);
            return d;
        }

        static float PolyBlep(float t, float dt)
        {
            if (dt <= 0f) return 0f;
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }
    }

    // How a mood colours the babble.
    public struct MoodStyle
    {
        public float speed;      // duration multiplier (lower is faster)
        public float pitch;      // base pitch multiplier
        public float range;      // how far the melody wanders, as a fraction of the pitch
        public float loud;
        public float press;
        public float singSong;   // the sweet up-and-down lilt of a calm grandmother

        public static MoodStyle For(VoiceMood m)
        {
            switch (m)
            {
                case VoiceMood.Annoyed: return new MoodStyle { speed = 0.88f, pitch = 1.05f, range = 0.09f, loud = 1.05f, press = 0.3f, singSong = 0.1f };
                case VoiceMood.Angry: return new MoodStyle { speed = 0.76f, pitch = 1.17f, range = 0.16f, loud = 1.2f, press = 0.65f, singSong = 0f };
                case VoiceMood.Police: return new MoodStyle { speed = 0.72f, pitch = 1.12f, range = 0.2f, loud = 1.15f, press = 0.5f, singSong = 0f };
                default: return new MoodStyle { speed = 1f, pitch = 1f, range = 0.11f, loud = 1f, press = 0.1f, singSong = 0.45f };
            }
        }
    }

    // Written line to babble. One quick syllable per written syllable, with a melody from the
    // punctuation and the mood.
    public static class Babble
    {
        struct Syl
        {
            public List<Phone> onset;
            public Phone vowel;
            public Phone coda;
            public bool stressed, shout, function;
        }

        struct Sentence { public char end; public bool trailing; public int first, count; }

        // Everything a line needs: the segments, ready for VoiceSynth.Render. maxSeconds keeps
        // the voice inside the speech bubble's time; a long line talks faster, never longer.
        public static List<Seg> FromText(string text, VoiceMood mood, bool french, uint seed, float maxSeconds = 4.5f)
        {
            var style = MoodStyle.For(mood);
            var rng = new Rng(seed);
            var syls = new List<Syl>();
            var sentences = new List<Sentence>();
            var pauses = new Dictionary<int, float>();   // pause before syllable index
            Parse(text ?? "", french, syls, sentences, pauses);

            var segs = new List<Seg>();
            if (syls.Count == 0) return segs;

            float prevPitch = style.pitch;
            float lilt = rng.Range(0f, 6.28f);
            for (int s = 0; s < sentences.Count; s++)
            {
                Sentence sen = sentences[s];
                for (int k = 0; k < sen.count; k++)
                {
                    int idx = sen.first + k;
                    Syl syl = syls[idx];
                    if (pauses.TryGetValue(idx, out float pause) && pause > 0f)
                        segs.Add(new Seg(Phone.Silence, pause, prevPitch, prevPitch, 0f));

                    float progress = sen.count > 1 ? k / (float)(sen.count - 1) : 1f;
                    float r = style.range;
                    // Declination: a sentence starts a little high and settles.
                    float p = 1f + r * (0.3f - 0.6f * progress);
                    p += r * style.singSong * MathF.Sin(k * 1.35f + lilt);
                    if (syl.stressed) p += r * 0.6f;
                    if (syl.function) p -= r * 0.25f;
                    p += r * 0.15f * rng.Bipolar;

                    int fromEnd = sen.count - 1 - k;
                    float lengthen = 1f;
                    float amp = style.loud;
                    if (sen.end == '?')
                    {
                        if (fromEnd == 1) p += r * 1.0f;
                        if (fromEnd == 0) { p += r * 2.4f; lengthen = 1.5f; }
                    }
                    else if (sen.end == '!')
                    {
                        if (k == 0 || syl.stressed) p += r * 1.1f;
                        amp *= 1.15f;
                        if (fromEnd == 0) { p -= r * 0.4f; lengthen = 1.3f; }
                    }
                    else if (sen.trailing)
                    {
                        if (fromEnd <= 1) { p -= r * 1.0f; lengthen = 1.6f; amp *= 0.8f; }
                    }
                    else if (fromEnd == 0)
                    {
                        p -= r * 0.9f;
                        lengthen = 1.5f;
                    }
                    if (syl.shout) { p *= 1.22f; amp *= 1.3f; }
                    p *= style.pitch;

                    float press = Math.Clamp(style.press + (syl.shout ? 0.3f : 0f), 0f, 1f);
                    float jitterDur = rng.Range(0.88f, 1.12f);
                    float sp = style.speed * jitterDur;

                    for (int o = 0; o < syl.onset.Count; o++)
                        segs.Add(new Seg(syl.onset[o], ConsonantSeconds(syl.onset[o]) * sp, prevPitch, prevPitch, amp, press));

                    float vowelSec = (syl.function ? 0.07f : 0.088f) * (syl.stressed ? 1.25f : 1f) * lengthen * sp;
                    // The vowel glides from where the voice was to the new note, and droops a
                    // little at its end, as speech does.
                    segs.Add(new Seg(syl.vowel, vowelSec, prevPitch * 0.6f + p * 0.4f, p * 0.97f, amp, press));
                    if (syl.coda != Phone.Silence)
                        segs.Add(new Seg(syl.coda, ConsonantSeconds(syl.coda) * 0.8f * sp, p * 0.97f, p * 0.95f, amp * 0.8f, press));
                    prevPitch = p;
                }
            }

            // A long line speaks faster rather than outlasting its bubble.
            float total = 0f;
            for (int i = 0; i < segs.Count; i++) total += segs[i].seconds;
            if (total > maxSeconds && total > 0f)
            {
                float k = maxSeconds / total;
                for (int i = 0; i < segs.Count; i++)
                {
                    var sg = segs[i];
                    sg.seconds *= k;
                    segs[i] = sg;
                }
            }
            return segs;
        }

        static float ConsonantSeconds(Phone p)
        {
            switch (p)
            {
                case Phone.P: case Phone.T: case Phone.K: return 0.05f;
                case Phone.B: case Phone.D: case Phone.G: return 0.042f;
                case Phone.S: case Phone.SH: return 0.07f;
                case Phone.Z: case Phone.J: case Phone.F: case Phone.V: return 0.06f;
                case Phone.H: return 0.05f;
                case Phone.M: case Phone.N: return 0.05f;
                case Phone.L: case Phone.R: case Phone.W: return 0.04f;
                default: return 0.04f;
            }
        }

        // ---- text to syllables ----

        static void Parse(string text, bool french, List<Syl> syls, List<Sentence> sentences, Dictionary<int, float> pauses)
        {
            int sentenceStart = 0;
            int i = 0;
            float pendingPause = 0f;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsLetter(c))
                {
                    int j = i;
                    while (j < text.Length && (char.IsLetter(text[j]) || text[j] == '\'')) j++;
                    string word = text.Substring(i, j - i);
                    int before = syls.Count;
                    Word(word, french, syls);
                    if (syls.Count > before)
                    {
                        pauses[before] = (pauses.TryGetValue(before, out float p0) ? p0 : 0f) + Math.Max(pendingPause, before > sentenceStart ? 0.02f : 0f);
                        pendingPause = 0f;
                    }
                    i = j;
                    continue;
                }
                if (c == ',' || c == ';' || c == ':') pendingPause = Math.Max(pendingPause, 0.14f);
                if (c == '.' || c == '!' || c == '?' || c == '…')
                {
                    bool trailing = c == '…' || (c == '.' && i + 1 < text.Length && text[i + 1] == '.');
                    int k = i;
                    char end = c;
                    while (k < text.Length && (text[k] == '.' || text[k] == '!' || text[k] == '?' || text[k] == '…'))
                    {
                        if (text[k] == '?') end = '?';
                        else if (text[k] == '!' && end != '?') end = '!';
                        k++;
                    }
                    CloseSentence(syls, sentences, ref sentenceStart, end, trailing);
                    pendingPause = Math.Max(pendingPause, trailing ? 0.3f : 0.24f);
                    i = k;
                    continue;
                }
                i++;
            }
            CloseSentence(syls, sentences, ref sentenceStart, '.', false);
        }

        static void CloseSentence(List<Syl> syls, List<Sentence> sentences, ref int start, char end, bool trailing)
        {
            int count = syls.Count - start;
            if (count <= 0) return;
            sentences.Add(new Sentence { end = end, trailing = trailing, first = start, count = count });
            start = syls.Count;
        }

        static void Word(string raw, bool french, List<Syl> syls)
        {
            bool shout = raw.Length >= 2;
            for (int k = 0; k < raw.Length; k++) if (char.IsLetter(raw[k]) && !char.IsUpper(raw[k])) { shout = false; break; }
            string w = raw.ToLowerInvariant();
            var phones = new List<Phone>(w.Length);
            var isVowel = new List<bool>(w.Length);
            ToPhones(w, french, phones, isVowel);

            // Silent endings: French drops most final consonants and the final e; English
            // drops a final e after a consonant ("late", "house").
            while (phones.Count > 1 && !isVowel[phones.Count - 1] && french && IsSilentFinal(phones[phones.Count - 1]))
            {
                phones.RemoveAt(phones.Count - 1);
                isVowel.RemoveAt(isVowel.Count - 1);
            }
            if (phones.Count > 2 && isVowel[phones.Count - 1] && phones[phones.Count - 1] == Phone.Schwa && !isVowel[phones.Count - 2])
            {
                phones.RemoveAt(phones.Count - 1);
                isVowel.RemoveAt(isVowel.Count - 1);
            }

            int first = syls.Count;
            var onset = new List<Phone>(2);
            for (int p = 0; p < phones.Count; p++)
            {
                if (!isVowel[p])
                {
                    onset.Add(phones[p]);
                    continue;
                }
                // Two vowels in a row (a diphthong the table did not catch) are one syllable.
                while (p + 1 < phones.Count && isVowel[p + 1]) p++;
                if (onset.Count > 2) onset.RemoveRange(0, onset.Count - 2);
                syls.Add(new Syl { onset = new List<Phone>(onset), vowel = phones[p], coda = Phone.Silence, shout = shout });
                onset.Clear();
            }
            if (syls.Count == first)
            {
                // No vowel at all ("hm", "pff", "tsk"): hum it.
                if (onset.Count == 0) return;
                syls.Add(new Syl { onset = new List<Phone>(), vowel = onset.Contains(Phone.M) ? Phone.M : Phone.Schwa, coda = Phone.Silence, shout = shout });
                onset.Clear();
            }
            else if (onset.Count > 0)
            {
                var last = syls[syls.Count - 1];
                last.coda = onset[0];
                syls[syls.Count - 1] = last;
            }

            int count = syls.Count - first;
            bool function = raw.Length <= 3;
            for (int k = first; k < syls.Count; k++)
            {
                var s = syls[k];
                s.function = function;
                // English stresses early, French at the end of the word.
                s.stressed = !function && count > 0 && (french ? k == syls.Count - 1 : k == first);
                syls[k] = s;
            }
        }

        static bool IsSilentFinal(Phone p) =>
            p == Phone.S || p == Phone.T || p == Phone.D || p == Phone.Z || p == Phone.P;

        static void ToPhones(string w, bool french, List<Phone> ph, List<bool> vowel)
        {
            int i = 0;
            while (i < w.Length)
            {
                char c = Fold(w[i]);
                char n1 = i + 1 < w.Length ? Fold(w[i + 1]) : '\0';
                char n2 = i + 2 < w.Length ? Fold(w[i + 2]) : '\0';

                if (c == 'e' && n1 == 'a' && n2 == 'u') { Add(ph, vowel, Phone.O, true); i += 3; continue; }
                if (c == 'o' && n1 == 'u') { Add(ph, vowel, Phone.U, true); i += 2; continue; }
                if (c == 'o' && n1 == 'o') { Add(ph, vowel, Phone.U, true); i += 2; continue; }
                if (c == 'o' && n1 == 'i') { Add(ph, vowel, Phone.W, false); Add(ph, vowel, Phone.A, true); i += 2; continue; }
                if (c == 'a' && (n1 == 'i' || n1 == 'y')) { Add(ph, vowel, Phone.E, true); i += 2; continue; }
                if (c == 'e' && n1 == 'i') { Add(ph, vowel, Phone.E, true); i += 2; continue; }
                if (c == 'a' && n1 == 'u') { Add(ph, vowel, Phone.O, true); i += 2; continue; }
                if (c == 'e' && (n1 == 'e' || n1 == 'a')) { Add(ph, vowel, Phone.I, true); i += 2; continue; }
                if (c == 'c' && n1 == 'h') { Add(ph, vowel, Phone.SH, false); i += 2; continue; }
                if (c == 's' && n1 == 'h') { Add(ph, vowel, Phone.SH, false); i += 2; continue; }
                if (c == 'p' && n1 == 'h') { Add(ph, vowel, Phone.F, false); i += 2; continue; }
                if (c == 't' && n1 == 'h') { Add(ph, vowel, Phone.T, false); i += 2; continue; }
                if (c == 'q' && n1 == 'u') { Add(ph, vowel, Phone.K, false); i += 2; continue; }
                if (c == 'g' && n1 == 'n') { Add(ph, vowel, Phone.N, false); i += 2; continue; }
                if (c == 'n' && n1 == 'g') { Add(ph, vowel, Phone.N, false); i += 2; continue; }
                if (c == n1 && !IsVowelLetter(c)) { i++; continue; }   // doubled consonant: one sound

                switch (c)
                {
                    case 'a': Add(ph, vowel, Phone.A, true); break;
                    case 'e':
                        // An accented e is a real vowel; a bare one is often a schwa.
                        Add(ph, vowel, IsAccentedE(w[i]) ? Phone.E : (french ? Phone.Schwa : Phone.E), true);
                        break;
                    case 'i': Add(ph, vowel, Phone.I, true); break;
                    case 'y': Add(ph, vowel, Phone.I, true); break;
                    case 'o': Add(ph, vowel, Phone.O, true); break;
                    case 'u': Add(ph, vowel, french ? Phone.Y : Phone.Schwa, true); break;
                    case 'b': Add(ph, vowel, Phone.B, false); break;
                    case 'c': Add(ph, vowel, n1 == 'e' || n1 == 'i' || n1 == 'y' ? Phone.S : Phone.K, false); break;
                    case 'd': Add(ph, vowel, Phone.D, false); break;
                    case 'f': Add(ph, vowel, Phone.F, false); break;
                    case 'g': Add(ph, vowel, n1 == 'e' || n1 == 'i' ? Phone.J : Phone.G, false); break;
                    case 'h': if (!french) Add(ph, vowel, Phone.H, false); break;
                    case 'j': Add(ph, vowel, Phone.J, false); break;
                    case 'k': Add(ph, vowel, Phone.K, false); break;
                    case 'l': Add(ph, vowel, Phone.L, false); break;
                    case 'm': Add(ph, vowel, Phone.M, false); break;
                    case 'n': Add(ph, vowel, Phone.N, false); break;
                    case 'p': Add(ph, vowel, Phone.P, false); break;
                    case 'q': Add(ph, vowel, Phone.K, false); break;
                    case 'r': Add(ph, vowel, Phone.R, false); break;
                    case 's':
                        bool between = i > 0 && IsVowelLetter(Fold(w[i - 1])) && IsVowelLetter(n1);
                        Add(ph, vowel, between ? Phone.Z : Phone.S, false);
                        break;
                    case 't': Add(ph, vowel, Phone.T, false); break;
                    case 'v': Add(ph, vowel, Phone.V, false); break;
                    case 'w': Add(ph, vowel, Phone.W, false); break;
                    case 'x': Add(ph, vowel, Phone.K, false); Add(ph, vowel, Phone.S, false); break;
                    case 'z': Add(ph, vowel, Phone.Z, false); break;
                    default:
                        if (w[i] == 'ç') Add(ph, vowel, Phone.S, false);   // c cedilla
                        break;
                }
                i++;
            }
        }

        static void Add(List<Phone> ph, List<bool> vowel, Phone p, bool isVowel)
        {
            ph.Add(p);
            vowel.Add(isVowel);
        }

        static bool IsVowelLetter(char c) => c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' || c == 'y';

        static bool IsAccentedE(char c) => c == 'é' || c == 'è' || c == 'ê' || c == 'ë';

        // Accented letters to their base letter (French lines).
        static char Fold(char c)
        {
            switch (c)
            {
                case 'à': case 'â': case 'ä': return 'a';
                case 'é': case 'è': case 'ê': case 'ë': return 'e';
                case 'î': case 'ï': return 'i';
                case 'ô': case 'ö': return 'o';
                case 'ù': case 'û': case 'ü': return 'u';
                case 'œ': return 'e';
                default: return c;
            }
        }

        // ---- non-verbal sounds, as segments ----

        public static List<Seg> Effort(EffortKind kind, Rng r)
        {
            float v = r.Range(0.95f, 1.06f);
            var s = new List<Seg>();
            switch (kind)
            {
                case EffortKind.Lift:
                    // "hnnnGH": a strained hum that breaks open.
                    s.Add(new Seg(Phone.H, 0.04f, 1.2f * v, 1.2f * v, 0.5f));
                    s.Add(new Seg(Phone.N, 0.16f, 1.28f * v, 1.42f * v, 0.8f, 0.8f));
                    s.Add(new Seg(Phone.Schwa, 0.2f, 1.45f * v, 1.3f * v, 1f, 0.9f, 0.15f));
                    s.Add(new Seg(Phone.H, 0.14f, 1.2f * v, 1.1f * v, 0.55f));
                    break;
                case EffortKind.Throw:
                    // "HUP!"
                    s.Add(new Seg(Phone.H, 0.035f, 1.3f * v, 1.3f * v, 0.7f));
                    s.Add(new Seg(Phone.Schwa, 0.1f, 1.35f * v, 1.18f * v, 1f, 0.6f));
                    s.Add(new Seg(Phone.P, 0.05f, 1.15f * v, 1.15f * v, 0.6f));
                    break;
                case EffortKind.Oof:
                    s.Add(new Seg(Phone.H, 0.03f, 1.3f * v, 1.3f * v, 0.8f));
                    s.Add(new Seg(Phone.U, 0.17f, 1.4f * v, 0.92f * v, 1f, 0.5f, 0.1f));
                    s.Add(new Seg(Phone.F, 0.09f, 0.9f * v, 0.9f * v, 0.5f));
                    break;
                case EffortKind.Whoa:
                    s.Add(new Seg(Phone.W, 0.08f, 1.1f * v, 1.35f * v, 0.9f, 0.3f));
                    s.Add(new Seg(Phone.O, 0.32f, 1.45f * v, 1.0f * v, 1f, 0.5f));
                    s.Add(new Seg(Phone.A, 0.14f, 1.0f * v, 0.85f * v, 0.7f, 0.2f));
                    break;
                case EffortKind.Ahh:
                    s.Add(new Seg(Phone.H, 0.07f, 1.05f * v, 1.05f * v, 0.5f));
                    s.Add(new Seg(Phone.A, 0.5f, 1.08f * v, 0.82f * v, 0.75f));
                    s.Add(new Seg(Phone.H, 0.12f, 0.8f * v, 0.8f * v, 0.3f));
                    break;
                case EffortKind.Burp:
                    s.Add(new Seg(Phone.O, 0.1f, 0.62f * v, 0.55f * v, 0.9f, 0.7f, 0.9f));
                    s.Add(new Seg(Phone.A, 0.34f, 0.55f * v, 0.47f * v, 1f, 0.8f, 1f));
                    s.Add(new Seg(Phone.Schwa, 0.1f, 0.47f * v, 0.42f * v, 0.5f, 0.5f, 1f));
                    break;
                case EffortKind.Sigh:
                    s.Add(new Seg(Phone.H, 0.12f, 1.1f * v, 1.1f * v, 0.45f));
                    s.Add(new Seg(Phone.A, 0.42f, 1.1f * v, 0.82f * v, 0.5f));
                    s.Add(new Seg(Phone.H, 0.2f, 0.8f * v, 0.8f * v, 0.25f));
                    break;
                case EffortKind.Hmm:
                    s.Add(new Seg(Phone.M, 0.18f, 1.0f * v, 1.08f * v, 0.7f));
                    s.Add(new Seg(Phone.M, 0.28f, 1.1f * v, 0.93f * v, 0.7f));
                    break;
                case EffortKind.Tsk:
                    s.Add(new Seg(Phone.Click, 0.12f, 1f, 1f, 0.8f));
                    s.Add(new Seg(Phone.Click, 0.12f, 1f, 1f, 0.7f));
                    s.Add(new Seg(Phone.M, 0.22f, 1.05f * v, 0.9f * v, 0.5f));
                    break;
                case EffortKind.Gasp:
                    s.Add(new Seg(Phone.H, 0.2f, 1.3f * v, 1.3f * v, 0.9f));
                    s.Add(new Seg(Phone.O, 0.12f, 1.5f * v, 1.6f * v, 0.6f, 0.2f));
                    break;
            }
            return s;
        }

        // A little tune on closed lips, for when she is busy and content: five to eight notes
        // of a major pentatonic, legato, falling home at the end.
        public static List<Seg> Hum(Rng r)
        {
            int[] scale = { 0, 2, 4, 7, 9, 12 };
            int notes = r.Range(5, 9);
            int deg = r.Range(0, 3);
            var s = new List<Seg>();
            float prev = 1f;
            for (int i = 0; i < notes; i++)
            {
                if (i == notes - 1) deg = 0;
                else deg = Math.Clamp(deg + r.Range(-2, 3), 0, scale.Length - 1);
                float p = MathF.Pow(2f, scale[deg] / 12f) * 0.95f;
                float len = r.Chance(0.3f) ? 0.42f : 0.24f;
                if (i == notes - 1) len = 0.6f;
                s.Add(new Seg(Phone.M, len, prev, p, 0.65f));
                prev = p;
            }
            return s;
        }
    }

    public enum EffortKind { Lift, Throw, Oof, Whoa, Ahh, Burp, Sigh, Hmm, Tsk, Gasp }

    // The seed of every voice clip, in one place: the game (VoiceBank) and the offline
    // preview renderer (tools/render) both take them from here, so a preview is the exact
    // clip the game plays, not a cousin of it.
    public static class VoiceSeeds
    {
        // A line of hers. The same text always babbles the same way, on every machine and
        // every run (string.GetHashCode is randomised per process on .NET Core and is not
        // the same on Mono). FNV-1a.
        public static uint Line(string text)
        {
            uint h = 2166136261u;
            if (text != null)
                for (int i = 0; i < text.Length; i++) { h ^= text[i]; h *= 16777619u; }
            return h == 0 ? 1u : h;
        }

        // A crew effort: one per player, kind and take (VoiceBank.Takes of each).
        public static uint CrewEffort(int player, int kind, int take) => (uint)(player * 7919 + kind * 131 + take * 17 + 1);

        // Her sighs, hmms, tsks and gasps.
        public static uint GrandmaEffort(int kind) => (uint)(4001 + kind);

        // Her hums: the tune from Hum, the voice's own breath and tremor from Hum + 1.
        public static uint Hum(int index) => (uint)(5001 + index * 3);
    }
}
