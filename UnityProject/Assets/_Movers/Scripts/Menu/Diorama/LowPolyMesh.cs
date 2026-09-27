using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Movers
{
    // Builds flat-shaded meshes: every triangle has its own three vertices, so its normal is
    // the face's and the light breaks on each facet, which is the whole low-poly look. Colours
    // come from LowPolyPalette (one UV per triangle). Used once when a scene starts (the
    // fields, the road, clouds, puffs), never per frame.
    public sealed class LowPolyMesh
    {
        readonly List<Vector3> verts;
        readonly List<Vector2> uvs;
        readonly List<int> tris;

        public LowPolyMesh(int triangleCapacity = 256)
        {
            verts = new List<Vector3>(triangleCapacity * 3);
            uvs = new List<Vector2>(triangleCapacity * 3);
            tris = new List<int>(triangleCapacity * 3);
        }

        public int TriangleCount => tris.Count / 3;

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uv)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        // a b c d counter-clockwise seen from the side the face shows.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv)
        {
            Triangle(a, b, c, uv);
            Triangle(a, c, d, uv);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---- shapes ----

        // A low-poly ball (an icosahedron, subdivided once), squashed by `scale`, with a little
        // random dent on each corner so no two puffs are the same. For clouds and smoke.
        public void Blob(Vector3 centre, Vector3 scale, Swatch colour, System.Random rng, float dent = 0.12f)
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
            // One subdivision: 80 faces, round enough, still faceted.
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
                Vector3 n = p[i].normalized * k;
                q[i] = centre + Vector3.Scale(n, scale);
            }
            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = q[faces[i]], b = q[faces[i + 1]], c = q[faces[i + 2]];
                // Unity shows a face whose Cross(b - a, c - a) points at the viewer: turn every
                // face outwards, whatever the winding of the table above.
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - centre) < 0f)
                {
                    Vector3 swap = b; b = c; c = swap;
                    n = -n;
                }
                // Faces turned down take the shade colour: a cloud's belly, a puff's underside.
                bool under = n.y < -0.3f * n.magnitude;
                Swatch s = under && colour == Swatch.Cloud ? Swatch.CloudShade : colour;
                Triangle(a, b, c, LowPolyPalette.UV(s, under ? 1 : 3 + rng.Next(3)));
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

        // An axis-aligned box from `min` to `max`, one colour, shades by side.
        public void Box(Vector3 min, Vector3 max, Swatch colour)
        {
            Vector3 a = new Vector3(min.x, min.y, min.z), b = new Vector3(max.x, min.y, min.z);
            Vector3 c = new Vector3(max.x, min.y, max.z), d = new Vector3(min.x, min.y, max.z);
            Vector3 e = new Vector3(min.x, max.y, min.z), f = new Vector3(max.x, max.y, min.z);
            Vector3 g = new Vector3(max.x, max.y, max.z), h = new Vector3(min.x, max.y, max.z);
            Quad(e, h, g, f, LowPolyPalette.UV(colour, 6));   // top
            Quad(a, e, f, b, LowPolyPalette.UV(colour, 4));   // -z
            Quad(c, g, h, d, LowPolyPalette.UV(colour, 4));   // +z
            Quad(d, h, e, a, LowPolyPalette.UV(colour, 3));   // -x
            Quad(b, f, g, c, LowPolyPalette.UV(colour, 3));   // +x
            Quad(a, b, c, d, LowPolyPalette.UV(colour, 1));   // bottom
        }
    }
}
