using UnityEngine;

namespace Movers
{
    // Every texture the smoke needs, generated in code.
    //
    // The greybox ships no art (CLAUDE.md rule 13) and a puff of smoke is the one thing a
    // primitive cannot fake: a cube of smoke reads as a cube. So the maps below are
    // built once at runtime and shared by every cloud and every screen. They are small, they
    // cost about a millisecond, and they keep the feature a single self-contained folder.
    public static class SmokeTextures
    {
        static Texture2D puff;
        static Texture2D noise;
        static Texture2D flat;
        static Texture2D vignette;
        static Material particleMat;
        static Texture2D wispSheet;
        static Material wispMat;

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

        // Four torn, uneven puffs in a 2 x 2 sheet: a round blob reads as a bubble up close, and
        // the cigarette's smoke is seen from 10 cm. Two are soft and full, two are pulled into
        // strands. Used by the cigarette's thread, the exhale and the clouds (UseWispSheet), each
        // particle keeping one of the four for its whole life.
        public static Texture2D WispSheet
        {
            get { if (wispSheet == null) wispSheet = BuildWispSheet(256, 4242); return wispSheet; }
        }

        // ParticleMaterial with the wisp sheet. A separate material: everything else that uses
        // ParticleMaterial (dust, explosions) draws the whole texture, not a quarter of it.
        public static Material WispMaterial
        {
            get
            {
                if (wispMat == null)
                {
                    wispMat = new Material(ParticleMaterial) { mainTexture = WispSheet };
                    wispMat.hideFlags = HideFlags.HideAndDontSave;
                }
                return wispMat;
            }
        }

        // Makes a particle system draw one random tile of WispSheet per particle.
        public static void UseWispSheet(ParticleSystem ps)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            // Random between two constants is drawn once per particle: one tile, kept.
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;
        }

        static Texture2D BuildWispSheet(int size, int seed)
        {
            int tile = size / 2;
            var px = new Color32[size * size];
            var rnd = new System.Random(seed);
            for (int k = 0; k < 4; k++)
            {
                int tx = k % 2, ty = k / 2;
                float ox = (float)rnd.NextDouble() * 200f, oy = (float)rnd.NextDouble() * 200f;
                float stretch = k >= 2 ? 2.2f : 1f;          // the last two: strands
                float angle = (float)rnd.NextDouble() * Mathf.PI;
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                for (int y = 0; y < tile; y++)
                {
                    for (int x = 0; x < tile; x++)
                    {
                        float u = (x + 0.5f) / tile * 2f - 1f;
                        float v = (y + 0.5f) / tile * 2f - 1f;
                        float ru = u * ca - v * sa, rv = u * sa + v * ca;
                        // The rim wanders with a coarse noise, the body is mottled by a finer one.
                        float n1 = Remap(Fbm(ru * 1.7f / stretch + ox, rv * 1.7f * stretch + oy, 4));
                        float n2 = Remap(Fbm(ru * 3.4f / stretch + oy, rv * 3.4f * stretch + ox, 3));
                        float d = Mathf.Sqrt(u * u + v * v);
                        float rim = 0.55f + 0.42f * n1;
                        float body = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rim * 0.2f, rim, d));
                        float a = body * body * Mathf.Lerp(0.4f, 1f, n2);
                        // Clear at the tile's border, so neighbours never bleed in through the mip maps.
                        float edge = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))) * 10f);
                        a *= edge;
                        px[(ty * tile + y) * size + tx * tile + x] =
                            new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                }
            }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.SetPixels32(px);
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * f, y * f);
                norm += amp;
                amp *= 0.5f;
                f *= 2.03f;
            }
            return sum / norm;
        }

        // Perlin sums bunch around the middle: spread them back over 0..1.
        static float Remap(float n) => Mathf.Clamp01((n - 0.3f) / 0.4f);

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
