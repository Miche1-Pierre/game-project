using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The little things in her hand: a book, a cup, a match, a watering can, a spoon, the keys.
    // Greybox props from primitive meshes, built the first time each is needed, parented to the
    // right-hand socket (HandSockets), shown one at a time.
    //
    // They are pure visuals: no collider, no MovableObject. Whether her cup should be the real,
    // stealable cup from the counter is a design question (REPORT.md), not decided here.
    [RequireComponent(typeof(HandSockets))]
    public sealed class GrandmaProps : MonoBehaviour
    {
        HandSockets sockets;
        readonly GameObject[] built = new GameObject[8];
        readonly List<Material> materials = new List<Material>();
        Mesh cube, cylinder, sphere;

        public GrandmaProp Shown { get; private set; } = GrandmaProp.None;

        void Awake()
        {
            sockets = GetComponent<HandSockets>();
        }

        public void Show(GrandmaProp prop)
        {
            if (prop == Shown) return;
            HideAll();
            if (prop == GrandmaProp.None || prop == GrandmaProp.Auto) return;
            GameObject go = Get(prop);
            if (go == null) return;
            go.SetActive(true);
            Shown = prop;
        }

        public void HideAll()
        {
            for (int i = 0; i < built.Length; i++)
                if (built[i] != null) built[i].SetActive(false);
            Shown = GrandmaProp.None;
        }

        GameObject Get(GrandmaProp prop)
        {
            int i = (int)prop;
            if (built[i] != null) return built[i];
            Transform socket = sockets != null ? sockets.Right : null;
            if (socket == null) return null;

            var root = new GameObject("Prop_" + prop).transform;
            root.SetParent(socket, false);
            // Sized in metres whatever the scale of the bones above.
            float s = socket.lossyScale.x;
            if (s > 1e-4f) root.localScale = Vector3.one / s;

            switch (prop)
            {
                case GrandmaProp.Book:
                    Piece(root, Cube, new Vector3(0f, 0.035f, 0.02f), new Vector3(0.15f, 0.035f, 0.21f), new Color(0.5f, 0.12f, 0.1f));
                    Piece(root, Cube, new Vector3(0.004f, 0.035f, 0.02f), new Vector3(0.14f, 0.028f, 0.2f), new Color(0.93f, 0.9f, 0.8f));
                    break;
                case GrandmaProp.Cup:
                    Piece(root, Cylinder, new Vector3(0f, 0.05f, 0.03f), new Vector3(0.08f, 0.045f, 0.08f), new Color(0.95f, 0.95f, 0.92f), new Vector3(0f, 0f, 90f));
                    break;
                case GrandmaProp.Match:
                    Piece(root, Cube, new Vector3(0f, 0.01f, 0.06f), new Vector3(0.006f, 0.006f, 0.06f), new Color(0.85f, 0.7f, 0.45f));
                    Piece(root, Sphere, new Vector3(0f, 0.01f, 0.092f), new Vector3(0.012f, 0.012f, 0.014f), new Color(0.8f, 0.1f, 0.05f));
                    break;
                case GrandmaProp.WateringCan:
                    Piece(root, Cylinder, new Vector3(0f, 0.11f, 0.03f), new Vector3(0.16f, 0.09f, 0.16f), new Color(0.25f, 0.5f, 0.32f), new Vector3(0f, 0f, 90f));
                    Piece(root, Cylinder, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.025f, 0.1f, 0.025f), new Color(0.25f, 0.5f, 0.32f), new Vector3(60f, 0f, 0f));
                    break;
                case GrandmaProp.Spoon:
                    Piece(root, Cube, new Vector3(0f, 0.01f, 0.06f), new Vector3(0.012f, 0.006f, 0.22f), new Color(0.6f, 0.45f, 0.3f));
                    Piece(root, Sphere, new Vector3(0f, 0.01f, 0.18f), new Vector3(0.04f, 0.012f, 0.05f), new Color(0.6f, 0.45f, 0.3f));
                    break;
                case GrandmaProp.Keys:
                    Piece(root, Cylinder, new Vector3(0f, 0.02f, 0.02f), new Vector3(0.035f, 0.003f, 0.035f), new Color(0.75f, 0.62f, 0.25f));
                    Piece(root, Cube, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.02f, 0.004f, 0.06f), new Color(0.8f, 0.68f, 0.3f));
                    break;
            }
            root.gameObject.SetActive(false);
            built[i] = root.gameObject;
            return built[i];
        }

        Mesh Cube => cube != null ? cube : (cube = PrimitiveMesh(PrimitiveType.Cube));
        Mesh Cylinder => cylinder != null ? cylinder : (cylinder = PrimitiveMesh(PrimitiveType.Cylinder));
        Mesh Sphere => sphere != null ? sphere : (sphere = PrimitiveMesh(PrimitiveType.Sphere));

        // Unity's own primitive mesh, borrowed from a throwaway primitive (as ExplosionFX does).
        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var tmp = GameObject.CreatePrimitive(type);
            Mesh mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            tmp.SetActive(false);
            Destroy(tmp);
            return mesh;
        }

        void Piece(Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Color color, Vector3 euler = default)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialFor(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Material MaterialFor(Color color)
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i].color == color) return materials[i];
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            var m = new Material(shader) { color = color, name = "GrandmaProp" };
            materials.Add(m);
            return m;
        }

        void OnDestroy()
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) Destroy(materials[i]);
            materials.Clear();
        }
    }
}
