using UnityEngine;

namespace Movers
{
    // Grey puffs rising from the grandmother's chimney: the fire is lit, she is home. Low-poly
    // balls that rise, drift with the wind, swell and then shrink away, one after the other.
    // No particle system and no transparency: the same flat look as everything else.
    [DisallowMultipleComponent]
    public sealed class ChimneySmoke : MonoBehaviour
    {
        public Material template;
        public int puffs = 7;
        public float lifetime = 6f;
        public float rise = 1.3f;
        public Vector3 wind = new Vector3(0.5f, 0f, 0.2f);
        public float startSize = 0.5f;
        public float endSize = 2.2f;

        Transform[] items;
        float[] seeds;
        Mesh mesh;
        float t;

        void Start()
        {
            var rng = new System.Random(11);
            var m = new LowPolyMesh(80);
            m.Blob(Vector3.zero, Vector3.one * 0.5f, Swatch.Smoke, rng, 0.15f);
            mesh = m.ToMesh("SmokePuff (runtime)");
            var material = LowPolyPalette.Land(template);
            items = new Transform[puffs];
            seeds = new float[puffs];
            for (int i = 0; i < puffs; i++)
            {
                var go = new GameObject("Puff" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                items[i] = go.transform;
                seeds[i] = (float)rng.NextDouble();
            }
        }

        void Update()
        {
            if (items == null) return;
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < items.Length; i++)
            {
                // Each puff is a phase of the same loop, evenly spread.
                float age = Mathf.Repeat(t / lifetime + i / (float)items.Length, 1f);
                float grow = age < 0.75f ? Mathf.Lerp(startSize, endSize, age / 0.75f) : Mathf.Lerp(endSize, 0f, (age - 0.75f) / 0.25f);
                float wobble = Mathf.Sin((t + seeds[i] * 10f) * 1.7f) * 0.25f * age;
                items[i].localPosition = Vector3.up * (rise * lifetime * age) + wind * (lifetime * age * age) + new Vector3(wobble, 0f, wobble * 0.5f);
                items[i].localScale = Vector3.one * Mathf.Max(0.001f, grow);
                items[i].localRotation = Quaternion.Euler(0f, (seeds[i] * 360f + t * 20f) % 360f, 0f);
            }
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
