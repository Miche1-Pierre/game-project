#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // Places EVERY kit prefab (Assets/_Project/Prefabs, recursively) into one scene, grouped by
    // folder into rows, so the whole pack is visible/usable in Unity. Menu: The Movers/Open Kit Catalog.
    public static class CottageKitCatalog
    {
        const string PrefRoot = "Assets/_Project/Prefabs";
        const string ScenePath = "Assets/_Movers/Scenes/GrandmaKit_Catalog.unity";

        [MenuItem("The Movers/Open Kit Catalog")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.64f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -40f, 0f);

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefRoot });
            var byFolder = new SortedDictionary<string, List<string>>();
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var d = Path.GetFileName(Path.GetDirectoryName(p));
                if (!byFolder.ContainsKey(d)) byFolder[d] = new List<string>();
                byFolder[d].Add(p);
            }
            Bounds all = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
            float z = 0f; int total = 0;
            foreach (var kv in byFolder)
            {
                float x = 0f, rowDepth = 1.5f;
                foreach (var pth in kv.Value.OrderBy(s => s))
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>(pth);
                    if (src == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    var rends = go.GetComponentsInChildren<Renderer>();
                    float w = 1.2f;
                    if (rends.Length > 0)
                    {
                        var b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                        w = Mathf.Max(b.size.x, 0.4f); rowDepth = Mathf.Max(rowDepth, b.size.z);
                    }
                    go.transform.position = new Vector3(x + w / 2f, 0f, z);
                    x += w + 0.6f; total++;
                    var rr = go.GetComponentsInChildren<Renderer>();
                    foreach (var r in rr) { if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds); }
                }
                z += rowDepth + 2.2f;
            }
            // camera framing the whole field
            var camGO = new GameObject("CatalogCam");
            var cam = camGO.AddComponent<Camera>();
            Vector3 c = any ? all.center : Vector3.zero; float span = any ? Mathf.Max(all.size.x, all.size.z) : 20f;
            camGO.transform.position = c + new Vector3(0, span * 0.85f, -span * 0.55f);
            camGO.transform.LookAt(c);
            cam.tag = "MainCamera";

            Directory.CreateDirectory("Assets/_Movers/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Kit catalog: placed {total} prefabs in {byFolder.Count} rows -> {ScenePath}");
        }
    }
}
#endif
