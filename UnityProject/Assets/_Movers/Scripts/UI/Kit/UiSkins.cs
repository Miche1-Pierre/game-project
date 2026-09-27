using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The game's surfaces, made once per theme from its palette and UIART's sprites. Shared by
    // every panel of a kind (see UiProbe for why sharing matters).
    //
    // Slice scales follow UIART's manifest: the art is 2x (0.5 shows it at its drawn size);
    // HUD pieces over the scene use less so a half-width split view keeps room for the game,
    // menus use more so their frames read as solid wood. Text colours follow the manifest's
    // "text" field: ink on the pale board, cardboard, paper and key caps; cream on walnut and
    // on the button plank.
    public sealed class UiSkins
    {
        public const float TapeHeight = 24f;   // bar_tape_bg is 80 texels tall: 0.3 shows it at 24

        public readonly UiSkin Wood;          // the pale board in lashed battens: menus, the controls sheet
        public readonly UiSkin WoodDark;      // walnut board: HUD panels over the scene
        public readonly UiSkin Branch;        // lashed branches, transparent inside: title frames
        public readonly UiSkin Plate;         // a plain walnut plate, inside the branch frame
        public readonly UiSkin Cardboard;     // a box flap with packing tape: the contract
        public readonly UiSkin CardboardBig;  // the same, full size: the intro card, the end screen
        public readonly UiSkin Tag;           // a paper luggage label: key hints, toasts
        public readonly UiSkin TagBig;        // the same, larger: the grandmother's words
        public readonly UiSkin TagTail;       // the speech bubble's little tail (plain paper)
        public readonly UiSkin Paper;         // seamless warm paper
        public readonly UiSkin ButtonNormal;
        public readonly UiSkin ButtonHover;
        public readonly UiSkin ButtonPressed;
        public readonly UiSkin ButtonDisabled;
        public readonly UiSkin KeyCap;        // a keyboard key; the letter is text
        public readonly UiSkin KeyWide;       // Espace, Maj, Ctrl, Echap, Tab
        public readonly UiSkin TapeBg;        // the tape measure's case
        public readonly UiSkin Chip;          // a small pill: "hold", "P2", "x2"
        public readonly UiSkin Scrim;         // dims the game behind a card or a menu
        public readonly UiSkin Slot;          // one pocket
        public readonly UiSkin SlotActive;    // the pocket what you hold came out of
        public readonly UiSkin Clear;         // no surface, only text settings
        public readonly UiSkin ClearInk;      // the same, ink text: rows on the pale board (the options)

        readonly UiTheme theme;
        readonly Dictionary<Color, UiSkin> tapeFills = new Dictionary<Color, UiSkin>();
        readonly Dictionary<Color, UiSkin> chips = new Dictionary<Color, UiSkin>();

        public UiSkins(UiTheme t)
        {
            theme = t;
            Wood = new UiSkin(UiSprites.FrameWood, t.woodLight, t.woodDark, 3f, 12f, t.ink, UiWeight.Medium, false, sliceScale: 0.4f, fallbackPadding: 16f);
            WoodDark = new UiSkin(UiSprites.FrameWoodDark, Alpha(t.woodDark, 0.95f), t.woodDarker, 3f, 12f, t.cream, UiWeight.Medium, true, sliceScale: 0.3f, fallbackPadding: 10f);
            Branch = new UiSkin(UiSprites.FrameBranch, t.woodDark, t.leaf, 4f, 14f, t.cream, UiWeight.Bold, true, sliceScale: 0.4f, fallbackPadding: 6f);
            Plate = new UiSkin(null, t.woodDark, t.woodDarker, 2f, 8f, t.cream, UiWeight.Bold, true, fallbackPadding: 8f);
            Cardboard = new UiSkin(UiSprites.FrameCardboard, t.cardboard, Hex(0x8E6A40), 3f, 6f, t.ink, UiWeight.Medium, false, sliceScale: 0.4f, fallbackPadding: 12f);
            CardboardBig = new UiSkin(UiSprites.FrameCardboard, t.cardboard, Hex(0x8E6A40), 4f, 8f, t.ink, UiWeight.Medium, false, sliceScale: 0.5f, fallbackPadding: 28f);
            Tag = new UiSkin(UiSprites.FrameTag, t.paper, t.inkSoft, 2f, 8f, t.ink, UiWeight.Medium, false, sliceScale: 0.3f, fallbackPadding: 8f);
            TagBig = new UiSkin(UiSprites.FrameTag, t.paper, t.inkSoft, 2f, 10f, t.ink, UiWeight.SemiBold, false, sliceScale: 0.36f, fallbackPadding: 12f);
            TagTail = new UiSkin(null, t.paper, t.inkSoft, 2f, 3f);
            Paper = new UiSkin(UiSprites.BgPaper, t.paper, t.woodDark, 3f, 12f, t.ink, UiWeight.Medium, false, sliceScale: 0.5f, fallbackPadding: 16f, tiled: true);
            ButtonNormal = new UiSkin(UiSprites.ButtonNormal, t.plank, t.woodDarker, 3f, 12f, t.cream, UiWeight.SemiBold, true, sliceScale: 0.42f, fallbackPadding: 12f);
            ButtonHover = new UiSkin(UiSprites.ButtonHover, t.wood, t.accent, 3f, 12f, Color.white, UiWeight.SemiBold, true, sliceScale: 0.42f, fallbackPadding: 12f);
            ButtonPressed = new UiSkin(UiSprites.ButtonPressed, t.woodDark, t.woodDarker, 3f, 12f, t.creamSoft, UiWeight.SemiBold, true, sliceScale: 0.42f, fallbackPadding: 12f);
            ButtonDisabled = new UiSkin(UiSprites.ButtonDisabled, Hex(0x8A7A6A), Hex(0x5A4C40), 3f, 12f, Hex(0xD9CCB8), UiWeight.SemiBold, false, sliceScale: 0.42f, fallbackPadding: 12f);
            KeyCap = new UiSkin(UiSprites.KeyBlank, t.cream, t.inkSoft, 2f, 7f, t.ink, UiWeight.SemiBold, false, sliceScale: 0.22f, fallbackPadding: 3f);
            KeyWide = new UiSkin(UiSprites.KeyWide, t.cream, t.inkSoft, 2f, 7f, t.ink, UiWeight.SemiBold, false, sliceScale: 0.22f, fallbackPadding: 3f);
            TapeBg = new UiSkin(UiSprites.BarTapeBg, Alpha(t.woodDarker, 0.85f), t.woodDarker, 2f, 7f, sliceScale: TapeHeight / 80f, fallbackPadding: 3f);
            Chip = new UiSkin(null, Alpha(t.woodDarker, 0.85f), Color.clear, 0f, 9f, t.creamSoft, UiWeight.SemiBold, false);
            Scrim = new UiSkin(null, new Color(0.10f, 0.05f, 0.02f, 0.62f), Color.clear, 0f, 0f);
            Slot = new UiSkin(UiSprites.FrameCardboard, t.cardboard, Hex(0x8E6A40), 3f, 8f, t.ink, UiWeight.SemiBold, false, sliceScale: 0.2f, fallbackPadding: 4f);
            SlotActive = new UiSkin(UiSprites.FrameCardboard, t.tape, t.accent, 3f, 8f, t.ink, UiWeight.Bold, false, tint: Hex(0xFFE6A8), sliceScale: 0.2f, fallbackPadding: 4f);
            Clear = new UiSkin(null, Color.clear, Color.clear, 0f, 0f, t.cream, UiWeight.Medium, true);
            ClearInk = new UiSkin(null, Color.clear, Color.clear, 0f, 0f, t.ink, UiWeight.Medium, false);
        }

        // The tape pulled out. With UIART's art it is the yellow tape (not meant to be tinted:
        // what the colour meant is carried by the chip and icon next to it); `warnTint` reddens
        // it for a bad state (overloaded). Without the art, a flat bar in `c`.
        public UiSkin TapeFill(Color c, bool warnTint = false)
        {
            Color key = warnTint ? new Color(c.r, c.g, c.b, 0.5f) : c;
            if (!tapeFills.TryGetValue(key, out var s))
            {
                s = new UiSkin(UiSprites.BarTapeFill, c, Color.clear, 0f, 4f,
                               tint: warnTint ? Hex(0xFF8C7A) : Color.white, sliceScale: TapeHeight / 80f, tiledCenter: true);
                tapeFills.Add(key, s);
            }
            return s;
        }

        // A pill in a colour, with dark text when the colour is light.
        public UiSkin ChipIn(Color c)
        {
            if (!chips.TryGetValue(c, out var s))
            {
                float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
                s = new UiSkin(null, c, Color.clear, 0f, 9f, lum > 0.6f ? theme.ink : theme.cream, UiWeight.SemiBold, false);
                chips.Add(c, s);
            }
            return s;
        }

        static Color Alpha(Color c, float a) { c.a = a; return c; }
        static Color Hex(int rgb) => UiTheme.Hex(rgb);
    }
}
