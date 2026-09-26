using UnityEngine;

namespace Movers
{
    // A box of grenades in the cellar. Put it on a crate (or anything with a collider) and at
    // Play it lays that many grenades on top, in a small tidy grid. There is no refill: when
    // the cellar is empty, it is empty, and that is part of the joke.
    //
    // Nothing is serialised into the scene except this component, like StartingItemSpawner:
    // the grenades are built from code at Play.
    public class GrenadeCrate : MonoBehaviour
    {
        [Min(0)] public int count = 6;
        // Centre to centre. Never under the grenade's own grab box (0.1 m), or neighbours
        // would start inside each other and the physics would blow the pile apart before
        // anything has exploded. That holds because every grenade is turned square to the grid
        // (see Start): a box turned 45 degrees is 0.14 m across and would not fit.
        public float spacing = 0.13f;
        // Dropped from this far above the top so no grenade starts touching the surface.
        public float clearance = 0.01f;

        const float MinSpacing = 0.11f;
        // One grenade's grab box, side to side (GrenadeItem.grabBox). Only used to tell whether
        // the grid fits on the top.
        const float GrenadeWidth = 0.1f;

        // The surface the grenades are laid on: its middle, its turn, and its size along that
        // turn (x across, y front to back). Size is zero when only the world box is known.
        struct Top
        {
            public Vector3 centre;
            public Quaternion turn;
            public Vector2 size;
        }

        void Start()
        {
            // Parented beside the crate, never under it: if the crate is itself a movable
            // rigidbody, a rigidbody nested inside it would be dragged along as part of it.
            Transform tidy = transform.parent;
            var body = GetComponentInParent<Rigidbody>();
            if (body != null) tidy = body.transform.parent;

            // Measured once, before the first grenade exists, so none of them count as crate.
            Top top = MeasureTop();
            WarnIfTooSmall(top);

            // Create gives each grenade a random turn, which looks right on a floor. In a grid it
            // does not fit: turned boxes reach into their neighbours. Squared to the top, every
            // box lines up with the rows. No physics step has run yet, so setting the transform
            // is enough.
            for (int i = 0; i < count; i++)
            {
                var g = GrenadeItem.Create(SlotPosition(i, top));
                g.transform.rotation = top.turn;
                g.transform.SetParent(tidy, true);
            }
        }

        void Grid(out int cols, out int rows, out float step)
        {
            int n = Mathf.Max(1, count);
            cols = Mathf.CeilToInt(Mathf.Sqrt(n));
            rows = Mathf.CeilToInt(n / (float)cols);   // never more than cols
            step = Mathf.Max(MinSpacing, spacing);
        }

        // The top is longer front to back than side to side: the grid's long side goes that way.
        static bool Deep(Top top)
        {
            return top.size.y > top.size.x;
        }

        // Where grenade i stands: a near-square grid, centred on the top and turned with it,
        // so a crate placed at an angle gets a grid at the same angle.
        Vector3 SlotPosition(int i, Top top)
        {
            Grid(out int cols, out int rows, out float s);
            float along = (i % cols - (cols - 1) * 0.5f) * s;   // the grid's long side
            float across = (i / cols - (rows - 1) * 0.5f) * s;
            var offset = Deep(top) ? new Vector3(across, 0f, along) : new Vector3(along, 0f, across);

            Vector3 p = top.centre + top.turn * offset;
            p.y += GrenadeItem.RestHeight + Mathf.Max(0f, clearance);
            return p;
        }

        // One warning, at Play, when the outer grenades would hang past the edge and roll off.
        // Easier to read in the console than to notice two grenades missing from the pile.
        void WarnIfTooSmall(Top top)
        {
            if (count <= 0 || top.size == Vector2.zero) return;
            Grid(out int cols, out int rows, out float s);
            float gridLong = (cols - 1) * s + GrenadeWidth;
            float gridShort = (rows - 1) * s + GrenadeWidth;
            float topLong = Mathf.Max(top.size.x, top.size.y);
            float topShort = Mathf.Min(top.size.x, top.size.y);
            if (gridLong <= topLong && gridShort <= topShort) return;
            Debug.LogWarning("[GrenadeCrate] " + name + ": " + count + " grenades need a top of about "
                             + gridLong.ToString("0.00") + " x " + gridShort.ToString("0.00") + " m, this one is "
                             + topLong.ToString("0.00") + " x " + topShort.ToString("0.00")
                             + " m, so some will fall off. Lower count or spacing, or use a bigger crate.");
        }

        // A single upright BoxCollider (the usual movable crate) is measured in its own frame, so
        // the grid sits on its real top face with its real size. Anything else (a mesh collider,
        // several colliders, a crate lying on its side) falls back to the world box around it,
        // turned with this object: the middle and the height are still right, the size is not
        // known, and the gizmo is the check.
        Top MeasureTop()
        {
            BoxCollider box = null;
            int solid = 0;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                solid++;
                box = c as BoxCollider;
            }

            if (solid == 1 && box != null && Vector3.Dot(box.transform.up, Vector3.up) > 0.98f)
            {
                Transform t = box.transform;
                Vector3 s = Vector3.Scale(box.size, t.lossyScale);
                return new Top
                {
                    centre = t.TransformPoint(box.center + Vector3.up * (box.size.y * 0.5f)),
                    turn = Quaternion.Euler(0f, t.eulerAngles.y, 0f),
                    size = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.z)),
                };
            }

            Bounds b = WorldBounds();
            return new Top
            {
                centre = new Vector3(b.center.x, b.max.y, b.center.z),
                turn = Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
                size = Vector2.zero,
            };
        }

        // This object's solid colliders, else its renderers, else just its position. Triggers
        // are skipped, they are not something to stand things on.
        Bounds WorldBounds()
        {
            bool found = false;
            var b = new Bounds(transform.position, Vector3.zero);

            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (!found) { b = c.bounds; found = true; }
                else b.Encapsulate(c.bounds);
            }
            if (found) return b;

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.40f, 0.46f, 0.20f, 0.9f);   // olive, like the grenades
            Top top = MeasureTop();
            for (int i = 0; i < count; i++)
            {
                Vector3 p = SlotPosition(i, top);
                Gizmos.DrawWireSphere(p, 0.04f);
                Gizmos.DrawLine(p, p + Vector3.up * 0.08f);
            }
        }
    }
}
