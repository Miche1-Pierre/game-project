#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // Builds the Map 01 (grandma's house) greybox: ground floor + upstairs + stairs + truck + garden.
    // Reuses existing gameplay scripts and real Broken Vector prefabs (boxes, cabinet, trees).
    // The truck is a clean primitive (open bed) so loading is unambiguous; a real truck model
    // with a modeled open rear is a later visual swap. Enterprise-clean, prototype-scope.
    public static class Map01Builder
    {
        const string ScenePath = "Assets/_Movers/Scenes/Map01_Grandma.unity";
        const string MatDir = "Assets/_Movers/Materials";
        const string Storage = "Assets/BrokenVector/LowPolyStoragePack/Prefabs/";
        const string Trees = "Assets/BrokenVector/LowPolyTreePack/Prefabs/";
        const string Furniture = "Assets/_Project/Art/Furniture/";

        static Transform systems, environment, gameplay, playerRoot, lighting, ui;
        static Transform house, garden, movablesRoot, truckRoot, spawnPoints;
        static readonly List<MovableObject> movables = new List<MovableObject>();
        static TruckCargo truck;

        static readonly Color CWall = new Color(0.82f, 0.80f, 0.74f);
        static readonly Color CFloor = new Color(0.45f, 0.43f, 0.40f);
        static readonly Color CFloor2 = new Color(0.55f, 0.52f, 0.48f);
        static readonly Color CGround = new Color(0.34f, 0.42f, 0.34f);
        static readonly Color CDrive = new Color(0.30f, 0.30f, 0.32f);
        static readonly Color CTruck = new Color(0.55f, 0.2f, 0.2f);
        static readonly Color CCab = new Color(0.20f, 0.35f, 0.55f);
        static readonly Color CTyre = new Color(0.12f, 0.12f, 0.12f);

        [MenuItem("The Movers/Build Map 01 (Grandma)")]
        public static void Build()
        {
            movables.Clear();
            truck = null;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);

            systems = Root("_Systems");
            environment = Root("Environment");
            gameplay = Root("Gameplay");
            playerRoot = Root("Player");
            lighting = Root("Lighting");
            ui = Root("UI");
            house = Child(environment, "House");
            garden = Child(environment, "Garden");
            movablesRoot = Child(gameplay, "MovableObjects");
            truckRoot = Child(gameplay, "Truck");
            spawnPoints = Child(gameplay, "SpawnPoints");

            BuildLighting();
            BuildGroundAndGarden();
            BuildGroundFloorWalls();
            BuildUpstairs();
            BuildTruck(new Vector3(0f, 0f, -5.5f));
            var spawn = BuildSpawn(new Vector3(0f, 1.2f, -1.5f));
            BuildPlayer(spawn);
            BuildObjects();
            BuildContractAndHud();

            Directory.CreateDirectory("Assets/_Movers/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"The Movers: Map01_Grandma v2 built. Movables: {movables.Count} (required expected 8). Open {ScenePath} and press Play.");
        }

        static Transform Root(string name) => new GameObject(name).transform;
        static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent); return t; }

        static void BuildLighting()
        {
            var sun = new GameObject("Sun");
            sun.transform.SetParent(lighting);
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.1f; l.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildGroundAndGarden()
        {
            Box(environment, "Ground", new Vector3(0, -0.5f, 3), new Vector3(52, 1, 48), CGround);
            Box(garden, "Driveway", new Vector3(0, 0.02f, -3), new Vector3(4.5f, 0.06f, 7), CDrive);
            TreeAt("Tree Type1 01", new Vector3(-11, 0, -2));
            TreeAt("Tree Type3 02", new Vector3(11, 0, -3));
            TreeAt("Tree Type5 01", new Vector3(-10, 0, 4));
            TreeAt("Tree Type7 02", new Vector3(12, 0, 8));
            TreeAt("Tree Type2 03", new Vector3(-12, 0, 11));
            TreeAt("Tree Type6 01", new Vector3(11, 0, 14));
        }

        static void BuildGroundFloorWalls()
        {
            const float h = 3f, t = 0.3f, y = 1.5f;
            Box(house, "GF_Floor", new Vector3(0, 0.05f, 6), new Vector3(16, 0.1f, 12), CFloor);
            Box(house, "Wall_Back", new Vector3(0, y, 12), new Vector3(16.3f, h, t), CWall);
            Box(house, "Wall_Left", new Vector3(-8, y, 6), new Vector3(t, h, 12), CWall);
            Box(house, "Wall_Right", new Vector3(8, y, 6), new Vector3(t, h, 12), CWall);
            Box(house, "Wall_Front_L", new Vector3(-5, y, 0), new Vector3(6, h, t), CWall);
            Box(house, "Wall_Front_R", new Vector3(5, y, 0), new Vector3(6, h, t), CWall);
            Box(house, "Wall_Div_L", new Vector3(-2.4f, y, 6), new Vector3(11.2f, h, t), CWall);
            Box(house, "Wall_Div_R", new Vector3(6.15f, y, 6), new Vector3(3.7f, h, t), CWall);
            Box(house, "Wall_Mid_A", new Vector3(0, y, 7), new Vector3(t, h, 2), CWall);
            Box(house, "Wall_Mid_B", new Vector3(0, y, 10.5f), new Vector3(t, h, 3), CWall);
            Box(house, "Wall_Front_Div", new Vector3(0, y, 2.5f), new Vector3(t, h, 5), CWall);
        }

        static void BuildUpstairs()
        {
            const float h = 3f, t = 0.3f, y = 4.5f, slabY = 3.05f;
            var up = Child(house, "Upstairs");
            Box(up, "UF_Left", new Vector3(-1.75f, slabY, 6), new Vector3(12.5f, 0.1f, 12), CFloor2);
            Box(up, "UF_Right", new Vector3(7.5f, slabY, 6), new Vector3(1f, 0.1f, 12), CFloor2);
            Box(up, "UF_Front", new Vector3(5.75f, slabY, 0.75f), new Vector3(2.5f, 0.1f, 1.5f), CFloor2);
            Box(up, "UF_Back", new Vector3(5.75f, slabY, 8.5f), new Vector3(2.5f, 0.1f, 7f), CFloor2);
            var ramp = Box(up, "Stairs_Ramp", new Vector3(5.75f, 1.5f, 3.25f), new Vector3(2.3f, 0.3f, 4.3f), CFloor2);
            ramp.transform.rotation = Quaternion.Euler(44f, 0f, 0f);
            Box(up, "UWall_Back", new Vector3(0, y, 12), new Vector3(16.3f, h, t), CWall);
            Box(up, "UWall_Left", new Vector3(-8, y, 6), new Vector3(t, h, 12), CWall);
            Box(up, "UWall_Right", new Vector3(8, y, 6), new Vector3(t, h, 12), CWall);
            Box(up, "UWall_Front_L", new Vector3(-4.5f, y, 0), new Vector3(7, h, t), CWall);
            Box(up, "UWall_Front_R", new Vector3(6.5f, y, 0), new Vector3(3, h, t), CWall);
            Box(up, "UWall_HoleRail", new Vector3(4.4f, y, 3.25f), new Vector3(t, h, 3.5f), CWall);
            Box(up, "UWall_Div_A", new Vector3(0, y, 7), new Vector3(t, h, 2), CWall);
            Box(up, "UWall_Div_B", new Vector3(0, y, 10.5f), new Vector3(t, h, 3), CWall);
        }

        static Transform BuildSpawn(Vector3 at)
        {
            var s = Child(spawnPoints, "PlayerSpawn");
            s.position = at;
            return s;
        }

        // One clean primitive truck: open cargo bed (loadable) + simple cab + wheels.
        static void BuildTruck(Vector3 at)
        {
            truckRoot.position = at;
            var bed = Child(truckRoot, "CargoBed");
            Box(bed, "Bed_Floor", at + new Vector3(0, 0.6f, 0.4f), new Vector3(4f, 0.3f, 3.2f), CTruck);
            Box(bed, "Bed_Back", at + new Vector3(0, 1.7f, -1.1f), new Vector3(4f, 2.2f, 0.2f), CTruck);
            Box(bed, "Bed_Left", at + new Vector3(-2f, 1.7f, 0.4f), new Vector3(0.2f, 2.2f, 3.2f), CTruck);
            Box(bed, "Bed_Right", at + new Vector3(2f, 1.7f, 0.4f), new Vector3(0.2f, 2.2f, 3.2f), CTruck);

            var cab = Child(truckRoot, "Cab");
            Box(cab, "Cab_Body", at + new Vector3(0, 1.2f, -2.7f), new Vector3(4f, 2.0f, 1.6f), CCab);
            Box(cab, "Cab_Roof", at + new Vector3(0, 2.35f, -3.0f), new Vector3(3.6f, 0.5f, 1.0f), CCab);
            Wheel(cab, at + new Vector3(-1.7f, 0.4f, -3.1f));
            Wheel(cab, at + new Vector3(1.7f, 0.4f, -3.1f));
            Wheel(cab, at + new Vector3(-1.7f, 0.4f, -1.5f));
            Wheel(cab, at + new Vector3(1.7f, 0.4f, -1.5f));

            var trig = new GameObject("CargoZone");
            trig.transform.SetParent(truckRoot);
            trig.transform.position = at + new Vector3(0, 1.5f, 0.4f);
            var bc = trig.AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.size = new Vector3(3.6f, 1.8f, 2.8f);
            truck = trig.AddComponent<TruckCargo>();
        }

        static void Wheel(Transform parent, Vector3 pos)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            w.name = "Wheel"; w.transform.SetParent(parent);
            w.transform.position = pos; w.transform.rotation = Quaternion.Euler(0, 0, 90);
            w.transform.localScale = new Vector3(0.8f, 0.15f, 0.8f);
            w.GetComponent<Renderer>().sharedMaterial = Mat(CTyre);
        }

        static void BuildPlayer(Transform spawn)
        {
            var p = new GameObject("Player");
            p.transform.SetParent(playerRoot);
            p.transform.position = spawn.position;
            var cc = p.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = Vector3.zero; cc.stepOffset = 0.3f; cc.slopeLimit = 50f;

            var camGO = new GameObject("PlayerCamera");
            camGO.transform.SetParent(p.transform);
            camGO.transform.localPosition = new Vector3(0, 0.7f, 0);
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.tag = "MainCamera";

            var pc = p.AddComponent<PlayerController>(); pc.cam = camGO.transform;
            var pg = p.AddComponent<PlayerGrab>(); pg.cam = camGO.transform; pg.controller = pc;
        }

        static void BuildObjects()
        {
            Color fabric = new Color(0.30f, 0.40f, 0.70f);
            Color wood = new Color(0.6f, 0.45f, 0.3f);
            Color dark = new Color(0.15f, 0.15f, 0.15f);
            Color linen = new Color(0.85f, 0.82f, 0.7f);
            Color glass = new Color(0.6f, 0.8f, 0.85f);

            // Big furniture: low-poly models made in Blender (SM_Furniture_*), tinted with greybox materials.
            MovablePrefab(Furniture + "SM_Furniture_Sofa.fbx", "Sofa", new Vector3(4.5f, 0.1f, 9f), 80, 350, true, false, fabric);
            MovablePrefab(Furniture + "SM_Furniture_Table.fbx", "DiningTable", new Vector3(-4f, 0.1f, 9f), 30, 120, true, false, wood);
            MovablePrefab(Furniture + "SM_Furniture_TV.fbx", "Television", new Vector3(-6.5f, 0.1f, 11.5f), 20, 400, true, false, dark);
            MovablePrefab(Storage + "Box_01_Red.prefab", "Box_A", new Vector3(-3f, 0.6f, 3f), 10, 20, true, false);
            MovablePrefab(Storage + "Box_01_Blue.prefab", "Box_B", new Vector3(-2f, 0.6f, 3.6f), 10, 20, true, false);

            // Upstairs (carry them down the ramp): bed, real cabinet dresser, a box.
            MovablePrefab(Furniture + "SM_Furniture_Bed.fbx", "Bed", new Vector3(4f, 3.2f, 9f), 60, 300, true, false, linen);
            MovablePrefab(Storage + "Cabinet_01.prefab", "Dresser", new Vector3(-4f, 3.2f, 10.5f), 45, 200, true, false);
            MovablePrefab(Storage + "Box_01_Yellow.prefab", "Box_C", new Vector3(-5f, 3.2f, 7.5f), 10, 20, true, false);

            // Non-required extras (the extra-value tease): fragile vase + heavy fridge appliance.
            MovablePrimitive("Vase", new Vector3(2.6f, 0.5f, 3f), new Vector3(0.4f, 0.7f, 0.4f), glass, 4, 90, false, true);
            MovablePrefab(Storage + "Suitcase_01_Red.prefab", "Suitcase", new Vector3(3.4f, 0.6f, 4.2f), 8, 150, false, false);
            MovablePrefab(Furniture + "SM_Furniture_Fridge.fbx", "Fridge", new Vector3(-6.8f, 0.1f, 7.5f), 120, 250, false, false, new Color(0.85f, 0.86f, 0.88f));
        }

        static void BuildContractAndHud()
        {
            var cmGO = new GameObject("ContractManager");
            cmGO.transform.SetParent(systems);
            var cm = cmGO.AddComponent<ContractManager>();
            cm.truck = truck; cm.allObjects = movables.ToArray(); cm.timeLimit = 600f;

            var hudGO = new GameObject("HUD");
            hudGO.transform.SetParent(ui);
            hudGO.AddComponent<GameHUD>().contract = cm;

            var dbg = new GameObject("DebugTools");
            dbg.transform.SetParent(systems);
            dbg.AddComponent<MoversDebugTools>();
        }

        static Material Mat(Color c)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/mat_{Mathf.RoundToInt(c.r * 255)}_{Mathf.RoundToInt(c.g * 255)}_{Mathf.RoundToInt(c.b * 255)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color col)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(parent);
            g.transform.position = pos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = Mat(col);
            return g;
        }

        static void TreeAt(string prefabName, Vector3 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Trees + prefabName + ".prefab");
            if (prefab == null) return;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            g.transform.SetParent(garden);
            g.transform.position = pos;
        }

        static void MovablePrimitive(string name, Vector3 pos, Vector3 size, Color col, float weight, int value, bool required, bool fragile)
        {
            var g = Box(movablesRoot, name, pos, size, col);
            g.AddComponent<Rigidbody>();
            var mo = g.AddComponent<MovableObject>();
            mo.displayName = name; mo.weight = weight; mo.contractValue = value;
            mo.requiredForContract = required; mo.fragile = fragile;
            movables.Add(mo);
        }

        static void MovablePrefab(string prefabPath, string name, Vector3 pos, float weight, int value, bool required, bool fragile, Color? tint = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Map01Builder: prefab not found at {prefabPath}, using a primitive for {name}.");
                MovablePrimitive(name, pos, new Vector3(0.8f, 0.8f, 0.8f), tint ?? new Color(0.6f, 0.45f, 0.3f), weight, value, required, fragile);
                return;
            }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            g.name = name; g.transform.SetParent(movablesRoot); g.transform.position = pos;
            if (tint.HasValue)
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mat(tint.Value);
            SimplifyCollider(g);
            if (g.GetComponent<Rigidbody>() == null) g.AddComponent<Rigidbody>();
            var mo = g.GetComponent<MovableObject>() ?? g.AddComponent<MovableObject>();
            mo.displayName = name; mo.weight = weight; mo.contractValue = value;
            mo.requiredForContract = required; mo.fragile = fragile;
            movables.Add(mo);
        }

        static void SimplifyCollider(GameObject g)
        {
            foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var bc = g.AddComponent<BoxCollider>();
            var renderers = g.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            var ls = g.transform.lossyScale;
            bc.center = g.transform.InverseTransformPoint(b.center);
            bc.size = new Vector3(
                b.size.x / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
                b.size.y / Mathf.Max(0.0001f, Mathf.Abs(ls.y)),
                b.size.z / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
        }
    }
}
#endif
