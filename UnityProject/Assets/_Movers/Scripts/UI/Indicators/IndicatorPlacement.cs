using UnityEngine;

namespace Movers
{
    // Where a world point lands in one player's view: in view (a ring over it), or out of view
    // (a token on an ellipse inside the view, with an arrow towards it).
    //
    // Why not the Target Indicators package for this: its Padded and Absolute boundaries measure
    // against Screen.width and Screen.height (ScreenData.ScreenCenter, the behind-the-camera flip,
    // the clamp), so with two cameras side by side P1's arrows would be centred on the middle of
    // the whole screen, which is P1's right edge. The package keeps the compass tape, which only
    // depends on a heading (CrewIndicatorView).
    //
    // Plain maths on a camera frame, no Camera call, so it can be checked outside Unity.
    public static class IndicatorPlacement
    {
        // A camera as the placement needs it. view is the camera's viewport in OnGUI pixels
        // (ViewportGUI.RectFor), so a split-screen half is just another rect.
        public struct Frame
        {
            public Vector3 position, right, up, forward;
            public float verticalFov;     // degrees
            public Rect view;

            public static Frame Of(Camera cam, Rect view)
            {
                Transform t = cam.transform;
                return new Frame
                {
                    position = t.position, right = t.right, up = t.up, forward = t.forward,
                    verticalFov = cam.fieldOfView, view = view,
                };
            }

            // Heading in degrees, clockwise from +Z seen from above, the angle the package's
            // compass measures from (CompassTapeScreenPose: atan2(forward.x, forward.z)). Looking
            // straight down, forward has no heading left: the camera's up then points where the
            // player faces.
            public float Heading => HeadingOf(forward, up);

            public Vector3 FlatForward => FlatForwardOf(forward, up);
        }

        public static Vector3 FlatForwardOf(Vector3 forward, Vector3 up)
        {
            var f = new Vector3(forward.x, 0f, forward.z);
            if (f.sqrMagnitude < 0.0025f) f = new Vector3(up.x, 0f, up.z);
            return f.sqrMagnitude > 1e-8f ? f.normalized : Vector3.forward;
        }

        public static float HeadingOf(Vector3 forward, Vector3 up)
        {
            Vector3 f = FlatForwardOf(forward, up);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        public struct Result
        {
            public bool onScreen;      // inside the arrow ellipse and clear of the HUD: a ring at gui
            public bool behind;        // behind the camera plane
            public bool underHud;      // in view, but under a HUD panel: an edge token beside the panel instead
            public bool moved;         // the edge token was slid round a HUD panel, off its ray from the centre
            public Vector2 gui;        // projected point (meaningless when behind)
            public Vector2 edge;       // where the token sits: gui in view, on the ellipse out of it
            public float angle;        // degrees, 0 right, 90 down: where the arrow points, towards the target
            public float depth;        // metres along the camera's forward
        }

        // Where no token centre may go: UICORE's HUD corner panels (HudCorners), each grown by how
        // far a token and its arrow reach plus a gap. The HUD's panel draws over the markers', so a
        // token under the contract card would simply be lost. OnGUI pixels, like the view. The view
        // fills a fixed array each frame: nothing is allocated. The default blocks nothing.
        public struct Keepout
        {
            public Rect[] rects;
            public int count;

            public bool Blocks(Vector2 p)
            {
                for (int i = 0; i < count; i++) if (rects[i].Contains(p)) return true;
                return false;
            }
        }

        // The ellipse the edge tokens ride on. Its insets keep the tape and the bottom middle (the
        // prompt, the pockets) free; the HUD's corner panels are kept free by a Keepout.
        public struct Ellipse
        {
            public Vector2 center;
            public float a, b;         // semi-axes, pixels

            const float StepRadians = 2f * Mathf.Deg2Rad;
            const int MaxSteps = 90;   // 90 steps of 2 degrees each way: the whole ellipse

            public bool Contains(Vector2 p)
            {
                float x = (p.x - center.x) / a, y = (p.y - center.y) / b;
                return x * x + y * y <= 1f;
            }

            // The point of the ellipse in this direction from the centre.
            public Vector2 Along(Vector2 dir)
            {
                float x = dir.x / a, y = dir.y / b;
                float d = Mathf.Sqrt(x * x + y * y);
                return d < 1e-6f ? center : center + dir / d;
            }

            // The point of the ellipse at this parameter angle (radians, 0 right, y down).
            public Vector2 At(float phi) => center + new Vector2(a * Mathf.Cos(phi), b * Mathf.Sin(phi));

            // The free point of the ellipse nearest to start (a point on it), walking both ways
            // round it, so a token slides past a HUD panel to the closer side instead of hiding
            // under it. start itself when it is free, and when the whole ellipse is covered.
            public Vector2 FreeNear(Vector2 start, in Keepout keep, out bool moved)
            {
                moved = false;
                if (keep.count == 0 || !keep.Blocks(start)) return start;
                float phi = Mathf.Atan2((start.y - center.y) / b, (start.x - center.x) / a);
                for (int i = 1; i <= MaxSteps; i++)
                {
                    Vector2 p = At(phi + i * StepRadians), q = At(phi - i * StepRadians);
                    bool pFree = !keep.Blocks(p), qFree = !keep.Blocks(q);
                    if (!pFree && !qFree) continue;
                    moved = true;
                    if (pFree && qFree) return (p - start).sqrMagnitude <= (q - start).sqrMagnitude ? p : q;
                    return pFree ? p : q;
                }
                return start;
            }
        }

        const float NearZ = 0.05f;

        // Half the width the compass tape may take, from the view's centre, clear of the HUD
        // panels level with it (rects between bandTop and bandBottom, OnGUI pixels). The tape
        // stays centred on the view, where its notch is the crosshair's heading, so the nearer
        // panel decides. The whole half view when nothing is level with it.
        public static float TapeHalfRoom(Rect view, float bandTop, float bandBottom, Rect[] rects, int count)
        {
            float cx = view.center.x;
            float room = view.width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Rect r = rects[i];
                if (r.yMin >= bandBottom || r.yMax <= bandTop) continue;
                if (r.xMin <= cx && r.xMax >= cx) continue;   // across the middle: not a corner panel
                room = Mathf.Min(room, r.center.x < cx ? cx - r.xMax : r.xMin - cx);
            }
            return Mathf.Max(0f, room);
        }

        public static Ellipse EdgeEllipse(Rect view, float topInset, float bottomInset, float sideInset)
        {
            float top = view.y + topInset, bottom = view.yMax - bottomInset;
            if (bottom - top < 8f) { top = view.center.y - 4f; bottom = view.center.y + 4f; }
            return new Ellipse
            {
                center = new Vector2(view.center.x, (top + bottom) * 0.5f),
                a = Mathf.Max(4f, view.width * 0.5f - sideInset),
                b = (bottom - top) * 0.5f,
            };
        }

        // Camera-space coordinates: x right, y up, z forward.
        public static Vector3 ToCamera(in Frame f, Vector3 world)
        {
            Vector3 d = world - f.position;
            return new Vector3(Vector3.Dot(d, f.right), Vector3.Dot(d, f.up), Vector3.Dot(d, f.forward));
        }

        // OnGUI point of a camera-space point in front of the camera: Camera.WorldToScreenPoint for
        // a standard perspective camera with this viewport, with y turned down.
        public static Vector2 Project(in Frame f, Vector3 local)
        {
            float t = Mathf.Tan(f.verticalFov * 0.5f * Mathf.Deg2Rad);
            float aspect = f.view.width / Mathf.Max(1f, f.view.height);
            float z = Mathf.Max(local.z, 1e-4f);
            float nx = local.x / (z * t * aspect);
            float ny = local.y / (z * t);
            return new Vector2(f.view.center.x + nx * f.view.width * 0.5f, f.view.center.y - ny * f.view.height * 0.5f);
        }

        public static Result Place(in Frame f, Vector3 world, in Ellipse e) => Place(f, world, e, default);

        public static Result Place(in Frame f, Vector3 world, in Ellipse e, in Keepout keep)
        {
            Vector3 local = ToCamera(f, world);
            var r = new Result { depth = local.z };
            Vector2 dir;
            bool inFront = local.z > NearZ;
            if (inFront)
            {
                r.gui = Project(f, local);
                if (e.Contains(r.gui))
                {
                    if (!keep.Blocks(r.gui))
                    {
                        r.onScreen = true;
                        r.edge = r.gui;
                        r.angle = 90f;          // a ring points down at its target
                        return r;
                    }
                    // A ring under the contract card would be drawn and never seen: an edge
                    // token beside the card points at it instead.
                    r.underHud = true;
                }
                dir = r.gui - e.center;
            }
            else
            {
                // Behind: on the side of the shorter turn, at a height that follows the target,
                // never at the bottom middle (the prompt lives there, and "behind" reads as "turn
                // round", which is a side). Unity's projection of a point behind the camera is
                // mirrored, which is why the package flips it; camera space is not.
                r.behind = true;
                float side = local.x >= 0f ? 1f : -1f;
                dir = new Vector2(side * Mathf.Max(Mathf.Abs(local.x), 2f * Mathf.Abs(local.y), 0.01f), -local.y);
            }
            if (dir.sqrMagnitude < 1e-8f) dir = Vector2.right;
            r.edge = e.FreeNear(e.Along(dir), keep, out r.moved);
            // In front, the arrow points from the token at the target's projection: the ray from
            // the centre when nothing moved the token, and still the target when the token was slid
            // round a panel or the target is under one. Behind, it shows the turn to make.
            Vector2 to = inFront ? r.gui - r.edge : dir;
            if (to.sqrMagnitude < 1f) to = dir;
            r.angle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            return r;
        }
    }
}
