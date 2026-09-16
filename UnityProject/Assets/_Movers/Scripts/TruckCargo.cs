using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A trigger volume placed inside the truck bed. Anything inside counts as loaded.
    // No magic inventory: the object physically has to be in the truck.
    public class TruckCargo : MonoBehaviour
    {
        public readonly HashSet<MovableObject> inside = new HashSet<MovableObject>();

        void OnTriggerEnter(Collider other)
        {
            var m = other.GetComponentInParent<MovableObject>();
            if (m != null) { inside.Add(m); m.loaded = true; }
        }

        void OnTriggerExit(Collider other)
        {
            var m = other.GetComponentInParent<MovableObject>();
            if (m != null) { inside.Remove(m); m.loaded = false; }
        }
    }
}
