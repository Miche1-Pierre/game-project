using UnityEngine;

namespace Movers
{
    // Every sound the destruction makes, synthesised in code.
    //
    // The greybox ships no audio files (CLAUDE.md rule 13), and a silent explosion is not an
    // explosion: half of "that was huge" is the ears. So each sound is a few lines of maths,
    // run once the first time it is asked for and kept as an AudioClip. The random seeds are
    // fixed, so the glass sounds the same on both our machines and a bug report can say "the
    // glass" and mean one thing.
    //
    // One small pool of voices plays everything. A wall coming apart can ask for forty crunches
    // in the same frame, and forty voices at once is noise, not detail. So each kind is rate
    // limited (at most six of a kind in any tenth of a second), and when every voice is busy
    // the oldest one is reused rather than the pool growing. A boom is never cut short for
    // something smaller: when every voice is a boom, the smaller sound is dropped.
    //
    // Every sound is also a fact for whoever listens (the grandmother): each Play raises a
    // LoudNoise world event, as loud as the kind and the volume make it, blamed on whoever
    // caused it when the caller knows. Kept coarse: at most one event per kind per tenth of a
    // second unless a louder or distant one comes along, so a wall coming apart is one noise.
    public static class ImpactAudio
    {
        public enum Kind { Boom, Glass, Wood, Crunch, Thud, Beep, Pin }

        const int Rate = 44100;
        const int KindCount = 7;
        const int PoolSize = 12;
        // At most this many of the same kind inside the window. Six crunches in a tenth of a
        // second already reads as "the whole wall", a seventh adds only volume.
        const int MaxPerWindow = 6;
        const float Window = 0.1f;

        const float MinDistance = 2f;
        const float BoomDistance = 70f;   // heard from the garden and from the truck
        const float SmallDistance = 25f;  // a plate breaking is heard in the next room, not the next street

        // How far each kind carries at volume 1, as WorldEvents loudness (x 30 m in the open).
        static readonly float[] Loudness = { 1f, 0.5f, 0.4f, 0.6f, 0.3f, 0.1f, 0.08f };
        const float QuietestNoise = 0.02f;
        static readonly float[] noiseTime = new float[KindCount];
        static readonly float[] noiseLoudness = new float[KindCount];
        static readonly Vector3[] noisePosition = new Vector3[KindCount];

        static readonly AudioClip[] clips = new AudioClip[KindCount];
        // The last MaxPerWindow play times of each kind, as a ring per kind: a sliding window,
        // so no burst straddling a window edge gets twice the allowance.
        static readonly float[] recent = new float[KindCount * MaxPerWindow];
        static readonly int[] oldest = new int[KindCount];
        static readonly Kind[] voiceKind = new Kind[PoolSize];
        static GameObject root;
        static AudioSource[] voices;
        static int cursor;
        static bool quitting;

        // Statics survive a play-mode exit when the domain reload is disabled (the playtest CLI
        // does that on purpose), and the voices do not: they are scene objects. The clips are
        // kept, they are plain data and still valid.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            root = null;
            voices = null;
            cursor = 0;
            for (int i = 0; i < recent.Length; i++) recent[i] = -1000f;
            for (int i = 0; i < KindCount; i++)
            {
                oldest[i] = 0;
                noiseTime[i] = -1000f;
                noiseLoudness[i] = 0f;
            }
            quitting = false;
            // Removed first: with the domain reload off this runs every play session.
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        // Leaving play mode destroys everything, and a vase breaking in its OnDestroy then must
        // not build a DontDestroyOnLoad voice pool in the middle of the teardown ("Some objects
        // were not cleaned up when closing the scene").
        static void OnQuitting() { quitting = true; }

        // Online client: the host's sounds arrive here (Props Sound, and the Boom of an
        // ExplosionFx), and only here: the client's own Play calls are ignored, so every sound
        // is heard once (NETCODE_SLICE 8). No LoudNoise: the host raised it, and forwarded it.
        public static void PlayFromNet(Kind kind, Vector3 position, float volume)
        {
            if (!Application.isPlaying || quitting) return;
            int k = (int)kind;
            if (k < 0 || k >= KindCount) return;
            if (float.IsNaN(volume)) return;
            float sum = position.x + position.y + position.z;
            if (float.IsNaN(sum) || float.IsInfinity(sum)) return;
            volume = Mathf.Clamp01(volume);
            if (volume <= 0f) return;
            Voice(kind, position, volume, false);
        }

        // Play one sound at a point in the world. volume is 0..1 on top of the clip's own level.
        public static void Play(Kind kind, Vector3 position, float volume = 1f)
        {
            Play(kind, position, volume, Actors.World);
        }

        // The same, with who made the noise (see Actors), for the LoudNoise it raises.
        public static void Play(Kind kind, Vector3 position, float volume, int instigator)
        {
            Play(kind, position, volume, instigator, true);
        }

        // forward false: an online host keeps this sound to itself, because the client plays its
        // own from another record (a wall chunk's ChunkDetached, DEV 2 3.9), and must not hear
        // it twice. The noise is raised all the same.
        public static void Play(Kind kind, Vector3 position, float volume, int instigator, bool forward)
        {
            if (Net.IsClient) return;   // the host's sound arrives through PlayFromNet
            if (!Application.isPlaying || quitting) return;   // never litter an edited scene with voices
            int k = (int)kind;
            if (k < 0 || k >= KindCount) return;
            if (float.IsNaN(volume)) return;
            // A contact point from a physics callback can be NaN; Unity logs an error for every
            // transform set to one.
            float sum = position.x + position.y + position.z;
            if (float.IsNaN(sum) || float.IsInfinity(sum)) return;
            volume = Mathf.Clamp01(volume);
            if (volume <= 0f) return;

            // The noise is a fact even when the voice below is rate limited away.
            RaiseNoise(k, position, volume, instigator);
            Voice(kind, position, volume, forward);
        }

        // The voice itself, rate limited. forward: an online host passes the sound on to the
        // client (Props Sound), except the Boom, which the client plays from the ExplosionFx.
        static void Voice(Kind kind, Vector3 position, float volume, bool forward)
        {
            int k = (int)kind;
            // Unscaled: a paused game must not keep the window shut forever. The slot holding
            // the oldest of the last six plays: if that one is still inside the window, this
            // would be the seventh.
            float now = Time.unscaledTime;
            int slot = k * MaxPerWindow + oldest[k];
            if (now - recent[slot] < Window) return;
            if (forward && Net.IsHost && kind != Kind.Boom) PropsSync.Sound(kind, position, volume);

            AudioClip clip = Clip(kind);
            if (clip == null) return;
            EnsureVoices();

            int v = PickVoice(kind);
            if (v < 0) return;   // every voice is a boom, and this is not one
            recent[slot] = now;
            oldest[k] = (oldest[k] + 1) % MaxPerWindow;

            var src = voices[v];
            src.transform.position = position;
            src.clip = clip;
            src.volume = volume;
            src.maxDistance = kind == Kind.Boom ? BoomDistance : SmallDistance;
            // The boom goes first when the mixer runs out of real voices (lower is more important).
            src.priority = kind == Kind.Boom ? 32 : 128;
            // A little pitch spread, so ten crunches in a row are ten crunches and not one
            // sample on repeat. The beep is a signal, it stays exact.
            src.pitch = kind == Kind.Beep ? 1f : Random.Range(0.93f, 1.07f);
            src.Play();
            // AUDIO: heard from the nearest player in split screen, muffled by walls, and scaled
            // by the effects volume, like every other sound (Audio/AudioDirector.cs).
            AudioDirector.Adopt(src, position, AudioChannel.Sfx);
        }

        static void RaiseNoise(int k, Vector3 position, float volume, int instigator)
        {
            if (!Net.HasAuthority) return;   // the grandmother's hearing runs on the host only
            float loud = Loudness[k] * volume;
            if (loud < QuietestNoise) return;
            float now = Time.time;
            bool due = now - noiseTime[k] >= Window
                       || loud > noiseLoudness[k] * 1.25f
                       || (position - noisePosition[k]).sqrMagnitude > 16f;
            if (!due) return;
            noiseTime[k] = now;
            noiseLoudness[k] = loud;
            noisePosition[k] = position;
            WorldEvents.Raise(WorldEventType.LoudNoise, position, instigator, loud);
        }

        // ---- voices ----

        static void EnsureVoices()
        {
            if (root != null && voices != null)
            {
                bool intact = true;
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i] == null) { intact = false; break; }
                if (intact) return;
                Object.Destroy(root);
            }

            root = new GameObject("ImpactAudio");
            // Survives a scene reload, so restarting the level does not rebuild the pool mid-blast.
            Object.DontDestroyOnLoad(root);
            voices = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(root.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = MinDistance;
                s.maxDistance = SmallDistance;
                s.dopplerLevel = 0f;   // flying debris should not warble
                s.spread = 0f;
                voices[i] = s;
                voiceKind[i] = Kind.Thud;
            }
            cursor = 0;
            EnsureListener();
        }

        // Round robin: the voice after the last one used is the one that started longest ago.
        // A free voice first; else the oldest one that is not a boom; a boom may also replace
        // the oldest boom. -1 when every voice is a boom and this is something smaller: a
        // crunch is dropped rather than cutting a blast short.
        static int PickVoice(Kind kind)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                int v = (cursor + i) % PoolSize;
                if (!voices[v].isPlaying) return Take(v, kind);
            }
            for (int i = 0; i < PoolSize; i++)
            {
                int v = (cursor + i) % PoolSize;
                if (kind == Kind.Boom || voiceKind[v] != Kind.Boom) return Take(v, kind);
            }
            return -1;
        }

        static int Take(int v, Kind kind)
        {
            cursor = (v + 1) % PoolSize;
            voiceKind[v] = kind;
            voices[v].Stop();
            return v;
        }

        // Spatial sound needs ears. P1's camera normally carries the one AudioListener; if
        // nobody does, the whole destruction is silent with no error that says why.
        static void EnsureListener()
        {
            if (Object.FindAnyObjectByType<AudioListener>() != null) return;
            var crew = CrewRoster.All;
            Camera cam = crew.Count > 0 && crew[0] != null ? crew[0].View : null;
            if (cam == null) return;
            cam.gameObject.AddComponent<AudioListener>();
            Debug.LogWarning("ImpactAudio: no AudioListener in the scene, added one to " + cam.name + ".");
        }

        // ---- clips ----

        static AudioClip Clip(Kind kind)
        {
            int k = (int)kind;
            if (clips[k] != null) return clips[k];

            float[] data = Synth(kind);
            var clip = AudioClip.Create("SFX_" + kind, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clips[k] = clip;
            return clip;
        }

        static float[] Synth(Kind kind)
        {
            switch (kind)
            {
                case Kind.Boom: return Boom();
                case Kind.Glass: return Glass();
                case Kind.Wood: return Wood();
                case Kind.Crunch: return Crunch();
                case Kind.Thud: return Thud();
                case Kind.Beep: return Beep();
                default: return Pin();
            }
        }

        // 1.8 s. Noise through a low-pass whose cutoff sweeps down from 900 to 120 Hz: the roar
        // closes into a rumble, which is what makes a blast sound big rather than loud. Under it
        // a 50 to 40 Hz thump for the chest, and 30 ms of raw noise on top for the front edge.
        static float[] Boom()
        {
            const float Length = 1.8f;
            var d = Samples(Length);
            var rnd = new System.Random(4101);
            float lp1 = 0f, lp2 = 0f, phase = 0f;
            float sweep = Mathf.Log(120f / 900f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float a = Coef(900f * Mathf.Exp(sweep * t / Length));
                float n = Noise(rnd);
                // Two poles in a row: one alone leaves too much hiss on top of the rumble.
                lp1 += a * (n - lp1);
                lp2 += a * (lp1 - lp2);
                float attack = Mathf.Clamp01(t / 0.004f);
                // Filtering noise also quietens it, more so as the cutoff falls. Dividing by
                // sqrt(a) makes up for it, so the tail stays a rumble instead of fading to
                // nothing halfway through.
                float roar = lp2 / Mathf.Sqrt(a) * attack * Mathf.Exp(-t * 2f);
                phase += 2f * Mathf.PI * Mathf.Lerp(50f, 40f, Mathf.Clamp01(t / 0.6f)) / Rate;
                float thump = Mathf.Sin(phase) * attack * Mathf.Exp(-t * 4f);
                float crack = t < 0.03f ? n * (1f - t / 0.03f) : 0f;
                d[i] = roar * 0.8f + thump * 0.7f + crack * 0.35f;
            }
            return Finish(d, 0.98f);
        }

        // 0.7 s. A short hiss of high-passed noise for the break itself, then a dozen tiny
        // bright pings scattered over the first third of a second: the pieces landing.
        static float[] Glass()
        {
            const int Pings = 12;
            var d = Samples(0.7f);
            var rnd = new System.Random(4102);
            var hz = new float[Pings];
            var start = new float[Pings];
            var decay = new float[Pings];
            var amp = new float[Pings];
            for (int p = 0; p < Pings; p++)
            {
                hz[p] = Mathf.Lerp(2500f, 7000f, (float)rnd.NextDouble());
                float r = (float)rnd.NextDouble();
                start[p] = r * r * 0.32f;           // bunched at the start, a few stragglers
                decay[p] = Mathf.Lerp(14f, 38f, (float)rnd.NextDouble());
                amp[p] = Mathf.Lerp(0.12f, 0.32f, (float)rnd.NextDouble());
            }

            float lp = 0f;
            float aHp = Coef(3000f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise(rnd);
                lp += aHp * (n - lp);
                float s = (n - lp) * Mathf.Clamp01(t / 0.001f) * Mathf.Exp(-t * 32f) * 0.9f;
                for (int p = 0; p < Pings; p++)
                {
                    float tt = t - start[p];
                    if (tt < 0f) continue;
                    s += Mathf.Sin(2f * Mathf.PI * hz[p] * tt) * amp[p] * Mathf.Exp(-tt * decay[p])
                         * Mathf.Clamp01(tt / 0.0005f);
                }
                d[i] = s;
            }
            return Finish(d, 0.8f);
        }

        // 0.35 s. Wood splits in two beats: a band of mid noise, a smaller second snap 30 ms
        // later, and a hollow knock at about 180 Hz for the body of the plank.
        static float[] Wood()
        {
            var d = Samples(0.35f);
            var rnd = new System.Random(4103);
            float lpA = 0f, lpB = 0f, phase = 0f;
            float aA = Coef(2600f), aB = Coef(450f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise(rnd);
                lpA += aA * (n - lpA);
                lpB += aB * (n - lpB);
                float band = lpA - lpB;
                float env = Mathf.Clamp01(t / 0.0008f) * Mathf.Exp(-t * 30f);
                float second = t > 0.03f ? Mathf.Exp(-(t - 0.03f) * 70f) * 0.6f : 0f;
                phase += 2f * Mathf.PI * Mathf.Lerp(190f, 165f, Mathf.Clamp01(t / 0.2f)) / Rate;
                float knock = Mathf.Sin(phase) * Mathf.Clamp01(t / 0.001f) * Mathf.Exp(-t * 22f);
                d[i] = band * (env + second) * 2.2f + knock * 0.55f;
            }
            return Finish(d, 0.8f);
        }

        // 0.5 s. Plaster, stone, a crushed box: mid-band noise chopped into a handful of micro
        // bursts, with a stepped sample-and-hold signal mixed in for the grit.
        static float[] Crunch()
        {
            const int Bursts = 9;
            var d = Samples(0.5f);
            var rnd = new System.Random(4104);
            var at = new float[Bursts];
            var fall = new float[Bursts];
            var amp = new float[Bursts];
            for (int b = 0; b < Bursts; b++)
            {
                at[b] = b == 0 ? 0f : (float)rnd.NextDouble() * 0.3f;
                fall[b] = Mathf.Lerp(45f, 110f, (float)rnd.NextDouble());
                amp[b] = b == 0 ? 1f : Mathf.Lerp(0.4f, 1f, (float)rnd.NextDouble());
            }

            float lpA = 0f, lpB = 0f, hold = 0f;
            int holdLeft = 0;
            float aA = Coef(2200f), aB = Coef(320f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise(rnd);
                lpA += aA * (n - lpA);
                lpB += aB * (n - lpB);
                if (--holdLeft <= 0)
                {
                    hold = Noise(rnd);
                    holdLeft = rnd.Next(20, 90);
                }
                float env = 0f;
                for (int b = 0; b < Bursts; b++)
                    if (t >= at[b]) env += amp[b] * Mathf.Exp(-(t - at[b]) * fall[b]);
                env *= Mathf.Clamp01(t / 0.0008f) * Mathf.Exp(-t * 3f);
                d[i] = ((lpA - lpB) * 2f + hold * 0.35f) * env;
            }
            return Finish(d, 0.75f);
        }

        // 0.25 s. Something heavy meeting the floor: a low sine that drops from 110 to 70 Hz,
        // with a puff of muffled noise for the contact.
        static float[] Thud()
        {
            var d = Samples(0.25f);
            var rnd = new System.Random(4105);
            float lp = 0f, phase = 0f;
            float aL = Coef(260f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                phase += 2f * Mathf.PI * Mathf.Lerp(110f, 70f, Mathf.Clamp01(t / 0.18f)) / Rate;
                float env = Mathf.Clamp01(t / 0.002f) * Mathf.Exp(-t * 16f);
                lp += aL * (Noise(rnd) - lp);
                d[i] = Mathf.Sin(phase) * env + lp * 3f * Mathf.Exp(-t * 40f);
            }
            return Finish(d, 0.8f);
        }

        // 70 ms at 1.9 kHz. A square wave built from its first three odd harmonics: square
        // enough to sound electronic, soft enough not to alias.
        static float[] Beep()
        {
            const float Length = 0.07f;
            var d = Samples(Length);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float x = 2f * Mathf.PI * 1900f * t;
                float s = Mathf.Sin(x) + Mathf.Sin(3f * x) / 3f + Mathf.Sin(5f * x) / 5f;
                float env = Mathf.Clamp01(t / 0.003f) * Mathf.Clamp01((Length - t) / 0.004f);
                d[i] = s * env;
            }
            return Finish(d, 0.45f);
        }

        // 0.12 s. A pin leaving a grenade: two short metallic clicks, each a high sine with an
        // inharmonic overtone (that is what makes it metal and not a bell) and a tick of noise.
        static float[] Pin()
        {
            var d = Samples(0.12f);
            var rnd = new System.Random(4106);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise(rnd);
                d[i] = Click(t, 0f, 3400f, 60f, n) + Click(t, 0.045f, 5300f, 75f, n) * 0.8f;
            }
            return Finish(d, 0.6f);
        }

        static float Click(float t, float at, float hz, float decay, float noise)
        {
            float tt = t - at;
            if (tt < 0f) return 0f;
            float w = 2f * Mathf.PI * hz * tt;
            return Mathf.Sin(w) * Mathf.Exp(-tt * decay)
                   + Mathf.Sin(w * 2.76f) * 0.35f * Mathf.Exp(-tt * decay * 1.6f)
                   + noise * 0.5f * Mathf.Exp(-tt * 500f);
        }

        // ---- helpers ----

        static float[] Samples(float seconds)
        {
            return new float[Mathf.Max(1, Mathf.CeilToInt(seconds * Rate))];
        }

        static float Noise(System.Random rnd)
        {
            return (float)(rnd.NextDouble() * 2.0 - 1.0);
        }

        // One-pole low-pass coefficient for a cutoff in Hz.
        static float Coef(float hz)
        {
            return 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate);
        }

        // Normalise to a set peak, which is what sets the mix between kinds, then fade the last
        // 5 ms so no clip ends on a click.
        static float[] Finish(float[] d, float peak)
        {
            float max = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float a = Mathf.Abs(d[i]);
                if (a > max) max = a;
            }
            float g = max > 1e-6f ? peak / max : 0f;
            for (int i = 0; i < d.Length; i++) d[i] *= g;

            int fade = Mathf.Min(d.Length, Rate / 200);
            for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= i / (float)fade;
            return d;
        }
    }
}
