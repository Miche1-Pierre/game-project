using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Movers.EditorTools
{
    // Swaps the greybox primitive LOOK for a real mesh, without touching the PHYSICS.
    //
    // Why it works this way: the greybox tunes weight, mass and collisions on scaled primitive
    // cubes. Replacing those cubes with imported meshes would change the collider shape and
    // silently retune the whole feel. So we keep the cube (collider + Rigidbody + MovableObject),
    // just hide its renderer and parent a visual mesh inside it, fitted to the same volume.
    //
    // Consequence: the pack can be removed at any time and the greybox still plays identically.
    public class MoversVisualSwap : EditorWindow
    {
        string folder = "Assets";
        bool preserveAspect = true;
        Vector2 scroll;
        readonly List<string> log = new List<string>();

        [MenuItem("The Movers/Visual Swap (keep physics)")]
        static void Open() => GetWindow<MoversVisualSwap>("Visual Swap");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Hides the primitive renderer and parents a matching prefab inside each MovableObject.\n" +
                "Colliders, Rigidbody and MovableObject values are never modified.\n" +
                "Matching is by name: a MovableObject called \"Sofa\" looks for a prefab whose name contains \"Sofa\".",
                MessageType.Info);

            folder = EditorGUILayout.TextField("Source folder", folder);
            EditorGUILayout.LabelField("Generated variants live in Assets/_Movers/Generated", EditorStyles.miniLabel);
            preserveAspect = EditorGUILayout.Toggle("Preserve aspect ratio", preserveAspect);

            EditorGUILayout.Space();
            if (GUILayout.Button("Swap visuals in open scene")) Swap();
            if (GUILayout.Button("Restore greybox primitives")) Restore();

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var line in log) EditorGUILayout.LabelField(line);
            EditorGUILayout.EndScrollView();
        }

        const string VisualName = "__visual";

        void Swap()
        {
            log.Clear();
            var prefabs = LoadPrefabs(folder);
            if (prefabs.Count == 0)
            {
                log.Add($"No mesh found under {folder}. Import the pack first.");
                return;
            }

            var movables = Object.FindObjectsByType<MovableObject>(FindObjectsSortMode.None);
            int done = 0;

            foreach (var mo in movables)
            {
                var match = FindMatch(prefabs, mo.name);
                if (match == null) { log.Add($"- {mo.name}: no prefab match"); continue; }

                var old = mo.transform.Find(VisualName);
                if (old != null) DestroyImmediate(old.gameObject);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(match, mo.transform);
                visual.name = VisualName;
                Fit(visual.transform, preserveAspect);

                var r = mo.GetComponent<Renderer>();
                if (r != null) r.enabled = false;

                log.Add($"+ {mo.name}: {match.name}");
                done++;
            }

            log.Add($"Swapped {done} of {movables.Length}. Physics untouched.");
            MarkSceneDirty();
        }

        void Restore()
        {
            log.Clear();
            var movables = Object.FindObjectsByType<MovableObject>(FindObjectsSortMode.None);
            foreach (var mo in movables)
            {
                var v = mo.transform.Find(VisualName);
                if (v != null) DestroyImmediate(v.gameObject);
                var r = mo.GetComponent<Renderer>();
                if (r != null) r.enabled = true;
            }
            log.Add($"Restored {movables.Length} primitives.");
            MarkSceneDirty();
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

            Vector3 s = new Vector3(1f / bounds.size.x, 1f / bounds.size.y, 1f / bounds.size.z);
            if (preserveAspect)
            {
                float uniform = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                s = new Vector3(uniform, uniform, uniform);
            }
            visual.localScale = s;
            visual.localPosition = -Vector3.Scale(bounds.center, s);
        }

        static Bounds LocalBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

            var b = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                b.Encapsulate(root.InverseTransformPoint(r.bounds.min));
                b.Encapsulate(root.InverseTransformPoint(r.bounds.max));
            }
            return b;
        }

        static List<GameObject> LoadPrefabs(string folder)
        {
            var list = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            // t:Prefab misses FBX. t:Model catches imported meshes. We want both.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab t:Model", new[] { folder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) list.Add(go);
            }
            return list;
        }

        // Greybox object name -> candidate mesh names, best first.
        // Several candidates means the A/B/C copies get DIFFERENT meshes instead of clones,
        // which is the cheapest visual variety available: it costs nothing to author.
        static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>
        {
            { "sofa",       new[] { "Bench" } },
            { "table",      new[] { "Table_Big", "Table_Small", "Desk" } },
            { "chair",      new[] { "Chair", "Stool_round", "Stool_square" } },
            { "box",        new[] { "Chest", "ChestSmall", "Barrel_Closed", "Chest_Worn", "Chest_Wide" } },
            { "vase",       new[] { "Amphora", "Jar_Big", "Jug" } },
            { "lamp",       new[] { "Candlestick", "Candlestick_Triple", "Chandelier" } },
            { "plant",      new[] { "Plant_Root", "Ivy_Branch" } },
            // No modern appliance in a dungeon pack. These stay grey until we kitbash them.
            { "television", new string[0] },
            { "fridge",     new string[0] },
        };

        static GameObject FindMatch(List<GameObject> prefabs, string objectName)
        {
            string key = objectName.Split('_')[0].ToLowerInvariant();
            // "Box_A" -> variant 0, "Box_B" -> 1, "Box_C" -> 2
            int variant = 0;
            var parts = objectName.Split('_');
            if (parts.Length > 1 && parts[1].Length == 1)
                variant = char.ToUpperInvariant(parts[1][0]) - 'A';
            if (variant < 0) variant = 0;

            if (Map.TryGetValue(key, out var candidates))
            {
                if (candidates.Length == 0) return null;
                for (int i = 0; i < candidates.Length; i++)
                {
                    string want = candidates[(variant + i) % candidates.Length];
                    var hit = prefabs.Find(p => p.name == want);
                    if (hit != null) return hit;
                }
                return null;
            }

            GameObject best = null;
            foreach (var p in prefabs)
            {
                string n = p.name.ToLowerInvariant();
                if (n == key) return p;
                if (best == null && n.Contains(key)) best = p;
            }
            return best;
        }

        static void MarkSceneDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
