using UnityEngine;

namespace Movers
{
    // Every texture the smoke needs, generated in code.
    //
    // The greybox ships no art (CLAUDE.md rule 13) and a puff of smoke is the one thing a
    // primitive cannot fake: a cube of smoke reads as a cube. So the three maps below are
    // built once at runtime and shared by every cloud and every screen. They are small, they
    // cost about a millisecond, and they keep the feature a single self-contained folder.
    public static class SmokeTextures
    {
        static Texture2D puff;
        static Texture2D noise;
        static Texture2D flat;
        static Texture2D vignette;
        static Material particleMat;

        // Unity objects survive a play-mode exit as "fake null", so every accessor re-checks
        // rather than caching a bool. This also covers the playtest CLI, which disables the
        // domain reload and therefore keeps these statics alive between runs.

        // Soft round blob for the particles: opaque in the middle, gone at the rim.
        public static Texture2D Puff
        {
            get { if (puff == null) puff = Radial(64, 1f, 0f, 0f, 1f); return puff; }
        }

        // The reverse, for the screen edges: clear in the middle, solid at the corners.
        public static Texture2D Vignette
        {
            get { if (vignette == null) vignette = Radial(128, 0f, 1f, 0.45f, 1.1f); return vignette; }
        }

        // One white pixel, for the flat haze layer.
        public static Texture2D Flat
        {
            get
            {
                if (flat == null)
                {
                    flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    flat.SetPixel(0, 0, Color.white);
                    flat.Apply();
                    flat.hideFlags = HideFlags.HideAndDontSave;
                }
                return flat;
            }
        }

        // Tileable cloud noise for the overlay. Scrolled at two scales it reads as moving
        // smoke rather than a grey filter, which is the whole difference between "my screen
        // is dirty" and "I am inside a cloud".
        public static Texture2D Noise
        {
            get { if (noise == null) noise = BlobNoise(128, 90, 1337); return noise; }
        }

        // Alpha-blended unlit material for the particles. Sprites/Default is the safe pick:
        // it is always included in a build, it is unlit, it respects the particle vertex
        // colour, and it never writes depth. The legacy particle shader is tried first only
        // because it sorts marginally better inside a dense cloud.
        public static Material ParticleMaterial
        {
            get
            {
                if (particleMat == null)
                {
                    Shader sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    if (sh == null) sh = Shader.Find("Unlit/Transparent");
                    particleMat = new Material(sh) { mainTexture = Puff };
                    particleMat.hideFlags = HideFlags.HideAndDontSave;
                }
                return particleMat;
            }
        }

        static Texture2D Radial(int size, float centerAlpha, float edgeAlpha, float from, float to)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, d));
                    float a = Mathf.Lerp(centerAlpha, edgeAlpha, k);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            t.SetPixels32(px);
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // Random soft blobs stamped with wrap-around addressing, which is what makes the
        // result tile with no seam. Perlin would not: it has no period at these scales.
        static Texture2D BlobNoise(int size, int blobs, int seed)
        {
            var acc = new float[size * size];
            var rnd = new System.Random(seed);
            for (int b = 0; b < blobs; b++)
            {
                float cx = (float)rnd.NextDouble() * size;
                float cy = (float)rnd.NextDouble() * size;
                float r = size * (0.06f + (float)rnd.NextDouble() * 0.16f);
                float amp = 0.35f + (float)rnd.NextDouble() * 0.65f;
                int R = Mathf.CeilToInt(r);
                for (int y = -R; y <= R; y++)
                {
                    for (int x = -R; x <= R; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y) / r;
                        if (d >= 1f) continue;
                        int ix = Mathf.RoundToInt(cx + x), iy = Mathf.RoundToInt(cy + y);
                        ix = ((ix % size) + size) % size;
                        iy = ((iy % size) + size) % size;
                        float f = 1f - d;
                        acc[iy * size + ix] += amp * f * f;
                    }
                }
            }

            float max = 0.0001f;
            for (int i = 0; i < acc.Length; i++) if (acc[i] > max) max = acc[i];

            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color32[size * size];
            for (int i = 0; i < acc.Length; i++)
            {
                // Lift the floor a little: pure holes in the noise read as clean glass in the
                // middle of a cloud, which breaks the illusion faster than anything else.
                float v = Mathf.Clamp01(0.25f + 0.75f * (acc[i] / max));
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(v * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            t.wrapMode = TextureWrapMode.Repeat;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
