using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // Moving between the title screen and the house, with a loading screen in between.
    // The one place that loads scenes, so the loading screen, the pause menu, the end screen
    // and the debug reset all go the same way. Scenes are loaded by name: both must be in the
    // Build Settings list (the integrator adds them). In the editor, a scene missing from the
    // list is loaded by path instead, so an unregistered scene still works while testing.
    public static class SceneFlow
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Map01_PierreKit_House";
        public const string MenuScenePath = "Assets/_Movers/Scenes/MainMenu.unity";
        public const string GameScenePath = "Assets/_Movers/Scenes/Map01_PierreKit_House.unity";

        // How many players the menu asked for. A game started directly in the editor (no menu)
        // reads 2, the slice's default.
        public static int RequestedPlayers { get; set; } = 2;

        public static bool IsLoading { get; private set; }
        public static float Progress { get; private set; }         // 0..1 while loading
        public static string LoadingTarget { get; private set; }

        public static event Action<string> LoadingStarted;          // scene name
        public static event Action<string> LoadingFinished;         // scene name

        // At least this long on the loading screen, so the running mover is seen at all.
        public static float MinimumLoadingSeconds = 1.5f;

        public static void LoadGame(int players)
        {
            RequestedPlayers = Mathf.Clamp(players, 1, 2);
            Load(GameScene, GameScenePath);
        }

        public static void LoadMenu() { Load(MenuScene, MenuScenePath); }

        public static void ReloadGame() { Load(GameScene, GameScenePath); }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Load(string name, string path)
        {
            if (IsLoading) return;
            Runner.Instance.StartCoroutine(LoadRoutine(name, path));
        }

        static IEnumerator LoadRoutine(string name, string path)
        {
            IsLoading = true;
            Progress = 0f;
            LoadingTarget = name;
            Time.timeScale = 1f;
            try { LoadingStarted?.Invoke(name); } catch (Exception e) { Debug.LogException(e); }
            float started = Time.unscaledTime;
            yield return null;   // one frame for the loading screen to appear

            AsyncOperation op = null;
            if (Application.CanStreamedLevelBeLoaded(name))
                op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
#if UNITY_EDITOR
            else
                op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
#endif
            if (op == null)
            {
                Debug.LogError("SceneFlow: scene '" + name + "' is not in the Build Settings.");
                IsLoading = false;
                yield break;
            }
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                Progress = Mathf.Clamp01(op.progress / 0.9f) * 0.95f;
                yield return null;
            }
            while (Time.unscaledTime - started < MinimumLoadingSeconds)
            {
                Progress = Mathf.Lerp(Progress, 1f, 0.1f);
                yield return null;
            }
            Progress = 1f;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            IsLoading = false;
            try { LoadingFinished?.Invoke(name); } catch (Exception e) { Debug.LogException(e); }
        }

        // Survives scene loads so the coroutine is not cut by the scene it unloads.
        sealed class Runner : MonoBehaviour
        {
            static Runner instance;
            public static Runner Instance
            {
                get
                {
                    if (instance == null)
                    {
                        var go = new GameObject("SceneFlow");
                        go.hideFlags = HideFlags.HideInHierarchy;
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        instance = go.AddComponent<Runner>();
                    }
                    return instance;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsLoading = false;
            Progress = 0f;
            LoadingTarget = null;
            LoadingStarted = null;
            LoadingFinished = null;
            RequestedPlayers = 2;
        }
    }
}
