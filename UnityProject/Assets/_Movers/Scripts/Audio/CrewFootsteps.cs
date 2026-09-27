using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // A crew member's feet: a step every stride while moving on the ground, by what the floor
    // is made of, and a landing after a fall. Added to each player at runtime (SceneAudioBinder).
    //
    // Steps come from distance covered, not from a timer, so they stop the instant the player
    // stops and speed up with him. The stride is longer than a real one on purpose: the crew
    // walks at 4.5 m/s, and real strides at that speed would chatter six steps a second.
    //
    // Read in LateUpdate, after PlayerController has moved the capsule this frame (isGrounded
    // and velocity are only true after its Move).
    [DisallowMultipleComponent]
    public sealed class CrewFootsteps : MonoBehaviour
    {
        public float walkVolume = 0.55f;
        public float runVolume = 0.75f;
        public float crouchVolume = 0.22f;
        public float minSpeed = 0.5f;

        CrewMember member;
        PlayerController controller;
        CharacterController body;
        float distance;
        bool wasGrounded = true;
        float airborneSince;
        float fallSpeed;
        bool leftFoot;

        public int StepCount { get; private set; }
        public int LandingCount { get; private set; }
        public float LastStepTime { get; private set; } = -99f;
        public Sfx.Surface LastSurface { get; private set; }

        void Awake()
        {
            member = GetComponent<CrewMember>();
            controller = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
        }

        void LateUpdate()
        {
            if (body == null || !body.enabled || (member != null && member.IsDriving) || Time.deltaTime <= 0f)
            {
                distance = 0f;
                wasGrounded = true;
                return;
            }
            Vector3 v = body.velocity;
            bool grounded = body.isGrounded;
            if (!grounded)
            {
                if (wasGrounded) { airborneSince = Time.time; fallSpeed = 0f; }
                fallSpeed = Mathf.Min(fallSpeed, v.y);
                wasGrounded = false;
                return;
            }
            if (!wasGrounded)
            {
                wasGrounded = true;
                // A step down a stair is not a landing; a jump or a fall is.
                if (Time.time - airborneSince > 0.3f && fallSpeed < -3.5f) Land(-fallSpeed);
                distance = 0f;
            }

            float speed = new Vector2(v.x, v.z).magnitude;
            if (speed < minSpeed)
            {
                // Standing still: the next step comes quickly once he moves again.
                distance = Mathf.Min(distance, 0.35f);
                return;
            }
            bool crouch = controller != null && controller.crouching;
            float stride = crouch ? 0.75f : Mathf.Lerp(0.8f, 1.9f, Mathf.Clamp01(speed / 7f));
            distance += speed * Time.deltaTime;
            if (distance < stride) return;
            distance -= stride;
            Step(speed, crouch);
        }

        // The bottom of the capsule, wherever the scene put the root (at the feet or the middle).
        Vector3 Feet => body.transform.TransformPoint(body.center) + Vector3.down * (body.height * 0.5f);

        void Step(float speed, bool crouch)
        {
            if (!Surfaces.Under(Feet, transform, out Sfx.Surface surface, out Vector3 at)) return;
            float vol = crouch ? crouchVolume : Mathf.Lerp(walkVolume, runVolume, Mathf.Clamp01((speed - 4.5f) / 2.7f));
            float pitch = Random.Range(0.95f, 1.05f);
            // Carrying something heavy: heavier, lower steps.
            MovableObject held = member != null ? member.Held : null;
            if (held != null && held.Mass > 30f)
            {
                vol = Mathf.Min(1f, vol + 0.15f);
                pitch *= 0.93f;
            }
            // Left and right feet a hand apart, so the steps sit under the body in stereo.
            leftFoot = !leftFoot;
            at += transform.right * (leftFoot ? -0.12f : 0.12f);
            AudioDirector.PlayAt(Surfaces.Step(surface), at, SoundPreset.Footstep, vol, pitch);
            StepCount++;
            LastStepTime = Time.time;
            LastSurface = surface;
        }

        void Land(float speed)
        {
            if (!Surfaces.Under(Feet, transform, out Sfx.Surface surface, out Vector3 at)) return;
            float vol = Mathf.Clamp01(0.45f + (speed - 3.5f) * 0.08f);
            AudioDirector.PlayAt(Surfaces.Land(surface), at, SoundPreset.Footstep, vol, Random.Range(0.95f, 1.03f));
            LandingCount++;
            LastSurface = surface;
            // A real fall knocks the wind out of him.
            if (speed > 9f && TryGetComponent(out CrewSounds sounds)) sounds.Grunt(EffortKind.Oof, 0.9f);
        }
    }
}
