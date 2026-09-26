using UnityEngine;

namespace Movers
{
    // A fire in the hearth that the grandmother lights (ActivityKind.LightFire). Put it on an
    // empty object inside the fireplace opening, on the logs, but NOT under the fireplace: a
    // shattered piece hands its child renderers to the debris maker, flames included. Point
    // `hearth` at the fireplace instead: when it is gone the fire goes out (and the spot, which
    // requires the fireplace, becomes "missing").
    //
    // Flames and a warm flickering light, built from code the first time it is lit so the
    // scene carries no asset for it. It burns for burnSeconds, then goes out and she can light
    // it again. Greybox: it hurts nobody and sets nothing on fire.
    [DisallowMultipleComponent]
    public sealed class FireplaceFire : MonoBehaviour
    {
        [Tooltip("The fireplace this fire sits in. Gone (shattered): the fire goes out.")]
        public GameObject hearth;
        public float burnSeconds = 240f;
        [Tooltip("Width, depth and height of the bed of flames, metres.")]
        public Vector3 size = new Vector3(0.8f, 0.3f, 0.1f);
        public Color lightColor = new Color(1f, 0.55f, 0.2f);
        public float lightIntensity = 1.8f;
        public float lightRange = 5f;

        ParticleSystem flames;
        Light glow;
        Material material;
        Texture2D texture;
        float litUntil = -1f;
        float seed;

        public bool IsLit => litUntil > Time.time;

        public void Ignite()
        {
            Build();
            litUntil = Time.time + burnSeconds;
            if (flames != null && !flames.isPlaying) flames.Play(true);
            if (glow != null) glow.enabled = true;
        }

        public void Extinguish()
        {
            litUntil = -1f;
            if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (glow != null) glow.enabled = false;
        }

        void Update()
        {
            if (glow == null || !glow.enabled) return;
            if (!IsLit || (hearth != null && !hearth.activeInHierarchy)) { Extinguish(); return; }
            float n = Mathf.PerlinNoise(Time.time * 7f, seed);
            glow.intensity = lightIntensity * (0.75f + 0.45f * n);
        }

        void Build()
        {
            if (flames != null) return;
            seed = Random.value * 100f;

            var flameObject = new GameObject("Flames");
            flameObject.transform.SetParent(transform, false);
            flames = flameObject.AddComponent<ParticleSystem>();
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = flames.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.35f), new Color(1f, 0.35f, 0.08f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.gravityModifier = -0.05f;

            var emission = flames.emission;
            emission.rateOverTime = 40f;

            // A flat box on the logs, emitting upwards: the box's own Z is its emission axis.
            var shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.y, size.z);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var colour = flames.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLife = flames.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

            var renderer = flameObject.GetComponent<ParticleSystemRenderer>();
            material = MakeMaterial();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = lightColor;
            glow.range = lightRange;
            glow.intensity = lightIntensity;
            glow.shadows = LightShadows.None;
            glow.enabled = false;
        }

        // An additive soft dot. The legacy particle shader is part of the Built-in pipeline;
        // the fallbacks keep the flames visible if a build strips it.
        Material MakeMaterial()
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            const int n = 32;
            texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FireDot" };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                byte v = (byte)(255f * a * a);
                pixels[y * n + x] = new Color32(255, 255, 255, v);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var m = new Material(shader) { name = "GrandmaFire" };
            m.mainTexture = texture;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.6f, 0.45f, 0.3f, 0.5f));
            return m;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (texture != null) Destroy(texture);
        }
    }
}
