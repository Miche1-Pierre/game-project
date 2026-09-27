using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // One first-person forearm and hand, built in code from chunky flat-shaded blocks (the low-poly
    // look of the house kit), and posed by FirstPersonHands. No MonoBehaviour: plain transforms
    // under the hands' rig, one renderer per block.
    //
    // Hand space, both hands: the origin is the wrist, +Z runs along the hand to the fingertips,
    // +Y is the back of the hand (the palm faces -Y). The right hand has its thumb on -X, the left
    // one on +X: everything below is written for the right hand and mirrored by `side` (+1 right,
    // -1 left). Forearm space: the origin is the wrist too, the forearm runs back along -Z to the
    // elbow, and its +Y follows the back of the hand so the sleeve twists with it.
    //
    // A little bigger than a real hand on purpose: at 40 cm from the eyes a true-size hand reads
    // thin next to the house's chunky props.
    public sealed class FirstPersonArm
    {
        // ---- dimensions (metres, right hand) ----
        public const float PalmLength = 0.088f;
        const float PalmWidth = 0.084f;
        public const float PalmThickness = 0.032f;
        const float FingerWidth = 0.0195f;
        const float FingerThickness = 0.02f;
        const float KnuckleY = 0.002f;

        // Index, middle, ring, little: across the knuckles (right hand) and the two segments.
        static readonly float[] FingerX = { -0.031f, -0.0105f, 0.0105f, 0.031f };
        static readonly float[] FingerNear = { 0.040f, 0.044f, 0.041f, 0.032f };
        static readonly float[] FingerFar = { 0.034f, 0.036f, 0.034f, 0.028f };
        static readonly float[] FingerFan = { -6f, -2f, 2f, 6f };   // degrees about Y when the hand is open

        static readonly Vector3 ThumbRoot = new Vector3(-0.036f, -0.008f, 0.02f);
        const float ThumbNear = 0.036f;
        const float ThumbFar = 0.03f;

        public readonly Transform forearm;
        public readonly Transform hand;
        readonly float side;
        readonly Transform[] knuckle = new Transform[4];
        readonly Transform[] middle = new Transform[4];
        readonly Transform thumbRoot;
        readonly Transform thumbJoint;
        readonly Quaternion thumbRest;

        public bool IsRight => side > 0f;

        // ---- grip points, in hand space ----

        // Between the index and middle fingers, a little way along the first segment: where a
        // cigarette sits. `curl` is the index and middle curl the grip uses.
        public static Vector3 PinchPoint(float side, float curl)
        {
            float a = Mathf.Lerp(6f, 82f, curl) * Mathf.Deg2Rad;
            const float along = 0.028f;
            float x = 0.5f * (FingerX[0] + FingerX[1]) * side;
            return new Vector3(x, KnuckleY - Mathf.Sin(a) * along, PalmLength + Mathf.Cos(a) * along);
        }

        // The axis of a cylinder of this radius held in the fist, under the palm by the knuckles.
        public static Vector3 FistPoint(float side, float radius)
        {
            return new Vector3(-0.004f * side, -(PalmThickness * 0.5f + radius), PalmLength * 0.72f);
        }

        // The middle of the palm's surface: what goes flat on the side of a carried box.
        public static Vector3 PalmPoint(float side)
        {
            return new Vector3(0f, -PalmThickness * 0.5f, PalmLength * 0.55f);
        }

        // ---- building ----

        public FirstPersonArm(Transform parent, float side, Material sleeve, Material cuff, Material skin,
                              List<Mesh> meshes, List<Renderer> renderers)
        {
            this.side = side >= 0f ? 1f : -1f;
            string n = this.side > 0f ? "Right" : "Left";

            forearm = new GameObject(n + "Forearm").transform;
            forearm.SetParent(parent, false);
            // Past the elbow at the back, so the cut end never shows; a rolled cuff at the wrist.
            Block(forearm, "Sleeve", Prism(8, -0.34f, new Vector2(0.050f, 0.044f), -0.06f, new Vector2(0.043f, 0.037f), meshes),
                  Vector3.zero, Quaternion.identity, sleeve, renderers);
            Block(forearm, "Cuff", Prism(8, -0.088f, new Vector2(0.050f, 0.044f), -0.034f, new Vector2(0.049f, 0.043f), meshes),
                  Vector3.zero, Quaternion.identity, cuff, renderers);
            Block(forearm, "Wrist", Prism(8, -0.06f, new Vector2(0.031f, 0.025f), 0.014f, new Vector2(0.029f, 0.022f), meshes),
                  Vector3.zero, Quaternion.identity, skin, renderers);

            hand = new GameObject(n + "Hand").transform;
            hand.SetParent(parent, false);
            Block(hand, "Palm", BevelBox(new Vector3(PalmWidth, PalmThickness, PalmLength), 0.009f, meshes),
                  new Vector3(0f, 0f, PalmLength * 0.5f), Quaternion.identity, skin, renderers);

            for (int i = 0; i < 4; i++)
            {
                knuckle[i] = Joint(hand, "Finger" + i, new Vector3(FingerX[i] * this.side, KnuckleY, PalmLength - 0.006f));
                Block(knuckle[i], "Near", BevelBox(new Vector3(FingerWidth, FingerThickness, FingerNear[i] + 0.006f), 0.0055f, meshes),
                      new Vector3(0f, 0f, FingerNear[i] * 0.5f), Quaternion.identity, skin, renderers);
                middle[i] = Joint(knuckle[i], "Joint", new Vector3(0f, 0f, FingerNear[i]));
                Block(middle[i], "Far", BevelBox(new Vector3(FingerWidth * 0.94f, FingerThickness * 0.92f, FingerFar[i] + 0.004f), 0.0055f, meshes),
                      new Vector3(0f, 0f, FingerFar[i] * 0.5f - 0.002f), Quaternion.identity, skin, renderers);
            }

            // The thumb leaves the heel of the palm pointing forward and out, a little under.
            thumbRest = Quaternion.LookRotation(new Vector3(-0.6f * this.side, -0.25f, 0.75f), new Vector3(-0.3f * this.side, 1f, 0f));
            thumbRoot = Joint(hand, "Thumb", new Vector3(ThumbRoot.x * this.side, ThumbRoot.y, ThumbRoot.z));
            thumbRoot.localRotation = thumbRest;
            Block(thumbRoot, "Near", BevelBox(new Vector3(0.024f, 0.021f, ThumbNear + 0.006f), 0.006f, meshes),
                  new Vector3(0f, 0f, ThumbNear * 0.5f), Quaternion.identity, skin, renderers);
            thumbJoint = Joint(thumbRoot, "Joint", new Vector3(0f, 0f, ThumbNear));
            Block(thumbJoint, "Far", BevelBox(new Vector3(0.022f, 0.019f, ThumbFar + 0.004f), 0.0055f, meshes),
                  new Vector3(0f, 0f, ThumbFar * 0.5f - 0.002f), Quaternion.identity, skin, renderers);

            Curl(0.4f, 0.4f, 0.4f, 0.4f, 0.3f);
        }

        // 0 straight, 1 a closed fist, per finger; the thumb folds across the palm.
        public void Curl(float index, float middleFinger, float ring, float little, float thumb)
        {
            CurlFinger(0, index);
            CurlFinger(1, middleFinger);
            CurlFinger(2, ring);
            CurlFinger(3, little);
            float t = Mathf.Clamp01(thumb);
            // Positive yaw in the thumb's own space swings it toward the fingers on both hands:
            // the rest rotation is mirrored, so its +X points at the palm on the right hand and
            // away from it on the left.
            thumbRoot.localRotation = thumbRest * Quaternion.Euler(24f * t, 38f * t * side, 0f);
            thumbJoint.localRotation = Quaternion.Euler(8f + 38f * t, 0f, 0f);
        }

        void CurlFinger(int i, float c)
        {
            c = Mathf.Clamp01(c);
            knuckle[i].localRotation = Quaternion.Euler(Mathf.Lerp(6f, 82f, c), FingerFan[i] * side * (1f - c), 0f);
            middle[i].localRotation = Quaternion.Euler(Mathf.Lerp(4f, 95f, c), 0f, 0f);
        }

        // Everything in the rig's space: the wrist, the hand's rotation and the elbow the forearm
        // comes from.
        public void Place(Vector3 wrist, Quaternion handRotation, Vector3 elbow)
        {
            hand.localPosition = wrist;
            hand.localRotation = handRotation;
            Vector3 along = wrist - elbow;
            Vector3 up = handRotation * Vector3.up;
            if (along.sqrMagnitude < 1e-6f) along = handRotation * Vector3.forward;
            if (Vector3.Cross(along, up).sqrMagnitude < 1e-6f) up = handRotation * Vector3.forward;
            forearm.localPosition = wrist;
            forearm.localRotation = Quaternion.LookRotation(along, up);
        }

        // ---- pieces ----

        static Transform Joint(Transform parent, string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        static void Block(Transform parent, string name, Mesh mesh, Vector3 at, Quaternion rotation,
                          Material mat, List<Renderer> renderers)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            // The body already casts the player's shadow, and the body's own arms (hidden from
            // this camera, still in the shadow pass) must not darken these.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.forceRenderingOff = true;   // FirstPersonHands shows them to their own camera only
            renderers.Add(r);
        }

        // ---- meshes: flat shaded, one normal per face ----

        // A box with its edges and corners cut off: 6 faces, 12 bevels, 8 corner triangles.
        public static Mesh BevelBox(Vector3 size, float bevel, List<Mesh> meshes)
        {
            Vector3 h = size * 0.5f;
            float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
            var mb = new MeshBuild();

            // The corner (sx, sy, sz) as it lies on the face across `axis`.
            Vector3 C(int axis, int sx, int sy, int sz)
            {
                var p = new Vector3(sx * (h.x - b), sy * (h.y - b), sz * (h.z - b));
                if (axis == 0) p.x = sx * h.x;
                else if (axis == 1) p.y = sy * h.y;
                else p.z = sz * h.z;
                return p;
            }

            int[] s = { -1, 1 };
            // Faces.
            for (int axis = 0; axis < 3; axis++)
                foreach (int f in s)
                {
                    var q = new Vector3[4];
                    int[,] uv = { { -1, -1 }, { -1, 1 }, { 1, 1 }, { 1, -1 } };
                    for (int k = 0; k < 4; k++)
                    {
                        int u = uv[k, 0], w = uv[k, 1];
                        if (axis == 0) q[k] = C(0, f, u, w);
                        else if (axis == 1) q[k] = C(1, u, f, w);
                        else q[k] = C(2, u, w, f);
                    }
                    mb.Poly(q, Vector3.zero);
                }
            // Edge bevels: the edge runs along `axis`, between the faces of the two other axes.
            for (int axis = 0; axis < 3; axis++)
            {
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                foreach (int s1 in s)
                    foreach (int s2 in s)
                    {
                        var q = new Vector3[4];
                        int[] sg = new int[3];
                        sg[a1] = s1; sg[a2] = s2;
                        sg[axis] = -1;
                        q[0] = C(a1, sg[0], sg[1], sg[2]);
                        q[3] = C(a2, sg[0], sg[1], sg[2]);
                        sg[axis] = 1;
                        q[1] = C(a1, sg[0], sg[1], sg[2]);
                        q[2] = C(a2, sg[0], sg[1], sg[2]);
                        mb.Poly(q, Vector3.zero);
                    }
            }
            // Corners.
            foreach (int x in s)
                foreach (int y in s)
                    foreach (int z in s)
                        mb.Poly(new[] { C(0, x, y, z), C(1, x, y, z), C(2, x, y, z) }, Vector3.zero);

            return mb.Done("BevelBox", meshes);
        }

        // A tapered prism along +Z, elliptic in section, capped at both ends. The first ring is
        // turned half a side so an even count has flat faces on top, bottom and sides.
        public static Mesh Prism(int sides, float z0, Vector2 r0, float z1, Vector2 r1, List<Mesh> meshes)
        {
            var mb = new MeshBuild();
            var a = new Vector3[sides];
            var c = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float t = (k + 0.5f) / sides * Mathf.PI * 2f;
                float cx = Mathf.Cos(t), cy = Mathf.Sin(t);
                a[k] = new Vector3(cx * r0.x, cy * r0.y, z0);
                c[k] = new Vector3(cx * r1.x, cy * r1.y, z1);
            }
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                Vector3 mid = (a[k] + a[n] + c[k] + c[n]) * 0.25f;
                mb.Poly(new[] { a[k], a[n], c[n], c[k] }, new Vector3(0f, 0f, mid.z));
            }
            mb.Poly(a, new Vector3(0f, 0f, z0 + (z1 - z0) * 10f));   // the start cap faces back
            mb.Poly(c, new Vector3(0f, 0f, z1 - (z1 - z0) * 10f));   // the end cap faces on
            return mb.Done("Prism", meshes);
        }

        sealed class MeshBuild
        {
            readonly List<Vector3> v = new List<Vector3>(128);
            readonly List<Vector3> n = new List<Vector3>(128);
            readonly List<int> t = new List<int>(256);

            // A convex polygon, turned to face away from `inside`.
            public void Poly(Vector3[] p, Vector3 inside)
            {
                Vector3 centre = Vector3.zero;
                for (int i = 0; i < p.Length; i++) centre += p[i];
                centre /= p.Length;
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < p.Length; i++)
                {
                    Vector3 a = p[i], b = p[(i + 1) % p.Length];
                    normal += Vector3.Cross(a - centre, b - centre);
                }
                if (normal.sqrMagnitude < 1e-14f) return;   // a bevel of zero width
                normal.Normalize();
                // Unity draws a triangle's front where Cross(b - a, c - a) points.
                bool flip = Vector3.Dot(normal, centre - inside) < 0f;
                if (flip) normal = -normal;
                int start = v.Count;
                for (int i = 0; i < p.Length; i++) { v.Add(p[i]); n.Add(normal); }
                for (int i = 1; i < p.Length - 1; i++)
                {
                    // The fan keeps the polygon's order, whose Cross points along the unflipped
                    // normal: reversed when that normal had to be turned outward.
                    if (flip) { t.Add(start); t.Add(start + i + 1); t.Add(start + i); }
                    else { t.Add(start); t.Add(start + i); t.Add(start + i + 1); }
                }
            }

            public Mesh Done(string name, List<Mesh> meshes)
            {
                var m = new Mesh { name = "FP_" + name, hideFlags = HideFlags.HideAndDontSave };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                meshes.Add(m);
                return m;
            }
        }
    }
}
