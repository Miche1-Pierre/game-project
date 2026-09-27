using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Movers
{
    // Puts a HudRoot in every gameplay scene that does not have one, at load, so the new HUD
    // needs no scene edit: Map01, Tutorial_01 and any test map get it the same way, and a
    // reload gets a fresh one. A gameplay scene is one with a player (PlayerController), a
    // crew member or a session; the main menu has none and is left alone.
    //
    // Enabled = false before a scene loads (a test, a capture without HUD) keeps the old
    // OnGUI HUD instead.
    public static class HudBootstrap
    {
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure(scene);

        public static HudRoot Ensure(Scene scene)
        {
            if (!Enabled || !scene.IsValid() || !scene.isLoaded) return null;
            var existing = Object.FindAnyObjectByType<HudRoot>();
            if (existing != null && existing.gameObject.scene == scene) return existing;
            if (!IsGameplay(scene)) return null;
            return Create(scene);
        }

        static bool IsGameplay(Scene scene)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null && player.gameObject.scene == scene) return true;
            var member = Object.FindAnyObjectByType<CrewMember>();
            if (member != null && member.gameObject.scene == scene) return true;
            var session = Object.FindAnyObjectByType<GameSession>();
            return session != null && session.gameObject.scene == scene;
        }

        public static HudRoot Create(Scene scene)
        {
            var go = new GameObject("HUD (LumaFlow)");
            go.SetActive(false);
            if (scene.IsValid() && scene.isLoaded && go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = HudPanel.Settings;
            doc.sortingOrder = 0f;
            var hud = go.AddComponent<HudRoot>();
            go.SetActive(true);
            return hud;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Enabled = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
