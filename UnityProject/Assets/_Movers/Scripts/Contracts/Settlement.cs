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
        // Completed through the exit with the police after the crew (EscapeMission), not delivered.
        public bool Escaped { get; private set; }
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

        // Got away from the police (DEV2 8.6). The contract is void; what the truck carries, and
        // what the members aboard carry of hers, is sold at escapeCargoPayFraction of what a
        // delivery would pay for it. What she saw taken is confiscated (no fine: the police have
        // it). The bills stay: the list objects destroyed, the break-in. Each arrested member,
        // and each one left behind, is fined. So for the same load a clean delivery always pays
        // about twice as much: calling the police is never the plan.
        // aboard: bit i, crew member i was aboard at the exit. arrested: how many are fined.
        public static Settlement ForEscape(ContractTracker tracker, TheftLedger ledger, bool brokeIn,
                                           string breakInWhat, int aboard, int arrested, GameLoopNumbers n)
        {
            var s = new Settlement { Completed = true, Escaped = true, Failure = FailReason.None };
            var sold = new Names(); var destroyed = new Names(); var seen = new Names();
            int sale = 0, destroyedBill = 0;

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
                    if (!InTruck(tracker.LocationOf(m))) continue;
                    sale += ListPay(m, c, n);
                    sold.Add(m);
                }
            }
            if (ledger != null)
            {
                var entries = ledger.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    var en = entries[i];
                    if (en.witnessed) { seen.Add(en.item); continue; }
                    if (!Escapes(en, aboard)) continue;
                    sale += TheftPay(en, n);
                    sold.Add(en.item);
                }
            }

            s.Add("You got away: the contract is void", "", 0);
            s.Add("Sold from the truck: " + sold.Count, sold.Text, EscapeShare(sale, n));
            if (seen.Count > 0) s.Add("Confiscated: " + seen.Count + " she saw you take", seen.Text, 0);
            if (destroyed.Count > 0) s.Add("Billed: " + destroyed.Count + " on the list destroyed", destroyed.Text, -destroyedBill);
            if (brokeIn) s.Add("Break-in (" + (string.IsNullOrEmpty(breakInWhat) ? "damage" : breakInWhat) + ")", "", -n.breakInCost);
            if (arrested > 0) s.Add("Fine: " + arrested + " arrested", "", -n.finePerArrest * arrested);
            return s;
        }

        // The HUD's running "Money" during the flee: what ForEscape would pay if the truck reached
        // the exit now with every free member aboard. Same rules, no allocation.
        public static int ProjectedEscape(ContractTracker tracker, TheftLedger ledger, bool brokeIn, GameLoopNumbers n)
        {
            int sale = 0, bills = 0;
            if (tracker != null)
            {
                var list = tracker.Required;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    ItemCondition c = tracker.ConditionOf(m);
                    if (c == ItemCondition.Destroyed) bills += ListPay(m, c, n);
                    else if (InTruck(tracker.LocationOf(m))) sale += ListPay(m, c, n);
                }
            }
            if (ledger != null)
            {
                int free = ~Session.ArrestedMask;
                var entries = ledger.Entries;
                for (int i = 0; i < entries.Count; i++)
                    if (!entries[i].witnessed && Escapes(entries[i], free)) sale += TheftPay(entries[i], n);
            }
            int total = EscapeShare(sale, n) + bills;
            if (brokeIn) total -= n.breakInCost;
            int arrested = 0;
            for (int i = 0; i < 8; i++) if (Session.IsArrested(i)) arrested++;
            return total - n.finePerArrest * arrested;
        }

        static bool InTruck(ItemLocation l) => l == ItemLocation.Loaded || l == ItemLocation.Delivered;

        // One thing of hers leaves with the escape: in the truck, or on a member aboard.
        static bool Escapes(TheftEntry en, int aboard)
        {
            if (en == null || en.item == null) return false;
            if (en.route == TheftRoute.Truck) return true;
            int carrier = TheftLedger.CarrierOf(en.item);
            return carrier >= 0 && carrier < 31 && (aboard & (1 << carrier)) != 0;
        }

        static int EscapeShare(int sale, GameLoopNumbers n) => Mathf.RoundToInt(sale * n.escapeCargoPayFraction);

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
        public static Settlement ForFailure(FailReason reason, TheftLedger ledger) => ForFailure(reason, ledger, false);

        // surrounded: an Intercepted run ended by the flee timer (the house surrounded), not by a
        // car stopping the truck.
        public static Settlement ForFailure(FailReason reason, TheftLedger ledger, bool surrounded)
        {
            var s = new Settlement { Completed = false, Failure = reason };
            string why;
            switch (reason)
            {
                case FailReason.TimeUp: why = "Time is up: the contract is void"; break;
                case FailReason.PoliceCalled: why = "The police came: the contract is void"; break;
                case FailReason.Intercepted:
                    why = surrounded ? "The police surrounded the house: the contract is void"
                                     : "The police stopped the truck: the contract is void";
                    break;
                case FailReason.CrewArrested: why = "Everyone was arrested: the contract is void"; break;
                default: why = "The contract is void"; break;
            }
            s.Add(why, "", 0);
            if (ledger != null && ledger.Entries.Count > 0)
            {
                var taken = new Names();
                for (int i = 0; i < ledger.Entries.Count; i++) taken.Add(ledger.Entries[i].item);
                s.Add("Her things you had to leave: " + taken.Count, taken.Text, 0);
            }
            return s;
        }

        // Online client (NETCODE_SLICE 11.7): the host's lines, verbatim (SettlementText parses
        // the English strings). The total is their sum, as on the host.
        public static Settlement FromReplica(bool completed, FailReason failure, IReadOnlyList<Line> lines) =>
            FromReplica(completed, false, failure, lines);

        public static Settlement FromReplica(bool completed, bool escaped, FailReason failure, IReadOnlyList<Line> lines)
        {
            var s = new Settlement { Completed = completed, Escaped = escaped, Failure = failure };
            if (lines != null)
                for (int i = 0; i < lines.Count; i++) s.Add(lines[i].label, lines[i].detail, lines[i].amount);
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
