using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // One puff of smoke: a volume that blocks vision, and the particles that show where it is.
    //
    // The two must never disagree. If the cloud you see and the cloud that blinds you are not
    // the same object, the player learns nothing from looking, and a blinding effect the player
    // cannot read is a punishment rather than a problem. So the particles simulate in LOCAL
    // space and ride this transform: the volume moves, the visible mass moves with it.
    //
    // Life is 7 seconds flat, start to finish, and that number is the spec. Emission stops
    // early and the longest-lived particle dies before the deadline, so nothing outlives it.
    public class SmokeCloud : MonoBehaviour
    {
        public const float DefaultLifetime = 7f;

        // Every live cloud. Read by SmokeVision, one entry per puff, never more than MaxActive.
        public static readonly List<SmokeCloud> Active = new List<SmokeCloud>();
        public const int MaxActive = 16;

        [Header("Life")]
        public float lifetime = DefaultLifetime;
        public float fadeIn = 0.45f;     // seconds to reach full density
        public float fadeOut = 2.5f;     // seconds of thinning before it is gone
        public float peakDensity = 1f;   // per-puff strength, 1 = a full mouthful

        [Header("Volume")]
        public float startRadius = 0.6f;
        public float endRadius = 2.8f;
        // Beyond this fraction of the radius the density falls off to nothing, so a cloud has
        // a soft shoulder instead of an invisible wall you cross in one step.
        public float softEdge = 0.45f;

        [Header("Drift")]
        public float rise = 0.35f;          // m/s, smoke goes up
        public float exhaleSpeed = 0.9f;    // m/s, the initial push away from the mouth
        public float exhaleDamping = 1.8f;  // how fast that push dies out

        float age;
        Vector3 exhale;
        ParticleSystem ps;

        // Writable for the editor tooling only (MoversSmokeCLI looks at a puff at any point in
        // its life without entering Play mode). Nothing in the game sets it: the clock is
        // Time.deltaTime and nothing else.
        public float Age
        {
            get => age;
            set => age = Mathf.Clamp(value, 0f, lifetime);
        }

        public float Radius => Mathf.Lerp(startRadius, endRadius, Mathf.Sqrt(Mathf.Clamp01(age / lifetime)));

        // Statics survive a play-mode exit when the domain reload is disabled, which the
        // playtest CLI does on purpose. Without this, a second run starts inside the smoke
        // left by the first one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active.Clear(); }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }

            exhale = Vector3.Lerp(exhale, Vector3.zero, Mathf.Clamp01(exhaleDamping * Time.deltaTime));
            transform.position += (Vector3.up * rise + exhale) * Time.deltaTime;
        }

        // 0 outside, 1 in the heart of the puff. The envelope is the same one the particles
        // fade on, so what you measure is what you see.
        public float DensityAt(Vector3 point)
        {
            float r = Radius;
            if (r <= 0.001f) return 0f;
            float d = Vector3.Distance(point, transform.position) / r;
            if (d >= 1f) return 0f;
            float falloff = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - d) / Mathf.Max(0.01f, softEdge)));
            return falloff * Envelope() * peakDensity;
        }

        float Envelope()
        {
            if (age < fadeIn) return Mathf.Clamp01(age / Mathf.Max(0.01f, fadeIn));
            float left = lifetime - age;
            if (left < fadeOut) return Mathf.Clamp01(left / Mathf.Max(0.01f, fadeOut));
            return 1f;
        }

        // How thick the smoke is at a point, all puffs combined. Transmittance, not a max:
        // two puffs in the same place really are thicker than one, which is what makes holding
        // the button down worth something.
        public static float DensityAtPoint(Vector3 point)
        {
            float clear = 1f;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var c = Active[i];
                if (c == null) { Active.RemoveAt(i); continue; }
                clear *= 1f - Mathf.Clamp01(c.DensityAt(point));
                if (clear <= 0.001f) break;
            }
            return 1f - clear;
        }

        // ---- factory ----

        public static SmokeCloud Spawn(Vector3 center, Vector3 exhaleDir, float lifetime = DefaultLifetime, float strength = 1f)
        {
            // Hold the population down rather than let a player leaning on the button build a
            // hundred particle systems. The oldest puff is already the thinnest one.
            while (Active.Count >= MaxActive)
            {
                var oldest = Active[0];
                for (int i = 1; i < Active.Count; i++)
                    if (Active[i] != null && Active[i].age > oldest.age) oldest = Active[i];
                if (oldest == null) { Active.RemoveAt(0); continue; }
                Active.Remove(oldest);
                Destroy(oldest.gameObject);
            }

            var go = new GameObject("SmokeCloud");
            go.transform.position = center;
            var cloud = go.AddComponent<SmokeCloud>();
            cloud.lifetime = Mathf.Max(0.5f, lifetime);
            cloud.peakDensity = Mathf.Clamp(strength, 0.1f, 1.5f);
            cloud.exhale = exhaleDir.normalized * cloud.exhaleSpeed;
            cloud.BuildParticles();
            return cloud;
        }

        void BuildParticles()
        {
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop();

            // Emission is over well before the deadline, and no particle outlives the volume:
            // 1.6 s of emission plus a 5.0 s life ends at 6.6 s, inside the 7 s contract.
            const float emitDuration = 1.6f;
            const float maxParticleLife = 5.0f;

            var main = ps.main;
            main.duration = emitDuration;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(maxParticleLife * 0.55f, maxParticleLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // Grey, not white. Real cigarette smoke is near white, and against the greybox walls,
            // which are near white too, a near-white cloud is invisible until you are inside it.
            // A cloud nobody can see coming is not a problem the player can solve.
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.68f, 0.72f, 1f), new Color(0.55f, 0.55f, 0.60f, 1f));
            // Local space: the particles belong to this transform, so they rise with the volume
            // instead of drifting away from the thing that is actually blinding you.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.maxParticles = 120;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 26f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = startRadius * 0.5f;
            shape.radiusThickness = 1f;

            // Grows to about the volume radius, so the mass you see is the mass you measure.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var grow = new AnimationCurve(
                new Keyframe(0f, 0.45f), new Keyframe(0.45f, 1.1f), new Keyframe(1f, 1.75f));
            size.size = new ParticleSystem.MinMaxCurve(1.6f, grow);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.62f, 0.16f),
                    new GradientAlphaKey(0.46f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(g);

            // Turbulence, so the puff curls and rolls instead of reading as a sphere of quads.
            // Two octaves: the big roll of the cloud and the small curl at its edges. Bounded, and
            // the particles stay in local space, so the mass you see stays on the volume.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.42f);
            noise.frequency = 0.45f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.3f);
            noise.octaveCount = 2;
            noise.damping = true;

            // Each wisp turns slowly as it drifts: soft smoke never holds still.
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            // Torn, uneven puffs instead of one round blob (SmokeTextures.WispSheet). A little
            // more alpha above makes up for the holes, so the cloud reads as thick as before.
            SmokeTextures.UseWispSheet(ps);
            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.sharedMaterial = SmokeTextures.WispMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
        }
    }
}
