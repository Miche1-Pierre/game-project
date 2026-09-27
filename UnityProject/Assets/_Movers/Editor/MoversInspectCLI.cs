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

        // Reports one imported model by path: what the game actually gets from a file Blender
        // wrote, to compare with the script's own ROBE_ / MEASURE lines.
        //
        //   Unity.exe -batchmode -projectPath C:\dev\game-project\UnityProject \
        //     -executeMethod Movers.EditorTools.MoversInspectCLI.ReportAsset \
        //     -asset Assets/_Project/Art/Crew/SM_Crew_Chest_Bathrobe.fbx \
        //     -body Assets/Floreswa/Models/male01_1.fbx -logFile inspect.log
        //
        // No -quit needed, it exits by itself with 0 when the import is clean and 2 otherwise.
        // -body is optional: for a skinned piece it checks that every bone the piece is weighted
        // to exists in the body's armature, which is what CrewEquip re-binds by name.
        public static void ReportAsset()
        {
            string path = Arg("-asset");
            var root = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
            {
                Debug.LogError("[Inspect] nothing imported at '" + path + "', pass -asset Assets/...");
                Exit(2);
                return;
            }

            if (AssetImporter.GetAtPath(path) is ModelImporter mi)
                Debug.Log("[Inspect] importer globalScale=" + mi.globalScale + " useFileScale=" + mi.useFileScale
                          + " bakeAxisConversion=" + mi.bakeAxisConversion + " importNormals=" + mi.importNormals
                          + " isReadable=" + mi.isReadable
                          + " materialImportMode=" + mi.materialImportMode + " animationType=" + mi.animationType);

            var bodyBones = new System.Collections.Generic.HashSet<string>();
            var rigNames = new System.Collections.Generic.HashSet<string>();
            SkinnedMeshRenderer best = null;
            string bodyPath = Arg("-body");
            if (!string.IsNullOrEmpty(bodyPath))
            {
                var body = AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath);
                if (body != null)
                    foreach (var s in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (best == null || s.bones.Length > best.bones.Length) best = s;
                if (best == null) Debug.LogWarning("[Inspect] no skinned body at '" + bodyPath + "', bone check skipped");
                else foreach (var b in best.bones) if (b != null) bodyBones.Add(b.name);

                // Leaf bones carry no weight and are not in the body renderer's bone list, but
                // they are still transforms of the rig, since the body imports with
                // optimizeGameObjects off, and CrewEquip resolves them there. Same rule here.
                if (best != null && best.rootBone != null)
                    foreach (var t in best.rootBone.GetComponentsInChildren<Transform>(true))
                        rigNames.Add(t.name);
            }

            bool ok = true;
            var sb = new StringBuilder();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                sb.Length = 0;
                Mesh mesh;
                if (r is SkinnedMeshRenderer smr)
                {
                    mesh = smr.sharedMesh;
                    sb.Append("skinned bones=").Append(smr.bones.Length)
                      .Append(" root=").Append(smr.rootBone != null ? smr.rootBone.name : "NONE");
                    if (bodyBones.Count > 0)
                    {
                        var missing = new System.Collections.Generic.List<string>();
                        int leaves = 0;
                        foreach (var b in smr.bones)
                        {
                            if (b != null && bodyBones.Contains(b.name)) continue;
                            if (b != null && rigNames.Contains(b.name)) { leaves++; continue; }
                            missing.Add(b == null ? "<null>" : b.name);
                        }
                        sb.Append(" in body=").Append(smr.bones.Length - missing.Count - leaves).Append("/").Append(smr.bones.Length)
                          .Append(" plus ").Append(leaves).Append(" rig leaves");
                        if (missing.Count > 0)
                        {
                            sb.Append(" missing=").Append(string.Join(",", missing));
                            ok = false;
                        }
                    }
                    if (best != null && !ReportBindPoses(smr, best)) ok = false;
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    mesh = mf != null ? mf.sharedMesh : null;
                    sb.Append("static");
                }
                if (mesh == null) { Debug.LogError("[Inspect] " + r.name + " has no mesh"); ok = false; continue; }

                int tris = 0;
                sb.Append(" | submeshes=").Append(mesh.subMeshCount).Append(" tris=");
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    int t = (int)mesh.GetIndexCount(i) / 3;
                    tris += t;
                    sb.Append(i == 0 ? "" : "+").Append(t);
                }
                sb.Append(" (").Append(tris).Append(") verts=").Append(mesh.vertexCount)
                  .Append(" normals=").Append(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal))
                  .Append(" bounds=").Append(mesh.bounds.size.ToString("F3"));

                sb.Append(" | mats=");
                foreach (var m in r.sharedMaterials)
                {
                    // The magenta error shader is an import that failed quietly, not a colour.
                    bool bad = m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader";
                    if (bad) ok = false;
                    sb.Append(m == null ? "NULL" : m.name).Append("(")
                      .Append(m == null || m.shader == null ? "NO SHADER" : m.shader.name)
                      .Append(m != null && m.HasProperty("_Color") ? " #" + ColorUtility.ToHtmlStringRGB(m.color) : "")
                      .Append("),");
                }
                Debug.Log("[Inspect] " + r.name + " | " + sb);
            }
            Debug.Log(ok ? "[Inspect] VERDICT: imports clean" : "[Inspect] VERDICT: failures above");
            Exit(ok ? 0 : 2);
        }

        // Every bone the piece is weighted to, against the body's bind pose, one line each, then
        // the joint fit CrewEquip uses to move the piece onto the body.
        //
        // A piece re-imported through Blender disagrees with the body's bind poses on every bone,
        // because the re-import turns the bone frames (the robe, 2026-09-25: arms off by 160 to
        // 170 degrees). That is expected and handled, so the table is information. The verdict
        // is the joint fit: CrewEquip.BakeIntoBodySpace refuses a piece whose joints miss the
        // body's by more than a centimetre, and so does this report.
        static bool ReportBindPoses(SkinnedMeshRenderer piece, SkinnedMeshRenderer body)
        {
            var pBind = piece.sharedMesh.bindposes;
            var bBind = body.sharedMesh.bindposes;
            var weighted = new System.Collections.Generic.HashSet<int>();
            foreach (var w in piece.sharedMesh.GetAllBoneWeights())
                if (w.weight > 0f) weighted.Add(w.boneIndex);

            var bodyIndex = new System.Collections.Generic.Dictionary<string, int>();
            for (int k = 0; k < body.bones.Length; k++)
                if (body.bones[k] != null && !bodyIndex.ContainsKey(body.bones[k].name)) bodyIndex.Add(body.bones[k].name, k);

            float refScale = -1f;
            int bad = 0;
            for (int i = 0; i < piece.bones.Length && i < pBind.Length; i++)
            {
                if (!weighted.Contains(i)) continue;
                string n = piece.bones[i] != null ? piece.bones[i].name : "<null>";
                if (!bodyIndex.TryGetValue(n, out int j) || j >= bBind.Length)
                {
                    Debug.Log("[Inspect] bind " + n + " is weighted but the body renderer has no such bone");
                    bad++;
                    continue;
                }
                var e = bBind[j].inverse * pBind[i];
                var s = e.lossyScale;
                float angle = Quaternion.Angle(e.rotation, Quaternion.identity);
                Vector3 move = e.GetColumn(3);
                if (refScale < 0f) refScale = s.x;
                bool off = angle > 2f || Mathf.Abs(s.x - refScale) > 0.02f || Mathf.Abs(s.y - refScale) > 0.02f
                           || Mathf.Abs(s.z - refScale) > 0.02f || move.magnitude > 0.01f;
                if (off) bad++;
                Debug.Log("[Inspect] bind " + n.PadRight(16) + (off ? " OFF " : " ok  ")
                          + " rotation " + angle.ToString("F1") + " deg, scale " + s.ToString("F3")
                          + ", offset " + move.ToString("F3"));
            }
            Debug.Log("[Inspect] bind poses: " + weighted.Count + " weighted bones, " + bad + " disagree with the body");
            return ReportJointFit(piece, pBind, bBind, bodyIndex);
        }

        // Bone frames can differ between two imports of one skeleton, joint positions cannot.
        // Runs the fit CrewEquip uses (CrewEquip.FitJoints) on the piece's and the body's joint
        // origins, both read in bind pose, and reports it the way the game will judge it.
        static bool ReportJointFit(SkinnedMeshRenderer piece, Matrix4x4[] pBind, Matrix4x4[] bBind,
                                   System.Collections.Generic.Dictionary<string, int> bodyIndex)
        {
            var src = new System.Collections.Generic.List<Vector3>();
            var dst = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < piece.bones.Length && i < pBind.Length; i++)
            {
                if (piece.bones[i] == null || !bodyIndex.TryGetValue(piece.bones[i].name, out int j) || j >= bBind.Length) continue;
                src.Add(pBind[i].inverse.GetColumn(3));
                dst.Add(bBind[j].inverse.GetColumn(3));
            }
            if (src.Count < 4)
            {
                Debug.Log("[Inspect] joint fit impossible, only " + src.Count + " bones shared");
                return false;
            }

            var a = CrewEquip.FitJoints(src, dst, out float rms);
            float worst = 0f;
            for (int k = 0; k < src.Count; k++) worst = Mathf.Max(worst, (a.MultiplyPoint3x4(src[k]) - dst[k]).magnitude);
            var col = new[] { (Vector3)a.GetColumn(0), (Vector3)a.GetColumn(1), (Vector3)a.GetColumn(2) };
            Debug.Log("[Inspect] joint fit over " + src.Count + " bones: rms " + (rms * 100f).ToString("F2")
                      + " cm, worst " + (worst * 100f).ToString("F2") + " cm, axis lengths "
                      + col[0].magnitude.ToString("F4") + " " + col[1].magnitude.ToString("F4") + " " + col[2].magnitude.ToString("F4")
                      + ", axis dots " + Vector3.Dot(col[0].normalized, col[1].normalized).ToString("F3") + " "
                      + Vector3.Dot(col[1].normalized, col[2].normalized).ToString("F3") + " "
                      + Vector3.Dot(col[0].normalized, col[2].normalized).ToString("F3"));
            return rms <= 0.01f;
        }

        // "-name value" from the command line, or null.
        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == name) return a[i + 1];
            return null;
        }

        static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
