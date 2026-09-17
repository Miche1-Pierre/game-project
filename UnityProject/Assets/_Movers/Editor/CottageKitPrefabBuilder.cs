#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Movers
{
    // Turns Blender-exported kit FBX into flat-colored, collider-ready drag-and-drop prefabs.
    // Scans Assets/_Project/Art/CottageKit and Assets/_Project/Art/GrandmaKit (recursively),
    // mirrors the folder tree into Assets/_Project/Prefabs, and assigns colors per submesh from
    // any *_mats.json found under those roots (slot order == Blender material order).
    public static class CottageKitPrefabBuilder
    {
        static readonly string[] Roots = {
            "Assets/_Project/Art/CottageKit",
            "Assets/_Project/Art/GrandmaKit",
        };
        const string MatDir = "Assets/_Project/Materials/CottageKit";

        static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            {"Cream",  new Color(0.92f,0.89f,0.80f)},
            {"White",  new Color(0.96f,0.95f,0.91f)},
            {"Roof",   new Color(0.49f,0.47f,0.50f)},
            {"Glass",  new Color(0.60f,0.78f,0.85f)},
            {"Wood",   new Color(0.50f,0.34f,0.21f)},
            {"WoodDark", new Color(0.34f,0.22f,0.14f)},
            {"Sage",   new Color(0.49f,0.56f,0.44f)},
            {"Stone",  new Color(0.60f,0.58f,0.54f)},
            {"Brick",  new Color(0.62f,0.40f,0.34f)},
            {"HedgeG", new Color(0.38f,0.48f,0.30f)},
            {"Metal",  new Color(0.50f,0.50f,0.53f)},
            {"Plank",  new Color(0.66f,0.50f,0.34f)},
            {"Dark",   new Color(0.16f,0.16f,0.18f)},
            {"FabRose",new Color(0.74f,0.52f,0.53f)},
            {"FabSage",new Color(0.55f,0.63f,0.50f)},
            {"FabCream",new Color(0.86f,0.80f,0.70f)},
            {"BookBlue",new Color(0.42f,0.50f,0.62f)},
            {"BookGold",new Color(0.70f,0.60f,0.35f)},
            {"Pot",    new Color(0.72f,0.42f,0.30f)},
            {"Mush",   new Color(0.78f,0.34f,0.30f)},
            {"FabMint",new Color(0.72f,0.83f,0.76f)},
            {"Bread",  new Color(0.80f,0.62f,0.40f)},
            {"Cheese", new Color(0.90f,0.80f,0.42f)},
            {"Wine",   new Color(0.24f,0.36f,0.26f)},
        };

        static Dictionary<string, Material> _cache;

        [MenuItem("The Movers/Build Kit Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(MatDir);
            _cache = new Dictionary<string, Material>();
            var map = LoadMaps();
            int n = 0;
            foreach (var root in Roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (src == null) continue;
                    string baseName = Path.GetFileNameWithoutExtension(path); // SM_X
                    string outPath = path.Replace("/Art/", "/Prefabs/")
                                         .Replace("SM_", "PF_").Replace(".fbx", ".prefab");
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    string[] slots = map.TryGetValue(baseName, out var s) ? s : null;
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    {
                        var arr = r.sharedMaterials;
                        for (int i = 0; i < arr.Length; i++)
                        {
                            string key = (slots != null && i < slots.Length) ? slots[i]
                                        : (arr[i] != null ? arr[i].name : null);
                            arr[i] = Mat(key);
                        }
                        r.sharedMaterials = arr;
                    }
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        if (mf.GetComponent<Collider>() == null)
                            mf.gameObject.AddComponent<MeshCollider>();
                    PrefabUtility.SaveAsPrefabAsset(go, outPath);
                    Object.DestroyImmediate(go);
                    n++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Kit prefabs: built {n} prefabs from {Roots.Length} roots.");
        }

        static Dictionary<string, string[]> LoadMaps()
        {
            var d = new Dictionary<string, string[]>();
            foreach (var root in Roots)
            {
                string abs = Path.Combine(Directory.GetCurrentDirectory(), root);
                if (!Directory.Exists(abs)) continue;
                foreach (var file in Directory.GetFiles(abs, "*_mats.json", SearchOption.AllDirectories))
                {
                    string txt = File.ReadAllText(file);
                    foreach (Match m in Regex.Matches(txt, "\"(SM_[^\"]+)\"\\s*:\\s*\\[([^\\]]*)\\]"))
                    {
                        var vals = new List<string>();
                        foreach (Match v in Regex.Matches(m.Groups[2].Value, "\"([^\"]+)\""))
                            vals.Add(v.Groups[1].Value);
                        d[m.Groups[1].Value] = vals.ToArray();
                    }
                }
            }
            return d;
        }

        static Material Mat(string key)
        {
            if (string.IsNullOrEmpty(key) || !Palette.ContainsKey(key)) key = "White";
            if (_cache.TryGetValue(key, out var ex)) return ex;
            string p = $"{MatDir}/MAT_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing != null) { _cache[key] = existing; return existing; }
            var mat = new Material(Shader.Find("Standard")) { color = Palette[key] };
            AssetDatabase.CreateAsset(mat, p);
            _cache[key] = mat;
            return mat;
        }
    }
}
#endif
