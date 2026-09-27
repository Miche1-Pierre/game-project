using System;
using UnityEngine;

namespace Movers
{
    public enum GlyphKind : byte
    {
        None,
        Key,        // a key cap with one or two characters: E, F, 1
        WideKey,    // a wide key cap with a word: Space, Shift, Ctrl, Esc, Tab
        Sprite,     // a drawn glyph: a mouse button, a pad button (text is its fallback)
    }

    // One button as the player sees it on screen. A value type with value equality, so a hint
    // row can be compared every frame without garbage.
    public readonly struct Glyph : IEquatable<Glyph>
    {
        public readonly GlyphKind kind;
        public readonly string text;     // the letter or word, or the fallback when the sprite is missing
        public readonly string sprite;   // UiSprites name for Sprite and for the key cap's own art

        public Glyph(GlyphKind kind, string text, string sprite = null)
        {
            this.kind = kind;
            this.text = text;
            this.sprite = sprite;
        }

        public bool IsNone => kind == GlyphKind.None;

        public bool Equals(Glyph o) => kind == o.kind && ReferenceEquals(text, o.text) && ReferenceEquals(sprite, o.sprite);
        public override bool Equals(object obj) => obj is Glyph o && Equals(o);
        public override int GetHashCode() => ((int)kind * 397) ^ (text != null ? text.GetHashCode() : 0);
    }

    // Which glyph a button has for the device a player holds: the keyboard's keys (read from
    // the player's KeyboardMouseSource, so a remapped key shows right), the mouse's buttons,
    // or the pad's, as Core/GamepadSource maps them:
    //
    //   A jump   B crouch   X interact   Y alt   LB rotate   RB grab   LT reach   RT throw/use
    //   LS click sprint   Start pause   d-pad up/right/down/left pockets 1-4
    //
    // Words that change with the language (Space is Espace in French) come from Loc, whose
    // strings are cached: nothing here allocates.
    public static class InputGlyphs
    {
        public static bool IsPad(ICrewInputSource source) => source is GamepadSource;
        public static bool IsPad(CrewInput input) => input != null && input.Source is GamepadSource;

        static readonly KeyboardMouseSource DefaultKeys = new KeyboardMouseSource();

        public static Glyph For(CrewInput input, CrewButton b) => For(input != null ? input.Source : null, b);

        public static Glyph For(ICrewInputSource source, CrewButton b)
        {
            if (source is GamepadSource) return Pad(b);
            return Keyboard(source as KeyboardMouseSource ?? DefaultKeys, b);
        }

        // A device's key without a player: the cards shared by the whole crew name both.
        public static Glyph ForKeyboard(CrewButton b) => Keyboard(DefaultKeys, b);
        public static Glyph ForPad(CrewButton b) => Pad(b);

        static Glyph Keyboard(KeyboardMouseSource k, CrewButton b)
        {
            switch (b)
            {
                case CrewButton.Jump: return Key(k.jumpKey);
                case CrewButton.Sprint: return Key(k.sprintKey);
                case CrewButton.Crouch: return Key(k.crouchKey);
                case CrewButton.Grab: return new Glyph(GlyphKind.Sprite, Loc.T("key.lmb"), UiSprites.MouseLeft);
                case CrewButton.Throw: return new Glyph(GlyphKind.Sprite, Loc.T("key.rmb"), UiSprites.MouseRight);
                case CrewButton.Rotate: return Key(k.rotateKey);
                case CrewButton.Reach: return Wheel;
                case CrewButton.Interact: return Key(k.interactKey);
                case CrewButton.Alt: return Key(k.altKey);
                case CrewButton.Pocket1: return Key(KeyCode.Alpha1);
                case CrewButton.Pocket2: return Key(KeyCode.Alpha2);
                case CrewButton.Pocket3: return Key(KeyCode.Alpha3);
                case CrewButton.Pocket4: return Key(KeyCode.Alpha4);
                case CrewButton.Pause: return Key(k.pauseKey);
            }
            return default;
        }

        static Glyph Pad(CrewButton b)
        {
            switch (b)
            {
                case CrewButton.Jump: return PadGlyph(UiSprites.PadA, "A");
                case CrewButton.Sprint: return PadGlyph(UiSprites.PadLS, "L3");
                case CrewButton.Crouch: return PadGlyph(UiSprites.PadB, "B");
                case CrewButton.Grab: return PadGlyph(UiSprites.PadRB, "RB");
                case CrewButton.Throw: return PadGlyph(UiSprites.PadRT, "RT");
                case CrewButton.Rotate: return PadGlyph(UiSprites.PadLB, "LB");
                case CrewButton.Reach: return PadGlyph(UiSprites.PadLT, "LT");
                case CrewButton.Interact: return PadGlyph(UiSprites.PadX, "X");
                case CrewButton.Alt: return PadGlyph(UiSprites.PadY, "Y");
                case CrewButton.Pocket1: return PadGlyph(UiSprites.PadDpadUp, "^");
                case CrewButton.Pocket2: return PadGlyph(UiSprites.PadDpadRight, ">");
                case CrewButton.Pocket3: return PadGlyph(UiSprites.PadDpadDown, "v");
                case CrewButton.Pocket4: return PadGlyph(UiSprites.PadDpadLeft, "<");
                case CrewButton.Pause: return PadGlyph(UiSprites.PadStart, "Start");
            }
            return default;
        }

        // ---- things that are not one CrewButton ----

        public static Glyph Wheel => new Glyph(GlyphKind.Sprite, Loc.T("key.wheel"), UiSprites.MouseWheel);

        // Walking: the left stick, or the keys.
        public static Glyph Move(ICrewInputSource source) =>
            source is GamepadSource ? PadGlyph(UiSprites.PadLS, "LS") : new Glyph(GlyphKind.WideKey, "WASD", UiSprites.KeyWide);

        // Looking: the right stick, or the mouse.
        public static Glyph Look(ICrewInputSource source) =>
            source is GamepadSource ? PadGlyph(UiSprites.PadRS, "RS") : new Glyph(GlyphKind.WideKey, Loc.T("key.mouse"), UiSprites.KeyWide);

        // The pad's roll while rotating is the d-pad left/right; the keyboard's is the wheel.
        public static Glyph Roll(ICrewInputSource source) =>
            source is GamepadSource ? PadGlyph(UiSprites.PadDpadLeft, "<>") : Wheel;

        // Reach on a pad is LT held plus the right stick: the LT glyph carries it.
        public static Glyph ReachStick(ICrewInputSource source) =>
            source is GamepadSource ? PadGlyph(UiSprites.PadRS, "RS") : default;

        // The controls sheet's own toggle: Tab on the keyboard, Back/View on a pad. Not a
        // CrewButton (that list is a frozen contract): read by HudKeys directly.
        public static Glyph ControlsToggle(ICrewInputSource source) =>
            source is GamepadSource ? new Glyph(GlyphKind.WideKey, Loc.T("key.back"), UiSprites.KeyWide) : new Glyph(GlyphKind.WideKey, Loc.T("key.tab"), UiSprites.KeyWide);

        public static Glyph PocketSlot(ICrewInputSource source, int slot)
        {
            switch (slot)
            {
                case 0: return For(source, CrewButton.Pocket1);
                case 1: return For(source, CrewButton.Pocket2);
                case 2: return For(source, CrewButton.Pocket3);
                default: return For(source, CrewButton.Pocket4);
            }
        }

        public static CrewButton PocketButton(int slot)
        {
            switch (slot)
            {
                case 0: return CrewButton.Pocket1;
                case 1: return CrewButton.Pocket2;
                case 2: return CrewButton.Pocket3;
                default: return CrewButton.Pocket4;
            }
        }

        static Glyph PadGlyph(string sprite, string fallback) => new Glyph(GlyphKind.Sprite, fallback, sprite);

        static Glyph Key(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Space: return new Glyph(GlyphKind.WideKey, Loc.T("key.space"), UiSprites.KeyWide);
                case KeyCode.LeftShift: case KeyCode.RightShift: return new Glyph(GlyphKind.WideKey, Loc.T("key.shift"), UiSprites.KeyWide);
                case KeyCode.LeftControl: case KeyCode.RightControl: return new Glyph(GlyphKind.WideKey, Loc.T("key.ctrl"), UiSprites.KeyWide);
                case KeyCode.Escape: return new Glyph(GlyphKind.WideKey, Loc.T("key.esc"), UiSprites.KeyWide);
                case KeyCode.Tab: return new Glyph(GlyphKind.WideKey, Loc.T("key.tab"), UiSprites.KeyWide);
                case KeyCode.Return: return new Glyph(GlyphKind.WideKey, Loc.T("key.enter"), UiSprites.KeyWide);
            }
            return new Glyph(GlyphKind.Key, KeyText(code), UiSprites.KeyBlank);
        }

        // "E", "1": a letter or digit key as one character. Cached per KeyCode, the enum's own
        // ToString allocating every call.
        static readonly string[] keyText = new string[512];
        static string KeyText(KeyCode code)
        {
            int i = (int)code;
            if (i < 0 || i >= keyText.Length) return code.ToString();
            if (keyText[i] != null) return keyText[i];
            string s;
            if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9) s = ((char)('0' + (code - KeyCode.Alpha0))).ToString();
            else if (code >= KeyCode.A && code <= KeyCode.Z) s = ((char)('A' + (code - KeyCode.A))).ToString();
            else s = code.ToString();
            keyText[i] = s;
            return s;
        }
    }
}
