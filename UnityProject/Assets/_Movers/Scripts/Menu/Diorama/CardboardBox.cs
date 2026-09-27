using UnityEngine;

namespace Movers
{
    // A moving box: brown cardboard with a strip of packing tape over the top and down two
    // sides. What the walkers of the title screen carry and what the loading screen's mover
    // trips over. One shared mesh (a unit box, pivot at the bottom centre), scaled per box.
    public static class CardboardBox
    {
        static Mesh mesh;

        public static Mesh Mesh
        {
            get
            {
                if (mesh != null) return mesh;
                var m = new LowPolyMesh(40);
                m.Box(new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 1f, 0.5f), Swatch.Cardboard);
                // The tape: a thin band over the top and down the front and back.
                m.Box(new Vector3(-0.09f, 0.995f, -0.505f), new Vector3(0.09f, 1.012f, 0.505f), Swatch.Tape);
                m.Box(new Vector3(-0.09f, 0.72f, -0.512f), new Vector3(0.09f, 1.012f, -0.5f), Swatch.Tape);
                m.Box(new Vector3(-0.09f, 0.72f, 0.5f), new Vector3(0.09f, 1.012f, 0.512f), Swatch.Tape);
                // The flap seam, a darker line across the top.
                m.Box(new Vector3(-0.5f, 0.998f, -0.012f), new Vector3(-0.09f, 1.006f, 0.012f), Swatch.CardboardDark);
                m.Box(new Vector3(0.09f, 0.998f, -0.012f), new Vector3(0.5f, 1.006f, 0.012f), Swatch.CardboardDark);
                mesh = m.ToMesh("CardboardBox (runtime)");
                return mesh;
            }
        }

        // A box of `size` (width, height, depth) under `parent`, at `localPosition` (its bottom
        // centre), on the given layer.
        public static GameObject Create(Transform parent, Vector3 localPosition, Vector3 size, Material template, int layer = 0)
        {
            var go = new GameObject("CardboardBox");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = LowPolyPalette.Land(template);
            return go;
        }
    }
}
