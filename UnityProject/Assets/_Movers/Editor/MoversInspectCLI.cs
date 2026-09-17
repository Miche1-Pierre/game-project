using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Reports what the swap actually produced: which mesh, which materials, what world size.
    // Exists because "it looks white in the render" is a symptom, not a diagnosis.
    public static class MoversInspectCLI
    {
        public static void Report()
        {
            EditorSceneManager.OpenScene("Assets/_Movers/Scenes/Tutorial_01.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();
            foreach (var mo in Object.FindObjectsByType<MovableObject>(FindObjectsSortMode.None))
            {
                var v = mo.transform.Find(MoversVisualSwap.VisualName);
                sb.Length = 0;
                sb.Append(mo.name).Append(" | box=").Append(mo.transform.localScale.ToString("F2"));
                if (v == null) { sb.Append(" | NO VISUAL"); Debug.Log("[Inspect] " + sb); continue; }

                var mf = v.GetComponentInChildren<MeshFilter>();
                sb.Append(" | mesh=").Append(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "NONE");
                sb.Append(" | vscale=").Append(v.localScale.ToString("F3"));

                var r = v.GetComponentInChildren<Renderer>();
                if (r == null) sb.Append(" | NO RENDERER");
                else
                {
                    sb.Append(" | mats=");
                    foreach (var m in r.sharedMaterials)
                        sb.Append(m == null ? "NULL" : m.name).Append(",");
                    if (r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null)
                    {
                        var s = r.sharedMaterials[0].shader;
                        sb.Append(" shader=").Append(s == null ? "NULL" : s.name);
                        if (r.sharedMaterials[0].HasProperty("_MainTex"))
                        {
                            var t = r.sharedMaterials[0].GetTexture("_MainTex");
                            sb.Append(" tex=").Append(t == null ? "NONE" : t.name);
                        }
                    }
                }
                Debug.Log("[Inspect] " + sb);
            }
            Debug.Log("[Inspect] done");
        }
    }
}
