using UnityEngine;

namespace Movers
{
    // The few words the indicators show. Numbers ("12 m", the other player's "2") read the same
    // in French and English; the compass letters do not (West is O in French), so they go
    // through the game's table (Loc, UICORE's UI/Loc.cs): registered here, French first, unless
    // the table already has them (then the table's own lines win).
    //
    // This is the only file of the indicators that calls Loc: Loc.T, Loc.Has and Loc.Register.
    public static class IndicatorText
    {
        public const string KeyNorth = "indicators.compass.n";
        public const string KeyEast = "indicators.compass.e";
        public const string KeySouth = "indicators.compass.s";
        public const string KeyWest = "indicators.compass.w";

        static readonly string[] Keys = { KeyNorth, KeyEast, KeySouth, KeyWest };
        public static readonly string[] French = { "N", "E", "S", "O" };
        public static readonly string[] English = { "N", "E", "S", "W" };

        static bool registered;

        // quarter: 0 north, 1 east, 2 south, 3 west. In the language the game is set to.
        public static string Cardinal(int quarter)
        {
            if (!registered) Register();
            quarter = ((quarter % 4) + 4) % 4;
            string key = Keys[quarter];
            string s = Loc.T(key);
            return string.IsNullOrEmpty(s) || s == key ? French[quarter] : s;
        }

        static void Register()
        {
            registered = true;
            for (int i = 0; i < Keys.Length; i++)
                if (!Loc.Has(Keys[i])) Loc.Register(Keys[i], French[i], English[i]);
        }

        // "12 m", made once per whole metre and kept: the labels change every frame the distance
        // does, and a new string per frame is garbage per frame.
        static readonly string[] metres = new string[1000];

        public static string Metres(float d)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(d), 0, metres.Length - 1);
            return metres[i] ?? (metres[i] = i + " m");
        }

        static readonly string[] numbers = { "1", "2", "3", "4" };

        public static string PlayerNumber(int index) => index >= 0 && index < numbers.Length ? numbers[index] : "?";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { registered = false; }
    }
}
