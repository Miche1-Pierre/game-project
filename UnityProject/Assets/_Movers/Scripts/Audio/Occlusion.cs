using UnityEngine;

namespace Movers
{
    // How many walls and floors stand between a sound and the ears that hear it. One ray,
    // every hit counted: the house's walls, floors, roof and window glass. What moves does not
    // count (a box in the way does not muffle a voice), nor do players, the grandmother,
    // debris or triggers.
    public static class Occlusion
    {
        const int MaxHits = 12;
        static readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        static int mask = 0;

        public static int Mask
        {
            get
            {
                if (mask == 0) mask = BuildMask();
                return mask;
            }
        }

        // Walls between `from` and `to`, 0 to 4. `ignore` is the sound's own body (her
        // CharacterController, the truck), whose colliders the ray would otherwise start in.
        public static int WallsBetween(Vector3 from, Vector3 to, Transform ignore)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 1f) return 0;
            // Stop 30 cm short: a sound on a wall (a door latch) is not behind that wall.
            int n = Physics.RaycastNonAlloc(from, d / dist, hits, dist - 0.3f, Mask, QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c is CharacterController) continue;
                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                // Online client: replicated bodies are kinematic there, but they are things that
                // move, as on the host.
                if (rb != null && Net.IsClient && NetIds.IdOf(rb.gameObject) != 0) continue;
                if (ignore != null && c.transform.IsChildOf(ignore)) continue;
                walls++;
            }
            return walls > 4 ? 4 : walls;
        }

        // Everything but the layers of things that are not the building. Layers are looked
        // up by name (SLICE_ARCHITECTURE section 10); a missing one is simply not excluded.
        static int BuildMask()
        {
            int m = Physics.DefaultRaycastLayers;
            m &= ~LayerBit("Crew");
            m &= ~LayerBit("NPC");
            m &= ~LayerBit("Props");
            m &= ~LayerBit("Debris");
            m &= ~LayerBit("Water");
            m &= ~LayerBit("UI");
            return m == 0 ? Physics.DefaultRaycastLayers : m;
        }

        static int LayerBit(string name)
        {
            int l = LayerMask.NameToLayer(name);
            return l >= 0 ? 1 << l : 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { mask = 0; }
    }
}
