using UnityEngine;

namespace Movers
{
    // What a hit looks and sounds like, scaled by its energy (03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md
    // 3.9): a knock raises a little dust and a small sound, a truck at full speed a cloud, a loud
    // crunch and a camera shake. One place for it, so walls, the ram and crashes agree.
    //
    // Host or offline only. Never doubled on the client:
    // - a hit that removed nothing: the host sends a Props ImpactFx (dust, chips, and the shake of
    //   a big one); its sound reaches the client as an ordinary Props Sound;
    // - a hit that removed a chunk or a piece: that removal already reaches the client
    //   (ChunkDetached, whose NetDetach plays its own dust and sound), so the host keeps its sound
    //   to itself and sends only the shake of a big hit (ImpactFx with size 0).
    public static class ImpactFeedback
    {
        // J. A hit this hard also shakes the cameras around it (a truck at about 20 km/h).
        public const float ShakeEnergy = 50000f;

        // The shake of a hit at ShakeEnergy, and of one at 1 MJ (a truck at 90 km/h): a jolt, never
        // a grenade at your feet (strength 1).
        const float ShakeWeakest = 0.15f;
        const float ShakeStrongest = 0.8f;
        const float ShakeReachWeakest = 8f;    // m
        const float ShakeReachStrongest = 30f;
        // A wall giving way under a ram is a dozen hits in a few steps: the client needs one
        // picture of it, not a record per contact.
        const int MaxSentPerWindow = 8;
        const float SendWindow = 0.1f;
        // Chips (3.9): the chunk is looked for within ChipSearch of the hit, the patch cut is
        // ChipRadius across, a tenth of the chunk's mass, thrown off the face at ChipSpeed.
        const float ChipSearch = 0.3f;     // m
        const float ChipRadius = 0.25f;    // m
        const float ChipMassShare = 0.1f;
        const float ChipSpeed = 3f;        // m/s
        static readonly Collider[] chipHits = new Collider[8];

        static float windowStart = -1000f;
        static int sentInWindow;
        static int countFrame = -1;
        static int hitsThisFrame;

        // For the overlay: hits fed back in the last frame that had any, and the last one's energy.
        public static int HitsLastBusyFrame { get; private set; }
        public static float LastEnergy { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            windowStart = -1000f;
            sentInWindow = 0;
            countFrame = -1;
            hitsThisFrame = 0;
            HitsLastBusyFrame = 0;
            LastEnergy = 0f;
        }

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
            if (!IsFinite(at) || float.IsNaN(energyJoules)) return;
            energyJoules = Mathf.Max(0f, energyJoules);
            size = float.IsNaN(size) ? 0f : Mathf.Max(0f, size);
            Count(energyJoules);

            float strength = Strength01(energyJoules);
            Dust(at, size, strength);
            ImpactAudio.Play(kind, at, Mathf.Lerp(0.3f, 1f, strength), instigator, !removedSomething);
            bool shakes = energyJoules >= ShakeEnergy;
            if (shakes) Shake(at, strength);

            if (!removedSomething) Chips(at, strength, instigator);

            if (!Net.IsHost) return;
            if (!removedSomething) Send(at, strength, Mathf.Max(0.1f, size));
            else if (shakes) Send(at, strength, 0f);
        }

        // Online client (Props ImpactFx): the host's dust and shake. size 0: the shake only.
        public static void PlayFromNet(Vector3 at, float strength01, float size)
        {
            if (!IsFinite(at) || float.IsNaN(strength01)) return;
            strength01 = Mathf.Clamp01(strength01);
            if (size > 0f)
            {
                Dust(at, size, strength01);
                Chips(at, strength01, Actors.World);
            }
            // The byte loses a little: a hit sent for its shake at exactly ShakeEnergy must still shake.
            if (size <= 0f || strength01 >= Strength01(ShakeEnergy) - 1f / 255f) Shake(at, strength01);
        }

        // More energy: a bigger and thicker cloud.
        static void Dust(Vector3 at, float size, float strength)
        {
            float cloud = Mathf.Max(0.1f, size) * (1f + strength);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, 24f, strength) + size * 4f), 4, 32);
            DestructionFX.Dust(at, cloud, count);
        }

        // A hit that removes nothing knocks 1 to 3 chips off the wall chunk it landed on (3.9): a
        // small patch of that chunk's surface, a tenth of its mass, on the ordinary debris budget
        // (MeshShatter.Chips). The chunk is found from the point on each machine (the host's Hit,
        // the client's ImpactFx), so nothing more goes on the wire. An intact wall or a prop
        // gives none: the dust says it.
        static void Chips(Vector3 at, float strength, int instigator)
        {
            int n = Physics.OverlapSphereNonAlloc(at, ChipSearch, chipHits, ~0, QueryTriggerInteraction.Ignore);
            DestructibleChunk best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                Collider c = chipHits[i];
                chipHits[i] = null;
                if (!DestructibleModule.TryGetChunk(c, out DestructibleChunk chunk)) continue;
                if (!chunk.Attached || chunk.Renderer == null || !chunk.Renderer.enabled) continue;
                float d = c.bounds.SqrDistance(at);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = chunk;
                }
            }
            if (best == null) return;

            Vector3 away = at - best.Renderer.bounds.center;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.up;
            int count = Mathf.Clamp(1 + Mathf.RoundToInt(strength * 2f), 1, 3);
            MeshShatter.Chips(best.Renderer, at, ChipRadius, count, ChipMassShare * best.Mass, away * ChipSpeed,
                              DestructionMaterialTable.Current.propDebrisLifetime, instigator);
        }

        static void Shake(Vector3 at, float strength)
        {
            float k = Mathf.InverseLerp(Strength01(ShakeEnergy), 1f, strength);
            CameraShake.Shake(at, Mathf.Lerp(ShakeWeakest, ShakeStrongest, k),
                              Mathf.Lerp(ShakeReachWeakest, ShakeReachStrongest, k));
        }

        static void Send(Vector3 at, float strength, float size)
        {
            float now = Time.unscaledTime;
            if (now - windowStart >= SendWindow)
            {
                windowStart = now;
                sentInWindow = 0;
            }
            if (sentInWindow >= MaxSentPerWindow) return;
            sentInWindow++;
            PropsSync.ImpactFx(at, strength, size);
        }

        static void Count(float energy)
        {
            int frame = Time.frameCount;
            if (frame != countFrame)
            {
                countFrame = frame;
                hitsThisFrame = 0;
            }
            hitsThisFrame++;
            HitsLastBusyFrame = hitsThisFrame;
            LastEnergy = energy;
        }

        static bool IsFinite(Vector3 v)
        {
            float s = v.x + v.y + v.z;
            return !(float.IsNaN(s) || float.IsInfinity(s));
        }
    }
}
