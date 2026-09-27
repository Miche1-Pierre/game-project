using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // What grows at a point of the countryside (CountryLand.GroundAt), for the scatter.
    public enum CountryGround { Yard, Lawn, Grass, Meadow, Wheat, Soil, Hedge, Forest, Verge, Road, Far }

    // The countryside round the grandmother's yard, in the title screen's style: rolling
    // low-poly fields in a patchwork, hedges, woods, big hills on the horizon, and the street
    // running on into the country at both ends. Built when the map starts (like the title
    // screen's MenuLand), from one height function that the scatter (trees, grass) asks too.
    //
    // The yard itself is NOT touched: a rectangle (yardMin..yardMax, the house's own flat grass,
    // the street, the truck's area) keeps its own ground, and the land has a hole there. Beyond
    // it the land starts at the yard's level and rises into the hills over rampWidth, so the
    // edge never shows a step.
    //
    // Chunked meshes (frustum culled per camera), no shadows cast (the ground only receives),
    // mesh colliders out to colliderRange so a player who jumps the fence, or drives the truck
    // off, stays on the ground. Every mesh here is the land's own and freed with it.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-250)]
    public sealed class CountryLand : MonoBehaviour
    {
        [Header("The yard (left exactly as it is)")]
        public Vector2 yardMin = new Vector2(-30f, -30f);
        public Vector2 yardMax = new Vector2(51f, 36f);
        [Tooltip("Height of the yard's ground (the top of its grass).")]
        public float yardLevel = -0.01f;
        [Tooltip("Metres beyond the yard over which the land rises from flat to rolling.")]
        public float rampWidth = 32f;

        [Header("Grid")]
        public float halfExtent = 1300f;
        [Range(60, 240)] public int cells = 176;
        [Tooltip("Size of a facet near the yard, in metres (they grow towards the horizon).")]
        public float centralSpacing = 3f;
        [Tooltip("Meshes per side: the land is cut in chunks x chunks pieces for culling.")]
        [Range(1, 16)] public int chunks = 8;
        public int seed = 11;

        [Header("Hills")]
        public float rolling = 8f;
        public float bumps = 1.6f;
        [Tooltip("The horizon's hills start this far from the yard and are fully grown here.")]
        public Vector2 farRange = new Vector2(150f, 620f);
        public float farHeight = 85f;

        [Header("Fields")]
        public Vector2 fieldSize = new Vector2(72f, 46f);
        public float fieldAngle = 17f;
        public float hedgeWidth = 1.5f;

        [Header("Woods")]
        [Tooltip("Belts of forest, world X and Z (their edges are ragged). Noise adds more patches.")]
        public Rect[] woods =
        {
            new Rect(-170f, 78f, 440f, 150f),     // behind the house, up the slope
            new Rect(-150f, -240f, 400f, 120f),   // across the fields in front
            new Rect(150f, -60f, 170f, 280f),     // east
            new Rect(-330f, -90f, 130f, 240f),    // west
        };
        [Tooltip("Noise woods: higher leaves fewer patches.")]
        [Range(0.3f, 0.9f)] public float patchThreshold = 0.6f;
        [Tooltip("No wood this close to the yard.")]
        public float woodClearance = 26f;

        [Header("Roads (continuing the street)")]
        public RoadPath[] roads = new RoadPath[0];
        [Tooltip("Half width where a road leaves the yard (the street's), and further on.")]
        public float roadStartHalfWidth = 4.8f;
        public float roadHalfWidth = 3.4f;
        public float roadShoulder = 1.1f;

        [Header("Rendering and physics")]
        [Tooltip("The Movers/Environment material for the land and the roads.")]
        public Material material;
        [Tooltip("Land and roads get colliders this far from the yard's centre.")]
        public float colliderRange = 520f;
        [Tooltip("The yard's own grass (its ground boxes), drawn at Play with yardMaterial so the yard and the fields meet without a seam. Colliders and names are untouched.")]
        public Renderer[] yardGround = new Renderer[0];
        public Material yardMaterial;

        public bool Built { get; private set; }
        public Vector2 YardCentre => (yardMin + yardMax) * 0.5f;

        // Colours, in the Built-in pipeline's gamma space, matching the title screen's palette.
        static readonly Color Lawn = new Color(0.44f, 0.60f, 0.32f);   // PK_grass, the yard's own
        static readonly Color GrassA = Hex(0x86AE55), GrassB = Hex(0x78A34C), GrassC = Hex(0x93B862);
        static readonly Color MeadowC = Hex(0xA3C46C), HedgeC = Hex(0x557F3E), WheatC = Hex(0xD9C27A);
        static readonly Color WheatDarkC = Hex(0xC8AC62), SoilC = Hex(0xA08064), VergeC = Hex(0xA6C878);
        static readonly Color ForestFloorC = Hex(0x4F7236), FarHillC = Hex(0x9DB88A), FarForestC = Hex(0x6F8F5C);
        public static readonly Color Asphalt = new Color(0.34f, 0.34f, 0.37f);
        static readonly Color Gravel = new Color(0.58f, 0.54f, 0.46f), LineC = new Color(0.66f, 0.66f, 0.63f);

        Vector2 o1, o2, o3, o4;
        bool prepared;
        readonly List<GameObject> built = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();

        void Awake() => Build();

        void Start()
        {
            if (yardMaterial == null) return;
            for (int i = 0; i < yardGround.Length; i++) if (yardGround[i] != null) yardGround[i].sharedMaterial = yardMaterial;
        }

        public void Build()
        {
            Clear();
            Prepare();
            BuildTerrain();
            for (int i = 0; i < roads.Length; i++) if (roads[i] != null) BuildRoad(roads[i], i);
            Built = true;
        }

        // ---- the height function ----

        void Prepare()
        {
            if (prepared) return;
            prepared = true;
            var rng = new System.Random(seed);
            o1 = new Vector2((float)rng.NextDouble() * 500f, (float)rng.NextDouble() * 500f);
            o2 = new Vector2((float)rng.NextDouble() * 500f, (float)rng.NextDouble() * 500f);
            o3 = new Vector2((float)rng.NextDouble() * 500f, (float)rng.NextDouble() * 500f);
            o4 = new Vector2((float)rng.NextDouble() * 500f, (float)rng.NextDouble() * 500f);

            // Each road: its centre line, then a height along it, the land's smoothed so it
            // never follows a bump, starting exactly at the street's level at the yard.
            for (int r = 0; r < roads.Length; r++)
            {
                var road = roads[r];
                if (road == null) continue;
                road.halfWidth = roadStartHalfWidth;
                road.shoulder = roadShoulder;
                road.BuildCenterline();
                if (!road.Ready) continue;
                int n = road.SampleCount;
                var raw = new float[n];
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = road.PointXZ(i);
                    raw[i] = Terrain(p.x, p.y);
                }
                float[] smooth = BoxBlur(BoxBlur(raw, 14), 10);
                for (int i = 0; i < n; i++)
                {
                    float s = road.DistanceAt(i);
                    smooth[i] = Mathf.Lerp(0f, smooth[i], Smooth(0f, 30f, s));
                }
                road.SetHeights(smooth);
            }
        }

        // Signed distance to the yard's rectangle: negative inside, positive outside.
        public float OutsideDistance(float x, float z)
        {
            float dx = Mathf.Max(yardMin.x - x, x - yardMax.x);
            float dz = Mathf.Max(yardMin.y - z, z - yardMax.y);
            if (dx <= 0f && dz <= 0f) return Mathf.Max(dx, dz);
            float ox = Mathf.Max(dx, 0f), oz = Mathf.Max(dz, 0f);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        public bool InYard(float x, float z) => x >= yardMin.x && x <= yardMax.x && z >= yardMin.y && z <= yardMax.y;

        float Base(float x, float z)
        {
            float n1 = Mathf.PerlinNoise(x * 0.0085f + o1.x, z * 0.0085f + o1.y) - 0.5f;
            float n2 = Mathf.PerlinNoise(x * 0.031f + o2.x, z * 0.031f + o2.y) - 0.5f;
            float far = Smooth(farRange.x, farRange.y, OutsideDistance(x, z));
            float big = Mathf.PerlinNoise(x * 0.0042f + 3.1f, z * 0.0042f + 7.7f);
            // The fields round the yard lean gently upwards: the house sits in a shallow bowl.
            return n1 * rolling + n2 * bumps + rolling * 0.25f + far * (big * farHeight + 10f);
        }

        // The land without the roads.
        float Terrain(float x, float z)
        {
            float d = OutsideDistance(x, z);
            if (d <= 0f) return yardLevel - 0.05f;   // under the yard's own ground, never seen
            return Mathf.Lerp(yardLevel - 0.005f, Base(x, z), Smooth(0f, rampWidth, d));
        }

        // The ground's height at a point, roads included.
        public float HeightAt(float x, float z)
        {
            Prepare();
            float h = Terrain(x, z);
            for (int r = 0; r < roads.Length; r++)
            {
                var road = roads[r];
                if (road == null || !road.Ready) continue;
                if (!road.Nearest(x, z, 20f, out int i, out float d)) continue;
                float inner = roadStartHalfWidth + roadShoulder;
                float w = 1f - Smooth(inner, inner + 8f, d);
                if (w > 0f) h = Mathf.Lerp(h, road.HeightAt(road.DistanceAt(i)) - 0.06f, w);
            }
            return h;
        }

        // Distance from a road's centre line (MaxValue if further than `reach`).
        public float RoadDistance(float x, float z, float reach = 20f)
        {
            float best = float.MaxValue;
            for (int r = 0; r < roads.Length; r++)
            {
                var road = roads[r];
                if (road == null || !road.Ready) continue;
                if (road.Nearest(x, z, reach, out _, out float d) && d < best) best = d;
            }
            return best;
        }

        // 0 (open land) to 1 (deep in a wood). Trees stand where a random number is below it.
        public float ForestAt(float x, float z)
        {
            Prepare();
            float d = OutsideDistance(x, z);
            if (d < woodClearance * 0.6f) return 0f;
            float edge = Mathf.PerlinNoise(x * 0.035f + o4.x, z * 0.035f + o4.y) - 0.5f;
            float n = Mathf.PerlinNoise(x * 0.0055f + o3.x, z * 0.0055f + o3.y);
            float f = Smooth(patchThreshold - 0.04f, patchThreshold + 0.04f, n + edge * 0.14f);
            for (int i = 0; i < woods.Length; i++)
            {
                Rect w = woods[i];
                float inside = Mathf.Min(Mathf.Min(x - w.xMin, w.xMax - x), Mathf.Min(z - w.yMin, w.yMax - z));
                f = Mathf.Max(f, Smooth(-6f, 12f, inside + edge * 30f));
            }
            f *= Smooth(woodClearance * 0.6f, woodClearance * 1.6f, d);
            if (f > 0f && RoadDistance(x, z, 14f) < roadHalfWidth + 7f) return 0f;
            return f;
        }

        // What grows at a point, for the scatter (grass, wheat, flowers).
        public CountryGround GroundAt(float x, float z)
        {
            Prepare();
            return Classify(x, z, 1f, out _, out _);
        }

        CountryGround Classify(float x, float z, float facet, out Color colour, out int shade)
        {
            shade = 3;
            float d = OutsideDistance(x, z);
            if (d <= 0f) { colour = Lawn; return CountryGround.Yard; }
            float rd = RoadDistance(x, z, 12f);
            float edge = roadStartHalfWidth + roadShoulder;
            if (rd < edge) { colour = Gravel; return CountryGround.Road; }
            if (rd < edge + 2.5f) { colour = VergeC; return CountryGround.Verge; }
            float wobble = (Mathf.PerlinNoise(x * 0.08f + o2.y, z * 0.08f + o2.x) - 0.5f) * 8f;
            if (d < 9f + wobble) { colour = Lawn; return CountryGround.Lawn; }

            float forest = ForestAt(x, z);
            float r = OutsideDistance(x, z);
            if (r > farRange.y * 0.8f)
            {
                colour = forest > 0.35f ? FarForestC : FarHillC;
                return CountryGround.Far;
            }
            if (forest > 0.45f) { colour = ForestFloorC; return CountryGround.Forest; }

            float ang = fieldAngle * Mathf.Deg2Rad;
            float u = x * Mathf.Cos(ang) + z * Mathf.Sin(ang);
            float v = -x * Mathf.Sin(ang) + z * Mathf.Cos(ang);
            float fu = u / fieldSize.x, fv = v / fieldSize.y;
            int cu = Mathf.FloorToInt(fu), cv = Mathf.FloorToInt(fv);
            float eu = Mathf.Min(fu - cu, 1f - (fu - cu)) * fieldSize.x;
            float ev = Mathf.Min(fv - cv, 1f - (fv - cv)) * fieldSize.y;
            if (hedgeWidth > 0f && facet < 5f && Mathf.Min(eu, ev) < hedgeWidth) { colour = HedgeC; return CountryGround.Hedge; }

            shade = Mathf.FloorToInt(Hash(cu, cv, 5) * 4f);
            float kind = Hash(cu, cv, 9);
            if (kind < 0.22f) { colour = GrassA; return CountryGround.Grass; }
            if (kind < 0.40f) { colour = GrassB; return CountryGround.Grass; }
            if (kind < 0.56f) { colour = MeadowC; return CountryGround.Meadow; }
            if (kind < 0.66f) { colour = GrassC; return CountryGround.Grass; }
            if (kind < 0.82f) { colour = WheatC; return CountryGround.Wheat; }
            if (kind < 0.89f) { colour = WheatDarkC; return CountryGround.Wheat; }
            colour = SoilC;
            return CountryGround.Soil;
        }

        // Is (x, z) on a field's edge (a hedge line)? For the hedge bushes.
        public bool OnHedge(float x, float z) => GroundAt(x, z) == CountryGround.Hedge;

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
                    sum += v[Mathf.Clamp(i + k, 0, v.Length - 1)];
                    count++;
                }
                o[i] = sum / count;
            }
            return o;
        }

        // A stable pseudo-random number in [0, 1) for a grid cell.
        public float Hash(int x, int y, int k)
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

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        // ---- the land's mesh ----

        void BuildTerrain()
        {
            int n = cells + 1;
            // x(u) = a u + b u^3 on u in [-1, 1]: `centralSpacing` in the middle, `halfExtent`
            // at the edges (MenuLand's grid).
            float du = 2f / cells;
            float a = centralSpacing / du;
            float b = Mathf.Max(0f, halfExtent - a);
            var g = new float[n];
            for (int i = 0; i < n; i++)
            {
                float u = -1f + i * du;
                g[i] = a * u + b * u * u * u;
            }

            Vector2 c = YardCentre;
            var pos = new Vector3[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = c.x + g[i], z = c.y + g[j];
                    // Hand-cut facets: each corner jittered a little, except near the yard and
                    // the roads, whose edges stay tidy.
                    if (i > 0 && j > 0 && i < n - 1 && j < n - 1)
                    {
                        float sx = g[i + 1] - g[i], sz = g[j + 1] - g[j];
                        float jx = (Hash(i, j, 1) - 0.5f) * 0.35f * sx, jz = (Hash(i, j, 2) - 0.5f) * 0.35f * sz;
                        if (OutsideDistance(x, z) > 6f && RoadDistance(x, z, 16f) > roadStartHalfWidth + 10f) { x += jx; z += jz; }
                    }
                    pos[j * n + i] = new Vector3(x, HeightAt(x, z), z);
                }

            int per = Mathf.CeilToInt(cells / (float)chunks);
            for (int cj = 0; cj < chunks; cj++)
                for (int ci = 0; ci < chunks; ci++)
                {
                    int i0 = ci * per, i1 = Mathf.Min(cells, i0 + per);
                    int j0 = cj * per, j1 = Mathf.Min(cells, j0 + per);
                    if (i0 >= i1 || j0 >= j1) continue;
                    var m = new EnvMesh((i1 - i0) * (j1 - j0) * 2);
                    for (int j = j0; j < j1; j++)
                        for (int i = i0; i < i1; i++)
                        {
                            Vector3 p00 = pos[j * n + i], p10 = pos[j * n + i + 1];
                            Vector3 p01 = pos[(j + 1) * n + i], p11 = pos[(j + 1) * n + i + 1];
                            // The yard has its own ground: skip what lies well inside it.
                            Vector3 mid = (p00 + p11) * 0.5f;
                            if (OutsideDistance(mid.x, mid.z) < -2.5f) continue;
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
                    if (m.TriangleCount == 0) continue;
                    var mesh = m.ToMesh("CountryLand " + ci + "_" + cj + " (runtime)");
                    meshes.Add(mesh);
                    // "Ground" in the name: the footsteps (Surfaces) hear grass.
                    var go = new GameObject("Ground_Land_" + ci + "_" + cj);
                    go.transform.SetParent(transform, false);
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = true;
                    Bounds bb = mesh.bounds;
                    Vector2 near = new Vector2(Mathf.Clamp(c.x, bb.min.x, bb.max.x), Mathf.Clamp(c.y, bb.min.z, bb.max.z));
                    if (Vector2.Distance(near, c) < colliderRange) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    built.Add(go);
                }
        }

        void Face(EnvMesh m, Vector3 a, Vector3 b, Vector3 c, int i, int j, int k)
        {
            Vector3 w = (a + b + c) / 3f;
            float size = Mathf.Max((a - b).magnitude, (a - c).magnitude);
            Classify(w.x, w.z, size, out Color colour, out int shade);
            // One shade per field, give or take a little per facet: fields read as patches.
            float s = 0.94f + shade * 0.03f + (Hash(i, j, 3 + k) - 0.5f) * 0.035f;
            m.Triangle(a, b, c, new Color(colour.r * s, colour.g * s, colour.b * s, 1f));
        }

        // ---- the roads ----

        // The tarmac, a dashed centre line like the street's, and gravel shoulders, as one mesh
        // that starts where the street ends. Its own collider, so wheels ride on the tarmac.
        void BuildRoad(RoadPath road, int index)
        {
            if (!road.Ready) return;
            var m = new EnvMesh(road.SampleCount * 8);
            const float step = 2f;
            float length = road.Length;
            for (float s = 0f; s < length - 0.01f; s += step)
            {
                float s1 = Mathf.Min(length, s + step);
                float w0 = HalfWidthAt(s), w1 = HalfWidthAt(s1);
                road.Sample(s, 0f, out Vector3 c0, out _);
                road.Sample(s1, 0f, out Vector3 c1, out _);
                road.Sample(s, -w0, out Vector3 l0, out _);
                road.Sample(s1, -w1, out Vector3 l1, out _);
                road.Sample(s, w0, out Vector3 r0, out _);
                road.Sample(s1, w1, out Vector3 r1, out _);
                road.Sample(s, -w0 - roadShoulder, out Vector3 sl0, out _);
                road.Sample(s1, -w1 - roadShoulder, out Vector3 sl1, out _);
                road.Sample(s, w0 + roadShoulder, out Vector3 sr0, out _);
                road.Sample(s1, w1 + roadShoulder, out Vector3 sr1, out _);
                Vector3 up = Vector3.up * 0.01f, down = Vector3.down * 0.05f;
                float k = 0.97f + 0.03f * Hash(index, Mathf.RoundToInt(s), 7);
                Color tar = new Color(Asphalt.r * k, Asphalt.g * k, Asphalt.b * k, 1f);
                Color grav = new Color(Gravel.r * k, Gravel.g * k, Gravel.b * k, 1f);
                // Which side is left depends on the road's direction: face both up.
                AddUp(m, l0 + up, l1 + up, c1 + up, c0 + up, tar);
                AddUp(m, c0 + up, c1 + up, r1 + up, r0 + up, tar);
                AddUp(m, sl0 + down, sl1 + down, l1 + up, l0 + up, grav);
                AddUp(m, r0 + up, r1 + up, sr1 + down, sr0 + down, grav);
            }
            // Centre dashes (the street's: 2.2 m every 5 m), a hair above the tarmac.
            for (float s = 1.4f; s + 2.2f < length; s += 5f)
            {
                road.Sample(s, -0.07f, out Vector3 p0, out _);
                road.Sample(s + 2.2f, -0.07f, out Vector3 p1, out _);
                road.Sample(s, 0.07f, out Vector3 q0, out _);
                road.Sample(s + 2.2f, 0.07f, out Vector3 q1, out _);
                Vector3 lift = Vector3.up * 0.018f;
                AddUp(m, p0 + lift, p1 + lift, q1 + lift, q0 + lift, LineC);
            }
            var mesh = m.ToMesh("CountryRoad " + index + " (runtime)");
            meshes.Add(mesh);
            var go = new GameObject("Road_Asphalt_" + road.name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            built.Add(go);
        }

        float HalfWidthAt(float s) => Mathf.Lerp(roadStartHalfWidth, roadHalfWidth, Smooth(0f, 60f, s));

        // A quad whose winding is chosen so its face looks up.
        static void AddUp(EnvMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
        {
            if (Vector3.Cross(b - a, c - a).y >= 0f) m.Quad(a, b, c, d, colour);
            else m.Quad(a, d, c, b, colour);
        }

        void Clear()
        {
            for (int i = 0; i < built.Count; i++) if (built[i] != null) Destroy(built[i]);
            built.Clear();
            for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Destroy(meshes[i]);
            meshes.Clear();
        }

        void OnDestroy() => Clear();

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.3f);
            Vector3 c = new Vector3((yardMin.x + yardMax.x) * 0.5f, yardLevel, (yardMin.y + yardMax.y) * 0.5f);
            Gizmos.DrawWireCube(c, new Vector3(yardMax.x - yardMin.x, 0.1f, yardMax.y - yardMin.y));
        }
    }
}
