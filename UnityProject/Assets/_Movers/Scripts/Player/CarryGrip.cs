using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Where the hands take hold of a carried thing (hot-fix of 2026-09-27: things floated between
    // the hands). Nothing is set up per object: the holds are worked out from the thing's own box
    // as the carrier sees it (from the eyes, in the carry frame, Frame), so a sugar bowl, a crate
    // and a sofa turned on end all get hands on them.
    //
    //   Sides    up to SideSpan across, as seen: a palm flat on each side, a little into its near
    //            half and at the height the arms carry at, the way a box is carried;
    //   Front    wider than that (a sofa across, a bed): the palms on its near face, about
    //            shoulder width apart;
    //   OneHand  no longer than OneHandSize on any side (the keys): the right hand alone, on its
    //            right side.
    //
    // A hold is the nearest point of the real box, turned as it is, not of a box drawn round it in
    // the view: a palm lies on a face, or wraps the edge of a thing held corner-on. A shape its box
    // reads wrong can carry its own points instead (HoldPoints).
    //
    // PlayerGrab places the carried thing so its holds are within the arms' reach, the wheel
    // choosing how far the arms are stretched; FirstPersonHands puts your own hands on them and
    // CrewCarryIK the body's hands the others see. All three ask here, from the same eyes and with
    // the style the carry chose, so they agree on where the hands are.
    public static class CarryGrip
    {
        public enum Style { Sides, Front, OneHand }

        public struct Hold
        {
            public bool used;
            public Vector3 point;    // world: where the middle of the palm goes
            public Vector3 normal;   // world: straight out of the surface there, toward the hand
        }

        // The thing as the eyes see it, for one pose of it.
        public struct Seen
        {
            public MovableObject mo;
            public HoldPoints authored;
            public Vector3 position;     // its root, world
            public Quaternion rotation;
            public Vector3 eyes;
            public Quaternion look;
            public Vector3 centre;       // the box's middle, in the eyes' space
            public Quaternion turn;      // the box's axes, in the eyes' space
            public Vector3 extents;      // the box's half size, metres
            public Vector3 min, max;     // what it covers along the eyes' axes, in their space
        }

        // The frame things are carried in: the body's heading, and only part of where the eyes
        // look up or down. The arms hang from the shoulders, not from the eyes, so looking at your
        // feet does not swing what you carry back into your chest (or behind you), and looking up
        // lifts it only so far. Within the range it follows the look, so it can still be aimed at
        // a shelf or a truck bed.
        public const float PitchDown = 20f;
        public const float PitchUp = 35f;

        public static Quaternion Frame(Transform eyes)
        {
            Vector3 f = eyes.forward;
            float down = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            return Quaternion.Euler(Mathf.Clamp(down, -PitchUp, PitchDown), yaw, 0f);
        }

        // The arms the holds are kept within reach of. On a crew body they are the body's own
        // (FirstPersonHands draws your forearms on its bones): its upper-arm bones where they are
        // now, and its arm's length to the wrist, which is the point the arm is bent to. Knocked
        // down, the bones lie on the floor while the eyes stay up, so the shoulders are put where
        // they stand instead (measured on the crew body, 2026-09-27). A player with no body (the
        // tutorial's capsule) has FirstPersonHands' arms, set in the eyes' space.
        public struct Arms
        {
            public Vector3 right, left;   // the shoulders, world
            public float reach;           // shoulder to the hold

            public Vector3 Shoulder(float side) => side > 0f ? right : left;

            static readonly Vector3 BodyShoulder = new Vector3(0.16f, -0.17f, -0.04f);   // from the eyes, the body's heading

            public static Arms Of(Animator body, bool bodyDown, FirstPersonHands hands, Transform eyes, Quaternion frame)
            {
                if (body != null && body.isHuman)
                {
                    Transform ru = body.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    Transform rl = body.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    Transform rh = body.GetBoneTransform(HumanBodyBones.RightHand);
                    Transform lu = body.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    if (ru != null && rl != null && rh != null && lu != null)
                    {
                        float length = Vector3.Distance(ru.position, rl.position) + Vector3.Distance(rl.position, rh.position);
                        if (!bodyDown) return new Arms { right = ru.position, left = lu.position, reach = length };
                        Quaternion heading = Quaternion.Euler(0f, frame.eulerAngles.y, 0f);
                        Vector3 s = BodyShoulder;
                        return new Arms
                        {
                            right = eyes.position + heading * s,
                            left = eyes.position + heading * new Vector3(-s.x, s.y, s.z),
                            reach = length,
                        };
                    }
                }
                Vector3 fs = hands != null ? hands.shoulder : new Vector3(0.17f, -0.27f, -0.1f);
                float reach = hands != null ? hands.upperArmLength + hands.forearmLength - 0.02f : 0.58f;
                return new Arms
                {
                    right = eyes.TransformPoint(fs),
                    left = eyes.TransformPoint(new Vector3(-fs.x, fs.y, fs.z)),
                    reach = reach,
                };
            }
        }

        public const float SideSpan = 0.9f;      // metres across, as seen, that a pair of palms spans
        public const float OneHandSize = 0.2f;   // the longest side of a thing one hand takes
        const float StyleMargin = 0.08f;         // how far past SideSpan before the hands change over
        // Metres under the eyes the hands carry at. Low enough for the arms, high enough that the
        // hands are in the picture when you look straight ahead (the view reaches 37.5 degrees down).
        const float CarryHeight = -0.24f;
        const float FrontSpread = 0.22f;         // each hand from the middle, on a near face
        const float EdgeMargin = 0.05f;          // a hold keeps this far in from a top or bottom edge
        const float Outside = 0.1f;              // how far off the box a hand comes at it from

        static readonly List<Renderer> renderers = new List<Renderer>(16);
        static readonly List<Collider> colliders = new List<Collider>(8);

        // The thing, placed with its root at (position, rotation), seen from the eyes.
        public static Seen See(MovableObject mo, Vector3 position, Quaternion rotation, Vector3 eyes, Quaternion look)
        {
            Bounds box = Box(mo);
            Vector3 s = mo.transform.lossyScale;
            Vector3 e = Vector3.Scale(box.extents, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            Quaternion toView = Quaternion.Inverse(look);
            Quaternion turn = toView * rotation;
            Vector3 centre = toView * (position + rotation * Vector3.Scale(box.center, s) - eyes);
            Vector3 ax = turn * new Vector3(e.x, 0f, 0f);
            Vector3 ay = turn * new Vector3(0f, e.y, 0f);
            Vector3 az = turn * new Vector3(0f, 0f, e.z);
            Vector3 half = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
                                       Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
                                       Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Seen
            {
                mo = mo,
                authored = mo.gripPoints,
                position = position,
                rotation = rotation,
                eyes = eyes,
                look = look,
                centre = centre,
                turn = turn,
                extents = e,
                min = centre - half,
                max = centre + half,
            };
        }

        // Which way the hands take it. `keep` holds on to the last style until the thing is well
        // past the limit, so one turned near it does not make the hands jump back and forth.
        public static void Decide(in Seen seen, ref Style style, bool keep)
        {
            if (seen.authored != null && seen.authored.right != null)
            {
                style = seen.authored.left != null ? Style.Sides : Style.OneHand;
                return;
            }
            Vector3 e = seen.extents;
            float width = seen.max.x - seen.min.x;
            if (2f * Mathf.Max(e.x, Mathf.Max(e.y, e.z)) <= OneHandSize) style = Style.OneHand;
            else if (!keep || style == Style.OneHand) style = width <= SideSpan ? Style.Sides : Style.Front;
            else if (style == Style.Sides && width > SideSpan + StyleMargin) style = Style.Front;
            else if (style == Style.Front && width < SideSpan - StyleMargin) style = Style.Sides;
        }

        // The holds for a style. OneHand uses the right hand only.
        public static void Holds(in Seen seen, Style style, out Hold left, out Hold right)
        {
            left = default;
            right = default;
            if (seen.mo == null) return;
            if (seen.authored != null && seen.authored.right != null)
            {
                Authored(seen, out left, out right);
                return;
            }

            Vector3 lo = seen.min, hi = seen.max, c = seen.centre;
            // The height the arms carry at, kept on the thing.
            float y = hi.y - lo.y > 2f * EdgeMargin ? Mathf.Clamp(CarryHeight, lo.y + EdgeMargin, hi.y - EdgeMargin) : c.y;
            switch (style)
            {
                case Style.OneHand:
                    right = Nearest(seen, new Vector3(hi.x + Outside, c.y, c.z));
                    break;
                case Style.Sides:
                {
                    float z = lo.z + Mathf.Min((hi.z - lo.z) * 0.3f, 0.15f);
                    right = Nearest(seen, new Vector3(hi.x + Outside, y, z));
                    left = Nearest(seen, new Vector3(lo.x - Outside, y, z));
                    break;
                }
                default:
                {
                    float spread = Mathf.Clamp((hi.x - lo.x) * 0.5f - EdgeMargin, 0f, FrontSpread);
                    right = Nearest(seen, new Vector3(c.x + spread, y, lo.z - Outside));
                    left = Nearest(seen, new Vector3(c.x - spread, y, lo.z - Outside));
                    break;
                }
            }
        }

        // See, Decide and Holds in one call.
        public static void Solve(MovableObject mo, Vector3 position, Quaternion rotation, Vector3 eyes, Quaternion look,
                                 ref Style style, bool keep, out Hold left, out Hold right)
        {
            left = default;
            right = default;
            if (mo == null) return;
            var seen = See(mo, position, rotation, eyes, look);
            Decide(seen, ref style, keep);
            Holds(seen, style, out left, out right);
        }

        // The point of the box nearest to `wanted` (in the eyes' space), and the way out of the
        // face it lies on (or between the two faces of an edge), both returned in the world.
        static Hold Nearest(in Seen seen, Vector3 wanted)
        {
            Vector3 e = seen.extents;
            Vector3 l = Quaternion.Inverse(seen.turn) * (wanted - seen.centre);
            Vector3 q = new Vector3(Mathf.Clamp(l.x, -e.x, e.x), Mathf.Clamp(l.y, -e.y, e.y), Mathf.Clamp(l.z, -e.z, e.z));
            if (q == l)
            {
                // Inside it: out through the nearest face.
                float dx = e.x - Mathf.Abs(l.x), dy = e.y - Mathf.Abs(l.y), dz = e.z - Mathf.Abs(l.z);
                if (dx <= dy && dx <= dz) q.x = Mathf.Sign(l.x) * e.x;
                else if (dy <= dz) q.y = Mathf.Sign(l.y) * e.y;
                else q.z = Mathf.Sign(l.z) * e.z;
            }
            Vector3 onBox = seen.centre + seen.turn * q;
            Vector3 n = new Vector3(Face(q.x, e.x), Face(q.y, e.y), Face(q.z, e.z));
            Vector3 normal = n.sqrMagnitude > 0f ? seen.turn * n : wanted - onBox;
            if (normal.sqrMagnitude < 1e-8f) normal = Vector3.back;
            return new Hold
            {
                used = true,
                point = seen.eyes + seen.look * onBox,
                normal = seen.look * normal.normalized,
            };
        }

        static float Face(float q, float e) => e > 1e-4f && Mathf.Abs(q) >= e - 1e-4f ? Mathf.Sign(q) : 0f;

        static void Authored(in Seen seen, out Hold left, out Hold right)
        {
            right = Place(seen, seen.authored.right);
            left = seen.authored.left != null ? Place(seen, seen.authored.left) : default;
            // Turned round in the hands: the right hand takes the point on the right.
            if (left.used && Vector3.Dot(right.point - left.point, seen.look * Vector3.right) < 0f)
            {
                var t = left;
                left = right;
                right = t;
            }
        }

        static Hold Place(in Seen seen, Transform t)
        {
            Transform root = seen.mo.transform;
            Vector3 local = root.InverseTransformPoint(t.position);
            Vector3 dir = root.InverseTransformDirection(t.forward);
            return new Hold
            {
                used = true,
                point = seen.position + seen.rotation * Vector3.Scale(root.lossyScale, local),
                normal = (seen.rotation * dir).normalized,
            };
        }

        // The thing's box in its own space (before its root's scale): its renderers, or its
        // colliders when it draws nothing. Measured the first time it is asked for.
        public static Bounds Box(MovableObject mo)
        {
            if (mo.gripKnown) return mo.gripBox;
            Transform root = mo.transform;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            mo.GetComponentsInChildren(false, renderers);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                // A skinned mesh's local bounds are its root bone's, not its own transform's.
                if (r is SkinnedMeshRenderer) Grow(ref b, ref any, toRoot, r.bounds);
                else Grow(ref b, ref any, toRoot * r.localToWorldMatrix, r.localBounds);
            }
            renderers.Clear();
            if (!any)
            {
                mo.GetComponentsInChildren(false, colliders);
                for (int i = 0; i < colliders.Count; i++)
                {
                    var c = colliders[i];
                    if (c == null || c.isTrigger) continue;
                    if (c is BoxCollider bc) Grow(ref b, ref any, toRoot * bc.transform.localToWorldMatrix, new Bounds(bc.center, bc.size));
                    else Grow(ref b, ref any, toRoot, c.bounds);
                }
                colliders.Clear();
            }
            if (!any) b = new Bounds(Vector3.zero, Vector3.one * 0.3f);
            mo.gripBox = b;
            mo.gripPoints = mo.GetComponent<HoldPoints>();
            mo.gripKnown = true;
            return b;
        }

        static void Grow(ref Bounds b, ref bool any, Matrix4x4 m, Bounds box)
        {
            Vector3 c = box.center, e = box.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
    }
}
