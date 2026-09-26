using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Movers
{
    // The end-of-run bill, line by line, so the end screen can say why the crew got what it got
    // (SLICE_ARCHITECTURE section 6). The total is the sum of the lines by construction: a line
    // is the only way money gets in.
    public sealed class Settlement
    {
        public struct Line
        {
            public string label;    // "Contract pay: 9 delivered"
            public string detail;   // the names, may be empty
            public int amount;      // signed; 0 for a line that only informs (confiscated)
        }

        readonly List<Line> lines = new List<Line>();

        public IReadOnlyList<Line> Lines => lines;
        public int Total { get; private set; }
        public bool Completed { get; private set; }
        public FailReason Failure { get; private set; }

        void Add(string label, string detail, int amount)
        {
            lines.Add(new Line { label = label, detail = detail ?? "", amount = amount });
            Total += amount;
        }

        // Delivered. tracker or ledger may be null (a scene without a contract, a forced end).
        public static Settlement ForDelivery(ContractTracker tracker, TheftLedger ledger,
                                             bool brokeIn, string breakInWhat, GameLoopNumbers n)
        {
            var s = new Settlement { Completed = true, Failure = FailReason.None };
            var intact = new Names(); var damaged = new Names(); var destroyed = new Names(); var missing = new Names();
            int intactPay = 0, damagedPay = 0, destroyedBill = 0;

            if (tracker != null)
            {
                var list = tracker.Required;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    ItemCondition c = tracker.ConditionOf(m);
                    if (c == ItemCondition.Destroyed)
                    {
                        destroyedBill -= ListPay(m, c, n);
                        destroyed.Add(m);
                        continue;
                    }
                    if (tracker.LocationOf(m) != ItemLocation.Delivered) { missing.Add(m); continue; }
                    if (c == ItemCondition.Damaged)
                    {
                        damagedPay += ListPay(m, c, n);
                        damaged.Add(m);
                    }
                    else
                    {
                        intactPay += ListPay(m, c, n);
                        intact.Add(m);
                    }
                }
            }

            s.Add("Contract pay: " + intact.Count + " delivered intact", intact.Text, intactPay);
            if (damaged.Count > 0) s.Add("Part pay: " + damaged.Count + " delivered damaged", damaged.Text, damagedPay);
            if (missing.Count > 0) s.Add("Not delivered: " + missing.Count + " left behind", missing.Text, 0);
            if (destroyed.Count > 0) s.Add("Billed: " + destroyed.Count + " on the list destroyed", destroyed.Text, -destroyedBill);

            if (ledger != null)
            {
                var unseen = new Names(); var seen = new Names();
                int unseenPay = 0, fine = 0;
                var entries = ledger.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    var en = entries[i];
                    if (en.witnessed)
                    {
                        fine -= TheftPay(en, n);
                        seen.Add(en.item);
                    }
                    else
                    {
                        unseenPay += TheftPay(en, n);
                        unseen.Add(en.item);
                    }
                }
                if (unseen.Count > 0) s.Add("Her things nobody saw you take: " + unseen.Count, unseen.Text, unseenPay);
                if (seen.Count > 0)
                {
                    s.Add("Confiscated: " + seen.Count + " she saw you take", seen.Text, 0);
                    s.Add("Fine for what she saw", "", -fine);
                }
            }

            if (brokeIn) s.Add("Break-in (" + (string.IsNullOrEmpty(breakInWhat) ? "damage" : breakInWhat) + ")", "", -n.breakInCost);
            return s;
        }

        // The total the settlement would show if the truck were handed over now, what is loaded
        // counting as delivered: the HUD's running "Money". Same rules as ForDelivery, so the
        // figure on the panel and the one on the end screen cannot disagree. No allocation.
        public static int Projected(ContractTracker tracker, TheftLedger ledger, bool brokeIn, GameLoopNumbers n)
        {
            int total = 0;
            if (tracker != null)
            {
                var list = tracker.Required;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    ItemCondition c = tracker.ConditionOf(m);
                    ItemLocation l = tracker.LocationOf(m);
                    if (c == ItemCondition.Destroyed || l == ItemLocation.Loaded || l == ItemLocation.Delivered)
                        total += ListPay(m, c, n);
                }
            }
            if (ledger != null)
            {
                var entries = ledger.Entries;
                for (int i = 0; i < entries.Count; i++) total += TheftPay(entries[i], n);
            }
            if (brokeIn) total -= n.breakInCost;
            return total;
        }

        // ---- the rules, in one place ----

        // One object on the list: its value delivered intact, a share of it damaged, a bill
        // (negative) when it was destroyed.
        static int ListPay(MovableObject m, ItemCondition c, GameLoopNumbers n)
        {
            int v = m != null ? m.contractValue : 0;
            switch (c)
            {
                case ItemCondition.Destroyed: return -Mathf.RoundToInt(v * n.destroyedBillFraction);
                case ItemCondition.Damaged: return Mathf.RoundToInt(v * n.damagedPayFraction);
                default: return v;
            }
        }

        // What one thing of hers is worth once taken. A damaged one is worth the share a damaged
        // object on the list pays: the same crack costs the same whether the thing leaves on
        // the list or in a pocket, so a broken vase is never worth more stolen than delivered.
        public static int TheftWorth(TheftEntry en, GameLoopNumbers n)
        {
            if (en == null) return 0;
            return en.Damaged ? Mathf.RoundToInt(en.value * n.damagedPayFraction) : en.value;
        }

        // One thing of hers the crew took: its worth when nobody saw, a fine (negative) on that
        // worth when she did. The item itself is confiscated then, so it pays nothing.
        static int TheftPay(TheftEntry en, GameLoopNumbers n)
        {
            int worth = TheftWorth(en, n);
            return en.witnessed ? -Mathf.RoundToInt(worth * n.witnessedFineFraction)
                                : Mathf.RoundToInt(worth * n.unseenTheftPayFraction);
        }

        // Failed: the contract is void and nothing is paid. The lines say what was lost.
        public static Settlement ForFailure(FailReason reason, TheftLedger ledger)
        {
            var s = new Settlement { Completed = false, Failure = reason };
            string why = reason == FailReason.TimeUp ? "Time is up: the contract is void"
                       : reason == FailReason.PoliceCalled ? "The police came: the contract is void"
                       : "The contract is void";
            s.Add(why, "", 0);
            if (ledger != null && ledger.Entries.Count > 0)
            {
                var taken = new Names();
                for (int i = 0; i < ledger.Entries.Count; i++) taken.Add(ledger.Entries[i].item);
                s.Add("Her things you had to leave: " + taken.Count, taken.Text, 0);
            }
            return s;
        }

        // "Sofa, Armchair, Lamp and 4 more". Built once per settlement, never per frame.
        sealed class Names
        {
            const int Shown = 5;
            readonly StringBuilder sb = new StringBuilder();
            public int Count { get; private set; }

            public void Add(MovableObject m)
            {
                Count++;
                if (Count > Shown) return;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(m != null ? m.displayName : "something");
            }

            public string Text => Count > Shown ? sb + " and " + (Count - Shown) + " more" : sb.ToString();
        }
    }
}
