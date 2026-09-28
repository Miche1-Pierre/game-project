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
    // Grit (DEV 2): a spray of small dark bits with every broken chunk of wall, thrown along
    // the hit and falling fast. Rubble (MeshShatter.ShatterChunk) gives the big pieces; grit is
    // the fine stuff between them that makes a hole read as broken masonry, at no physics cost.
    //
    // Three particle systems for the whole scene, built on first use and fed with Emit, so a
    // burst allocates nothing. They live in the scene (a reload rebuilds them), world space.
    // Only grit collides (with the world, low quality), so it lands on the floor instead of
    // sinking through it.
    public static class DestructionFX
    {
        static ParticleSystem dust;
        static ParticleSystem glass;
        static ParticleSystem grit;
        static GameObject root;
        static bool quitting;

        static readonly Color DustLight = new Color(0.78f, 0.75f, 0.70f, 0.85f);
        static readonly Color DustDark = new Color(0.55f, 0.52f, 0.48f, 0.85f);
        static readonly Color GlassA = new Color(0.85f, 0.95f, 1f, 0.95f);
        static readonly Color GlassB = new Color(0.65f, 0.80f, 0.90f, 0.8f);
        static readonly Color GritLight = new Color(0.62f, 0.58f, 0.52f, 1f);
        static readonly Color GritDark = new Color(0.36f, 0.33f, 0.30f, 1f);

        const float MaxDustPush = 4f;    // m/s: dust billows, a blast does not turn it into a jet
        const float MaxGritPush = 9f;    // m/s

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            dust = null;
            glass = null;
            grit = null;
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
            if (dust == null) dust = Build("Dust", Kind.Dust);
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
            if (glass == null) glass = Build("Glass", Kind.Glass);
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

        // The same, blown along push (m/s): the dust of a chunk torn out by a blast or a truck
        // leaves the way the chunk went. Half the puff drifts with the push, half stays.
        public static void Dust(Vector3 at, float size, int count, Vector3 push)
        {
            if (!Usable(at)) return;
            if (dust == null) dust = Build("Dust", Kind.Dust);
            if (dust == null) return;
            if (count <= 0) count = Mathf.Clamp(Mathf.RoundToInt(6f + size * 8f), 4, 24);
            var shape = dust.shape;
            shape.radius = Mathf.Clamp(size * 0.4f, 0.1f, 1.2f);
            push = Vector3.ClampMagnitude(push, MaxDustPush);
            int moving = count / 2;
            var ep = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            if (count - moving > 0) dust.Emit(ep, count - moving);
            if (moving > 0)
            {
                ep.velocity = push;
                dust.Emit(ep, moving);
            }
        }

        // Small bits of masonry thrown from a broken chunk: size is the chunk's size (m), push
        // the way it was hit (m/s). count 0 picks it from the size.
        public static void Grit(Vector3 at, float size, Vector3 push, int count = 0)
        {
            if (!Usable(at)) return;
            if (grit == null) grit = Build("Grit", Kind.Grit);
            if (grit == null) return;
            if (count <= 0) count = Mathf.Clamp(Mathf.RoundToInt(8f + size * 14f), 6, 40);
            var shape = grit.shape;
            shape.radius = Mathf.Clamp(size * 0.35f, 0.05f, 1f);
            push = Vector3.ClampMagnitude(push, MaxGritPush);
            // A few batches, each with its own throw, so the spray fans out.
            const int Batches = 3;
            int per = Mathf.Max(1, count / Batches);
            for (int i = 0; i < Batches; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = at,
                    applyShapeToPosition = true,
                    velocity = push * Random.Range(0.5f, 1f) + Random.insideUnitSphere * 2.5f + Vector3.up * 1.2f,
                };
                grit.Emit(ep, per);
            }
        }

        static bool Usable(Vector3 at)
        {
            if (!Application.isPlaying || quitting) return false;
            float s = at.x + at.y + at.z;
            return !(float.IsNaN(s) || float.IsInfinity(s));
        }

        enum Kind { Dust, Glass, Grit }

        static ParticleSystem Build(string name, Kind kind)
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

            if (kind == Kind.Glass)
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
            else if (kind == Kind.Grit)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
                main.startColor = new ParticleSystem.MinMaxGradient(GritLight, GritDark);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(1.4f);
                main.maxParticles = 700;
                // Lands and skids instead of falling through the floor. World collision in low
                // quality is a cheap cached test, and grit is the only system that pays it.
                var collision = ps.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.quality = ParticleSystemCollisionQuality.Low;
                collision.dampen = new ParticleSystem.MinMaxCurve(0.6f);
                collision.bounce = new ParticleSystem.MinMaxCurve(0.15f);
                int debris = DestructionLayers.Debris;
                collision.collidesWith = debris >= 0 ? ~(1 << debris) : ~0;
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            }
            else
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                main.startColor = new ParticleSystem.MinMaxGradient(DustLight, DustDark);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f);
                // 800, was 600: DEV 2 breaks more wall per blast, and a capped system silently
                // drops the newest puffs, the ones in front of the player.
                main.maxParticles = 800;
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
