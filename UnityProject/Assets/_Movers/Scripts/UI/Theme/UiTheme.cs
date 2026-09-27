using System;
using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using UnityEngine.TextCore.Text;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;
using LumaTextStyle = LumaFlow.TextStyle;

namespace Movers
{
    public enum UiWeight { Regular, Medium, SemiBold, Bold }

    // The look of every screen of the game: the fonts, the palette, the sizes and the sprites,
    // by their fixed names (UiSprites). One asset, Assets/_Movers/UI/Resources/MoversUiTheme,
    // loaded by name so the HUD, the pause menu and the main menu share it without a scene
    // reference. Every field has a code default: with no asset at all (or before UIART's
    // sprites exist) the UI still renders, in flat colours with rounded corners.
    //
    // Cozy and warm on purpose: a moving crew's world is wood, cardboard, packing tape and
    // paper labels, never chrome. Text is dark ink on paper, or cream on wood.
    [CreateAssetMenu(menuName = "Movers/UI Theme", fileName = "MoversUiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        public const string ResourcePath = "MoversUiTheme";

        [Header("Fonts (Fredoka, SIL OFL: Assets/_Movers/UI/Fonts)")]
        public Font regular;
        public Font medium;
        public Font semiBold;
        public Font bold;

        // The palette of UIART's kit (sprites.json "palette"), so flat fills and text match the
        // rendered wood, cardboard and paper.
        [Header("Palette")]
        public Color ink = Hex(0x3B2A1E);          // text on paper, cardboard, the pale board, key caps
        public Color inkSoft = Hex(0x6E5644);      // secondary text on those
        public Color cream = Hex(0xFFF7E8);        // text on walnut and on the buttons
        public Color creamSoft = Hex(0xE8D8BC);    // secondary text on walnut
        public Color paper = Hex(0xF4E6CB);
        public Color paperShade = Hex(0xE3CFA8);
        public Color woodLight = Hex(0xE6CDA3);    // pine: the pale board
        public Color wood = Hex(0xA57F5E);
        public Color plank = Hex(0x9C6B43);        // the button plank
        public Color woodDark = Hex(0x5A3F2B);     // walnut: HUD panels over the scene
        public Color woodDarker = Hex(0x3C2A1C);
        public Color cardboard = Hex(0xC99A64);
        public Color tape = Hex(0xDEC38C);
        public Color tapeYellow = Hex(0xEDBE3C);   // the tape measure
        public Color rope = Hex(0xD4B680);
        public Color leaf = Hex(0x709952);
        public Color accent = Hex(0xE8893A);       // the kit's orange: what to look at
        public Color good = Hex(0x7FB870);
        public Color bad = Hex(0xCF4B3B);
        public Color warn = Hex(0xE5853C);
        public Color info = Hex(0x74AFCF);
        public Color shadow = new Color(0.12f, 0.07f, 0.03f, 0.7f);

        [Header("The same meanings, dark enough to read on paper and cardboard")]
        public Color goodInk = Hex(0x3E7A2A);
        public Color badInk = Hex(0xA8321F);
        public Color warnInk = Hex(0xA55A15);
        public Color infoInk = Hex(0x2F6F93);

        [Header("The grandmother's mood, calm to police")]
        public Color moodSweet = Hex(0x7FB870);
        public Color moodAnnoyed = Hex(0xE6B845);
        public Color moodAngry = Hex(0xE5853C);
        public Color moodFurious = Hex(0xCF4B3B);
        public Color moodPolice = Hex(0x8F2A1E);

        [Header("Sizes, in panel units at 1080 lines")]
        public float textCaption = 15f;
        public float textSmall = 17f;
        public float textBody = 19f;
        public float textLabel = 21f;
        public float textTitle = 30f;
        public float textDisplay = 64f;
        public float keyCap = 32f;          // height of a key glyph in a hint row
        public float gap = 8f;
        [Tooltip("Sprites are rendered at 2x density: their 9-slice borders are drawn at this scale.")]
        public float spriteSliceScale = 0.5f;

        [Serializable]
        public struct NamedSprite
        {
            public string name;
            public Sprite sprite;
            [Tooltip("sprites.json content: the inner padding where text goes, in texels, left, bottom, right, top.")]
            public Vector4 content;
        }

        [Header("Sprites, by their fixed names (filled by the integration recipe)")]
        public List<NamedSprite> sprites = new List<NamedSprite>();

        // ---- access ----

        static UiTheme current;

        // The theme asset, or a default one built from the code values above.
        public static UiTheme Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<UiTheme>(ResourcePath);
                if (current == null)
                {
                    current = CreateInstance<UiTheme>();
                    current.name = "UiTheme (code defaults)";
                    current.hideFlags = HideFlags.DontSave;
                }
                return current;
            }
        }

        Dictionary<string, Sprite> lookup;
        Dictionary<string, Vector4> contents;

        // A sprite by its fixed name, or null when UIART has not delivered it (callers draw a
        // flat fallback then).
        public Sprite Sprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite>(sprites != null ? sprites.Count : 0);
                if (sprites != null)
                    for (int i = 0; i < sprites.Count; i++)
                        if (!string.IsNullOrEmpty(sprites[i].name) && sprites[i].sprite != null)
                            lookup[sprites[i].name] = sprites[i].sprite;
            }
            return lookup.TryGetValue(spriteName, out var s) ? s : null;
        }

        // The sprite's content rect insets (left, bottom, right, top, texels); its 9-slice
        // border when the manifest gave none.
        public Vector4 Content(string spriteName)
        {
            if (contents == null)
            {
                contents = new Dictionary<string, Vector4>(sprites != null ? sprites.Count : 0);
                if (sprites != null)
                    for (int i = 0; i < sprites.Count; i++)
                        if (!string.IsNullOrEmpty(sprites[i].name))
                            contents[sprites[i].name] = sprites[i].content != Vector4.zero || sprites[i].sprite == null
                                ? sprites[i].content : sprites[i].sprite.border;
            }
            return spriteName != null && contents.TryGetValue(spriteName, out var c) ? c : Vector4.zero;
        }

        public bool HasSprite(string spriteName) => Sprite(spriteName) != null;

        public Color Mood(MoodTier tier)
        {
            switch (tier)
            {
                case MoodTier.Sweet: return moodSweet;
                case MoodTier.Annoyed: return moodAnnoyed;
                case MoodTier.Angry: return moodAngry;
                case MoodTier.Furious: return moodFurious;
                default: return moodPolice;
            }
        }

        public static string MoodIcon(MoodTier tier)
        {
            switch (tier)
            {
                case MoodTier.Sweet: return UiSprites.IconGrandmaCalm;
                case MoodTier.Annoyed: return UiSprites.IconGrandmaAnnoyed;
                case MoodTier.Angry: return UiSprites.IconGrandmaAngry;
                default: return UiSprites.IconGrandmaFurious;
            }
        }

        // ---- fonts ----

        // Runtime font assets, one per weight, made from the TTFs on first use. The medium and
        // semibold faces point their bold slot at the real Bold face, so a Text asking for
        // FontStyle.Bold draws Fredoka Bold instead of a smeared fake bold.
        readonly FontAsset[] fontAssets = new FontAsset[4];
        // NonSerialized: the editor's domain reload at each Play keeps an asset's private
        // fields but not readonly ones, so without it the flag said "built" over an empty
        // array and every Play after the first drew the default font instead of Fredoka.
        [NonSerialized] bool fontsBuilt;

        public FontAsset FontFor(UiWeight weight)
        {
            if (!fontsBuilt) BuildFonts();
            return fontAssets[(int)weight];
        }

        void BuildFonts()
        {
            fontsBuilt = true;
            Font[] faces = { regular, medium, semiBold, bold };
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == null) continue;
                try
                {
                    var fa = FontAsset.CreateFontAsset(faces[i]);
                    if (fa == null) continue;
                    fa.name = faces[i].name + " (runtime)";
                    fa.hideFlags = HideFlags.DontSave;
                    fontAssets[i] = fa;
                }
                catch (Exception e)
                {
                    // A font without its source data cannot become an asset; the UI falls back
                    // to the panel's default font rather than failing to draw at all.
                    Debug.LogWarning("[UiTheme] font " + faces[i].name + " could not be used: " + e.Message);
                }
            }
            var boldFace = fontAssets[(int)UiWeight.Bold] ?? fontAssets[(int)UiWeight.SemiBold];
            if (boldFace == null) return;
            for (int i = 0; i < 3; i++) LinkBold(fontAssets[i], boldFace);
        }

        static void LinkBold(FontAsset face, FontAsset boldFace)
        {
            if (face == null || face == boldFace) return;
            var table = face.fontWeightTable;
            if (table == null || table.Length < 8) return;
            table[7].regularTypeface = boldFace;   // index 7 is weight 700, what Bold asks for
        }

        // ---- surfaces and text ----

        UiSkins skins;
        UiTextStyles text;

        // The game's panel surfaces (wood, cardboard, paper tags...), made from this palette.
        public UiSkins Skins => skins ?? (skins = new UiSkins(this));

        // The game's text sizes, as shared LumaFlow TextStyles.
        public UiTextStyles Text => text ?? (text = new UiTextStyles(this));

        // ---- LumaFlow ----

        ThemeData luma;

        // The LumaFlow theme for widgets that read one (sliders, switches, the default text).
        // Text colours are left unset on purpose: a Text without an explicit colour inherits
        // the ink or cream of the panel it sits on (UiSkin sets it).
        public ThemeData Luma
        {
            get
            {
                if (luma != null) return luma;
                var colors = new ColorScheme(
                    canvas: paper, surface: paper, surfaceVariant: paperShade,
                    primary: wood, primaryContainer: woodLight, onPrimary: cream,
                    onSurface: ink, onSurfaceVariant: inkSoft, outline: woodDark);
                var typography = new TypographyTheme(
                    title: new LumaTextStyle(null, textTitle, FontStyle.Bold),
                    headline: new LumaTextStyle(null, textLabel, FontStyle.Bold),
                    body: new LumaTextStyle(null, textBody, FontStyle.Normal),
                    label: new LumaTextStyle(null, textBody, FontStyle.Bold));
                var primary = new ButtonStyle(
                    background: wood, foreground: cream,
                    padding: EdgeInsets.Symmetric(horizontal: 22f, vertical: 10f),
                    shape: BorderRadius.All(10f),
                    typography: new LumaTextStyle(null, textLabel, FontStyle.Bold),
                    border: Border.All(woodDarker, 3f),
                    hovered: new ButtonStateStyle(background: woodLight),
                    pressed: new ButtonStateStyle(background: woodDark));
                var secondary = new ButtonStyle(
                    background: paper, foreground: ink,
                    padding: EdgeInsets.Symmetric(horizontal: 18f, vertical: 10f),
                    shape: BorderRadius.All(10f),
                    typography: new LumaTextStyle(null, textBody, FontStyle.Bold),
                    border: Border.All(woodDark, 2f),
                    hovered: new ButtonStateStyle(background: paperShade),
                    pressed: new ButtonStateStyle(background: tape));
                luma = new ThemeData(
                    colors, typography,
                    new SpacingTheme(4f, 8f, 12f, 20f, 32f),
                    new RadiusTheme(BorderRadius.All(6f), BorderRadius.All(10f), BorderRadius.All(16f)),
                    new ButtonTheme(primary, secondary),
                    iconTheme: new IconThemeData(22f, cream));
                return luma;
            }
        }

        // ---- helpers ----

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        void OnValidate()
        {
            // Edited in the inspector: rebuild the caches on next use.
            lookup = null;
            contents = null;
            luma = null;
            skins = null;
            text = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
        }
    }
}
