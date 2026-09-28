using UnityEngine;

namespace Movers
{
    // Every number of the crew indicators (compass tape, edge arrows, rings) in one place. First
    // guesses, like every number of the slice: a playtest note maps to one edit here.
    // Sizes are pixels for a view 1080 pixels high and scale with the view's height (the HUD's
    // rule, ViewportGUI.FontSize), so a half-width split view keeps full-size markers.
    [System.Serializable]
    public sealed class IndicatorSettings
    {
        [Header("Compass tape (top of each view)")]
        public bool showTape = true;
        [Tooltip("Degrees of heading the tape spans, centred on where the player looks.")]
        public float tapeArcDegrees = 180f;
        [Tooltip("Preferred share of the view width. The tape never reaches under the HUD's top corner panels (the contract card, the grandmother's panel): it is narrower when they leave less room (keepOutOfHud).")]
        [Range(0.2f, 0.8f)] public float tapeWidthShare = 0.36f;
        public float tapeMinWidth = 220f;
        public float tapeMaxWidth = 560f;
        [Tooltip("The narrowest the tape gets when the HUD's top panels leave less room (a narrow split view); below it its ends go under them.")]
        public float tapeFloorWidth = 150f;
        public float tapeHeight = 34f;
        public float tapeTop = 10f;
        public float tapeTokenSize = 26f;
        [Tooltip("A target this many metres above or below the player gets a little up or down caret (another floor).")]
        public float floorCaretHeight = 2.2f;

        [Header("Edge arrows (targets out of view)")]
        public float edgeTokenSize = 40f;
        public float arrowSize = 24f;
        [Tooltip("Closer than this, no arrow: he is right behind you, you know.")]
        public float edgeMinDistance = 3f;
        [Tooltip("Share of the view width kept free at the left and right of the arrows.")]
        [Range(0f, 0.3f)] public float edgeSideInset = 0.05f;
        [Tooltip("Share of the view height kept free above the arrows (the tape and the top of the view). The HUD's corner panels are kept free by measuring them (keepOutOfHud).")]
        [Range(0f, 0.4f)] public float edgeTopInset = 0.17f;
        [Tooltip("Share of the view height kept free below the arrows (prompt, hints, pockets).")]
        [Range(0f, 0.4f)] public float edgeBottomInset = 0.2f;

        [Header("Keeping clear of the HUD")]
        [Tooltip("Measure UICORE's HUD corner panels (contract card, grandmother, toasts, controls, pockets; HudCorners) and keep the tape and every token out from under them: the HUD draws over the markers. Off: the shares above only.")]
        public bool keepOutOfHud = true;
        [Tooltip("Pixels (at 1080) left between a HUD panel and a marker, arrow included.")]
        public float hudGap = 8f;

        [Header("Rings (targets in view, walls included)")]
        public float ringTokenSize = 32f;
        [Tooltip("Closer than this, no ring: the body is enough.")]
        public float ringMinDistance = 4f;
        [Tooltip("The grandmother's ring from this far only: closer, her speech bubble sits over her head.")]
        public float grandmaRingMinDistance = 7f;
        [Tooltip("A ring over the delivery board once everything is loaded. Off: the HUD's own \"Deliver here\" sign over the board does it (UICORE, WorldLabelsView), the edge arrow still leads there.")]
        public bool deliverRingInView = false;
        [Tooltip("Farther than this, no ring: the tape says where, the screen stays clean.")]
        public float ringMaxDistance = 40f;
        [Tooltip("Share of the view height around the crosshair where rings fade out, so they never cover what you aim at.")]
        [Range(0f, 0.5f)] public float ringCrosshairFade = 0.09f;

        [Header("Who shows")]
        public GrandmaReveal grandmaOnTape = GrandmaReveal.Always;
        public GrandmaReveal grandmaMarkers = GrandmaReveal.Always;
        [Tooltip("The delivery board gets an edge arrow while you carry something (where it goes). Always once everything is loaded.")]
        public bool deliverArrowWhenCarrying = true;
        [Tooltip("The exit of the police flee, from the call to the end of the run: a ring or an edge arrow at any distance, for the driver and the passenger too.")]
        public bool showExit = true;
        [Tooltip("The dispatched police cars.")]
        public bool showPolice = true;
        [Tooltip("A police car farther than this gets no ring or edge arrow, only its token on the tape.")]
        public float policeMarkerMaxDistance = 150f;

        [Header("Text (Fredoka)")]
        public float distanceFontSize = 17f;
        public float numberFontSize = 18f;
        public float letterFontSize = 17f;

        [Header("Look")]
        [Tooltip("The markers' UI Toolkit panel draws under panels with a higher order (the HUD, the menus).")]
        public float panelSortingOrder = -5f;
        [Tooltip("The ring sprite is a frame over a disc of the target's colour. Off: the ring sprite itself is tinted and no disc is drawn.")]
        public bool ringIsFrame = true;
        [Tooltip("Tint the arrow sprite with the target's colour (right for light art).")]
        public bool tintArrow = true;
        public Color wood = new Color32(0x9A, 0x66, 0x3C, 0xFF);
        public Color woodLight = new Color32(0xC8, 0x8F, 0x58, 0xFF);
        public Color woodDark = new Color32(0x3E, 0x28, 0x1A, 0xFF);
        public Color cream = new Color32(0xFF, 0xF3, 0xDC, 0xFF);
        public Color north = new Color32(0xE8, 0x6A, 0x4A, 0xFF);
        public Color truck = new Color32(0xF1, 0xE3, 0xC2, 0xFF);
        public Color deliver = new Color32(0xF2, 0xB8, 0x3B, 0xFF);
        public Color keys = new Color32(0xF6, 0xCD, 0x4C, 0xFF);
        [Tooltip("The exit of the police flee.")]
        public Color exit = new Color32(0x6F, 0xC8, 0x5A, 0xFF);
        [Tooltip("The police cars.")]
        public Color police = new Color32(0x3E, 0x7B, 0xE0, 0xFF);
        [Tooltip("The grandmother's colour by mood: Sweet, Annoyed, Angry, Furious, Police (MoodTier order).")]
        public Color[] mood =
        {
            new Color32(0x8D, 0xB8, 0x6A, 0xFF), new Color32(0xE6, 0xBE, 0x4A, 0xFF), new Color32(0xE5, 0x81, 0x3A, 0xFF),
            new Color32(0xCF, 0x4A, 0x3A, 0xFF), new Color32(0x8E, 0x2A, 0x2A, 0xFF),
        };

        public Color MoodColor(MoodTier tier)
        {
            int i = (int)tier;
            return mood != null && i >= 0 && i < mood.Length ? mood[i] : cream;
        }

        public static readonly IndicatorSettings Defaults = new IndicatorSettings();
    }

    // The optional asset (Assets/_Movers/Data/IndicatorTuning.asset). Without one, the code
    // defaults above, so the scene never breaks for want of it.
    [CreateAssetMenu(menuName = "Movers/Indicator Tuning", fileName = "IndicatorTuning")]
    public sealed class IndicatorTuning : ScriptableObject
    {
        public IndicatorSettings settings = new IndicatorSettings();
    }
}
