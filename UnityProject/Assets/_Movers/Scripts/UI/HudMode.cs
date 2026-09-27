using UnityEngine;

namespace Movers
{
    // Which HUD draws the game: the old OnGUI one, or the LumaFlow one (HudRoot).
    //
    // Every OnGUI that the LumaFlow HUD replaces starts with one line,
    //     if (!HudMode.UseLegacy) return;
    // so both HUDs never draw at once, and a scene without a HudRoot (an old test map, a
    // package-less build) still shows the old one. HudRoot sets this to false while it is
    // enabled. Debug overlays (SliceDebug, F3/F4/F7) are not replaced and never check it.
    public static class HudMode
    {
        public static bool UseLegacy { get; private set; } = true;

        static int owners;

        // A HudRoot came up: the new HUD draws. Counted, so a reload where the old root goes
        // away after the new one arrived leaves the new HUD in charge.
        public static void Acquire()
        {
            owners++;
            UseLegacy = false;
        }

        public static void Release()
        {
            owners = Mathf.Max(0, owners - 1);
            UseLegacy = owners == 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            owners = 0;
            UseLegacy = true;
        }
    }
}
