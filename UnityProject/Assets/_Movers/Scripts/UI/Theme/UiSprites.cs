namespace Movers
{
    // The fixed names of the UI sprites (UIART renders them into Assets/_Movers/UI/Sprites/,
    // PNG with alpha at 2x density, 9-slice borders in sprites.json). Code names them only
    // through these constants, so a renamed file is one edit here.
    public static class UiSprites
    {
        // Frames and panels (9-slice)
        public const string FrameWood = "frame_wood";
        public const string FrameWoodDark = "frame_wood_dark";
        public const string FrameBranch = "frame_branch";
        public const string FrameCardboard = "frame_cardboard";
        public const string FrameTag = "frame_tag";

        // Buttons (9-slice)
        public const string ButtonNormal = "button_wood_normal";
        public const string ButtonHover = "button_wood_hover";
        public const string ButtonPressed = "button_wood_pressed";
        public const string ButtonDisabled = "button_wood_disabled";

        // Tape-measure bar (9-slice)
        public const string BarTapeBg = "bar_tape_bg";
        public const string BarTapeFill = "bar_tape_fill";

        // Input glyphs
        public const string KeyBlank = "key_blank";   // 9-slice; the letter is drawn by the UI
        public const string KeyWide = "key_wide";     // 9-slice: Space, Shift, Ctrl, Esc, Tab
        public const string MouseLeft = "mouse_left";
        public const string MouseRight = "mouse_right";
        public const string MouseWheel = "mouse_wheel";
        public const string PadA = "pad_a";
        public const string PadB = "pad_b";
        public const string PadX = "pad_x";
        public const string PadY = "pad_y";
        public const string PadLB = "pad_lb";
        public const string PadRB = "pad_rb";
        public const string PadLT = "pad_lt";
        public const string PadRT = "pad_rt";
        public const string PadDpadUp = "pad_dpad_up";
        public const string PadDpadDown = "pad_dpad_down";
        public const string PadDpadLeft = "pad_dpad_left";
        public const string PadDpadRight = "pad_dpad_right";
        public const string PadLS = "pad_ls";
        public const string PadRS = "pad_rs";
        public const string PadStart = "pad_start";

        // Icons
        public const string IconMoney = "icon_money";
        public const string IconClock = "icon_clock";
        public const string IconTruck = "icon_truck";
        public const string IconBox = "icon_box";
        public const string IconKey = "icon_key";
        public const string IconPocket = "icon_pocket";
        public const string IconCigarette = "icon_cigarette";
        public const string IconBeer = "icon_beer";
        public const string IconGrenade = "icon_grenade";
        public const string IconCrate = "icon_crate";
        public const string IconWarning = "icon_warning";
        public const string IconCheck = "icon_check";
        public const string IconCross = "icon_cross";
        public const string IconGrandmaCalm = "icon_grandma_calm";
        public const string IconGrandmaAnnoyed = "icon_grandma_annoyed";
        public const string IconGrandmaAngry = "icon_grandma_angry";
        public const string IconGrandmaFurious = "icon_grandma_furious";

        // Indicators (INDICATORS track) and the rest
        public const string IndicatorArrow = "indicator_arrow";
        public const string IndicatorRing = "indicator_ring";
        public const string CompassTapeBg = "compass_tape_bg";
        public const string CompassTick = "compass_tick";
        public const string CrosshairDot = "crosshair_dot";
        public const string LogoTheMovers = "logo_the_movers";
        public const string BgPaper = "bg_paper";

        // Every name, for the asset that lists them and the test that checks the folder.
        public static readonly string[] All =
        {
            FrameWood, FrameWoodDark, FrameBranch, FrameCardboard, FrameTag,
            ButtonNormal, ButtonHover, ButtonPressed, ButtonDisabled,
            BarTapeBg, BarTapeFill,
            KeyBlank, KeyWide, MouseLeft, MouseRight, MouseWheel,
            PadA, PadB, PadX, PadY, PadLB, PadRB, PadLT, PadRT,
            PadDpadUp, PadDpadDown, PadDpadLeft, PadDpadRight, PadLS, PadRS, PadStart,
            IconMoney, IconClock, IconTruck, IconBox, IconKey, IconPocket, IconCigarette, IconBeer,
            IconGrenade, IconCrate, IconWarning, IconCheck, IconCross,
            IconGrandmaCalm, IconGrandmaAnnoyed, IconGrandmaAngry, IconGrandmaFurious,
            IndicatorArrow, IndicatorRing, CompassTapeBg, CompassTick, CrosshairDot, LogoTheMovers, BgPaper,
        };
    }
}
