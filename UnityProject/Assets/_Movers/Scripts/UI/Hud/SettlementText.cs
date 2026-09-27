using System;
using System.Text;

namespace Movers
{
    // The settlement's lines in the game's language. Settlement (GAMELOOP) writes them in
    // English with a fixed shape ("Contract pay: 9 delivered intact"); this reads the shape back
    // and says it through Loc. A line whose shape it does not know is shown as written, so a
    // new line in Settlement never disappears, it is only untranslated. Runs once per end
    // screen, never per frame.
    public static class SettlementText
    {
        public static string Label(string english)
        {
            if (string.IsNullOrEmpty(english)) return "";
            if (TryCount(english, "Contract pay: ", " delivered intact", out var n)) return Loc.F("settle.intact", n);
            if (TryCount(english, "Part pay: ", " delivered damaged", out n)) return Loc.F("settle.damaged", n);
            if (TryCount(english, "Not delivered: ", " left behind", out n)) return Loc.F("settle.missing", n);
            if (TryCount(english, "Billed: ", " on the list destroyed", out n)) return Loc.F("settle.destroyed", n);
            if (TryCount(english, "Her things nobody saw you take: ", "", out n)) return Loc.F("settle.unseen", n);
            if (TryCount(english, "Confiscated: ", " she saw you take", out n)) return Loc.F("settle.confiscated", n);
            if (TryCount(english, "Her things you had to leave: ", "", out n)) return Loc.F("settle.left", n);
            switch (english)
            {
                case "Fine for what she saw": return Loc.T("settle.fine");
                case "Time is up: the contract is void": return Loc.T("settle.voidTime");
                case "The police came: the contract is void": return Loc.T("settle.voidPolice");
                case "The contract is void": return Loc.T("settle.void");
            }
            if (english.StartsWith("Break-in (", StringComparison.Ordinal) && english.EndsWith(")", StringComparison.Ordinal))
            {
                string what = english.Substring(10, english.Length - 11);
                return Loc.F("settle.breakIn", What(what));
            }
            return english;
        }

        // "Sofa, Armchair and 3 more": each name through Loc.Item, the tail through Loc.
        public static string Detail(string english)
        {
            if (string.IsNullOrEmpty(english) || Loc.Current == Language.English) return english ?? "";
            string list = english;
            string more = null;
            int and = english.LastIndexOf(" and ", StringComparison.Ordinal);
            if (and > 0 && english.EndsWith(" more", StringComparison.Ordinal))
            {
                string count = english.Substring(and + 5, english.Length - and - 5 - 5);
                if (int.TryParse(count, out int extra))
                {
                    list = english.Substring(0, and);
                    more = Loc.F("settle.more", extra);
                }
            }
            var sb = new StringBuilder(list.Length + 16);
            string[] names = list.Split(new[] { ", " }, StringSplitOptions.None);
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Loc.Item(names[i]));
            }
            if (more != null) sb.Append(' ').Append(more);
            return sb.ToString();
        }

        static bool TryCount(string s, string prefix, string suffix, out int n)
        {
            n = 0;
            if (!s.StartsWith(prefix, StringComparison.Ordinal) || !s.EndsWith(suffix, StringComparison.Ordinal)) return false;
            string middle = s.Substring(prefix.Length, s.Length - prefix.Length - suffix.Length);
            return int.TryParse(middle, out n);
        }

        static string What(string english)
        {
            switch (english)
            {
                case "a window": return Loc.T("what.window");
                case "a door": return Loc.T("what.door");
                case "a wall": return Loc.T("what.wall");
                case "damage": return Loc.T("settle.damage");
            }
            return english;
        }
    }
}
