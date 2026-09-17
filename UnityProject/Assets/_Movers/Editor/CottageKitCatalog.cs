#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // Builds a browsable scene with every Cottage Kit prefab laid out in a grid, on the ground,
    // so the pack can be inspected and dragged from. Menu: The Movers/Open Cottage Kit Catalog.
    public static class CottageKitCatalog
    {
        const string PrefDir = "Assets/_Project/Prefabs/CottageKit";
        const string ScenePath = "Assets/_Movers/Scenes/CottageKit_Catalog.unity";

        [MenuItem("The Movers/Open Cottage Kit Catalog")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.62f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -40f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.localScale = new Vector3(12, 1, 8);
            ground.transform.position = new Vector3(15, -0.01f, 8);
            var gm = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.62f, 0.45f) };
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefDir });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            int cols = 7; float sx = 5f, sz = 6f;
            for (int i = 0; i < paths.Count; i++)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (src == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                int c = i % cols, r = i / cols;
                go.transform.position = new Vector3(c * sx, 0f, r * sz);
            }
            Directory.CreateDirectory("Assets/_Movers/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Cottage Kit catalog: placed {paths.Count} prefabs.");
        }
    }
}
#endif
