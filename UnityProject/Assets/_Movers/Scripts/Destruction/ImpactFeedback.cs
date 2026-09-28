using UnityEngine;

namespace Movers
{
    // What a hit looks and sounds like, scaled by its energy (03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md
    // 3.9): a knock raises a little dust and a small sound, a truck at full speed a cloud, a loud
    // crunch and a camera shake. One place for it, so walls, the ram and crashes agree.
    //
    // Host or offline only. The client gets chunk removals through ChunkDetached (NetDetach plays
    // its own dust and sound) and, for hits that remove nothing, a Props ImpactFx record.
    public static class ImpactFeedback
    {
        // 0 at 1 kJ, 1 at 1 MJ, on a log10 scale.
        public static float Strength01(float energyJoules)
        {
            if (!(energyJoules > 1000f)) return 0f;
            return Mathf.Clamp01((Mathf.Log10(energyJoules) - 3f) / 3f);
        }

        // at: where it hit. size: m, roughly what broke or was hit. removedSomething: the hit took
        // a chunk or a piece out (its removal already reaches the client on its own).
        public static void Hit(Vector3 at, float energyJoules, float size, ImpactAudio.Kind kind, int instigator,
                               bool removedSomething)
        {
            if (!Net.HasAuthority) return;
            float strength = Strength01(energyJoules);
            DestructionFX.Dust(at, Mathf.Max(0.1f, size));
            ImpactAudio.Play(kind, at, Mathf.Lerp(0.3f, 1f, strength), instigator);
        }
    }
}
