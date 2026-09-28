using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The police road of a flee (ADR-013): a scene marker, placed per map. Ordered segments,
    // each a span of a RoadPath (fromS to toS, either way) or a list of waypoints, sampled into
    // one polyline every `step` metres. Distance 0 is the first segment's start (the police
    // side); the distance grows toward the far end. XZ only: a car's height comes from physics,
    // a marker's from RoadAnchor.GroundHeight.
    //
    // A RoadPath used only by a route (the street in front of the house) must never be added
    // to CountryLand.roads, or the land draws asphalt over it. Its centre line is built here.
    [DefaultExecutionOrder(-200)]   // after CountryLand (-250), which builds its own roads
    [DisallowMultipleComponent]
    public sealed class MissionRoute : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Segment
        {
            [Tooltip("A road of the scene. Empty: the waypoints below are used instead.")]
            public RoadPath road;
            [Tooltip("Distance along the road where this segment starts...")]
            public float fromS;
            [Tooltip("...and where it ends. Lower than fromS drives the road backwards.")]
            public float toS;
            [Tooltip("Used when no road is set: straight lines through these points, in order (only X and Z count).")]
            public Transform[] waypoints = new Transform[0];
        }

        public Segment[] segments = new Segment[0];
        [Tooltip("Where the police stop when they arrive at the house: its projection on the route.")]
        public Transform arrival;
        [Tooltip("Metres between two samples of the polyline.")]
        public float step = 2f;

        Vector2[] xz;
        Vector2[] dir;
        float[] dist;
        float arrivalS = -1f;
        bool built;

        public bool Ready => xz != null && xz.Length > 1;
        public float Length { get { Build(); return Ready ? dist[dist.Length - 1] : 0f; } }
        // The distance of the arrival marker along the route (the route's end when there is none).
        public float ArrivalDistance
        {
            get
            {
                Build();
                if (arrivalS < 0f) arrivalS = arrival != null && Ready ? ProgressOf(arrival.position, -1f) : Length;
                return arrivalS;
            }
        }

        void Awake() => Build();

        // Samples the segments once; later calls do nothing. Safe before CountryLand has built
        // its roads: only their centre lines (XZ) are read.
        public void Build()
        {
            if (built || segments == null) return;
            built = true;
            var pts = new List<Vector2>(512);
            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                if (seg == null) continue;
                if (seg.road != null) AddRoad(pts, seg);
                else AddWaypoints(pts, seg.waypoints);
            }
            if (pts.Count < 2) { xz = null; return; }

            xz = pts.ToArray();
            dist = new float[xz.Length];
            dir = new Vector2[xz.Length];
            for (int i = 1; i < xz.Length; i++) dist[i] = dist[i - 1] + Vector2.Distance(xz[i - 1], xz[i]);
            for (int i = 0; i < xz.Length; i++)
            {
                Vector2 d = xz[Mathf.Min(xz.Length - 1, i + 1)] - xz[Mathf.Max(0, i - 1)];
                dir[i] = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
            }
        }

        void AddRoad(List<Vector2> pts, Segment seg)
        {
            if (!seg.road.Ready) seg.road.BuildCenterline();
            if (!seg.road.Ready) return;
            float len = seg.road.Length;
            float a = Mathf.Clamp(seg.fromS, 0f, len), b = Mathf.Clamp(seg.toS, 0f, len);
            float span = Mathf.Abs(b - a);
            int n = Mathf.Max(1, Mathf.CeilToInt(span / Mathf.Max(0.5f, step)));
            for (int k = 0; k <= n; k++)
            {
                seg.road.Sample(Mathf.Lerp(a, b, k / (float)n), 0f, out Vector3 p, out _);
                Append(pts, new Vector2(p.x, p.z));
            }
        }

        void AddWaypoints(List<Vector2> pts, Transform[] way)
        {
            if (way == null) return;
            Vector2 last = Vector2.zero;
            bool has = false;
            for (int i = 0; i < way.Length; i++)
            {
                if (way[i] == null) continue;
                Vector2 p = new Vector2(way[i].position.x, way[i].position.z);
                if (has)
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(last, p) / Mathf.Max(0.5f, step)));
                    for (int k = 1; k <= n; k++) Append(pts, Vector2.Lerp(last, p, k / (float)n));
                }
                else Append(pts, p);
                last = p;
                has = true;
            }
        }

        // Two segments meet at one point: the join is not sampled twice.
        static void Append(List<Vector2> pts, Vector2 p)
        {
            if (pts.Count > 0 && (pts[pts.Count - 1] - p).sqrMagnitude < 0.25f) return;
            pts.Add(p);
        }

        // A point `lateral` metres right of the centre line (negative: left) at a distance, y 0,
        // and the flat direction of travel there.
        public void Sample(float d, float lateral, out Vector3 position, out Vector3 forward)
        {
            Build();
            if (!Ready) { position = transform.position; forward = transform.forward; return; }
            Locate(d, out int i, out float t);
            Vector2 p = Vector2.Lerp(xz[i], xz[i + 1], t);
            Vector2 f = Vector2.Lerp(dir[i], dir[i + 1], t).normalized;
            p += new Vector2(f.y, -f.x) * lateral;
            position = new Vector3(p.x, 0f, p.y);
            forward = new Vector3(f.x, 0f, f.y);
        }

        public Vector3 Forward(float d)
        {
            Sample(d, 0f, out _, out Vector3 f);
            return f;
        }

        // The distance along the route of the point nearest to a position. hint: a distance the
        // answer is expected near (the last one), or negative to search the whole route.
        public float ProgressOf(Vector3 position, float hint) => ProgressOf(position, hint, out _);

        // lateral: signed metres from the centre line, right positive (as Sample).
        public float ProgressOf(Vector3 position, float hint, out float lateral)
        {
            Build();
            lateral = 0f;
            if (!Ready) return 0f;
            Vector2 p = new Vector2(position.x, position.z);
            int from = 0, to = xz.Length - 2;
            if (hint >= 0f)
            {
                Locate(hint - HintWindow, out from, out _);
                Locate(hint + HintWindow, out to, out _);
            }
            float best = Nearest(p, from, to, out float s, out lateral);
            // Far from where it was: it left the window (a snap, a long off-route chase).
            if (hint >= 0f && best > HintWindow * 0.5f) Nearest(p, 0, xz.Length - 2, out s, out lateral);
            return s;
        }

        const float HintWindow = 60f;

        float Nearest(Vector2 p, int from, int to, out float s, out float lateral)
        {
            float bestSq = float.MaxValue;
            s = 0f;
            lateral = 0f;
            for (int i = from; i <= to; i++)
            {
                Vector2 a = xz[i], ab = xz[i + 1] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                Vector2 q = a + ab * t;
                float d2 = (p - q).sqrMagnitude;
                if (d2 >= bestSq) continue;
                bestSq = d2;
                s = Mathf.Lerp(dist[i], dist[i + 1], t);
                // Right of the direction of travel is positive: cross of the direction and the offset.
                Vector2 off = p - q;
                lateral = len2 > 1e-6f ? (ab.y * off.x - ab.x * off.y) / Mathf.Sqrt(len2) : 0f;
            }
            return Mathf.Sqrt(bestSq);
        }

        void Locate(float d, out int i, out float t)
        {
            d = Mathf.Clamp(d, 0f, dist[dist.Length - 1]);
            i = Mathf.Clamp(Mathf.FloorToInt(d / Mathf.Max(0.5f, step)), 0, xz.Length - 2);
            while (i > 0 && dist[i] > d) i--;
            while (i < xz.Length - 2 && dist[i + 1] < d) i++;
            float span = dist[i + 1] - dist[i];
            t = span > 1e-5f ? Mathf.Clamp01((d - dist[i]) / span) : 0f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.4f, 1f);
            if (Ready)
            {
                for (int i = 0; i + 1 < xz.Length; i++)
                    Gizmos.DrawLine(new Vector3(xz[i].x, transform.position.y, xz[i].y),
                                    new Vector3(xz[i + 1].x, transform.position.y, xz[i + 1].y));
                return;
            }
            if (segments == null) return;
            for (int k = 0; k < segments.Length; k++)
            {
                var way = segments[k] != null ? segments[k].waypoints : null;
                if (way == null) continue;
                for (int i = 0; i + 1 < way.Length; i++)
                    if (way[i] != null && way[i + 1] != null) Gizmos.DrawLine(way[i].position, way[i + 1].position);
            }
        }
    }
}
