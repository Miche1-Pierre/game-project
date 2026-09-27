using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // The loading screen's hidden mini stage: a country road seen from the side, a crew member
    // running along it on the spot while the verge, the fence, the trees and the clouds rush
    // past, and every now and then a cardboard box (or a puddle) coming at him. He trips, goes
    // down, gets up, runs on (RunnerAnimator). A camera renders it into a RenderTexture that
    // the loading screen shows in a wooden frame.
    //
    // It lives 3 km under the world, on its own layer (31, unused by the slice), with its own
    // light; its camera sees only that layer, and its light lights only that layer, so neither
    // the menu nor the house ever shows it and it never lights them. The other way round, the
    // scene's own suns would light the stage too (and differently in the menu and in the
    // house): while the stage camera draws, and only then, they leave its layer out. No
    // AudioListener, not tagged MainCamera: nothing in the game takes it for a player's view.
    [DisallowMultipleComponent]
    public sealed class LoadingStage : MonoBehaviour
    {
        public const int Layer = 31;
        public static readonly Vector3 Origin = new Vector3(0f, -3000f, 0f);

        public RenderTexture Texture { get; private set; }
        public RunnerAnimator Runner { get; private set; }
        public int Obstacles { get; private set; }

        LoadingStageSettings settings;
        Material template;
        Camera cam;
        Light stageSun;
        readonly List<Light> suns = new List<Light>(8);
        readonly List<int> sunMasks = new List<int>(8);
        bool fogWas;
        Transform pivot;
        readonly List<Transform> scrollers = new List<Transform>(64);
        readonly List<float> wraps = new List<float>(64);
        readonly List<Mesh> meshes = new List<Mesh>(8);
        Transform box, puddle, active, leaving;
        bool activeIsSlip, leavingIsSlip, nextIsSlip;
        float tipT, obstacleX, leavingX;
        float gap;                // seconds of running before the next obstacle comes (the first: at once)
        const float RunnerX = -0.7f;
        const float SpawnX = 4.6f;
        // The first box starts just out of view (the picture shows about +-3.4 m at the box),
        // so the first trip comes about 1.1 s in: well inside SceneFlow's 1.5 s minimum, so even
        // the shortest load shows the gag, and the Play-mode tests can require one trip.
        const float FirstSpawnX = 4.0f;

        static readonly Color Sky = new Color(0.98f, 0.86f, 0.66f, 1f);

        public void Build(LoadingStageSettings s, int width, int height)
        {
            settings = s;
            template = s.template;
            transform.position = Origin;
            gameObject.layer = Layer;

            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "LoadingStage (runtime)",
                antiAliasing = 4,
                hideFlags = HideFlags.DontSave,
            };
            Texture.Create();

            var camGo = Child("StageCamera", new Vector3(RunnerX + 0.7f, 1.25f, -8.2f));
            camGo.transform.localRotation = Quaternion.Euler(3.5f, 0f, 0f);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.depth = -50f;
            cam.allowHDR = false;
            cam.useOcclusionCulling = false;
            cam.targetTexture = Texture;

            var lightGo = Child("StageLight", Vector3.zero);
            lightGo.transform.localRotation = Quaternion.Euler(36f, 28f, 0f);
            stageSun = lightGo.AddComponent<Light>();
            stageSun.type = LightType.Directional;
            stageSun.color = new Color(1f, 0.9f, 0.78f);
            stageSun.intensity = 0.95f;
            stageSun.shadows = LightShadows.Soft;
            stageSun.shadowStrength = 0.5f;
            stageSun.cullingMask = 1 << Layer;
            FindSuns();

            BuildGround();
            BuildScenery();
            BuildRunner();
            BuildObstacles();
        }

        GameObject Child(string name, Vector3 local)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            return go;
        }

        Material Land => LowPolyPalette.Land(template);

        GameObject MeshObject(string name, Mesh mesh, Vector3 local, bool shadows = true)
        {
            var go = Child(name, local);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Land;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---- the set ----

        // The road and the verges: long enough that the ends are never in view; they do not
        // move (they look the same all along), the things on them do.
        void BuildGround()
        {
            var m = new LowPolyMesh(64);
            m.Box(new Vector3(-60f, -0.4f, -4f), new Vector3(60f, 0f, -1.3f), Swatch.Verge);
            m.Box(new Vector3(-60f, -0.4f, -1.3f), new Vector3(60f, 0.005f, 1.3f), Swatch.Asphalt);
            m.Box(new Vector3(-60f, -0.4f, 1.3f), new Vector3(60f, 0f, 2f), Swatch.Gravel);
            m.Box(new Vector3(-60f, -0.4f, 2f), new Vector3(60f, -0.02f, 40f), Swatch.GrassB);
            var mesh = m.ToMesh("StageGround (runtime)");
            meshes.Add(mesh);
            MeshObject("Ground", mesh, Vector3.zero, false);
        }

        void BuildScenery()
        {
            var rng = new System.Random(5);

            // Centre-line dashes behind the runner.
            var dash = new LowPolyMesh(12);
            dash.Box(new Vector3(-0.9f, 0f, -0.07f), new Vector3(0.9f, 0.012f, 0.07f), Swatch.Line);
            var dashMesh = dash.ToMesh("StageDash (runtime)");
            meshes.Add(dashMesh);
            for (int i = 0; i < 8; i++) Scroll(MeshObject("Dash", dashMesh, new Vector3(-14f + i * 4f, 0.005f, 0.9f), false).transform, 16f);

            // Tufts of grass on both verges.
            var tuft = new LowPolyMesh(80);
            tuft.Blob(new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.16f, 0.22f), Swatch.Hedge, rng, 0.2f);
            var tuftMesh = tuft.ToMesh("StageTuft (runtime)");
            meshes.Add(tuftMesh);
            for (int i = 0; i < 10; i++)
            {
                float x = -14f + i * 3.1f + (float)rng.NextDouble();
                Scroll(MeshObject("Tuft", tuftMesh, new Vector3(x, 0f, -2.2f - (float)rng.NextDouble()), false).transform, 16f);
                Scroll(MeshObject("Tuft", tuftMesh, new Vector3(x + 1.4f, 0f, 1.9f + (float)rng.NextDouble() * 0.6f), false).transform, 16f);
            }

            // The fence along the far verge: the kit's own, or plain posts.
            for (int i = 0; i < 12; i++)
            {
                var p = settings.fence != null ? Place(settings.fence, new Vector3(-16f + i * 3f, 0f, 3.2f), 0f)
                                               : Post(new Vector3(-16f + i * 3f, 0f, 3.2f));
                Scroll(p, 18f);
            }

            // Trees behind, some near, some far.
            for (int i = 0; i < 9; i++)
            {
                float z = i % 2 == 0 ? 7f + (float)rng.NextDouble() * 3f : 14f + (float)rng.NextDouble() * 6f;
                var pos = new Vector3(-22f + i * 5.5f + (float)rng.NextDouble() * 2f, 0f, z);
                Transform t = settings.trees != null && settings.trees.Length > 0 && settings.trees[i % settings.trees.Length] != null
                    ? Place(settings.trees[i % settings.trees.Length], pos, (float)rng.NextDouble() * 360f)
                    : Blob("Tree", pos + Vector3.up * 2f, new Vector3(1.6f, 2.2f, 1.6f), Swatch.Hedge, rng);
                Scroll(t, (8.2f + z) * 0.45f + 4f);
            }

            // Hills on the horizon and two clouds: they do not scroll (too far to move).
            Blob("Hill", new Vector3(-10f, -6f, 60f), new Vector3(26f, 14f, 8f), Swatch.FarHill, rng);
            Blob("Hill", new Vector3(14f, -7f, 64f), new Vector3(30f, 15f, 8f), Swatch.FarHill, rng);
            Blob("Hill", new Vector3(34f, -6f, 58f), new Vector3(22f, 12f, 8f), Swatch.FarHill, rng);
            Scroll(Blob("Cloud", new Vector3(-6f, 9f, 50f), new Vector3(5f, 2f, 2f), Swatch.Cloud, rng), 30f);
            Scroll(Blob("Cloud", new Vector3(12f, 11f, 55f), new Vector3(6f, 2.4f, 2f), Swatch.Cloud, rng), 30f);
        }

        Transform Blob(string name, Vector3 local, Vector3 size, Swatch colour, System.Random rng)
        {
            var m = new LowPolyMesh(80);
            m.Blob(Vector3.zero, size, colour, rng, 0.12f);
            var mesh = m.ToMesh("Stage" + name + " (runtime)");
            meshes.Add(mesh);
            return MeshObject(name, mesh, local, colour != Swatch.Cloud && colour != Swatch.FarHill).transform;
        }

        Transform Post(Vector3 local)
        {
            var m = new LowPolyMesh(12);
            m.Box(new Vector3(-0.08f, 0f, -0.08f), new Vector3(0.08f, 1.1f, 0.08f), Swatch.CardboardDark);
            var mesh = m.ToMesh("StagePost (runtime)");
            meshes.Add(mesh);
            return MeshObject("Post", mesh, local).transform;
        }

        Transform Place(GameObject prefab, Vector3 local, float yaw)
        {
            var go = Instantiate(prefab, transform);
            go.name = prefab.name;
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Strip(go);
            return go.transform;
        }

        // A prefab on the stage keeps only what draws: on the stage's layer, no colliders.
        static void Strip(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }

        void Scroll(Transform t, float wrap)
        {
            scrollers.Add(t);
            wraps.Add(wrap);
        }

        // ---- the mover ----

        void BuildRunner()
        {
            pivot = Child("RunnerPivot", new Vector3(RunnerX, 0f, 0f)).transform;
            GameObject prefab = null;
            if (settings.crewBodies != null && settings.crewBodies.Length > 0)
                prefab = settings.crewBodies[Random.Range(0, settings.crewBodies.Length)];
            Animator animator = null;
            if (prefab != null)
            {
                var body = Instantiate(prefab, pivot);
                body.name = "Runner";
                // The crew model faces -Z: turned to face +X, the way he runs.
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                Strip(body);
                animator = body.GetComponentInChildren<Animator>();
            }
            else
            {
                // No crew asset: a little wooden figure, which the code tumble still throws down.
                var m = new LowPolyMesh(60);
                m.Box(new Vector3(-0.12f, 0f, -0.1f), new Vector3(0.12f, 0.8f, 0.1f), Swatch.CardboardDark);
                m.Box(new Vector3(-0.2f, 0.8f, -0.14f), new Vector3(0.2f, 1.45f, 0.14f), Swatch.Cardboard);
                m.Box(new Vector3(-0.13f, 1.48f, -0.13f), new Vector3(0.13f, 1.76f, 0.13f), Swatch.Tape);
                var mesh = m.ToMesh("StageFigure (runtime)");
                meshes.Add(mesh);
                var fig = MeshObject("Figure", mesh, Vector3.zero);
                fig.transform.SetParent(pivot, false);
            }
            Runner = new RunnerAnimator(animator, pivot, settings);
        }

        void BuildObstacles()
        {
            box = CardboardBox.Create(transform, new Vector3(SpawnX + 20f, 0f, 0f), new Vector3(0.62f, 0.48f, 0.5f), template, Layer).transform;
            var rng = new System.Random(9);
            var m = new LowPolyMesh(80);
            m.Blob(Vector3.zero, new Vector3(0.75f, 0.02f, 0.5f), Swatch.Puddle, rng, 0.08f);
            var mesh = m.ToMesh("StagePuddle (runtime)");
            meshes.Add(mesh);
            puddle = MeshObject("Puddle", mesh, new Vector3(SpawnX + 20f, 0.012f, 0f), false).transform;
            box.gameObject.SetActive(false);
            puddle.gameObject.SetActive(false);
        }

        // ---- the stage's own light ----

        void OnEnable()
        {
            Camera.onPreCull += BeforeStage;
            Camera.onPostRender += AfterStage;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void OnDisable()
        {
            Camera.onPreCull -= BeforeStage;
            Camera.onPostRender -= AfterStage;
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        void OnSceneChanged(Scene from, Scene to) => FindSuns();

        // The scene's directional lights (point and spot lights never reach 3 km down). Looked
        // up when the scene changes, not every frame.
        void FindSuns()
        {
            suns.Clear();
            foreach (var l in FindObjectsByType<Light>())
                if (l != null && l != stageSun && l.type == LightType.Directional) suns.Add(l);
            sunMasks.Clear();
            for (int i = 0; i < suns.Count; i++) sunMasks.Add(0);
        }

        void BeforeStage(Camera c)
        {
            if (c != cam) return;
            for (int i = 0; i < suns.Count; i++)
            {
                if (suns[i] == null) continue;
                sunMasks[i] = suns[i].cullingMask;
                suns[i].cullingMask = sunMasks[i] & ~(1 << Layer);
            }
            // The scene's haze is tuned for its own distances, not for a set 8 m away.
            fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
        }

        void AfterStage(Camera c)
        {
            if (c != cam) return;
            for (int i = 0; i < suns.Count; i++)
                if (suns[i] != null) suns[i].cullingMask = sunMasks[i];
            RenderSettings.fog = fogWas;
        }

        // ---- per frame ----

        public void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.1f);
            if (Runner == null) return;
            Runner.Tick(dt);
            float move = settings.runSpeed * Runner.WorldSpeed * dt;

            for (int i = 0; i < scrollers.Count; i++)
            {
                var t = scrollers[i];
                if (t == null) continue;
                Vector3 p = t.localPosition;
                p.x -= move;
                if (p.x < -wraps[i]) p.x += wraps[i] * 2f;
                t.localPosition = p;
            }

            TickObstacle(dt, move);
        }

        void TickObstacle(float dt, float move)
        {
            // The one he tripped on rolls away with the world (the box kicked to the verge).
            if (leaving != null)
            {
                leavingX -= move;
                Vector3 q = new Vector3(leavingX, leavingIsSlip ? 0.012f : 0f, leaving.localPosition.z);
                if (!leavingIsSlip)
                {
                    // Kicked: up, spinning, out of his path onto the near verge, where it lies.
                    tipT = Mathf.Min(1f, tipT + dt / 0.6f);
                    q.x += 0.9f * tipT;
                    q.z = Mathf.Lerp(0f, -2.6f, tipT);
                    q.y = Mathf.Sin(tipT * Mathf.PI) * 0.9f;
                    leaving.localRotation = Quaternion.Euler(-190f * tipT, 0f, -110f * tipT);
                }
                leaving.localPosition = q;
                if (leavingX < -7f)
                {
                    leaving.gameObject.SetActive(false);
                    leaving = null;
                }
            }

            if (active == null)
            {
                if (!Runner.IsRunning) return;
                gap -= dt;
                if (gap > 0f) return;
                bool slip = nextIsSlip;
                Transform next = slip ? puddle : box;
                if (next == leaving) return;   // still rolling away: wait for it
                nextIsSlip = !nextIsSlip;
                activeIsSlip = slip;
                active = next;
                obstacleX = Obstacles == 0 ? FirstSpawnX : SpawnX;
                active.localPosition = new Vector3(obstacleX, slip ? 0.012f : 0f, 0f);
                active.localRotation = Quaternion.identity;
                active.gameObject.SetActive(true);
                Obstacles++;
                return;
            }

            obstacleX -= move;
            active.localPosition = new Vector3(obstacleX, activeIsSlip ? 0.012f : 0f, 0f);
            // His leading foot reaches it: down he goes.
            if (obstacleX <= RunnerX + (activeIsSlip ? 0.25f : 0.5f))
            {
                Runner.Trip(activeIsSlip);
                leaving = active;
                leavingX = obstacleX;
                leavingIsSlip = activeIsSlip;
                tipT = 0f;
                active = null;
                // The next one comes a moment after he is back on his feet.
                gap = Random.Range(0.8f, 1.6f);
            }
        }

        void OnDestroy()
        {
            Runner?.Dispose();
            if (cam != null) cam.targetTexture = null;
            if (Texture != null)
            {
                Texture.Release();
                Destroy(Texture);
            }
            for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Destroy(meshes[i]);
        }
    }
}
