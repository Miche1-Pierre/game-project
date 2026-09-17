using UnityEngine;

namespace Movers
{
    // A real mirror, on the glass of grandmother's wall mirror.
    //
    // Why it exists: the game is first person, so you never see your own outfit, and there is
    // no second player yet. Something stolen and worn is visible to nobody. A mirror is the
    // cheapest way to give a solo test the reaction the greybox exists to measure, and the
    // house already owns the prop (SM_Mirror_Wall, GrandmaKit). You steal her dressing gown,
    // you go and look at yourself in her glass.
    //
    // Standard planar reflection for the Built-in pipeline: a second camera placed at the
    // mirror image of the rendering camera, with an oblique near plane on the mirror plane so
    // nothing behind the glass leaks in. OnWillRenderObject means it costs nothing while the
    // mirror is off screen.
    // MeshRenderer and not Renderer: RequireComponent cannot add an abstract type, and a
    // mirror without a surface would fail at the least useful moment.
    [RequireComponent(typeof(MeshRenderer))]
    public class MirrorSurface : MonoBehaviour
    {
        [Tooltip("Square texture the reflection renders into. 512 is plenty for a greybox.")]
        public int resolution = 512;

        [Tooltip("Pushes the clip plane slightly out of the glass, so the surface itself does not flicker.")]
        public float clipOffset = 0.03f;

        [Tooltip("Set this if the reflection looks like it is behind you: the surface faces the wrong way.")]
        public bool flipNormal = false;

        [Tooltip("Which material slot holds the glass. The GrandmaKit mirror is one renderer with two submeshes, the gilt frame and the pane, so the reflection goes in slot 1 and the frame is left alone.")]
        public int materialIndex = 0;

        [Tooltip("Distance from this transform to the plane of the glass, along the normal. A prop whose origin sits at the wall needs the pane pushed out to its own front face.")]
        public float planeOffset = 0f;

        public LayerMask reflectMask = ~0;

        Camera reflectionCam;
        RenderTexture rt;
        Material glass;

        // One mirror must not render a second mirror rendering the first one.
        static bool rendering;

        void OnWillRenderObject()
        {
            var src = Camera.current;
            if (src == null || rendering) return;
            if (reflectionCam != null && src == reflectionCam) return;

            EnsureResources(src);

            Vector3 normal = flipNormal ? -transform.forward : transform.forward;
            Vector3 pos = transform.position + normal * planeOffset;

            // Mirror the rendering camera across the plane of the glass.
            float d = -Vector3.Dot(normal, pos) - clipOffset;
            Matrix4x4 reflection = ReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, d));

            reflectionCam.worldToCameraMatrix = src.worldToCameraMatrix * reflection;

            // Oblique projection: the near plane becomes the glass, so the wall behind the
            // mirror and anything in the next room never appear in it.
            Vector4 clipPlane = CameraSpacePlane(reflectionCam, pos, normal, 1f);
            reflectionCam.projectionMatrix = src.CalculateObliqueMatrix(clipPlane);

            reflectionCam.cullingMask = reflectMask;
            reflectionCam.targetTexture = rt;
            reflectionCam.transform.position = reflection.MultiplyPoint(src.transform.position);

            rendering = true;
            GL.invertCulling = true;          // a mirrored view winds its triangles the other way
            reflectionCam.Render();
            GL.invertCulling = false;
            rendering = false;

            glass.mainTexture = rt;
        }

        void EnsureResources(Camera src)
        {
            if (rt == null || rt.width != resolution)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(resolution, resolution, 16) { name = "RT_Mirror_" + name };
                rt.Create();
            }

            if (reflectionCam == null)
            {
                var go = new GameObject("MirrorCamera_" + name) { hideFlags = HideFlags.HideAndDontSave };
                reflectionCam = go.AddComponent<Camera>();
                reflectionCam.enabled = false;        // we drive it by hand, once per visible frame
            }

            reflectionCam.CopyFrom(src);
            reflectionCam.enabled = false;
            reflectionCam.targetTexture = rt;

            if (glass == null)
            {
                // The greybox answer to "which shader shows a render texture in Built-in RP".
                // A mirror is not lit, it shows what the other camera saw.
                glass = new Material(Shader.Find("Unlit/Texture")) { name = "MAT_Mirror_" + name };

                // Only the glass slot is replaced. Assigning renderer.material would overwrite
                // slot 0, which on the kit prop is the gilt frame, and the mirror would come
                // back as a floating rectangle with no frame around it.
                var rend = GetComponent<Renderer>();
                var mats = rend.materials;
                int i = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
                mats[i] = glass;
                rend.materials = mats;
            }
        }

        void OnDisable()
        {
            if (reflectionCam != null) DestroyImmediate(reflectionCam.gameObject);
            reflectionCam = null;
            if (rt != null) rt.Release();
            rt = null;
        }

        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y;      m.m02 = -2f * p.x * p.z;      m.m03 = -2f * p.x * p.w;
            m.m10 = -2f * p.y * p.x;     m.m11 = 1f - 2f * p.y * p.y;  m.m12 = -2f * p.y * p.z;      m.m13 = -2f * p.y * p.w;
            m.m20 = -2f * p.z * p.x;     m.m21 = -2f * p.z * p.y;      m.m22 = 1f - 2f * p.z * p.z;  m.m23 = -2f * p.z * p.w;
            m.m30 = 0f;                  m.m31 = 0f;                   m.m32 = 0f;                   m.m33 = 1f;
            return m;
        }

        Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            Vector3 offset = pos + normal * clipOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(offset);
            Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }
    }
}
