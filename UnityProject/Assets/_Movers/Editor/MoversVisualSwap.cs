using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Swaps the greybox primitive LOOK for a real mesh, without touching the PHYSICS.
    //
    // The greybox tunes weight, mass and collisions on scaled primitive cubes. Replacing
    // those cubes with imported meshes would change the collider shape and silently retune
    // the whole feel. So we keep the cube (collider + Rigidbody + MovableObject), hide its
    // renderer, and nest a visual mesh inside it fitted to the same volume.
    //
    // Consequence: the pack can be removed at any time and the greybox still plays identically.
    public class MoversVisualSwap : EditorWindow
    {
        public const string VisualName = "__visual";

        // Above this ratio between the largest and smallest fill scale, stop filling and
        // keep the mesh proportions instead. Tuned on the dungeon pack, 2026-09-17.
        const float MaxAnisotropy = 2.5f;

        // The house kit, not the whole project: scanning "Assets" also finds the dungeon
        // and storage packs, and the fallback name match then picks a dungeon mesh.
        public const string DefaultSourceFolder = "Assets/_Project/Prefabs";

        string folder = DefaultSourceFolder;
        bool preserveAspect = false;   // default: fill the collider, see note in Fit()
        Vector2 scroll;
        readonly List<string> log = new List<string>();

        [MenuItem("The Movers/Visual Swap (keep physics)")]
        static void Open() => GetWindow<MoversVisualSwap>("Visual Swap");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Hides the primitive renderer and nests a matching mesh inside each MovableObject.\n" +
                "Colliders, Rigidbody and MovableObject values are never modified.\n" +
                "Box_A / Box_B / Box_C deliberately receive different meshes.",
                MessageType.Info);

            folder = EditorGUILayout.TextField("Source folder", folder);
            EditorGUILayout.LabelField("Generated variants live in Assets/_Movers/Generated", EditorStyles.miniLabel);
            preserveAspect = EditorGUILayout.Toggle("Preserve aspect ratio", preserveAspect);

            EditorGUILayout.Space();
            if (GUILayout.Button("Swap visuals in open scene"))
            {
                log.Clear();
                log.AddRange(Swap(folder, preserveAspect));
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            if (GUILayout.Button("Restore greybox primitives"))
            {
                log.Clear();
                log.AddRange(Restore());
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var line in log) EditorGUILayout.LabelField(line);
            EditorGUILayout.EndScrollView();
        }

        // ---------- public operations, shared by the window and the batch-mode CLI ----------

        public static List<string> Swap(string sourceFolder, bool preserveAspect)
        {
            var log = new List<string>();
            var meshes = LoadMeshSources(sourceFolder);
            if (meshes.Count == 0)
            {
                log.Add("No mesh found under " + sourceFolder + ". Import the pack first.");
                return log;
            }
            log.Add(meshes.Count + " mesh sources available.");

            var movables = Object.FindObjectsByType<MovableObject>(FindObjectsSortMode.None);
            int done = 0;

            foreach (var mo in movables)
            {
                var match = FindMatch(meshes, mo.name);
                if (match == null) { log.Add("- " + mo.name + ": no match, left as a grey box"); continue; }

                var old = mo.transform.Find(VisualName);
                if (old != null) DestroyImmediate(old.gameObject);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(match, mo.transform);
                if (visual == null) { log.Add("- " + mo.name + ": instantiate failed"); continue; }
                visual.name = VisualName;
                StripPhysics(visual);
                Fit(visual.transform, preserveAspect);

                var r = mo.GetComponent<Renderer>();
                if (r != null) r.enabled = false;

                log.Add("+ " + mo.name + " -> " + match.name);
                done++;
            }

            log.Add("Swapped " + done + " of " + movables.Length + ". Physics untouched.");
            return log;
        }

        public static List<string> Restore()
        {
            var log = new List<string>();
            var movables = Object.FindObjectsByType<MovableObject>(FindObjectsSortMode.None);
            foreach (var mo in movables)
            {
                var v = mo.transform.Find(VisualName);
                if (v != null) DestroyImmediate(v.gameObject);
                var r = mo.GetComponent<Renderer>();
                if (r != null) r.enabled = true;
            }
            log.Add("Restored " + movables.Length + " primitives.");
            return log;
        }

        // ---------- internals ----------

        // The pack prefabs ship their own MeshColliders. Nesting one inside a movable gave the
        // object a second collider, and a concave MeshCollider on a dynamic Rigidbody is invalid:
        // Unity logs an error and the collision response is wrong. The greybox BoxCollider on the
        // parent cube is the only collider that should exist, so the visual is stripped of
        // everything physical. This is what makes "physics untouched" actually true.
        static void StripPhysics(GameObject visual)
        {
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c, true);
            foreach (var rb in visual.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb, true);
        }

        // Scales the visual so it fills the parent cube's volume.
        // The parent cube uses a unit mesh, so its world size equals its localScale.
        // A child inherits that scale, so a child local scale of 1/bounds gives an exact fit.
        static void Fit(Transform visual, bool preserveAspect)
        {
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            var bounds = LocalBounds(visual);
            if (bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f) return;

            // Default is to FILL the parent cube, not to fit inside it.
            // The cube is the collider. In a greybox the player must see what they collide
            // with, so a stretched mesh that matches the collider is more honest than a
            // correctly proportioned one floating inside it. Tick the box to keep proportions.
            Vector3 s = new Vector3(1f / bounds.size.x, 1f / bounds.size.y, 1f / bounds.size.z);

            // Guard against grotesque stretching. Filling the box is fine when the mesh is
            // roughly the right shape, and absurd when it is not: Plant_Root is a small root,
            // so filling a 0.6 x 1.2 x 0.6 box stretched it 27x vertically. Past a ratio of
            // MaxAnisotropy between the largest and smallest required scale, fall back to a
            // uniform fit so the object stays recognisable.
            float lo = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            float hi = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            bool tooStretched = lo > 0f && (hi / lo) > MaxAnisotropy;

            if (preserveAspect || tooStretched)
            {
                s = new Vector3(lo, lo, lo);
            }
            visual.localScale = s;
            visual.localPosition = -Vector3.Scale(bounds.center, s);
        }

        // Mesh bounds rather than Renderer bounds: this has to work in batch mode,
        // where renderer bounds are not guaranteed to be up to date.
        static Bounds LocalBounds(Transform root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>();
            bool any = false;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var f in filters)
            {
                if (f.sharedMesh == null) continue;
                var mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3(
                        (i & 1) == 0 ? mb.min.x : mb.max.x,
                        (i & 2) == 0 ? mb.min.y : mb.max.y,
                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var p = root.InverseTransformPoint(f.transform.TransformPoint(c));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        static List<GameObject> LoadMeshSources(string sourceFolder)
        {
            var list = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(sourceFolder)) return list;
            // t:Prefab misses FBX. t:Model catches imported meshes. We want both.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab t:Model", new[] { sourceFolder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) list.Add(go);
            }
            return list;
        }

        // Greybox object name -> candidate prefab names, best first.
        // Several candidates means the A/B/C copies get DIFFERENT meshes instead of clones,
        // which is the cheapest visual variety available: it costs nothing to author.
        //
        // Targets are the house kit under Assets/_Project/Prefabs (GrandmaKit). The first
        // pass of this table pointed at BrokenVector/LowPolyDungeon, which is why Tutorial_01
        // read as a dungeon. Remapped 2026-09-17.
        static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>
        {
            { "sofa",       new[] { "PF_Sofa_01", "PF_Sofa_02", "PF_Armchair_01" } },
            { "table",      new[] { "PF_Dining_Table_01", "PF_Coffee_Table", "PF_Side_Table" } },
            { "chair",      new[] { "PF_Dining_Chair_01", "PF_Armchair_02", "PF_Rocking_Chair" } },
            { "box",        new[] { "PF_Crate", "PF_Suitcase", "PF_Trunk", "PF_Chest", "PF_Barrel" } },
            { "vase",       new[] { "PF_Vase_02", "PF_Vase_Plant", "PF_Jar_01" } },
            { "lamp",       new[] { "PF_Table_Lamp", "PF_Floor_Lamp", "PF_Oil_Lamp" } },
            { "plant",      new[] { "PF_Vase_Plant", "PF_Bush_01", "PF_Flower_01" } },
            // The house kit has the appliances the dungeon pack did not: 12 of 12 now resolve.
            { "television", new[] { "PF_TV_Old" } },
            { "fridge",     new[] { "PF_Fridge_Old" } },
        };

        static GameObject FindMatch(List<GameObject> meshes, string objectName)
        {
            var parts = objectName.Split('_');
            string key = parts[0].ToLowerInvariant();

            // "Box_A" -> variant 0, "Box_B" -> 1, "Box_C" -> 2
            int variant = 0;
            if (parts.Length > 1 && parts[1].Length == 1)
                variant = char.ToUpperInvariant(parts[1][0]) - 'A';
            if (variant < 0) variant = 0;

            string[] candidates;
            if (Map.TryGetValue(key, out candidates))
            {
                if (candidates.Length == 0) return null;
                for (int i = 0; i < candidates.Length; i++)
                {
                    string want = candidates[(variant + i) % candidates.Length];
                    var hit = meshes.Find(p => p.name == want);
                    if (hit != null) return hit;
                }
                return null;
            }

            GameObject best = null;
            foreach (var p in meshes)
            {
                string n = p.name.ToLowerInvariant();
                if (n == key) return p;
                if (best == null && n.Contains(key)) best = p;
            }
            return best;
        }
    }
}
