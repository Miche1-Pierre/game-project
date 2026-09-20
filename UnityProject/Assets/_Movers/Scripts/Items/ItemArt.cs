using UnityEngine;

namespace Movers
{
    // Shared primitives for the two starting items, so neither of them carries its own copy
    // of the same six lines. Greybox rules apply: no prefab, no mesh, no texture (CLAUDE.md 13).
    public static class ItemArt
    {
        public static Material Mat(Color albedo, Color emission)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { color = albedo, hideFlags = HideFlags.HideAndDontSave };
            if (emission != Color.black && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        // A visual piece with no collider of its own: the item carries one hand-sized collider
        // on its root instead, because a 13 mm cigarette is not a thing anyone can click at
        // three metres.
        public static Transform Piece(PrimitiveType type, Transform parent, string name,
                                      Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        // Ground under a point, so an item can be laid down rather than dropped from the sky.
        // Falls back to the point itself when nothing is below, which only happens off the map.
        public static Vector3 GroundUnder(Vector3 point, float clearance = 0.05f)
        {
            if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f,
                                ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * clearance;
            return point;
        }
    }
}
