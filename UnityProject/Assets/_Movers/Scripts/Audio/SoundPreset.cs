using UnityEngine;

namespace Movers
{
    // How a sound behaves in the world, chosen by the caller in one word: which volume
    // slider it answers to, whether it is 2D or placed in the house, how far it carries, how
    // much a wall muffles it, and how important it is when the mixer runs out of voices.
    public enum SoundPreset
    {
        Ui,            // clicks and hovers: 2D, keeps playing in the pause menu
        Music,         // 2D, keeps playing in the pause menu
        Stinger,       // "she saw that", the jingles: 2D, effects slider, never cut
        AmbienceBed,   // wind, room tone: 2D loops
        Ambience3D,    // birds, a car on the road: far, fully muffled by walls
        Footstep,      // the crew's boots
        GrandmaStep,   // her slippers: soft, but heard through a wall on purpose
        CrewVoice,     // grunts, "ahh", burps
        GrandmaVoice,  // her babble: carries, and through walls
        Handling,      // pockets, keys, gulps, smoking: small, close
        Door,          // latches, creaks, sashes
        Engine,        // the truck
        Activity,      // her TV, her pot, the fire
        Impact,        // things dropped, thrown, knocked over
        Count
    }

    public struct PresetSettings
    {
        public AudioChannel channel;
        public bool spatial;
        public float minDistance;    // full volume inside this
        public float maxDistance;    // silent from here
        public float occlusion;      // 0: walls do nothing, 1: a wall halves it and muffles hard
        public int priority;         // Unity's: lower is more important
        public bool ignorePause;     // keeps playing while the game is paused
        public float spread;         // degrees; softens the pan for split screen
    }

    public static class SoundPresets
    {
        static readonly PresetSettings[] table = Build();
        static readonly AnimationCurve[] curves = new AnimationCurve[(int)SoundPreset.Count];

        public static PresetSettings Get(SoundPreset p) => table[(int)p];

        // The distance curve of a preset, built once. Unity's own curves both misbehave in a
        // house: Linear is too loud across the room and cuts dead at the end, Logarithmic
        // never reaches zero, so a pan dropped in the cellar is heard, faintly, in the garden.
        // This one is the inverse distance law near the source (what ears expect) folded
        // smoothly to silence at maxDistance.
        public static AnimationCurve Curve(SoundPreset p)
        {
            int i = (int)p;
            if (curves[i] == null) curves[i] = Rolloff(table[i].minDistance, table[i].maxDistance);
            return curves[i];
        }

        // Custom rolloff curves are read with distance / maxDistance on the x axis.
        public static AnimationCurve Rolloff(float min, float max)
        {
            const int n = 16;
            var keys = new Keyframe[n + 1];
            float r = Mathf.Clamp01(min / Mathf.Max(0.01f, max));
            for (int k = 0; k <= n; k++)
            {
                // Squared spacing: most keys near the source, where the curve bends most.
                float x = (k / (float)n);
                x *= x;
                keys[k] = new Keyframe(x, Gain(x, r));
            }
            for (int k = 0; k <= n; k++)
            {
                int a = Mathf.Max(0, k - 1), b = Mathf.Min(n, k + 1);
                float slope = (keys[b].value - keys[a].value) / Mathf.Max(1e-5f, keys[b].time - keys[a].time);
                keys[k].inTangent = slope;
                keys[k].outTangent = slope;
            }
            return new AnimationCurve(keys);
        }

        static float Gain(float x, float r)
        {
            if (x <= r) return 1f;
            float g = Mathf.Pow(r / x, 0.9f);
            // The last 40 % fades what is left to nothing.
            float t = Mathf.Clamp01((x - 0.6f) / 0.4f);
            return g * (1f - t * t * (3f - 2f * t));
        }

        static PresetSettings[] Build()
        {
            var t = new PresetSettings[(int)SoundPreset.Count];
            t[(int)SoundPreset.Ui] = Flat(AudioChannel.Ui, 4, true);
            t[(int)SoundPreset.Music] = Flat(AudioChannel.Music, 0, true);
            t[(int)SoundPreset.Stinger] = Flat(AudioChannel.Sfx, 6, false);
            t[(int)SoundPreset.AmbienceBed] = Flat(AudioChannel.Ambience, 20, false);
            t[(int)SoundPreset.Ambience3D] = World(AudioChannel.Ambience, 6f, 70f, 1f, 200);
            t[(int)SoundPreset.Footstep] = World(AudioChannel.Sfx, 1.4f, 24f, 0.85f, 150);
            t[(int)SoundPreset.GrandmaStep] = World(AudioChannel.Sfx, 1.6f, 26f, 0.4f, 110);
            t[(int)SoundPreset.CrewVoice] = World(AudioChannel.Voice, 1.6f, 30f, 0.6f, 100);
            t[(int)SoundPreset.GrandmaVoice] = World(AudioChannel.Voice, 2.5f, 36f, 0.4f, 40);
            t[(int)SoundPreset.Handling] = World(AudioChannel.Sfx, 1.2f, 16f, 0.8f, 140);
            t[(int)SoundPreset.Door] = World(AudioChannel.Sfx, 1.6f, 30f, 0.6f, 120);
            t[(int)SoundPreset.Engine] = World(AudioChannel.Sfx, 4f, 70f, 0.5f, 60);
            t[(int)SoundPreset.Activity] = World(AudioChannel.Sfx, 1.6f, 18f, 0.8f, 170);
            t[(int)SoundPreset.Impact] = World(AudioChannel.Sfx, 1.6f, 30f, 0.7f, 130);
            return t;
        }

        static PresetSettings Flat(AudioChannel c, int priority, bool ignorePause) =>
            new PresetSettings { channel = c, spatial = false, priority = priority, ignorePause = ignorePause };

        static PresetSettings World(AudioChannel c, float min, float max, float occlusion, int priority) =>
            new PresetSettings { channel = c, spatial = true, minDistance = min, maxDistance = max, occlusion = occlusion, priority = priority, spread = 40f };
    }
}
