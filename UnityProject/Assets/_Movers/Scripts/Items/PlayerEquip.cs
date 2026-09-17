using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Wearing what you just stole. Goes on the player, next to PlayerGrab.
    //
    // One key, two jobs, split by whether your hands are full, which is the same trick the
    // right button already plays between throwing and the cigarette, and the wheel plays
    // between roll and reach:
    //   hands full  -> put on what you are holding, if it is something you can wear
    //   hands empty -> take off the last thing you put on, and drop it at your feet
    //
    // No inventory, no menu, no UI. An equippable is an ordinary object of the house that you
    // picked up with the grab that already existed (05_ART/CHARACTERS.md).
    public class PlayerEquip : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        public PlayerGrab grab;
        public CrewEquip body;

        [Header("Input")]
        public KeyCode equipKey = KeyCode.F;

        // The order things went on, so taking off is last on first off. CrewEquip knows what is
        // worn, it does not know in which order, and order is what an undo key needs.
        readonly List<EquipSlot> order = new List<EquipSlot>();

        void Awake()
        {
            if (grab == null) grab = GetComponent<PlayerGrab>();
            if (body == null) body = GetComponentInChildren<CrewEquip>();

            if (grab == null || body == null)
            {
                Debug.LogWarning("[PlayerEquip] no PlayerGrab or no CrewEquip under " + name + ", nothing can be worn.");
                enabled = false;
            }
        }

        void Update()
        {
            if (!Input.GetKeyDown(equipKey)) return;

            var held = grab.Held;
            if (held != null) Wear(held);
            else TakeOffLast();
        }

        void Wear(MovableObject held)
        {
            var item = held.GetComponent<EquipItem>();
            if (item == null) return;   // most things in a house are not clothes

            // Let go first, cleanly: Release restores the damping the carry had saved, so the
            // object does not come back later with the carry values baked into it.
            grab.Release(false);

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
