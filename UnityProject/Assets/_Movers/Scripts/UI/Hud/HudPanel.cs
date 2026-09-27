using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Movers
{
    // The UI Toolkit panel the in-game HUD draws on, and the styles every screen of the game
    // starts from. The panel asset lives in Resources (Assets/_Movers/UI/Resources/
    // MoversHudPanel.asset, made by the integration recipe) so a HUD created at runtime finds
    // it; without it one is made in memory with the same settings.
    //
    // Scale with the screen's height against 1080 lines: a view keeps the same text size
    // whether it is full screen or half of a split, which is what "readable in split screen"
    // needs, and the layouts are designed for a view 960 units wide.
    public static class HudPanel
    {
        public const string PanelResource = "MoversHudPanel";
        public const string ThemeResource = "MoversPanelTheme";
        public const int SortingOrder = 10;       // over the game; the loading screen sits above (MENU)

        static PanelSettings runtimePanel;

        public static PanelSettings Settings
        {
            get
            {
                var asset = Resources.Load<PanelSettings>(PanelResource);
                if (asset != null) return asset;
                if (runtimePanel != null) return runtimePanel;
                runtimePanel = ScriptableObject.CreateInstance<PanelSettings>();
                runtimePanel.name = "MoversHudPanel (runtime)";
                runtimePanel.hideFlags = HideFlags.DontSave;
                Configure(runtimePanel);
                var theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
                if (theme == null)
                {
                    // An empty theme, like the asset's: the HUD styles everything it draws itself
                    // (a default theme would colour every label and break inheritance), and a
                    // panel with no theme at all logs a warning every time it is created.
                    theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                    theme.hideFlags = HideFlags.DontSave;
                }
                runtimePanel.themeStyleSheet = theme;
                return runtimePanel;
            }
        }

        // The settings the asset is made with too (the integration recipe calls this).
        public static void Configure(PanelSettings p)
        {
            p.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            p.referenceResolution = new Vector2Int(1920, 1080);
            p.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            p.match = 1f;
            p.sortingOrder = SortingOrder;
            p.clearColor = false;
        }

        // The base look of a root element: the theme's medium face, cream text with a soft
        // shadow (the HUD sits on whatever the camera sees), and no picking.
        public static void StyleRoot(VisualElement root)
        {
            var theme = UiTheme.Current;
            FontAsset face = theme.FontFor(UiWeight.Medium);
            if (face != null) root.style.unityFontDefinition = FontDefinition.FromSDFFont(face);
            else
            {
                // No Fredoka: Unity's built-in runtime font, so text still shows.
                var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacy != null) root.style.unityFontDefinition = FontDefinition.FromFont(legacy);
            }
            root.style.color = theme.cream;
            root.style.fontSize = theme.textBody;
            root.style.textShadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 0f, color = theme.shadow };
            root.pickingMode = PickingMode.Ignore;
            // The document's root covers the whole panel, so the layers under it can be laid
            // in panel units and the cards can centre on the screen.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            runtimePanel = null;
        }
    }
}
