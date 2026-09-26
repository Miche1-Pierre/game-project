using System.Globalization;
using System.Text;
using UnityEngine;

namespace Movers
{
    // The contract, top left of each player's view (HUD region TopLeft): the clock, what is on
    // the list and where each piece is, the money, and what the crew has of hers so far. One
    // text for both views (the contract is shared), rebuilt a few times a second rather than
    // every frame, drawn in each viewport.
    public sealed class ContractPanel
    {
        const float RebuildInterval = 0.25f;
        const int WidthAt1080 = 380;
        // At most this share of the view, so in a half-width split view the toasts (TopCenter)
        // keep room between the panel and the patience bar (TopRight). The names on the list
        // are short (12 characters at most on Map01): 30% of a 960 px view still fits them.
        const float MaxViewShare = 0.3f;
        const float NameColumn = 0.5f;

        readonly StringBuilder sb = new StringBuilder(512);
        const string Title = "<b>MOVING CONTRACT</b>";
        string clock = "", names = "", states = "", footer = "";
        int itemLines, footerLines;
        float nextBuild;
        Color clockColor = Color.white;

        public void Refresh(ContractManager contract, GameSession session)
        {
            if (Time.unscaledTime < nextBuild) return;
            nextBuild = Time.unscaledTime + RebuildInterval;
            if (contract == null) return;
            var t = contract.Tracker;

            BuildClock(session);

            sb.Length = 0;
            var list = t.Required;
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(list[i] != null ? list[i].displayName : "?");
            }
            names = sb.ToString();
            itemLines = list.Count;

            sb.Length = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                AppendState(t, list[i]);
            }
            states = sb.ToString();

            sb.Length = 0;
            sb.Append("Loaded ").Append(t.RemainingLoaded).Append('/').Append(t.Remaining);
            if (t.DestroyedCount > 0) sb.Append("   <color=#ff6b6b>").Append(t.DestroyedCount).Append(" destroyed</color>");
            // While the run is on, what handing the truck over now would pay (thefts and fines
            // included), so the figure moves as they load, break and steal. After, the settlement.
            var ledger = session != null ? session.Ledger : null;
            if (session != null && session.Result != null) sb.Append("\nMoney: ").Append(Money(session.Result.Total));
            else if (session != null)
                sb.Append("\nMoney: ").Append(Money(Settlement.Projected(t, ledger, session.BrokeIn, session.Numbers))).Append(" so far");
            else sb.Append("\nMoney: ").Append(Money(contract.money));
            footerLines = 2;
            if (ledger != null && (ledger.UnseenValue > 0 || ledger.SeenValue > 0))
            {
                // Two lines: as one, it would wrap in a split view and spill out of the panel.
                sb.Append("\nStolen so far:\n").Append(Money(ledger.UnseenValue)).Append(" unseen, ")
                  .Append("<color=#ff8a80>").Append(Money(ledger.SeenValue)).Append(" seen</color>");
                footerLines = 4;
            }
            footer = sb.ToString();
        }

        void BuildClock(GameSession session)
        {
            clockColor = Color.white;
            if (Session.State == SessionState.Intro) { clock = "keys first"; return; }
            if (session != null && session.PoliceCalled)
            {
                clock = "POLICE " + Mathf.CeilToInt(session.PoliceIn);
                clockColor = new Color(1f, 0.35f, 0.3f);
                return;
            }
            if (Session.TimeLimit <= 0f) { clock = ""; return; }
            int s = Mathf.CeilToInt(Session.TimeLeft);
            clock = (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
            if (s <= 60) clockColor = new Color(1f, 0.45f, 0.35f);
        }

        void AppendState(ContractTracker t, MovableObject m)
        {
            ItemCondition c = t.ConditionOf(m);
            if (c == ItemCondition.Destroyed) { sb.Append("<color=#ff6b6b>Destroyed</color>"); return; }
            switch (t.LocationOf(m))
            {
                case ItemLocation.Loaded: sb.Append("<color=#9be29b>Loaded</color>"); break;
                case ItemLocation.Delivered: sb.Append("<color=#7fd4ff>Delivered</color>"); break;
                case ItemLocation.Pocketed: sb.Append("<color=#ffd27f>Pocketed</color>"); break;
                case ItemLocation.Worn: sb.Append("<color=#ffd27f>Worn</color>"); break;
                default: sb.Append("<color=#d0d0d0>Missing</color>"); break;
            }
            if (c == ItemCondition.Damaged) sb.Append(", <color=#ffa04d>Damaged</color>");
        }

        // The panel's width in this view. HudToasts reads it to keep the toasts clear of it.
        public static float Width(Rect view, int size)
        {
            return Mathf.Min(WidthAt1080 * size / 16f, view.width * MaxViewShare);
        }

        public void Draw(Rect view, int size)
        {
            if (footer.Length == 0) return;   // not built yet
            float scale = size / 16f;
            float lineH = size * 1.3f;
            float w = Width(view, size);
            float pad = 8f * scale;
            float h = pad * 2f + lineH * (1 + itemLines + footerLines) + pad;
            Rect box = ViewportGUI.Region(view, HudRegion.TopLeft, w, h);
            ViewportGUI.Panel(box);

            float x = box.x + pad, y = box.y + pad, inner = box.width - pad * 2f;
            ViewportGUI.Label(new Rect(x, y, inner, lineH), Title, size, Color.white);
            ViewportGUI.Label(new Rect(x, y, inner, lineH), clock, size, clockColor, TextAnchor.UpperRight, true);
            y += lineH;

            float listH = lineH * itemLines;
            float nameW = inner * NameColumn;
            ViewportGUI.Label(new Rect(x, y, nameW, listH + lineH), names, size, Color.white);
            ViewportGUI.Label(new Rect(x + nameW, y, inner - nameW, listH + lineH), states, size, Color.white);
            y += listH + pad;

            ViewportGUI.Label(new Rect(x, y, inner, lineH * footerLines + lineH), footer, size, Color.white);
        }

        public static string Money(int v)
        {
            return (v < 0 ? "-$" : "$") + Mathf.Abs(v).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
