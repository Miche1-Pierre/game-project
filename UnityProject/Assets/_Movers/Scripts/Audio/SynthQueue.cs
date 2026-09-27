using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // Runs synth recipes on the thread pool and hands the samples back to the main thread.
    //
    // The recipes are plain C# with no Unity call in them (Scripts/Audio/Synth), so they are
    // safe off the main thread; only turning samples into an AudioClip must happen on it
    // (SynthClips.Make, from the owner's Pump). The whole bank takes a second or two of CPU:
    // on the main thread that is a visible hitch at load, here it is nothing.
    //
    // Each owner keeps its own queue. When statics are reset (a new Play session with domain
    // reload off) the owner makes a new queue, and whatever the old tasks still deliver lands
    // in the old one, which nobody reads.
    public sealed class SynthQueue<TKey>
    {
        struct Done
        {
            public TKey key;
            public Rendered rendered;
            public string error;
        }

        readonly ConcurrentQueue<Done> done = new ConcurrentQueue<Done>();
        int running;

        public int Running => running;

        public void Run(TKey key, Func<Rendered> job)
        {
            System.Threading.Interlocked.Increment(ref running);
            Task.Run(() =>
            {
                var d = new Done { key = key };
                try { d.rendered = job(); }
                catch (Exception e) { d.error = e.GetType().Name + ": " + e.Message; }
                done.Enqueue(d);
                System.Threading.Interlocked.Decrement(ref running);
            });
        }

        // Several results from one job (a whole bank rendered in one pass): the job calls this
        // for each as it finishes it, from the worker thread.
        public void Deliver(TKey key, in Rendered rendered)
        {
            done.Enqueue(new Done { key = key, rendered = rendered });
        }

        public void DeliverError(TKey key, string error)
        {
            done.Enqueue(new Done { key = key, error = error });
        }

        public void RunBatch(Action<SynthQueue<TKey>> job)
        {
            System.Threading.Interlocked.Increment(ref running);
            Task.Run(() =>
            {
                try { job(this); }
                catch (Exception e) { done.Enqueue(new Done { error = e.GetType().Name + ": " + e.Message }); }
                System.Threading.Interlocked.Decrement(ref running);
            });
        }

        public bool TryTake(out TKey key, out Rendered rendered, out string error)
        {
            if (done.TryDequeue(out Done d))
            {
                key = d.key;
                rendered = d.rendered;
                error = d.error;
                return true;
            }
            key = default;
            rendered = default;
            error = null;
            return false;
        }
    }

    public static class SynthClips
    {
        // An AudioClip from rendered samples. Kept out of the scene (DontSave) and never
        // unloaded behind our back when a scene changes; the banks destroy their own clips
        // when Play mode ends, so nothing leaks from one session to the next.
        public static AudioClip Make(string name, in Rendered r)
        {
            if (r.samples == null || r.Frames <= 0 || r.rate <= 0) return null;
            var clip = AudioClip.Create(name, r.Frames, r.Channels, r.rate, false);
            clip.SetData(r.samples, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        public static void Destroy(AudioClip clip)
        {
            if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
