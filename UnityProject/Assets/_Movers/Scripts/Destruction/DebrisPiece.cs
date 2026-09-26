using UnityEngine;

namespace Movers
{
    // Marks a fragment (MeshShatter's shards, a wall chunk that broke off, a fallen roof
    // section) and carries the little state DebrisManager needs to age it out, freeze it and
    // thaw it.
    //
    // Breakables, panes and walls look for this marker when something hits them. Light debris
    // does not count: a wall that falls apart must not grind every vase it lands on into more
    // debris, that is a chain reaction nobody can read and the fastest way to blow the piece
    // budget. Heavy debris does (a chunk of wall, a roof): that is a crush, and the one who blew
    // the wall out is to blame for it (instigator).
    [DisallowMultipleComponent]
    public class DebrisPiece : MonoBehaviour
    {
        // Bookkeeping owned by DebrisManager, not tuning: nothing here belongs in the inspector.
        internal Rigidbody rb;
        internal float born;
        internal float lifetime = 18f;
        internal float sleepTime;
        internal float shrinkStart = -1f;
        internal float shrinkDuration = 1f;
        internal Vector3 baseScale = Vector3.one;
        internal CollisionDetectionMode detectionMode = CollisionDetectionMode.Discrete;
        internal float size;                  // m, the extent of its box: small ones are culled first
        // Who broke it off, for what it crushes (see Actors).
        internal int instigator = Actors.World;
        internal BreakMaterial material = BreakMaterial.Plaster;
        // Never aged out, never culled: a fallen roof section stays where it landed.
        internal bool pinned;

        // The fragment mesh is built for this piece alone, so it dies with it. Box chunks use
        // Unity's shared cube and own nothing; wall chunks use their FBX's mesh.
        Mesh ownedMesh;

        public Rigidbody Body => rb;
        public bool Shrinking => shrinkStart >= 0f;
        public bool Frozen => rb != null && rb.isKinematic;
        public bool Pinned => pinned;
        public int Instigator => instigator;

        // Slower than this, a touch is something settling against the piece, not a push.
        const float BumpSpeed = 0.3f;

        internal void Init(Rigidbody body, Mesh mesh)
        {
            rb = body;
            ownedMesh = mesh;
            detectionMode = body != null ? body.collisionDetectionMode : CollisionDetectionMode.Discrete;
        }

        // A frozen piece is kinematic, which to everything else means immovable: left alone,
        // a shard on the floor would stop a dragged sofa like a kerb. So anything moving that
        // runs into it thaws it, and from the next physics step it gets knocked aside like any
        // other piece. Other debris does not count: that is the pile settling.
        void OnCollisionEnter(Collision c)
        {
            if (rb == null || !rb.isKinematic || Shrinking) return;
            Rigidbody other = c.rigidbody;
            if (other == null || other.isKinematic) return;
            if (other.TryGetComponent(out DebrisPiece _)) return;
            if (c.relativeVelocity.sqrMagnitude < BumpSpeed * BumpSpeed) return;
            DebrisManager.Thaw(this);
        }

        void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }
}
