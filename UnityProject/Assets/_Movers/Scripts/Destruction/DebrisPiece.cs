using UnityEngine;
using UnityEngine.Rendering;

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
    //
    // MeshShatter's pieces come from DebrisPool and go back to it: the same component then
    // serves many pieces in turn, so everything here is reset on each rent (ResetForRent).
    [DisallowMultipleComponent]
    public class DebrisPiece : MonoBehaviour
    {
        // Bookkeeping owned by DebrisManager, not tuning: nothing here belongs in the inspector.
        internal Rigidbody rb;
        internal float born;
        internal float lifetime = 18f;
        internal float expireAt = float.PositiveInfinity;   // may leave from here, once no crew camera sees it
        internal float hardAt = float.PositiveInfinity;     // leaves from here, seen or not
        internal float sleepTime;
        internal float shrinkStart = -1f;
        internal float shrinkDuration = 1f;
        internal bool awaitingRelease;        // shrunk and inactive, waiting for the frame's destroy budget
        internal Vector3 baseScale = Vector3.one;
        internal CollisionDetectionMode detectionMode = CollisionDetectionMode.Discrete;
        internal float size;                  // m, the extent of its box: small ones are culled first
        internal Renderer rend;               // for the view test and the shadow rules; may be null
        internal ShadowCastingMode shadowMode = ShadowCastingMode.On;   // what it cast when registered
        // Who broke it off, for what it crushes (see Actors).
        internal int instigator = Actors.World;
        internal BreakMaterial material = BreakMaterial.Plaster;
        // Never aged out, never culled: a fallen roof section stays where it landed.
        internal bool pinned;
        // The pool shell this piece is, or null when it is not pooled (wall chunks, roof
        // sections, and every piece while the pool is off): then it is destroyed at the end.
        internal DebrisPool.Shell shell;
        // A wall chunk its wall launched (host RemoveChunk, client NetDetach): the blast that
        // launched it does not push it again within structureLaunchGrace of launchedAt, and it
        // never damages the attached chunks of structureSource (the DestructibleModule).
        public bool structureChunk;
        public Component structureSource;
        public float launchedAt = float.NegativeInfinity;

        // The fragment mesh is built for this piece alone, so it dies with it. Box chunks use
        // Unity's shared cube and own nothing; wall chunks use their FBX's mesh; pooled pieces
        // leave their mesh to the pool, which refills it for the next piece.
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

        // A pooled shell starting a new life: nothing of the last piece it was may leak into it.
        internal void ResetForRent()
        {
            born = 0f;
            lifetime = 18f;
            expireAt = hardAt = float.PositiveInfinity;
            sleepTime = 0f;
            shrinkStart = -1f;
            shrinkDuration = 1f;
            awaitingRelease = false;
            baseScale = Vector3.one;
            size = 0f;
            rend = null;
            shadowMode = ShadowCastingMode.On;
            instigator = Actors.World;
            material = BreakMaterial.Plaster;
            pinned = false;
            shell = null;
            structureChunk = false;
            structureSource = null;
            launchedAt = float.NegativeInfinity;
            ownedMesh = null;
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
