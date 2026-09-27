using UnityEngine;

namespace Movers
{
    // The title screen's countryside: rolling low-poly fields in a patchwork, with hedgerows,
    // wheat and ploughed strips, big hills on the horizon, a flat lawn for the grandmother's
    // house and the road laid into it. One generated mesh, built when the scene starts, from a
    // height function that everything else asks too (GroundSnap, the road), so trees, fences
    // and the house always stand on the ground whatever the numbers below.
    //
    // The grid is finer near `center` (about 2 m facets where the camera looks) and coarser
    // towards the horizon (30 m facets that fog hides), which keeps a 1.4 km landscape under
    // 120 k vertices. Runs first in the scene (-200) so the road, the truck and the walkers find
    // it built in their own Start.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class MenuLand : MonoBehaviour
    {
        [Tooltip("The road laid into the fields.")]
        public RoadPath road;
        [Tooltip("A Standard material asset: its shader draws the palette (the material itself is copied).")]
        public Material template;

        [Header("Grid")]
        public Vector2 center = new Vector2(4f, 22f);
        public float halfExtent = 700f;
        [Range(40, 180)] public int cells = 140;
        [Tooltip("Size of a facet at the centre of the grid, in metres.")]
        public float centralSpacing = 2.2f;
        public int seed = 7;

        [Header("Hills")]
        public float rolling = 7f;          // gentle swell of the fields, metres peak to peak
        public float bumps = 1.6f;
        [Tooltip("The horizon's hills start this far from the centre and are fully grown here.")]
        public Vector2 farRange = new Vector2(110f, 420f);
        public float farHeight = 60f;

        [Header("The house's lawn")]
        public Transform plateau;
        public float plateauRadius = 30f;
        public float plateauBlend = 22f;
        public float plateauLift = 1.5f;

        [Header("Fields")]
        public Vector2 fieldSize = new Vector2(64f, 42f);
        public float fieldAngle = 22f;
        [Tooltip("Hedge colour along field edges, where the facets are small enough to draw it (0: none).")]
        public float hedgeWidth = 1.4f;

        public bool Built { get; private set; }

        MeshFilter shown;
        Vector2 seedOffset;
        float plateauHeight;
        bool prepared;

        void Awake() => Build();

        // Samples the road, lays it into the ground and builds the meshes. Called once at load;
        // callable in edit mode by the scene setup (then without meshes: `meshes` false).
        public void Build(bool meshes = true)
        {
            Prepare();
            if (meshes)
            {
                BuildTerrain();
                if (road != null) road.BuildMesh(LowPolyPalette.Land(template));
            }
            SnapAll();
            Built = true;
        }

        // The ground's height at a point, the road and the lawn included.
        public float HeightAt(float x, float z)
        {
            Prepare();
            float h = WithPlateau(x, z);
            if (road != null && road.Ready && road.Nearest(x, z, 20f, out int i, out float d))
            {
                float inner = road.halfWidth + road.shoulder;
                float w = 1f - Smooth(inner, inner + 7f, d);
                if (w > 0f) h = Mathf.Lerp(h, road.HeightAt(road.DistanceAt(i)) - 0.06f, w);
            }
            return h;
        }

        void Prepare()
        {
            if (prepared) return;
            prepared = true;
            var rng = new System.Random(seed);
            seedOffset = new Vector2((float)rng.NextDouble() * 500f, (float)rng.NextDouble() * 500f);
            plateauHeight = plateau != null ? Base(plateau.position.x, plateau.position.z) + plateauLift : 0f;
            if (road == null) return;
            road.BuildCenterline();
            if (!road.Ready) return;

            // The road's own height: the ground along it, smoothed so it never follows a bump.
            int n = road.SampleCount;
            var raw = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = road.PointXZ(i);
                raw[i] = WithPlateau(p.x, p.y);
            }
            float[] smooth = BoxBlur(BoxBlur(raw, 14), 10);
            road.SetHeights(smooth);
        }

        // ---- the height function ----

        float Base(float x, float z)
        {
            float n1 = Mathf.PerlinNoise(x * 0.011f + seedOffset.x, z * 0.011f + seedOffset.y) - 0.5f;
            float n2 = Mathf.PerlinNoise(x * 0.035f + seedOffset.y, z * 0.035f + seedOffset.x) - 0.5f;
            float r = Vector2.Distance(new Vector2(x, z), center);
            float far = Smooth(farRange.x, farRange.y, r);
            float big = Mathf.PerlinNoise(x * 0.0045f + 3.1f, z * 0.0045f + 7.7f);
            return n1 * rolling + n2 * bumps + far * (big * farHeight + 6f);
        }

        float WithPlateau(float x, float z)
        {
            float h = Base(x, z);
            if (plateau == null) return h;
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(plateau.position.x, plateau.position.z));
            float w = 1f - Smooth(plateauRadius, plateauRadius + plateauBlend, d);
            return Mathf.Lerp(h, plateauHeight, w);
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a));
            return t * t * (3f - 2f * t);
        }

        static float[] BoxBlur(float[] v, int radius)
        {
            var o = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int j = Mathf.Clamp(i + k, 0, v.Length - 1);
                    sum += v[j];
                    count++;
                }
                o[i] = sum / count;
            }
            return o;
        }

        // ---- the mesh ----

        void BuildTerrain()
        {
            int n = cells + 1;
            // x(u) = a u + b u^3 on u in [-1, 1]: `centralSpacing` at the middle, the whole
            // `halfExtent` at the edges.
            float du = 2f / cells;
            float a = centralSpacing / du;
            float b = Mathf.Max(0f, halfExtent - a);
            var gx = new float[n];
            for (int i = 0; i < n; i++)
            {
                float u = -1f + i * du;
                gx[i] = a * u + b * u * u * u;
            }

            var pos = new Vector3[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = center.x + gx[i], z = center.y + gx[j];
                    // A little jitter of each corner, so the facets look cut by hand; none on
                    // the grid's rim, and none near the road, whose verges stay tidy.
                    if (i > 0 && j > 0 && i < n - 1 && j < n - 1)
                    {
                        float sx = gx[Mathf.Min(n - 1, i + 1)] - gx[i], sz = gx[Mathf.Min(n - 1, j + 1)] - gx[j];
                        float jx = (Hash(i, j, 1) - 0.5f) * 0.35f * sx, jz = (Hash(i, j, 2) - 0.5f) * 0.35f * sz;
                        bool nearRoad = road != null && road.Ready && road.Nearest(x, z, 12f, out _, out float rd) && rd < 12f;
                        if (!nearRoad) { x += jx; z += jz; }
                    }
                    pos[j * n + i] = new Vector3(x, HeightAt(x, z), z) - transform.position;
                }

            var m = new LowPolyMesh(cells * cells * 2);
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                {
                    Vector3 p00 = pos[j * n + i], p10 = pos[j * n + i + 1];
                    Vector3 p01 = pos[(j + 1) * n + i], p11 = pos[(j + 1) * n + i + 1];
                    // Alternate the diagonal: facets read as a weave, not as stripes.
                    if (((i + j) & 1) == 0)
                    {
                        Face(m, p00, p01, p11, i, j, 0);
                        Face(m, p00, p11, p10, i, j, 1);
                    }
                    else
                    {
                        Face(m, p00, p01, p10, i, j, 0);
                        Face(m, p10, p01, p11, i, j, 1);
                    }
                }

            if (shown == null)
            {
                var go = new GameObject("TerrainMesh");
                go.transform.SetParent(transform, false);
                shown = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = LowPolyPalette.Land(template);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // the ground only receives
            }
            if (shown.sharedMesh != null) Release(shown.sharedMesh);
            shown.sharedMesh = m.ToMesh("MenuLand (runtime)");
        }

        void Face(LowPolyMesh m, Vector3 a, Vector3 b, Vector3 c, int i, int j, int k)
        {
            Vector3 w = (a + b + c) / 3f + transform.position;
            // Small facets draw hedges; big ones would turn a 1 m hedge into a 10 m triangle.
            float size = Mathf.Max((a - b).magnitude, (a - c).magnitude);
            Swatch s = Colour(w.x, w.z, size, out int fieldShade);
            // One shade per field, give or take one per facet: fields read as patches.
            int shade = Mathf.Clamp(fieldShade + (Hash(i, j, 3 + k) < 0.5f ? 0 : 1), 0, LowPolyPalette.Shades - 1);
            m.Triangle(a, b, c, LowPolyPalette.UV(s, shade));
        }

        // The patchwork: fields on a tilted grid, a hedge along field edges near the camera,
        // wheat, meadows and a ploughed field or two, the lawn round the house, a green verge
        // along the road. `shade` is the field's own (every facet of a field shares it).
        Swatch Colour(float x, float z, float facet, out int shade)
        {
            shade = 3;
            if (road != null && road.Ready && road.Nearest(x, z, 8f, out _, out float rd) && rd < road.halfWidth + road.shoulder + 1.8f)
                return Swatch.Verge;
            if (plateau != null)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(plateau.position.x, plateau.position.z));
                if (d < plateauRadius + 4f) return Swatch.Lawn;
            }
            float r = Vector2.Distance(new Vector2(x, z), center);
            if (r > farRange.y * 0.8f) return Swatch.FarHill;

            float ang = fieldAngle * Mathf.Deg2Rad;
            float u = x * Mathf.Cos(ang) + z * Mathf.Sin(ang);
            float v = -x * Mathf.Sin(ang) + z * Mathf.Cos(ang);
            float fu = u / fieldSize.x, fv = v / fieldSize.y;
            int cu = Mathf.FloorToInt(fu), cv = Mathf.FloorToInt(fv);
            float eu = Mathf.Min(fu - cu, 1f - (fu - cu)) * fieldSize.x;
            float ev = Mathf.Min(fv - cv, 1f - (fv - cv)) * fieldSize.y;
            if (hedgeWidth > 0f && facet < 4.5f && Mathf.Min(eu, ev) < hedgeWidth) return Swatch.Hedge;

            shade = 2 + Mathf.FloorToInt(Hash(cu, cv, 5) * 3f);
            float kind = Hash(cu, cv, 9);
            if (kind < 0.24f) return Swatch.GrassA;
            if (kind < 0.42f) return Swatch.GrassB;
            if (kind < 0.56f) return Swatch.Meadow;
            if (kind < 0.66f) return Swatch.GrassC;
            if (kind < 0.84f) return Swatch.Wheat;
            if (kind < 0.9f) return Swatch.WheatDark;
            return Swatch.Soil;
        }

        // What grows at a point (the hay bales look for wheat).
        public Swatch FieldAt(float x, float z)
        {
            Prepare();
            return Colour(x, z, 1f, out _);
        }

        // A stable pseudo-random number in [0, 1) for a grid cell.
        float Hash(int x, int y, int k)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(k * 83492791) ^ (uint)(seed * 2654435761u);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // ---- things standing on the ground ----

        void SnapAll()
        {
            var all = FindObjectsByType<GroundSnap>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                var g = all[i];
                if (g == null || g.gameObject.scene != gameObject.scene) continue;
                Transform t = g.transform;
                Vector3 p = t.position;
                float h = HeightAt(p.x, p.z);
                t.position = new Vector3(p.x, h + g.offset, p.z);
                if (g.followSlope)
                {
                    Vector3 side = t.right;
                    side.y = 0f;
                    if (side.sqrMagnitude < 1e-4f) continue;
                    side.Normalize();
                    float h0 = HeightAt(p.x - side.x, p.z - side.z), h1 = HeightAt(p.x + side.x, p.z + side.z);
                    float tilt = Mathf.Atan2(h1 - h0, 2f) * Mathf.Rad2Deg;
                    Vector3 e = t.eulerAngles;
                    t.rotation = Quaternion.Euler(0f, e.y, 0f) * Quaternion.Euler(0f, 0f, tilt);
                }
            }
        }

        void OnDestroy()
        {
            if (shown != null) Release(shown.sharedMesh);
        }

        static void Release(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
        }
    }
}
