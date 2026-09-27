using System;

namespace Movers.AudioSynth
{
    // The building blocks every synthesised sound is made of. Plain C#, no UnityEngine: the
    // same code runs in the game (clips built at load) and in the offline preview renderer
    // (tools/render), so a WAV preview is exactly what the game will play.
    //
    // Everything is deterministic: a sound is a function of its recipe, its variant number and
    // its seed, so "the third wood step" is the same sound on both our machines.

    // xorshift32. A class, not a struct: a struct copied by accident would replay the same
    // numbers, and a recipe that repeats its noise sounds like a buzz.
    public sealed class Rng
    {
        uint s;

        public Rng(uint seed)
        {
            s = seed == 0u ? 0x9E3779B9u : seed;
            for (int i = 0; i < 4; i++) Next();
        }

        public Rng(int kind, int variant) : this((uint)(kind * 7919 + variant * 104729 + 17)) { }

        public uint Next()
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }

        public float Value => (Next() >> 8) * (1f / 16777216f);          // [0, 1)
        public float Bipolar => Value * 2f - 1f;                          // [-1, 1)
        public float Range(float a, float b) => a + (b - a) * Value;
        public int Range(int a, int bExclusive) => Math.Min(bExclusive - 1, a + (int)(Value * (bExclusive - a)));
        public bool Chance(float p) => Value < p;
    }

    // One-pole low-pass (and the high-pass that is its complement).
    public struct OnePole
    {
        float a, y;

        public static float Coef(float hz, int rate) => 1f - MathF.Exp(-2f * MathF.PI * Math.Max(1f, hz) / rate);
        public void Set(float hz, int rate) { a = Coef(hz, rate); }
        public float Low(float x) { y += a * (x - y); return y; }
        public float High(float x) { y += a * (x - y); return x - y; }
    }

    // RBJ cookbook biquad, transposed direct form II. Fixed settings: set once, run many.
    public struct Biquad
    {
        float b0, b1, b2, a1, a2, z1, z2;

        public static Biquad LowPass(float hz, float q, int rate) { var f = new Biquad(); f.SetLowPass(hz, q, rate); return f; }
        public static Biquad HighPass(float hz, float q, int rate) { var f = new Biquad(); f.SetHighPass(hz, q, rate); return f; }
        public static Biquad BandPass(float hz, float q, int rate) { var f = new Biquad(); f.SetBandPass(hz, q, rate); return f; }
        public static Biquad Peak(float hz, float q, float db, int rate) { var f = new Biquad(); f.SetPeak(hz, q, db, rate); return f; }

        public void SetLowPass(float hz, float q, int rate)
        {
            Prep(hz, q, rate, out float cs, out float alpha);
            Norm((1f - cs) * 0.5f, 1f - cs, (1f - cs) * 0.5f, 1f + alpha, -2f * cs, 1f - alpha);
        }

        public void SetHighPass(float hz, float q, int rate)
        {
            Prep(hz, q, rate, out float cs, out float alpha);
            Norm((1f + cs) * 0.5f, -(1f + cs), (1f + cs) * 0.5f, 1f + alpha, -2f * cs, 1f - alpha);
        }

        // Constant 0 dB peak gain: the centre frequency passes at unity whatever the Q.
        public void SetBandPass(float hz, float q, int rate)
        {
            Prep(hz, q, rate, out float cs, out float alpha);
            Norm(alpha, 0f, -alpha, 1f + alpha, -2f * cs, 1f - alpha);
        }

        public void SetPeak(float hz, float q, float db, int rate)
        {
            Prep(hz, q, rate, out float cs, out float alpha);
            float A = MathF.Pow(10f, db / 40f);
            Norm(1f + alpha * A, -2f * cs, 1f - alpha * A, 1f + alpha / A, -2f * cs, 1f - alpha / A);
        }

        static void Prep(float hz, float q, int rate, out float cs, out float alpha)
        {
            float w = 2f * MathF.PI * Math.Clamp(hz, 10f, rate * 0.45f) / rate;
            cs = MathF.Cos(w);
            alpha = MathF.Sin(w) / (2f * Math.Max(0.05f, q));
        }

        void Norm(float nb0, float nb1, float nb2, float na0, float na1, float na2)
        {
            b0 = nb0 / na0; b1 = nb1 / na0; b2 = nb2 / na0; a1 = na1 / na0; a2 = na2 / na0;
        }

        public float Process(float x)
        {
            float y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            return y;
        }
    }

    // Topology-preserving state variable filter (Zavalishin). Stable while its cutoff moves,
    // which the biquad is not: wind, creaks and the voice formants sweep with it.
    public struct Svf
    {
        float a1, a2, a3, k, ic1, ic2;

        public void Set(float hz, float q, int rate)
        {
            float g = MathF.Tan(MathF.PI * Math.Clamp(hz, 10f, rate * 0.45f) / rate);
            k = 1f / Math.Max(0.05f, q);
            a1 = 1f / (1f + g * (g + k));
            a2 = g * a1;
            a3 = g * a2;
        }

        public float Band(float x) { Tick(x, out _, out float b); return b; }
        public float Low(float x) { Tick(x, out float l, out _); return l; }

        public void Tick(float x, out float low, out float band)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            low = v2;
            band = v1;
        }
    }

    // Pink-ish noise (Paul Kellet's economy filter): wind, room tone and road rumble sound
    // natural with it, where white noise hisses.
    public struct Pink
    {
        float b0, b1, b2;

        public float Next(Rng r)
        {
            float w = r.Bipolar;
            b0 = 0.99765f * b0 + w * 0.0990460f;
            b1 = 0.96300f * b1 + w * 0.2965164f;
            b2 = 0.57000f * b2 + w * 1.0526913f;
            return (b0 + b1 + b2 + w * 0.1848f) * 0.2f;
        }
    }

    // A small mono Schroeder reverb (four combs, two all-passes, Freeverb's numbers scaled to
    // the rate). A room, not a hall: the music and the TV get a bit of air, nothing more.
    public sealed class TinyVerb
    {
        readonly float[][] comb;
        readonly int[] combPos;
        readonly float[] combLp;
        readonly float[][] ap;
        readonly int[] apPos;
        readonly float feedback, damp;

        public TinyVerb(int rate, float size = 0.8f, float damping = 0.35f)
        {
            int[] c = { 1116, 1188, 1277, 1356 };
            int[] a = { 556, 441 };
            float s = rate / 44100f;
            comb = new float[c.Length][];
            for (int i = 0; i < c.Length; i++) comb[i] = new float[Math.Max(8, (int)(c[i] * s))];
            combPos = new int[c.Length];
            combLp = new float[c.Length];
            ap = new float[a.Length][];
            for (int i = 0; i < a.Length; i++) ap[i] = new float[Math.Max(8, (int)(a[i] * s))];
            apPos = new int[a.Length];
            feedback = 0.70f + 0.28f * Math.Clamp(size, 0f, 1f);
            damp = Math.Clamp(damping, 0f, 1f);
        }

        public float Process(float x)
        {
            float input = x * 0.12f;
            float sum = 0f;
            for (int i = 0; i < comb.Length; i++)
            {
                float[] buf = comb[i];
                int p = combPos[i];
                float y = buf[p];
                combLp[i] = y * (1f - damp) + combLp[i] * damp;
                buf[p] = input + combLp[i] * feedback;
                combPos[i] = (p + 1) % buf.Length;
                sum += y;
            }
            for (int i = 0; i < ap.Length; i++)
            {
                float[] buf = ap[i];
                int p = apPos[i];
                float b = buf[p];
                float y = -sum + b;
                buf[p] = sum + b * 0.5f;
                apPos[i] = (p + 1) % buf.Length;
                sum = y;
            }
            return sum;
        }
    }

    public static class Env
    {
        // Linear attack, exponential decay (time constant `decay` seconds). 0 before t = 0.
        public static float AD(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / Math.Max(1e-5f, attack);
            return MathF.Exp(-(t - attack) / Math.Max(1e-5f, decay));
        }

        // Rises over `attack`, holds, falls over `release` to 0 at `length`.
        public static float ASR(float t, float length, float attack, float release)
        {
            if (t < 0f || t > length) return 0f;
            float a = attack > 0f ? Math.Min(1f, t / attack) : 1f;
            float r = release > 0f ? Math.Min(1f, (length - t) / release) : 1f;
            return Math.Min(a, r);
        }

        // Half-sine bump from 0 to 0 over `length`.
        public static float Bump(float t, float length)
        {
            if (t <= 0f || t >= length) return 0f;
            return MathF.Sin(MathF.PI * t / length);
        }

        public static float Smooth(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }

    public static class Buf
    {
        public static float[] Seconds(float seconds, int rate) => new float[Math.Max(1, (int)MathF.Ceiling(seconds * rate))];

        public static float Hz(float midi) => 440f * MathF.Pow(2f, (midi - 69f) / 12f);

        // Scales to a peak (which is what sets the mix between recipes), removes any DC
        // offset first, and fades the last few milliseconds so nothing ends on a click.
        public static float[] Finish(float[] d, float peak, int rate, float fadeSeconds = 0.005f)
        {
            double mean = 0;
            for (int i = 0; i < d.Length; i++) mean += d[i];
            float dc = (float)(mean / Math.Max(1, d.Length));
            float max = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                d[i] -= dc;
                float a = Math.Abs(d[i]);
                if (a > max) max = a;
            }
            float g = max > 1e-6f ? peak / max : 0f;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            FadeOut(d, fadeSeconds, rate);
            return d;
        }

        public static void FadeOut(float[] d, float seconds, int rate)
        {
            int n = Math.Min(d.Length, (int)(seconds * rate));
            for (int i = 0; i < n; i++) d[d.Length - 1 - i] *= i / (float)Math.Max(1, n);
        }

        public static void FadeIn(float[] d, float seconds, int rate)
        {
            int n = Math.Min(d.Length, (int)(seconds * rate));
            for (int i = 0; i < n; i++) d[i] *= i / (float)Math.Max(1, n);
        }

        // Adds src into dst at a sample offset, clipped to dst.
        public static void Mix(float[] dst, float[] src, int offset, float gain)
        {
            for (int i = 0; i < src.Length; i++)
            {
                int j = offset + i;
                if (j < 0) continue;
                if (j >= dst.Length) break;
                dst[j] += src[i] * gain;
            }
        }

        // For music: a render of one pass plus the tail that rings past its end. The tail is
        // added onto the start, which is exactly what the start sounds like when the loop comes
        // round (the last chord still ringing under the first), so the seam is not there at all.
        // interleaved: frames of `channels` samples.
        public static float[] WrapLoop(float[] longer, int loopFrames, int channels)
        {
            var d = new float[loopFrames * channels];
            Array.Copy(longer, d, d.Length);
            int tail = longer.Length - d.Length;
            for (int i = 0; i < tail; i++) d[i % d.Length] += longer[d.Length + i];
            return d;
        }

        // Turns a render longer than the loop into a seamless loop: the samples past the end
        // are crossfaded (equal power) into the start, so the wrap has no seam and no click.
        public static float[] Loop(float[] longer, int loopLength, int fade)
        {
            fade = Math.Min(fade, Math.Min(loopLength, longer.Length - loopLength));
            var d = new float[loopLength];
            Array.Copy(longer, d, loopLength);
            for (int i = 0; i < fade; i++)
            {
                float x = (i + 0.5f) / fade;
                float fin = MathF.Sin(x * MathF.PI * 0.5f);
                float fout = MathF.Cos(x * MathF.PI * 0.5f);
                d[i] = longer[i] * fin + longer[loopLength + i] * fout;
            }
            return d;
        }

        public static float SoftClip(float x) => MathF.Tanh(x);

        public static float Peak(float[] d)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) m = Math.Max(m, Math.Abs(d[i]));
            return m;
        }

        public static float Rms(float[] d)
        {
            double s = 0;
            for (int i = 0; i < d.Length; i++) s += d[i] * d[i];
            return (float)Math.Sqrt(s / Math.Max(1, d.Length));
        }
    }

    // Small sound generators most recipes are made of.
    public static class Gen
    {
        // Band-limited noise burst: white noise through a band-pass, under an AD envelope.
        public static void NoiseBurst(float[] d, int rate, Rng r, float at, float hz, float q,
                                      float attack, float decay, float gain)
        {
            var bp = Biquad.BandPass(hz, q, rate);
            int start = Math.Max(0, (int)(at * rate));
            int len = Math.Min(d.Length - start, (int)((attack + decay * 7f) * rate));
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)rate;
                d[start + i] += bp.Process(r.Bipolar) * Env.AD(t, attack, decay) * gain;
            }
        }

        // A struck object: a few decaying sine modes. Inharmonic ratios read as metal or
        // glass, near-harmonic low ones as wood.
        public static void Modes(float[] d, int rate, float at, float[] hz, float[] decay, float[] amp, float attack = 0.0008f)
        {
            int start = Math.Max(0, (int)(at * rate));
            float longest = 0f;
            for (int m = 0; m < decay.Length; m++) longest = Math.Max(longest, decay[m]);
            int len = Math.Min(d.Length - start, (int)(longest * 7f * rate));
            for (int m = 0; m < hz.Length; m++)
            {
                float w = 2f * MathF.PI * hz[m] / rate;
                // A rotating phasor instead of a Sin per sample: two multiplies.
                float c = MathF.Cos(w), s = MathF.Sin(w);
                float re = 1f, im = 0f;
                float k = MathF.Exp(-1f / (Math.Max(1e-4f, decay[m]) * rate));
                float g = amp[m];
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)rate;
                    float a = t < attack ? t / attack : 1f;
                    d[start + i] += im * g * a;
                    float nre = re * c - im * s;
                    im = re * s + im * c;
                    re = nre;
                    g *= k;
                }
            }
        }

        // A soft low thump: a sine gliding down, the "body" of anything heavy landing.
        public static void Thump(float[] d, int rate, float at, float fromHz, float toHz, float glide, float decay, float gain)
        {
            int start = Math.Max(0, (int)(at * rate));
            int len = Math.Min(d.Length - start, (int)(decay * 7f * rate));
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)rate;
                float f = toHz + (fromHz - toHz) * MathF.Exp(-t / Math.Max(1e-4f, glide));
                phase += 2f * MathF.PI * f / rate;
                d[start + i] += MathF.Sin(phase) * Env.AD(t, 0.002f, decay) * gain;
            }
        }

        // Karplus-Strong plucked string: bass notes, pizzicato, the kalimba's cousin.
        public static void Pluck(float[] d, int rate, Rng r, float at, float hz, float seconds, float gain,
                                 float brightness = 0.5f, float damping = 0.996f)
        {
            int start = Math.Max(0, (int)(at * rate));
            int period = Math.Max(2, (int)(rate / Math.Max(20f, hz)));
            var line = new float[period];
            var lp = new OnePole();
            lp.Set(300f + brightness * 6000f, rate);
            for (int i = 0; i < period; i++) line[i] = lp.Low(r.Bipolar);
            int len = Math.Min(d.Length - start, (int)(seconds * rate));
            int p = 0;
            float prev = 0f;
            for (int i = 0; i < len; i++)
            {
                float y = line[p];
                float avg = (y + prev) * 0.5f * damping;
                prev = y;
                line[p] = avg;
                p = (p + 1) % period;
                float fade = i > len - 200 ? (len - i) / 200f : 1f;
                d[start + i] += y * gain * fade;
            }
        }

        // A tuned plucked string (Karplus-Strong with an all-pass for the fractional part of
        // the period: without it a string at 330 Hz and 32 kHz is 8 cents sharp, which a chord
        // makes audible). brightness 0..1 is the pick: soft thumb to hard nail. pluckAt 0..0.5
        // is where along the string it is plucked: near the bridge (small) is thin and bright,
        // near the middle is round. t60 is how long the fundamental takes to fall 60 dB.
        public static void String(float[] d, int rate, Rng r, float at, float hz, float seconds, float gain,
                                  float brightness, float t60, float pluckAt = 0.2f)
        {
            int start = Math.Max(0, (int)(at * rate));
            int len = Math.Min(d.Length - start, (int)(seconds * rate));
            if (len <= 0) return;
            // The two-point average in the loop delays half a sample; the rest is the line
            // (integer) and the all-pass (fraction).
            float period = rate / Math.Max(20f, hz) - 0.5f;
            int n = Math.Max(2, (int)MathF.Floor(period - 0.1f));
            float frac = period - n;
            float ap = (1f - frac) / (1f + frac);
            // Loss per trip round the loop, so the fundamental is down 60 dB after t60 seconds.
            float g = MathF.Pow(0.001f, n / Math.Max(1f, t60 * rate));
            var line = new float[n];
            var lp = new OnePole();
            lp.Set(250f + brightness * brightness * 7000f, rate);
            for (int i = 0; i < n; i++) line[i] = lp.Low(r.Bipolar);
            // Pluck position: a comb on the excitation removes the harmonics that have a node there.
            int comb = Math.Max(1, (int)(n * Math.Clamp(pluckAt, 0.05f, 0.5f)));
            var exc = new float[n];
            for (int i = 0; i < n; i++) exc[i] = line[i] - 0.9f * line[(i - comb + n) % n];
            Array.Copy(exc, line, n);
            float mean = 0f;
            for (int i = 0; i < n; i++) mean += line[i];
            mean /= n;
            for (int i = 0; i < n; i++) line[i] -= mean;

            int p = 0;
            float prev = 0f, apX = 0f, apY = 0f;
            int fade = Math.Min(len, rate / 50);
            for (int i = 0; i < len; i++)
            {
                float y = line[p];
                float avg = (y + prev) * 0.5f * g;
                prev = y;
                // First-order all-pass: y[n] = C x[n] + x[n-1] - C y[n-1].
                float o = ap * avg + apX - ap * apY;
                apX = avg;
                apY = o;
                line[p] = o;
                p++;
                if (p >= n) p = 0;
                float f = i > len - fade ? (len - i) / (float)fade : 1f;
                d[start + i] += y * gain * f;
            }
        }

        // A mallet tone (kalimba, marimba, UI "plink"): fundamental plus a bright inharmonic
        // partial that dies fast. ratio 4 is a marimba bar, about 5.4 reads as a kalimba tine.
        public static void Mallet(float[] d, int rate, float at, float hz, float decay, float gain,
                                  float ratio = 4f, float bright = 0.35f)
        {
            Modes(d, rate, at,
                  new[] { hz, hz * ratio, hz * 2f },
                  new[] { decay, decay * 0.18f, decay * 0.4f },
                  new[] { gain, gain * bright, gain * 0.12f }, 0.0015f);
        }
    }
}
