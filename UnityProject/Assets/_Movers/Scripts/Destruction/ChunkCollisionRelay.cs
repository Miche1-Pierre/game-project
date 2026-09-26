using UnityEngine;

namespace Movers
{
    // Added to every chunk object when a wall breaks up. A static chunk hit by a moving body
    // gets the collision message itself (Unity sends it to both sides), so it passes it to its
    // wall, which measures the hit like any other.
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class ChunkCollisionRelay : MonoBehaviour
    {
        internal DestructibleModule owner;
        internal int index;

        void OnCollisionEnter(Collision c)
        {
            if (owner != null) owner.OnChunkCollision(index, c);
        }
    }
}
