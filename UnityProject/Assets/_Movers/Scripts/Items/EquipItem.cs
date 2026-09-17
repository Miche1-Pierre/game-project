using UnityEngine;

namespace Movers
{
    // Which part of the body a piece occupies. One piece per slot, so a dressing gown and a
    // moving harness cannot be worn at once and the silhouette stays readable
    // (05_ART/CHARACTERS.md).
    //
    // Head and Face share a bone and differ only in where they sit on it. They are separate
    // slots because a hat and a pair of glasses are worn together.
    public enum EquipSlot { Head, Face, Chest, Hands, Waist, Feet }

    // Declares that an object of the house can be worn, and how it sits once it is.
    //
    // It lives on the same GameObject as MovableObject, deliberately: a wearable is an ordinary
    // object of the house first. You pick it up with the grab that already exists, it has a
    // weight and a contract value, and it can go in the truck instead of on your back. Wear it
    // or sell it is a real choice with no system behind it.
    //
    // The fit is authored here rather than computed. A greybox is tuned by dragging numbers in
    // the inspector until the slipper stops floating, and bone scale inheritance depends on how
    // the character pack was imported, so there is no correct value to compute up front.
    [RequireComponent(typeof(MovableObject))]
    public class EquipItem : MonoBehaviour
    {
        public EquipSlot slot = EquipSlot.Head;

        [Header("How it sits on the bone")]
        public Vector3 localPosition;
        public Vector3 localEuler;
        public float localScale = 1f;

        [Header("Symmetric slots: hands, feet")]
        // Slippers and gloves need a second mesh on the other side. The art ships a left and a
        // right, it is not mirrored in code: negating a scale axis flips the winding and the
        // piece lights inside out. A mirror modifier in Blender is free, a double-sided shader
        // is a material we said we would not add.
        public Transform secondPart;
        public Vector3 secondLocalPosition;
        public Vector3 secondLocalEuler;
    }
}
