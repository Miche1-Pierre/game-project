using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Movers.EditorTools
{
    // Clears the "MaterialLocation.External is obsolete" errors the dungeon pack raises.
    //
    // Broken Vector authored its 258 models with External material location, which Unity 6
    // deprecated. The modern equivalent is InPrefab plus an explicit remap pointing each
    // embedded material slot at the .mat asset that already exists in the project.
    //
    // Two passes are needed and that is not avoidable: the embedded materials only exist as
    // sub-assets AFTER the model has been reimported with InPrefab, and their names are what
    // the remap keys on.
    public static class MoversMaterialFixCLI
    {
        static readonly string[] Roots = { "Assets/BrokenVector", "Assets/_Movers/Generated" };

        [MenuItem("The Movers/Fix Deprecated Material Location")]
        public static void Fix()
        {
            var models = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", Roots))
                models.Add(AssetDatabase.GUIDToAssetPath(guid));

            Debug.Log("[MaterialFix] " + models.Count + " models to inspect");

            // Pass 1: move every model off the deprecated location.
            int moved = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in models)
                {
                    var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (imp == null) continue;
#pragma warning disable CS0618
                    bool isExternal = imp.materialLocation == ModelImporterMaterialLocation.External;
#pragma warning restore CS0618
                    if (!isExternal) continue;
                    imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    imp.SaveAndReimport();
                    moved++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log("[MaterialFix] pass 1: moved " + moved + " models to InPrefab");

            // Build a name -> .mat lookup once.
            var byName = new Dictionary<string, Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", Roots))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null && !byName.ContainsKey(m.name)) byName[m.name] = m;
            }
            Debug.Log("[MaterialFix] " + byName.Count + " project materials available for remapping");

            // Pass 2: point each embedded slot back at the shared material.
            int remapped = 0, slots = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in models)
                {
                    var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (imp == null) continue;

                    bool changed = false;
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        var mat = sub as Material;
                        if (mat == null) continue;
                        slots++;
                        Material shared;
                        if (!byName.TryGetValue(mat.name, out shared)) continue;
                        if (shared == mat) continue;   // already the shared one
                        imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name), shared);
                        changed = true;
                    }
                    if (changed) { imp.SaveAndReimport(); remapped++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();

            Debug.Log("[MaterialFix] pass 2: remapped " + remapped + " models across " + slots + " material slots");
            Debug.Log("[MaterialFix] done");
        }
    }
}
