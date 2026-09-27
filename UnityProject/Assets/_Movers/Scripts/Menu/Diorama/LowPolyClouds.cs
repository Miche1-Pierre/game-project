using UnityEngine;

namespace Movers
{
    // A few fat low-poly clouds drifting across the title screen's sky: each is a handful of
    // faceted balls merged into one mesh, cream on top and a shade warmer underneath, moved
    // along the wind and wrapped round a box above the land. Built once at load.
    [DisallowMultipleComponent]
    public sealed class LowPolyClouds : MonoBehaviour
    {
        public Material template;
        public int count = 9;
        [Tooltip("The box the clouds live in, centred on this object: X across, Y altitude band, Z depth.")]
        public Vector3 area = new Vector3(700f, 40f, 500f);
        public Vector3 wind = new Vector3(2.2f, 0f, 0.4f);
        public Vector2 sizeRange = new Vector2(14f, 34f);
        public int seed = 3;

        Transform[] clouds;
        Mesh[] meshes;

        void Start()
        {
            var rng = new System.Random(seed);
            clouds = new Transform[count];
            meshes = new Mesh[count];
            var material = LowPolyPalette.Land(template);
            for (int i = 0; i < count; i++)
            {
                float size = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)rng.NextDouble());
                var m = new LowPolyMesh(600);
                int puffs = 3 + rng.Next(4);
                for (int k = 0; k < puffs; k++)
                {
                    float along = (k - (puffs - 1) * 0.5f) * size * 0.42f;
                    float r = size * Mathf.Lerp(0.38f, 0.62f, (float)rng.NextDouble()) * (k == puffs / 2 ? 1.2f : 1f);
                    var c = new Vector3(along, r * 0.15f * (float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * size * 0.3f);
                    m.Blob(c, new Vector3(r, r * 0.62f, r * 0.8f), Swatch.Cloud, rng, 0.1f);
                }
                meshes[i] = m.ToMesh("Cloud (runtime)");
                var go = new GameObject("Cloud" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * area.x,
                    ((float)rng.NextDouble() - 0.5f) * area.y,
                    ((float)rng.NextDouble() - 0.5f) * area.z);
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 40f - 20f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                var r2 = go.AddComponent<MeshRenderer>();
                r2.sharedMaterial = material;
                r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r2.receiveShadows = false;
                clouds[i] = go.transform;
            }
        }

        void Update()
        {
            if (clouds == null) return;
            Vector3 step = wind * Time.unscaledDeltaTime;
            Vector3 half = area * 0.5f;
            for (int i = 0; i < clouds.Length; i++)
            {
                Vector3 p = clouds[i].localPosition + step;
                if (p.x > half.x) p.x -= area.x;
                else if (p.x < -half.x) p.x += area.x;
                if (p.z > half.z) p.z -= area.z;
                else if (p.z < -half.z) p.z += area.z;
                clouds[i].localPosition = p;
            }
        }

        void OnDestroy()
        {
            if (meshes == null) return;
            for (int i = 0; i < meshes.Length; i++) if (meshes[i] != null) Destroy(meshes[i]);
        }
    }
}
