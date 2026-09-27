using UnityEngine;

namespace Movers
{
    // Round hay bales lying in the title screen's wheat fields, in little rows the way a baler
    // leaves them: the harvested fields read as fields, not as flat yellow. Placed once at
    // load, only where the land says there is wheat and away from the road.
    [DisallowMultipleComponent]
    public sealed class HayBales : MonoBehaviour
    {
        public MenuLand land;
        public Material template;
        [Tooltip("Where to look for wheat, world X and Z.")]
        public Vector2 areaMin = new Vector2(20f, 35f);
        public Vector2 areaMax = new Vector2(120f, 110f);
        public int rows = 5;
        public int balesPerRow = 3;
        public float radius = 0.7f;
        public float length = 1.3f;
        public int seed = 13;

        Mesh mesh;

        void Start()
        {
            if (land == null || !land.Built) return;
            mesh = BaleMesh();
            var material = LowPolyPalette.Land(template);
            var rng = new System.Random(seed);
            int placed = 0;
            for (int r = 0, tries = 0; r < rows && tries < rows * 12; tries++)
            {
                float x = Mathf.Lerp(areaMin.x, areaMax.x, (float)rng.NextDouble());
                float z = Mathf.Lerp(areaMin.y, areaMax.y, (float)rng.NextDouble());
                if (!IsWheat(x, z)) continue;
                float heading = (float)rng.NextDouble() * 360f;
                Vector3 step = Quaternion.Euler(0f, heading, 0f) * Vector3.forward * 4.5f;
                for (int b = 0; b < balesPerRow; b++)
                {
                    float bx = x + step.x * b, bz = z + step.z * b;
                    if (!IsWheat(bx, bz)) break;
                    var go = new GameObject("HayBale");
                    go.transform.SetParent(transform, false);
                    go.transform.SetPositionAndRotation(new Vector3(bx, land.HeightAt(bx, bz) + radius * 0.92f, bz),
                        Quaternion.Euler(0f, heading + 90f + (float)rng.NextDouble() * 30f - 15f, 0f));
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                    placed++;
                }
                r++;
            }
        }

        bool IsWheat(float x, float z)
        {
            Swatch s = land.FieldAt(x, z);
            return s == Swatch.Wheat || s == Swatch.WheatDark;
        }

        // A drum on its side (axis along X), ten facets round, straw a shade darker than the
        // stubble around it, the rolled ends browner.
        Mesh BaleMesh()
        {
            var m = new LowPolyMesh(60);
            const int sides = 10;
            float h = length * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(0f, Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(0f, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);
                Vector3 l = Vector3.left * h, r = Vector3.right * h;
                var side = LowPolyPalette.UV(Swatch.WheatDark, 3 + (i % 3));
                var end = LowPolyPalette.UV(Swatch.CardboardDark, 4);
                // Side, then the two end caps; each face turned outwards.
                AddOutward(m, p0 + l, p1 + l, p1 + r, side, Vector3.zero);
                AddOutward(m, p0 + l, p1 + r, p0 + r, side, Vector3.zero);
                AddOutward(m, l, p0 + l, p1 + l, end, Vector3.zero);
                AddOutward(m, r, p1 + r, p0 + r, end, Vector3.zero);
            }
            var mesh = m.ToMesh("HayBale (runtime)");
            return mesh;
        }

        // Unity shows a face whose Cross(b - a, c - a) points at the viewer: turned away from
        // `inside` (the drum's centre), so every face shows from outside.
        static void AddOutward(LowPolyMesh m, Vector3 a, Vector3 b, Vector3 c, Vector2 uv, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) m.Triangle(a, c, b, uv);
            else m.Triangle(a, b, c, uv);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
