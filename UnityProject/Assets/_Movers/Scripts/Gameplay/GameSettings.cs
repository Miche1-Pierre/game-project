using System;
using UnityEngine;

namespace Movers
{
    public enum Language { French, English }

    // How two views share the screen. Side by side is the slice's layout (SplitScreen explains
    // why: the carry happens low in the view and the stairs need height). Stacked is offered
    // because some players prefer it on a wide screen; it costs the view's height.
    public enum SplitLayout { SideBySide, Stacked }

    // The player's settings, kept in PlayerPrefs so they survive a restart. One set for the
    // machine, not one per player: two people on one sofa rarely want to fight over a menu,
    // and the pause menu of either player edits the same values.
    //
    // Read by: the in-game HUD and pause menu (UICORE), the main menu (MENU), the look in
    // PlayerController and the truck's chase camera in VehicleSeat (sensitivity, invert Y,
    // through ApplyLook) and SplitScreen (layout). The volumes are not
    // here: they live in GameAudio, per AudioChannel, next to the sounds that use them.
    public static class GameSettings
    {
        const string KeySensitivity = "movers.look.sensitivity";
        const string KeyInvertY = "movers.look.invertY";
        const string KeyLanguage = "movers.language";
        const string KeyLayout = "movers.split.layout";
        const string KeyHints = "movers.hints.shown";

        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;

        static bool loaded;
        static float sensitivity = 1f;
        static bool invertY;
        static Language language = Language.French;
        static SplitLayout layout = SplitLayout.SideBySide;
        static bool hintsShown = true;

        // Raised after any value changed (and was saved). Listeners re-read what they need.
        public static event Action Changed;

        // A multiplier on the look speed, 1 = the tuned speed. The head and the truck camera:
        // the rotate mode of the carry keeps its own numbers (the carry feel is frozen,
        // playtest 001).
        public static float LookSensitivity
        {
            get { Load(); return sensitivity; }
            set { Load(); Set(ref sensitivity, Mathf.Clamp(value, MinSensitivity, MaxSensitivity), KeySensitivity); }
        }

        // A look input (mouse delta or right stick) as this player set it up: scaled by the
        // sensitivity, its vertical flipped when inverted. The head (PlayerController) and the
        // truck's chase camera (VehicleSeat) both go through it; at the defaults it returns the
        // input unchanged.
        public static Vector2 ApplyLook(Vector2 look)
        {
            float k = LookSensitivity;
            return new Vector2(look.x * k, look.y * k * (InvertY ? -1f : 1f));
        }

        public static bool InvertY
        {
            get { Load(); return invertY; }
            set { Load(); if (invertY == value) return; invertY = value; PlayerPrefs.SetInt(KeyInvertY, value ? 1 : 0); Save(); }
        }

        // French first: the team and the first players are French.
        public static Language Language
        {
            get { Load(); return language; }
            set { Load(); if (language == value) return; language = value; PlayerPrefs.SetInt(KeyLanguage, (int)value); Save(); }
        }

        public static SplitLayout Layout
        {
            get { Load(); return layout; }
            set { Load(); if (layout == value) return; layout = value; PlayerPrefs.SetInt(KeyLayout, (int)value); Save(); }
        }

        // The key hints next to the crosshair. On by default; a player who knows the game can
        // switch them off from the pause menu.
        public static bool HintsShown
        {
            get { Load(); return hintsShown; }
            set { Load(); if (hintsShown == value) return; hintsShown = value; PlayerPrefs.SetInt(KeyHints, value ? 1 : 0); Save(); }
        }

        static void Set(ref float field, float value, string key)
        {
            if (Mathf.Approximately(field, value)) return;
            field = value;
            PlayerPrefs.SetFloat(key, value);
            Save();
        }

        static void Save()
        {
            PlayerPrefs.Save();
            try { Changed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        // Lazily, on first read: PlayerPrefs may not be touched from a static constructor.
        static void Load()
        {
            if (loaded) return;
            loaded = true;
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 1f), MinSensitivity, MaxSensitivity);
            invertY = PlayerPrefs.GetInt(KeyInvertY, 0) != 0;
            language = (Language)Mathf.Clamp(PlayerPrefs.GetInt(KeyLanguage, (int)Language.French), 0, 1);
            layout = (SplitLayout)Mathf.Clamp(PlayerPrefs.GetInt(KeyLayout, (int)SplitLayout.SideBySide), 0, 1);
            hintsShown = PlayerPrefs.GetInt(KeyHints, 1) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }
    }
}
