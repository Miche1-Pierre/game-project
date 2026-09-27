using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The flat colours of the title screen's landscape and of the loading screen's little stage.
    //
    // Generated meshes (the fields, the road, the clouds) take their colour from a small palette
    // texture, one texel per colour and shade, the way BrokenVector's trees read their colour
    // sheet: every triangle's UVs point at one texel, so a face is one flat colour and a whole
    // landscape is one material and one draw call. Simple props (a cardboard box, a puddle) use
    // a plain coloured material instead, cached per colour.
    //
    // Everything is made at runtime from one Standard material asset (the scene or the stage
    // settings hand it over), so nothing here needs a texture asset. Made once and kept for the
    // session: it is a few hundred bytes of texture and a handful of materials.
    public enum Swatch
    {
        GrassA, GrassB, GrassC, Meadow, Hedge, Wheat, WheatDark, Soil, SoilDark, Verge,
        Asphalt, AsphaltEdge, Line, Gravel, Cardboard, CardboardDark, Tape, Cloud, CloudShade,
        Puddle, FarHill, Lawn, Smoke, Count
    }

    public static class LowPolyPalette
    {
        public const int Shades = 8;          // rows: darker to lighter variants of a colour

        static Texture2D texture;
        static Material landMaterial;
        static readonly Dictionary<Color, Material> solids = new Dictionary<Color, Material>();

        // Late afternoon, warm: the greens lean yellow, the shadows are carried by the light.
        static readonly Color[] Colors =
        {
            Hex(0x86AE55), Hex(0x78A34C), Hex(0x93B862), Hex(0xA3C46C), Hex(0x557F3E),
            Hex(0xD9C27A), Hex(0xC8AC62), Hex(0xA08064), Hex(0x8A6B50), Hex(0xA6C878),
            Hex(0x6E6862), Hex(0x8A847D), Hex(0xF3E7C9), Hex(0xCDBDA2), Hex(0xC99A64),
            Hex(0xA87C4B), Hex(0xDEC38C), Hex(0xFFF7EC), Hex(0xEADCD2), Hex(0x8DB9D6),
            Hex(0x9DB88A), Hex(0x8CB85E), Hex(0xE4DCD2),
        };

        public static Color ColorOf(Swatch s) => Colors[(int)s];

        // The palette texture: Swatch.Count columns, Shades rows, point sampled.
        public static Texture2D Texture
        {
            get
            {
                if (texture != null) return texture;
                int w = (int)Swatch.Count;
                texture = new Texture2D(w, Shades, TextureFormat.RGBA32, false, false)
                {
                    name = "MenuPalette (runtime)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                var px = new Color32[w * Shades];
                for (int y = 0; y < Shades; y++)
                {
                    // 0.93 .. 1.05: enough to tell two facets apart, never a different colour.
                    float k = Mathf.Lerp(0.93f, 1.05f, y / (float)(Shades - 1));
                    for (int x = 0; x < w; x++)
                    {
                        Color c = Colors[x] * k;
                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                }
                texture.SetPixels32(px);
                texture.Apply(false, true);
                return texture;
            }
        }

        // The UV of one colour and shade, at the texel's centre.
        public static Vector2 UV(Swatch s, int shade)
        {
            shade = Mathf.Clamp(shade, 0, Shades - 1);
            return new Vector2(((int)s + 0.5f) / (float)Swatch.Count, (shade + 0.5f) / Shades);
        }

        // The material every generated mesh shares. `template` is a Standard material asset (so
        // the shader is in the build); without one, the Standard shader is looked up by name.
        public static Material Land(Material template)
        {
            if (landMaterial != null) return landMaterial;
            landMaterial = Make(template, Color.white);
            landMaterial.name = "MenuLand (runtime)";
            landMaterial.mainTexture = Texture;
            return landMaterial;
        }

        // A plain matte colour, cached.
        public static Material Solid(Material template, Color c)
        {
            if (solids.TryGetValue(c, out var m) && m != null) return m;
            m = Make(template, c);
            m.name = "MenuSolid (runtime)";
            solids[c] = m;
            return m;
        }

        public static Material Solid(Material template, Swatch s) => Solid(template, ColorOf(s));

        static Material Make(Material template, Color c)
        {
            Material m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.hideFlags = HideFlags.DontSave;
            m.mainTexture = null;
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
