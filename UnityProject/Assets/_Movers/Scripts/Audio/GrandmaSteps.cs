using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // Her slippers: a shuffle every short stride while she walks, by floor. Added next to
    // GrandmaMover at runtime (SceneAudioBinder).
    //
    // This is gameplay as much as mood: the crew should hear her coming down the hall, through
    // the wall, before she turns the corner. So her steps are a preset that walls dim only a
    // little (SoundPreset.GrandmaStep), and the shuffle, the part that carries, is the loud
    // part of the recipe.
    [DisallowMultipleComponent]
    public sealed class GrandmaSteps : MonoBehaviour
    {
        public float volume = 0.8f;
        public float stride = 0.42f;

        GrandmaMover mover;
        CharacterController body;
        float distance;
        Vector3 last;
        bool hasLast;

        public int StepCount { get; private set; }
        public float LastStepTime { get; private set; } = -99f;
        public Sfx.Surface LastSurface { get; private set; }

        void Awake()
        {
            mover = GetComponent<GrandmaMover>();
            body = GetComponent<CharacterController>();
        }

        void LateUpdate()
        {
            // Seated or sliding into a chair, her body is off: no steps.
            if (mover == null || !mover.CollisionsOn || Time.deltaTime <= 0f)
            {
                hasLast = false;
                distance = 0f;
                return;
            }
            // Measured from how far she really went, not her commanded speed: pushed against a
            // wardrobe she walks on the spot, and slippers on the spot make no sound.
            Vector3 p = transform.position;
            if (!hasLast) { last = p; hasLast = true; return; }
            Vector3 d = p - last;
            last = p;
            d.y = 0f;
            float moved = d.magnitude;
            if (moved > 1f) { distance = 0f; return; }   // a teleport
            if (moved / Time.deltaTime < 0.15f)
            {
                distance = Mathf.Min(distance, stride * 0.5f);
                return;
            }
            // Hurrying (1 m/s, angry) takes longer steps than pottering about (0.6 m/s).
            float s = Mathf.Lerp(stride, stride * 1.25f, Mathf.Clamp01((mover.CurrentSpeed - 0.6f) / 0.4f));
            distance += moved;
            if (distance < s) return;
            distance -= s;
            Vector3 feet = body != null ? transform.TransformPoint(body.center) + Vector3.down * (body.height * 0.5f) : p;
            if (!Surfaces.Under(feet, transform, out Sfx.Surface surface, out Vector3 at)) return;
            AudioDirector.PlayAt(Surfaces.Slipper(surface), at, SoundPreset.GrandmaStep, volume, Random.Range(0.94f, 1.04f));
            StepCount++;
            LastStepTime = Time.time;
            LastSurface = surface;
        }
    }
}
