using UnityEngine;

namespace Movers
{
    // One pre-fractured piece of a wall, while the wall is broken up. A plain class, not a
    // component: a fractured wall has a dozen of these and they need no Update, no messages,
    // nothing Unity does for a MonoBehaviour. The one message a chunk must hear (a collision)
    // comes through ChunkCollisionRelay.
    public sealed class DestructibleChunk
    {
        public DestructibleModule Owner { get; }
        public int Index { get; }
        public Transform Transform { get; }
        public Collider Collider { get; }
        public Renderer Renderer { get; }
        public float Mass { get; }
        public float MaxHealth { get; }
        public float Health { get; internal set; }
        public DestructionState State { get; internal set; }

        // Still part of the wall. False once it broke off or fell: it is debris then (or gone),
        // and the wall no longer answers for it.
        public bool Attached { get; internal set; } = true;
        // Its node in the StructureGraph, -1 when the wall has none.
        public int Node { get; internal set; } = -1;

        // Which faces of the wall it reaches, from the sidecar or measured.
        internal bool bottom, top, sideNeg, sidePos;
        internal bool Border => bottom || top || sideNeg || sidePos;

        internal DestructibleChunk(DestructibleModule owner, int index, Transform t, Collider c, Renderer r,
                                   float mass, float maxHealth)
        {
            Owner = owner;
            Index = index;
            Transform = t;
            Collider = c;
            Renderer = r;
            Mass = mass;
            MaxHealth = maxHealth;
            Health = maxHealth;
        }

        // World box. Attached chunks never move, so this is stable while it matters.
        public Bounds Bounds
        {
            get
            {
                if (Collider != null && Collider.enabled && Collider.gameObject.activeInHierarchy) return Collider.bounds;
                if (Renderer != null) return Renderer.bounds;
                return new Bounds(Transform != null ? Transform.position : Vector3.zero, Vector3.zero);
            }
        }
    }
}
