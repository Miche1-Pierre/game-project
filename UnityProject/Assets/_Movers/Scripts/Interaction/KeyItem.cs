using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A key. It is an ordinary MovableObject (picked up, thrown, pocketed like anything else)
    // that also opens every DoorLock with the same keyId. Nothing else about it is special: no
    // inventory, no key ring. Who has it is read from the world, every time:
    //   - in the hands: the crew member's Held is this item;
    //   - on the body: the item sits anywhere under the crew member (a pocket keeps its item as
    //     an inactive child of the player), so it works whatever the pockets look like inside.
    //
    // On the body it unlocks (the door's E reads "Unlock"). In the hands it can also lock: the
    // Alt button (F, Y on a pad) aimed at a shut door the key fits turns the key, whichever way
    // the lock is. That is the key's verb, not the door's, so E stays "Open" for the one
    // carrying the key and the door prompt adds "[F] Lock" after it.
    //
    // Put it on the "Keys" MovableObject of the house (0.2 kg, on the hall table).
    [RequireComponent(typeof(MovableObject))]
    [DisallowMultipleComponent]
    public sealed class KeyItem : MonoBehaviour
    {
        public string keyId = DoorLock.HouseKey;

        MovableObject item;
        Vector3 homePosition;
        Quaternion homeRotation;

        // Registered from Awake to OnDestroy, not while enabled: a key in a pocket is inactive,
        // and it still opens doors.
        static readonly List<KeyItem> all = new List<KeyItem>();

        public static IReadOnlyList<KeyItem> All => all;
        public MovableObject Item => item;

        void Awake()
        {
            item = GetComponent<MovableObject>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDestroy() { all.Remove(this); }

        // Runs only while the key is active: in the hands or lying about. In a pocket it is
        // switched off, and a key in a pocket turns nothing. After PlayerInteract (-100), so
        // its Target is this frame's.
        void Update()
        {
            if (!Net.HasAuthority) return;   // online, the host turns keys from the client's input
            CrewMember who = HeldBy();
            if (who == null || who.Input == null || who.Interact == null) return;
            CrewInput input = who.Input;
            // Down, then consumed only when a door took the turn: Alt on anything else is left
            // to whoever else wants it (wearing, drinking).
            if (!input.Down(CrewButton.Alt) || input.IsConsumed(CrewButton.Alt)) return;
            if (TurnIn(who.Interact.Target, who)) input.TryConsume(CrewButton.Alt);
        }

        // Turns this key, in this player's hands, in a door (a HingedPanel or the HingedGroup
        // around one). What the Alt button does; public so a test or a script can do it too.
        public bool TurnIn(Interactable door, CrewMember who)
        {
            if (door == null || !IsHeldBy(who)) return false;
            if (door is HingedPanel panel) return panel.TurnKey(who);
            if (door is HingedGroup group) return group.TurnKey(who);
            return false;
        }

        // The crew member holding this key in the hands, or null.
        CrewMember HeldBy()
        {
            if (item == null) return null;
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m != null && m.Held == item) return m;
            }
            return null;
        }

        public bool Fits(string id) => !string.IsNullOrEmpty(id) && keyId == id;

        public bool IsHeldBy(CrewMember who) => who != null && item != null && who.Held == item;

        public bool IsCarriedBy(CrewMember who)
        {
            if (who == null || item == null || item.destroyed) return false;
            return IsHeldBy(who) || transform.IsChildOf(who.transform);
        }

        // Does this crew member have a key for keyId, in the hands or on the body?
        public static bool Carries(CrewMember who, string keyId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                KeyItem k = all[i];
                if (k != null && k.Fits(keyId) && k.IsCarriedBy(who)) return true;
            }
            return false;
        }

        // In the hands only.
        public static bool Holds(CrewMember who, string keyId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                KeyItem k = all[i];
                if (k != null && k.Fits(keyId) && k.IsHeldBy(who)) return true;
            }
            return false;
        }

        public static KeyItem Find(string keyId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                KeyItem k = all[i];
                if (k != null && k.item != null && !k.item.destroyed && k.Fits(keyId)) return k;
            }
            return null;
        }

        // ---- the debug key (F8) ----

        // Puts the key in this crew member's hands, taking it from whoever held it. A key in
        // somebody's pocket stays there: the pockets own what is inside them. Hands already
        // full: the key drops in front of the player instead. Returns false when it could not
        // be moved at all.
        public bool GiveTo(CrewMember who)
        {
            if (who == null || item == null || item.destroyed) return false;
            if (IsHeldBy(who)) return true;
            if (item.inPocket || !gameObject.activeInHierarchy) return false;
            LetGo();

            // In front of the eyes, so the carry does not pull it through a wall to get there.
            Place(who.EyePosition + who.LookDirection * 0.5f, homeRotation);
            if (who.Grab != null && who.Held == null) who.Grab.Hold(item);
            return true;
        }

        // Back where the scene put it. false while it rides in a pocket.
        public bool ReturnHome()
        {
            if (item == null || item.destroyed) return false;
            if (item.inPocket || !gameObject.activeInHierarchy) return false;
            LetGo();
            Place(homePosition, homeRotation);
            return true;
        }

        // Out of whoever's hands it is in. MovableObject.holder says who, once the player code
        // fills it; the roster is asked as well, so a carry that did not fill it is not left
        // pulling the key back through the house.
        void LetGo()
        {
            if (item.holder != null) item.holder.Release(false);
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m != null && m.Grab != null && m.Held == item) m.Grab.Release(false);
            }
        }

        void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            NetTransforms.Snap(gameObject);   // host: the client's copy jumps there too
            Rigidbody rb = item.rb != null ? item.rb : GetComponent<Rigidbody>();
            if (rb == null) return;
            rb.position = position;
            rb.rotation = rotation;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }
    }
}
