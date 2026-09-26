using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Door leaves are not in her NavMesh (they hang on kinematic hinges, and carving the shut
    // ones would lock her in: A8 1.4), so a path runs straight through a closed door. This finds
    // the first door on the path ahead that is shut or still swinging, so the mover can stop,
    // open it and walk through. HingedPanel.SetOpen ignores locks on purpose: she has the keys.
    public sealed class DoorPassage
    {
        [Tooltip("Panels at least this tall are doors; the rest are window casements.")]
        public float minHeight = 1.6f;
        [Tooltip("Metres added around a door's shut box, on each side.")]
        public float margin = 0.3f;

        readonly List<HingedPanel> doors = new List<HingedPanel>();
        readonly List<Bounds> shutBoxes = new List<Bounds>();
        readonly List<Bounds> sweepBoxes = new List<Bounds>();

        public int Count => doors.Count;

        // Once, after every HingedPanel has seated its hinge (they do it in Awake). Doors are
        // fixed things, so their shut boxes and swings are measured once.
        public void Collect()
        {
            doors.Clear();
            shutBoxes.Clear();
            sweepBoxes.Clear();
            var all = Object.FindObjectsByType<HingedPanel>();
            for (int i = 0; i < all.Length; i++)
            {
                HingedPanel p = all[i];
                if (p.TryGetClosedBounds(null, out Bounds b) && b.size.y >= minHeight)
                {
                    doors.Add(p);
                    shutBoxes.Add(b);
                    sweepBoxes.Add(SweepOf(p, b));
                }
            }
        }

        // Everything the leaf passes through between shut and open, as one world box: a side
        // door sweeps about its own width into the room, a garage door hinged at the top sweeps
        // its whole height (2.6 m) into the garage. Six steps of 15 degrees or so: the arc
        // between two of them bulges out by a couple of centimetres at most.
        static Bounds SweepOf(HingedPanel p, Bounds shut)
        {
            const int Steps = 6;
            Bounds sweep = shut;
            for (int k = 1; k <= Steps; k++)
            {
                if (!p.TryGetSwingBox(p.FullOpenAngle * k / Steps, out Vector3 c, out Vector3 half, out Quaternion rot)) break;
                for (int corner = 0; corner < 8; corner++)
                {
                    var o = new Vector3((corner & 1) == 0 ? -half.x : half.x, (corner & 2) == 0 ? -half.y : half.y, (corner & 4) == 0 ? -half.z : half.z);
                    sweep.Encapsulate(c + rot * o);
                }
            }
            return sweep;
        }

        // The first door along the polyline from `from` through corners[first..count) that is
        // shut or moving, within `ahead` metres of walking, and how far along it is.
        public HingedPanel FindAhead(Vector3 from, Vector3[] corners, int first, int count, float ahead, out float distance)
        {
            distance = 0f;
            Vector3 a = from;
            float walked = 0f;
            for (int c = first; c < count && walked < ahead; c++)
            {
                Vector3 b = corners[c];
                Vector3 seg = b - a;
                float len = seg.magnitude;
                if (len < 1e-3f) { a = b; continue; }
                float reach = Mathf.Min(len, ahead - walked);
                var ray = SegmentRay(a, seg, len);
                for (int i = 0; i < doors.Count; i++)
                {
                    HingedPanel d = doors[i];
                    if (d == null || !d.CanSwing) continue;          // gone or wrecked: the doorway is open
                    if (d.IsOpen && !d.IsMoving) continue;
                    if (Crosses(i, ray, reach, out float hit))
                    {
                        distance = walked + Mathf.Max(0f, hit);
                        return d;
                    }
                }
                walked += len;
                a = b;
            }
            return null;
        }

        // Does the path ahead still go through this door, open or not, within `ahead` metres?
        public bool IsOnPath(HingedPanel door, Vector3 from, Vector3[] corners, int first, int count, float ahead)
        {
            int i = doors.IndexOf(door);
            if (i < 0) return false;
            Vector3 a = from;
            float walked = 0f;
            for (int c = first; c < count && walked < ahead; c++)
            {
                Vector3 b = corners[c];
                Vector3 seg = b - a;
                float len = seg.magnitude;
                if (len < 1e-3f) { a = b; continue; }
                if (Crosses(i, SegmentRay(a, seg, len), Mathf.Min(len, ahead - walked), out _)) return true;
                walked += len;
                a = b;
            }
            return false;
        }

        // At knee height: the NavMesh floats a few centimetres above the floor.
        static Ray SegmentRay(Vector3 a, Vector3 seg, float len) => new Ray(a + Vector3.up * 0.5f, seg / len);

        bool Crosses(int i, Ray ray, float reach, out float hit)
        {
            Bounds box = shutBoxes[i];
            box.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
            return box.IntersectRay(ray, out hit) && hit <= reach;
        }

        // Is she still in the way of this door (its shut box, grown by the margin)?
        public bool IsInside(HingedPanel door, Vector3 position, float extra)
        {
            int i = doors.IndexOf(door);
            if (i < 0) return false;
            Bounds box = shutBoxes[i];
            box.Expand(new Vector3((margin + extra) * 2f, 2f, (margin + extra) * 2f));
            return box.Contains(position);
        }

        // Is she where the leaf passes when it swings (its sweep, grown by `extra`)? A leaf
        // closed on her there pushes her CharacterController out of its way.
        public bool InSweep(HingedPanel door, Vector3 position, float extra)
        {
            int i = doors.IndexOf(door);
            if (i < 0) return false;
            Bounds box = sweepBoxes[i];
            box.Expand(new Vector3(extra * 2f, 2f, extra * 2f));
            return box.Contains(position);
        }

        // Which side of the shut leaf a point is on: +1 or -1 along the door's normal. The house's
        // doors sit in walls along X or Z, so the normal is the thin horizontal axis of the box.
        public int SideOf(HingedPanel door, Vector3 position)
        {
            int i = doors.IndexOf(door);
            if (i < 0) return 0;
            Bounds box = shutBoxes[i];
            float d = box.size.x < box.size.z ? position.x - box.center.x : position.z - box.center.z;
            return d >= 0f ? 1 : -1;
        }

        public Vector3 CenterOf(HingedPanel door)
        {
            int i = doors.IndexOf(door);
            return i >= 0 ? shutBoxes[i].center : (door != null ? door.transform.position : Vector3.zero);
        }
    }
}
