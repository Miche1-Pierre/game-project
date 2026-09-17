#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Movers
{
    // Turns the imported kit models (SM_*.fbx) into configured, drag-and-drop prefabs (PF_*.prefab):
    // a modern low-poly flat-colour material (Broken Vector style, no textures) + a mesh collider.
    // Run after changing the kit. Destructibility later = add one component to these prefabs.
    public static class KitPrefabBuilder
    {
        const string ArchFbx = "Assets/_Project/Art/Architecture";
        const string PropsFbx = "Assets/_Project/Art/Props";
        const string FurnFbx = "Assets/_Project/Art/Furniture";
        const string ArchPf = "Assets/_Project/Prefabs/Architecture";
        const string PropsPf = "Assets/_Project/Prefabs/Props";
        const string FurnPf = "Assets/_Project/Prefabs/Furniture";
        const string MatDir = "Assets/_Project/Materials";

        static Material mWall, mRoof, mFloor, mWood, mStone, mDoor, mFrame, mMetal, mDark, mFabric;

        [MenuItem("The Movers/Build Kit Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(ArchPf); Directory.CreateDirectory(PropsPf);
            Directory.CreateDirectory(FurnPf); Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();

            // Modern low-poly palette (flat colours, no textures) - cottage look
            mWall = Mat("MAT_Wall", new Color(0.90f, 0.87f, 0.79f));   // warm cream walls
            mRoof = Mat("MAT_Roof", new Color(0.64f, 0.33f, 0.26f));   // terracotta roof
            mFloor = Mat("MAT_Floor", new Color(0.80f, 0.68f, 0.52f)); // light wood floor
            mWood = Mat("MAT_Wood", new Color(0.52f, 0.38f, 0.26f));   // brown wood trim
            mStone = Mat("MAT_Stone", new Color(0.55f, 0.55f, 0.55f)); // grey stone
            mDoor = Mat("MAT_Door", new Color(0.28f, 0.42f, 0.55f));   // muted blue front door
            mFrame = Mat("MAT_Frame", new Color(0.95f, 0.95f, 0.93f)); // white window frame
            mMetal = Mat("MAT_Metal", new Color(0.80f, 0.82f, 0.85f)); // light grey metal (garage door / fridge)
            mDark = Mat("MAT_Dark", new Color(0.15f, 0.15f, 0.15f));   // TV
            mFabric = Mat("MAT_Fabric", new Color(0.30f, 0.40f, 0.70f)); // sofa

            int n = 0;
            n += Process(ArchFbx, ArchPf);
            n += Process(PropsFbx, PropsPf);
            n += Process(FurnFbx, FurnPf);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"The Movers: built {n} kit prefabs under Assets/_Project/Prefabs/ (modern flat-colour palette).");
        }

        static int Process(string fbxFolder, string pfFolder)
        {
            if (!AssetDatabase.IsValidFolder(fbxFolder)) return 0;
            var guids = AssetDatabase.FindAssets("t:Model", new[] { fbxFolder });
            int count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                string baseName = Path.GetFileNameWithoutExtension(path);
                string pfName = baseName.StartsWith("SM_") ? "PF_" + baseName.Substring(3) : "PF_" + baseName;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = pfName;
                var mat = PickMat(baseName);
                foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();

                PrefabUtility.SaveAsPrefabAsset(inst, $"{pfFolder}/{pfName}.prefab");
                Object.DestroyImmediate(inst);
                count++;
            }
            return count;
        }

        static Material PickMat(string name)
        {
            string n = name.ToLower();
            if (n.Contains("roof")) return mRoof;
            if (n.Contains("floor")) return mFloor;
            if (n.Contains("stairs")) return mFloor;
            if (n.Contains("wall")) return mWall;        // all wall variants (incl. _Door/_Window holes)
            if (n.Contains("window")) return mFrame;     // the SM_Window insert
            if (n.Contains("chimney")) return mWall;
            if (n.Contains("garagedoor")) return mMetal;
            if (n.Contains("rock")) return mStone;
            if (n.Contains("sofa")) return mFabric;
            if (n.Contains("fridge")) return mMetal;
            if (n.Contains("tv") || n.Contains("television")) return mDark;
            if (n.Contains("door")) return mDoor;        // SM_Door (front door)
            return mWood;                                 // fence, gate, ladder, table, bench, balcony, porch...
        }

        static Material Mat(string name, Color c)
        {
            string path = $"{MatDir}/{name}.mat";
            var ex = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (ex != null) { ex.color = c; if (ex.HasProperty("_BaseColor")) ex.SetColor("_BaseColor", c); return ex; }
            Shader sh = Shader.Find("Standard"); if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { color = c }; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            AssetDatabase.CreateAsset(m, path); return m;
        }
    }
}
#endif
