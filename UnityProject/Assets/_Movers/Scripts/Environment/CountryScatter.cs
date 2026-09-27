using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Movers
{
    // Everything that grows on the countryside (CountryLand) and in the grandmother's yard:
    // woods and field trees (BrokenVector's pack), hedgerows, stones, and lots of grass, wheat
    // and flowers. Nothing here is a GameObject per copy: all of it is drawn with GPU instancing
    // in chunks (InstancedChunks), per camera, so two split-screen views pay for what they see.
    //
    // - Trees and hedges are placed once at load, deterministically (same seed, same wood).
    //   Their chunks are squares, small near the yard and big far away, so a view from the yard
    //   draws few chunks, and only the near ones cast shadows. Beyond lodRadius the woods use
    //   the pack's simplest trees (60 to 160 triangles), scaled up. Trees close to the yard get
    //   a trunk collider; hedges and grass have none.
    // - Grass is made lazily in square chunks round each camera (a few milliseconds a frame),
    //   so it goes wherever the players go. In the yard it is laid where a ray finds the yard's
    //   own grass (its colliders named yardGroundPrefix), so it never grows through a path,
    //   the driveway, a floor or the street; outside, on the land, by what the field is.
    // - Wind, colour and lighting are the Movers/Environment shader's.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(900)]
    public sealed class CountryScatter : MonoBehaviour
    {
        public CountryLand land;
        public int seed = 5;

        [Header("Trees")]
        [Tooltip("Broadleaf trees of the woods (tree prefabs: one mesh, one material each). Some turn to autumn.")]
        public GameObject[] forestTrees = new GameObject[0];
        [Tooltip("Firs and cypresses of the woods: they never turn.")]
        public GameObject[] conifers = new GameObject[0];
        [Tooltip("Trees standing alone in the fields and in the yard.")]
        public GameObject[] fieldTrees = new GameObject[0];
        [Tooltip("The simplest trees of the pack, for the woods beyond lodRadius.")]
        public GameObject[] farTrees = new GameObject[0];
        [Tooltip("Movers/Environment material with the tree pack's colour sheet.")]
        public Material treeMaterial;
        [Tooltip("The same with the pack's autumn colour sheet (same layout).")]
        public Material autumnMaterial;
        [Range(0f, 1f)] public float autumnShare = 0.12f;
        [Range(0f, 1f)] public float coniferShare = 0.45f;
        public float forestSpacing = 7f;
        public float lodRadius = 210f;
        public float innerRadius = 430f;
        public float farSpacing = 15f;
        public float outerRadius = 950f;
        [Range(0f, 0.1f)] public float fieldTreeChance = 0.01f;
        [Tooltip("Big trees in the yard's margins, world X and Z.")]
        public Vector2[] yardTrees = new Vector2[0];
        public float treeColliderRange = 240f;
        public float treeDrawDistance = 700f;
        [Tooltip("Trees cast shadows only in chunks this close to the camera.")]
        public float treeShadowDistance = 55f;

        [Header("Hedges and stones")]
        public Material bushMaterial;
        [Tooltip("Pebbles (vertex coloured).")]
        public Material stoneMaterial;
        [Tooltip("The stone prefabs' material (plain colour).")]
        public Material rockMaterial;
        [Tooltip("Stone prefabs (one mesh each), scattered along hedges and wood edges.")]
        public GameObject[] rockPrefabs = new GameObject[0];
        public float hedgeRange = 170f;
        public float hedgeSpacing = 2f;
        [Range(0f, 1f)] public float hedgeFill = 0.85f;
        [Tooltip("Bushes along the outside of the garden fence: the fence's rectangle.")]
        public Vector2 gardenMin = new Vector2(-9f, -15f);
        public Vector2 gardenMax = new Vector2(30f, 18f);
        public float propDrawDistance = 260f;

        [Header("Grass, wheat and flowers")]
        public Material lawnGrass;
        public Material fieldGrass;
        public Material wheat;
        public Material flowers;
        public float chunkSize = 28f;
        public float grassDrawDistance = 48f;
        public float grassFullDensity = 18f;
        [Range(0.05f, 1f)] public float grassFarDensity = 0.25f;
        [Tooltip("Tufts per square metre on the yard's lawn.")]
        public float lawnDensity = 5f;
        public float fieldDensity = 2.8f;
        public float wheatDensity = 4f;
        [Tooltip("The yard's grass colliders are named with this prefix.")]
        public string yardGroundPrefix = "Grass_";
        [Tooltip("No grass here even on grass (the street), world X and Z.")]
        public Rect[] noGrass = { new Rect(-31f, -28.7f, 83f, 10.3f) };
        public float generateMsPerFrame = 3f;

        [Header("Debug")]
        [Tooltip("Switch a layer off to see what it costs (the frame rate in split screen).")]
        public bool drawTrees = true;
        public bool drawProps = true;
        public bool drawGrass = true;

        // Ring edges (metres from the yard's centre) and the chunk size in each ring, for the
        // trees and hedges.
        static readonly float[] Rings = { 0f, 200f, 450f };
        static readonly float[] ChunkSizes = { 80f, 160f, 320f };

        readonly InstancedChunks trees = new InstancedChunks();
        readonly InstancedChunks props = new InstancedChunks();
        readonly InstancedChunks grass = new InstancedChunks();
        readonly Dictionary<long, bool> grassDone = new Dictionary<long, bool>();
        readonly List<Vector2Int> spiral = new List<Vector2Int>();
        readonly List<Object> owned = new List<Object>();
        readonly Stopwatch watch = new Stopwatch();
        Camera[] cameras = new Camera[8];

        struct Kind
        {
            public Mesh mesh;
            public Matrix4x4 local;
            public Material material;
            public Material autumn;
        }

        Kind[] forestKinds, coniferKinds, fieldKinds, farKinds, rockKinds;
        Mesh bushMesh, grassMesh, wheatMesh, wildFlowerMesh, gardenFlowerMesh, pebbleMesh;
        Vector2 centre;
        Transform trunks;

        public int TreeCount { get; private set; }
        public int BushCount { get; private set; }
        public int GrassChunks => grass.ChunkCount;
        public int GrassInstances => grass.InstanceCount;
        public float SetupMs { get; private set; }

        void Start()
        {
            if (land == null) land = FindFirstObjectByType<CountryLand>();
            if (land == null) { enabled = false; return; }
            watch.Restart();
            centre = land.YardCentre;
            MakeMeshes();
            forestKinds = Kinds(forestTrees, treeMaterial, autumnMaterial);
            coniferKinds = Kinds(conifers, treeMaterial, null);
            fieldKinds = Kinds(fieldTrees, treeMaterial, autumnMaterial);
            farKinds = Kinds(farTrees, treeMaterial, null);
            rockKinds = Kinds(rockPrefabs, rockMaterial, null);

            trees.drawDistance = treeDrawDistance;
            trees.shadowDistance = treeShadowDistance;
            props.drawDistance = propDrawDistance;
            props.shadowDistance = 35f;
            grass.drawDistance = grassDrawDistance;
            grass.shadowDistance = 0f;
            grass.fullDensityDistance = grassFullDensity;
            grass.minDensity = grassFarDensity;

            PlaceTrees();
            PlaceHedges();
            BuildSpiral();
            // The first views get their grass now, the rest comes a few chunks a frame.
            int n = Camera.GetAllCameras(Grow());
            for (int i = 0; i < n; i++) GrowGrassAround(cameras[i], float.MaxValue);
            watch.Stop();
            SetupMs = (float)watch.Elapsed.TotalMilliseconds;
            UnityEngine.Debug.Log("[CountryScatter] " + TreeCount + " trees, " + BushCount + " bushes and stones, " +
                                  grass.InstanceCount + " tufts in " + grass.ChunkCount + " chunks, " + SetupMs.ToString("0") + " ms.");
        }

        Camera[] Grow()
        {
            if (cameras.Length < Camera.allCamerasCount) cameras = new Camera[Camera.allCamerasCount + 4];
            return cameras;
        }

        void LateUpdate()
        {
            int n = Camera.GetAllCameras(Grow());
            for (int i = 0; i < n; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (drawTrees) trees.Draw(cam);
                if (drawProps) props.Draw(cam);
                if (drawGrass)
                {
                    GrowGrassAround(cam, generateMsPerFrame);
                    grass.Draw(cam);
                }
            }
        }

        // ---- meshes and materials ----

        void MakeMeshes()
        {
            bushMesh = Own(EnvMesh.Bush(seed + 1, new Color(0.40f, 0.58f, 0.27f)));
            grassMesh = Own(EnvMesh.GrassTuft(seed + 2, 6, 0.42f, 0.13f, new Color(0.9f, 0.9f, 0.9f), Color.white));
            wheatMesh = Own(EnvMesh.WheatTuft(seed + 3, 7, 0.78f, new Color(0.86f, 0.74f, 0.43f), new Color(0.93f, 0.80f, 0.47f)));
            wildFlowerMesh = Own(WildFlowers(seed + 4));
            gardenFlowerMesh = Own(EnvMesh.FlowerTuft(seed + 5, 5, 0.42f, new Color(0.33f, 0.52f, 0.22f), new Color(0.93f, 0.42f, 0.55f), new Color(0.98f, 0.86f, 0.35f)));
            pebbleMesh = Own(EnvMesh.Pebble(seed + 6, new Color(0.62f, 0.60f, 0.56f)));
        }

        // Meadow flowers: white, yellow and blue stems in one tuft.
        static Mesh WildFlowers(int s)
        {
            var white = EnvMesh.FlowerTuft(s, 3, 0.36f, new Color(0.36f, 0.55f, 0.24f), new Color(0.97f, 0.96f, 0.92f), new Color(0.98f, 0.82f, 0.25f));
            var yellow = EnvMesh.FlowerTuft(s + 1, 2, 0.3f, new Color(0.36f, 0.55f, 0.24f), new Color(0.98f, 0.85f, 0.25f), new Color(0.9f, 0.6f, 0.15f));
            var blue = EnvMesh.FlowerTuft(s + 2, 2, 0.33f, new Color(0.36f, 0.55f, 0.24f), new Color(0.55f, 0.6f, 0.95f), new Color(0.98f, 0.95f, 0.8f));
            var ci = new CombineInstance[3];
            ci[0] = new CombineInstance { mesh = white, transform = Matrix4x4.identity };
            ci[1] = new CombineInstance { mesh = yellow, transform = Matrix4x4.Translate(new Vector3(0.09f, 0f, 0.05f)) };
            ci[2] = new CombineInstance { mesh = blue, transform = Matrix4x4.Translate(new Vector3(-0.07f, 0f, -0.08f)) };
            var m = new Mesh { name = "WildFlowers (runtime)", hideFlags = HideFlags.DontSave };
            m.CombineMeshes(ci, true, true);
            Object.Destroy(white);
            Object.Destroy(yellow);
            Object.Destroy(blue);
            return m;
        }

        // A prefab's mesh and the mesh's place in the prefab, drawn with `material` (a
        // Movers/Environment material: the batches are drawn procedurally, which the pack's
        // Standard materials cannot do).
        static Kind[] Kinds(GameObject[] prefabs, Material material, Material autumn)
        {
            var list = new List<Kind>();
            if (prefabs == null || material == null) return list.ToArray();
            for (int i = 0; i < prefabs.Length; i++)
            {
                var p = prefabs[i];
                if (p == null) continue;
                var mf = p.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Matrix4x4 inner = mf.transform == p.transform ? Matrix4x4.identity : p.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                list.Add(new Kind
                {
                    mesh = mf.sharedMesh,
                    local = Matrix4x4.Scale(p.transform.localScale) * inner,
                    material = material,
                    autumn = autumn,
                });
            }
            return list.ToArray();
        }

        T Own<T>(T o) where T : Object
        {
            if (o != null) owned.Add(o);
            return o;
        }

        // ---- trees ----

        // Sector chunks: key -> (batch key -> matrices), with bounds.
        sealed class Build
        {
            public readonly Dictionary<long, Dictionary<long, List<Matrix4x4>>> chunks = new Dictionary<long, Dictionary<long, List<Matrix4x4>>>();
            public readonly Dictionary<long, Bounds> bounds = new Dictionary<long, Bounds>();
            public readonly Dictionary<long, Mesh> meshes = new Dictionary<long, Mesh>();
            public readonly Dictionary<long, Material> materials = new Dictionary<long, Material>();
            public readonly Dictionary<long, int> submeshes = new Dictionary<long, int>();
            public readonly Dictionary<long, bool> shadows = new Dictionary<long, bool>();
        }

        long SectorKey(float x, float z)
        {
            float dx = x - centre.x, dz = z - centre.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            int ring = 0;
            while (ring < Rings.Length - 1 && r >= Rings[ring + 1]) ring++;
            float size = ChunkSizes[ring];
            long ix = Mathf.FloorToInt(dx / size) + 50000, iz = Mathf.FloorToInt(dz / size) + 50000;
            return ((long)ring << 40) | (ix << 20) | iz;
        }

        static void Put(Build b, long chunk, long batch, Mesh mesh, int submesh, Material material, bool castShadows, Matrix4x4 m, Vector3 pos, float radius, float height)
        {
            if (!b.chunks.TryGetValue(chunk, out var batches)) b.chunks.Add(chunk, batches = new Dictionary<long, List<Matrix4x4>>());
            if (!batches.TryGetValue(batch, out var list)) batches.Add(batch, list = new List<Matrix4x4>(64));
            list.Add(m);
            b.meshes[batch] = mesh;
            b.materials[batch] = material;
            b.submeshes[batch] = submesh;
            b.shadows[batch] = castShadows;
            var box = new Bounds(pos + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height, radius * 2f));
            if (b.bounds.TryGetValue(chunk, out Bounds old)) { old.Encapsulate(box); b.bounds[chunk] = old; }
            else b.bounds.Add(chunk, box);
        }

        static void Flush(Build b, InstancedChunks into)
        {
            foreach (var pair in b.chunks)
            {
                var chunk = new InstancedChunks.Chunk { bounds = b.bounds[pair.Key] };
                foreach (var batch in pair.Value)
                    InstancedChunks.AddBatches(chunk, b.meshes[batch.Key], b.submeshes[batch.Key], b.materials[batch.Key], batch.Value, b.shadows[batch.Key]);
                into.Add(chunk);
            }
        }

        void PlaceTrees()
        {
            if (forestKinds.Length == 0 && coniferKinds.Length == 0 && fieldKinds.Length == 0) return;
            var rng = new System.Random(seed);
            var b = new Build();
            trunks = new GameObject("TreeTrunks").transform;
            trunks.SetParent(transform, false);
            trunks.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // The woods near the yard, then the far woods on the hills, coarser and bigger.
            Grid(b, rng, forestSpacing, 0f, innerRadius, 0.85f, 1.45f, true);
            Grid(b, rng, farSpacing, innerRadius, outerRadius, 1.3f, 2.1f, false);
            // The yard's own big trees.
            for (int i = 0; i < yardTrees.Length && fieldKinds.Length > 0; i++)
            {
                Vector2 p = yardTrees[i];
                AddTree(b, rng, fieldKinds, p.x, p.y, 1.25f + 0.35f * (float)rng.NextDouble(), 0.15f);
            }
            Flush(b, trees);
        }

        void Grid(Build b, System.Random rng, float spacing, float r0, float r1, float s0, float s1, bool fieldTreesToo)
        {
            for (float x = centre.x - r1; x < centre.x + r1; x += spacing)
                for (float z = centre.y - r1; z < centre.y + r1; z += spacing)
                {
                    float px = x + ((float)rng.NextDouble() - 0.5f) * spacing * 0.9f;
                    float pz = z + ((float)rng.NextDouble() - 0.5f) * spacing * 0.9f;
                    float roll = (float)rng.NextDouble();
                    float scale = Mathf.Lerp(s0, s1, (float)rng.NextDouble());
                    float r = Vector2.Distance(new Vector2(px, pz), centre);
                    if (r < r0 || r >= r1) continue;
                    if (land.InYard(px, pz)) continue;
                    float f = land.ForestAt(px, pz);
                    if (roll < f)
                    {
                        // Far away, the simplest trees, bigger (they stand for a wood, not a tree).
                        if (r > lodRadius && farKinds.Length > 0) AddTree(b, rng, farKinds, px, pz, scale * 1.7f, 0f);
                        else if (coniferKinds.Length > 0 && (forestKinds.Length == 0 || rng.NextDouble() < coniferShare)) AddTree(b, rng, coniferKinds, px, pz, scale, 0f);
                        else if (forestKinds.Length > 0) AddTree(b, rng, forestKinds, px, pz, scale, autumnShare);
                        continue;
                    }
                    // A lone tree in the fields or on a hedge line.
                    if (!fieldTreesToo || fieldKinds.Length == 0) continue;
                    if (roll > 1f - fieldTreeChance && land.OutsideDistance(px, pz) > 14f && land.RoadDistance(px, pz, 12f) > 9f)
                        AddTree(b, rng, fieldKinds, px, pz, scale * 1.15f, autumnShare);
                }
        }

        void AddTree(Build b, System.Random rng, Kind[] kinds, float x, float z, float scale, float autumn)
        {
            int k = rng.Next(kinds.Length);
            float yaw = (float)rng.NextDouble() * 360f;
            bool fall = kinds[k].autumn != null && rng.NextDouble() < autumn;
            float y = land.InYard(x, z) ? land.yardLevel : land.HeightAt(x, z);
            var pos = new Vector3(x, y - 0.15f, z);
            Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale) * kinds[k].local;
            Material mat = fall ? kinds[k].autumn : kinds[k].material;
            // One batch per mesh and material, whichever list the tree came from.
            long batch = ((long)kinds[k].mesh.GetHashCode() << 2) | (fall ? 1L : 0L);
            Put(b, SectorKey(x, z), batch, kinds[k].mesh, 0, mat, true, m, pos, 4f * scale, 11f * scale);
            TreeCount++;
            if (Vector2.Distance(new Vector2(x, z), centre) < treeColliderRange)
            {
                var c = trunks.gameObject.AddComponent<CapsuleCollider>();
                c.center = pos + Vector3.up * 1.6f * scale;
                c.radius = 0.22f * scale;
                c.height = 3.2f * scale;
            }
        }

        // ---- hedges, bushes and stones ----

        void PlaceHedges()
        {
            var rng = new System.Random(seed + 101);
            var b = new Build();
            // Hedge lines along the fields' edges, round the yard.
            for (float x = centre.x - hedgeRange; x < centre.x + hedgeRange; x += hedgeSpacing)
                for (float z = centre.y - hedgeRange; z < centre.y + hedgeRange; z += hedgeSpacing)
                {
                    float px = x + ((float)rng.NextDouble() - 0.5f) * hedgeSpacing * 0.6f;
                    float pz = z + ((float)rng.NextDouble() - 0.5f) * hedgeSpacing * 0.6f;
                    float roll = (float)rng.NextDouble(), yaw = (float)rng.NextDouble() * 360f, s = (float)rng.NextDouble();
                    if (land.InYard(px, pz)) continue;
                    var g = land.GroundAt(px, pz);
                    if (g == CountryGround.Hedge && roll < hedgeFill)
                        AddBush(b, px, pz, yaw, Mathf.Lerp(1.4f, 2.2f, s));
                    else if ((g == CountryGround.Hedge || g == CountryGround.Forest || g == CountryGround.Soil) && roll > 0.985f && rockKinds.Length > 0)
                        AddRock(b, rng, px, pz, yaw, Mathf.Lerp(0.5f, 1.6f, s));
                }
            // Bushes along the outside of the garden fence, in clumps with gaps (not along the
            // front: the street side stays open).
            float o = 1.3f;
            BushRow(b, rng, new Vector2(gardenMin.x + 1f, gardenMax.y + o), new Vector2(gardenMax.x - 1f, gardenMax.y + o));
            BushRow(b, rng, new Vector2(gardenMin.x - o, gardenMin.y + 2f), new Vector2(gardenMin.x - o, gardenMax.y - 1f));
            BushRow(b, rng, new Vector2(gardenMax.x + o, gardenMin.y + 2f), new Vector2(gardenMax.x + o, gardenMax.y - 1f));
            // A bush or two at the foot of each yard tree.
            for (int i = 0; i < yardTrees.Length; i++)
            {
                AddBush(b, yardTrees[i].x + 2.2f, yardTrees[i].y + 1.1f, (float)rng.NextDouble() * 360f, 1.2f);
                if (rockKinds.Length > 0) AddRock(b, rng, yardTrees[i].x - 1.8f, yardTrees[i].y - 1.4f, (float)rng.NextDouble() * 360f, 0.7f);
            }
            Flush(b, props);
        }

        void BushRow(Build b, System.Random rng, Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            for (float s = 0f; s <= length; s += 1.9f)
            {
                float roll = (float)rng.NextDouble(), yaw = (float)rng.NextDouble() * 360f, k = (float)rng.NextDouble();
                // Gaps: a slow noise along the row decides where the clumps are.
                float n = Mathf.PerlinNoise(from.x * 0.1f + s * 0.12f, from.y * 0.1f + 3.3f);
                if (n < 0.42f || roll < 0.15f) continue;
                Vector2 p = Vector2.Lerp(from, to, s / Mathf.Max(0.01f, length));
                AddBush(b, p.x, p.y, yaw, Mathf.Lerp(0.9f, 1.5f, k));
            }
        }

        void AddBush(Build b, float x, float z, float yaw, float scale)
        {
            if (bushMaterial == null) return;
            float y = land.InYard(x, z) ? land.yardLevel : land.HeightAt(x, z);
            var pos = new Vector3(x, y - 0.08f, z);
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale * 0.9f, scale));
            Put(b, SectorKey(x, z), 1, bushMesh, 0, bushMaterial, true, m, pos, scale, scale * 1.2f);
            BushCount++;
        }

        void AddRock(Build b, System.Random rng, float x, float z, float yaw, float scale)
        {
            int k = rng.Next(rockKinds.Length);
            float y = land.InYard(x, z) ? land.yardLevel : land.HeightAt(x, z);
            var pos = new Vector3(x, y - 0.12f * scale, z);
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale) * rockKinds[k].local;
            Put(b, SectorKey(x, z), 10 + k, rockKinds[k].mesh, 0, rockKinds[k].material, true, m, pos, scale, scale);
            BushCount++;
        }

        // ---- grass ----

        void BuildSpiral()
        {
            spiral.Clear();
            int reach = Mathf.CeilToInt((grassDrawDistance + chunkSize) / chunkSize);
            for (int x = -reach; x <= reach; x++)
                for (int z = -reach; z <= reach; z++)
                    if ((x * x + z * z) * chunkSize * chunkSize <= (grassDrawDistance + chunkSize * 1.5f) * (grassDrawDistance + chunkSize * 1.5f))
                        spiral.Add(new Vector2Int(x, z));
            spiral.Sort((a, c) => (a.x * a.x + a.y * a.y).CompareTo(c.x * c.x + c.y * c.y));
        }

        void GrowGrassAround(Camera cam, float budgetMs)
        {
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            int cx = Mathf.FloorToInt(p.x / chunkSize), cz = Mathf.FloorToInt(p.z / chunkSize);
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < spiral.Count; i++)
            {
                int x = cx + spiral[i].x, z = cz + spiral[i].y;
                long key = ((long)x << 32) ^ (uint)z;
                if (grassDone.ContainsKey(key)) continue;
                grassDone.Add(key, true);
                grass.Add(GrassChunk(x, z));
                if (clock.Elapsed.TotalMilliseconds > budgetMs) return;
            }
        }

        bool NoGrass(float x, float z)
        {
            for (int i = 0; i < noGrass.Length; i++) if (noGrass[i].Contains(new Vector2(x, z))) return true;
            return false;
        }

        InstancedChunks.Chunk GrassChunk(int cx, int cz)
        {
            var rng = new System.Random(unchecked(seed * 7919 + cx * 92821 + cz * 68917));
            float x0 = cx * chunkSize, z0 = cz * chunkSize;
            float peak = Mathf.Max(lawnDensity, Mathf.Max(fieldDensity, wheatDensity));
            int candidates = Mathf.CeilToInt(chunkSize * chunkSize * peak);
            var lawn = new List<Matrix4x4>();
            var field = new List<Matrix4x4>();
            var ears = new List<Matrix4x4>();
            var wild = new List<Matrix4x4>();
            var garden = new List<Matrix4x4>();
            var stones = new List<Matrix4x4>();
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < candidates; i++)
            {
                float x = x0 + (float)rng.NextDouble() * chunkSize, z = z0 + (float)rng.NextDouble() * chunkSize;
                float pick = (float)rng.NextDouble() * peak, roll = (float)rng.NextDouble();
                float yaw = (float)rng.NextDouble() * 360f, size = (float)rng.NextDouble();
                float y;
                CountryGround g;
                if (land.InYard(x, z))
                {
                    if (pick >= lawnDensity || NoGrass(x, z)) continue;
                    if (!Physics.Raycast(new Vector3(x, 2.4f, z), Vector3.down, out RaycastHit hit, 3.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!hit.collider.name.StartsWith(yardGroundPrefix, System.StringComparison.Ordinal)) continue;
                    y = hit.point.y;
                    g = CountryGround.Yard;
                }
                else
                {
                    g = land.GroundAt(x, z);
                    if (pick >= Density(g)) continue;
                    y = land.HeightAt(x, z);
                }
                var pos = new Vector3(x, y - 0.02f, z);
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                List<Matrix4x4> into;
                float scale;
                switch (g)
                {
                    case CountryGround.Yard:
                        // Flower borders inside the garden fence, a few flowers on the lawn.
                        bool border = InGardenBorder(x, z);
                        if (roll < (border ? 0.3f : 0.02f)) { into = garden; scale = Mathf.Lerp(0.8f, 1.2f, size); }
                        else if (roll > 0.992f) { into = stones; scale = Mathf.Lerp(0.12f, 0.3f, size); }
                        // Her lawn is mowed: shorter than the fields.
                        else { into = lawn; scale = Mathf.Lerp(0.5f, 0.85f, size); }
                        break;
                    case CountryGround.Wheat:
                        into = ears; scale = Mathf.Lerp(0.85f, 1.15f, size);
                        break;
                    case CountryGround.Meadow:
                        if (roll < 0.14f) { into = wild; scale = Mathf.Lerp(0.8f, 1.3f, size); }
                        else { into = field; scale = Mathf.Lerp(0.8f, 1.4f, size); }
                        break;
                    case CountryGround.Lawn:
                    case CountryGround.Forest:
                        if (roll < 0.025f) { into = wild; scale = Mathf.Lerp(0.8f, 1.2f, size); }
                        else { into = lawn; scale = Mathf.Lerp(0.7f, 1.2f, size); }
                        break;
                    case CountryGround.Soil:
                        if (roll < 0.12f) { into = stones; scale = Mathf.Lerp(0.1f, 0.25f, size); }
                        else { into = field; scale = Mathf.Lerp(0.5f, 0.9f, size); }
                        break;
                    default:
                        if (roll < 0.02f) { into = wild; scale = Mathf.Lerp(0.8f, 1.2f, size); }
                        else { into = field; scale = Mathf.Lerp(0.75f, 1.3f, size); }
                        break;
                }
                into.Add(Matrix4x4.TRS(pos, rot, Vector3.one * scale));
                minY = Mathf.Min(minY, pos.y);
                maxY = Mathf.Max(maxY, pos.y);
            }
            if (minY > maxY) return null;
            var chunk = new InstancedChunks.Chunk
            {
                bounds = new Bounds(new Vector3(x0 + chunkSize * 0.5f, (minY + maxY) * 0.5f + 0.5f, z0 + chunkSize * 0.5f),
                                    new Vector3(chunkSize + 1f, maxY - minY + 2f, chunkSize + 1f)),
            };
            InstancedChunks.AddBatches(chunk, grassMesh, 0, lawnGrass, lawn, false);
            InstancedChunks.AddBatches(chunk, grassMesh, 0, fieldGrass, field, false);
            InstancedChunks.AddBatches(chunk, wheatMesh, 0, wheat, ears, false);
            InstancedChunks.AddBatches(chunk, wildFlowerMesh, 0, flowers, wild, false);
            InstancedChunks.AddBatches(chunk, gardenFlowerMesh, 0, flowers, garden, false);
            InstancedChunks.AddBatches(chunk, pebbleMesh, 0, stoneMaterial, stones, false);
            return chunk;
        }

        float Density(CountryGround g)
        {
            switch (g)
            {
                case CountryGround.Wheat: return wheatDensity;
                case CountryGround.Lawn: return lawnDensity * 0.8f;
                case CountryGround.Verge: return fieldDensity;
                case CountryGround.Soil: return 0.35f;
                case CountryGround.Forest: return fieldDensity * 0.35f;
                case CountryGround.Road:
                case CountryGround.Far: return 0f;
                default: return fieldDensity;
            }
        }

        bool InGardenBorder(float x, float z)
        {
            if (x < gardenMin.x || x > gardenMax.x || z < gardenMin.y || z > gardenMax.y) return false;
            float d = Mathf.Min(Mathf.Min(x - gardenMin.x, gardenMax.x - x), Mathf.Min(z - gardenMin.y, gardenMax.y - z));
            return d < 1.2f;
        }

        void OnDestroy()
        {
            for (int i = 0; i < owned.Count; i++) if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
            trees.Clear();
            props.Clear();
            grass.Clear();
        }
    }
}
