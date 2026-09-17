using UnityEditor;
using UnityEngine;

namespace Movers.EditorTools
{
    // Generated FBX arrive with two defects: material stubs with no texture, and a unit
    // scale Unity guesses wrong. Both are fixed here, at import time, by measurement
    // rather than by trusting the file's declared units.
    public static class MoversFixGeneratedCLI
    {
        const string Folder = "Assets/_Movers/Generated";
        // The source these variants were derived from. Their real-world size must match it.
        const string Reference = "Assets/BrokenVector/LowPolyDungeon/Models/Furniture/Chest.fbx";

        public static void Remap()
        {
            float refSize = LongestEdge(Reference);
            Debug.Log("[FixGenerated] reference longest edge = " + refSize.ToString("F4"));

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.materialLocation = ModelImporterMaterialLocation.External;
                imp.materialSearch = ModelImporterMaterialSearch.Everywhere;

                // Unit correction by measurement. The variants are proportional edits of the
                // reference, so their longest edge should land within a factor of ~2 of it.
                // Anything far outside that is a unit conversion error, not a design choice.
                imp.SaveAndReimport();

                // Converge on the right unit scale by measurement. Unity's guess about FBX
                // units cannot be trusted here, and setting globalScale changes what the next
                // measurement returns, so this iterates instead of computing once.
                for (int pass = 0; pass < 4; pass++)
                {
                    float cur = LongestEdge(path);
                    if (cur <= 0f || refSize <= 0f) break;
                    float ratio = refSize / cur;
                    if (ratio < 1.5f && ratio > 0.67f) break;   // close enough

                    imp.useFileScale = false;
                    imp.globalScale = imp.globalScale * ratio;
                    imp.SaveAndReimport();
                    Debug.Log("[FixGenerated] " + path + " pass " + pass + ": off by " +
                              ratio.ToString("F2") + "x, globalScale -> " + imp.globalScale.ToString("F4"));
                }
                Debug.Log("[FixGenerated] remapped " + path + " longest edge now " +
                          LongestEdge(path).ToString("F4"));
            }
            AssetDatabase.Refresh();
            Debug.Log("[FixGenerated] done");
        }

        static float LongestEdge(string assetPath)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go == null) return 0f;
            float best = 0f;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var s = mf.sharedMesh.bounds.size;
                best = Mathf.Max(best, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
            }
            return best;
        }
    }
}
