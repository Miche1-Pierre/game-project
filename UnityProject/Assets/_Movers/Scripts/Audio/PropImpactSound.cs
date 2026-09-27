using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // A movable hitting something: a chair dropped, a box thrown at a wall, a lamp knocked
    // off a table. Until now only breaking made a sound, so most of what the crew does with
    // the furniture, the actual job, was silent. Added to every MovableObject at runtime
    // (SceneAudioBinder).
    //
    // Heard, not reported: it raises no LoudNoise, so the grandmother's hearing is exactly what
    // it was. Whether she should hear a dropped box is a design question (NOTES.md), not an
    // audio one.
    //
    // The kind of knock comes from what the thing is made of (its Breakable material) and its
    // weight; the loudness from how hard it hit along the contact normal, so sliding along a
    // floor is not an impact.
    [DisallowMultipleComponent]
    public sealed class PropImpactSound : MonoBehaviour
    {
        const float MinSpeed = 1f;
        const float Cooldown = 0.12f;

        MovableObject mo;
        SfxKind kind;
        float pitchBase = 1f, weight = 1f;
        float armedAt, nextTime;

        public int Impacts { get; private set; }

        void Awake()
        {
            mo = GetComponent<MovableObject>();
            // Everything settles when the scene starts; that is not a hundred things dropped.
            armedAt = Mathf.Max(SceneAudioBinder.BoundAt + 1.5f, Time.time);
            // Here and not in Start: added to a bottle mid-game, it may collide before Start.
            float mass = mo != null ? mo.Mass : 5f;
            kind = KindFor(mo != null ? mo.Material : BreakMaterial.Wood, mass, mo != null && mo.CanBreak);
            // Small things ring higher, big things lower.
            pitchBase = Mathf.Clamp(1.2f - mass / 150f, 0.8f, 1.2f);
            weight = mass < 1f ? 0.45f : mass < 5f ? 0.7f : mass < 40f ? 0.9f : 1.1f;
        }

        void OnCollisionEnter(Collision c)
        {
            float now = Time.time;
            if (!enabled || now < armedAt || now < nextTime || c.contactCount == 0) return;
            // Its own carrier's hands are not an impact.
            if (mo != null && mo.holder != null && c.collider is CharacterController) return;
            ContactPoint p = c.GetContact(0);
            float hit = Mathf.Abs(Vector3.Dot(c.relativeVelocity, p.normal));
            if (hit < MinSpeed) return;
            nextTime = now + Cooldown;
            float volume = Mathf.Clamp01((hit - 0.8f) / 4.5f) * weight;
            if (mo != null && mo.holder != null) volume *= 0.6f;   // bumped while carried
            AudioDirector.PlayAt(kind, p.point, SoundPreset.Impact, volume, pitchBase * Random.Range(0.94f, 1.06f));
            Impacts++;
        }

        static SfxKind KindFor(BreakMaterial m, float mass, bool breakable)
        {
            if (mass >= 40f && m != BreakMaterial.Metal && m != BreakMaterial.Glass) return SfxKind.ImpactHeavy;
            if (!breakable) return mass < 3f ? SfxKind.ImpactSoft : SfxKind.ImpactWood;
            switch (m)
            {
                case BreakMaterial.Glass: return SfxKind.ImpactGlass;
                case BreakMaterial.Ceramic: return SfxKind.ImpactCeramic;
                case BreakMaterial.Metal: return SfxKind.ImpactMetal;
                case BreakMaterial.Fabric: return SfxKind.ImpactFabric;
                case BreakMaterial.Plastic: return SfxKind.ImpactSoft;
                case BreakMaterial.Stone:
                case BreakMaterial.Brick:
                case BreakMaterial.Concrete:
                case BreakMaterial.Plaster: return mass >= 15f ? SfxKind.ImpactHeavy : SfxKind.ImpactSoft;
                default: return SfxKind.ImpactWood;
            }
        }
    }
}
