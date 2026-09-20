#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Puts the crew's smokes and beer by the truck, in scenes that were saved before they were
    // objects.
    //
    // It also cleans up after the version that came first. Until ADR-007 the two items were
    // components on the player, `PlayerCigarette` and `PlayerBeer`, and those scripts are gone
    // now: every scene that was equipped the old way carries two component entries with no
    // script behind them. Unity draws those as a yellow warning and keeps them forever, so
    // they are removed here rather than left for someone to find.
    //
    //   Unity.exe -batchmode -quit \
    //     -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversStartingInventoryCLI.RunInstall \
    //     -logFile inventory.log
    //
    // Idempotent, and it refuses to run while the game is playing, because a scene edited in
    // Play mode is thrown away when Play stops.
    public static class MoversStartingInventoryCLI
    {
        const string ScenesFolder = "Assets/_Movers/Scenes";
        const string CigaretteSpawn = "Spawn_Cigarette";
        const string BeerSpawn = "Spawn_Beer";

        [MenuItem("The Movers/Install Starting Inventory in All Scenes")]
        public static void RunInstall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Inventory] the game is running. Stop Play and run this again.");
                return;
            }

            var openScene = EditorSceneManager.GetActiveScene();
            string openPath = openScene.path;
            int total = 0;

            if (!string.IsNullOrEmpty(openPath))
            {
                int n = InstallInOpenScene();
                total += n;
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

            Debug.Log("[Inventory] pass done, " + total + " scene change(s).");
        }

        // Returns how many things it changed in the open scene.
        public static int InstallInOpenScene()
        {
            string sceneName = EditorSceneManager.GetActiveScene().name;
            int changed = 0;

            // 1. the player keeps the two things that are states rather than objects
            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = pc.gameObject;

                int stale = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (stale > 0)
                {
                    changed += stale;
                    Debug.Log("[Inventory] removed " + stale + " dead component(s) from " + go.name + " in " + sceneName);
                }

                if (go.GetComponent<Drunkenness>() == null)
                {
                    Undo.AddComponent<Drunkenness>(go);
                    changed++;
                    Debug.Log("[Inventory] gave " + go.name + " a head that beer can turn, in " + sceneName);
                }

                Transform camT = pc.cam;
                if (camT == null)
                {
                    var c = go.GetComponentInChildren<Camera>();
                    if (c != null) camT = c.transform;
                }
                if (camT != null && camT.GetComponent<SmokeVision>() == null)
                {
                    Undo.AddComponent<SmokeVision>(camT.gameObject);
                    changed++;
                    Debug.Log("[Inventory] gave " + go.name + " eyes that smoke can blind, in " + sceneName);
                }
            }

            // 2. the items themselves, on the ground at the tailgate
            var truck = Object.FindFirstObjectByType<TruckCargo>();
            if (truck == null)
            {
                Debug.Log("[Inventory] " + sceneName + " has no truck, so it gets no starting items.");
                if (changed > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                return changed;
            }

            var t = truck.transform;
            changed += EnsureSpawner(CigaretteSpawn, StartingItemSpawner.Kind.Cigarette,
                                     t.position + t.right * -1.6f + t.forward * 1.8f, sceneName);
            changed += EnsureSpawner(BeerSpawn, StartingItemSpawner.Kind.Beer,
                                     t.position + t.right * -2.2f + t.forward * 1.8f, sceneName);

            if (changed > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return changed;
        }

        // The height does not matter: the spawner lays the item on whatever floor is under it.
        static int EnsureSpawner(string name, StartingItemSpawner.Kind kind, Vector3 at, string sceneName)
        {
            foreach (var s in Object.FindObjectsByType<StartingItemSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.kind == kind) return 0;

            var go = new GameObject(name);
            go.transform.position = at;
            go.AddComponent<StartingItemSpawner>().kind = kind;
            Undo.RegisterCreatedObjectUndo(go, "add starting item spawner");
            Debug.Log("[Inventory] put " + kind + " by the truck in " + sceneName + " at " + at.ToString("F1"));
            return 1;
        }
    }
}
#endif
