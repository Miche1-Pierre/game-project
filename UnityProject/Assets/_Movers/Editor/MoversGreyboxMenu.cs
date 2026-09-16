#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // One-click greybox: creates an empty scene with a GreyboxBootstrap and saves it.
    public static class MoversGreyboxMenu
    {
        [MenuItem("The Movers/Create Greybox Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("GreyboxBootstrap");
            go.AddComponent<GreyboxBootstrap>();

            Directory.CreateDirectory("Assets/_Movers/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/_Movers/Scenes/Tutorial_01.unity");
            EditorSceneManager.OpenScene("Assets/_Movers/Scenes/Tutorial_01.unity");
            Debug.Log("The Movers: Tutorial_01 created. Press Play.");
        }
    }
}
#endif
