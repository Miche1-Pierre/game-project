using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Renders Tutorial_01 to a PNG from batch mode, so the greybox can be reviewed
    // without opening the editor.
    //
    //   Unity.exe -batchmode -quit -projectPath <path> \
    //             -executeMethod Movers.EditorTools.MoversScreenshotCLI.Shoot
    //
    // Needs a graphics device, so do NOT pass -nographics.
    public static class MoversScreenshotCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Tutorial_01.unity";
        const string OutDir = "Assets/_Movers/Generated";

        public static void Shoot()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Frame the movable objects, not the building. Hand-picked camera positions kept
            // ending up behind a wall, so the view is derived from where the objects actually are.
            var b = MovablesBounds();
            var c = b.center;
            float reach = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

            // Steep angle so the 3 m walls never occlude the contents.
            Shot("greybox_overview.png", c + new Vector3(0f, reach * 1.25f, -reach * 0.55f), c, 50f, 1600, 900);
            Shot("greybox_topdown.png", c + new Vector3(0f, reach * 1.5f, -reach * 0.05f), c, 45f, 1400, 1000);

            Debug.Log("[Screenshot] framed on bounds center " + c.ToString("F2") + " size " + b.size.ToString("F2"));
            Debug.Log("[Screenshot] done");
        }

        static Bounds MovablesBounds()
        {
            var movables = Object.FindObjectsByType<MovableObject>(FindObjectsSortMode.None);
            bool any = false;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var m in movables)
            {
                var p = m.transform.position;
                var half = m.transform.localScale * 0.5f;
                if (!any) { b = new Bounds(p, m.transform.localScale); any = true; }
                else { b.Encapsulate(p - half); b.Encapsulate(p + half); }
            }
            if (!any) b = new Bounds(Vector3.zero, new Vector3(10f, 3f, 10f));
            return b;
        }

        static void Shot(string file, Vector3 from, Vector3 lookAt, float fov, int w, int h)
        {
            var go = new GameObject("__shotcam");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - from).normalized, Vector3.up);
            cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[Screenshot] wrote " + path);

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }
}
