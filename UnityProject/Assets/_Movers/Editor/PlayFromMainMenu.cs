#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Movers
{
    // Play always starts on the title menu, whatever scene is open, like the built game.
    // The open scene stays open in the editor. Untick "The Movers/Play From Main Menu" to
    // press Play straight into the open scene (testing a map without the menu).
    [InitializeOnLoad]
    public static class PlayFromMainMenu
    {
        const string MenuPath = "The Movers/Play From Main Menu";
        const string PrefKey = "Movers.PlayFromMainMenu";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayFromMainMenu() { Apply(); }

        static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFlow.MenuScenePath)
                : null;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
#endif
