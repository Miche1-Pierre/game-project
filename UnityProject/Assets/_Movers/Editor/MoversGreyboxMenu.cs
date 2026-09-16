#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // One-click greybox: creates a scene, builds the world as real (visible) objects, and saves it.
    public static class MoversGreyboxMenu
    {
        [MenuItem("The Movers/Create Greybox Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("GreyboxBootstrap");
            go.AddComponent<GreyboxBootstrap>().BuildEditor(); // build now so it is visible before Play

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Assets/_Movers/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Movers/Scenes/Tutorial_01.unity");

            Debug.Log("The Movers: Tutorial_01 built. You should see the house in the Scene view now. Press Play to walk around (Game view).");
        }
    }
}
#endif
