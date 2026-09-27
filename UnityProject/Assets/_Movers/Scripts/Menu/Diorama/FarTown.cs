using UnityEngine;

namespace Movers
{
    // The maps after the grandmother's house, seen far away on the title screen's horizon.
    public enum FarTownKind { SnowyVillage, SeasideTown, City }

    // A town far on the title screen's horizon: one of the next maps, the places the crew will
    // move people out of once the grandmother's house is done. Cheap low-poly shapes built at
    // load into one mesh (vertex coloured, Movers/Environment material), stood on the land by
    // its GroundSnap, never close enough to need detail. No gameplay.
    //
    // Prepared for the unlocking: `mapId` and `displayName` name the map, and a locked town is
    // drawn through a veil of haze (its colours pulled towards the fog); set `unlocked` and call
    // Rebuild() to show it in full colour. Nothing sets it yet: there is one map.
    [DisallowMultipleComponent]
    public sealed class FarTown : MonoBehaviour
    {
        public FarTownKind kind;
        public string mapId = "";
        public string displayName = "";
        public bool unlocked;
        [Tooltip("A Movers/Environment material (vertex colours).")]
        public Material material;
        [Tooltip("How much a locked town fades into the haze, 0 to 1.")]
        [Range(0f, 1f)] public float lockedHaze = 0.3f;
        public float scale = 1f;
        public int seed = 1;

        Mesh mesh;
        Material tinted;

        void Start() => Rebuild();

        public void Rebuild()
        {
            var m = new EnvMesh(2000);
            var rng = new System.Random(seed);
            switch (kind)
            {
                case FarTownKind.SnowyVillage: SnowyVillage(m, rng); break;
                case FarTownKind.SeasideTown: SeasideTown(m, rng); break;
                default: City(m, rng); break;
            }
            if (mesh != null) Destroy(mesh);
            mesh = m.ToMesh("FarTown " + kind + " (runtime)");

            var mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var r = GetComponent<MeshRenderer>();
            if (r == null) r = gameObject.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (tinted != null) Destroy(tinted);
            tinted = null;
            if (material != null && !unlocked)
            {
                tinted = new Material(material) { name = material.name + " (locked haze)", hideFlags = HideFlags.DontSave };
                tinted.color = Color.Lerp(material.color, RenderSettings.fogColor, lockedHaze);
            }
            r.sharedMaterial = tinted != null ? tinted : material;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (tinted != null) Destroy(tinted);
        }

        // ---- the three places ----

        static readonly Color Snow = new Color(0.97f, 0.97f, 1f), Rock = new Color(0.55f, 0.53f, 0.56f);
        static readonly Color Pine = new Color(0.23f, 0.38f, 0.27f), Timber = new Color(0.52f, 0.36f, 0.24f);
        static readonly Color Plaster = new Color(0.93f, 0.88f, 0.78f), Tile = new Color(0.72f, 0.36f, 0.27f);
        static readonly Color Sea = new Color(0.36f, 0.62f, 0.78f), Sand = new Color(0.9f, 0.82f, 0.6f);

        // A big snowy mountain and two smaller peaks, a village of chalets with white roofs and
        // a little church at their foot, firs round it.
        void SnowyVillage(EnvMesh m, System.Random rng)
        {
            float s = scale;
            Mountain(m, rng, new Vector3(0f, 0f, 60f) * s, 120f * s, 165f * s, 0.52f);
            Mountain(m, rng, new Vector3(-120f, 0f, 30f) * s, 80f * s, 105f * s, 0.6f);
            Mountain(m, rng, new Vector3(115f, 0f, 45f) * s, 90f * s, 120f * s, 0.58f);
            for (int i = 0; i < 9; i++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * 90f * s, z = ((float)rng.NextDouble() - 0.8f) * 40f * s;
                House(m, new Vector3(x, 0f, z), 9f * s, 7f * s, 6f * s, (float)rng.NextDouble() * 40f - 20f,
                      i % 3 == 0 ? Timber : Plaster, Snow, 0.6f);
            }
            Church(m, new Vector3(10f, 0f, -12f) * s, s, Plaster, Snow);
            for (int i = 0; i < 40; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = (60f + 80f * (float)rng.NextDouble()) * s;
                Fir(m, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.6f), (9f + 7f * (float)rng.NextDouble()) * s, true);
            }
        }

        // Pastel houses on a bay, a sandy strip, the sea, and a striped lighthouse on the point.
        void SeasideTown(EnvMesh m, System.Random rng)
        {
            float s = scale;
            // The sea: a wide flat fan behind the town, a little lower, with the beach in front.
            Fan(m, new Vector3(0f, -1f, 20f) * s, 40f * s, 380f * s, -40f, 220f, Sea);
            Fan(m, new Vector3(0f, -0.6f, 20f) * s, 22f * s, 44f * s, -40f, 220f, Sand);
            Color[] walls =
            {
                new Color(0.96f, 0.86f, 0.75f), new Color(0.72f, 0.84f, 0.92f), new Color(0.97f, 0.78f, 0.78f),
                new Color(0.95f, 0.93f, 0.8f), new Color(0.8f, 0.9f, 0.8f),
            };
            for (int i = 0; i < 12; i++)
            {
                float a = Mathf.Lerp(-150f, -30f, i / 11f) * Mathf.Deg2Rad;
                float r = (48f + 18f * (float)rng.NextDouble()) * s;
                var p = new Vector3(Mathf.Cos(a) * r, 0f, 20f * s + Mathf.Sin(a) * r * 0.55f);
                House(m, p, 8f * s, (7f + 6f * (float)rng.NextDouble()) * s, 7f * s, -Mathf.Rad2Deg * a - 90f, walls[i % walls.Length], Tile, 0.45f);
            }
            Lighthouse(m, new Vector3(70f, 0f, 38f) * s, s);
        }

        // A skyline: towers of every height, a few with a lighter band of windows, one spire.
        void City(EnvMesh m, System.Random rng)
        {
            float s = scale;
            Color[] tones =
            {
                new Color(0.62f, 0.66f, 0.74f), new Color(0.72f, 0.72f, 0.7f), new Color(0.55f, 0.6f, 0.68f),
                new Color(0.8f, 0.76f, 0.68f), new Color(0.48f, 0.53f, 0.62f),
            };
            for (int i = 0; i < 26; i++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * 150f * s, z = ((float)rng.NextDouble() - 0.5f) * 60f * s;
                float centre = 1f - Mathf.Abs(x) / (80f * s);
                float h = (18f + (70f + 50f * (float)rng.NextDouble()) * Mathf.Clamp01(centre) + 20f * (float)rng.NextDouble()) * s;
                float w = (10f + 10f * (float)rng.NextDouble()) * s, d = (10f + 8f * (float)rng.NextDouble()) * s;
                Color c = tones[rng.Next(tones.Length)];
                Box(m, new Vector3(x - w * 0.5f, 0f, z - d * 0.5f), new Vector3(x + w * 0.5f, h, z + d * 0.5f), c);
                // Bands of windows catching the sun.
                for (float y = 6f * s; y < h - 4f * s; y += 9f * s)
                    if (rng.NextDouble() < 0.5)
                        Box(m, new Vector3(x - w * 0.51f, y, z - d * 0.51f), new Vector3(x + w * 0.51f, y + 2.2f * s, z + d * 0.51f), Color.Lerp(c, new Color(1f, 0.93f, 0.75f), 0.55f));
                if (i == 0)
                {
                    // The tallest, with a spire.
                    Box(m, new Vector3(-8f, 0f, -8f) * s, new Vector3(8f, 150f, 8f) * s, tones[0]);
                    Spire(m, new Vector3(0f, 150f * s, 0f), 5f * s, 35f * s, tones[1]);
                }
            }
        }

        // ---- shapes ----

        void Mountain(EnvMesh m, System.Random rng, Vector3 c, float radius, float height, float snowLine)
        {
            const int sides = 9;
            var baseRing = new Vector3[sides];
            var midRing = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.3f * (float)rng.NextDouble()) / sides * Mathf.PI * 2f;
                float k = Mathf.Lerp(0.8f, 1.15f, (float)rng.NextDouble());
                baseRing[i] = c + new Vector3(Mathf.Cos(a) * radius * k, -8f, Mathf.Sin(a) * radius * k);
                float km = snowLine * Mathf.Lerp(0.9f, 1.1f, (float)rng.NextDouble());
                midRing[i] = c + new Vector3(Mathf.Cos(a) * radius * (1f - km) * k, height * km, Mathf.Sin(a) * radius * (1f - km) * k);
            }
            Vector3 top = c + new Vector3(radius * 0.08f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                float shade = 0.85f + 0.25f * (float)rng.NextDouble();
                Outward(m, baseRing[i], baseRing[j], midRing[j], c, Rock * shade);
                Outward(m, baseRing[i], midRing[j], midRing[i], c, Rock * shade * 0.95f);
                Outward(m, midRing[i], midRing[j], top, c, Snow * (0.92f + 0.08f * (float)rng.NextDouble()));
            }
        }

        void House(EnvMesh m, Vector3 p, float w, float h, float d, float yaw, Color wall, Color roof, float roofRise)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 X(float x, float y, float z) => p + q * new Vector3(x, y, z);
            float hw = w * 0.5f, hd = d * 0.5f;
            // Walls.
            Vector3 a = X(-hw, 0, -hd), b = X(hw, 0, -hd), c = X(hw, 0, hd), e = X(-hw, 0, hd);
            Vector3 a2 = X(-hw, h, -hd), b2 = X(hw, h, -hd), c2 = X(hw, h, hd), e2 = X(-hw, h, hd);
            Outward(m, a, b, b2, p + Vector3.up * h * 0.5f, wall); Outward(m, a, b2, a2, p + Vector3.up * h * 0.5f, wall);
            Outward(m, b, c, c2, p + Vector3.up * h * 0.5f, wall * 0.9f); Outward(m, b, c2, b2, p + Vector3.up * h * 0.5f, wall * 0.9f);
            Outward(m, c, e, e2, p + Vector3.up * h * 0.5f, wall); Outward(m, c, e2, c2, p + Vector3.up * h * 0.5f, wall);
            Outward(m, e, a, a2, p + Vector3.up * h * 0.5f, wall * 0.9f); Outward(m, e, a2, e2, p + Vector3.up * h * 0.5f, wall * 0.9f);
            // A pitched roof over the width, overhanging a little, and its two gables.
            float o = 0.6f, ridge = h + w * roofRise * 0.5f;
            Vector3 r1 = X(0, ridge, -hd - o), r2 = X(0, ridge, hd + o);
            Vector3 l1 = X(-hw - o, h - 0.4f, -hd - o), l2 = X(-hw - o, h - 0.4f, hd + o);
            Vector3 q1 = X(hw + o, h - 0.4f, -hd - o), q2 = X(hw + o, h - 0.4f, hd + o);
            Vector3 inside = p + Vector3.up * h * 0.5f;
            Outward(m, l1, r1, r2, inside, roof); Outward(m, l1, r2, l2, inside, roof);
            Outward(m, q1, q2, r2, inside, roof * 0.88f); Outward(m, q1, r2, r1, inside, roof * 0.88f);
            Outward(m, X(-hw, h, -hd), X(hw, h, -hd), X(0, ridge, -hd), inside, wall);
            Outward(m, X(hw, h, hd), X(-hw, h, hd), X(0, ridge, hd), inside, wall);
        }

        void Church(EnvMesh m, Vector3 p, float s, Color wall, Color roof)
        {
            House(m, p, 10f * s, 9f * s, 16f * s, 0f, wall, roof, 0.8f);
            Box(m, p + new Vector3(-3f, 0f, -12f) * s, p + new Vector3(3f, 22f, -6f) * s, wall);
            Spire(m, p + new Vector3(0f, 22f, -9f) * s, 3.6f * s, 12f * s, roof);
        }

        void Fir(EnvMesh m, Vector3 p, float h, bool snowy)
        {
            Cone(m, p, h * 0.28f, h * 0.55f, 0f, Pine);
            Cone(m, p + Vector3.up * h * 0.35f, h * 0.22f, h * 0.65f, 0f, snowy ? Color.Lerp(Pine, Snow, 0.55f) : Pine);
        }

        void Lighthouse(EnvMesh m, Vector3 p, float s)
        {
            const int stripes = 6;
            float h = 34f * s, r0 = 4f * s, r1 = 2.8f * s;
            Color red = new Color(0.82f, 0.22f, 0.2f);
            for (int i = 0; i < stripes; i++)
            {
                float y0 = h * i / stripes, y1 = h * (i + 1) / stripes;
                Prism(m, p, Mathf.Lerp(r0, r1, (float)i / stripes), Mathf.Lerp(r0, r1, (float)(i + 1) / stripes), y0, y1, i % 2 == 0 ? Color.white : red);
            }
            Prism(m, p, r1 * 1.3f, r1 * 1.3f, h, h + 1f * s, new Color(0.25f, 0.25f, 0.27f));
            Prism(m, p, r1 * 0.9f, r1 * 0.9f, h + 1f * s, h + 4.5f * s, new Color(1f, 0.92f, 0.6f));
            Cone(m, p + Vector3.up * (h + 4.5f * s), r1 * 1.1f, 3.5f * s, 0f, red * 0.8f);
            // The rocks of the point under it.
            Prism(m, p + Vector3.down * 2f * s, 9f * s, 6f * s, 0f, 3f * s, Rock);
        }

        void Spire(EnvMesh m, Vector3 p, float r, float h, Color c) => Cone(m, p, r, h, 0f, c);

        static void Cone(EnvMesh m, Vector3 p, float r, float h, float y0, Color c)
        {
            const int sides = 7;
            Vector3 top = p + Vector3.up * (y0 + h);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 b0 = p + new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r);
                Vector3 b1 = p + new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                Outward(m, b0, b1, top, p + Vector3.up * (y0 + h * 0.3f), c * (0.88f + 0.12f * (i % 2)));
            }
        }

        static void Prism(EnvMesh m, Vector3 p, float r0, float r1, float y0, float y1, Color c)
        {
            const int sides = 8;
            Vector3 inside = p + Vector3.up * (y0 + y1) * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 b0 = p + new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0), b1 = p + new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0);
                Vector3 t0 = p + new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1), t1 = p + new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1);
                Color k = c * (0.9f + 0.1f * (i % 2));
                Outward(m, b0, b1, t1, inside, k);
                Outward(m, b0, t1, t0, inside, k);
                Outward(m, t0, t1, p + Vector3.up * y1, p + Vector3.up * (y1 - 1f), k);
            }
        }

        static void Box(EnvMesh m, Vector3 min, Vector3 max, Color c)
        {
            Vector3 centre = (min + max) * 0.5f;
            Vector3 a = new Vector3(min.x, min.y, min.z), b = new Vector3(max.x, min.y, min.z);
            Vector3 cc = new Vector3(max.x, min.y, max.z), d = new Vector3(min.x, min.y, max.z);
            Vector3 e = new Vector3(min.x, max.y, min.z), f = new Vector3(max.x, max.y, min.z);
            Vector3 g = new Vector3(max.x, max.y, max.z), h = new Vector3(min.x, max.y, max.z);
            Quad(m, e, h, g, f, centre, c * 1.05f);
            Quad(m, a, e, f, b, centre, c);
            Quad(m, cc, g, h, d, centre, c);
            Quad(m, d, h, e, a, centre, c * 0.88f);
            Quad(m, b, f, g, cc, centre, c * 0.88f);
        }

        // A flat fan (a ring sector) between two radii and two angles (degrees).
        static void Fan(EnvMesh m, Vector3 c, float r0, float r1, float fromDeg, float toDeg, Color colour)
        {
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(fromDeg, toDeg, (i + 1) / (float)steps) * Mathf.Deg2Rad;
                Vector3 i0 = c + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0), i1 = c + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                Vector3 o0 = c + new Vector3(Mathf.Cos(a0) * r1, 0f, Mathf.Sin(a0) * r1), o1 = c + new Vector3(Mathf.Cos(a1) * r1, 0f, Mathf.Sin(a1) * r1);
                UpQuad(m, i0, o0, o1, i1, colour * (0.97f + 0.03f * (i % 2)));
            }
        }

        static void Quad(EnvMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside, Color colour)
        {
            Outward(m, a, b, c, inside, colour);
            Outward(m, a, c, d, inside, colour);
        }

        static void UpQuad(EnvMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
        {
            Quad(m, a, b, c, d, (a + c) * 0.5f + Vector3.down, colour);
        }

        // A triangle turned so its face looks away from `inside`.
        static void Outward(EnvMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Color colour)
        {
            colour.a = 1f;
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) m.Triangle(a, c, b, colour, 1f, 1f, 1f);
            else m.Triangle(a, b, c, colour, 1f, 1f, 1f);
        }
    }
}
