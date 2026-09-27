using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Wears things. Goes on a crew body, meaning the GameObject that carries the Animator whose
    // avatar is Humanoid.
    //
    // Why this is so small: the character imports with optimizeGameObjects off, so the bone
    // transforms exist in the scene and a worn piece is nothing more than a mesh parented to
    // one of them. No skinning, no second renderer, no mesh combining. Only a piece that
    // crosses a joint that really bends has to be skinned, and in the first lot that is the
    // dressing gown alone (05_ART/CHARACTERS.md).
    //
    // Anchors resolve through the Humanoid avatar rather than by bone name, so this survives
    // changing character pack.
    public class CrewEquip : MonoBehaviour
    {
        public Animator animator;

        // What is worn, and what to put back when it comes off.
        class Worn
        {
            public EquipItem item;
            public Transform originalParent;
            public bool wasKinematic;
            public bool hadGravity;
            public Collider[] colliders;
            // An imported FBX can carry a compensation on its root scale, and assigning over it
            // is how the slippers first arrived one millimetre long. Kept, multiplied, restored.
            public Vector3 scale;
            public Vector3 secondScale;
            // Same story for rotation: an FBX exported without baking the axis conversion
            // carries it on the root, and assigning a world rotation lays the piece on its
            // back. Composed with, not replaced.
            public Quaternion rotation;
            public Quaternion secondRotation;
        }

        readonly Dictionary<EquipSlot, Worn> worn = new Dictionary<EquipSlot, Worn>();

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[CrewEquip] no humanoid Animator under " + name + ", nothing can be worn.");
                enabled = false;
            }
        }

        public bool IsWearing(EquipSlot slot) => worn.ContainsKey(slot);

        public EquipItem WornIn(EquipSlot slot) => worn.TryGetValue(slot, out var w) ? w.item : null;

        // The bone a slot hangs from, or null. Chest walks down the spine because not every rig
        // maps UpperChest, and a missing bone should cost a warning rather than an exception.
        public Transform Anchor(EquipSlot slot, bool otherSide = false)
        {
            if (animator == null) return null;
            switch (slot)
            {
                case EquipSlot.Head:
                case EquipSlot.Face:  return Bone(HumanBodyBones.Head);
                // Not the ?? operator: it ignores the == overload Unity uses to report a
                // destroyed or unassigned object, so a fake null would pass straight through.
                case EquipSlot.Chest: return FirstMapped(HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine);
                case EquipSlot.Waist: return Bone(HumanBodyBones.Hips);
                case EquipSlot.Hands: return Bone(otherSide ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                case EquipSlot.Feet:  return Bone(otherSide ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            }
            return null;
        }

        // Unity reports an unmapped bone as null, so this is the one place that check lives.
        Transform Bone(HumanBodyBones b)
        {
            var t = animator.GetBoneTransform(b);
            return t == null ? null : t;
        }

        Transform FirstMapped(params HumanBodyBones[] candidates)
        {
            foreach (var b in candidates)
            {
                var t = Bone(b);
                if (t != null) return t;
            }
            return null;
        }

        // Put it on. Returns false and says why rather than failing silently: the likeliest
        // cause is an unmapped bone, and that is invisible from the scene view.
        public bool Equip(EquipItem item)
        {
            if (!enabled || item == null) return false;

            var anchor = Anchor(item.slot);
            if (anchor == null)
            {
                Debug.LogWarning("[CrewEquip] the avatar maps no bone for slot " + item.slot + ", " + item.name + " stays on the floor.");
                return false;
            }

            if (worn.ContainsKey(item.slot)) Unequip(item.slot);

            var mo = item.GetComponent<MovableObject>();
            var rb = mo != null && mo.rb != null ? mo.rb : item.GetComponent<Rigidbody>();
            if (mo != null)
            {
                // On a body is not in the truck, even if it was put on standing in the truck:
                // switched-off colliders send the cargo zone no exit (same as a pocket).
                mo.worn = true;
                if (Net.HasAuthority) PlayerPockets.LeaveTruck(mo);   // the client's cargo comes from the host
            }

            var record = new Worn
            {
                item = item,
                originalParent = item.transform.parent,
                colliders = item.GetComponentsInChildren<Collider>(),
                scale = item.transform.localScale,
                secondScale = item.secondPart != null ? item.secondPart.localScale : Vector3.one,
                rotation = item.transform.localRotation,
                secondRotation = item.secondPart != null ? item.secondPart.localRotation : Quaternion.identity,
            };

            // Physics off while worn. A gown with a live collider inside the player capsule
            // fights the CharacterController, and a worn object is not a physical object any
            // more, it is part of a body.
            if (rb != null)
            {
                record.wasKinematic = rb.isKinematic;
                record.hadGravity = rb.useGravity;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            foreach (var c in record.colliders) c.enabled = false;

            Fit(item.transform, anchor, item.localPosition, item.localEuler, record.scale, record.rotation, item.localScale);

            if (item.secondPart != null)
            {
                var other = Anchor(item.slot, true);
                if (other != null)
                    Fit(item.secondPart, other, item.secondLocalPosition, item.secondLocalEuler, record.secondScale, record.secondRotation, item.localScale);
            }

            worn[item.slot] = record;
            NetTransforms.Snap(item.gameObject);
            return true;
        }

        // Places a piece on a bone using the BODY frame, not the bone frame.
        //
        // Measured on this pack, 2026-09-17: the head bone is spine.005 and its local +Z points
        // at the floor, because bone axes are whatever the rig author did in Blender. An offset
        // authored in bone space is therefore unreadable and unportable: "4 cm up" put the
        // glasses 10 cm below the skull. Against the body, x is right, y is up, z is forward,
        // which is what anyone tuning a slipper in the inspector expects.
        //
        // The world pose is set after parenting, so Unity works out the local transform and the
        // piece still follows the bone when the head turns.
        void Fit(Transform piece, Transform anchor, Vector3 offset, Vector3 euler, Vector3 baseScale, Quaternion baseRotation, float scale)
        {
            piece.SetParent(anchor, false);
            piece.position = anchor.position
                           + transform.right * offset.x
                           + transform.up * offset.y
                           + transform.forward * offset.z;
            piece.rotation = transform.rotation * Quaternion.Euler(euler) * baseRotation;
            // Multiplied into whatever the import left there, never assigned over it.
            piece.localScale = baseScale * scale;
        }

        // Take it off. It becomes an ordinary object of the house again, where the body is.
        public EquipItem Unequip(EquipSlot slot)
        {
            if (!worn.TryGetValue(slot, out var w)) return null;
            worn.Remove(slot);

            var item = w.item;
            if (item == null) return null;

            if (item.secondPart != null)
            {
                item.secondPart.SetParent(item.transform, true);
                item.secondPart.localScale = w.secondScale;
                item.secondPart.localRotation = w.secondRotation;
            }

            item.transform.SetParent(w.originalParent, true);
            item.transform.localScale = w.scale;
            item.transform.localRotation = w.rotation;

            foreach (var c in w.colliders) if (c != null) c.enabled = true;

            var mo = item.GetComponent<MovableObject>();
            var rb = mo != null && mo.rb != null ? mo.rb : item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = w.wasKinematic;
                rb.useGravity = w.hadGravity;
            }
            if (mo != null) mo.worn = false;
            NetTransforms.Snap(item.gameObject);

            return item;
        }
    }
}
