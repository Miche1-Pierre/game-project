using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

namespace Movers
{
    // Builds the grandmother's NavMesh at Play from the physics colliders, and rebuilds it when
    // the house changes (A8_navigation.md, measured recipe). Unity's built-in module, no package.
    //
    // - Physics colliders, not render meshes: the stairs only exist as invisible WalkRamp boxes.
    // - Left out: triggers, character capsules (hers and the crew's), and every collider with a
    //   rigidbody (A8 3.2's filter rule): the door leaves hang on kinematic hinges (she opens
    //   doors, they are not walls), and loose things move.
    // - A MeshCollider whose mesh is not Read/Write enabled goes in as a box of its mesh bounds
    //   (the builder cannot read the mesh: an error per source per build in the editor).
    // - furnitureAsObstacles (off by default, an open design question) keeps furniture instead:
    //   a loose body of at least obstacleMinKg, at rest and not carried, goes in as Not
    //   Walkable where it stands, so she walks round the sofa instead of into it. Furniture
    //   moves, so the baker remembers what it baked and where (FurnitureChanged).
    // - Every update passes the SAME full bounds as the first build. The bounds are the extent
    //   of the NavMesh, not a dirty region: a smaller box deletes everything outside it (A8
    //   3.1, measured). The builder already skips tiles whose inputs did not change.
    // - The first build runs at once, on the main thread, while the scene is loading anyway
    //   (A8: 81 to 234 ms): she has a NavMesh on her first frame, and the time is the real work,
    //   not work plus frames. Every later update is asynchronous, so a falling wall is no hitch.
    public sealed class NavMeshBaker
    {
        public Bounds bounds;
        public NavMeshBuildSettings settings;
        public bool furnitureAsObstacles = false;
        public float obstacleMinKg = 5f;
        public int layerMask = ~0;
        public Transform self;                  // the grandmother: never part of her own floor
        public Transform[] ignoreRoots;         // e.g. roofs, where a NavMesh island is useless

        readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>(1024);
        readonly List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
        // furnitureAsObstacles only, as of the last collect: the furniture baked in and where it
        // stood, and the heavy bodies left out only because they were moving or carried.
        readonly List<Rigidbody> baked = new List<Rigidbody>();
        readonly List<Vector3> bakedAt = new List<Vector3>();
        readonly List<Rigidbody> unsettled = new List<Rigidbody>();
        readonly Stopwatch watch = new Stopwatch();
        readonly Stopwatch collectWatch = new Stopwatch();
        NavMeshData data;
        NavMeshDataInstance instance;
        AsyncOperation running;
        float startedAt;
        bool again;

        public bool HasNavMesh { get; private set; }
        public bool Busy => running != null;
        public int BuildCount { get; private set; }
        // Collect plus build, main thread, milliseconds.
        public float FirstBuildMs { get; private set; } = -1f;
        public float CollectMs { get; private set; } = -1f;
        // Request to done for an asynchronous update, frames included.
        public float LastBuildMs { get; private set; } = -1f;
        public int SourceCount { get; private set; }
        public int ObstacleCount { get; private set; }
        // Colliders whose mesh is not readable, baked as a box of their bounds (see Collect).
        public int BoxedCount { get; private set; }

        public event Action Built;

        // The first build, now. False when the builder refused (no sources, bad settings).
        public bool BuildNow()
        {
            if (running != null) return false;
            watch.Restart();
            Collect();
            if (data == null) data = new NavMeshData(settings.agentTypeID) { name = "GrandmaNavMesh" };
            bool ok = NavMeshBuilder.UpdateNavMeshData(data, settings, sources, bounds);
            if (ok && !instance.valid) instance = NavMesh.AddNavMeshData(data);
            watch.Stop();
            LastBuildMs = (float)watch.Elapsed.TotalMilliseconds;
            if (FirstBuildMs < 0f) FirstBuildMs = LastBuildMs;
            if (!ok) return false;
            Done();
            return true;
        }

        // An update: now if nothing is running, otherwise right after the running one (its
        // sources must stay as they are until it is done).
        public void Request()
        {
            if (data == null) { BuildNow(); return; }
            if (running != null) { again = true; return; }
            Begin();
        }

        void Begin()
        {
            Collect();
            startedAt = Time.realtimeSinceStartup;
            running = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
        }

        // Called every frame by the owner. Finishes an update once the workers are done.
        public void Tick()
        {
            if (running == null || !running.isDone) return;
            running = null;
            LastBuildMs = (Time.realtimeSinceStartup - startedAt) * 1000f;
            Done();
            if (again)
            {
                again = false;
                Begin();
            }
        }

        void Done()
        {
            BuildCount++;
            HasNavMesh = true;
            try { Built?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void Collect()
        {
            collectWatch.Restart();
            sources.Clear();
            baked.Clear();
            bakedAt.Clear();
            unsettled.Clear();
            NavMeshBuilder.CollectSources(bounds, layerMask, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            int obstacles = 0, boxed = 0;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                NavMeshBuildSource s = sources[i];
                var col = s.component as Collider;
                if (col == null) continue;   // not a collider: keep as it came

                int verdict = Judge(col);
                if (verdict == Unsettled) Remember(unsettled, null, col.attachedRigidbody);
                if (verdict < 0) { sources.RemoveAt(i); continue; }
                if (BoxIfUnreadable(ref s)) { sources[i] = s; boxed++; }
                if (verdict > 0)
                {
                    s.area = 1;   // Not Walkable
                    sources[i] = s;
                    obstacles++;
                    Remember(baked, bakedAt, col.attachedRigidbody);
                }
            }
            SourceCount = sources.Count;
            ObstacleCount = obstacles;
            BoxedCount = boxed;
            collectWatch.Stop();
            CollectMs = (float)collectWatch.Elapsed.TotalMilliseconds;
        }

        // A MeshCollider whose mesh is not Read/Write enabled (garden and kitchen dressing): the
        // builder cannot read it, so in the editor it logs an error per source and per build,
        // and in a player it would leave the thing out. Its bounds stay readable: an oriented
        // box of them, in the collider's own frame, stands in for it.
        static bool BoxIfUnreadable(ref NavMeshBuildSource s)
        {
            if (s.shape != NavMeshBuildSourceShape.Mesh) return false;
            var mesh = s.sourceObject as Mesh;
            if (mesh == null || mesh.isReadable) return false;
            Bounds b = mesh.bounds;
            s.shape = NavMeshBuildSourceShape.Box;
            s.size = b.size;
            s.transform = s.transform * Matrix4x4.Translate(b.center);
            s.sourceObject = null;
            return true;
        }

        const int Unsettled = -2;

        // -1 drop, -2 drop for now (furniture on the move), 0 walkable geometry, 1 an obstacle
        // to walk around.
        int Judge(Collider col)
        {
            if (col.isTrigger || col is CharacterController) return -1;
            Transform t = col.transform;
            if (self != null && t.IsChildOf(self)) return -1;
            if (CrewRoster.Owner(t) != null) return -1;
            if (ignoreRoots != null)
                for (int r = 0; r < ignoreRoots.Length; r++)
                    if (ignoreRoots[r] != null && t.IsChildOf(ignoreRoots[r])) return -1;

            Rigidbody body = col.attachedRigidbody;
            if (body == null) return 0;
            if (!furnitureAsObstacles || body.isKinematic || body.mass < obstacleMinKg) return -1;
            if (!AtRest(body)) return Unsettled;   // flying past or carried, not in the way yet
            return 1;
        }

        static bool AtRest(Rigidbody body)
        {
            if (body.linearVelocity.sqrMagnitude > 0.25f) return false;
            return !(body.TryGetComponent(out MovableObject mo) && (mo.holder != null || mo.inPocket));
        }

        static void Remember(List<Rigidbody> bodies, List<Vector3> at, Rigidbody body)
        {
            if (body == null || bodies.Contains(body)) return;   // one entry per body, not per collider
            bodies.Add(body);
            if (at != null) at.Add(body.position);
        }

        // furnitureAsObstacles: has the furniture changed since the last collect? A baked body
        // gone, switched off, carried or moved more than `tolerance` metres leaves a stale hole;
        // a body that was on the move then and is at rest now belongs in the NavMesh.
        public bool FurnitureChanged(float tolerance)
        {
            float tol2 = tolerance * tolerance;
            for (int i = 0; i < baked.Count; i++)
            {
                Rigidbody b = baked[i];
                if (b == null || !b.gameObject.activeInHierarchy || !AtRest(b)) return true;
                if ((b.position - bakedAt[i]).sqrMagnitude > tol2) return true;
            }
            for (int i = 0; i < unsettled.Count; i++)
            {
                Rigidbody b = unsettled[i];
                if (b != null && b.gameObject.activeInHierarchy && !b.isKinematic && AtRest(b)) return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (instance.valid) instance.Remove();
            // A build still on the workers holds the data: leave it to the next asset unload
            // rather than pull it out from under them.
            if (data != null && running == null) UnityEngine.Object.Destroy(data);
            data = null;
            running = null;
            HasNavMesh = false;
        }
    }
}
