#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // Lays the key Broken Vector kit prefabs in a row and logs their real sizes, so the map can be
    // assembled from existing assets (reuse) instead of remodelled.
    public static class KitInspector
    {
        static readonly string[] paths = {
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/Dungeon_Wall_Var1.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/Dungeon_Wall_Window.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/Dungeon_Big_Wall.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/FloorTIle.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/Staircase.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/Door_Wooden_Round_Left.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Tiles/Balcony_Railing.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Furniture/Table_Big.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Furniture/Bench.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Furniture/Barrel_Big.prefab",
            "Assets/BrokenVector/LowPolyStoragePack/Prefabs/Crate_01.prefab",
        };

        [MenuItem("The Movers/Inspect Kit")]
        public static void Inspect()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var sb = new StringBuilder("KIT BOUNDS:\n");
            float x = 0f;
            foreach (var p in paths)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf == null) { sb.AppendLine("MISSING " + p); continue; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                g.transform.position = new Vector3(x, 0f, 0f);
                var rs = g.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    sb.AppendLine($"{pf.name}: size=({b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2})  baseOffset={(b.min.y):F2}");
                }
                else sb.AppendLine(pf.name + ": (no renderer)");
                x += 5f;
            }
            Debug.Log(sb.ToString());
        }
    }
}
#endif
