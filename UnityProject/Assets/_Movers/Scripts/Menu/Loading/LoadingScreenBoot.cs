using UnityEngine;

namespace Movers
{
    // Puts up the loading screen whenever SceneFlow loads a scene (the menu's buttons, the
    // pause menu's "Retour au menu", the end screen's "Rejouer", the debug reset through
    // SceneFlow), and tells it when the new scene is there. No scene holds it: it listens from
    // the start of Play, after SceneFlow has cleared its events for the session.
    public static class LoadingScreenBoot
    {
        // False: no loading screen (a capture, a test that wants the raw load).
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneFlow.LoadingStarted -= OnStarted;
            SceneFlow.LoadingFinished -= OnFinished;
            SceneFlow.LoadingStarted += OnStarted;
            SceneFlow.LoadingFinished += OnFinished;
        }

        static void OnStarted(string scene)
        {
            if (!Enabled) return;
            LoadingScreen.Show(scene);
        }

        static void OnFinished(string scene)
        {
            if (LoadingScreen.Active != null) LoadingScreen.Active.Finish(scene);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Enabled = true;
        }
    }
}
