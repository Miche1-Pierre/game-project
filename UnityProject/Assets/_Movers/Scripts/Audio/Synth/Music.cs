using System;

namespace Movers.AudioSynth
{
    public enum MusicTrack { Menu, House, Count }

    // The two pieces of music, written out note by note and played by small synthesised
    // instruments: a fingerpicked nylon guitar, an upright bass, a marimba, a kalimba, a soft
    // electric piano, a whistle, a pad and brushes. All plucked, struck or breathed, never a
    // bare oscillator: that is what keeps it warm rather than chiptune.
    //
    // Menu, "Moving Day": F major, 88 bpm with a light swing, 16 bars (about 44 s). A lilting
    // fingerpicked tune on marimba, the second half doubled by a whistle, a minor iv chord
    // (Bbm6) in bar 14 for the small lump in the throat of leaving a house.
    //
    // House, "Tea Time": the same key, 70 bpm, 16 bars (about 55 s), electric piano chords, a
    // kalimba that comes in for four bars now and then, no drums. Under the game, never over it.
    //
    // Both are rendered one pass plus the ring-out, and the ring-out is folded back onto the
    // start (Buf.WrapLoop), so the loop has no seam. Stereo, 32 kHz.
    public static class Music
    {
        public const int Rate = 32000;

        public static Rendered Render(MusicTrack track)
        {
            return track == MusicTrack.Menu ? Menu() : House();
        }

        // ---------------------------------------------------------------- the chords

        // A chord as the guitar holds it: a bass string and three upper notes, plus the root
        // and fifth an octave down for the upright bass. MIDI numbers.
        struct Chord
        {
            public int bass, u1, u2, u3, low, lowFifth;
            public Chord(int bass, int u1, int u2, int u3, int low, int lowFifth)
            { this.bass = bass; this.u1 = u1; this.u2 = u2; this.u3 = u3; this.low = low; this.lowFifth = lowFifth; }
            public int Upper(int i) => i == 0 ? u1 : i == 1 ? u2 : u3;
        }

        static readonly Chord Fmaj7 = new Chord(41, 57, 60, 64, 29, 36);
        static readonly Chord Am7 = new Chord(45, 55, 60, 64, 33, 40);
        static readonly Chord Bbmaj7 = new Chord(46, 53, 57, 62, 34, 41);
        static readonly Chord C7sus4 = new Chord(48, 53, 55, 58, 36, 43);
        static readonly Chord C7 = new Chord(48, 52, 55, 58, 36, 43);
        static readonly Chord Dm7 = new Chord(50, 53, 57, 60, 38, 45);
        static readonly Chord Gm7 = new Chord(43, 53, 58, 62, 31, 38);
        static readonly Chord Bbm6 = new Chord(46, 53, 55, 61, 34, 41);
        static readonly Chord FoverA = new Chord(45, 53, 57, 60, 33, 36);

        // The House piece's electric piano voicings (four notes, no root: the bass has it).
        static readonly Chord Fmaj9 = new Chord(57, 60, 64, 67, 29, 36);        // A C E G
        static readonly Chord Cmaj7E = new Chord(55, 59, 60, 64, 28, 35);       // G B C E over E
        static readonly Chord Dm9 = new Chord(53, 57, 60, 64, 26, 33);          // F A C E
        static readonly Chord Bbmaj9 = new Chord(53, 57, 58, 62, 34, 41);       // F A Bb D
        static readonly Chord Am7b = new Chord(52, 55, 60, 64, 33, 40);         // E G C E
        static readonly Chord Gm9 = new Chord(53, 57, 58, 62, 31, 38);          // F A Bb D
        static readonly Chord C9sus = new Chord(53, 55, 58, 62, 36, 43);        // F G Bb D

        // One note of a tune: when (in beats from the start of its bar), how long, which.
        struct N
        {
            public float beat, len;
            public int midi;
            public N(float beat, float len, int midi) { this.beat = beat; this.len = len; this.midi = midi; }
        }

        // ---------------------------------------------------------------- Menu: "Moving Day"

        static Rendered Menu()
        {
            const float bpm = 88f;
            const int bars = 16;
            const float swing = 0.56f;   // the off-beat eighth lands at 56 % of the beat, not 50
            var mix = new Mixer(bars * 4, bpm, 3.5f, 0x4D6F76u);
            var r = mix.rng;

            // Two chords per bar at most: the second one from beat 2.
            Chord[] first =
            {
                Fmaj7, Am7, Bbmaj7, C7sus4, Fmaj7, Dm7, Gm7, C7,
                Bbmaj7, Am7, Gm7, Fmaj7, Bbmaj7, Bbm6, FoverA, Gm7,
            };
            Chord[] second =
            {
                Fmaj7, Am7, Bbmaj7, C7, Fmaj7, Dm7, Gm7, C7,
                Bbmaj7, Am7, Gm7, Fmaj7, Bbmaj7, Bbm6, Dm7, C7,
            };

            N[][] tune =
            {
                new[] { new N(0, 1, 72), new N(1, 1, 69), new N(2, .5f, 67), new N(2.5f, .5f, 69), new N(3, 1, 72) },
                new[] { new N(0, 1.5f, 76), new N(1.5f, .5f, 74), new N(2, 2, 72) },
                new[] { new N(0, 1, 74), new N(1, .5f, 72), new N(1.5f, .5f, 70), new N(2, 1, 69), new N(3, 1, 65) },
                new[] { new N(0, 1.5f, 72), new N(1.5f, .5f, 70), new N(2, 2, 67) },
                new[] { new N(0, 1, 69), new N(1, 1, 72), new N(2, 1, 77), new N(3, .5f, 76), new N(3.5f, .5f, 74) },
                new[] { new N(0, 2, 72), new N(2, 1, 69), new N(3, 1, 74) },
                new[] { new N(0, 1.5f, 70), new N(1.5f, .5f, 69), new N(2, 1, 67), new N(3, 1, 70) },
                new[] { new N(0, 2, 72), new N(2, 1, 70), new N(3, 1, 67) },
                new[] { new N(0, 1, 74), new N(1, 1, 77), new N(2, 2, 81) },
                new[] { new N(0, 1, 79), new N(1, 1, 76), new N(2, 2, 72) },
                new[] { new N(0, 1, 74), new N(1, 1, 70), new N(2, 1.5f, 74), new N(3.5f, .5f, 72) },
                new[] { new N(0, 3, 69), new N(3, .5f, 72), new N(3.5f, .5f, 74) },
                new[] { new N(0, 1, 77), new N(1, 1, 74), new N(2, 1, 81), new N(3, 1, 79) },
                new[] { new N(0, 2, 77), new N(2, 1, 73), new N(3, 1, 70) },
                new[] { new N(0, 1, 72), new N(1, 1, 69), new N(2, 1, 74), new N(3, 1, 77) },
                new[] { new N(0, 1.5f, 74), new N(1.5f, .5f, 72), new N(2, 1, 70), new N(3, 1, 67) },
            };

            for (int bar = 0; bar < bars; bar++)
            {
                bool b = bar >= 8;
                float bar0 = bar * 4f;

                // Guitar: a Travis-style pattern in eighths, bass on the beats 1 and 3
                // (root, then the fifth), the three upper strings rolling in between.
                for (int e = 0; e < 8; e++)
                {
                    Chord c = e < 4 ? first[bar] : second[bar];
                    float beat = bar0 + Swing(e * 0.5f, swing);
                    int midi;
                    float vel;
                    if (e == 0) { midi = c.bass; vel = 0.85f; }
                    else if (e == 4) { midi = c.lowFifth + 12; vel = 0.75f; }   // the alternating bass
                    else
                    {
                        midi = c.Upper(Roll[e]);
                        vel = e % 2 == 1 ? 0.5f : 0.6f;
                    }
                    mix.Guitar(beat, midi, vel * r.Range(0.9f, 1.05f), -0.35f);
                }

                // Upright bass: the root on 1, the fifth on 3.
                mix.Bass(bar0, first[bar].low + 12, 0.95f, 1.6f);
                mix.Bass(bar0 + 2f, second[bar].lowFifth + 12, 0.75f, 1.4f);

                // The tune on marimba; the second half doubled by a soft whistle.
                foreach (var n in tune[bar])
                {
                    float at = bar0 + Swing(n.beat, swing);
                    mix.Marimba(at, n.midi, (n.beat % 1f == 0f ? 0.85f : 0.7f) * r.Range(0.92f, 1.05f), 0.3f, 0.5f);
                    if (b) mix.Whistle(at, n.len * mix.beat, n.midi, 0.22f, 0.15f);
                }

                // A pad under everything, a little more of it in the second half.
                mix.Pad(bar0, 2f, first[bar], b ? 0.05f : 0.035f);
                mix.Pad(bar0 + 2f, 2f, second[bar], b ? 0.05f : 0.035f);

                // Brushes: a soft kick on 1, swishes on 2 and 4, a shaker on the eighths.
                mix.Kick(bar0, 0.5f);
                if (!b) mix.Kick(bar0 + 2f, 0.28f);
                mix.Brush(bar0 + 1f, 0.45f);
                mix.Brush(bar0 + 3f, 0.5f);
                for (int e = 0; e < 8; e++)
                    mix.Shaker(bar0 + Swing(e * 0.5f, swing), (e % 2 == 1 ? 0.35f : 0.22f) * r.Range(0.8f, 1.1f));
            }
            return mix.Finish(0.6f);
        }

        // ---------------------------------------------------------------- House: "Tea Time"

        static Rendered House()
        {
            const float bpm = 70f;
            const int bars = 16;
            var mix = new Mixer(bars * 4, bpm, 4f, 0x54656Du);
            var r = mix.rng;

            Chord[] prog =
            {
                Fmaj9, Cmaj7E, Dm9, Bbmaj9, Fmaj9, Am7b, Gm9, C9sus,
                Bbmaj9, Am7b, Dm9, Cmaj7E, Bbmaj9, Am7b, Gm9, C9sus,
            };

            // The kalimba plays in bars 4 to 7 and 12 to 15 only, so most of the time the room is
            // just the piano, and when the tune comes it is noticed.
            N[][] tune = new N[16][];
            tune[4] = new[] { new N(.5f, .5f, 77), new N(1, .5f, 76), new N(1.5f, 1.5f, 72), new N(3, 1, 69) };
            tune[5] = new[] { new N(.5f, .5f, 76), new N(1, .5f, 74), new N(1.5f, 2.5f, 72) };
            tune[6] = new[] { new N(.5f, .5f, 74), new N(1, .5f, 70), new N(1.5f, 1.5f, 69), new N(3, 1, 65) };
            tune[7] = new[] { new N(0, 1, 70), new N(1, 3, 67) };
            tune[12] = new[] { new N(.5f, .5f, 81), new N(1, .5f, 77), new N(1.5f, 1.5f, 74), new N(3, 1, 72) };
            tune[13] = new[] { new N(0, 1.5f, 72), new N(1.5f, .5f, 69), new N(2, 2, 64) };
            tune[14] = new[] { new N(.5f, .5f, 74), new N(1, .5f, 72), new N(1.5f, 1.5f, 70), new N(3, 1, 69) };
            tune[15] = new[] { new N(0, 4, 67) };
            // A single high twinkle at the end of two of the quiet bars.
            tune[2] = new[] { new N(3, 1, 84) };
            tune[10] = new[] { new N(3, 1, 81) };

            for (int bar = 0; bar < bars; bar++)
            {
                float bar0 = bar * 4f;
                Chord c = prog[bar];

                // Piano: the chord on 1, lifted softly on the and of 2, one note on 4.
                for (int i = 0; i < 4; i++)
                {
                    int m = i == 0 ? c.bass : c.Upper(i - 1);
                    float roll = i * 0.012f / mix.beat;   // rolled by 12 ms a note, like a real hand
                    mix.Piano(bar0 + roll, m, 0.55f * r.Range(0.9f, 1.05f), 2.6f * mix.beat, (i - 1.5f) * 0.25f);
                }
                for (int i = 1; i < 4; i++)
                    mix.Piano(bar0 + 1.5f + i * 0.01f, c.Upper(i - 1), 0.3f * r.Range(0.9f, 1.05f), 1.3f * mix.beat, (i - 2f) * 0.3f);
                mix.Piano(bar0 + 3f, c.Upper(2) + 12, 0.22f, 1f * mix.beat, 0.3f);

                // Bass: one long soft note a bar.
                mix.Bass(bar0, c.low + 12, 0.7f, 3.4f);

                mix.Pad(bar0, 4f, c, 0.03f);

                if (tune[bar] != null)
                    foreach (var n in tune[bar])
                        mix.Kalimba(bar0 + n.beat, n.midi, 0.7f * r.Range(0.9f, 1.05f), 0.25f);
            }
            return mix.Finish(0.55f);
        }

        // Which upper string each eighth of the guitar pattern picks (0, 4 are the bass).
        static readonly int[] Roll = { 0, 0, 1, 2, 0, 2, 1, 0 };

        // Where an eighth note lands with swing: on-beat eighths stay, off-beat ones move late.
        static float Swing(float beat, float amount)
        {
            float whole = MathF.Floor(beat);
            float part = beat - whole;
            if (MathF.Abs(part - 0.5f) < 0.01f) return whole + amount;
            return beat;
        }

        // ---------------------------------------------------------------- the band

        // Renders notes one by one into a scratch buffer, then pans them into the stereo mix
        // with a send to a small stereo room. One scratch, reused: the whole piece allocates the
        // mix, the send and little else.
        sealed class Mixer
        {
            public readonly Rng rng;
            public readonly float beat;          // seconds per beat
            readonly int rate = Rate;
            readonly int loopFrames, frames;
            readonly float[] lr, send, scratch;

            public Mixer(int beats, float bpm, float tailSeconds, uint seed)
            {
                rng = new Rng(seed);
                beat = 60f / bpm;
                loopFrames = (int)MathF.Round(beats * beat * rate);
                frames = loopFrames + (int)(tailSeconds * rate);
                lr = new float[frames * 2];
                send = new float[frames];
                scratch = new float[(int)(4.5f * rate)];
            }

            int At(float beats)
            {
                // A few milliseconds of human timing, never ahead of the downbeat of the piece.
                float t = beats * beat + rng.Range(-0.004f, 0.006f);
                return Math.Max(0, (int)(t * rate));
            }

            void Clear(int n) { Array.Clear(scratch, 0, Math.Min(n, scratch.Length)); }

            void Add(int start, int len, float pan, float gain, float sendGain)
            {
                float a = (Math.Clamp(pan, -1f, 1f) + 1f) * 0.25f * MathF.PI;
                float gl = MathF.Cos(a) * gain * 1.41f, gr = MathF.Sin(a) * gain * 1.41f;
                len = Math.Min(len, scratch.Length);
                for (int i = 0; i < len; i++)
                {
                    int j = start + i;
                    if (j >= frames) break;
                    float s = scratch[i];
                    lr[2 * j] += s * gl;
                    lr[2 * j + 1] += s * gr;
                    send[j] += s * sendGain;
                }
            }

            public void Guitar(float beats, int midi, float vel, float pan)
            {
                int len = (int)(2.6f * rate);
                Clear(len);
                float hz = Buf.Hz(midi);
                float t60 = midi < 50 ? 3.2f : 2.4f;
                Gen.String(scratch, rate, rng, 0f, hz, 2.6f, 1f, 0.45f + 0.35f * vel, t60, 0.17f + rng.Range(0f, 0.06f));
                // The body: a hollow wooden box resonating somewhere around 200 Hz.
                var body = Biquad.Peak(210f, 1.2f, 4f, rate);
                var top = new OnePole(); top.Set(6500f, rate);
                for (int i = 0; i < len; i++) scratch[i] = top.Low(body.Process(scratch[i]));
                Add(At(beats), len, pan + rng.Range(-0.05f, 0.05f), 0.16f * vel, 0.22f);
            }

            public void Bass(float beats, int midi, float vel, float lengthBeats)
            {
                float seconds = Math.Min(4f, lengthBeats * beat + 0.3f);
                int len = (int)(seconds * rate);
                Clear(len);
                Gen.String(scratch, rate, rng, 0f, Buf.Hz(midi - 12), seconds, 1f, 0.12f, 1.8f, 0.35f);
                Gen.Thump(scratch, rate, 0f, Buf.Hz(midi - 12) * 2f, Buf.Hz(midi - 12), 0.01f, 0.05f, 0.25f);
                // Fingers leave the string at the end of the note.
                int rel = Math.Min(len, rate / 12);
                for (int i = 0; i < rel; i++) scratch[len - 1 - i] *= i / (float)rel;
                var lp = new OnePole(); lp.Set(900f, rate);
                for (int i = 0; i < len; i++) scratch[i] = lp.Low(scratch[i]);
                Add(At(beats), len, 0f, 0.42f * vel, 0.06f);
            }

            public void Marimba(float beats, int midi, float vel, float pan, float decay)
            {
                int len = (int)((decay * 5f + 0.1f) * rate);
                Clear(len);
                Gen.Mallet(scratch, rate, 0f, Buf.Hz(midi), decay, 1f, 4f, 0.35f + 0.2f * vel);
                // The mallet's felt: a soft knock under the bar.
                Gen.NoiseBurst(scratch, rate, rng, 0f, 1400f, 0.9f, 0.0008f, 0.006f, 0.08f);
                Add(At(beats), len, pan, 0.2f * vel, 0.35f);
            }

            public void Kalimba(float beats, int midi, float vel, float pan)
            {
                int len = (int)(3.4f * rate);
                Clear(len);
                Gen.Mallet(scratch, rate, 0f, Buf.Hz(midi), 0.7f, 1f, 5.4f, 0.3f);
                // The tine buzzes very slightly against the box.
                Gen.Modes(scratch, rate, 0f, new[] { Buf.Hz(midi) * 2.02f }, new[] { 0.25f }, new[] { 0.08f });
                Add(At(beats), len, pan, 0.22f * vel, 0.45f);
            }

            // A soft electric piano (two-operator FM: a sine bent by another sine at the same
            // frequency, the bend fading as the note rings, the way a tine loses its bark).
            public void Piano(float beats, int midi, float vel, float seconds, float pan)
            {
                seconds = Math.Min(seconds + 0.6f, 4.4f);
                int len = (int)(seconds * rate);
                Clear(len);
                float hz = Buf.Hz(midi);
                float w = 2f * MathF.PI * hz / rate;
                float wTine = w * 14f;
                float release = seconds - 0.5f;
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)rate;
                    float index = 1.25f * vel * MathF.Exp(-t / 0.5f) + 0.18f;
                    float mod = MathF.Sin(w * i) * index;
                    float amp = Env.AD(t, 0.004f, 1.9f);
                    if (t > release) amp *= Math.Max(0f, 1f - (t - release) / 0.5f);
                    float tine = MathF.Sin(wTine * i) * 0.1f * MathF.Exp(-t / 0.025f);
                    scratch[i] = (MathF.Sin(w * i + mod) + tine) * amp;
                }
                var lp = new OnePole(); lp.Set(3600f, rate);
                for (int i = 0; i < len; i++) scratch[i] = lp.Low(scratch[i]);
                Add(At(beats), len, pan, 0.14f * vel, 0.4f);
            }

            // A person whistling softly: a sine with a breath, a slow vibrato that only starts
            // once the note has settled.
            public void Whistle(float beats, float seconds, int midi, float vel, float pan)
            {
                seconds = Math.Min(seconds, 3.5f);
                int len = (int)((seconds + 0.1f) * rate);
                Clear(len);
                float hz = Buf.Hz(midi);
                var breath = Biquad.BandPass(hz * 2f, 3f, rate);
                float ph = 0f;
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)rate;
                    float vib = 1f + 0.004f * MathF.Sin(2f * MathF.PI * 5.2f * t) * Env.Smooth((t - 0.15f) / 0.2f);
                    ph += hz * vib / rate;
                    ph -= MathF.Floor(ph);
                    float env = Env.ASR(t, seconds + 0.08f, 0.06f, 0.09f);
                    scratch[i] = (MathF.Sin(2f * MathF.PI * ph) + 0.08f * MathF.Sin(4f * MathF.PI * ph)
                                  + breath.Process(rng.Bipolar) * 0.05f) * env;
                }
                // A sustained tone: the room amplifies it far more than a pluck, so a small send.
                Add(At(beats), len, pan, 0.12f * vel, 0.08f);
            }

            // Sustained chord tones, each voice two slightly detuned oscillators, one per ear.
            // Written straight into the mix: it is the one thing that should be wide.
            public void Pad(float beats, float lengthBeats, Chord c, float gain)
            {
                float seconds = lengthBeats * beat;
                int start = Math.Max(0, (int)(beats * beat * rate));
                int len = (int)((seconds + 0.9f) * rate);
                for (int v = 0; v < 3; v++)
                {
                    float hz = Buf.Hz(c.Upper(v));
                    float wl = 2f * MathF.PI * hz * 0.997f / rate, wr = 2f * MathF.PI * hz * 1.003f / rate;
                    var lpl = new OnePole(); lpl.Set(1300f, rate);
                    var lpr = new OnePole(); lpr.Set(1300f, rate);
                    float pl = rng.Range(0f, 6.28f), pr = rng.Range(0f, 6.28f);
                    for (int i = 0; i < len; i++)
                    {
                        int j = start + i;
                        if (j >= frames) break;
                        float t = i / (float)rate;
                        float env = Env.ASR(t, seconds + 0.9f, 0.6f, 0.9f);
                        pl += wl; pr += wr;
                        float l = MathF.Sin(pl) + 0.12f * MathF.Sin(3f * pl);
                        float rr = MathF.Sin(pr) + 0.12f * MathF.Sin(3f * pr);
                        lr[2 * j] += lpl.Low(l) * env * gain;
                        lr[2 * j + 1] += lpr.Low(rr) * env * gain;
                        send[j] += (l + rr) * env * gain * 0.25f;
                    }
                }
            }

            public void Kick(float beats, float vel)
            {
                int len = (int)(0.5f * rate);
                Clear(len);
                Gen.Thump(scratch, rate, 0f, 95f, 52f, 0.025f, 0.09f, 1f);
                Add(At(beats), len, 0f, 0.3f * vel, 0.03f);
            }

            public void Brush(float beats, float vel)
            {
                int len = (int)(0.45f * rate);
                Clear(len);
                // A swish, not a hit: the brush slides in over 30 ms.
                Gen.NoiseBurst(scratch, rate, rng, 0f, 3800f, 0.6f, 0.03f, 0.07f, 1f);
                Gen.NoiseBurst(scratch, rate, rng, 0.02f, 1500f, 0.8f, 0.01f, 0.05f, 0.25f);
                Add(At(beats), len, -0.3f, 0.09f * vel, 0.2f);
            }

            public void Shaker(float beats, float vel)
            {
                int len = (int)(0.15f * rate);
                Clear(len);
                Gen.NoiseBurst(scratch, rate, rng, 0f, 7200f, 1.1f, 0.006f, 0.022f, 1f);
                Add(At(beats), len, 0.45f, 0.09f * vel, 0.1f);
            }

            // The room, the fold into a loop, a touch of tape warmth, and the level.
            public Rendered Finish(float peak)
            {
                var verbL = new TinyVerb(rate, 0.78f, 0.45f);
                var verbR = new TinyVerb((int)(rate * 1.07f), 0.8f, 0.45f);
                var hp = new OnePole(); hp.Set(180f, rate);   // no mud in the room
                for (int i = 0; i < frames; i++)
                {
                    float s = hp.High(send[i]);
                    lr[2 * i] += verbL.Process(s) * 1.1f;
                    lr[2 * i + 1] += verbR.Process(s) * 1.1f;
                }
                float[] d = Buf.WrapLoop(lr, loopFrames, 2);
                // A little air: a gentle shelf above 3 kHz, so the picking and the mallets
                // read on laptop speakers instead of the mix sounding like it is next door.
                var airL = new OnePole(); airL.Set(3000f, rate);
                var airR = new OnePole(); airR.Set(3000f, rate);
                for (int i = 0; i < loopFrames; i++)
                {
                    d[2 * i] += airL.High(d[2 * i]) * 0.9f;
                    d[2 * i + 1] += airR.High(d[2 * i + 1]) * 0.9f;
                }
                float max = Buf.Peak(d);
                float g = max > 1e-6f ? 1.25f / max : 0f;
                float norm = 1f / MathF.Tanh(1.25f);
                for (int i = 0; i < d.Length; i++) d[i] = MathF.Tanh(d[i] * g) * norm * peak;
                return new Rendered { samples = d, rate = rate, loop = true, channels = 2 };
            }
        }
    }
}
