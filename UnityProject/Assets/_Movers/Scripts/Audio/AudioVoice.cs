using UnityEngine;

namespace Movers
{
    // One AudioSource of the AudioDirector's pool, and what it is doing: which preset, where it
    // is (a point, or a transform it follows), how loud its owner asked for, its fade, and how
    // muffled the walls make it. Also used, with external = true, for a source someone else
    // owns and hands over for placement and volume (ImpactAudio's voices, AudioDirector.Adopt).
    internal sealed class AudioVoice
    {
        public readonly int index;
        public readonly AudioSource src;
        public readonly AudioLowPassFilter lpf;
        public readonly Transform tr;
        public readonly bool external;

        public int generation;
        public bool busy;
        public bool loop;
        public SoundPreset preset;
        public PresetSettings settings;
        public int curvePreset = -1;       // which preset's rolloff curve the source has now
        public int kind = -1;              // the SfxKind, for the debug overlay and the tests; -1 for a clip

        public float baseVolume = 1f;
        public float fade = 1f, fadeTarget = 1f, fadeRate;
        public bool stopWhenSilent;

        public Transform follow;
        public bool hasFollow;
        public Vector3 offset;             // local to follow, or a world position
        public Transform ignore;           // its own body, for the occlusion ray
        public Vector3 world;              // where the sound really is
        public Vector3 ear;                // the head it is heard from

        public int walls;
        public float occlusionVolume = 1f, occlusionCutoff = 22000f;
        public float targetVolume = 1f, targetCutoff = 22000f;
        public float nextOcclusion;
        public float startedAt;

        public AudioVoice(int index, AudioSource src, AudioLowPassFilter lpf, bool external)
        {
            this.index = index;
            this.src = src;
            this.lpf = lpf;
            this.tr = src.transform;
            this.external = external;
        }

        public int Handle => (generation << 8) | index;

        public string Label
        {
            get
            {
                if (kind >= 0) return ((AudioSynth.SfxKind)kind).ToString();
                if (external) return "ImpactAudio";
                return src != null && src.clip != null ? src.clip.name : "-";
            }
        }
    }
}
