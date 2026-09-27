using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    public enum TheftRoute { Pocket, Truck, Worn }

    public sealed class TheftEntry
    {
        public MovableObject item;
        public int thief = Actors.World;
        public TheftRoute route;
        public int value;
        public bool witnessed;
        public float since;

        // Broken after it was taken, or before: it sells for less (Settlement.TheftWorth).
        public bool Damaged => item != null && item.broken;
    }

    // What the crew has of hers that is not on the list (ADR-009, decision 5). Nothing here is
    // a theft yet: an entry is provisional while the item rides in a pocket, in the truck or on
    // a body, and goes away if it is put back. At delivery every entry left becomes final and
    // is announced as ItemStolen; the settlement pays the unseen ones and fines the others.
    //
    // Events say when something happened and who did it. The object flags (inPocket, loaded,
    // worn) say what is true now. Both are used: the events for the thief and for the witness,
    // a periodic scan to catch what no event reports (wearing raises none) or what an event
    // missed. Plain C#, owned by GameSession.
    public sealed class TheftLedger
    {
        readonly GameLoopNumbers numbers;
        readonly List<TheftEntry> entries = new List<TheftEntry>();
        readonly Dictionary<MovableObject, TheftEntry> byItem = new Dictionary<MovableObject, TheftEntry>();
        // She remembers. Seen taken once, an item stays "seen" for the rest of the run, even
        // put back and taken again out of her sight, and even when she saw it being carried
        // before it ever reached a pocket or the truck.
        readonly HashSet<MovableObject> seenTaken = new HashSet<MovableObject>();

        public IReadOnlyList<TheftEntry> Entries => entries;
        // What the taken things are worth now (damaged ones at their reduced worth), unseen and
        // seen: the HUD's "stolen so far", on the same rule as the settlement.
        public int UnseenValue { get; private set; }
        public int SeenValue { get; private set; }
        // Bumped on every change, so the HUD rebuilds its text only when something moved.
        public int Version { get; private set; }
        public bool IsFinal { get; private set; }

        public TheftLedger(GameLoopNumbers numbers)
        {
            this.numbers = numbers ?? GameLoopNumbers.Defaults;
        }

        // Hers, not on the list, worth something, and still an object rather than debris. The
        // crew's own cigarettes, beers and grenades are not hers (ownedByGrandma false), and are
        // worth nothing anyway.
        public static bool IsLoot(MovableObject m)
        {
            return m != null && m.IsTheftTarget && m.contractValue > 0 && !m.destroyed;
        }

        public bool Contains(MovableObject m) => m != null && byItem.ContainsKey(m);
        public bool WasSeen(MovableObject m) => m != null && seenTaken.Contains(m);

        public void Handle(in WorldEvent e)
        {
            if (IsFinal) return;
            MovableObject item = e.Item;
            switch (e.type)
            {
                case WorldEventType.ItemPocketed:
                    if (IsLoot(item)) Put(item, Thief(e.instigator, item), TheftRoute.Pocket);
                    break;
                case WorldEventType.ItemUnpocketed:
                    Drop(item, TheftRoute.Pocket);
                    break;
                case WorldEventType.CargoLoaded:
                    if (IsLoot(item)) Put(item, Thief(e.instigator, item), TheftRoute.Truck);
                    break;
                case WorldEventType.CargoUnloaded:
                    Drop(item, TheftRoute.Truck);
                    break;
                case WorldEventType.ObjectDestroyed:
                    // A smashed vase in the truck is a pile of shards, not a sale.
                    if (item != null && byItem.TryGetValue(item, out var gone)) Remove(gone);
                    break;
                case WorldEventType.ObjectDamaged:
                    // A cracked one sells for less: the running totals move with it.
                    if (item != null && byItem.ContainsKey(item)) Changed();
                    break;
                case WorldEventType.TheftWitnessed:
                    MarkSeen(item, e.instigator);
                    break;
            }
        }

        // Check every entry against the object flags and pick up what the events missed.
        // house: the objects of the house (ContractManager.allObjects), may be null.
        public void Reconcile(MovableObject[] house)
        {
            if (IsFinal) return;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var en = entries[i];
                if (!IsLoot(en.item)) { Remove(en); continue; }
                if (TryRouteOf(en.item, out TheftRoute r)) { if (r != en.route) { en.route = r; Changed(); } continue; }
                // A pocket that raised ItemPocketed but never set inPocket still leaves the
                // object switched off and parented to the player: trust the event.
                if (en.route == TheftRoute.Pocket && !en.item.gameObject.activeSelf) continue;
                Remove(en);
            }

            if (house == null) return;
            for (int i = 0; i < house.Length; i++)
            {
                var m = house[i];
                if (!IsLoot(m) || byItem.ContainsKey(m)) continue;
                if (TryRouteOf(m, out TheftRoute r)) Put(m, ThiefOf(m, r), r);
            }
        }

        // Delivery: what is still taken is stolen. Raises ItemStolen once per item.
        public void FinalizeAll(MovableObject[] house)
        {
            if (IsFinal) return;
            Reconcile(house);
            IsFinal = true;
            for (int i = 0; i < entries.Count; i++)
            {
                var en = entries[i];
                Vector3 at = en.item != null ? en.item.transform.position : Vector3.zero;
                WorldEvents.Raise(WorldEventType.ItemStolen, at, en.thief, 0f,
                                  en.witnessed ? 1f : 0f, Settlement.TheftWorth(en, numbers), en.item);
            }
        }

        // ---- online client (NETCODE_SLICE 11.7): the host's entries, applied as they are ----
        // No events and no scan: SessionSync writes what the host's ledger holds. The worth of
        // a damaged entry follows the replicated broken flag, like the host's.

        public void ApplyPut(MovableObject item, int thief, TheftRoute route, int value, bool witnessed)
        {
            if (item == null) return;
            if (!byItem.TryGetValue(item, out var en))
            {
                en = new TheftEntry { item = item, since = Time.time };
                entries.Add(en);
                byItem.Add(item, en);
            }
            en.thief = thief;
            en.route = route;
            en.value = value;
            en.witnessed = witnessed;
            if (witnessed) seenTaken.Add(item);
            Changed();
        }

        public void ApplyDrop(MovableObject item)
        {
            if (item != null && byItem.TryGetValue(item, out var en)) Remove(en);
        }

        public void ApplyFinal()
        {
            IsFinal = true;
            Changed();
        }

        void Put(MovableObject item, int thief, TheftRoute route)
        {
            if (byItem.TryGetValue(item, out var en))
            {
                en.route = route;
                if (Actors.IsPlayer(thief)) en.thief = thief;
            }
            else
            {
                en = new TheftEntry
                {
                    item = item,
                    thief = thief,
                    route = route,
                    value = item.contractValue,
                    witnessed = seenTaken.Contains(item),
                    since = Time.time,
                };
                entries.Add(en);
                byItem.Add(item, en);
            }
            Changed();
        }

        void Drop(MovableObject item, TheftRoute route)
        {
            if (item == null || !byItem.TryGetValue(item, out var en) || en.route != route) return;
            Remove(en);
        }

        void Remove(TheftEntry en)
        {
            entries.Remove(en);
            // By reference: an object Unity already destroyed compares equal to null, but its
            // key still sits in the dictionary under the same hash.
            if (!ReferenceEquals(en.item, null)) byItem.Remove(en.item);
            Changed();
        }

        void MarkSeen(MovableObject item, int thief)
        {
            if (item != null)
            {
                seenTaken.Add(item);
                if (byItem.TryGetValue(item, out var en) && !en.witnessed) { en.witnessed = true; Changed(); }
                return;
            }
            // No item named: the newest thing that thief took is what she saw.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var en = entries[i];
                if (en.thief != thief || en.witnessed) continue;
                en.witnessed = true;
                if (en.item != null) seenTaken.Add(en.item);
                Changed();
                return;
            }
        }

        void Changed()
        {
            int unseen = 0, seen = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                int worth = Settlement.TheftWorth(entries[i], numbers);
                if (entries[i].witnessed) seen += worth;
                else unseen += worth;
            }
            UnseenValue = unseen;
            SeenValue = seen;
            Version++;
        }

        static bool TryRouteOf(MovableObject m, out TheftRoute route)
        {
            route = TheftRoute.Pocket;
            if (m.worn) { route = TheftRoute.Worn; return true; }
            if (m.inPocket) { route = TheftRoute.Pocket; return true; }
            if (m.loaded) { route = TheftRoute.Truck; return true; }
            return false;
        }

        static int Thief(int instigator, MovableObject item)
        {
            if (Actors.IsPlayer(instigator)) return instigator;
            return item != null && Actors.IsPlayer(item.lastHandler) ? item.lastHandler : Actors.World;
        }

        // Worn and pocketed things ride on a body, so the body says who. The transform walk
        // (not GetComponentInParent) also works for a pocketed item, which is switched off.
        static int ThiefOf(MovableObject m, TheftRoute route)
        {
            if (route != TheftRoute.Truck)
            {
                for (Transform t = m.transform; t != null; t = t.parent)
                    if (t.TryGetComponent(out CrewMember member)) return member.index;
            }
            return Thief(Actors.World, m);
        }
    }
}
