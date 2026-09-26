using UnityEngine;

namespace Movers
{
    // The two full-screen cards, drawn over both views (SLICE_ARCHITECTURE section 12): the
    // intro, which states the job, and the end screen, which shows the settlement line by line.
    // Their texts are built once per card, never per frame.
    public sealed class SessionCards
    {
        static readonly Color Title = new Color(1f, 0.85f, 0.35f);
        static readonly Color Dim = new Color(0.78f, 0.78f, 0.78f);
        static readonly Color Win = new Color(0.55f, 0.95f, 0.55f);
        static readonly Color Lose = new Color(1f, 0.42f, 0.35f);

        string introBody;
        int introListCount = -1;

        Settlement shown;
        string endTitle, endReason, totalText;
        string[] labels = new string[0], details = new string[0], amounts = new string[0];
        int footerSecond = -2, footerDevices = -1;
        string footer = "";
        bool f5Works;

        // The cards are shared by both players, so they name the key of every device the crew
        // holds: E on the keyboard, X on a pad (PLAYER's ButtonLabels), both in a mixed crew.
        // Indexed by Devices(): 1 keyboard, 2 pad, 3 both.
        const int Keyboard = 1, Pad = 2;
        static readonly string[] StartKeys = { "", "E: start", "X: start", "E / X: start" };
        static readonly string[] RestartKeys = { "", "E: play again", "X: play again", "E / X: play again" };
        static readonly string[] RestartKeysF5 = { "", "E or F5: play again", "X or F5: play again", "E / X or F5: play again" };

        // ---- intro ----

        public void DrawIntro(Rect screen, GameSession session, ContractManager contract)
        {
            int listCount = contract != null ? contract.Tracker.Total : 0;
            if (introBody == null || listCount != introListCount)
            {
                introListCount = listCount;
                introBody =
                    "The job: load the " + (listCount > 0 ? listCount.ToString() : "") + " things on the list into the truck,\n" +
                    "then deliver them at the yellow board on the truck's right side, at the back.\n\n" +
                    "Anything else of hers that leaves with you is yours to sell,\n" +
                    "as long as she never sees you take it.\n" +
                    "Break things, make noise, dawdle: she loses patience. At the end of it, she calls the police.";
            }

            int size = ViewportGUI.FontSize(screen, 22);
            ViewportGUI.Panel(screen, 0.8f);
            float w = Mathf.Min(screen.width * 0.8f, 1100f * size / 22f);
            float x = screen.x + (screen.width - w) * 0.5f;
            float y = screen.y + screen.height * 0.2f;

            ViewportGUI.Label(new Rect(x, y, w, size * 3f), "THE MOVERS", size * 2, Title, TextAnchor.UpperCenter, true);
            y += size * 2.8f;
            ViewportGUI.Label(new Rect(x, y, w, size * 1.6f), "Grandma's house", size, Dim, TextAnchor.UpperCenter);
            y += size * 2.6f;
            ViewportGUI.Label(new Rect(x, y, w, size * 9f), introBody, size, Color.white, TextAnchor.UpperCenter);
            y += size * 9.5f;
            ViewportGUI.Label(new Rect(x, y, w, size * 2f), "Talk to the grandmother to get the keys", Mathf.RoundToInt(size * 1.3f), Title, TextAnchor.UpperCenter, true);

            if (session != null && session.IntroCardAge >= session.Numbers.introCardSkipAfter)
                ViewportGUI.Label(new Rect(x, screen.yMax - size * 3f, w, size * 2f), StartKeys[Devices()], size, Dim, TextAnchor.UpperCenter);
        }

        // ---- end screen ----

        public void DrawEnd(Rect screen, GameSession session)
        {
            var s = session != null ? session.Result : null;
            if (s == null) return;
            if (s != shown) Build(s);
            BuildFooter(session);

            int size = ViewportGUI.FontSize(screen, 22);
            int small = Mathf.Max(10, size - 6);
            ViewportGUI.Panel(screen, 0.85f);
            float w = Mathf.Min(screen.width * 0.8f, 1000f * size / 22f);
            float x = screen.x + (screen.width - w) * 0.5f;
            float y = screen.y + screen.height * 0.12f;

            ViewportGUI.Label(new Rect(x, y, w, size * 3f), endTitle, size * 2, s.Completed ? Win : Lose, TextAnchor.UpperCenter, true);
            y += size * 2.8f;
            if (endReason.Length > 0)
            {
                ViewportGUI.Label(new Rect(x, y, w, size * 1.6f), endReason, size, Dim, TextAnchor.UpperCenter);
                y += size * 2f;
            }
            y += size;

            float amountW = w * 0.2f;
            for (int i = 0; i < labels.Length; i++)
            {
                ViewportGUI.Label(new Rect(x, y, w - amountW, size * 1.5f), labels[i], size, Color.white);
                ViewportGUI.Label(new Rect(x + w - amountW, y, amountW, size * 1.5f), amounts[i], size, AmountColor(s.Lines[i].amount), TextAnchor.UpperRight, true);
                y += size * 1.35f;
                if (details[i].Length > 0)
                {
                    ViewportGUI.Label(new Rect(x + size, y, w - amountW - size, small * 1.5f), details[i], small, Dim);
                    y += small * 1.45f;
                }
                y += size * 0.3f;
            }

            y += size * 0.5f;
            ViewportGUI.Panel(new Rect(x, y, w, 2f), 0.9f);
            y += size * 0.5f;
            ViewportGUI.Label(new Rect(x, y, w - amountW, size * 1.8f), "TOTAL", Mathf.RoundToInt(size * 1.2f), Color.white, TextAnchor.UpperLeft, true);
            ViewportGUI.Label(new Rect(x + w - amountW * 1.5f, y, amountW * 1.5f, size * 1.8f), totalText, Mathf.RoundToInt(size * 1.2f), AmountColor(s.Total), TextAnchor.UpperRight, true);

            ViewportGUI.Label(new Rect(x, screen.yMax - size * 3f, w, size * 2f), footer, size, Dim, TextAnchor.UpperCenter);
        }

        void Build(Settlement s)
        {
            shown = s;
            if (s.Completed)
            {
                endTitle = "CONTRACT COMPLETE";
                endReason = "";
            }
            else
            {
                endTitle = "RUN FAILED";
                endReason = s.Failure == FailReason.TimeUp ? "Time is up."
                          : s.Failure == FailReason.PoliceCalled ? "Grandma called the police."
                          : "The run was stopped.";
            }
            int n = s.Lines.Count;
            labels = new string[n];
            details = new string[n];
            amounts = new string[n];
            for (int i = 0; i < n; i++)
            {
                var line = s.Lines[i];
                labels[i] = line.label;
                details[i] = line.detail ?? "";
                amounts[i] = line.amount > 0 ? "+" + ContractPanel.Money(line.amount) : ContractPanel.Money(line.amount);
            }
            totalText = ContractPanel.Money(s.Total);
            f5Works = F5Works();
            footerSecond = -2;
        }

        // F5 is only a key when someone registered it (SessionDebug) and something reads it
        // (SliceDebug, debug builds only). Tutorial_01 has neither: the footer must not promise it.
        static bool F5Works()
        {
            if (!Debug.isDebugBuild || Object.FindAnyObjectByType<SliceDebug>() == null) return false;
            var all = DebugCommands.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].key == KeyCode.F5 && !all[i].shift) return true;
            return false;
        }

        // "E: play again (2)" while the delay runs, rebuilt once a second (or when a pad comes
        // or goes).
        void BuildFooter(GameSession session)
        {
            float left = session.Numbers.restartDelay - session.EndAge;
            int second = left > 0f ? Mathf.CeilToInt(left) : 0;
            int devices = Devices();
            if (second == footerSecond && devices == footerDevices) return;
            footerSecond = second;
            footerDevices = devices;
            string keys = (f5Works ? RestartKeysF5 : RestartKeys)[devices];
            footer = second > 0 ? keys + "  (" + second + ")" : keys;
        }

        // Which devices drive the crew. A scripted player reads the keyboard words, as PLAYER's
        // prompts do; nobody at all (no crew rig) is the keyboard, which MutedInputReader reads.
        static int Devices()
        {
            int mask = 0;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var input = all[i] != null ? all[i].Input : null;
                if (input == null) continue;
                string label = input.SourceLabel;
                if (label == "None") continue;
                mask |= label.StartsWith("Gamepad", System.StringComparison.Ordinal) ? Pad : Keyboard;
            }
            return mask == 0 ? Keyboard : mask;
        }

        static Color AmountColor(int amount)
        {
            if (amount > 0) return Win;
            if (amount < 0) return Lose;
            return Dim;
        }
    }
}
