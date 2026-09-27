using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The AudioDirector's 48 AudioSources and what happens to each every frame.
    //
    // Taking a voice: a free one within the channel's budget (effects 30, voices 8, ambience 8,
    // interface 4), so a wall coming down cannot take the voices her lines or the menu need.
    // When the channel is over budget, or nothing is free, the least important one-shot of that
    // channel (else of any) is replaced: by preset priority, then the oldest. Loops are never
    // replaced, and a sound less important than everything it could replace is dropped.
    //
    // Every frame, each voice: follows what it is attached to, is placed as the nearest player
    // hears it (Ears), gets its walls judged a few times a second (Occlusion) and smoothed into a
    // volume and a low-pass, and plays at caller's volume x fade x walls x GameAudio.Gain.
    //
    // Also keeps sources other systems own and hand over (ImpactAudio, Adopt): same placement,
    // walls and effects slider, until they stop.
    internal sealed class VoicePool
    {
        public const int Size = 48;
        const int OcclusionChecksPerFrame = 8;
        const float OcclusionInterval = 0.12f;
        // Voices each channel may hold at once, by AudioChannel (Master, Music, Sfx, Voice,
        // Ambience, Ui). Music has its own two sources (MusicPlayer).
        static readonly int[] ChannelBudget = { 0, 2, 30, 8, 8, 4 };

        readonly AudioVoice[] voices = new AudioVoice[Size];
        readonly List<AudioVoice> adopted = new List<AudioVoice>(16);

        public VoicePool(Transform parent)
        {
            for (int i = 0; i < Size; i++)
            {
                var go = new GameObject("Voice" + i.ToString("00"));
                go.transform.SetParent(parent, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.dopplerLevel = 0f;
                src.rolloffMode = AudioRolloffMode.Custom;
                var lpf = go.AddComponent<AudioLowPassFilter>();
                lpf.cutoffFrequency = 22000f;
                lpf.lowpassResonanceQ = 1f;
                lpf.enabled = false;
                voices[i] = new AudioVoice(i, src, lpf, false);
            }
        }

        // ---------------------------------------------------------------- starting and stopping

        public int Begin(AudioClip clip, SoundPreset preset, Transform follow, Vector3 offset, float volume, float pitch,
                         bool loop, int kind, Transform ignore)
        {
            if (clip == null) return -1;
            // A one-shot nobody would hear is not worth a voice; a loop may start silent and rise.
            if (float.IsNaN(volume) || (!loop && volume <= 0.0001f)) return -1;
            PresetSettings s = SoundPresets.Get(preset);
            AudioVoice v = Acquire(s.priority, s.channel);
            if (v == null) return -1;

            v.generation++;
            v.busy = true;
            v.loop = loop;
            v.preset = preset;
            v.settings = s;
            v.kind = kind;
            v.baseVolume = volume;
            v.fade = 1f;
            v.fadeTarget = 1f;
            v.fadeRate = 0f;
            v.stopWhenSilent = false;
            v.follow = follow;
            v.hasFollow = follow != null;
            v.offset = offset;
            v.ignore = ignore;
            v.startedAt = Time.unscaledTime;
            v.nextOcclusion = 0f;

            AudioSource src = v.src;
            src.clip = clip;
            src.loop = loop;
            src.pitch = pitch;
            src.priority = s.priority;
            src.ignoreListenerPause = s.ignorePause;
            src.spatialBlend = s.spatial ? 1f : 0f;
            if (s.spatial)
            {
                if (v.curvePreset != (int)preset)
                {
                    src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, SoundPresets.Curve(preset));
                    v.curvePreset = (int)preset;
                }
                src.minDistance = s.minDistance;
                src.maxDistance = s.maxDistance;
                src.spread = s.spread;
                Place(v);
                // Right from the first frame: a line of hers from behind a wall does not start
                // clear and then muffle.
                Judge(v);
                v.occlusionVolume = v.targetVolume;
                v.occlusionCutoff = v.targetCutoff;
            }
            else
            {
                v.occlusionVolume = 1f;
                v.occlusionCutoff = 22000f;
                v.walls = 0;
            }
            ApplyFilter(v);
            ApplyVolume(v);
            src.Play();
            return v.Handle;
        }

        public void FadeIn(int handle, float seconds)
        {
            AudioVoice v = Live(handle);
            if (v == null || seconds <= 0f) return;
            v.fade = 0f;
            v.fadeTarget = 1f;
            v.fadeRate = 1f / seconds;
            ApplyVolume(v);
        }

        public void Stop(AudioVoice v, float fadeSeconds)
        {
            if (v == null) return;
            if (fadeSeconds <= 0f)
            {
                v.src.Stop();
                Release(v);
                return;
            }
            v.fadeTarget = 0f;
            v.fadeRate = 1f / fadeSeconds;
            v.stopWhenSilent = true;
        }

        public AudioVoice Live(int handle)
        {
            if (handle < 0) return null;
            int i = handle & 0xFF;
            if (i >= Size) return null;
            AudioVoice v = voices[i];
            return v.busy && v.Handle == handle ? v : null;
        }

        // A scene change: what was sounding belonged to the scene that is gone. The interface
        // carries on.
        public void StopAllWorld()
        {
            for (int i = 0; i < Size; i++)
            {
                AudioVoice v = voices[i];
                if (!v.busy || v.preset == SoundPreset.Ui) continue;
                v.src.Stop();
                Release(v);
            }
            adopted.Clear();
        }

        public void Adopt(AudioSource src, Vector3 position, AudioChannel channel)
        {
            AudioVoice v = null;
            for (int i = 0; i < adopted.Count; i++)
                if (adopted[i].src == src) { v = adopted[i]; break; }
            if (v == null)
            {
                var lpf = src.GetComponent<AudioLowPassFilter>();
                if (lpf == null) lpf = src.gameObject.AddComponent<AudioLowPassFilter>();
                lpf.enabled = false;
                v = new AudioVoice(-1, src, lpf, true);
                adopted.Add(v);
            }
            v.busy = true;
            v.baseVolume = src.volume;
            v.settings = SoundPresets.Get(SoundPreset.Impact);
            v.settings.channel = channel;
            v.settings.occlusion = 0.5f;
            v.preset = SoundPreset.Impact;
            v.offset = position;
            v.hasFollow = false;
            v.startedAt = Time.unscaledTime;
            v.fade = v.fadeTarget = 1f;
            Place(v);
            Judge(v);
            v.occlusionVolume = v.targetVolume;
            v.occlusionCutoff = v.targetCutoff;
            ApplyFilter(v);
            ApplyVolume(v);
        }

        AudioVoice Acquire(int priority, AudioChannel channel)
        {
            int inChannel = 0;
            AudioVoice free = null;
            for (int i = 0; i < Size; i++)
            {
                AudioVoice v = voices[i];
                if (!v.busy) { if (free == null) free = v; }
                else if (v.settings.channel == channel) inChannel++;
            }
            int budget = ChannelBudget[Mathf.Clamp((int)channel, 0, ChannelBudget.Length - 1)];
            bool full = budget > 0 && inChannel >= budget;
            if (!full && free != null) return free;

            AudioVoice best = null;
            for (int i = 0; i < Size; i++)
            {
                AudioVoice v = voices[i];
                if (!v.busy || v.loop || v.settings.priority < priority) continue;
                if (full && v.settings.channel != channel) continue;
                if (best == null || v.settings.priority > best.settings.priority ||
                    (v.settings.priority == best.settings.priority && v.startedAt < best.startedAt))
                    best = v;
            }
            if (best == null) return null;
            best.src.Stop();
            Release(best);
            return best;
        }

        static void Release(AudioVoice v)
        {
            v.busy = false;
            v.follow = null;
            v.ignore = null;
            v.hasFollow = false;
            if (!v.external)
            {
                v.src.clip = null;
                v.lpf.enabled = false;
            }
        }

        // ---------------------------------------------------------------- every frame

        public void Update(bool paused)
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            float k = 1f - Mathf.Exp(-dt / 0.15f);
            int checks = 0;

            for (int i = 0; i < Size; i++)
            {
                AudioVoice v = voices[i];
                if (!v.busy) continue;
                if (!v.loop && !paused && !v.src.isPlaying && now - v.startedAt > 0.05f)
                {
                    Release(v);
                    continue;
                }
                if (v.fade != v.fadeTarget) v.fade = Mathf.MoveTowards(v.fade, v.fadeTarget, v.fadeRate * dt);
                if (v.stopWhenSilent && v.fade <= 0.0001f)
                {
                    v.src.Stop();
                    Release(v);
                    continue;
                }
                if (v.settings.spatial)
                {
                    if (v.hasFollow && v.follow == null)
                    {
                        // What it followed is gone (a thrown bottle that broke, a reload): a
                        // loop stops, a one-shot finishes where it was.
                        if (v.loop) { v.src.Stop(); Release(v); continue; }
                        v.hasFollow = false;
                        v.offset = v.world;
                    }
                    Place(v);
                    if (checks < OcclusionChecksPerFrame && now >= v.nextOcclusion)
                    {
                        checks++;
                        Judge(v);
                    }
                    Smooth(v, k);
                }
                ApplyVolume(v);
            }

            for (int i = adopted.Count - 1; i >= 0; i--)
            {
                AudioVoice v = adopted[i];
                if (v.src == null) { adopted.RemoveAt(i); continue; }
                if (!v.busy) continue;
                if (!v.src.isPlaying && !paused) { v.busy = false; v.lpf.enabled = false; continue; }
                Place(v);
                if (checks < OcclusionChecksPerFrame && now >= v.nextOcclusion)
                {
                    checks++;
                    Judge(v);
                }
                Smooth(v, k);
                ApplyVolume(v);
            }
        }

        static void Place(AudioVoice v)
        {
            v.world = v.hasFollow && v.follow != null ? v.follow.TransformPoint(v.offset) : v.offset;
            v.tr.position = Ears.Map(v.world, out v.ear);
        }

        // Walls between the sound and its ears, turned into a volume and a cutoff. Each wall
        // takes a share of the volume (the preset's occlusion says how much) and always takes
        // the top off: through a wall you hear the thump of a voice, not its consonants.
        static void Judge(AudioVoice v)
        {
            v.nextOcclusion = Time.unscaledTime + OcclusionInterval;
            float s = v.settings.occlusion;
            float max = v.settings.maxDistance > 0f ? v.settings.maxDistance : 40f;
            int walls = s > 0f && (v.world - v.ear).sqrMagnitude <= max * max
                ? Occlusion.WallsBetween(v.ear, v.world, v.ignore)
                : 0;
            v.walls = walls;
            if (walls == 0)
            {
                v.targetVolume = 1f;
                v.targetCutoff = 22000f;
                return;
            }
            v.targetVolume = Mathf.Pow(Mathf.Lerp(1f, 0.4f, s), walls);
            float cutoff = walls == 1 ? 1500f : walls == 2 ? 750f : 450f;
            v.targetCutoff = cutoff * (2f - s);
        }

        static void Smooth(AudioVoice v, float k)
        {
            v.occlusionVolume += (v.targetVolume - v.occlusionVolume) * k;
            v.occlusionCutoff += (v.targetCutoff - v.occlusionCutoff) * k;
            ApplyFilter(v);
        }

        static void ApplyFilter(AudioVoice v)
        {
            bool on = v.occlusionCutoff < 18000f;
            if (v.lpf.enabled != on) v.lpf.enabled = on;
            if (on) v.lpf.cutoffFrequency = v.occlusionCutoff;
        }

        static void ApplyVolume(AudioVoice v)
        {
            v.src.volume = Mathf.Clamp01(v.baseVolume * v.fade * v.occlusionVolume * GameAudio.Gain(v.settings.channel));
        }

        // ---------------------------------------------------------------- counting

        public int CountActive(SoundPreset preset)
        {
            int n = 0;
            for (int i = 0; i < Size; i++)
                if (voices[i].busy && voices[i].preset == preset && voices[i].fadeTarget > 0f) n++;
            return n;
        }

        public int CountBusy()
        {
            int n = 0;
            for (int i = 0; i < Size; i++) if (voices[i].busy) n++;
            return n;
        }

        public int CountAdopted()
        {
            int n = 0;
            for (int i = 0; i < adopted.Count; i++) if (adopted[i].busy) n++;
            return n;
        }

        public bool IsAdopted(AudioSource src)
        {
            for (int i = 0; i < adopted.Count; i++)
                if (adopted[i].src == src && adopted[i].busy) return true;
            return false;
        }

        public AudioVoice At(int i) => voices[i];
        public int AdoptedCount => adopted.Count;
        public AudioVoice AdoptedAt(int i) => adopted[i];
    }
}
