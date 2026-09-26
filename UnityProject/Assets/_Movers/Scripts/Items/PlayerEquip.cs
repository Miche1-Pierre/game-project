using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The alt button (F, Y on a pad): wearing what you just stole, or drinking what you carry.
    // Goes on the player, next to PlayerGrab.
    //
    // One button, split by what is in your hands, which is the same trick the right button
    // already plays between throwing and the cigarette, and the wheel plays between roll and
    // reach:
    //   holding a wearable        -> press: put it on
    //   holding an alt-use item   -> hold: use it (the beer drinks while the button is down)
    //   hands empty               -> press: take off the last thing you put on, drop it at your feet
    //
    // No inventory, no menu, no UI. An equippable is an ordinary object of the house that you
    // picked up with the grab that already existed (05_ART/CHARACTERS.md).
    public class PlayerEquip : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        public PlayerGrab grab;
        public CrewEquip body;

        // The order things went on, so taking off is last on first off. CrewEquip knows what is
        // worn, it does not know in which order, and order is what an undo button needs.
        readonly List<EquipSlot> order = new List<EquipSlot>();

        CrewInput input;
        // The item being used with the alt button right now, told when that stops.
        HeldUsable altInUse;
        // Looked up when the hands change, not every frame.
        MovableObject lastHeld;
        HeldUsable heldUsable;
        EquipItem heldWearable;

        void Awake()
        {
            input = CrewSetup.InputOf(gameObject);
            if (grab == null) grab = GetComponent<PlayerGrab>();
            if (body == null) body = GetComponentInChildren<CrewEquip>();

            // No body (the tutorial's bare capsule) means nothing can be worn; the beer still
            // drinks, so the component stays on.
            if (grab == null)
            {
                Debug.LogWarning("[PlayerEquip] no PlayerGrab on " + name + ", nothing can be worn or drunk.");
                enabled = false;
            }
        }

        void Update()
        {
            var held = grab.Held;
            if (!ReferenceEquals(held, lastHeld))
            {
                lastHeld = held;
                heldUsable = held != null ? held.GetComponent<HeldUsable>() : null;
                heldWearable = held != null ? held.GetComponent<EquipItem>() : null;
            }

            // Held, not pressed: a bottle picked up with the button already down drinks at
            // once, as it always did.
            HeldUsable alt = held != null && heldWearable == null && heldUsable != null && heldUsable.UsesAltButton
                             && input.Held(CrewButton.Alt) ? heldUsable : null;
            if (!ReferenceEquals(alt, altInUse))
            {
                if (altInUse != null) altInUse.OnAltRelease();
                altInUse = alt;
            }
            if (alt != null) alt.OnAltHold(Time.deltaTime);

            if (!input.Down(CrewButton.Alt) || body == null) return;
            if (held == null) TakeOffLast();
            else if (heldWearable != null) Wear(held, heldWearable);
        }

        void OnDisable()
        {
            if (altInUse != null) altInUse.OnAltRelease();
            altInUse = null;
        }

        void Wear(MovableObject held, EquipItem item)
        {
            // Let go first, cleanly: Release restores the damping the carry had saved, so the
            // object does not come back later with the carry values baked into it. Silent: it
            // is not dropped, it goes on the body.
            grab.Release(false, false);

            if (!body.Equip(item)) return;

            order.Remove(item.slot);
            order.Add(item.slot);
        }

        void TakeOffLast()
        {
            if (order.Count == 0) return;

            var slot = order[order.Count - 1];
            order.RemoveAt(order.Count - 1);

            var item = body.Unequip(slot);
            if (item == null) return;

            // It falls where you are standing. Putting it back in your hands would need the
            // grab to accept an object it did not raycast, and a dressing gown on the floor is
            // a fine place for a dressing gown.
            item.transform.position = transform.position + transform.forward * 0.5f + Vector3.up * 0.2f;
        }
    }
}
