using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // The clean reset: load the scene again. Resetting field by field cannot work here, because
    // the house rebuilds itself at Play (panes, casements, hinges, debris) and the destruction
    // leaves things a hand-written reset forgets (A5b 4.2).
    public static class SceneReload
    {
        static int requestedFrame = -1;

        public static void Reload()
        {
            // Online (NETCODE_SLICE 3.6): the host reloads both machines through one funnel; the
            // client never reloads on its own (its E reaches the host as input).
            if (Net.IsOnline)
            {
                if (Net.IsHost) NetSession.ReloadForBoth();
                return;
            }
            // F5 and E on the end screen can land on the same frame.
            if (requestedFrame == Time.frameCount) return;
            requestedFrame = Time.frameCount;

            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // The scene is not in Build Settings (m_Scenes is empty), so SceneManager.LoadScene
            // cannot find it. The editor can load any scene asset by path in Play mode. It loads
            // the saved file: unsaved edits made before pressing Play are not in the reload.
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("SceneReload: the active scene was never saved, nothing to reload.");
                return;
            }
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else Debug.LogWarning("SceneReload: " + scene.name + " is not in the build, it cannot be reloaded.");
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            requestedFrame = -1;
        }
    }
}
