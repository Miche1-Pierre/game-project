#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // Map 01 environment - coherent layout pass.
    // Flat grass property with rolling hills around it; house + attached garage; driveway with a
    // parked car aligned to it; front porch + door; back paved patio with the garden table/benches;
    // storage corner by the cellar; small fenced farm; lean-to shed. Reused Broken Vector props are
    // ground-snapped and scaled to a sensible real size (trees normalised to ~7 m). No textures.
    public static class Map01EnvBuilder
    {
        const string ScenePath = "Assets/_Movers/Scenes/Map01_House.unity";
        const string MatDir = "Assets/_Movers/Materials";
        const string Arch = "Assets/_Project/Art/Architecture/";
        const string TreesP = "Assets/BrokenVector/LowPolyTreePack/Prefabs/";
        const string DungF = "Assets/BrokenVector/LowPolyDungeon/Prefabs/Furniture/";
        const string DungT = "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/";
        const string StoreP = "Assets/BrokenVector/LowPolyStoragePack/Prefabs/";
        const string CarsP = "Assets/BrokenVector/LowPolyCarPack/Prefabs/";

        static readonly Color CGrass = new Color(0.36f, 0.53f, 0.27f);
        static readonly Color CDrive = new Color(0.33f, 0.33f, 0.35f);
        static readonly Color CPatio = new Color(0.78f, 0.74f, 0.66f);
        static readonly Color CGlass = new Color(0.55f, 0.75f, 0.88f);
        static readonly Color CWood = new Color(0.52f, 0.38f, 0.26f);
        static readonly Color CRoofC = new Color(0.64f, 0.33f, 0.26f);
        static readonly Color CWall = new Color(0.90f, 0.87f, 0.79f);
        static readonly Color CStone = new Color(0.55f, 0.55f, 0.55f);
        static readonly Color CHedge = new Color(0.30f, 0.42f, 0.22f);

        static Transform envRoot, houseRoot, playerRoot, lightRoot, yardRoot;

        [MenuItem("The Movers/Build Map 01 Environment")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            lightRoot = new GameObject("Lighting").transform;
            envRoot = new GameObject("Environment").transform;
            playerRoot = new GameObject("Player").transform;
            houseRoot = new GameObject("House").transform; houseRoot.SetParent(envRoot);
            yardRoot = new GameObject("Yard").transform; yardRoot.SetParent(envRoot);

            BuildLighting();
            BuildTerrain();
            BuildHouse();
            BuildCellar();
            BuildGarage();
            BuildDriveAndCar();
            BuildFrontYard();
            BuildBackYard();
            BuildStorageCorner();
            BuildFarm();
            BuildShed();
            BuildPlayer(new Vector3(0f, 1.2f, -5f));

            Directory.CreateDirectory("Assets/_Movers/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("The Movers: Map01_House rebuilt (coherent layout pass).");
        }

        static void BuildLighting()
        {
            var sun = new GameObject("Sun"); sun.transform.SetParent(lightRoot);
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.15f; l.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        // Flat grass under the property (world y=0), gentle rolling hills beyond, pocket under the cellar.
        static void BuildTerrain()
        {
            const int res = 513; const float tsize = 160f, theight = 10f, ox = -80f, oz = -70f;
            const float padN = 3f / theight; // terrain object at y=-3 -> property surface at y=0
            var td = new TerrainData { heightmapResolution = res, size = new Vector3(tsize, theight, tsize) };
            var h = new float[res, res];
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    float wx = ox + xi * (tsize / (res - 1)), wz = oz + zi * (tsize / (res - 1));
                    float dx = Mathf.Max(0f, Mathf.Max(-26f - wx, wx - 22f));
                    float dz = Mathf.Max(0f, Mathf.Max(-18f - wz, wz - 30f));
                    float ramp = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dz * dz) - 3f) / 22f);
                    float hills = Mathf.PerlinNoise(wx * 0.03f + 11.1f, wz * 0.03f + 4.7f);
                    float v = padN + ramp * (0.03f + 0.32f * hills);
                    if (wx > -5.7f && wx < -1.3f && wz > 0.3f && wz < 4.7f) v = 0.02f; // cellar pocket
                    h[zi, xi] = v;
                }
            td.SetHeights(0, 0, h);
            const string tdPath = "Assets/_Movers/Scenes/Map01_Terrain.asset";
            AssetDatabase.DeleteAsset(tdPath); AssetDatabase.CreateAsset(td, tdPath);
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain"; go.transform.SetParent(envRoot); go.transform.position = new Vector3(ox, -3f, oz);
            go.GetComponent<Terrain>().materialTemplate = Mat(CGrass);
        }

        // House footprint x[-6,6] z[0,8], 1 storey + attic, gable roof.
        static void BuildHouse()
        {
            var gf = Child(houseRoot, "GroundFloor");
            var at = Child(houseRoot, "Attic");
            var roof = Child(houseRoot, "Roof");
            var inner = Child(houseRoot, "Interior");

            WExt("Window", -4, 0, 0, gf); WExt("Door", 0, 0, 0, gf); WExt("Window", 4, 0, 0, gf);
            WExt("Window", -4, 8, 0, gf); WExt("Bay", 0, 8, 0, gf); WExt("Window", 4, 8, 0, gf);
            WExt("Window", -6, 2, 90, gf); WExt("Window", -6, 6, 90, gf);
            WExt("Door", 6, 2, 90, gf); WExt("Window", 6, 6, 90, gf);

            for (int xi = -1; xi <= 1; xi++)
                foreach (int z in new[] { 2, 6 })
                {
                    if (xi == -1 && z == 2) continue; // cellar hole
                    Floor(xi * 4, 0, z, gf);
                }
            for (int xi = -1; xi <= 1; xi++)
                foreach (int z in new[] { 2, 6 })
                {
                    if (xi == 1 && z == 6) continue; // stairwell hole
                    Floor(xi * 4, 3, z, at);
                }

            RoofFit("SM_Roof_Big", 13f, 9f, 0f, 3f, 4f, 12f, 8f, roof);

            WInt("Opening", -4, 4, 0, inner); WInt("Door", 0, 4, 0, inner); WInt("Opening", 4, 4, 0, inner);
            WInt("Door", 0, 6, 90, inner);
            P(Arch + "SM_Stairs.fbx", new Vector3(4, 0.2f, 4.3f), 0, inner, CWall);

            P(Arch + "SM_Chimney.fbx", new Vector3(-3, 3.5f, 2f), 0, roof, CRoofC);
            P(Arch + "SM_PorchPost.fbx", new Vector3(-1.4f, 0, -1.7f), 0, inner, CWood);
            P(Arch + "SM_PorchPost.fbx", new Vector3(1.4f, 0, -1.7f), 0, inner, CWood);
            Prim("PorchCanopy", new Vector3(0, 2.72f, -1.1f), new Vector3(3.4f, 0.16f, 2.4f), CRoofC, inner);
            P(Arch + "SM_Door.fbx", new Vector3(0, 0, 0.03f), 0, inner, CWall);
        }

        static void BuildCellar()
        {
            var c = Child(houseRoot, "Cellar");
            Prim("Cel_Floor", new Vector3(-3.5f, -2.7f, 2.5f), new Vector3(5f, 0.2f, 5f), CStone, c);
            Prim("Cel_WallL", new Vector3(-6f, -1.4f, 2.5f), new Vector3(0.2f, 2.8f, 5f), CStone, c);
            Prim("Cel_WallR", new Vector3(-1f, -1.4f, 2.5f), new Vector3(0.2f, 2.8f, 5f), CStone, c);
            Prim("Cel_WallB", new Vector3(-3.5f, -1.4f, 5f), new Vector3(5f, 2.8f, 0.2f), CStone, c);
            Prim("Cel_WallF", new Vector3(-3.5f, -1.4f, 0f), new Vector3(5f, 2.8f, 0.2f), CStone, c);
            P(Arch + "SM_Stairs.fbx", new Vector3(-4f, -2.6f, 0.6f), 0, c, CStone);
        }

        // Attached garage x[6,14] z[0,4]; roof scaled to fit; garage door on the front toward the drive.
        static void BuildGarage()
        {
            var g = Child(envRoot, "Garage");
            WExt("BigOpening", 8, 0, 0, g); P(Arch + "SM_GarageDoor.fbx", new Vector3(8, 0, 0), 0, g, CStone);
            WExt("Solid", 12, 0, 0, g);
            WExt("Solid", 8, 4, 0, g); WExt("Window", 12, 4, 0, g);
            WExt("Solid", 14, 2, 90, g);
            Floor(8, 0, 2, g); Floor(12, 0, 2, g);
            RoofFit("SM_Roof_Small", 7f, 6f, 10f, 3f, 2f, 8f, 4f, g);
        }

        static void BuildDriveAndCar()
        {
            var d = Child(yardRoot, "Driveway");
            Prim("Road", new Vector3(-2, 0.02f, -13.5f), new Vector3(60f, 0.06f, 3f), CDrive, d);
            Prim("Drive", new Vector3(8, 0.03f, -6.5f), new Vector3(5f, 0.05f, 13f), CDrive, d);
            Prim("Path", new Vector3(0, 0.03f, -7f), new Vector3(2f, 0.05f, 12f), CDrive, d);
            PlaceRaw(CarsP + "Car_2_Blue.prefab", new Vector3(8, 0, -8f), 90, d); // parked on the drive, clear of the garage door
        }

        static void BuildFrontYard()
        {
            var f = Child(yardRoot, "FrontYard");
            // boundary railing along the road, gaps at the path (x0) and the drive (x8)
            foreach (float fx in new[] { -14f, -10f, -6f, -2f, 13f, 17f, 21f })
                PlaceRaw(DungT + "Balcony_Railing.prefab", new Vector3(fx, 0, -12.5f), 0, f);
            // trimmed foundation hedge along the front wall, split for the porch/path
            Prim("Hedge_W", new Vector3(-3.9f, 0.35f, -0.7f), new Vector3(3.8f, 0.7f, 0.5f), CHedge, f);
            Prim("Hedge_E", new Vector3(3.9f, 0.35f, -0.7f), new Vector3(3.8f, 0.7f, 0.5f), CHedge, f);
            TreeAt("Tree Type1 01", -13, -4, 6.5f, 20, f);
            TreeAt("Tree Type3 02", 16, -6, 7.5f, 120, f);
            TreeAt("Tree Type1 03", -17, 2, 6f, 200, f);
        }

        static void BuildBackYard()
        {
            var b = Child(yardRoot, "BackYard");
            // paved patio off the rear bay window, with the garden table + benches (scaled down)
            Prim("Patio", new Vector3(0, 0.04f, 10.5f), new Vector3(7f, 0.08f, 5f), CPatio, b);
            PlaceRaw(DungF + "Table_Small.prefab", new Vector3(0, 0.08f, 10.5f), 0, b, 0.62f);
            PlaceRaw(DungF + "Bench.prefab", new Vector3(0, 0.08f, 9.4f), 180, b, 0.62f);
            PlaceRaw(DungF + "Bench.prefab", new Vector3(0, 0.08f, 11.6f), 0, b, 0.62f);
            // back-garden trees, normalised heights
            TreeAt("Tree Type3 03", 14, 16, 7.5f, 0, b);
            TreeAt("Tree Type3 04", 10, 22, 7f, 60, b);
            TreeAt("Tree Type1 02", -14, 20, 6.5f, 150, b);
            TreeAt("Tree Type2 03", 18, 8, 7.5f, 30, b);
        }

        // Barrels + crates tucked against the west wall, by the cellar side.
        static void BuildStorageCorner()
        {
            var s = Child(yardRoot, "StorageCorner");
            PlaceRaw(StoreP + "Barrel_01.prefab", new Vector3(-7.2f, 0, 2f), 0, s);
            PlaceRaw(StoreP + "Barrel_02.prefab", new Vector3(-7.4f, 0, 3.2f), 30, s);
            PlaceRaw(DungF + "Barrel_Closed.prefab", new Vector3(-7.3f, 0, 4.4f), 0, s);
            PlaceRaw(StoreP + "Crate_01.prefab", new Vector3(-8.3f, 0, 2.6f), 20, s);
        }

        // Small fenced farm, back-left: barn (8x4, roof fitted) + a fenced field.
        static void BuildFarm()
        {
            var f = Child(yardRoot, "Farm");
            WExt("BigOpening", -18, 14, 0, f); WExt("Solid", -14, 14, 0, f);
            WExt("Solid", -18, 18, 0, f); WExt("Solid", -14, 18, 0, f);
            WExt("Solid", -20, 16, 90, f); WExt("Solid", -12, 16, 90, f);
            Floor(-18, 0, 16, f); Floor(-14, 0, 16, f);
            RoofFit("SM_Roof_Small", 7f, 6f, -16f, 3f, 16f, 8f, 4f, f);
            // fenced field in front of the barn
            foreach (float fx in new[] { -21f, -17f, -13f })
                PlaceRaw(DungT + "Balcony_Railing.prefab", new Vector3(fx, 0, 20.5f), 0, f);
            foreach (float fz in new[] { 12.5f, 16.5f, 20.5f })
            {
                PlaceRaw(DungT + "Balcony_Railing.prefab", new Vector3(-22.5f, 0, fz), 90, f);
                PlaceRaw(DungT + "Balcony_Railing.prefab", new Vector3(-10.5f, 0, fz), 90, f);
            }
        }

        // Lean-to hangar, back-right: sloped sheet roof sized to its posts.
        static void BuildShed()
        {
            var s = Child(yardRoot, "Shed");
            Prim("Shed_Post", new Vector3(11, 1.3f, 15), new Vector3(0.22f, 2.6f, 0.22f), CWood, s);
            Prim("Shed_Post", new Vector3(17, 1.3f, 15), new Vector3(0.22f, 2.6f, 0.22f), CWood, s);
            Prim("Shed_Post", new Vector3(11, 1.05f, 19), new Vector3(0.22f, 2.1f, 0.22f), CWood, s);
            Prim("Shed_Post", new Vector3(17, 1.05f, 19), new Vector3(0.22f, 2.1f, 0.22f), CWood, s);
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Shed_Roof"; roof.transform.SetParent(s);
            roof.transform.position = new Vector3(14, 2.5f, 17); roof.transform.localScale = new Vector3(7f, 0.12f, 4.9f);
            roof.transform.rotation = Quaternion.Euler(7f, 0, 0);
            roof.GetComponent<Renderer>().sharedMaterial = Mat(CRoofC);
            PlaceRaw(StoreP + "Crate_02.prefab", new Vector3(13, 0, 16.5f), 15, s);
            PlaceRaw(StoreP + "Barrel_01.prefab", new Vector3(16, 0, 18), 0, s);
        }

        static void BuildPlayer(Vector3 at)
        {
            var p = new GameObject("Player"); p.transform.SetParent(playerRoot); p.transform.position = at;
            var cc = p.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = Vector3.zero; cc.stepOffset = 0.35f; cc.slopeLimit = 55f;
            var camGO = new GameObject("PlayerCamera"); camGO.transform.SetParent(p.transform);
            camGO.transform.localPosition = new Vector3(0, 0.7f, 0);
            camGO.AddComponent<Camera>(); camGO.AddComponent<AudioListener>(); camGO.tag = "MainCamera";
            camGO.AddComponent<SmokeVision>();   // the blindness belongs to the eyes
            var pc = p.AddComponent<PlayerController>(); pc.cam = camGO.transform;
            var drunk = p.AddComponent<Drunkenness>();
            var cig = p.AddComponent<PlayerCigarette>(); cig.cam = camGO.transform;
            var beer = p.AddComponent<PlayerBeer>(); beer.cam = camGO.transform; beer.drunk = drunk;
        }

        // ---- helpers ----
        static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent); return t; }

        static void WExt(string v, float x, float z, float yRot, Transform parent)
        {
            P(Arch + "SM_Wall_Ext_" + v + ".fbx", new Vector3(x, 0, z), yRot, parent, CWall);
            if (v == "Window") { P(Arch + "SM_Window.fbx", new Vector3(x, 1.5f, z), yRot, parent, CWall); Glass(x, 1.5f, z, 1.5f, 1.1f, yRot, parent); }
            if (v == "Bay") Glass(x, 1.3f, z, 2.5f, 1.7f, yRot, parent);
        }
        static void WInt(string v, float x, float z, float yRot, Transform parent) => P(Arch + "SM_Wall_Int_" + v + ".fbx", new Vector3(x, 0, z), yRot, parent, CWall);
        static void Floor(float x, float y, float z, Transform parent) => P(Arch + "SM_Floor_4x4.fbx", new Vector3(x, y, z), 0, parent, CWall);

        static void Glass(float x, float y, float z, float w, float ht, float yRot, Transform parent)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Glass"; g.transform.SetParent(parent);
            g.transform.position = new Vector3(x, y, z); g.transform.rotation = Quaternion.Euler(0, yRot, 0);
            g.transform.localScale = new Vector3(w, ht, 0.05f);
            g.GetComponent<Renderer>().sharedMaterial = Mat(CGlass);
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        static void RoofFit(string prefab, float baseL, float baseW, float cx, float baseY, float cz, float fx, float fz, Transform parent)
        {
            var g = P(Arch + prefab + ".fbx", new Vector3(cx, baseY, cz), 0, parent, CRoofC);
            if (g == null) return;
            const float over = 1.2f;
            g.transform.localScale = new Vector3((fx + over) / baseL, 1f, (fz + over) / baseW);
        }

        // Scale a tree to a target height regardless of its source size, then ground-snap.
        static void TreeAt(string name, float x, float z, float targetH, float yRot, Transform parent)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(TreesP + name + ".prefab");
            if (pf == null) return;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            g.transform.SetParent(parent); g.transform.rotation = Quaternion.Euler(0, yRot, 0);
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (b.size.y > 0.01f) g.transform.localScale = Vector3.one * (targetH / b.size.y);
            g.transform.position = new Vector3(x, 0, z);
            rs = g.GetComponentsInChildren<Renderer>();
            b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            g.transform.position += Vector3.up * (0f - b.min.y);
        }

        static void Prim(string name, Vector3 pos, Vector3 scale, Color col, Transform parent)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(parent); g.transform.position = pos; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(col);
        }

        static GameObject P(string path, Vector3 pos, float yRot, Transform parent, Color col)
        {
            string pfPath = path.Replace("/Art/Architecture/SM_", "/Prefabs/Architecture/PF_")
                                .Replace("/Art/Props/SM_", "/Prefabs/Props/PF_").Replace(".fbx", ".prefab");
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(pfPath);
            bool isPrefab = src != null;
            if (src == null) src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogWarning("Map01EnvBuilder: missing " + path); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
            g.transform.SetParent(parent); g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0, yRot, 0);
            if (!isPrefab)
            {
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mat(col);
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();
            }
            return g;
        }

        // Reused third-party prefab: ground-snap (mesh bottom on pos.y) + optional uniform scale.
        static GameObject PlaceRaw(string path, Vector3 pos, float yRot, Transform parent, float scale = 1f)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (pf == null) { Debug.LogWarning("Map01EnvBuilder: reuse asset missing " + path); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            g.transform.SetParent(parent); g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0, yRot, 0);
            if (scale != 1f) g.transform.localScale = Vector3.one * scale;
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                g.transform.position += Vector3.up * (pos.y - b.min.y);
            }
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();
            return g;
        }

        static Material Mat(Color c)
        {
            Shader sh = Shader.Find("Standard"); if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { color = c }; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/mat_{Mathf.RoundToInt(c.r * 255)}_{Mathf.RoundToInt(c.g * 255)}_{Mathf.RoundToInt(c.b * 255)}.mat";
            var ex = AssetDatabase.LoadAssetAtPath<Material>(path); if (ex != null) return ex;
            AssetDatabase.CreateAsset(m, path); return m;
        }
    }
}
#endif
