using Movers.AudioSynth;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // The music, owned by the AudioDirector.
    //
    // - The menu (and the loading screen after it) plays "Moving Day" in a loop.
    // - The house plays "Tea Time", but not all the time: it comes in a few seconds after the
    //   arrival, plays through once or twice, then leaves room for the house for a minute or
    //   two, and comes back. Music that never stops becomes wallpaper; music that returns is
    //   noticed (the Animal Crossing and Minecraft way).
    // - It steps aside under the grandmother's voice (a gentle duck), stops for the end of the
    //   run (the jingle has the floor) and for the police (the siren has it).
    //
    // Each piece is synthesised on a worker the first time it is wanted (half a second of
    // CPU, off the main thread) and the other one is dropped when it is not playing, so only
    // one piece (about 11 MB of samples) is in memory at a time.
    public sealed class MusicPlayer
    {
        const float MenuLevel = 0.85f;
        const float HouseLevel = 0.55f;

        readonly AudioSource[] sources = new AudioSource[2];
        readonly float[] level = new float[2];        // target level of each source
        readonly float[] current = new float[2];
        readonly float[] fadeRate = new float[2];
        readonly MusicTrack[] trackOf = { MusicTrack.Count, MusicTrack.Count };
        readonly AudioClip[] clips = new AudioClip[(int)MusicTrack.Count];
        readonly bool[] requested = new bool[(int)MusicTrack.Count];
        SynthQueue<int> queue = new SynthQueue<int>();

        MusicTrack wanted = MusicTrack.Count;   // Count: no music
        bool comeAndGo;
        float nextStart, stopAt;
        float duck = 1f;
        bool silenced;                          // the run ended, or the police are coming

        public float Duck => duck;
        public MusicTrack Wanted => wanted;
        public bool IsPlaying(MusicTrack t)
        {
            for (int i = 0; i < 2; i++) if (trackOf[i] == t && sources[i].isPlaying && level[i] > 0f) return true;
            return false;
        }
        public AudioSource SourceOf(MusicTrack t)
        {
            for (int i = 0; i < 2; i++) if (trackOf[i] == t) return sources[i];
            return null;
        }

        public MusicPlayer(Transform parent)
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Music" + i);
                go.transform.SetParent(parent, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.priority = 0;
                s.ignoreListenerPause = true;
                s.volume = 0f;
                sources[i] = s;
            }
            Application.quitting += DestroyClips;
        }

        public void OnScene(Scene scene, bool hasCrew)
        {
            silenced = false;
            string name = scene.name;
            if (name == SceneFlow.MenuScene || name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Want(MusicTrack.Menu, false);
            }
            else if (hasCrew)
            {
                // Let the arrival breathe: birds, the truck, her greeting, then the music.
                Want(MusicTrack.House, true);
                nextStart = Time.unscaledTime + Random.Range(8f, 14f);
                FadeAll(1.5f);
            }
            else
            {
                Want(MusicTrack.Count, false);
                FadeAll(1.5f);
            }
        }

        void Want(MusicTrack t, bool cycle)
        {
            wanted = t;
            comeAndGo = cycle;
            if (t != MusicTrack.Count) Request(t);
            if (!cycle && t != MusicTrack.Count) nextStart = Time.unscaledTime;
        }

        // The end of the run, or the police: fade out and stay out until the next scene.
        public void Silence(float seconds)
        {
            silenced = true;
            FadeAll(seconds);
        }

        void Request(MusicTrack t)
        {
            int i = (int)t;
            if (requested[i] || clips[i] != null) return;
            requested[i] = true;
            queue.Run(i, () => AudioSynth.Music.Render(t));
        }

        public void Update()
        {
            while (queue.TryTake(out int key, out Rendered r, out string error))
            {
                if (error != null) { Debug.LogWarning("[MusicPlayer] " + error); continue; }
                if (key >= 0 && key < clips.Length)
                {
                    SynthClips.Destroy(clips[key]);
                    clips[key] = SynthClips.Make("MUS_" + (MusicTrack)key, r);
                }
            }

            float now = Time.unscaledTime;
            // Under her voice, the music takes a step back.
            float duckTarget = AudioDirector.ActiveCount(SoundPreset.GrandmaVoice) > 0 ? 0.55f : 1f;
            if (AudioDirector.Paused) duckTarget *= 0.6f;
            duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime * (duckTarget < duck ? 2.5f : 0.8f));

            if (!silenced && wanted != MusicTrack.Count && clips[(int)wanted] != null)
            {
                bool playing = IsPlaying(wanted);
                if (!playing && now >= nextStart) StartTrack(wanted, comeAndGo ? 3f : 1.2f);
                if (comeAndGo && playing && now >= stopAt)
                {
                    FadeAll(6f);
                    nextStart = now + 6f + Random.Range(60f, 120f);
                }
            }

            float gain = GameAudio.Gain(AudioChannel.Music) * duck;
            for (int i = 0; i < 2; i++)
            {
                current[i] = Mathf.MoveTowards(current[i], level[i], fadeRate[i] * Time.unscaledDeltaTime);
                AudioSource s = sources[i];
                s.volume = current[i] * gain;
                if (current[i] <= 0f && level[i] <= 0f && s.isPlaying)
                {
                    s.Stop();
                    s.clip = null;
                    MusicTrack gone = trackOf[i];
                    trackOf[i] = MusicTrack.Count;
                    // Only one piece in memory: the one that just stopped goes unless wanted.
                    if (gone != MusicTrack.Count && gone != wanted)
                    {
                        SynthClips.Destroy(clips[(int)gone]);
                        clips[(int)gone] = null;
                        requested[(int)gone] = false;
                    }
                }
            }
        }

        void StartTrack(MusicTrack t, float fadeIn)
        {
            AudioClip clip = clips[(int)t];
            if (clip == null) return;
            // The quieter source takes the new piece; the other one fades out under it.
            int i = current[0] <= current[1] ? 0 : 1;
            int other = 1 - i;
            level[other] = 0f;
            fadeRate[other] = Mathf.Max(fadeRate[other], 1f / 1.5f);
            AudioSource s = sources[i];
            s.clip = clip;
            s.time = 0f;
            s.Play();
            trackOf[i] = t;
            current[i] = 0f;
            level[i] = t == MusicTrack.Menu ? MenuLevel : HouseLevel;
            fadeRate[i] = level[i] / Mathf.Max(0.05f, fadeIn);
            // In the house, once or twice through, then a rest.
            stopAt = Time.unscaledTime + clip.length * (Random.value < 0.5f ? 1f : 2f) - 6f;
        }

        void FadeAll(float seconds)
        {
            for (int i = 0; i < 2; i++)
            {
                level[i] = 0f;
                fadeRate[i] = Mathf.Max(0.01f, current[i] / Mathf.Max(0.05f, seconds));
            }
        }

        // The director is going (Play mode ends): let go of the quitting hook and the clips.
        public void Dispose()
        {
            Application.quitting -= DestroyClips;
            DestroyClips();
        }

        void DestroyClips()
        {
            for (int i = 0; i < clips.Length; i++)
            {
                SynthClips.Destroy(clips[i]);
                clips[i] = null;
                requested[i] = false;
            }
            queue = new SynthQueue<int>();
        }
    }
}
