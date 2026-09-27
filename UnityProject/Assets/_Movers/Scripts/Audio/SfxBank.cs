using System;
using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // Every one-shot and loop of the game (SfxKind), synthesised once per Play session and
    // kept as AudioClips. Rendered on a worker in one pass, the interface sounds first (the
    // menu needs them at once), then what the crew makes most often, then the rest; the main
    // thread turns a few results into clips each frame (Pump). About 16 MB of samples, ready
    // a second or two after the game starts.
    //
    // Several variants per kind; Pick never plays the same one twice in a row, which is what
    // stops six footsteps from sounding like one sample on repeat.
    public static class SfxBank
    {
        const int MaxVariants = 8;

        static AudioClip[][] clips;
        static int[] lastVariant;
        static SynthQueue<int> queue;
        static bool started;
        static int loaded, total;

        public static bool Started => started;
        public static bool AllLoaded => started && loaded >= total;
        public static float Progress => total > 0 ? loaded / (float)total : 0f;
        public static int LoadedCount => loaded;
        public static int TotalCount => total;

        // What the game needs first, in order. Everything else follows in enum order.
        static readonly SfxKind[] First =
        {
            SfxKind.UiHover, SfxKind.UiClick, SfxKind.UiConfirm, SfxKind.UiBack, SfxKind.UiOpen, SfxKind.UiClose,
            SfxKind.UiToast, SfxKind.UiDenied, SfxKind.UiTick, SfxKind.UiCoins, SfxKind.UiSlider,
            SfxKind.StepWood, SfxKind.StepStone, SfxKind.StepGrass, SfxKind.StepCarpet, SfxKind.StepMetal,
            SfxKind.SlipperWood, SfxKind.SlipperStone, SfxKind.SlipperGrass, SfxKind.SlipperCarpet,
            SfxKind.WindLoop, SfxKind.RoomToneLoop, SfxKind.BirdCall,
            SfxKind.ImpactWood, SfxKind.ImpactSoft, SfxKind.ImpactHeavy, SfxKind.LatchClick, SfxKind.DoorCreak,
        };

        public static void EnsureStarted()
        {
            if (started) return;
            started = true;
            int kinds = (int)SfxKind.Count;
            clips = new AudioClip[kinds][];
            lastVariant = new int[kinds];
            total = 0;
            for (int k = 0; k < kinds; k++)
            {
                int n = Mathf.Clamp(Sfx.Variants((SfxKind)k), 1, MaxVariants);
                clips[k] = new AudioClip[n];
                lastVariant[k] = -1;
                total += n;
            }
            loaded = 0;
            queue = new SynthQueue<int>();
            queue.RunBatch(RenderAll);
            Application.quitting -= DestroyAll;
            Application.quitting += DestroyAll;
        }

        // Worker thread: no Unity calls in here.
        static void RenderAll(SynthQueue<int> q)
        {
            int kinds = (int)SfxKind.Count;
            var done = new bool[kinds];
            for (int i = 0; i < First.Length; i++)
            {
                RenderKind(q, First[i]);
                done[(int)First[i]] = true;
            }
            for (int k = 0; k < kinds; k++)
                if (!done[k]) RenderKind(q, (SfxKind)k);
        }

        static void RenderKind(SynthQueue<int> q, SfxKind kind)
        {
            int n = Math.Min(Sfx.Variants(kind), MaxVariants);
            for (int v = 0; v < n; v++)
            {
                int key = (int)kind * MaxVariants + v;
                try { q.Deliver(key, Sfx.Render(kind, v)); }
                catch (Exception e) { q.DeliverError(key, kind + ": " + e.Message); }
            }
        }

        // Main thread, once a frame: at most `budget` new clips (each is a copy into the audio
        // engine; a handful per frame is invisible, all 170 in one frame is not).
        public static void Pump(int budget)
        {
            if (!started || queue == null) return;
            while (budget-- > 0 && queue.TryTake(out int key, out Rendered r, out string error))
            {
                int k = key / MaxVariants, v = key % MaxVariants;
                if (error != null)
                {
                    Debug.LogWarning("[SfxBank] " + error);
                    loaded++;
                    continue;
                }
                if (k < 0 || k >= clips.Length || v >= clips[k].Length) continue;
                SynthClips.Destroy(clips[k][v]);
                clips[k][v] = SynthClips.Make("SFX_" + (SfxKind)k + "_" + v, r);
                loaded++;
            }
        }

        public static bool Has(SfxKind kind)
        {
            int k = (int)kind;
            if (!started || k < 0 || k >= clips.Length) return false;
            var set = clips[k];
            for (int i = 0; i < set.Length; i++) if (set[i] != null) return true;
            return false;
        }

        // A variant of this kind, never the one played last. Null while it is still rendering.
        public static AudioClip Pick(SfxKind kind)
        {
            int k = (int)kind;
            if (!started || k < 0 || k >= clips.Length) return null;
            var set = clips[k];
            int n = set.Length;
            if (n == 1) return set[0];
            int start = UnityEngine.Random.Range(0, n);
            for (int i = 0; i < n; i++)
            {
                int v = (start + i) % n;
                if (v == lastVariant[k] || set[v] == null) continue;
                lastVariant[k] = v;
                return set[v];
            }
            return set[Mathf.Max(0, lastVariant[k])];
        }

        public static AudioClip Get(SfxKind kind, int variant)
        {
            int k = (int)kind;
            if (!started || k < 0 || k >= clips.Length) return null;
            var set = clips[k];
            return set[((variant % set.Length) + set.Length) % set.Length];
        }

        public static int VariantCount(SfxKind kind)
        {
            int k = (int)kind;
            return started && k >= 0 && k < clips.Length ? clips[k].Length : 1;
        }

        static void DestroyAll()
        {
            if (clips != null)
                for (int k = 0; k < clips.Length; k++)
                    for (int v = 0; v < clips[k].Length; v++)
                    {
                        SynthClips.Destroy(clips[k][v]);
                        clips[k][v] = null;
                    }
            started = false;
            queue = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // The clips of a previous session were destroyed when it quit; start clean.
            clips = null;
            lastVariant = null;
            queue = null;
            started = false;
            loaded = total = 0;
        }
    }
}
