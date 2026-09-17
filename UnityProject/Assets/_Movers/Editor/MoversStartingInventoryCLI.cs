#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Puts the starting inventory in the hands of players who already exist.
    //
    // The scene builders equip anything built from now on, but the scenes were saved before
    // these items existed and a saved scene does not re-run its builder. This walks every
    // scene under _Movers/Scenes, finds the players, and adds what is missing: the cigarette
    // and the beer in their hands, the eyes that smoke can blind, the head that beer can turn.
    //
    //   Unity.exe -batchmode -quit \
    //     -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversStartingInventoryCLI.RunInstall \
    //     -logFile inventory.log
    //
    // Idempotent: a second run reports "already" and changes nothing. It was called
    // MoversCigaretteCLI until the beer arrived and made the name a lie.
    public static class MoversStartingInventoryCLI
    {
        const string ScenesFolder = "Assets/_Movers/Scenes";

        [MenuItem("The Movers/Install Starting Inventory in All Scenes")]
        public static void RunInstall()
        {
            // Not while the game is running. A scene edited in Play mode is thrown away when
            // Play stops, so this would look like it worked and change nothing, and the save
            // at the end throws. Found the hard way, with someone playing in the other window.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Inventory] the game is running. Stop Play and run this again.");
                return;
            }

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

            Debug.Log("[Inventory] install done, " + total + " player(s) equipped.");
        }

        // Every player in the open scene gets the inventory, and every player camera gets the
        // eyes that the smoke can blind. Returns how many players were touched.
        public static int InstallInOpenScene()
        {
            var players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                    Debug.LogWarning("[Inventory] " + go.name + " has no camera, skipped.");
                    continue;
                }

                bool changed = false;

                if (camT.GetComponent<SmokeVision>() == null)
                {
                    Undo.AddComponent<SmokeVision>(camT.gameObject);
                    changed = true;
                }

                var drunk = go.GetComponent<Drunkenness>();
                if (drunk == null) { drunk = Undo.AddComponent<Drunkenness>(go); changed = true; }

                var cig = go.GetComponent<PlayerCigarette>();
                if (cig == null) { cig = Undo.AddComponent<PlayerCigarette>(go); changed = true; }

                var beer = go.GetComponent<PlayerBeer>();
                if (beer == null) { beer = Undo.AddComponent<PlayerBeer>(go); changed = true; }

                // Wired explicitly even though Awake would find all of it, so the inspector
                // shows what is connected to what instead of a column of empty slots.
                var pg = go.GetComponent<PlayerGrab>();
                cig.cam = camT;
                cig.grab = pg;
                beer.cam = camT;
                beer.grab = pg;
                beer.drunk = drunk;
                EditorUtility.SetDirty(cig);
                EditorUtility.SetDirty(beer);

                if (changed) touched++;
                Debug.Log("[Inventory] " + (changed ? "equipped " : "already equipped ") + go.name
                          + " in " + EditorSceneManager.GetActiveScene().name);
            }

            if (touched > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return touched;
        }
    }
}
#endif
