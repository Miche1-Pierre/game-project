using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Movers
{
    // Recycles the pieces MeshShatter builds (DEV 2 section 5). A grenade in a furnished room
    // breaks dozens of things in one frame, and each piece used to be a new GameObject with five
    // components and its own Mesh, destroyed again a minute later: garbage and native churn on
    // the frame that can least afford it. A shell (GameObject, MeshFilter, MeshRenderer,
    // BoxCollider, Rigidbody, DebrisPiece and a Mesh) is now rented, refilled and given back.
    // Wall chunks are not pooled: their debris is the chunk itself.
    //
    // The network id sweep (NetIds) must never see a shell, or the ids and the tracked bodies
    // would differ between a machine that pooled and one that did not:
    // - every shell carries HideFlags.DontSaveInEditor, which the sweep skips with everything
    //   under it (NetIds.VisitSiblings), rented or not, and it is never registered;
    // - a pooled shell is kinematic, so it could not pass for a body even without the flag;
    // - the warm-up waits a few frames after the scene loaded, so the sweep (first frame of the
    //   game scene) has always run before the first shell exists (DebrisManager.Update);
    // - OnDestroy destroys every shell and mesh the pool made, so none survives a scene change.
    //
    // Owned by DebrisManager, on the same object. Debris stays local to each machine, online
    // too: both machines pool their own.
    [DisallowMultipleComponent]
    public sealed class DebrisPool : MonoBehaviour
    {
        [Tooltip("Shells kept at most. Past it a piece gets a throwaway shell, destroyed at the end of its life.")]
        public int capacity = 256;
        [Tooltip("Shells made ahead of the first break, a few per frame after the scene loaded.")]
        public int warmCount = 64;
        [Tooltip("Shells made per frame while warming up.")]
        public int warmPerFrame = 8;

        // Off for the NetIds count check (DEV 2 section 12): start the game with -nodebrispool.
        // With the pool off every piece is built and destroyed as before.
        public static bool Enabled { get; set; } = true;

        internal sealed class Shell
        {
            public GameObject go;
            public Transform transform;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public BoxCollider box;
            public Rigidbody rb;
            public DebrisPiece piece;
            public Mesh mesh;       // made on first use; the pool's for owned shells, the piece's otherwise
            public bool owned;      // goes back to the pool (else destroyed with its piece)
            public bool rented;
        }

        readonly List<Shell> all = new List<Shell>(256);    // every owned shell, rented or not
        readonly Stack<Shell> free = new Stack<Shell>(256);
        Transform shelf;

        static Mesh cubeMesh;
        static Material defaultMaterial;

        public int Size => all.Count;
        public int Free => free.Count;
        public bool Warm => all.Count >= Mathf.Min(warmCount, capacity);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Enabled = !HasArg("-nodebrispool");
            cubeMesh = null;
            defaultMaterial = null;
        }

        static bool HasArg(string arg)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], arg, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void Awake()
        {
            var go = new GameObject("DebrisPool") { hideFlags = HideFlags.DontSaveInEditor };
            go.SetActive(false);
            shelf = go.transform;
            shelf.SetParent(transform, false);
        }

        // A shell for one piece: inactive, kinematic, at the pool's shelf or nowhere yet. The
        // caller positions it, fills it and hands it to MeshShatter.Launch, which activates it.
        internal Shell Rent()
        {
            while (free.Count > 0)
            {
                Shell s = free.Pop();
                if (s.go == null) continue;   // destroyed by someone else: forget it
                s.rented = true;
                return s;
            }
            if (all.Count < capacity)
            {
                Shell s = Make(true);
                all.Add(s);
                s.rented = true;
                return s;
            }
            // Over capacity: same shell, flagged the same, but destroyed at the end of its life.
            Shell extra = Make(true);
            extra.owned = false;
            extra.rented = true;
            return extra;
        }

        // What MeshShatter used before the pool, for when it is off: a plain object, destroyed
        // at the end of its life, invisible to nothing.
        internal static Shell MakeLoose()
        {
            Shell s = Make(false);
            s.owned = false;
            return s;
        }

        // Back on the shelf: kinematic, inactive, mesh emptied. False when it is not the
        // pool's (the caller destroys it).
        internal bool Return(Shell s)
        {
            if (s == null || !s.owned || s.go == null || shelf == null) return false;
            if (!s.rented) return true;
            s.rented = false;
            if (!s.rb.isKinematic)
            {
                // Continuous detection first: kinematic bodies do not support it.
                s.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                s.rb.isKinematic = true;
            }
            s.go.SetActive(false);
            s.transform.SetParent(shelf, false);
            s.transform.localScale = Vector3.one;
            s.renderer.SetPropertyBlock(null);
            s.renderer.shadowCastingMode = ShadowCastingMode.On;
            if (s.mesh != null)
            {
                s.mesh.Clear();
                s.filter.sharedMesh = s.mesh;
            }
            free.Push(s);
            return true;
        }

        // A few shells a frame until warmCount exist. Called by DebrisManager once the scene
        // has been up long enough for the NetIds sweep to have run.
        internal void WarmStep()
        {
            int target = Mathf.Min(warmCount, capacity);
            for (int i = 0; i < warmPerFrame && all.Count < target; i++)
            {
                Shell s = Make(true);
                EnsureMesh(s);
                all.Add(s);
                free.Push(s);
            }
        }

        static Shell Make(bool pooled)
        {
            var go = new GameObject("Debris");
            // Inactive before anything is added: no physics body exists until the shell is used.
            go.SetActive(false);
            if (pooled) go.hideFlags = HideFlags.DontSaveInEditor;
            var s = new Shell
            {
                go = go,
                transform = go.transform,
                filter = go.AddComponent<MeshFilter>(),
                renderer = go.AddComponent<MeshRenderer>(),
                box = go.AddComponent<BoxCollider>(),
                rb = go.AddComponent<Rigidbody>(),
                piece = go.AddComponent<DebrisPiece>(),
                owned = pooled,
            };
            s.rb.isKinematic = true;
            return s;
        }

        // Owned shells get their mesh once and keep it; loose ones hand it to their piece.
        internal static Mesh EnsureMesh(Shell s)
        {
            if (s.mesh == null)
            {
                s.mesh = new Mesh { name = "Debris" };
                s.mesh.MarkDynamic();
            }
            s.filter.sharedMesh = s.mesh;
            return s.mesh;
        }

        // Unity's cube and its default material, for the box pieces of unreadable meshes.
        // Taken once from a primitive, switched off at once and destroyed at the end of the
        // frame (DestroyImmediate is refused inside the physics callbacks that break things).
        internal static Mesh CubeMesh
        {
            get
            {
                if (cubeMesh == null) TakePrimitive();
                return cubeMesh;
            }
        }

        internal static Material DefaultMaterial
        {
            get
            {
                if (defaultMaterial == null) TakePrimitive();
                return defaultMaterial;
            }
        }

        static void TakePrimitive()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.hideFlags = HideFlags.DontSaveInEditor;
            cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            defaultMaterial = cube.GetComponent<MeshRenderer>().sharedMaterial;
            cube.SetActive(false);
            Destroy(cube);
        }

        void OnDestroy()
        {
            for (int i = 0; i < all.Count; i++)
            {
                Shell s = all[i];
                if (s.mesh != null) Destroy(s.mesh);
                if (s.go != null) Destroy(s.go);
            }
            all.Clear();
            free.Clear();
            if (shelf != null) Destroy(shelf.gameObject);
        }
    }
}
