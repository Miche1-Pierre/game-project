using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Batch-mode entry points, so the swap can run without a human clicking the window.
    //
    //   Unity.exe -batchmode -quit -projectPath <path> \
    //             -executeMethod Movers.EditorTools.MoversVisualSwapCLI.RunSwap
    //
    // The heavy lifting lives in MoversVisualSwap. This file only opens the scene,
    // drives the same code path, and saves.
    public static class MoversVisualSwapCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Tutorial_01.unity";

        public static void RunSwap()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[VisualSwap] opened scene: " + scene.name);

            var log = MoversVisualSwap.Swap(MoversVisualSwap.DefaultSourceFolder, false);
            foreach (var l in log) Debug.Log("[VisualSwap] " + l);

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene);
            Debug.Log("[VisualSwap] " + (ok ? "scene saved" : "SCENE SAVE FAILED"));
        }

        public static void RunRestore()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = MoversVisualSwap.Restore();
            foreach (var l in log) Debug.Log("[VisualSwap] " + l);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[VisualSwap] restore done");
        }
    }
}
