using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Movers
{
    // Builds the countryside's generated meshes for the Movers/Environment shader: every
    // triangle has its own three vertices and one colour (flat shading, the low-poly look), and
    // uv.y says how far up a blade a vertex is (0 root, 1 tip) for the shader's shading and
    // wind. Also the small shapes scattered by thousands: grass tufts, wheat, flowers, pebbles.
    // Built once when the map starts, never per frame.
    public sealed class EnvMesh
    {
        readonly List<Vector3> verts;
        readonly List<Color> colors;
        readonly List<Vector2> uvs;
        readonly List<int> tris;

        public EnvMesh(int triangleCapacity = 256)
        {
            verts = new List<Vector3>(triangleCapacity * 3);
            colors = new List<Color>(triangleCapacity * 3);
            uvs = new List<Vector2>(triangleCapacity * 3);
            tris = new List<int>(triangleCapacity * 3);
        }

        public int TriangleCount => tris.Count / 3;

        // Heights (uv.y) per corner: 0 at the root, 1 at the tip. Unity shows the face whose
        // Cross(b - a, c - a) points at the viewer.
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, float ha = 0f, float hb = 0f, float hc = 0f)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            uvs.Add(new Vector2(0f, ha)); uvs.Add(new Vector2(0f, hb)); uvs.Add(new Vector2(0f, hc));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Triangle(a, b, c, color);
            Triangle(a, c, d, color);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetColors(colors);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---- shapes (pivot at the root, on the ground, +Y up) ----

        // A tuft of grass: `blades` thin blades round a small circle, leaning outwards, each a
        // bent strip (a quad to the knee, a point to the tip). Drawn double sided (the material
        // culls nothing). `root` and `tip` are per-blade colour extremes, both near white: the
        // material's tint gives the green.
        public static Mesh GrassTuft(int seed, int blades, float height, float spread, Color root, Color tip)
        {
            var rng = new System.Random(seed);
            var m = new EnvMesh(blades * 3);
            for (int i = 0; i < blades; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.6f) / blades * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = new Vector3(-outward.z, 0f, outward.x);
                float h = height * Mathf.Lerp(0.55f, 1f, (float)rng.NextDouble());
                float lean = Mathf.Lerp(0.15f, 0.55f, (float)rng.NextDouble()) * h;
                float w = Mathf.Lerp(0.035f, 0.06f, (float)rng.NextDouble());
                Vector3 b = outward * spread * (float)rng.NextDouble();
                Vector3 mid = b + outward * lean * 0.3f + Vector3.up * h * 0.55f;
                Vector3 top = b + outward * lean + Vector3.up * h;
                Color c = Color.Lerp(root, tip, (float)rng.NextDouble());
                Vector3 bl = b - side * w, br = b + side * w, ml = mid - side * w * 0.6f, mr = mid + side * w * 0.6f;
                m.Triangle(bl, ml, mr, c, 0f, 0.55f, 0.55f);
                m.Triangle(bl, mr, br, c, 0f, 0.55f, 0f);
                m.Triangle(ml, top, mr, c, 0.55f, 1f, 0.55f);
            }
            return m.ToMesh("GrassTuft (runtime)");
        }

        // Wheat: straight golden stalks with an ear on each, a little taller than the grass.
        public static Mesh WheatTuft(int seed, int stalks, float height, Color stalk, Color ear)
        {
            var rng = new System.Random(seed);
            var m = new EnvMesh(stalks * 6);
            for (int i = 0; i < stalks; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 b = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.14f * (float)rng.NextDouble();
                Vector3 lean = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.12f;
                float h = height * Mathf.Lerp(0.8f, 1.05f, (float)rng.NextDouble());
                Vector3 side = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f) * Vector3.right;
                Vector3 top = b + lean + Vector3.up * h;
                m.Triangle(b - side * 0.02f, top, b + side * 0.02f, stalk, 0f, 1f, 0f);
                // The ear: a thin diamond at the top, seen from two sides.
                Vector3 e0 = top - Vector3.up * 0.16f, e1 = top + Vector3.up * 0.05f + lean * 0.3f;
                Vector3 s = side * 0.035f, s2 = Vector3.Cross(side, Vector3.up) * 0.035f;
                m.Triangle(e0, top + s, e1, ear, 0.8f, 1f, 1f);
                m.Triangle(e0, e1, top - s, ear, 0.8f, 1f, 1f);
                m.Triangle(e0, top + s2, e1, ear, 0.8f, 1f, 1f);
                m.Triangle(e0, e1, top - s2, ear, 0.8f, 1f, 1f);
            }
            return m.ToMesh("WheatTuft (runtime)");
        }

        // A clump of flowers: a few stems, each with a small star of petals facing the sky and
        // a heart. Colours are the flower's own (the material stays white).
        public static Mesh FlowerTuft(int seed, int stems, float height, Color stem, Color petal, Color heart)
        {
            var rng = new System.Random(seed);
            var m = new EnvMesh(stems * 12);
            for (int i = 0; i < stems; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 b = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.12f * (float)rng.NextDouble();
                float h = height * Mathf.Lerp(0.6f, 1f, (float)rng.NextDouble());
                Vector3 top = b + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.08f + Vector3.up * h;
                Vector3 side = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f) * Vector3.right * 0.015f;
                m.Triangle(b - side, top, b + side, stem, 0f, 1f, 0f);
                // A leaf halfway up.
                Vector3 leafBase = Vector3.Lerp(b, top, 0.3f);
                Vector3 leafDir = new Vector3(Mathf.Cos(a + 1f), 0.5f, Mathf.Sin(a + 1f)).normalized * 0.09f;
                m.Triangle(leafBase - side * 2f, leafBase + leafDir, leafBase + side * 2f, stem, 0.3f, 0.45f, 0.3f);
                // Five petals round the heart, tilted up a little.
                float r = Mathf.Lerp(0.045f, 0.07f, (float)rng.NextDouble());
                float spin = (float)rng.NextDouble() * 72f;
                Color p = Color.Lerp(petal, Color.white, 0.12f * (float)rng.NextDouble());
                for (int k = 0; k < 5; k++)
                {
                    float a0 = (spin + k * 72f - 22f) * Mathf.Deg2Rad, a1 = (spin + k * 72f + 22f) * Mathf.Deg2Rad;
                    Vector3 p0 = top + new Vector3(Mathf.Cos(a0) * r, 0.012f, Mathf.Sin(a0) * r);
                    Vector3 p1 = top + new Vector3(Mathf.Cos(a1) * r, 0.012f, Mathf.Sin(a1) * r);
                    m.Triangle(top, p1, p0, p, 1f, 1f, 1f);
                }
                Vector3 hs = new Vector3(0.018f, 0f, 0f), hz = new Vector3(0f, 0f, 0.018f);
                Vector3 hc = top + Vector3.up * 0.022f;
                m.Triangle(hc, top + hs, top + hz, heart, 1f, 1f, 1f);
                m.Triangle(hc, top + hz, top - hs, heart, 1f, 1f, 1f);
                m.Triangle(hc, top - hs, top - hz, heart, 1f, 1f, 1f);
                m.Triangle(hc, top - hz, top + hs, heart, 1f, 1f, 1f);
            }
            return m.ToMesh("FlowerTuft (runtime)");
        }

        // A round shrub for the hedgerows and the yard's edges: two faceted balls (an
        // icosahedron cut once) squashed and merged, each face a slightly different green, the
        // undersides darker. About 1.4 m across and 1 m high at scale 1; pivot on the ground.
        public static Mesh Bush(int seed, Color colour)
        {
            var rng = new System.Random(seed);
            var m = new EnvMesh(260);
            const int balls = 2;
            for (int b = 0; b < balls; b++)
            {
                var c = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.75f, 0.42f + 0.16f * (float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.45f);
                float r = Mathf.Lerp(0.4f, 0.58f, (float)rng.NextDouble());
                m.Ball(c, new Vector3(r, r * 0.82f, r), colour, rng, 0.16f, 1.05f);
            }
            return m.ToMesh("Bush (runtime)");
        }

        // A faceted ball (icosahedron, subdivided once: 80 faces), dented at random. Face colour
        // varies a little; faces turned down are darker. uv.y is the height over `top`.
        public void Ball(Vector3 centre, Vector3 scale, Color colour, System.Random rng, float dent, float top)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var p = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new Dictionary<long, int>();
            var faces = new List<int>(240);
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(p, mid, a, b), bc = Mid(p, mid, b, c), ca = Mid(p, mid, c, a);
                faces.Add(a); faces.Add(ab); faces.Add(ca);
                faces.Add(b); faces.Add(bc); faces.Add(ab);
                faces.Add(c); faces.Add(ca); faces.Add(bc);
                faces.Add(ab); faces.Add(bc); faces.Add(ca);
            }
            var q = new Vector3[p.Count];
            for (int i = 0; i < p.Count; i++)
            {
                float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * dent;
                q[i] = centre + Vector3.Scale(p[i].normalized * k, scale);
            }
            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = q[faces[i]], b = q[faces[i + 1]], c = q[faces[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - centre) < 0f)
                {
                    Vector3 swap = b; b = c; c = swap;
                    n = -n;
                }
                float shade = 0.86f + 0.24f * (float)rng.NextDouble();
                if (n.y < -0.3f * n.magnitude) shade *= 0.72f;
                var col = new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1f);
                Triangle(a, b, c, col, Mathf.Clamp01(a.y / top), Mathf.Clamp01(b.y / top), Mathf.Clamp01(c.y / top));
            }
        }

        static int Mid(List<Vector3> p, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int i)) return i;
            p.Add((p[a].normalized + p[b].normalized) * 0.5f);
            i = p.Count - 1;
            cache.Add(key, i);
            return i;
        }

        // A faceted stone: an octahedron-ish ball with random dents, flattened. Pivot at the
        // bottom, half sunk by the scatter.
        public static Mesh Pebble(int seed, Color color)
        {
            var rng = new System.Random(seed);
            var m = new EnvMesh(40);
            const int ring = 6;
            var top = new Vector3(0f, 0.55f + 0.2f * (float)rng.NextDouble(), 0f);
            var bottom = new Vector3(0f, -0.1f, 0f);
            var r = new Vector3[ring];
            for (int i = 0; i < ring; i++)
            {
                float a = (i + 0.3f * (float)rng.NextDouble()) / ring * Mathf.PI * 2f;
                float k = Mathf.Lerp(0.75f, 1.1f, (float)rng.NextDouble());
                r[i] = new Vector3(Mathf.Cos(a) * 0.5f * k, 0.18f + 0.12f * (float)rng.NextDouble(), Mathf.Sin(a) * 0.42f * k);
            }
            for (int i = 0; i < ring; i++)
            {
                Vector3 a = r[i], b = r[(i + 1) % ring];
                float shade = 0.9f + 0.18f * (float)rng.NextDouble();
                Color c = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                // Outward faces: top cone, bottom cone.
                m.Triangle(top, b, a, c, 1f, 1f, 1f);
                m.Triangle(bottom, a, b, c * 0.85f, 1f, 1f, 1f);
            }
            return m.ToMesh("Pebble (runtime)");
        }
    }
}
