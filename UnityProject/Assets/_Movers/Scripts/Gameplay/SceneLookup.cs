using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // FindAnyObjectByType, limited to one scene. During the debug reset the finished scene and
    // the reloaded one can both be loaded for a moment (the new one wakes before the old one is
    // destroyed), and a plain search could hand the new run the old run's contract or board.
    // Allocates an array: for Awake and Start, never per frame.
    public static class SceneLookup
    {
        public static T Find<T>(Scene scene) where T : Component
        {
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.scene == scene) return all[i];
            return null;
        }
    }
}
