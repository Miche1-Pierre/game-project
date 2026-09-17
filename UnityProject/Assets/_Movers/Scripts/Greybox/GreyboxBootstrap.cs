using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Builds the whole Tutorial_01 greybox from primitives.
    // Built in the editor by the "The Movers > Create Greybox Scene" menu (visible before Play),
    // and also at runtime if you just drop this component into an empty scene and press Play.
    public class GreyboxBootstrap : MonoBehaviour
    {
        readonly List<MovableObject> objects = new List<MovableObject>();
        TruckCargo truck;

        void Awake()
        {
            // If the scene was pre-built in the editor, do not rebuild at Play.
            if (transform.childCount == 0) Build();
        }

#if UNITY_EDITOR
        public void BuildEditor() { Build(); }
#endif

        void Build()
        {
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);

            var sun = new GameObject("Sun");
            sun.transform.SetParent(transform);
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Box("Floor", new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40), new Color(0.35f, 0.4f, 0.35f));

            // ---- house: two rooms, wall height 3, thickness 0.3 ----
            Color wall = new Color(0.82f, 0.80f, 0.74f);
            const float h = 3f, t = 0.3f, y = 1.5f;
            Box("Wall_Back", new Vector3(0, y, 9), new Vector3(12, h, t), wall);
            Box("Wall_Left", new Vector3(-6, y, 4.5f), new Vector3(t, h, 9), wall);
            Box("Wall_Right", new Vector3(6, y, 4.5f), new Vector3(t, h, 9), wall);
            // front wall with a 3m opening to the garden (gap x -1.5..1.5)
            Box("Wall_Front_L", new Vector3(-3.75f, y, 0), new Vector3(4.5f, h, t), wall);
            Box("Wall_Front_R", new Vector3(3.75f, y, 0), new Vector3(4.5f, h, t), wall);
            // interior divider at z=5 with a NARROW ~1m door (gap x 3.6..4.6) -> the sofa puzzle
            Box("Wall_Div_L", new Vector3(-1.4f, y, 5), new Vector3(9.2f, h, t), wall);
            Box("Wall_Div_R", new Vector3(5.3f, y, 5), new Vector3(1.4f, h, t), wall);

            BuildTruck(new Vector3(0, 0, -6));
            // Spawn outside, between the truck and the house, facing the front opening.
            // The original spawn put the player inside the far room facing a bare wall 1.5 m
            // away with every object behind them. No interior spot works either: the room is
            // 12 m wide and 4 m deep, so from inside you never see both ends. Starting outside
            // states the job in one frame: that is your truck, that is the house, go.
            BuildPlayer(new Vector3(0, 1.2f, -3.0f), 0f);

            Color wood = new Color(0.6f, 0.45f, 0.3f);
            Color fabric = new Color(0.30f, 0.40f, 0.70f);
            Color glass = new Color(0.6f, 0.8f, 0.85f);
            Color metal = new Color(0.7f, 0.7f, 0.75f);
            Color green = new Color(0.3f, 0.6f, 0.35f);
            Color dark = new Color(0.15f, 0.15f, 0.15f);

            // far room (z 5..9): the big awkward stuff
            Movable("Sofa", new Vector3(-3, 0.5f, 7), new Vector3(2.4f, 0.9f, 1.0f), fabric, 80, 350, true, false);
            Movable("Table", new Vector3(3, 0.6f, 7), new Vector3(1.4f, 1.0f, 0.9f), wood, 30, 120, true, false);
            Movable("Television", new Vector3(4.6f, 1.2f, 8.6f), new Vector3(1.2f, 0.7f, 0.2f), dark, 20, 700, false, true); // not required, valuable: a tease
            Movable("Chair_A", new Vector3(1.8f, 0.5f, 8), new Vector3(0.6f, 1.0f, 0.6f), wood, 8, 40, true, false);
            Movable("Chair_B", new Vector3(2.6f, 0.5f, 8), new Vector3(0.6f, 1.0f, 0.6f), wood, 8, 40, true, false);
            Movable("Lamp", new Vector3(-5, 0.7f, 8), new Vector3(0.4f, 1.4f, 0.4f), glass, 5, 30, true, true);

            // near room (z 0..5)
            Movable("Box_A", new Vector3(-4, 0.5f, 2.5f), new Vector3(0.8f, 0.8f, 0.8f), wood, 10, 20, true, false);
            Movable("Box_B", new Vector3(-4, 1.4f, 2.5f), new Vector3(0.8f, 0.8f, 0.8f), wood, 10, 20, true, false);
            Movable("Box_C", new Vector3(-3, 0.5f, 2.5f), new Vector3(0.8f, 0.8f, 0.8f), wood, 10, 20, true, false);
            Movable("Plant", new Vector3(4.5f, 0.7f, 3), new Vector3(0.6f, 1.2f, 0.6f), green, 12, 25, true, false);
            Movable("Fridge", new Vector3(4.8f, 1.1f, 4), new Vector3(0.9f, 2.0f, 0.9f), metal, 120, 200, true, false); // heavy: weight feel
            Movable("Vase", new Vector3(0, 0.5f, 4.5f), new Vector3(0.4f, 0.7f, 0.4f), glass, 4, 90, false, true);      // not required, fragile: a tease

            var cmGO = new GameObject("ContractManager");
            cmGO.transform.SetParent(transform);
            var cm = cmGO.AddComponent<ContractManager>();
            cm.truck = truck;
            cm.allObjects = objects.ToArray();
            cm.timeLimit = 600f;

            var hudGO = new GameObject("HUD");
            hudGO.transform.SetParent(transform);
            hudGO.AddComponent<GameHUD>().contract = cm;
        }

        // ---- helpers ----
        Material Mat(Color c)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
#if UNITY_EDITOR
            // In the editor, save materials as assets so colors persist when the scene is saved.
            if (!Application.isPlaying)
            {
                System.IO.Directory.CreateDirectory("Assets/_Movers/Materials");
                string path = $"Assets/_Movers/Materials/mat_{Mathf.RoundToInt(c.r * 255)}_{Mathf.RoundToInt(c.g * 255)}_{Mathf.RoundToInt(c.b * 255)}.mat";
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) return existing;
                UnityEditor.AssetDatabase.CreateAsset(m, path);
                return m;
            }
#endif
            return m;
        }

        GameObject Box(string name, Vector3 pos, Vector3 size, Color col)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(transform);
            g.transform.position = pos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = Mat(col);
            return g;
        }

        void Movable(string name, Vector3 pos, Vector3 size, Color col, float weight, int value, bool required, bool fragile)
        {
            var g = Box(name, pos, size, col);
            g.AddComponent<Rigidbody>();
            var mo = g.AddComponent<MovableObject>();
            mo.displayName = name;
            mo.weight = weight;
            mo.contractValue = value;
            mo.requiredForContract = required;
            mo.fragile = fragile;
            objects.Add(mo);
        }

        void BuildTruck(Vector3 at)
        {
            var root = new GameObject("Truck");
            root.transform.SetParent(transform);
            root.transform.position = at;
            Color tc = new Color(0.55f, 0.2f, 0.2f);

            Box("Truck_Floor", at + new Vector3(0, 0.5f, 0), new Vector3(4f, 0.3f, 3f), tc).transform.SetParent(root.transform);
            Box("Truck_Back", at + new Vector3(0, 1.6f, -1.5f), new Vector3(4f, 2.2f, 0.2f), tc).transform.SetParent(root.transform);
            Box("Truck_Left", at + new Vector3(-2f, 1.6f, 0), new Vector3(0.2f, 2.2f, 3f), tc).transform.SetParent(root.transform);
            Box("Truck_Right", at + new Vector3(2f, 1.6f, 0), new Vector3(0.2f, 2.2f, 3f), tc).transform.SetParent(root.transform);
            // open side faces +z (toward the house)

            var trig = new GameObject("Truck_CargoTrigger");
            trig.transform.SetParent(root.transform);
            trig.transform.position = at + new Vector3(0, 1.4f, 0);
            var bc = trig.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(3.6f, 1.8f, 2.6f);
            truck = trig.AddComponent<TruckCargo>();
        }

        void BuildPlayer(Vector3 at, float yaw)
        {
            var p = new GameObject("Player");
            p.transform.SetParent(transform);
            p.transform.position = at;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cc = p.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = Vector3.zero;

            var camGO = new GameObject("PlayerCamera");
            camGO.transform.SetParent(p.transform);
            camGO.transform.localPosition = new Vector3(0, 0.7f, 0);
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.tag = "MainCamera";
            // Smoke blinds eyes, not bodies, so this rides the camera. Every player carries one,
            // whether or not they smoke: the cloud you have to walk through is someone else's.
            camGO.AddComponent<SmokeVision>();

            var pc = p.AddComponent<PlayerController>();
            pc.cam = camGO.transform;
            var pg = p.AddComponent<PlayerGrab>();
            pg.cam = camGO.transform;
            pg.controller = pc;
            // Starting inventory, GREYBOX_SPEC. It builds its own view at Play.
            var cig = p.AddComponent<PlayerCigarette>();
            cig.cam = camGO.transform;
            cig.grab = pg;
        }
    }
}
