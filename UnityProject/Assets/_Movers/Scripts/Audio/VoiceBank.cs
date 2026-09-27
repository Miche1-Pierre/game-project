using System.Collections.Generic;
using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // Everything said or grunted, as clips.
    //
    // - The crew's efforts (lift, throw, oof, ahh, burp...), three takes each, per player: each
    //   player has his own pitch, so a spectator hears who dropped the fridge.
    // - The grandmother's small noises (sigh, hmm, tsk, gasp) and her hums.
    // - Her lines. Each written line becomes babble (VoiceSynth, "Animalese"), rendered the
    //   first time she says it in that mood and language, on a worker, in a few milliseconds.
    //   Kept in a small cache: her lines repeat, and a line already heard costs nothing.
    public static class VoiceBank
    {
        public const int Takes = 3;
        const int MaxPlayers = 4;
        const int EffortKinds = (int)EffortKind.Gasp + 1;
        const int Hums = 4;
        const int CacheSize = 40;

        sealed class Line
        {
            public AudioClip clip;
            public bool pending;
            public float lastUsed;
        }

        static AudioClip[] efforts;          // [player][kind][take], flattened
        static bool[] playerRequested;
        static AudioClip[] grandma;          // her efforts, then her hums
        static bool grandmaRequested;
        static SynthQueue<int> clipQueue;
        static SynthQueue<string> lineQueue;
        static Dictionary<string, Line> lines;
        static readonly List<string> evict = new List<string>(8);

        static void Init()
        {
            if (clipQueue != null) return;
            efforts = new AudioClip[MaxPlayers * EffortKinds * Takes];
            playerRequested = new bool[MaxPlayers];
            grandma = new AudioClip[EffortKinds + Hums];
            grandmaRequested = false;
            clipQueue = new SynthQueue<int>();
            lineQueue = new SynthQueue<string>();
            lines = new Dictionary<string, Line>(CacheSize + 8);
            Application.quitting -= DestroyAll;
            Application.quitting += DestroyAll;
        }

        // ---------------------------------------------------------------- the crew

        public static void EnsurePlayer(int player)
        {
            Init();
            if (player < 0 || player >= MaxPlayers || playerRequested[player]) return;
            playerRequested[player] = true;
            clipQueue.RunBatch(q =>
            {
                var profile = VoiceProfile.Crew(player);
                for (int k = 0; k < EffortKinds; k++)
                    for (int t = 0; t < Takes; t++)
                    {
                        uint seed = VoiceSeeds.CrewEffort(player, k, t);
                        var segs = Babble.Effort((EffortKind)k, new Rng(seed));
                        var r = new Rendered { samples = VoiceSynth.Render(segs, profile, seed), rate = profile.rate };
                        q.Deliver(EffortKey(player, k, t), r);
                    }
            });
        }

        static int EffortKey(int player, int kind, int take) => (player * EffortKinds + kind) * Takes + take;

        public static AudioClip Effort(int player, EffortKind kind)
        {
            if (efforts == null || player < 0 || player >= MaxPlayers) return null;
            int take = Random.Range(0, Takes);
            for (int i = 0; i < Takes; i++)
            {
                var c = efforts[EffortKey(player, (int)kind, (take + i) % Takes)];
                if (c != null) return c;
            }
            return null;
        }

        // ---------------------------------------------------------------- the grandmother

        public static void EnsureGrandma()
        {
            Init();
            if (grandmaRequested) return;
            grandmaRequested = true;
            clipQueue.RunBatch(q =>
            {
                var profile = VoiceProfile.Grandma();
                for (int k = 0; k < EffortKinds; k++)
                {
                    var kind = (EffortKind)k;
                    // Only what she makes; the crew's noises in her voice would be odd.
                    if (kind != EffortKind.Sigh && kind != EffortKind.Hmm && kind != EffortKind.Tsk && kind != EffortKind.Gasp) continue;
                    uint seed = VoiceSeeds.GrandmaEffort(k);
                    var r = new Rendered { samples = VoiceSynth.Render(Babble.Effort(kind, new Rng(seed)), profile, seed), rate = profile.rate };
                    q.Deliver(-1 - k, r);
                }
                for (int h = 0; h < Hums; h++)
                {
                    uint seed = VoiceSeeds.Hum(h);
                    var r = new Rendered { samples = VoiceSynth.Render(Babble.Hum(new Rng(seed)), profile, seed + 1), rate = profile.rate };
                    q.Deliver(-100 - h, r);
                }
            });
        }

        public static AudioClip GrandmaEffort(EffortKind kind) => grandma != null ? grandma[(int)kind] : null;

        public static AudioClip GrandmaHum()
        {
            if (grandma == null) return null;
            int start = Random.Range(0, Hums);
            for (int i = 0; i < Hums; i++)
            {
                var c = grandma[EffortKinds + (start + i) % Hums];
                if (c != null) return c;
            }
            return null;
        }

        // ---------------------------------------------------------------- her lines

        public static string LineKey(string text, VoiceMood mood, bool french)
        {
            return (char)('0' + (int)mood) + (french ? "F" : "E") + text;
        }

        // Starts rendering a line unless it is cached or on its way. Cheap to call again.
        public static void RequestLine(string key, string text, VoiceMood mood, bool french)
        {
            Init();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(text)) return;
            if (lines.TryGetValue(key, out Line l))
            {
                l.lastUsed = Time.unscaledTime;
                return;
            }
            lines[key] = new Line { pending = true, lastUsed = Time.unscaledTime };
            uint seed = VoiceSeeds.Line(text);
            lineQueue.Run(key, () =>
            {
                var profile = VoiceProfile.Grandma();
                var segs = Babble.FromText(text, mood, french, seed);
                return new Rendered { samples = VoiceSynth.Render(segs, profile, seed), rate = profile.rate };
            });
            TrimCache();
        }

        public static bool TryGetLine(string key, out AudioClip clip)
        {
            clip = null;
            if (lines == null || key == null || !lines.TryGetValue(key, out Line l) || l.clip == null) return false;
            l.lastUsed = Time.unscaledTime;
            clip = l.clip;
            return true;
        }

        public static bool IsLinePending(string key) => lines != null && key != null && lines.TryGetValue(key, out Line l) && l.pending;

        public static int CachedLines => lines != null ? lines.Count : 0;

        // Least recently used lines go first, never one used in the last 15 s (it may still be
        // playing, and destroying a clip stops its voice).
        static void TrimCache()
        {
            if (lines.Count <= CacheSize) return;
            evict.Clear();
            float now = Time.unscaledTime;
            while (lines.Count - evict.Count > CacheSize)
            {
                string oldest = null;
                float t = float.MaxValue;
                foreach (var kv in lines)
                {
                    if (kv.Value.pending || now - kv.Value.lastUsed < 15f || evict.Contains(kv.Key)) continue;
                    if (kv.Value.lastUsed < t) { t = kv.Value.lastUsed; oldest = kv.Key; }
                }
                if (oldest == null) break;
                evict.Add(oldest);
            }
            for (int i = 0; i < evict.Count; i++)
            {
                SynthClips.Destroy(lines[evict[i]].clip);
                lines.Remove(evict[i]);
            }
        }

        // ---------------------------------------------------------------- main thread

        public static void Pump(int budget)
        {
            if (clipQueue == null) return;
            // Her lines first: she is talking now, the efforts can wait a frame.
            while (budget > 0 && lineQueue.TryTake(out string key, out Rendered r, out string error))
            {
                budget--;
                if (!lines.TryGetValue(key, out Line l)) continue;
                l.pending = false;
                if (error != null) { Debug.LogWarning("[VoiceBank] line: " + error); continue; }
                l.clip = SynthClips.Make("VO_Line", r);
            }
            while (budget > 0 && clipQueue.TryTake(out int key, out Rendered r, out string error))
            {
                budget--;
                if (error != null) { Debug.LogWarning("[VoiceBank] " + error); continue; }
                if (key >= 0)
                {
                    if (key < efforts.Length) efforts[key] = SynthClips.Make("VO_Effort_" + key, r);
                }
                else if (key <= -100)
                {
                    int h = -100 - key;
                    if (h < Hums) grandma[EffortKinds + h] = SynthClips.Make("VO_GrandmaHum_" + h, r);
                }
                else
                {
                    int k = -1 - key;
                    if (k < EffortKinds) grandma[k] = SynthClips.Make("VO_Grandma_" + (EffortKind)k, r);
                }
            }
        }

        static void DestroyAll()
        {
            if (efforts != null) for (int i = 0; i < efforts.Length; i++) SynthClips.Destroy(efforts[i]);
            if (grandma != null) for (int i = 0; i < grandma.Length; i++) SynthClips.Destroy(grandma[i]);
            if (lines != null) foreach (var kv in lines) SynthClips.Destroy(kv.Value.clip);
            clipQueue = null;
            lineQueue = null;
            lines = null;
            efforts = null;
            grandma = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clipQueue = null;
            lineQueue = null;
            lines = null;
            efforts = null;
            grandma = null;
            playerRequested = null;
            grandmaRequested = false;
        }
    }
}
