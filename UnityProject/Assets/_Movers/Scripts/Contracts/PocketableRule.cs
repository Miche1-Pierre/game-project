using UnityEngine;

namespace Movers
{
    // Which objects of the house fit in a pocket. A rule rather than a tick box on each object:
    // the house's data table (_ArtSource/house_objects.txt, generated from the scene) has no
    // pocket column, and an object Pierre adds later should fit if it is small, without anyone
    // remembering to say so. The rule only ever says yes: an object marked pocketable by hand
    // (a valuable, the crew's own items) stays pocketable.
    public static class PocketableRule
    {
        public static int Apply(MovableObject[] items, float maxKg, float maxSide)
        {
            if (items == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Length; i++)
            {
                var m = items[i];
                if (m == null || m.pocketable || m.requiredForContract) continue;
                if (m.weight > maxKg || LargestSide(m) > maxSide) continue;
                m.pocketable = true;
                n++;
            }
            return n;
        }

        // The object's own box when it has one (every movable of the house does), because its
        // world bounds grow when it sits at an angle; otherwise the bounds of its colliders.
        public static float LargestSide(MovableObject m)
        {
            if (m.TryGetComponent(out BoxCollider box))
            {
                Vector3 s = Vector3.Scale(box.size, m.transform.lossyScale);
                return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            }
            bool any = false;
            Bounds b = default;
            foreach (var c in m.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; }
                else b.Encapsulate(c.bounds);
            }
            if (!any) return float.PositiveInfinity;
            return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        }
    }
}
