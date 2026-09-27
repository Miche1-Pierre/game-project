using UnityEngine;

namespace Movers
{
    // One drag on a cigarette, as a cycle that repeats while the button is held. Three things
    // follow it and must agree: the cigarette (the ember brightens on the inhale, the smoke leaves
    // the mouth on the exhale), the first-person pose (HeldPose: the filter comes to the lips) and
    // the body the other player sees (Crew_Smoke, played by motion time on this cycle, so the hand
    // is at the mouth exactly while the ember glows).
    //
    // The numbers are Crew_Smoke's keys in tools/blender/author_clips.py (SMOKE_N = 40 frames at
    // 24 fps, SMOKE_KEYS lips 7, inhale_end 18, exhale 19, aside 26). Change one side, change the
    // other and re-export the clip.
    public static class SmokeTimeline
    {
        public const float CycleSeconds = 40f / 24f;     // 1.667 s
        public const float Lips = 7f / 40f;              // 0.175: the filter reaches the lips
        public const float InhaleEnd = 18f / 40f;        // 0.450: the drag is over
        public const float Exhale = 19f / 40f;           // 0.475: the smoke leaves the mouth
        public const float Aside = 26f / 40f;            // 0.650: the hand is back down

        // How far the cigarette is from its resting place (0) to the lips (1) at this point of
        // the cycle. Eased both ways, as the clip's keys are.
        public static float AtLips(float phase)
        {
            phase = Mathf.Repeat(phase, 1f);
            if (phase < Lips) return Mathf.SmoothStep(0f, 1f, phase / Lips);
            if (phase <= Exhale) return 1f;
            if (phase < Aside) return 1f - Mathf.SmoothStep(0f, 1f, (phase - Exhale) / (Aside - Exhale));
            return 0f;
        }

        // How hard the ember is drawing, 0 at rest, 1 at the end of the inhale.
        public static float Draw(float phase)
        {
            phase = Mathf.Repeat(phase, 1f);
            if (phase < Lips) return 0f;
            if (phase <= InhaleEnd) return Mathf.SmoothStep(0f, 1f, (phase - Lips) / (InhaleEnd - Lips));
            if (phase < Aside) return 1f - Mathf.SmoothStep(0f, 1f, (phase - InhaleEnd) / (Aside - InhaleEnd));
            return 0f;
        }
    }
}
