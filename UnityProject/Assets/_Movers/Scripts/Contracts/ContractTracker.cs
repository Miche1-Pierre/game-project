using System.Collections.Generic;

namespace Movers
{
    // Where an object on the list is. Damage is a separate axis: a damaged sofa can still be
    // loaded, so "Damaged" is never a location (A5b 4.3).
    public enum ItemLocation { Missing, Loaded, Delivered, Pocketed, Worn }
    public enum ItemCondition { Intact, Damaged, Destroyed }

    // The contract's list and the state of each object on it, read from the object flags that
    // the truck (loaded), the pockets (inPocket), the body (worn) and the destruction (broken,
    // destroyed) already keep. Nothing here is a second copy of those facts, so it cannot drift
    // from them. The one fact it owns is Delivered. Plain C#, owned by ContractManager.
    public sealed class ContractTracker
    {
        readonly List<MovableObject> required = new List<MovableObject>();
        readonly HashSet<MovableObject> delivered = new HashSet<MovableObject>();

        public ContractTracker(MovableObject[] all)
        {
            if (all == null) return;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].requiredForContract) required.Add(all[i]);
        }

        // Everything on the list, destroyed ones included (they are on the bill).
        public IReadOnlyList<MovableObject> Required => required;
        public int Total => required.Count;

        public ItemCondition ConditionOf(MovableObject m)
        {
            if (m == null || m.destroyed) return ItemCondition.Destroyed;
            return m.broken ? ItemCondition.Damaged : ItemCondition.Intact;
        }

        public ItemLocation LocationOf(MovableObject m)
        {
            if (m == null) return ItemLocation.Missing;
            if (delivered.Contains(m)) return ItemLocation.Delivered;
            if (m.worn) return ItemLocation.Worn;
            if (m.inPocket) return ItemLocation.Pocketed;
            if (m.loaded) return ItemLocation.Loaded;
            return ItemLocation.Missing;
        }

        // Objects on the list that still exist: a destroyed one no longer blocks delivery.
        public int Remaining
        {
            get
            {
                int n = 0;
                for (int i = 0; i < required.Count; i++) if (IsAlive(required[i])) n++;
                return n;
            }
        }

        public int RemainingLoaded
        {
            get
            {
                int n = 0;
                for (int i = 0; i < required.Count; i++)
                    if (IsAlive(required[i]) && (required[i].loaded || delivered.Contains(required[i]))) n++;
                return n;
            }
        }

        public int DestroyedCount => Total - Remaining;

        // The client bills the crew for every object on the list they smashed.
        public int DestroyedValue
        {
            get
            {
                int v = 0;
                for (int i = 0; i < required.Count; i++)
                    if (required[i] != null && required[i].destroyed) v += required[i].contractValue;
                return v;
            }
        }

        // There was a list, and every object on it that still exists is in the truck. Smashing
        // all of them also "finishes" the job, at a price: that is the joke, not a bug.
        public bool AllRemainingLoaded
        {
            get
            {
                if (required.Count == 0) return false;
                for (int i = 0; i < required.Count; i++)
                {
                    var m = required[i];
                    if (IsAlive(m) && !m.loaded && !delivered.Contains(m)) return false;
                }
                return true;
            }
        }

        // The truck is handed over: what is in it is delivered.
        public void MarkDelivered()
        {
            for (int i = 0; i < required.Count; i++)
                if (IsAlive(required[i]) && required[i].loaded) delivered.Add(required[i]);
        }

        public static string Word(ItemLocation l)
        {
            switch (l)
            {
                case ItemLocation.Loaded: return "Loaded";
                case ItemLocation.Delivered: return "Delivered";
                case ItemLocation.Pocketed: return "Pocketed";
                case ItemLocation.Worn: return "Worn";
                default: return "Missing";
            }
        }

        static bool IsAlive(MovableObject m) => m != null && !m.destroyed;
    }
}
