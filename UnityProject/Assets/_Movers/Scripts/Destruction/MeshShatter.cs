using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Movers
{
    // Runtime fracture: turns a set of renderers into a handful of physical chunks.
    //
    // No precomputed fracture and no solid Voronoi cells. Every triangle of the source goes
    // to the nearest of K seed points (a spread-out random start, then one k-means pass), and
    // each cluster of triangles becomes one piece. The pieces are shells, not solids, which is
    // why each one is double-sided: a chunk of wall seen from its broken side still reads as
    // a chunk instead of a hole. Large flat triangles are split first, otherwise a plastered
    // face made of two big triangles could only ever break into two pieces.
    //
    // Everything is generated in code (CLAUDE.md 13). A mesh that is not readable (an FBX
    // imported without Read/Write) cannot be taken apart, so it breaks into plain boxes that
    // tile its bounds, with a single warning saying how to fix it. A mesh too dense to cut
    // up in one frame (see MaxSourceTriangles) takes the same way out.
    //
    // Each piece is a DebrisPool shell (rented, its mesh refilled, given back when it leaves),
    // so a burst of breaks allocates almost nothing. Three kinds of break (DEV 2):
    // - Shatter: a prop, a pane, a fixture bursting apart around its centre;
    // - ShatterChunk: a wall chunk torn out by a big hit, cut into rubble that keeps the
    //   chunk's launch (section 3.7);
    // - Chips: a few flakes knocked off a surface by a hit that removes nothing (section 3.9).
    public static class MeshShatter
    {
        const float MinBoxSize = 0.04f;           // collider floor on each axis, so a flat shard still has a body
        const float ContinuousBelow = 0.15f;      // smaller pieces get continuous collision detection
        const float MinPieceMass = 0.05f;
        const float MaxKickSpeed = 30f;           // cap on impulse / mass, whatever an explosion passes in
        const float DepenetrationSpeed = 3f;      // pieces spawn touching the floor and each other: separate them gently
        const int MaxWorkingTriangles = 8000;     // subdivision never grows the soup past this
        const int ExtraTrianglesPerPiece = 48;    // subdivision budget per requested piece
        // What one shatter may read from its sources. Clustering costs triangles x pieces and
        // every triangle is written twice (double-sided), so a dense 50k-triangle model would
        // cost a visible hitch and a few hundred thousand vertices. A source that would push
        // the total past this breaks into boxes instead, like an unreadable one.
        const int MaxSourceTriangles = MaxWorkingTriangles * 2;

        // The triangle soup, world space, one entry per triangle. Kept between calls so a
        // burst of shatters (one grenade, six walls) does not feed the garbage collector.
        static readonly List<Vector3> pa = new List<Vector3>(1024);
        static readonly List<Vector3> pb = new List<Vector3>(1024);
        static readonly List<Vector3> pc = new List<Vector3>(1024);
        static readonly List<Vector2> ta = new List<Vector2>(1024);
        static readonly List<Vector2> tb = new List<Vector2>(1024);
        static readonly List<Vector2> tc = new List<Vector2>(1024);
        static readonly List<int> triMaterial = new List<int>(1024);
        static readonly List<Material> materials = new List<Material>(8);

        static float[] triArea = new float[1024];
        static float[] cumArea = new float[1024];
        static Vector3[] centroid = new Vector3[1024];
        static int[] clusterOf = new int[1024];
        static int[] order = new int[1024];
        static int[] bucket = new int[256];
        static int[] cursor = new int[256];

        static readonly List<Vector3> seeds = new List<Vector3>(32);
        static Vector3[] seedSum = new Vector3[32];
        static float[] seedWeight = new float[32];

        static readonly List<Vector3> readVerts = new List<Vector3>(1024);
        static readonly List<Vector2> readUVs = new List<Vector2>(1024);
        static readonly List<int> readIndices = new List<int>(3072);

        static readonly List<Vector3> outVerts = new List<Vector3>(4096);
        static readonly List<Vector2> outUVs = new List<Vector2>(4096);
        static readonly List<int> outIndices = new List<int>(4096);
        static readonly List<int> subStart = new List<int>(8);
        static readonly List<int> subLength = new List<int>(8);
        static readonly List<Material> subMaterial = new List<Material>(8);

        static readonly List<Renderer> boxSources = new List<Renderer>(4);
        static readonly List<Renderer> single = new List<Renderer>(1);
        static readonly List<Material> readMaterials = new List<Material>(8);
        // What Shatter returns: reused by every call (no caller keeps it; copy it to keep it).
        static readonly List<GameObject> spawnedScratch = new List<GameObject>(32);
        static MaterialPropertyBlock propertyBlock;

        // One generator for every shatter, so two identical vases never break the same way and
        // no call allocates its own. Local cosmetics only: it never touches the gameplay's Random.
        static System.Random rng = new System.Random(System.Environment.TickCount);
        static bool warnedUnreadable;
        static bool notedTooDense;

        // How the pieces of one kind of break leave their source.
        struct LaunchStyle
        {
            public float outwardMin, outwardMax;   // m/s away from the source's centre
            public float kickMin, kickMax;         // share of the impulse kick
            public float spinMin, spinMax;         // rad/s
            public float maxMass;                  // kg of Rigidbody mass per piece at most
        }

        static readonly LaunchStyle Burst = new LaunchStyle
        {
            outwardMin = 0.5f, outwardMax = 2.5f, kickMin = 0.6f, kickMax = 1.4f,
            spinMin = 1f, spinMax = 8f, maxMass = float.PositiveInfinity,
        };

        // The playtest setup can keep statics alive between play sessions (domain reload off),
        // and the one-time messages should come back once per session, not once per editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            warnedUnreadable = false;
            notedTooDense = false;
            rng = new System.Random(System.Environment.TickCount);
        }

        // Breaks the given renderers into about 'pieces' rigidbodies (fewer when the frame's
        // debris budget is spent) and returns them.
        // The sources are left untouched: hiding or deactivating them is the caller's job.
        // totalMass is shared between the pieces by surface area. Every piece gets
        //   inheritVelocity + outward (from the sources' centre) * 0.5..2.5 m/s
        //   + impulse / max(totalMass, 1) * 0.6..1.4
        // and a random spin, and lives 'lifetime' seconds under DebrisManager. 'point' is where
        // the hit landed; the velocity model does not need it today, it is in the signature so
        // callers do not have to change when it does. 'instigator' is who broke it (see Actors):
        // a heavy piece that lands on something crushes it in that player's name.
        // The list returned is reused by the next call: read it now, copy it to keep it.
        public static List<GameObject> Shatter(IList<Renderer> sources, int pieces, float totalMass,
                                               Vector3 inheritVelocity, Vector3 point, Vector3 impulse,
                                               float lifetime, int instigator = Actors.World)
        {
            spawnedScratch.Clear();
            if (sources == null || sources.Count == 0) return spawnedScratch;
            DebrisManager manager = DebrisManager.Instance;
            if (manager == null) return spawnedScratch;   // the application is closing

            // The frame's piece budget decides, not the caller: past it this breaks into fewer
            // pieces, or into nothing but its sound and dust (DebrisManager.TryReserve).
            if (!manager.TryReserve(Mathf.Max(1, pieces), out int granted)) return spawnedScratch;

            int built = Build(sources, granted, totalMass, inheritVelocity, impulse, lifetime, instigator,
                              Burst, manager, spawnedScratch, Vector3.zero, 0f);
            if (built < granted) manager.Refund(granted - built, false);
            return spawnedScratch;
        }

        // One wall chunk torn out by a big hit, cut into 'pieces' pieces of rubble (DEV 2 section
        // 3.7). Every piece keeps the chunk's launch: velocity, plus a little burst apart and the
        // table's chunkSpin, its Rigidbody mass capped at debrisPhysicsMassCap. The pieces are
        // marked like the chunk they came from (structureChunk, structureSource = its
        // DestructibleModule, launchedAt = now, its material), so the blast that launched them
        // does not push them again and they never hurt their own wall.
        // Asks the structure budget (TryReserveStructure). Returns an empty list when fewer than
        // two pieces fit, or when the chunk cannot be read: the caller then detaches the whole
        // slab, never nothing. The chunk must still be active and shown, and is left for the
        // caller to remove. The list returned is the caller's.
        public static List<GameObject> ShatterChunk(Renderer chunk, int pieces, float totalMass, Vector3 velocity,
                                                    Vector3 point, float lifetime, int instigator)
        {
            var result = new List<GameObject>(Mathf.Max(2, pieces));
            if (chunk == null || pieces < 2 || !chunk.enabled || !chunk.gameObject.activeInHierarchy) return result;
            DebrisManager manager = DebrisManager.Instance;
            if (manager == null) return result;
            if (!manager.TryReserveStructure(pieces, out int granted)) return result;
            if (granted < 2)
            {
                manager.Refund(granted, true);
                return result;
            }

            var t = DestructionMaterialTable.Current;
            var style = new LaunchStyle
            {
                outwardMin = 0.3f, outwardMax = 1.2f, kickMin = 0f, kickMax = 0f,
                spinMin = Mathf.Max(0f, t.chunkSpin.x), spinMax = Mathf.Max(t.chunkSpin.x, t.chunkSpin.y),
                maxMass = t.debrisPhysicsMassCap > 0f ? t.debrisPhysicsMassCap : float.PositiveInfinity,
            };
            single.Clear();
            single.Add(chunk);
            // Rubble only from a real mesh: boxes tiling a wall slab would read worse than the slab.
            int built = Build(single, granted, totalMass, velocity, Vector3.zero, lifetime, instigator, style, manager,
                              result, Vector3.zero, 0f, true);
            single.Clear();
            if (built < granted) manager.Refund(granted - built, true);
            if (built == 0) return result;

            var module = chunk.GetComponentInParent<DestructibleModule>(true);
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            chunk.GetPropertyBlock(propertyBlock);
            bool tinted = !propertyBlock.isEmpty;
            float now = Time.time;
            for (int i = 0; i < result.Count; i++)
            {
                if (!result[i].TryGetComponent(out DebrisPiece piece)) continue;
                piece.structureChunk = true;
                piece.structureSource = module;
                piece.launchedAt = now;
                if (module != null) piece.material = module.Material;
                // The damage tint of the chunk stays on its rubble.
                if (tinted && piece.rend != null) piece.rend.SetPropertyBlock(propertyBlock);
            }
            return result;
        }

        // A few flakes knocked off 'source' around 'point' by a hit that removes nothing (DEV 2
        // section 3.9): only its triangles within 'radius' of the point are cut, so a chip is a
        // small patch of the surface, not a copy of the whole chunk. Each flies at velocity plus
        // a little spray and lives 'lifetime' seconds. Asks the ordinary budget (TryReserve),
        // never the wall-chunk reserve: chips are the first thing to go in a busy frame. Nothing
        // comes of an unreadable mesh (the dust says it). The list returned is reused by the
        // next call, like Shatter's.
        public static List<GameObject> Chips(Renderer source, Vector3 point, float radius, int count, float totalMass,
                                             Vector3 velocity, float lifetime, int instigator)
        {
            spawnedScratch.Clear();
            if (source == null || count <= 0 || radius <= 0f) return spawnedScratch;
            DebrisManager manager = DebrisManager.Instance;
            if (manager == null) return spawnedScratch;
            if (!manager.TryReserve(count, out int granted)) return spawnedScratch;

            var style = new LaunchStyle
            {
                outwardMin = 0.5f, outwardMax = 2f, kickMin = 0f, kickMax = 0f,
                spinMin = 4f, spinMax = 12f, maxMass = float.PositiveInfinity,
            };
            single.Clear();
            single.Add(source);
            int built = Build(single, granted, totalMass, velocity, Vector3.zero, lifetime, instigator, style, manager,
                              spawnedScratch, point, radius);
            single.Clear();
            if (built < granted) manager.Refund(granted - built, false);
            return spawnedScratch;
        }

        // The shared body of the three: reads the sources, cuts them, launches the pieces into
        // 'into' and returns how many it built. With nearRadius > 0 only the triangles around
        // 'near' are kept (chips). With meshOnly (or for chips) unreadable sources give nothing
        // instead of boxes.
        static int Build(IList<Renderer> sources, int pieces, float totalMass, Vector3 inheritVelocity, Vector3 impulse,
                         float lifetime, int instigator, in LaunchStyle style, DebrisManager manager,
                         List<GameObject> into, Vector3 near, float nearRadius, bool meshOnly = false)
        {
            int before = into.Count;
            totalMass = Mathf.Max(MinPieceMass, totalMass);
            bool chips = nearRadius > 0f;
            meshOnly |= chips;

            ClearSoup();
            boxSources.Clear();

            bool any = false;
            Bounds whole = default;
            Quaternion frame = Quaternion.identity;
            int layer = 0;
            float meshVolume = 0f, boxVolume = 0f;

            for (int i = 0; i < sources.Count; i++)
            {
                Renderer r = sources[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                if (!any)
                {
                    whole = b;
                    // Pieces are built in the first source's rotation, so their box colliders
                    // hug a tilted chair or a rotated wall instead of its world-aligned bounds.
                    frame = r.transform.rotation;
                    layer = r.gameObject.layer;
                    any = true;
                }
                else whole.Encapsulate(b);

                float volume = Mathf.Max(1e-4f, b.size.x * b.size.y * b.size.z);
                if (AddTriangles(r)) meshVolume += volume;
                else if (!meshOnly) { boxSources.Add(r); boxVolume += volume; }
            }
            if (!any) return 0;
            if (chips && !KeepNear(near, nearRadius))
            {
                ClearSoup();
                return 0;
            }

            Vector3 centre = chips ? near : whole.center;
            Vector3 kick = Vector3.ClampMagnitude(impulse / Mathf.Max(totalMass, 1f), MaxKickSpeed);
            bool hasTriangles = pa.Count > 0;
            float meshShare = boxSources.Count == 0 ? 1f : (hasTriangles ? meshVolume / (meshVolume + boxVolume) : 0f);
            int meshPieces = boxSources.Count == 0 ? pieces : Mathf.Max(1, Mathf.RoundToInt(pieces * meshShare));

            if (hasTriangles)
                BuildMeshPieces(meshPieces, totalMass * meshShare, frame, layer, centre,
                                inheritVelocity, kick, lifetime, instigator, style, manager, into);

            if (boxSources.Count > 0)
            {
                int boxPieces = hasTriangles ? Mathf.Max(1, pieces - meshPieces) : pieces;
                BuildBoxChunks(boxPieces, totalMass * (1f - meshShare), layer, centre,
                               inheritVelocity, kick, lifetime, instigator, style, manager, into);
            }

            ClearSoup();
            boxSources.Clear();
            return into.Count - before;
        }

        // ---- reading the sources ----

        static void ClearSoup()
        {
            pa.Clear(); pb.Clear(); pc.Clear();
            ta.Clear(); tb.Clear(); tc.Clear();
            triMaterial.Clear();
            materials.Clear();
        }

        // Appends every triangle of the renderer, world space, with the material of its
        // submesh. False means "cannot read it, break it into boxes instead".
        static bool AddTriangles(Renderer r)
        {
            if (!(r is MeshRenderer)) return false;
            // A statically batched renderer points at the combined batch, not its own mesh.
            if (r.isPartOfStaticBatch) return false;
            if (!r.TryGetComponent(out MeshFilter filter)) return false;
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) return false;
            if (!mesh.isReadable)
            {
                WarnUnreadable(mesh);
                return false;
            }

            r.GetSharedMaterials(readMaterials);
            // Submeshes past the material array are not drawn, so they are not debris either.
            int subCount = Mathf.Min(mesh.subMeshCount, readMaterials.Count);
            int triangles = TriangleCount(mesh, subCount);
            if (pa.Count + triangles > MaxSourceTriangles)
            {
                NoteTooDense(mesh, triangles);
                return false;
            }

            mesh.GetVertices(readVerts);
            mesh.GetUVs(0, readUVs);
            bool hasUV = readUVs.Count == readVerts.Count;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            // A mirrored transform flips the winding; swap two corners to keep faces outward.
            bool mirrored = m.determinant < 0f;
            int before = pa.Count;

            for (int s = 0; s < subCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                int slot = MaterialSlot(readMaterials[s]);
                mesh.GetTriangles(readIndices, s);
                for (int i = 0; i + 2 < readIndices.Count; i += 3)
                {
                    int i0 = readIndices[i], i1 = readIndices[i + 1], i2 = readIndices[i + 2];
                    if (mirrored) { int t = i1; i1 = i2; i2 = t; }
                    pa.Add(m.MultiplyPoint3x4(readVerts[i0]));
                    pb.Add(m.MultiplyPoint3x4(readVerts[i1]));
                    pc.Add(m.MultiplyPoint3x4(readVerts[i2]));
                    ta.Add(hasUV ? readUVs[i0] : Vector2.zero);
                    tb.Add(hasUV ? readUVs[i1] : Vector2.zero);
                    tc.Add(hasUV ? readUVs[i2] : Vector2.zero);
                    triMaterial.Add(slot);
                }
            }

            readVerts.Clear();
            readUVs.Clear();
            readIndices.Clear();
            readMaterials.Clear();
            return pa.Count > before;
        }

        static int MaterialSlot(Material mat)
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] == mat) return i;
            materials.Add(mat);
            return materials.Count - 1;
        }

        // Read from the index counts, which does not need the mesh data itself.
        static int TriangleCount(Mesh mesh, int subCount)
        {
            long n = 0;
            for (int s = 0; s < subCount; s++)
                if (mesh.GetTopology(s) == MeshTopology.Triangles) n += (long)mesh.GetIndexCount(s) / 3;
            return (int)System.Math.Min(n, int.MaxValue);
        }

        static void NoteTooDense(Mesh mesh, int triangles)
        {
            if (notedTooDense) return;
            notedTooDense = true;
            Debug.Log("[MeshShatter] mesh '" + mesh.name + "' (" + triangles + " triangles) is over the fracture " +
                      "budget of " + MaxSourceTriangles + " triangles per shatter, so it breaks into plain boxes. " +
                      "(Shown once: other meshes may be in the same state.)");
        }

        static void WarnUnreadable(Mesh mesh)
        {
            if (warnedUnreadable) return;
            warnedUnreadable = true;
            Debug.LogWarning("[MeshShatter] mesh '" + mesh.name + "' is not readable, so it breaks into plain boxes. " +
                             "Tick Read/Write on its model import to get real fragments. " +
                             "(Shown once: other meshes may be in the same state.)");
        }

        // ---- clustering ----

        static void BuildMeshPieces(int k, float mass, Quaternion frame, int layer, Vector3 sourceCentre,
                                    Vector3 inherit, Vector3 kick, float lifetime, int instigator, in LaunchStyle style,
                                    DebrisManager manager, List<GameObject> spawned)
        {
            float totalArea = Measure();
            Subdivide(k, totalArea);
            totalArea = Measure();

            int n = pa.Count;
            if (n == 0) return;
            k = Mathf.Clamp(k, 1, n);

            PickSeeds(k, n, rng);
            Assign(n);
            Refine(k, n);
            Assign(n);
            SortByClusterAndMaterial(n, k);

            Quaternion inverse = Quaternion.Inverse(frame);
            int mCount = Mathf.Max(1, materials.Count);

            for (int c = 0; c < k; c++)
            {
                int from = bucket[c * mCount];
                int to = bucket[c * mCount + mCount];
                if (to <= from) continue;   // a seed no triangle chose

                // Bounds in the piece frame, and the share of the surface this piece carries.
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                float area = 0f;
                for (int j = from; j < to; j++)
                {
                    int t = order[j];
                    area += triArea[t];
                    Grow(ref min, ref max, inverse * pa[t]);
                    Grow(ref min, ref max, inverse * pb[t]);
                    Grow(ref min, ref max, inverse * pc[t]);
                }
                Vector3 localCentre = (min + max) * 0.5f;

                outVerts.Clear(); outUVs.Clear(); outIndices.Clear();
                subStart.Clear(); subLength.Clear(); subMaterial.Clear();
                for (int mi = 0; mi < mCount; mi++)
                {
                    int s0 = bucket[c * mCount + mi];
                    int s1 = bucket[c * mCount + mi + 1];
                    if (s1 <= s0) continue;
                    int start = outIndices.Count;
                    for (int j = s0; j < s1; j++)
                    {
                        int t = order[j];
                        Vector3 a = inverse * pa[t] - localCentre;
                        Vector3 b = inverse * pb[t] - localCentre;
                        Vector3 d = inverse * pc[t] - localCentre;
                        AddTriangle(a, b, d, ta[t], tb[t], tc[t]);
                        // The back face gets its own corners so its normal can point the other way.
                        AddTriangle(a, d, b, ta[t], tc[t], tb[t]);
                    }
                    subStart.Add(start);
                    subLength.Add(outIndices.Count - start);
                    subMaterial.Add(mi < materials.Count ? materials[mi] : null);
                }
                if (subStart.Count == 0) continue;

                Vector3 worldCentre = frame * localCentre;
                DebrisPool.Shell shell = manager.RentShell();
                Mesh mesh = DebrisPool.EnsureMesh(shell);
                FillMesh(mesh);

                shell.go.layer = layer;
                shell.transform.SetPositionAndRotation(worldCentre, frame);
                shell.transform.localScale = Vector3.one;
                shell.renderer.SetSharedMaterials(subMaterial);
                BoxCollider box = shell.box;
                Bounds mb = mesh.bounds;
                box.center = mb.center;
                box.size = Vector3.Max(mb.size, Vector3.one * MinBoxSize);

                float share = totalArea > 0f ? area / totalArea : 1f / k;
                Launch(shell, box.size, mass * share, worldCentre, sourceCentre, inherit, kick,
                       lifetime, instigator, style, manager);
                spawned.Add(shell.go);
            }
        }

        static void Grow(ref Vector3 min, ref Vector3 max, Vector3 p)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        // Area and centroid of every triangle; returns the total area.
        static float Measure()
        {
            int n = pa.Count;
            Ensure(ref triArea, n);
            Ensure(ref cumArea, n);
            Ensure(ref centroid, n);
            float total = 0f;
            for (int t = 0; t < n; t++)
            {
                Vector3 a = pa[t], b = pb[t], c = pc[t];
                float area = 0.5f * Vector3.Cross(b - a, c - a).magnitude;
                triArea[t] = area;
                centroid[t] = (a + b + c) / 3f;
                total += area;
            }
            return total;
        }

        // Splits triangles along their longest edge until no edge is much longer than a piece.
        // Done in passes so the budget is spread over the whole surface rather than spent on
        // the first big face in the list.
        static void Subdivide(int k, float totalArea)
        {
            if (totalArea <= 0f) return;
            float maxEdge = Mathf.Max(0.06f, Mathf.Sqrt(totalArea / Mathf.Max(1, k)) * 0.6f);
            float maxSq = maxEdge * maxEdge;
            int budget = Mathf.Min(MaxWorkingTriangles, pa.Count + k * ExtraTrianglesPerPiece);

            for (int pass = 0; pass < 12 && pa.Count < budget; pass++)
            {
                bool split = false;
                int n = pa.Count;
                for (int i = 0; i < n && pa.Count < budget; i++)
                    if (SplitLongestEdge(i, maxSq)) split = true;
                if (!split) break;
            }
        }

        static bool SplitLongestEdge(int i, float maxSq)
        {
            Vector3 a = pa[i], b = pb[i], c = pc[i];
            Vector2 ua = ta[i], ub = tb[i], uc = tc[i];
            float ab = (b - a).sqrMagnitude, bc = (c - b).sqrMagnitude, ca = (a - c).sqrMagnitude;

            // Rotate the corners (winding kept) so the longest edge is always a-b.
            if (bc >= ab && bc >= ca)
            {
                Vector3 p = a; a = b; b = c; c = p;
                Vector2 q = ua; ua = ub; ub = uc; uc = q;
                ab = bc;
            }
            else if (ca >= ab && ca >= bc)
            {
                Vector3 p = a; a = c; c = b; b = p;
                Vector2 q = ua; ua = uc; uc = ub; ub = q;
                ab = ca;
            }
            if (ab <= maxSq) return false;

            Vector3 mid = (a + b) * 0.5f;
            Vector2 umid = (ua + ub) * 0.5f;
            pa[i] = a; pb[i] = mid; pc[i] = c;
            ta[i] = ua; tb[i] = umid; tc[i] = uc;
            pa.Add(mid); pb.Add(b); pc.Add(c);
            ta.Add(umid); tb.Add(ub); tc.Add(uc);
            triMaterial.Add(triMaterial[i]);
            return true;
        }

        // Chips: keeps only the surface within 'radius' of 'point'. The big faces there are split
        // first (a plastered face is two triangles, whose centroids may both be far from the
        // hit), then every triangle whose centroid is out of reach is dropped. False when
        // nothing is left.
        static bool KeepNear(Vector3 point, float radius)
        {
            float maxEdge = Mathf.Max(0.03f, radius * 0.5f);
            float maxSq = maxEdge * maxEdge;
            int budget = Mathf.Min(MaxWorkingTriangles, pa.Count + 400);
            for (int pass = 0; pass < 12 && pa.Count < budget; pass++)
            {
                bool split = false;
                int n = pa.Count;
                for (int i = 0; i < n && pa.Count < budget; i++)
                {
                    Vector3 a = pa[i], b = pb[i], c = pc[i];
                    float reach = radius + Mathf.Sqrt(Mathf.Max((b - a).sqrMagnitude,
                                                     Mathf.Max((c - b).sqrMagnitude, (a - c).sqrMagnitude)));
                    if (((a + b + c) / 3f - point).sqrMagnitude > reach * reach) continue;
                    if (SplitLongestEdge(i, maxSq)) split = true;
                }
                if (!split) break;
            }

            float r2 = radius * radius;
            int write = 0;
            for (int t = 0; t < pa.Count; t++)
            {
                if (((pa[t] + pb[t] + pc[t]) / 3f - point).sqrMagnitude > r2) continue;
                pa[write] = pa[t]; pb[write] = pb[t]; pc[write] = pc[t];
                ta[write] = ta[t]; tb[write] = tb[t]; tc[write] = tc[t];
                triMaterial[write] = triMaterial[t];
                write++;
            }
            int drop = pa.Count - write;
            if (drop > 0)
            {
                pa.RemoveRange(write, drop); pb.RemoveRange(write, drop); pc.RemoveRange(write, drop);
                ta.RemoveRange(write, drop); tb.RemoveRange(write, drop); tc.RemoveRange(write, drop);
                triMaterial.RemoveRange(write, drop);
            }
            return write > 0;
        }

        // Area-weighted random centroids, each the best of a few candidates (the one farthest
        // from the seeds already placed), so the pieces come out roughly even in size.
        static void PickSeeds(int k, int n, System.Random rng)
        {
            seeds.Clear();
            float run = 0f;
            for (int t = 0; t < n; t++) { run += triArea[t]; cumArea[t] = run; }

            for (int s = 0; s < k; s++)
            {
                Vector3 best = Vector3.zero;
                float bestDistance = -1f;
                int tries = seeds.Count == 0 ? 1 : 4;
                for (int attempt = 0; attempt < tries; attempt++)
                {
                    int t = run > 0f ? SampleByArea(n, run, rng) : rng.Next(n);
                    Vector3 candidate = centroid[t];
                    float d = NearestSeedDistance(candidate);
                    if (d > bestDistance) { bestDistance = d; best = candidate; }
                }
                seeds.Add(best);
            }
        }

        static int SampleByArea(int n, float total, System.Random rng)
        {
            float u = (float)rng.NextDouble() * total;
            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (cumArea[mid] < u) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        static float NearestSeedDistance(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < seeds.Count; i++)
            {
                float d = (seeds[i] - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return best;
        }

        static void Assign(int n)
        {
            Ensure(ref clusterOf, n);
            int k = seeds.Count;
            for (int t = 0; t < n; t++)
            {
                Vector3 p = centroid[t];
                int best = 0;
                float bestDistance = float.MaxValue;
                for (int s = 0; s < k; s++)
                {
                    float d = (seeds[s] - p).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = s; }
                }
                clusterOf[t] = best;
            }
        }

        // One k-means step: every seed moves to the area-weighted middle of its triangles.
        static void Refine(int k, int n)
        {
            Ensure(ref seedSum, k);
            Ensure(ref seedWeight, k);
            for (int s = 0; s < k; s++) { seedSum[s] = Vector3.zero; seedWeight[s] = 0f; }
            for (int t = 0; t < n; t++)
            {
                int c = clusterOf[t];
                float w = triArea[t] + 1e-6f;
                seedSum[c] += centroid[t] * w;
                seedWeight[c] += w;
            }
            for (int s = 0; s < k; s++)
                if (seedWeight[s] > 0f) seeds[s] = seedSum[s] / seedWeight[s];
        }

        // Counting sort of the triangles by (cluster, material): each piece then reads its
        // triangles as contiguous runs, one run per submesh, in a single pass.
        static void SortByClusterAndMaterial(int n, int k)
        {
            int mCount = Mathf.Max(1, materials.Count);
            int keys = k * mCount;
            Ensure(ref bucket, keys + 1);
            Ensure(ref cursor, keys);
            Ensure(ref order, n);
            for (int i = 0; i <= keys; i++) bucket[i] = 0;
            for (int t = 0; t < n; t++) bucket[clusterOf[t] * mCount + triMaterial[t] + 1]++;
            for (int i = 1; i <= keys; i++) bucket[i] += bucket[i - 1];
            for (int i = 0; i < keys; i++) cursor[i] = bucket[i];
            for (int t = 0; t < n; t++) order[cursor[clusterOf[t] * mCount + triMaterial[t]]++] = t;
        }

        // ---- building the pieces ----

        static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int v = outVerts.Count;
            outVerts.Add(a); outVerts.Add(b); outVerts.Add(c);
            outUVs.Add(ua); outUVs.Add(ub); outUVs.Add(uc);
            outIndices.Add(v); outIndices.Add(v + 1); outIndices.Add(v + 2);
        }

        // Writes the piece being built into a shell's mesh (emptied first: it may hold the last
        // piece that shell was).
        static void FillMesh(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = outVerts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(outVerts);
            mesh.SetUVs(0, outUVs);
            mesh.subMeshCount = subStart.Count;
            for (int s = 0; s < subStart.Count; s++)
                mesh.SetTriangles(outIndices, subStart[s], subLength[s], s, false);
            // Unshared corners give flat normals, which is the faceted look debris should have.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // Fallback for meshes we cannot read: a grid of boxes over the renderer's own bounds,
        // in its own orientation, wearing its first material.
        static void BuildBoxChunks(int k, float mass, int layer, Vector3 sourceCentre, Vector3 inherit,
                                   Vector3 kick, float lifetime, int instigator, in LaunchStyle style,
                                   DebrisManager manager, List<GameObject> spawned)
        {
            float totalVolume = 0f;
            for (int i = 0; i < boxSources.Count; i++) totalVolume += BoundsVolume(boxSources[i]);
            if (totalVolume <= 0f) totalVolume = 1f;

            for (int i = 0; i < boxSources.Count; i++)
            {
                Renderer r = boxSources[i];
                float volumeShare = BoundsVolume(r) / totalVolume;
                int kr = Mathf.Max(1, Mathf.RoundToInt(k * volumeShare));

                Transform t = r.transform;
                MeshFilter f = null;
                bool oriented = !r.isPartOfStaticBatch && r.TryGetComponent(out f) && f.sharedMesh != null;
                Bounds local = oriented ? f.sharedMesh.bounds : r.bounds;
                Vector3 scale = oriented ? Abs(t.lossyScale) : Vector3.one;
                Quaternion rotation = oriented ? t.rotation : Quaternion.identity;

                Grid(Vector3.Scale(local.size, scale), kr, out int nx, out int ny, out int nz);
                Vector3 cell = new Vector3(local.size.x / nx, local.size.y / ny, local.size.z / nz);
                Vector3 cellWorld = Vector3.Max(Vector3.Scale(cell, scale) * 0.95f, Vector3.one * MinBoxSize);
                Material mat = FirstMaterial(r);
                if (mat == null) mat = DebrisPool.DefaultMaterial;
                float cellMass = mass * volumeShare / (nx * ny * nz);

                for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 lc = local.min + new Vector3((ix + 0.5f) * cell.x, (iy + 0.5f) * cell.y, (iz + 0.5f) * cell.z);
                    Vector3 wc = oriented ? t.TransformPoint(lc) : lc;

                    // Unity's cube, scaled to the cell: the shell's own mesh stays unused.
                    DebrisPool.Shell shell = manager.RentShell();
                    shell.filter.sharedMesh = DebrisPool.CubeMesh;
                    shell.renderer.sharedMaterial = mat;
                    shell.box.center = Vector3.zero;
                    shell.box.size = Vector3.one;
                    shell.go.layer = layer;
                    shell.transform.SetPositionAndRotation(wc, rotation);
                    shell.transform.localScale = cellWorld;

                    Launch(shell, cellWorld, cellMass, wc, sourceCentre, inherit, kick, lifetime, instigator, style, manager);
                    spawned.Add(shell.go);
                }
            }
        }

        static float BoundsVolume(Renderer r)
        {
            Vector3 s = r.bounds.size;
            return Mathf.Max(1e-4f, s.x * s.y * s.z);
        }

        static Material FirstMaterial(Renderer r)
        {
            r.GetSharedMaterials(readMaterials);
            Material first = null;
            for (int i = 0; i < readMaterials.Count && first == null; i++) first = readMaterials[i];
            readMaterials.Clear();
            return first;
        }

        // Roughly cubic cells, about k of them, never more than 8 along an axis.
        static void Grid(Vector3 size, int k, out int nx, out int ny, out int nz)
        {
            size = Vector3.Max(size, Vector3.one * 0.01f);
            k = Mathf.Max(1, k);
            float cell = Mathf.Pow(size.x * size.y * size.z / k, 1f / 3f);
            nx = ny = nz = 1;
            for (int i = 0; i < 10; i++)
            {
                nx = Mathf.Clamp(Mathf.RoundToInt(size.x / cell), 1, 8);
                ny = Mathf.Clamp(Mathf.RoundToInt(size.y / cell), 1, 8);
                nz = Mathf.Clamp(Mathf.RoundToInt(size.z / cell), 1, 8);
                int total = nx * ny * nz;
                if (total < k * 0.7f) cell *= 0.85f;
                else if (total > k * 1.5f) cell *= 1.15f;
                else break;
            }
        }

        // Wakes a filled shell up as a piece: active, dynamic, moving, registered. Rigidbody
        // settings a previous piece may have left are set again.
        static void Launch(DebrisPool.Shell shell, Vector3 size, float mass, Vector3 worldCentre, Vector3 sourceCentre,
                           Vector3 inherit, Vector3 kick, float lifetime, int instigator, in LaunchStyle style,
                           DebrisManager manager)
        {
            shell.go.SetActive(true);
            Rigidbody rb = shell.rb;
            rb.isKinematic = false;   // before anything continuous: kinematic bodies do not support it
            rb.mass = Mathf.Clamp(mass, MinPieceMass, Mathf.Max(MinPieceMass, style.maxMass));
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.constraints = RigidbodyConstraints.None;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxDepenetrationVelocity = DepenetrationSpeed;
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            rb.collisionDetectionMode = largest < ContinuousBelow
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.Discrete;

            Vector3 outward = worldCentre - sourceCentre;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : RandomUnit();
            rb.linearVelocity = inherit + outward * Range(style.outwardMin, style.outwardMax)
                                + kick * Range(style.kickMin, style.kickMax);
            rb.angularVelocity = RandomUnit() * Range(style.spinMin, style.spinMax);

            DebrisPiece piece = shell.piece;
            piece.ResetForRent();
            // A pooled shell's mesh belongs to the pool; a loose one's dies with its piece.
            piece.Init(rb, shell.owned ? null : shell.mesh);
            piece.shell = shell.owned ? shell : null;
            piece.instigator = instigator;
            manager.Register(piece, lifetime);
        }

        // ---- small helpers ----

        static float Range(float a, float b)
        {
            return a + (float)rng.NextDouble() * (b - a);
        }

        static Vector3 RandomUnit()
        {
            for (int i = 0; i < 16; i++)
            {
                var v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                float sq = v.sqrMagnitude;
                if (sq > 1e-4f && sq <= 1f) return v / Mathf.Sqrt(sq);
            }
            return Vector3.up;
        }

        static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        static void Ensure(ref float[] a, int n) { if (a.Length < n) a = new float[Mathf.NextPowerOfTwo(n)]; }
        static void Ensure(ref int[] a, int n) { if (a.Length < n) a = new int[Mathf.NextPowerOfTwo(n)]; }
        static void Ensure(ref Vector3[] a, int n) { if (a.Length < n) a = new Vector3[Mathf.NextPowerOfTwo(n)]; }
    }
}
