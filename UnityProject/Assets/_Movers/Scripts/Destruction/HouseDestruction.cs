using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Movers
{
    // Makes the grandmother's house breakable when the scene starts, so nothing has to be
    // authored piece by piece and the kit stays exactly as Pierre built it.
    //
    // Four passes, in this order:
    // a) Glass. Kit windows carry their glass as one submesh of the wall mesh, so it cannot
    //    break on its own. Each glass submesh is cut out of the module and every separate
    //    pane becomes a child "GlassPane" with its own collider. Many walls share one FBX
    //    mesh, so each distinct mesh is split once and the result reused.
    // b) Structure, from the module catalog (by the kit module name of each mesh):
    //    - a wall with pre-fractured chunk sets gets a DestructibleModule (breaks chunk by chunk);
    //    - any other breakable piece gets a structural Breakable (breaks whole, as before);
    //    - the foundation gets a Breakable capped at Damaged;
    //    - roof pieces get a RoofSection (they never break, they may fall);
    //    - floors, stairs, plinths and steps are only remembered, as what holds the rest up.
    //    Plinths are taken out of the ground-floor walls first: they are foundation, and used
    //    to shatter with the wall above them.
    // c) Furniture. Every MovableObject gets a Breakable with a material guessed from its
    //    name, except the cigarette and the beer, which already handle their own ends.
    // d) The structure graph: what rests on what, built once from the pieces' boxes.
    //
    // It then owns the graph for the session: it ticks the collapse queue every physics step,
    // and it is what the destruction reset (F11) rebuilds.
    //
    // Runs before everything else (-200) so any system that looks for Breakables or
    // GlassPanes in its own Awake or Start finds them already there.
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class HouseDestruction : MonoBehaviour
    {
        [Tooltip("Left empty, the object named GrandmaHouse_PierreKit is used.")]
        public Transform houseRoot;
        public bool splitGlass = true;
        public bool breakableStructure = true;
        public bool breakableMovables = true;

        [Header("Data (optional: code defaults when empty)")]
        public DestructionMaterialTable materials;
        public DestructibleModuleCatalog catalog;

        [Header("Structure")]
        [Tooltip("World height of the ground floor's top. Anything whose bottom is at or under it stands on the ground.")]
        public float groundLevel = 0.30f;
        public float groundTolerance = 0.06f;
        [Tooltip("m: a piece sits on another when its bottom and the other's top are this close.")]
        public float contactTolerance = 0.05f;
        [Tooltip("m: overlap under which two pieces only touch (the kit's 0.1 m joins are slivers, not seats).")]
        public float minFootprint = 0.15f;
        [Tooltip("Sideways steps a chunk may hang from its neighbours inside one wall.")]
        public int chunkLateralHops = 2;
        [Tooltip("Sideways steps a roof section or an upper floor may hang from the piece beside it.")]
        public int hangingHops = 1;
        [Tooltip("Pieces released per frame at most when something collapses (the rest wait for the next frames).")]
        public int collapsePerFrame = 40;
        [Tooltip("Random delay (s) before each unsupported piece falls, so a wall crumbles instead of dropping as one.")]
        public float collapseStagger = 0.3f;

        [Header("Debug")]
        [Tooltip("Adds the destruction debug keys (F3, F9, F10, F11) in the editor and development builds.")]
        public bool debugTools = true;

        const string DefaultRootName = "GrandmaHouse_PierreKit";
        const float WeldDistance = 1e-4f;   // corners closer than this belong to the same pane

        public static HouseDestruction Instance { get; private set; }

        // Setup figures, for the log, the overlay and the tests.
        public int PaneCount { get; private set; }
        public int ChunkedWallCount { get; private set; }
        public int WholePieceCount { get; private set; }
        public int RoofSectionCount { get; private set; }
        public int FixedPieceCount { get; private set; }
        public float SetupMs { get; private set; }
        public StructureGraph Graph => graph;

        // Meshes built here die with this component, so nothing leaks across play sessions.
        readonly List<Mesh> createdMeshes = new List<Mesh>();
        DestructibleModuleCatalog runtimeCatalog;
        StructureGraph graph;
        int collapseFrame = -1;      // the frame the collapse budget below belongs to
        int releasedThisFrame;

        // Everything that is a node of the graph, with what the graph needs to rebuild it after
        // a reset. Bounds are taken once: the house stands still until it breaks.
        struct Part
        {
            public Component component;
            public IStructurePart owner;
            public StructureGraph.NodeKind kind;
            public Bounds bounds;
            public bool anchor, neverFalls, hangs;
        }
        readonly List<Part> parts = new List<Part>(256);

        // Where each movable started, so the reset can put back the ones that were destroyed.
        struct StartPose
        {
            public MovableObject mo;
            public Transform parent;
            public Vector3 position;
            public Quaternion rotation;
        }
        readonly List<StartPose> startPoses = new List<StartPose>(128);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        // Nothing under these groups breaks, whatever its mesh, except what the catalog says
        // may (the gables). Roofs, floors and stairs are still read as supports.
        static readonly string[] SolidGroups = { "Roofs", "Stairs", "Floors" };

        // ---- the material guess for furniture ----

        // Read from the last word back, because the last word of an English name is the thing
        // itself: a "Table Lamp" is a lamp, a "Wine Barrel" is a barrel, a "Sewing Desk" a desk.
        static readonly string[] WoodWords =
        {
            "table", "desk", "barrel", "crate", "box", "shelf", "shelves", "bookshelf", "cabinet",
            "chest", "bench", "dresser", "wardrobe", "nightstand", "trunk", "rack", "workbench",
            "vanity", "piano", "stand", "chair", "book", "broom", "painting",
        };
        static readonly string[] GlassWords =
        {
            "bottle", "wine", "perfume", "flask", "mirror", "television", "tv", "glass", "glasses",
            "lamp", "photo", "frame", "microscope", "globe", "clock",
        };
        static readonly string[] CeramicWords =
        {
            "vase", "plate", "cup", "jar", "candle", "candlestick", "bowl", "teapot", "cage", "ship",
        };
        static readonly string[] MetalWords =
        {
            "fridge", "stove", "bucket", "wheelbarrow", "kettle", "pot", "pan", "telescope", "sewing",
            "axe", "hammer", "saw", "shovel", "keys", "key", "utensil",
        };
        static readonly string[] FabricWords =
        {
            "sofa", "armchair", "bed", "cushion", "rug", "basket", "yarn", "slipper", "dress", "suitcase",
        };

        // ---- setup ----

        void Awake()
        {
            double t0 = Time.realtimeSinceStartupAsDouble;
            Instance = this;
            DestructionMaterialTable.Use(materials);
            if (catalog == null)
            {
                runtimeCatalog = DestructibleModuleCatalog.CreateDefault();
                catalog = runtimeCatalog;
            }
            BlastSolver.ForgetColliders();

            if (houseRoot == null)
            {
                GameObject found = GameObject.Find(DefaultRootName);
                if (found != null) houseRoot = found.transform;
            }

            int panes = 0, modules = 0, distinct = 0, movables = 0;
            var unreadable = new List<string>();

            if (houseRoot != null)
            {
                if (splitGlass) SplitAllGlass(ref panes, ref modules, ref distinct, unreadable);
                if (breakableStructure) WireStructure();
            }
            else Debug.LogWarning("[HouseDestruction] no house root and no object named " + DefaultRootName +
                                  ": windows and walls stay unbreakable.");

            if (breakableMovables) movables = WireMovables();
            RecordStartPoses();
            PaneCount = panes;

            var selfSupported = new List<int>();
            BuildGraph(selfSupported);
            // Online client: the same parts and colliders, but no graph running. Nothing falls for
            // lack of support here; the host's structure records say what fell (NETCODE_SLICE 11.4).
            if (Net.IsClient) StructureGraph.Uninstall(graph);

            if (debugTools && Debug.isDebugBuild && !TryGetComponent(out DestructionDebug _))
                gameObject.AddComponent<DestructionDebug>();

            SetupMs = (float)((Time.realtimeSinceStartupAsDouble - t0) * 1000.0);
            Debug.Log("[HouseDestruction] glass: " + panes + " panes on " + modules + " modules (" + distinct +
                      " distinct meshes)  |  structure: " + ChunkedWallCount + " chunked walls, " + WholePieceCount +
                      " whole pieces, " + RoofSectionCount + " roof sections, " + FixedPieceCount + " supports  |  movables: " +
                      movables + " breakable  |  graph: " + graph.NodeCount + " nodes, " + graph.EdgeCount / 2 + " edges, " +
                      selfSupported.Count + " standing as built  |  " + SetupMs.ToString("0") + " ms");
            if (selfSupported.Count > 0) Debug.Log("[HouseDestruction] held up as built (no support found in the kit boxes): " + Names(selfSupported, 12));
            if (unreadable.Count > 0)
                Debug.LogWarning("[HouseDestruction] " + unreadable.Count + " kit mesh(es) with glass are not readable, " +
                                 "their windows cannot break: " + string.Join(", ", unreadable) +
                                 ". Tick Read/Write on these model imports.");
        }

        void OnDestroy()
        {
            for (int i = 0; i < createdMeshes.Count; i++)
                if (createdMeshes[i] != null) Destroy(createdMeshes[i]);
            createdMeshes.Clear();
            if (graph != null)
            {
                graph.Queue.Clear();
                StructureGraph.Uninstall(graph);
            }
            if (runtimeCatalog != null) Destroy(runtimeCatalog);
            // On a scene reload the next house may already be awake: only clear what is ours.
            if (Instance == this)
            {
                Instance = null;
                BlastSolver.ForgetColliders();
                DestructionMaterialTable.Use(null);
            }
        }

        // Collapse runs on the physics clock: pieces are released as rigidbodies, at most
        // collapsePerFrame of them a frame (a slow frame runs two physics steps; they share the
        // budget). Breaks from collisions (reported during the last step) are resolved here
        // too; a blast resolves its own at once.
        void FixedUpdate()
        {
            if (!Net.HasAuthority) return;
            if (graph == null) return;
            graph.Queue.maxStagger = Mathf.Max(0f, collapseStagger);
            graph.ResolvePending();
            if (graph.Queue.Count == 0) return;
            int frame = Time.frameCount;
            if (frame != collapseFrame)
            {
                collapseFrame = frame;
                releasedThisFrame = 0;
            }
            int budget = Mathf.Max(1, collapsePerFrame) - releasedThisFrame;
            if (budget <= 0) return;
            releasedThisFrame += graph.Queue.Tick(graph, Time.time, budget);
            graph.ResolvePending();
        }

        string Names(List<int> nodes, int max)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < nodes.Count && i < max; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(graph.Describe(nodes[i]));
            }
            if (nodes.Count > max) sb.Append(" and " + (nodes.Count - max) + " more");
            return sb.ToString();
        }

        // ---- a) glass ----

        sealed class GlassSplit
        {
            public Mesh body;                                   // null when the module was all glass
            public int[] kept;                                  // original submesh index of each body submesh
            public readonly List<Mesh> panes = new List<Mesh>();
            public readonly List<int> paneSubmesh = new List<int>();   // which glass submesh each pane came from
        }

        struct SplitKey : IEquatable<SplitKey>
        {
            public Mesh mesh;
            public ulong glassMask;
            public int slots;

            public bool Equals(SplitKey o)
            {
                return ReferenceEquals(mesh, o.mesh) && glassMask == o.glassMask && slots == o.slots;
            }
            public override bool Equals(object obj) { return obj is SplitKey k && Equals(k); }
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = mesh != null ? RuntimeHelpers.GetHashCode(mesh) : 0;
                    return (h * 397) ^ glassMask.GetHashCode() ^ (slots * 7919);
                }
            }
        }

        void SplitAllGlass(ref int panes, ref int modules, ref int distinct, List<string> unreadable)
        {
            MeshRenderer[] renderers = houseRoot.GetComponentsInChildren<MeshRenderer>(true);
            var cache = new Dictionary<SplitKey, GlassSplit>();

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || r.isPartOfStaticBatch) continue;
                if (!r.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                Mesh src = filter.sharedMesh;
                GameObject go = r.gameObject;

                // Doors swing and get their own treatment; anything with a body moves.
                if (go.name.Contains("Door_Leaf") || src.name.Contains("Door_Leaf")) continue;
                if (r.GetComponentInParent<Rigidbody>(true) != null) continue;
                if (go.TryGetComponent(out GlassPane _) || HasPaneChild(r.transform)) continue;

                Material[] mats = r.sharedMaterials;
                int slots = Mathf.Min(src.subMeshCount, mats.Length);
                ulong mask = GlassMask(src, mats, slots);
                if (mask == 0) continue;

                if (!src.isReadable)
                {
                    if (!unreadable.Contains(src.name)) unreadable.Add(src.name);
                    continue;
                }

                var key = new SplitKey { mesh = src, glassMask = mask, slots = slots };
                if (!cache.TryGetValue(key, out GlassSplit split))
                {
                    split = BuildSplit(src, mask, slots);
                    cache[key] = split;   // null is cached too: "no glass triangles here"
                    if (split != null) distinct++;
                }
                if (split == null || split.panes.Count == 0) continue;

                ApplySplit(r, filter, src, mats, split);
                modules++;
                panes += split.panes.Count;
            }
        }

        static ulong GlassMask(Mesh mesh, Material[] mats, int slots)
        {
            ulong mask = 0;
            int n = Mathf.Min(slots, 64);
            for (int s = 0; s < n; s++)
            {
                Material m = mats[s];
                if (m == null || !m.name.StartsWith("PK_glass", StringComparison.OrdinalIgnoreCase)) continue;
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                mask |= 1UL << s;
            }
            return mask;
        }

        static bool HasPaneChild(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).TryGetComponent(out GlassPane _)) return true;
            return false;
        }

        GlassSplit BuildSplit(Mesh src, ulong mask, int slots)
        {
            var kept = new List<int>();
            var glass = new List<int>();
            int glassTriangles = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                bool isGlass = s < 64 && (mask & (1UL << s)) != 0;
                if (isGlass)
                {
                    glass.Add(s);
                    glassTriangles += (int)(src.GetIndexCount(s) / 3);
                }
                // Submeshes past the material array were never drawn; the body drops them.
                else if (s < slots) kept.Add(s);
            }
            if (glassTriangles == 0) return null;

            var split = new GlassSplit { kept = kept.ToArray() };
            split.body = kept.Count > 0 ? BuildBody(src, kept) : null;

            var verts = new List<Vector3>(src.vertexCount);
            src.GetVertices(verts);
            for (int g = 0; g < glass.Count; g++) BuildPanes(src, glass[g], verts, split);
            return split;
        }

        // The module without its glass: same vertices and attributes, fewer submeshes. It keeps
        // the source name so anything that recognises kit pieces by mesh name still does.
        Mesh BuildBody(Mesh src, List<int> kept)
        {
            var body = new Mesh { name = src.name, indexFormat = src.indexFormat };
            int vc = src.vertexCount;

            var v3 = new List<Vector3>(vc);
            src.GetVertices(v3);
            body.SetVertices(v3);
            src.GetNormals(v3);
            if (v3.Count == vc) body.SetNormals(v3);

            var v4 = new List<Vector4>(vc);
            src.GetTangents(v4);
            if (v4.Count == vc) body.SetTangents(v4);

            var colors = new List<Color>(vc);
            src.GetColors(colors);
            if (colors.Count == vc) body.SetColors(colors);

            var uv2 = new List<Vector2>(vc);
            for (int ch = 0; ch < 8; ch++)
            {
                var attr = (UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + ch);
                if (!src.HasVertexAttribute(attr)) continue;
                if (src.GetVertexAttributeDimension(attr) == 2)
                {
                    src.GetUVs(ch, uv2);
                    if (uv2.Count == vc) body.SetUVs(ch, uv2);
                }
                else
                {
                    src.GetUVs(ch, v4);
                    if (v4.Count == vc) body.SetUVs(ch, v4);
                }
            }

            body.subMeshCount = kept.Count;
            var indices = new List<int>();
            for (int k = 0; k < kept.Count; k++)
            {
                src.GetIndices(indices, kept[k]);
                body.SetIndices(indices, src.GetTopology(kept[k]), k, false);
            }
            body.RecalculateBounds();
            createdMeshes.Add(body);
            return body;
        }

        // Splits one glass submesh into panes: triangles that share a corner position belong
        // to the same pane. Positions, not vertex indices, because a flat-shaded box of glass
        // has a separate vertex per face at every corner.
        void BuildPanes(Mesh src, int submesh, List<Vector3> verts, GlassSplit split)
        {
            var indices = new List<int>();
            src.GetIndices(indices, submesh);
            int triCount = indices.Count / 3;
            if (triCount == 0) return;

            int vc = verts.Count;
            var posId = new int[vc];
            for (int i = 0; i < vc; i++) posId[i] = -1;
            var cells = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < triCount * 3; i++)
            {
                int v = indices[i];
                if (posId[v] >= 0) continue;
                Vector3 p = verts[v] / WeldDistance;
                var cell = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (!cells.TryGetValue(cell, out int id)) { id = cells.Count; cells[cell] = id; }
                posId[v] = id;
            }

            var parent = new int[cells.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int t = 0; t < triCount; t++)
            {
                int a = posId[indices[t * 3]];
                Union(parent, a, posId[indices[t * 3 + 1]]);
                Union(parent, a, posId[indices[t * 3 + 2]]);
            }

            var islandOfRoot = new Dictionary<int, int>();
            var islands = new List<List<int>>();
            for (int t = 0; t < triCount; t++)
            {
                int root = Find(parent, posId[indices[t * 3]]);
                if (!islandOfRoot.TryGetValue(root, out int island))
                {
                    island = islands.Count;
                    islandOfRoot[root] = island;
                    islands.Add(new List<int>());
                }
                islands[island].Add(t);
            }

            var normals = new List<Vector3>(vc);
            src.GetNormals(normals);
            bool hasNormals = normals.Count == vc;
            var uvs = new List<Vector2>(vc);
            src.GetUVs(0, uvs);
            bool hasUVs = uvs.Count == vc;

            var remap = new int[vc];
            for (int i = 0; i < vc; i++) remap[i] = -1;

            for (int isl = 0; isl < islands.Count; isl++)
            {
                List<int> tris = islands[isl];
                var pv = new List<Vector3>();
                var pn = new List<Vector3>();
                var pu = new List<Vector2>();
                var pi = new List<int>(tris.Count * 3);
                var used = new List<int>();
                for (int j = 0; j < tris.Count; j++)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int v = indices[tris[j] * 3 + corner];
                        if (remap[v] < 0)
                        {
                            remap[v] = pv.Count;
                            used.Add(v);
                            pv.Add(verts[v]);
                            if (hasNormals) pn.Add(normals[v]);
                            if (hasUVs) pu.Add(uvs[v]);
                        }
                        pi.Add(remap[v]);
                    }
                }
                for (int j = 0; j < used.Count; j++) remap[used[j]] = -1;

                var pane = new Mesh { name = src.name + "_Glass" + split.panes.Count };
                pane.SetVertices(pv);
                if (hasNormals) pane.SetNormals(pn);
                if (hasUVs) pane.SetUVs(0, pu);
                pane.SetTriangles(pi, 0, true);
                if (!hasNormals) pane.RecalculateNormals();
                createdMeshes.Add(pane);
                split.panes.Add(pane);
                split.paneSubmesh.Add(submesh);
            }
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[ra] = rb;
        }

        static void ApplySplit(MeshRenderer r, MeshFilter filter, Mesh src, Material[] mats, GlassSplit split)
        {
            if (split.body != null)
            {
                filter.sharedMesh = split.body;
                var kept = new Material[split.kept.Length];
                for (int i = 0; i < kept.Length; i++) kept[i] = mats[split.kept[i]];
                r.sharedMaterials = kept;
            }
            else
            {
                // Nothing but glass: keep the mesh (its name still identifies the module),
                // just stop drawing and colliding with it. The panes take over both.
                r.enabled = false;
            }

            // Only the collider that was the render mesh is swapped: a custom collision mesh
            // is somebody's deliberate choice and is left alone.
            if (r.TryGetComponent(out MeshCollider mc) && (mc.sharedMesh == src || mc.sharedMesh == null))
            {
                if (split.body != null) mc.sharedMesh = split.body;
                else mc.enabled = false;
            }

            for (int i = 0; i < split.panes.Count; i++)
            {
                int sub = split.paneSubmesh[i];
                CreatePane(r, split.panes[i], sub < mats.Length ? mats[sub] : null);
            }
        }

        static void CreatePane(MeshRenderer owner, Mesh mesh, Material glass)
        {
            var go = new GameObject("GlassPane");
            go.layer = DestructionLayers.Glass >= 0 ? DestructionLayers.Glass : owner.gameObject.layer;
            go.transform.SetParent(owner.transform, false);   // local identity: the mesh is in module space

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glass;
            mr.shadowCastingMode = owner.shadowCastingMode;
            mr.receiveShadows = owner.receiveShadows;
            mr.lightProbeUsage = owner.lightProbeUsage;
            mr.reflectionProbeUsage = owner.reflectionProbeUsage;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            go.AddComponent<GlassPane>();
        }

        // ---- b) structure ----

        void WireStructure()
        {
            FreePlinths();
            int structure = DestructionLayers.Structure;
            MeshFilter[] filters = houseRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter f = filters[i];
                if (f == null || f.sharedMesh == null) continue;
                GameObject go = f.gameObject;
                // A pane already has its own health and breaks on its own; its wall takes it
                // along. A second, structural health model would fight it.
                if (go.TryGetComponent(out GlassPane _)) continue;
                if (!catalog.TryGet(f.sharedMesh.name, out var entry)) continue;
                if (f.GetComponentInParent<MovableObject>(true) != null) continue;
                bool inSolidGroup = UnderSolidGroup(f.transform, houseRoot);
                Bounds b = PieceBounds(go);
                bool onGround = b.min.y <= groundLevel + groundTolerance;

                switch (entry.kind)
                {
                    case DestructibleModuleCatalog.Kind.RoofSection:
                    {
                        if (go.TryGetComponent(out RoofSection _)) break;
                        var roof = go.AddComponent<RoofSection>();
                        roof.Bind(entry);
                        DestructionLayers.Assign(go, structure);
                        AddPart(roof, roof, StructureGraph.NodeKind.Roof, b, false, false, true);
                        RoofSectionCount++;
                        break;
                    }
                    case DestructibleModuleCatalog.Kind.Floor:
                    {
                        // The ground slab holds; an upper slab carries, and may lose what it rests on.
                        DestructionLayers.Assign(go, structure);
                        var fixedPart = new FixedPart(go.name);
                        if (onGround || entry.anchor) AddPart(go.transform, fixedPart, StructureGraph.NodeKind.Floor, b, true, true, false);
                        else AddPart(go.transform, fixedPart, StructureGraph.NodeKind.Floor, b, false, true, true);
                        FixedPieceCount++;
                        break;
                    }
                    case DestructibleModuleCatalog.Kind.Stairs:
                    case DestructibleModuleCatalog.Kind.Footing:
                    {
                        DestructionLayers.Assign(go, structure);
                        AddPart(go.transform, new FixedPart(go.name), StructureGraph.NodeKind.Fixed, b, true, true, false);
                        FixedPieceCount++;
                        break;
                    }
                    default:
                    {
                        if (inSolidGroup && !entry.inSolidGroup) break;
                        if (go.TryGetComponent(out Breakable _) || go.TryGetComponent(out DestructibleModule _)) break;
                        // A door leaf in its wall hangs from the wall: it breaks with it, it is not
                        // a support of its own.
                        bool attachment = HasStructuralParent(f.transform);
                        bool anchor = entry.anchor || onGround;
                        DestructionLayers.Assign(go, structure);

                        var variant = entry.kind == DestructibleModuleCatalog.Kind.Wall ? entry.PickVariant(go.transform.position) : null;
                        if (variant != null && !attachment)
                        {
                            var module = go.AddComponent<DestructibleModule>();
                            module.Bind(entry, variant, anchor);
                            AddPart(module, module, StructureGraph.NodeKind.Module, module.WorldBounds, anchor, false, false);
                            ChunkedWallCount++;
                        }
                        else
                        {
                            var br = go.AddComponent<Breakable>();
                            br.Configure(entry.material, entry.health, true);
                            br.stateCap = entry.stateCap;
                            if (!attachment) AddPart(br, br, StructureGraph.NodeKind.Element, br.WorldBounds, anchor,
                                                     entry.stateCap < DestructionState.Destroyed, false);
                            WholePieceCount++;
                        }
                        break;
                    }
                }
            }
        }

        // Every plinth that is the child of a wall module moves out from under it, to a
        // "Foundation" group under the house (same place in the world): it is foundation, and a
        // wall that shatters or breaks up takes its own children along.
        void FreePlinths()
        {
            Transform foundation = null;
            MeshFilter[] filters = houseRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter f = filters[i];
                if (f == null || f.sharedMesh == null) continue;
                if (!catalog.TryGet(f.sharedMesh.name, out var entry) || entry.kind != DestructibleModuleCatalog.Kind.Footing) continue;
                Transform parent = f.transform.parent;
                if (parent == null || !parent.TryGetComponent(out MeshFilter pf) || pf.sharedMesh == null) continue;
                if (!catalog.TryGet(pf.sharedMesh.name, out var wall) || !wall.IsDamageable) continue;
                if (foundation == null)
                {
                    foundation = houseRoot.Find("Foundation");
                    if (foundation == null)
                    {
                        foundation = new GameObject("Foundation").transform;
                        foundation.SetParent(houseRoot, false);
                    }
                }
                f.transform.SetParent(foundation, true);
                f.gameObject.name = parent.name + "_" + f.gameObject.name;
            }
        }

        static bool HasStructuralParent(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.TryGetComponent(out Breakable _) || p.TryGetComponent(out DestructibleModule _)) return true;
            return false;
        }

        // The box the graph judges a piece by: its own renderer (not its children), or its
        // collider when the renderer is gone (a module that was all glass).
        static Bounds PieceBounds(GameObject go)
        {
            if (go.TryGetComponent(out MeshRenderer r) && r.enabled) return r.bounds;
            // A disabled collider reports an empty box at the origin: only an enabled one counts.
            if (go.TryGetComponent(out Collider c) && c.enabled) return c.bounds;
            if (r != null) return r.bounds;
            return new Bounds(go.transform.position, Vector3.zero);
        }

        void AddPart(Component component, IStructurePart owner, StructureGraph.NodeKind kind, Bounds b,
                     bool anchor, bool neverFalls, bool hangs)
        {
            parts.Add(new Part
            {
                component = component,
                owner = owner,
                kind = kind,
                bounds = b,
                anchor = anchor,
                neverFalls = neverFalls,
                hangs = hangs,
            });
        }

        static bool UnderSolidGroup(Transform t, Transform root)
        {
            for (Transform p = t; p != null && p != root; p = p.parent)
                for (int i = 0; i < SolidGroups.Length; i++)
                    if (p.name == SolidGroups[i]) return true;
            return false;
        }

        // Kept from the old table API: what the catalog says about a mesh name.
        public static bool TryStructuralSpec(string meshName, out BreakMaterial material, out float health)
        {
            var c = Instance != null ? Instance.catalog : null;
            DestructibleModuleCatalog.Entry e = null;
            bool found = false;
            if (c != null) found = c.TryGet(meshName, out e);
            else
            {
                var defaults = DestructibleModuleCatalog.Defaults();
                for (int i = 0; i < defaults.Length && !found; i++)
                    if (!string.IsNullOrEmpty(meshName) && DestructibleModuleCatalog.IsModule(meshName, defaults[i].module)) { e = defaults[i]; found = true; }
            }
            found = found && e.IsDamageable;
            material = found ? e.material : BreakMaterial.Wood;
            health = found ? e.health : 0f;
            return found;
        }

        // ---- d) the structure graph ----

        void BuildGraph(List<int> selfSupported)
        {
            if (graph != null)
            {
                graph.Queue.Clear();
                StructureGraph.Uninstall(graph);
            }
            graph = new StructureGraph
            {
                contactTolerance = contactTolerance,
                minFootprint = minFootprint,
                chunkLateralHops = Mathf.Max(0, chunkLateralHops),
                hangingHops = Mathf.Max(0, hangingHops),
            };
            graph.Queue.maxStagger = Mathf.Max(0f, collapseStagger);

            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p.component == null) continue;
                int node = graph.AddNode(p.bounds, p.kind, p.owner, -1, p.anchor, p.neverFalls, p.hangs);
                if (p.component is DestructibleModule m) m.SetGraphNode(node);
                else if (p.component is Breakable br) br.GraphNode = node;
                else if (p.component is RoofSection roof) roof.GraphNode = node;
            }
            graph.BuildAdjacency();
            graph.AnchorUnsupportedAsBuilt(selfSupported);
            StructureGraph.Install(graph);
        }

        // ---- the destruction reset (F11) ----

        // Everything destruction did, undone: debris gone, walls and roofs back, props and
        // panes repaired, destroyed movables back where the level started them, the graph
        // rebuilt. Movables that were only moved stay where the crew left them.
        public void ResetDestruction()
        {
            if (!Net.HasAuthority) return;   // host only; F11 is refused online anyway
            if (graph != null) graph.Queue.Clear();
            var debris = DebrisManager.Existing;
            if (debris != null) debris.Clear();

            var modules = DestructibleModule.All;
            for (int i = modules.Count - 1; i >= 0; i--)
                if (modules[i] != null) modules[i].Revive();
            var roofs = RoofSection.All;
            for (int i = roofs.Count - 1; i >= 0; i--)
                if (roofs[i] != null) roofs[i].Revive();

            int back = 0;
            for (int i = 0; i < startPoses.Count; i++)
            {
                var s = startPoses[i];
                if (s.mo == null || !s.mo.destroyed) continue;
                s.mo.transform.SetParent(s.parent, false);
                s.mo.transform.localPosition = s.position;
                s.mo.transform.localRotation = s.rotation;
                back++;
            }
            int repaired = Breakable.ReviveAll();
            for (int i = 0; i < startPoses.Count; i++)
            {
                var s = startPoses[i];
                if (s.mo == null || s.mo.rb == null || s.mo.rb.isKinematic) continue;
                s.mo.rb.linearVelocity = Vector3.zero;
                s.mo.rb.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            BlastSolver.ForgetColliders();

            var selfSupported = new List<int>();
            BuildGraph(selfSupported);
            // Walls, doors and roofs stand again: one structure fact for the whole house, so what
            // was built on the broken house (the grandmother's NavMesh) is rebuilt. Intact, by the
            // world: it is nobody's way in and nobody's fault.
            DestructionEvents.Structure(this, transform.position, DestructionState.Intact, Actors.World);
            Debug.Log("[HouseDestruction] reset: " + repaired + " pieces repaired, " + back + " destroyed movables back in place, graph " +
                      graph.NodeCount + " nodes.");
        }

        void RecordStartPoses()
        {
            startPoses.Clear();
            MovableObject[] all = FindObjectsByType<MovableObject>();
            for (int i = 0; i < all.Length; i++)
            {
                var mo = all[i];
                if (mo == null || !mo.TryGetComponent(out Breakable _)) continue;
                startPoses.Add(new StartPose
                {
                    mo = mo,
                    parent = mo.transform.parent,
                    position = mo.transform.localPosition,
                    rotation = mo.transform.localRotation,
                });
            }
        }

        // A floor, a staircase, a plinth: in the graph only to hold things up. Never released.
        sealed class FixedPart : IStructurePart
        {
            readonly string label;
            public FixedPart(string label) { this.label = label; }
            public float Release(int node, int part, in DamageEvent cause) => 0f;
            public bool SplitForSupport(int node) => false;
            public string Describe(int part) => label;
        }

        // ---- c) furniture ----

        int WireMovables()
        {
            int count = 0;
            MovableObject[] all = FindObjectsByType<MovableObject>();
            for (int i = 0; i < all.Length; i++)
            {
                MovableObject mo = all[i];
                if (mo == null) continue;
                AssignPropsLayer(mo.transform);
                if (mo.TryGetComponent(out Breakable _) || mo.TryGetComponent(out HeldUsable _)) continue;
                mo.gameObject.AddComponent<Breakable>().Configure(GuessMaterial(mo), 0f, false);
                count++;
            }
            return count;
        }

        // A movable and its parts go on the Props layer (only what sits on Default: a layer
        // somebody chose on purpose stays).
        static void AssignPropsLayer(Transform t)
        {
            int props = DestructionLayers.Props;
            if (props < 0) return;
            DestructionLayers.Assign(t.gameObject, props);
            for (int i = 0; i < t.childCount; i++) AssignPropsLayer(t.GetChild(i));
        }

        // A guess, not a truth: tune the Breakable in the inspector if a name misleads it.
        public static BreakMaterial GuessMaterial(MovableObject mo)
        {
            BreakMaterial m;
            if (!TryGuess(mo.displayName, out m) && !TryGuess(mo.name, out m)) m = BreakMaterial.Wood;
            // The scene author's fragile flag wins over a name that sounds sturdy: a "Flower
            // Pot" reads as metal, but it was marked fragile because it is terracotta.
            if (mo.fragile && m != BreakMaterial.Glass && m != BreakMaterial.Ceramic) m = BreakMaterial.Ceramic;
            return m;
        }

        static bool TryGuess(string text, out BreakMaterial material)
        {
            material = BreakMaterial.Wood;
            if (string.IsNullOrEmpty(text)) return false;
            List<string> words = Words(text);
            for (int i = words.Count - 1; i >= 0; i--)
            {
                string w = words[i];
                if (Matches(w, WoodWords)) { material = BreakMaterial.Wood; return true; }
                if (Matches(w, GlassWords)) { material = BreakMaterial.Glass; return true; }
                if (Matches(w, CeramicWords)) { material = BreakMaterial.Ceramic; return true; }
                if (Matches(w, MetalWords)) { material = BreakMaterial.Metal; return true; }
                if (Matches(w, FabricWords)) { material = BreakMaterial.Fabric; return true; }
            }
            return false;
        }

        // Whole words only, plural tolerated: "Cupboard" is not a cup, "Plates" are plates.
        static bool Matches(string word, string[] list)
        {
            for (int i = 0; i < list.Length; i++)
            {
                string k = list[i];
                if (word == k) return true;
                if (word.Length == k.Length + 1 && word[word.Length - 1] == 's' && word.StartsWith(k, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // "WineBottle_02 (1)" -> [wine, bottle, 02, 1]
        static List<string> Words(string text)
        {
            var words = new List<string>();
            var sb = new System.Text.StringBuilder();
            char prev = ' ';
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                bool boundary = !char.IsLetterOrDigit(ch) || (char.IsUpper(ch) && char.IsLower(prev));
                if (boundary && sb.Length > 0) { words.Add(sb.ToString()); sb.Length = 0; }
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                prev = ch;
            }
            if (sb.Length > 0) words.Add(sb.ToString());
            return words;
        }
    }
}
