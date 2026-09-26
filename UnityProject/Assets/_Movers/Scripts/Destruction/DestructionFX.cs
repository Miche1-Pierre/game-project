using UnityEngine;

namespace Movers
{
    // Cheap particles for breakage: a puff of plaster dust when a chunk of wall breaks off or
    // falls, and a glitter of glass when a pane goes.
    //
    // Why particles and not more debris: a pane used to become 8 to 16 rigidbodies and that
    // was most of a blast's debris (A5a: 516 of 854 pieces). Now a pane gives 3 to 5 real
    // shards you can kick about, and the rest of the "it shattered" is this glitter, which has
    // no collider and costs nothing to the physics.
    //
    // Two particle systems for the whole scene, built on first use and fed with Emit, so a
    // burst allocates nothing. They live in the scene (a reload rebuilds them), world space,
    // no collisions.
    public static class DestructionFX
    {
        static ParticleSystem dust;
        static ParticleSystem glass;
        static GameObject root;
        static bool quitting;

        static readonly Color DustLight = new Color(0.78f, 0.75f, 0.70f, 0.85f);
        static readonly Color DustDark = new Color(0.55f, 0.52f, 0.48f, 0.85f);
        static readonly Color GlassA = new Color(0.85f, 0.95f, 1f, 0.95f);
        static readonly Color GlassB = new Color(0.65f, 0.80f, 0.90f, 0.8f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            dust = null;
            glass = null;
            root = null;
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnQuitting() { quitting = true; }

        // A puff of dust around a point. size is roughly the size of what broke, in metres.
        public static void Dust(Vector3 at, float size, int count = 0)
        {
            if (!Usable(at)) return;
            if (dust == null) dust = Build("Dust", false);
            if (dust == null) return;
            if (count <= 0) count = Mathf.Clamp(Mathf.RoundToInt(6f + size * 8f), 4, 24);
            var shape = dust.shape;
            shape.radius = Mathf.Clamp(size * 0.4f, 0.1f, 1.2f);
            var ep = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            dust.Emit(ep, count);
        }

        // A glitter of glass from a pane, thrown along push (m/s) plus a little spray.
        public static void Glass(Bounds pane, Vector3 push)
        {
            if (!Usable(pane.center)) return;
            if (glass == null) glass = Build("Glass", true);
            if (glass == null) return;
            var shape = glass.shape;
            shape.radius = Mathf.Clamp(pane.extents.magnitude * 0.6f, 0.05f, 1f);
            push = Vector3.ClampMagnitude(push, 6f);
            // A few batches, each with its own throw, so the burst is not one flat sheet.
            for (int i = 0; i < 4; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = pane.center,
                    applyShapeToPosition = true,
                    velocity = push + Random.insideUnitSphere * 2.2f + Vector3.up * 0.6f,
                };
                glass.Emit(ep, 6);
            }
        }

        static bool Usable(Vector3 at)
        {
            if (!Application.isPlaying || quitting) return false;
            float s = at.x + at.y + at.z;
            return !(float.IsNaN(s) || float.IsInfinity(s));
        }

        static ParticleSystem Build(string name, bool isGlass)
        {
            if (root == null)
            {
                root = new GameObject("DestructionFX");
            }
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Looping with no emission of its own: the system keeps simulating, so what Emit
            // puts in moves and fades even long after the last burst.
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radiusThickness = 1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();

            if (isGlass)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
                main.startColor = new ParticleSystem.MinMaxGradient(GlassA, GlassB);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(1f);
                main.maxParticles = 400;
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            }
            else
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                main.startColor = new ParticleSystem.MinMaxGradient(DustLight, DustDark);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f);
                main.maxParticles = 600;
                // Thrown out, stopped by the air at once: dust billows, it does not spray.
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = new ParticleSystem.MinMaxCurve(0.4f);
                limit.dampen = 0.15f;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.3f)));
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.08f), new GradientAlphaKey(0f, 1f) });
            }
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = SmokeTextures.ParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
