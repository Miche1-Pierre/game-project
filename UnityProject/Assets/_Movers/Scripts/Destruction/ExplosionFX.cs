using UnityEngine;

namespace Movers
{
    // What an explosion looks like: a fireball, a flash, a burst of smoke and a spray of sparks.
    //
    // All of it is built in code from a sphere and the puff texture the cigarette already
    // generates, so a grenade costs no art (CLAUDE.md rule 13). It is only a picture: the
    // damage, the forces and the knockback have all happened in Explosion by the time this is
    // spawned. Nothing here has a collider, so the next blast of a chain reaction, which runs
    // in the same frame, never mistakes the previous fireball for a wall.
    //
    // The smoke is deliberately not a SmokeCloud. A cigarette cloud blinds whoever stands in
    // it, and that is its point; a blast that also blinded everyone for seven seconds would
    // hide the one thing worth watching, which is the room coming apart.
    public class ExplosionFX : MonoBehaviour
    {
        const float Lifetime = 5f;         // everything below is over by then
        const float GrowTime = 0.12f;      // the fireball bursts open...
        const float FireballTime = 0.6f;   // ...and is gone, faded and shrunk, by this
        const float FlashTime = 0.35f;
        const float FlashIntensity = 7f;
        // Past this many flashes at once, more lights add cost and no light: a chain reaction
        // is already as bright as it is going to get.
        const int MaxFlashes = 4;

        // Emission above 1 so the fireball reads as a light source, and clipped to orange and
        // pale yellow on a camera without HDR rather than washing out to white.
        static readonly Color FireAlbedo = new Color(1f, 0.42f, 0.08f);
        static readonly Color FireGlow = new Color(1.6f, 0.62f, 0.12f);
        static readonly Color CoreAlbedo = new Color(1f, 0.86f, 0.5f);
        static readonly Color CoreGlow = new Color(1.8f, 1.35f, 0.55f);
        static readonly Color EmberGlow = new Color(0.45f, 0.08f, 0.02f);   // what the fire cools to as it fades
        static readonly Color FlashColor = new Color(1f, 0.72f, 0.42f);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        static Mesh sphere;
        static Material fireMat;
        static Material sparkMat;
        static MaterialPropertyBlock block;
        static int liveFlashes;

        Renderer fire;
        Renderer core;
        Light flash;
        bool holdsFlash;
        float fireSize;
        float age;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { liveFlashes = 0; }

        public static ExplosionFX Spawn(Vector3 position, float radius = 6.5f, float power = 1f)
        {
            if (!Application.isPlaying) return null;
            if (float.IsNaN(radius) || float.IsNaN(power)) return null;
            radius = Mathf.Clamp(radius, 0.5f, 40f);
            power = Mathf.Clamp(power, 0.2f, 3f);

            var go = new GameObject("ExplosionFX");
            go.transform.position = position;
            // Scheduled before anything is built, so even a half-built effect goes away.
            Destroy(go, Lifetime);
            var fx = go.AddComponent<ExplosionFX>();
            try
            {
                fx.Build(radius, power);
            }
            catch (System.Exception e)
            {
                // Only the picture failed: say so, drop it, and let the blast carry on.
                Debug.LogException(e);
                Destroy(go);
                return null;
            }
            return fx;
        }

        void Build(float radius, float power)
        {
            if (block == null) block = new MaterialPropertyBlock();

            fireSize = radius * 0.45f;
            // No shader at all (a stripped build): no fireball, the rest still plays.
            if (FireMaterial != null)
            {
                fire = Ball("Fireball", 0);
                // Drawn after the shell so the hot centre shows through it; two transparent
                // spheres at the same centre would otherwise swap order from frame to frame.
                core = Ball("Core", 1);
            }

            if (liveFlashes < MaxFlashes)
            {
                var lg = new GameObject("Flash");
                lg.transform.SetParent(transform, false);
                flash = lg.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.range = radius * 2.4f;
                flash.intensity = FlashIntensity;
                flash.color = FlashColor;
                flash.shadows = LightShadows.None;
                // It lasts a third of a second: it must not lose its slot to a lamp and fall
                // back to a vertex light, which would make the flash look like a smear.
                flash.renderMode = LightRenderMode.ForcePixel;
                liveFlashes++;
                holdsFlash = true;
            }

            BuildSmoke(radius, power);
            BuildSparks(radius, power);
            Animate(0f);
        }

        void Update()
        {
            age += Time.deltaTime;
            Animate(age);
        }

        void OnDestroy()
        {
            ReleaseFlash();
        }

        void Animate(float t)
        {
            if (fire != null && core != null)
            {
                if (t >= FireballTime)
                {
                    fire.gameObject.SetActive(false);
                    core.gameObject.SetActive(false);
                    fire = null;
                    core = null;
                }
                else
                {
                    // Ease out: the ball bursts open, then hangs for a beat while it burns out.
                    float grow = Mathf.Clamp01(t / GrowTime);
                    grow = 1f - (1f - grow) * (1f - grow);
                    float fade = t <= GrowTime ? 0f : (t - GrowTime) / (FireballTime - GrowTime);
                    float size = fireSize * grow * (1f - 0.35f * fade);
                    fire.transform.localScale = Vector3.one * size;
                    core.transform.localScale = Vector3.one * (size * 0.62f * (1f - 0.6f * fade));

                    // Bright for most of its life, then gone quickly, cooling to red on the way.
                    float alpha = 1f - fade * fade;
                    Tint(fire, FireAlbedo, Color.Lerp(FireGlow, EmberGlow, fade), alpha);
                    Tint(core, CoreAlbedo, CoreGlow, alpha * (1f - fade));
                }
            }

            if (flash != null)
            {
                float k = 1f - t / FlashTime;
                if (k <= 0f)
                {
                    Destroy(flash.gameObject);
                    flash = null;
                    ReleaseFlash();
                }
                else
                {
                    flash.intensity = FlashIntensity * k * k;
                }
            }
        }

        void ReleaseFlash()
        {
            if (!holdsFlash) return;
            holdsFlash = false;
            liveFlashes = Mathf.Max(0, liveFlashes - 1);
        }

        // One shared material for every fireball; the colour and fade of each one go through a
        // property block, so a chain of twenty blasts makes no new materials.
        static void Tint(Renderer r, Color albedo, Color glow, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            block.SetColor(ColorId, new Color(albedo.r, albedo.g, albedo.b, alpha));
            block.SetColor(EmissionId, glow * alpha);
            r.SetPropertyBlock(block);
        }

        Renderer Ball(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = FireMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.sortingOrder = order;
            return r;
        }

        // ---- smoke and sparks ----

        // About thirty soft grey puffs thrown outwards, slowed down, then left to rise and swell.
        // World space: the smoke stays where the blast was, it does not follow anything.
        // Puff size is capped in metres, not only scaled with the radius: the cellar and the
        // upper rooms are about 2.5 m high, and a puff bigger than the room pokes through the
        // ceiling and the walls as a hard-edged grey square, seen from the room next door.
        void BuildSmoke(float radius, float power)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.2f, radius * 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(radius * 0.2f, 1.0f), Mathf.Min(radius * 0.38f, 1.6f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // Darker than the cigarette smoke: this is burnt, and it has to read against the
            // pale greybox walls from the other end of the house.
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.24f, 0.23f, 0.22f, 1f), new Color(0.46f, 0.44f, 0.42f, 1f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.05f);   // hot smoke rises
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            int count = Mathf.RoundToInt(30f * Mathf.Clamp(power, 0.6f, 1.5f));
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.12f;
            shape.radiusThickness = 1f;

            // The blast throws the puffs out, the air stops them almost at once: that is the
            // difference between a cloud that billows and a cloud that sprays.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(0.6f);
            limit.dampen = 0.12f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var grow = new AnimationCurve(
                new Keyframe(0f, 0.45f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.25f));
            size.size = new ParticleSystem.MinMaxCurve(1f, grow);

            // Lit orange by the fireball for the first instant, then plain smoke.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.62f, 0.35f), 0f),
                    new GradientColorKey(Color.white, 0.12f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.04f),
                    new GradientAlphaKey(0.6f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.5f);
            noise.frequency = 0.4f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.3f);
            noise.damping = true;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.sharedMaterial = SmokeTextures.ParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
        }

        // Short orange streaks that fall: they are what makes the blast read as fire and not
        // as a puff of dust.
        void BuildSparks(float radius, float power)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.1f, radius * 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.45f, 1f), new Color(1f, 0.5f, 0.15f, 1f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            int count = Mathf.RoundToInt(50f * Mathf.Clamp(power, 0.6f, 1.5f));
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            shape.radiusThickness = 1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1f;
            r.sharedMaterial = SparkMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
        }

        // ---- shared resources, built once ----

        // Unity objects survive a play-mode exit as "fake null", so these re-check every time
        // rather than trusting a flag (same reasoning as SmokeTextures).

        // The built-in sphere mesh, taken from a throwaway primitive. The primitive is switched
        // off on the spot, which takes its collider out of the physics scene at once (a chain
        // reaction later in the same frame would otherwise raycast into it), then destroyed the
        // normal, deferred way. Not DestroyImmediate: Unity refuses that inside a physics
        // callback, and a blast set off by a collision would leave the sphere there for good.
        // The mesh is a built-in asset and outlives the primitive.
        static Mesh SphereMesh
        {
            get
            {
                if (sphere == null)
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    tmp.name = "ExplosionFX_SphereSource";
                    sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                    tmp.SetActive(false);
                    Destroy(tmp);
                }
                return sphere;
            }
        }

        // Standard, switched to its Fade mode so the ball can fade out, with emission so it
        // glows instead of waiting to be lit. Fade rather than Transparent: Transparent keeps
        // its highlights at zero alpha, and a fireball has no business having highlights.
        static Material FireMaterial
        {
            get
            {
                if (fireMat == null)
                {
                    Shader sh = Shader.Find("Standard");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    if (sh == null) return null;
                    fireMat = new Material(sh) { name = "MAT_ExplosionFire", hideFlags = HideFlags.HideAndDontSave };
                    fireMat.color = FireAlbedo;
                    if (fireMat.HasProperty("_Mode"))
                    {
                        fireMat.SetFloat("_Mode", 2f);
                        fireMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        fireMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        fireMat.SetFloat("_ZWrite", 0f);
                        fireMat.DisableKeyword("_ALPHATEST_ON");
                        fireMat.EnableKeyword("_ALPHABLEND_ON");
                        fireMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        fireMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    }
                    if (fireMat.HasProperty("_Glossiness")) fireMat.SetFloat("_Glossiness", 0f);
                    if (fireMat.HasProperty("_Metallic")) fireMat.SetFloat("_Metallic", 0f);
                    if (fireMat.HasProperty("_EmissionColor"))
                    {
                        fireMat.EnableKeyword("_EMISSION");
                        // A fireball lasts half a second; it has nothing to bake into the GI.
                        fireMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                        fireMat.SetColor("_EmissionColor", FireGlow);
                    }
                }
                return fireMat;
            }
        }

        // Additive, so sparks crossing each other get brighter, the way embers do. The puff
        // texture gives each one a soft round end instead of a hard quad.
        static Material SparkMaterial
        {
            get
            {
                if (sparkMat == null)
                {
                    Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    if (sh == null) return null;
                    sparkMat = new Material(sh) { name = "MAT_ExplosionSparks", mainTexture = SmokeTextures.Puff };
                    sparkMat.hideFlags = HideFlags.HideAndDontSave;
                }
                return sparkMat;
            }
        }
    }
}
