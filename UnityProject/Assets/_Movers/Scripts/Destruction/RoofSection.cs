using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // One piece of roof. Roofs never break (floors and stairs neither: REJECTED.md), but since
    // ADR-009 a roof section falls, whole and rigid, when nothing holds it up any more: blow out
    // the walls under the garage roof and it comes down on whatever is inside.
    //
    // Added by HouseDestruction to every kit roof piece. It does nothing at all until the
    // structure graph releases it. Then:
    // - the glass in it breaks (a glass roof does not land in one piece);
    // - its kit colliders, non-convex meshes that a moving body cannot use, are switched off and
    //   replaced by one convex slab fitted to the slope, built once per roof mesh (cooking the
    //   real 7000-triangle roof as convex costs 15 to 27 ms, measured; the slab costs nothing);
    // - it becomes a heavy rigidbody, registered as debris that never expires, so what it lands
    //   on is crushed (ImpactDamage: debris over the crush mass counts as a Crush).
    [DisallowMultipleComponent]
    public sealed class RoofSection : MonoBehaviour, IStructurePart
    {
        // Wood roof plus its tiles, as a share of the bounding box: most of the box is attic air.
        const float Fill = 0.15f;
        const float MinMass = 100f, MaxMass = 3000f;
        const float MinThickness = 0.1f;

        public int GraphNode { get; internal set; } = -1;
        public bool HasFallen { get; private set; }
        public float Mass { get; private set; }

        public static readonly List<RoofSection> All = new List<RoofSection>();

        BreakMaterial material = BreakMaterial.Wood;
        Transform homeParent;
        Vector3 homePosition;
        Quaternion homeRotation;
        int homeLayer;
        readonly List<Collider> switchedOff = new List<Collider>();
        MeshCollider slab;
        Rigidbody body;
        DebrisPiece piece;

        // One slab per roof mesh, shared by every section that uses it, kept across sessions
        // (plain data, hidden).
        static readonly Dictionary<Mesh, Mesh> slabs = new Dictionary<Mesh, Mesh>();
        static readonly List<Vector3> verts = new List<Vector3>(16384);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDestroy() { All.Remove(this); }

        internal void Bind(DestructibleModuleCatalog.Entry spec)
        {
            if (spec != null) material = spec.material;
            homeParent = transform.parent;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeLayer = gameObject.layer;
            Bounds b = WorldBounds();
            float density = DestructionMaterialTable.Get(BreakMaterial.Wood).density;
            Mass = Mathf.Clamp(b.size.x * b.size.y * b.size.z * density * Fill, MinMass, MaxMass);
        }

        public Bounds WorldBounds()
        {
            if (TryGetComponent(out Renderer r)) return r.bounds;
            if (TryGetComponent(out Collider c)) return c.bounds;
            return new Bounds(transform.position, Vector3.one);
        }

        // ---- falling ----

        public float Fall(in DamageEvent cause)
        {
            if (HasFallen) return 0f;
            HasFallen = true;
            Bounds b = WorldBounds();

            var panes = GetComponentsInChildren<GlassPane>(false);
            for (int i = 0; i < panes.Length; i++)
                if (panes[i] != null && !panes[i].IsBroken) panes[i].Shatter(cause.With(0f, 0f, panes[i].transform.position, Vector3.down));

            // A piece that was nothing but glass (the flat veranda panes) has no body of its own
            // left once its panes are gone: nothing falls.
            if (!TryGetComponent(out MeshRenderer visible) || !visible.enabled)
            {
                StructureGraph.Current?.MarkRemoved(GraphNode, cause.instigator);
                return 0f;
            }

            switchedOff.Clear();
            var cols = GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || !cols[i].enabled) continue;
                cols[i].enabled = false;
                switchedOff.Add(cols[i]);
            }

            Mesh shape = SlabFor(GetComponent<MeshFilter>());
            if (shape != null)
            {
                slab = gameObject.AddComponent<MeshCollider>();
                slab.sharedMesh = shape;
                slab.convex = true;
            }
            else
            {
                // Nothing to fit: a plain box over the renderer, still a body.
                var box = gameObject.AddComponent<BoxCollider>();
                var mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                }
            }

            body = gameObject.AddComponent<Rigidbody>();
            body.mass = Mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.maxDepenetrationVelocity = 2f;
            body.linearVelocity = Vector3.down * 0.5f;

            piece = gameObject.AddComponent<DebrisPiece>();
            piece.Init(body, null);
            piece.instigator = cause.instigator;
            piece.material = material;
            piece.pinned = true;
            var manager = DebrisManager.Instance;
            if (manager != null) manager.Register(piece, float.PositiveInfinity);

            StructureGraph.Current?.MarkRemoved(GraphNode, cause.instigator);
            ImpactAudio.Play(ImpactAudio.Kind.Crunch, b.center, 1f, cause.instigator);
            DestructionFX.Dust(b.center, b.extents.magnitude);
            return Mass;
        }

        // The debug reset: back where it was built, static again, its own colliders back.
        public void Revive()
        {
            if (!HasFallen) return;
            HasFallen = false;
            if (piece != null) Destroy(piece);
            // Kinematic before the kit's non-convex colliders come back on: a moving body may
            // not carry them, and Destroy only lands at the end of the frame.
            if (body != null)
            {
                body.isKinematic = true;
                Destroy(body);
            }
            if (slab != null)
            {
                slab.enabled = false;
                Destroy(slab);
            }
            var box = GetComponent<BoxCollider>();
            if (box != null && !switchedOff.Contains(box))
            {
                box.enabled = false;
                Destroy(box);
            }
            piece = null;
            body = null;
            slab = null;
            for (int i = 0; i < switchedOff.Count; i++)
                if (switchedOff[i] != null) switchedOff[i].enabled = true;
            switchedOff.Clear();
            transform.SetParent(homeParent, false);
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            gameObject.layer = homeLayer;
        }

        // ---- IStructurePart ----

        float IStructurePart.Release(int node, int part, in DamageEvent cause) => Fall(cause);
        bool IStructurePart.SplitForSupport(int node) => false;
        string IStructurePart.Describe(int part) => name + " (roof section, " + Mass.ToString("0") + " kg)";

        // ---- the slab ----

        // A roof piece is a sloped plate. Fit a plane y = k * t + c through its vertices, t along
        // x or along z (whichever fits thinner), and build the box around that plate, in the
        // mesh's own space so the transform's scale applies exactly. Pieces that are not one
        // plate (the crossing) get their plain bounding box.
        static Mesh SlabFor(MeshFilter mf)
        {
            if (mf == null || mf.sharedMesh == null) return null;
            Mesh src = mf.sharedMesh;
            if (slabs.TryGetValue(src, out Mesh cached) && cached != null) return cached;

            Bounds b = src.bounds;
            Vector3 center = b.center, size = b.size;
            Quaternion rot = Quaternion.identity;
            if (src.isReadable)
            {
                verts.Clear();
                src.GetVertices(verts);
                if (verts.Count > 3)
                {
                    FitAlong(verts, 2, out float kz, out float cz, out float tz, out float z0, out float z1, out float mz);
                    FitAlong(verts, 0, out float kx, out float cx, out float tx, out float x0, out float x1, out float mx);
                    bool alongZ = tz <= tx;
                    float k = alongZ ? kz : kx, c = alongZ ? cz : cx, thick = alongZ ? tz : tx;
                    float lo = alongZ ? z0 : x0, hi = alongZ ? z1 : x1, mid = alongZ ? mz : mx;
                    if (thick < b.size.y * 0.6f)
                    {
                        float angle = Mathf.Atan(k);
                        float cos = Mathf.Cos(angle);
                        float t = (lo + hi) * 0.5f;
                        float y = k * t + c + mid;
                        float along = (hi - lo) / Mathf.Max(0.1f, cos);
                        float across = Mathf.Max(MinThickness, thick);
                        if (alongZ)
                        {
                            center = new Vector3(b.center.x, y, t);
                            size = new Vector3(b.size.x, across, along);
                            rot = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
                        }
                        else
                        {
                            center = new Vector3(t, y, b.center.z);
                            size = new Vector3(along, across, b.size.z);
                            rot = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                        }
                    }
                }
                verts.Clear();
            }

            var m = BoxMesh(center, rot, size);
            m.name = src.name + "_FallSlab";
            m.hideFlags = HideFlags.HideAndDontSave;
            slabs[src] = m;
            return m;
        }

        // Least squares y = k * t + c over the vertices, t = x (axis 0) or z (axis 2). thick is the
        // plate thickness measured square to the plate, mid the middle of the residuals.
        static void FitAlong(List<Vector3> v, int axis, out float k, out float c, out float thick,
                             out float lo, out float hi, out float mid)
        {
            double st = 0, sy = 0, stt = 0, sty = 0;
            int n = v.Count;
            lo = float.MaxValue;
            hi = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float t = axis == 0 ? v[i].x : v[i].z;
                float y = v[i].y;
                st += t; sy += y; stt += t * t; sty += t * y;
                if (t < lo) lo = t;
                if (t > hi) hi = t;
            }
            double den = n * stt - st * st;
            k = den != 0 ? (float)((n * sty - st * sy) / den) : 0f;
            c = (float)((sy - k * st) / n);
            float rMin = float.MaxValue, rMax = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float t = axis == 0 ? v[i].x : v[i].z;
                float r = v[i].y - (k * t + c);
                if (r < rMin) rMin = r;
                if (r > rMax) rMax = r;
            }
            mid = (rMin + rMax) * 0.5f;
            thick = (rMax - rMin) * Mathf.Cos(Mathf.Atan(k));
        }

        static Mesh BoxMesh(Vector3 center, Quaternion rot, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z);
                v[i] = center + rot * corner;
            }
            int[] t =
            {
                0, 2, 1, 1, 2, 3,  4, 5, 6, 5, 7, 6,
                0, 1, 4, 1, 5, 4,  2, 6, 3, 3, 6, 7,
                0, 4, 2, 2, 4, 6,  1, 3, 5, 3, 7, 5,
            };
            var m = new Mesh();
            m.vertices = v;
            m.triangles = t;
            m.RecalculateBounds();
            return m;
        }
    }
}
