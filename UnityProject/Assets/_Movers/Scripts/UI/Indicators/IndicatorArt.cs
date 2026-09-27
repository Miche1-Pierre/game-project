using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // The pictures and fonts of the crew indicators, as the scene hands them over. The sprites
    // are the UIART set in Assets/_Movers/UI/Sprites (fixed names, 2x density) and the font is
    // Fredoka in Assets/_Movers/UI/Fonts; neither folder is a Resources folder, so the references
    // live here, filled in the editor by integration/02_indicator_assets.cs.
    //
    // Every slot may be empty: the indicators then draw that piece themselves (IndicatorShape,
    // flat low-poly shapes in the palette), and text falls back to the theme's font. The game
    // runs the same before the art is delivered, only plainer.
    [CreateAssetMenu(menuName = "Movers/Indicator Art", fileName = "IndicatorArt")]
    public sealed class IndicatorArt : ScriptableObject
    {
        [Header("Indicator sprites")]
        public Sprite arrow;             // indicator_arrow (its direction: arrowPointsTo)
        public Sprite ring;              // indicator_ring
        public Sprite tapeBackground;    // compass_tape_bg, 9-sliced
        public Sprite tick;              // compass_tick

        [Header("Icons")]
        public Sprite truck;             // icon_truck
        public Sprite box;               // icon_box
        public Sprite key;               // icon_key
        public Sprite grandmaCalm;       // icon_grandma_calm
        public Sprite grandmaAnnoyed;    // icon_grandma_annoyed
        public Sprite grandmaAngry;      // icon_grandma_angry
        public Sprite grandmaFurious;    // icon_grandma_furious

        [Header("Fredoka")]
        public Font semiBold;            // distances and compass letters
        public Font bold;                // the other player's number

        [Header("Panel")]
        [Tooltip("The markers' own overlay panel (ConstantPixelSize, scale 1). Empty: one is made at Play with the theme below.")]
        public PanelSettings panelSettings;
        public ThemeStyleSheet theme;

        [Tooltip("Pixels of the source art per pixel of a 1080-high screen: the UIART sprites are drawn at 2x.")]
        public float artDensity = 2f;

        [Tooltip("Which way indicator_arrow points in its file, in screen degrees: 0 right, -90 up, 90 down, 180 left. The indicators turn it to point right first.")]
        public float arrowPointsTo = 0f;

        public Sprite Icon(IndicatorIcon icon)
        {
            switch (icon)
            {
                case IndicatorIcon.GrandmaCalm: return grandmaCalm;
                case IndicatorIcon.GrandmaAnnoyed: return grandmaAnnoyed;
                case IndicatorIcon.GrandmaAngry: return grandmaAngry;
                case IndicatorIcon.GrandmaFurious: return grandmaFurious;
                case IndicatorIcon.Truck: return truck;
                case IndicatorIcon.Box: return box;
                case IndicatorIcon.Key: return key;
                default: return null;
            }
        }

        // How many sprites of the set are there: the integration snippet prints it, a test reads it.
        public int SpriteCount
        {
            get
            {
                int n = 0;
                if (arrow != null) n++;
                if (ring != null) n++;
                if (tapeBackground != null) n++;
                if (tick != null) n++;
                if (truck != null) n++;
                if (box != null) n++;
                if (key != null) n++;
                if (grandmaCalm != null) n++;
                if (grandmaAnnoyed != null) n++;
                if (grandmaAngry != null) n++;
                if (grandmaFurious != null) n++;
                return n;
            }
        }

        public const int SpriteSlots = 11;
    }
}
