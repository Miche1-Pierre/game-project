using UnityEngine;

namespace Movers
{
    // A jolt of the view, for a blast you felt as well as saw.
    //
    // Rotation only, never position. Moving the camera would move the eyes, and the eyes are
    // what SmokeVision samples and what PlayerGrab aims from; a shake that could push your head
    // through a wall or change what you pick up is a bug, not a feeling.
    //
    // It never accumulates. PlayerController rewrites the camera rotation every Update, so an
    // offset added in LateUpdate is gone by the next frame on its own. A camera nobody drives
    // would keep it, so the offset is also taken off again after the camera has rendered
    // (OnPostRender), and before anything reads the camera next frame: only the picture shakes,
    // the aim does not. If something else rewrote the rotation in the meantime, its value wins
    // and nothing is undone.
    //
    // Added to a camera on demand by Shake and switched off again when the jolt is over, so a
    // camera that never saw an explosion carries nothing, and one that did costs nothing at rest.
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    // Late, so the offset lands after anything else that writes the camera in LateUpdate.
    [DefaultExecutionOrder(1000)]
    public class CameraShake : MonoBehaviour
    {
        [Header("Feel")]
        // Degrees at strength 1, which is a grenade at your feet. Pitch leads because a blast
        // reads as a punch from below; roll sells it; yaw is kept small so you do not lose the
        // thing you were looking at.
        public float maxPitch = 6f;
        public float maxYaw = 3f;
        public float maxRoll = 4.5f;
        // How fast the noise scrolls. High enough to read as a jolt, not as a drunk sway, which
        // is a different effect with its own owner (Drunkenness).
        public float frequency = 18f;
        // Seconds for a full-strength shake to die out.
        public float decaySeconds = 0.6f;
        // Several blasts stack, up to this. Past it a chain reaction is just noise on the screen.
        public float maxTrauma = 1.2f;

        float trauma;
        float seed;

        // What the rotation was before our offset, and what we wrote, so the offset can be
        // taken off again only if nobody else has touched the camera since.
        bool applied;
        Quaternion baseRotation;
        Quaternion written;

        static Camera[] cameras = new Camera[8];

        public float Trauma => trauma;

        // Shake one camera. strength 1 is point blank, 0 is nothing.
        public static void Shake(Camera cam, float strength)
        {
            if (cam == null || float.IsNaN(strength) || strength <= 0f) return;
            var shake = cam.GetComponent<CameraShake>();
            if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
            shake.AddTrauma(strength);
        }

        // Shake every enabled camera within range of a point, harder the closer it is. Squared
        // falloff: at the edge of the range it is a rattle, up close it is a punch.
        public static void Shake(Vector3 position, float strength, float range)
        {
            if (float.IsNaN(strength) || strength <= 0f || !(range > 0f)) return;

            int total = Camera.allCamerasCount;
            if (cameras.Length < total) cameras = new Camera[total + 4];
            int n = Camera.GetAllCameras(cameras);
            for (int i = 0; i < n; i++)
            {
                var cam = cameras[i];
                cameras[i] = null;
                if (cam == null) continue;
                float d = Vector3.Distance(cam.transform.position, position);
                if (d >= range) continue;
                float k = 1f - d / range;
                Shake(cam, strength * k * k);
            }
        }

        void Awake()
        {
            // Two cameras side by side should not twitch in step.
            seed = Random.Range(0f, 100f);
        }

        void AddTrauma(float strength)
        {
            trauma = Mathf.Min(maxTrauma, trauma + strength);
            enabled = true;
        }

        void Update()
        {
            // Normally a no-op: OnPostRender already took the offset off.
            Restore();
        }

        void LateUpdate()
        {
            Restore();

            if (trauma <= 0f)
            {
                trauma = 0f;
                enabled = false;
                return;
            }

            trauma = Mathf.Max(0f, trauma - Time.deltaTime / Mathf.Max(0.05f, decaySeconds));
            // Squared, so the shake falls away quickly and the tail is a tremor, not a wobble.
            float a = trauma * trauma;
            if (a <= 0.0001f) return;

            float t = Time.time * frequency;
            var offset = Quaternion.Euler(Noise(0f, t) * maxPitch * a,
                                          Noise(17.3f, t) * maxYaw * a,
                                          Noise(41.7f, t) * maxRoll * a);

            baseRotation = transform.localRotation;
            written = baseRotation * offset;
            transform.localRotation = written;
            applied = true;
        }

        // Built-in pipeline: called on components that sit next to the Camera, right after it
        // rendered. The frame has been drawn shaken, the game goes on unshaken.
        void OnPostRender()
        {
            Restore();
        }

        void OnDisable()
        {
            Restore();
        }

        void Restore()
        {
            if (!applied) return;
            applied = false;
            if (transform.localRotation == written) transform.localRotation = baseRotation;
        }

        // Perlin in [-1, 1], one channel per axis.
        float Noise(float channel, float t)
        {
            return Mathf.PerlinNoise(seed + channel, t) * 2f - 1f;
        }
    }
}
