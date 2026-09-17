#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Puts the cigarette in the hands of players who already exist.
    //
    // The scene builders add it to anything built from now on, but Tutorial_01 and the map
    // scenes were built and saved before the cigarette existed, and a saved scene does not
    // re-run its builder. This walks every scene under _Movers/Scenes, finds the players and
    // adds the two components, exactly like the visual swap does for meshes.
    //
    //   Unity.exe -batchmode -quit \
    //     -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversCigaretteCLI.RunInstall \
    //     -logFile cigarette.log
    //
    // Idempotent: a second run reports "already" and changes nothing.
    public static class MoversCigaretteCLI
    {
        const string ScenesFolder = "Assets/_Movers/Scenes";

        [MenuItem("The Movers/Install Cigarette in All Scenes")]
        public static void RunInstall()
        {
            // The open scene first, and saved before anything else is opened, so no work in
            // progress is lost when the loop below changes scenes under the editor.
            var openScene = EditorSceneManager.GetActiveScene();
            string openPath = openScene.path;
            int total = 0;

            if (!string.IsNullOrEmpty(openPath))
            {
                int n = InstallInOpenScene();
                total += n;
                // Saved when we changed it, and also when someone else had: the loop below
                // switches scenes, and unsaved work in the open one would go with it. A clean
                // scene we did not touch is left alone, so no scene file churns for nothing.
                if (n > 0 || openScene.isDirty) EditorSceneManager.SaveScene(openScene);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == openPath) continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = InstallInOpenScene();
                if (n > 0) EditorSceneManager.SaveScene(scene);
                total += n;
            }

            if (!string.IsNullOrEmpty(openPath)) EditorSceneManager.OpenScene(openPath, OpenSceneMode.Single);

            Debug.Log("[Cigarette] install done, " + total + " player(s) equipped.");
        }

        // Every player in the open scene gets a cigarette, and every player camera gets the
        // eyes that the smoke can blind. Returns how many players were touched.
        public static int InstallInOpenScene()
        {
            var players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            int touched = 0;

            foreach (var pc in players)
            {
                var go = pc.gameObject;
                Transform camT = pc.cam;
                if (camT == null)
                {
                    var c = go.GetComponentInChildren<Camera>();
                    if (c != null) camT = c.transform;
                }
                if (camT == null)
                {
                    Debug.LogWarning("[Cigarette] " + go.name + " has no camera, skipped.");
                    continue;
                }

                bool changed = false;

                if (camT.GetComponent<SmokeVision>() == null)
                {
                    Undo.AddComponent<SmokeVision>(camT.gameObject);
                    changed = true;
                }

                var cig = go.GetComponent<PlayerCigarette>();
                if (cig == null)
                {
                    cig = Undo.AddComponent<PlayerCigarette>(go);
                    changed = true;
                }
                // Wired explicitly even though Awake would find both, so the inspector shows
                // what is connected to what instead of a pair of empty slots.
                cig.cam = camT;
                cig.grab = go.GetComponent<PlayerGrab>();
                EditorUtility.SetDirty(cig);

                if (changed) touched++;
                Debug.Log("[Cigarette] " + (changed ? "equipped " : "already equipped ") + go.name
                          + " in " + EditorSceneManager.GetActiveScene().name);
            }

            if (touched > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return touched;
        }
    }
}
#endif
