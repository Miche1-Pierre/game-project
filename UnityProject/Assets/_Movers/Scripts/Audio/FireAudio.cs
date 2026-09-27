using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // A lit fireplace crackles, whoever lit it. Added to each FireplaceFire at runtime.
    [DisallowMultipleComponent]
    public sealed class FireAudio : MonoBehaviour
    {
        FireplaceFire fire;
        int loop = -1;

        void Awake() { fire = GetComponent<FireplaceFire>(); }

        void Update()
        {
            bool lit = fire != null && fire.IsLit;
            bool playing = AudioDirector.IsPlaying(loop);
            if (lit && !playing && SfxBank.Has(SfxKind.FireCrackleLoop))
                loop = AudioDirector.StartLoop(SfxKind.FireCrackleLoop, transform, Vector3.zero, SoundPreset.Activity, 0.7f, 1f, 2f);
            else if (!lit && playing)
            {
                AudioDirector.Stop(loop, 2f);
                loop = -1;
            }
        }

        void OnDisable()
        {
            AudioDirector.Stop(loop, 0f);
            loop = -1;
        }
    }
}
