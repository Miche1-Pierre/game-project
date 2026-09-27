using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The winding country road of the title screen: a smooth curve through a few control
    // points (only their X and Z count), sampled every metre. The land (MenuLand) gives it its
    // heights and flattens the fields along it; the road then builds its own ribbon mesh
    // (asphalt, a dashed centre line, gravel shoulders). The truck and the walking crew follow
    // it by distance (Sample), so everything that moves stays on the tarmac without physics.
    //
    // Open: the truck drives it from one end to the other and starts again from the first end,
    // which the scene hides behind the camera.
    [DisallowMultipleComponent]
    public sealed class RoadPath : MonoBehaviour
    {
        [Tooltip("Control points in world space; only X and Z are used, the land gives the height.")]
        public Vector3[] points = new Vector3[0];
        public float halfWidth = 2.5f;
        public float shoulder = 0.9f;
        [Tooltip("Metres between two samples of the curve.")]
        public float step = 1f;
        [Tooltip("Dash and gap of the centre line, in metres.")]
        public Vector2 dash = new Vector2(3f, 4f);

        Vector2[] xz;
        Vector2[] dir;
        float[] dist;
        float[] height;
        readonly Dictionary<int, List<int>> buckets = new Dictionary<int, List<int>>();
        const float BucketSize = 10f;

        public bool Ready => xz != null && xz.Length > 1;
        public float Length => Ready ? dist[dist.Length - 1] : 0f;
        public int SampleCount => Ready ? xz.Length : 0;
        public float Width => halfWidth;

        // ---- the curve ----

        // Samples the curve (heights all zero until SetHeights). Safe to call again.
        public void BuildCenterline()
        {
            buckets.Clear();
            if (points == null || points.Length < 2) { xz = null; return; }

            // Dense points along a centripetal Catmull-Rom spline through the control points,
            // then resampled at an even `step`, so distances along the road are true metres.
            var dense = new List<Vector2>(points.Length * 40);
            int n = points.Length;
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p0 = Flat(points[Mathf.Max(0, i - 1)]), p1 = Flat(points[i]);
                Vector2 p2 = Flat(points[i + 1]), p3 = Flat(points[Mathf.Min(n - 1, i + 2)]);
                if (i == 0) p0 = p1 - (p2 - p1);
                if (i == n - 2) p3 = p2 + (p2 - p1);
                for (int k = 0; k < 40; k++) dense.Add(CatmullRom(p0, p1, p2, p3, k / 40f));
            }
            dense.Add(Flat(points[n - 1]));

            var outXZ = new List<Vector2>(512) { dense[0] };
            float carried = 0f;
            for (int i = 1; i < dense.Count; i++)
            {
                Vector2 a = dense[i - 1], b = dense[i];
                float seg = Vector2.Distance(a, b);
                while (carried + seg >= step && seg > 0f)
                {
                    float t = (step - carried) / seg;
                    a = Vector2.Lerp(a, b, t);
                    outXZ.Add(a);
                    seg = Vector2.Distance(a, b);
                    carried = 0f;
                }
                carried += seg;
            }
            if ((outXZ[outXZ.Count - 1] - dense[dense.Count - 1]).sqrMagnitude > 0.01f) outXZ.Add(dense[dense.Count - 1]);

            xz = outXZ.ToArray();
            dist = new float[xz.Length];
            dir = new Vector2[xz.Length];
            height = new float[xz.Length];
            for (int i = 1; i < xz.Length; i++) dist[i] = dist[i - 1] + Vector2.Distance(xz[i - 1], xz[i]);
            for (int i = 0; i < xz.Length; i++)
            {
                Vector2 d = xz[Mathf.Min(xz.Length - 1, i + 1)] - xz[Mathf.Max(0, i - 1)];
                dir[i] = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
            }
            for (int i = 0; i < xz.Length; i++)
            {
                int key = Key(xz[i].x, xz[i].y);
                if (!buckets.TryGetValue(key, out var list)) buckets.Add(key, list = new List<int>(16));
                list.Add(i);
            }
        }

        public Vector2 PointXZ(int i) => xz[i];
        public float DistanceAt(int i) => dist[i];

        public void SetHeights(float[] h)
        {
            if (h == null || height == null || h.Length != height.Length) return;
            System.Array.Copy(h, height, h.Length);
        }

        // The road surface's height at a distance along it.
        public float HeightAt(float s)
        {
            if (!Ready) return 0f;
            Locate(s, out int i, out float t);
            return Mathf.Lerp(height[i], height[i + 1], t);
        }

        // A point `lateral` metres to the right of the centre line (negative: left), on the road
        // surface, and the direction of travel there (tilted with the slope).
        public void Sample(float s, float lateral, out Vector3 position, out Vector3 forward)
        {
            if (!Ready) { position = transform.position; forward = Vector3.forward; return; }
            Locate(s, out int i, out float t);
            Vector2 p = Vector2.Lerp(xz[i], xz[i + 1], t);
            Vector2 d = Vector2.Lerp(dir[i], dir[i + 1], t).normalized;
            float y = Mathf.Lerp(height[i], height[i + 1], t);
            Vector2 right = new Vector2(d.y, -d.x);
            p += right * lateral;
            position = new Vector3(p.x, y, p.y);
            float slope = (height[i + 1] - height[i]) / Mathf.Max(0.01f, dist[i + 1] - dist[i]);
            forward = new Vector3(d.x, slope, d.y).normalized;
        }

        void Locate(float s, out int i, out float t)
        {
            s = Mathf.Clamp(s, 0f, Length);
            // Evenly spaced samples: the index is almost s / step; walk to the exact segment.
            i = Mathf.Clamp(Mathf.FloorToInt(s / Mathf.Max(0.01f, step)), 0, xz.Length - 2);
            while (i > 0 && dist[i] > s) i--;
            while (i < xz.Length - 2 && dist[i + 1] < s) i++;
            float span = dist[i + 1] - dist[i];
            t = span > 1e-5f ? Mathf.Clamp01((s - dist[i]) / span) : 0f;
        }

        // The nearest sample to a point, and how far the point is from the centre line. Only
        // looks in the buckets around the point: `maxDistance` beyond that reads as "far".
        public bool Nearest(float x, float z, float maxDistance, out int index, out float distance)
        {
            index = -1;
            distance = float.MaxValue;
            if (!Ready) return false;
            int reach = Mathf.CeilToInt(maxDistance / BucketSize);
            int bx = Mathf.FloorToInt(x / BucketSize), bz = Mathf.FloorToInt(z / BucketSize);
            float best = maxDistance * maxDistance;
            for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                {
                    if (!buckets.TryGetValue(Pack(bx + dx, bz + dz), out var list)) continue;
                    for (int k = 0; k < list.Count; k++)
                    {
                        Vector2 p = xz[list[k]];
                        float d2 = (p.x - x) * (p.x - x) + (p.y - z) * (p.y - z);
                        if (d2 < best) { best = d2; index = list[k]; }
                    }
                }
            if (index < 0) return false;
            // Distance to the segment around the nearest sample, not to the sample itself.
            distance = Mathf.Min(SegmentDistance(x, z, Mathf.Max(0, index - 1)), SegmentDistance(x, z, Mathf.Min(xz.Length - 2, index)));
            return true;
        }

        float SegmentDistance(float x, float z, int i)
        {
            Vector2 a = xz[i], b = xz[i + 1], p = new Vector2(x, z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        static int Key(float x, float z) => Pack(Mathf.FloorToInt(x / BucketSize), Mathf.FloorToInt(z / BucketSize));
        static int Pack(int bx, int bz) => (bx & 0xFFFF) | (bz << 16);

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        // Centripetal Catmull-Rom (alpha 0.5): no cusps or loops where points are uneven.
        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
        {
            float t0 = 0f;
            float t1 = t0 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p0, p1)));
            float t2 = t1 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p1, p2)));
            float t3 = t2 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p2, p3)));
            float t = Mathf.Lerp(t1, t2, u);
            Vector2 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector2 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector2 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector2 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector2 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        // ---- the ribbon ----

        MeshFilter shown;

        // The asphalt, the centre dashes and the shoulders, as one mesh under this object.
        public void BuildMesh(Material landMaterial)
        {
            if (!Ready) return;
            var m = new LowPolyMesh(xz.Length * 10);
            int stride = Mathf.Max(1, Mathf.RoundToInt(2f / step));   // a facet every 2 m
            float edge = halfWidth + shoulder;
            for (int i = 0; i + stride < xz.Length; i += stride)
            {
                int j = i + stride;
                Vector3 c0 = At(i, 0f, 0.06f), c1 = At(j, 0f, 0.06f);
                Vector3 l0 = At(i, -halfWidth, 0.06f), l1 = At(j, -halfWidth, 0.06f);
                Vector3 r0 = At(i, halfWidth, 0.06f), r1 = At(j, halfWidth, 0.06f);
                Vector3 sl0 = At(i, -edge, -0.08f), sl1 = At(j, -edge, -0.08f);
                Vector3 sr0 = At(i, edge, -0.08f), sr1 = At(j, edge, -0.08f);
                int shade = 3 + (i / stride) % 3;
                Vector2 tar = LowPolyPalette.UV(Swatch.Asphalt, shade);
                m.Quad(l0, l1, c1, c0, tar);
                m.Quad(c0, c1, r1, r0, tar);
                Vector2 gravel = LowPolyPalette.UV(Swatch.Gravel, 2 + (i / stride) % 4);
                m.Quad(sl0, sl1, l1, l0, gravel);
                m.Quad(r0, r1, sr1, sr0, gravel);
            }

            // Centre dashes, a hair above the tarmac.
            float period = dash.x + dash.y;
            Vector2 line = LowPolyPalette.UV(Swatch.Line, 5);
            for (float s = dash.y * 0.5f; s + dash.x < Length; s += period)
                for (float k = 0f; k < dash.x - 0.01f; k += 1f)
                {
                    float a = s + k, b = Mathf.Min(s + dash.x, a + 1f);
                    Sample(a, -0.08f, out Vector3 p0, out _);
                    Sample(b, -0.08f, out Vector3 p1, out _);
                    Sample(a, 0.08f, out Vector3 q0, out _);
                    Sample(b, 0.08f, out Vector3 q1, out _);
                    Vector3 lift = Vector3.up * 0.075f;
                    m.Quad(p0 + lift, p1 + lift, q1 + lift, q0 + lift, line);
                }

            if (shown == null)
            {
                var go = new GameObject("RoadMesh");
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                shown = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = landMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Release(shown.sharedMesh);
            shown.sharedMesh = m.ToMesh("Road (runtime)");
        }

        Vector3 At(int i, float lateral, float lift)
        {
            Vector2 right = new Vector2(dir[i].y, -dir[i].x);
            Vector2 p = xz[i] + right * lateral;
            // The ribbon lives in world space; this object stays at the origin.
            return new Vector3(p.x, height[i] + lift, p.y) - transform.position;
        }

        void OnDestroy()
        {
            if (shown != null) Release(shown.sharedMesh);
        }

        // Meshes made here are the road's own (never assets): freed with it.
        static void Release(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
        }

        void OnDrawGizmosSelected()
        {
            if (points == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            for (int i = 0; i + 1 < points.Length; i++) Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }
}
