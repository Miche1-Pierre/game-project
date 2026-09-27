#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // One piece, worn in the real house, photographed. Play mode, never saves a scene.
    //
    // A garment is judged in the game and not in Blender: the render there has no scene light,
    // no crew colour on the trim and no animation. This puts the piece on the player's own crew
    // body in Map01_PierreKit_House through CrewEquip.Equip, the call PlayerEquip makes on F,
    // then shoots it from outside at 3 m and at 8 m (the 8 metre test in 05_ART/CHARACTERS.md),
    // and from the player's own eyes in grandmother's mirror.
    //
    //   Unity.exe -batchmode -screen-width 1920 -screen-height 1080 \
    //     -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversWearCLI.Run \
    //     -piece Assets/_Project/Art/Crew/SM_Crew_Chest_Bathrobe.fbx -slot Chest -logFile wear.log
    //
    // Do NOT pass -quit, it exits by itself: 0 when the piece went on with no CrewEquip warning
    // and no error, 2 otherwise. The images land in Assets/_Movers/Generated/review/, which is
    // gitignored. NOT covered: the F key itself. That is input, press it yourself.
    public static class MoversWearCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Map01_PierreKit_House.unity";
        const string ShotDir = "Assets/_Movers/Generated/review";

        static string piecePath;
        static EquipSlot slot;
        static int step;
        static float t0;
        static bool failed;
        static int warnings, errors;
        static System.DateTime startedAt;

        static PlayerController player;
        static CrewEquip body;
        static EquipItem item;

        static bool savedOptionsEnabled;
        static EnterPlayModeOptions savedOptions;
        static bool savedRunInBackground;
        static bool touchedRunInBackground;

        [MenuItem("The Movers/Wear Test (enters Play for ~10s)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Wear] already in Play mode, nothing done.");
                return;
            }
            // Opening the map would throw away unsaved work in whatever scene is open.
            if (!Application.isBatchMode && EditorSceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("[Wear] the open scene has unsaved changes, save or discard them first.");
                return;
            }

            piecePath = Arg("-piece") ?? "Assets/_Project/Art/Crew/SM_Crew_Chest_Bathrobe.fbx";
            var s = Arg("-slot");
            slot = string.IsNullOrEmpty(s) ? EquipSlot.Chest : (EquipSlot)System.Enum.Parse(typeof(EquipSlot), s);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            step = 0; t0 = 0f; failed = false; warnings = 0; errors = 0; touchedRunInBackground = false;
            player = null; body = null; item = null; crewAnimator = null;
            startedAt = System.DateTime.UtcNow;

            Application.logMessageReceived += Count;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if ((System.DateTime.UtcNow - startedAt).TotalSeconds > 120)
            {
                Check(false, "finished before the deadline, stuck at step " + step);
                Finish();
                return;
            }

            if (!EditorApplication.isPlaying) return;
            float t = t0 <= 0f ? 0f : Time.realtimeSinceStartup - t0;

            switch (step)
            {
                case 0: Setup(); break;
                case 1: if (t > 1.0f) Outside(); break;          // one second of idle, so the pose settles
                case 2: if (t > 0.2f) StartCarrying(); break;
                case 3: if (t > 0.8f) OutsideCarrying(); break;  // the blend is 0.15 s, then it settles
                case 4: if (t > 0.2f) ToMirror(); break;
                case 5: if (t > 1.5f) Mirror(); break;           // the controller lands on the floor
            }
        }

        static void Setup()
        {
            if (t0 <= 0f)
            {
                t0 = Time.realtimeSinceStartup;
                // An unfocused editor freezes the player loop while this callback keeps ticking.
                savedRunInBackground = Application.runInBackground;
                touchedRunInBackground = true;
                Application.runInBackground = true;
            }

            player = Object.FindAnyObjectByType<PlayerController>();
            var pe = player != null ? player.GetComponent<PlayerEquip>() : null;
            body = pe != null && pe.body != null ? pe.body : Object.FindAnyObjectByType<CrewEquip>();
            // Awake and Start have not necessarily run on the tick that first sees the scene.
            if ((player == null || body == null) && Time.realtimeSinceStartup - t0 < 3f) return;

            Check(player != null && body != null, "the map has a player with a crew body");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(piecePath);
            Check(asset != null, "the piece is imported at " + piecePath);
            if (player == null || body == null || asset == null) { Finish(); return; }

            var go = Object.Instantiate(asset, player.transform.position + player.transform.forward * 1.5f, Quaternion.identity);
            go.name = asset.name;
            item = go.AddComponent<EquipItem>();   // brings a MovableObject and a Rigidbody with it
            item.slot = slot;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

            int before = warnings;
            bool on = body.Equip(item);
            Check(on, "CrewEquip puts " + asset.name + " on the " + slot + " slot");
            Check(warnings == before, "with no CrewEquip warning");
            if (!on) { Finish(); return; }

            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null)
            {
                Check(skin.rootBone != null && skin.rootBone.IsChildOf(body.transform), "its bones are the body's now");
                var mats = skin.sharedMaterials;
                if (mats.Length > 1)
                    Check(mats[1] != null && mats[1].name.StartsWith("MAT_Crew"),
                          "the trim wears the crew colour (" + (mats[1] != null ? mats[1].name : "NULL") + ")");
            }

            Directory.CreateDirectory(ShotDir);
            t0 = Time.realtimeSinceStartup;
            step = 1;
        }

        // A worn piece bends, it does not tear. Bakes the skinned piece as it is posed right now
        // and compares every edge with its length in bind pose: cloth over a moving body
        // stretches a little, while a vertex bound to the wrong bone, or through the wrong bind
        // pose, pulls its edges out into a spike. Lists the vertices at the worst edges with the
        // bones they are weighted to, as Unity reads them.
        //
        // Call it after the piece has been drawn once. A skinned renderer is deformed when it is
        // drawn, and baked before its first draw it comes back in its bind pose: that is how the
        // robe's needles passed this check on 2026-09-25 while every image showed them. A
        // distance from the body's axis missed them too, because they stayed inside the radius.
        static void CheckStretch(float limit)
        {
            var skin = item.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null) return;
            var posed = new Mesh();
            skin.BakeMesh(posed, true);
            var p = posed.vertices;
            var src = skin.sharedMesh;
            var rest = src.vertices;
            var tris = src.triangles;

            var worstAt = new System.Collections.Generic.Dictionary<int, float>();
            float worst = 0f;
            int stretched = 0;
            for (int t = 0; t < tris.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = tris[t + e], b = tris[t + (e + 1) % 3];
                    float r = (rest[a] - rest[b]).magnitude;
                    if (r < 1e-4f) continue;
                    float s = (p[a] - p[b]).magnitude / r;
                    worst = Mathf.Max(worst, s);
                    if (s <= limit) continue;
                    stretched++;
                    foreach (int i in new[] { a, b })
                        if (!worstAt.TryGetValue(i, out float old) || s > old) worstAt[i] = s;
                }

            var sb = new System.Text.StringBuilder();
            if (stretched > 0)
            {
                var perVertex = src.GetBonesPerVertex();
                var weights = src.GetAllBoneWeights();
                var starts = new int[perVertex.Length];
                for (int i = 1; i < perVertex.Length; i++) starts[i] = starts[i - 1] + perVertex[i - 1];
                var order = new System.Collections.Generic.List<int>(worstAt.Keys);
                order.Sort((x, y) => worstAt[y].CompareTo(worstAt[x]));
                for (int n = 0; n < order.Count && n < 10; n++)
                {
                    int i = order[n];
                    sb.Append("\n  vertex ").Append(i).Append(" x").Append(worstAt[i].ToString("F1"))
                      .Append(" rest ").Append(rest[i].ToString("F3")).Append(" bones");
                    for (int k = 0; k < perVertex[i]; k++)
                    {
                        var bw = weights[starts[i] + k];
                        var bone = skin.bones[bw.boneIndex];
                        sb.Append(" ").Append(bone != null ? bone.name : "<null>").Append("=").Append(bw.weight.ToString("F2"));
                    }
                }
            }
            Object.DestroyImmediate(posed);
            Debug.Log("[Wear] worst edge stretch x" + worst.ToString("F2") + ", " + stretched + " edges over x" + limit + sb);
            Check(stretched == 0, "no edge of the worn piece stretches past x" + limit + " of its bind length");
        }

        // The body at rest, the way every other player sees a crew member with empty hands.
        static void Outside()
        {
            CheckCarryLayer(false, "with empty hands the Carry layer is off");
            var cam = Aim(out Vector3 centre, out Vector3 fwd);
            Shoot(cam, centre, fwd, 0f, 3f, "front_3m");
            CheckStretch(2.5f);   // after a draw, see CheckStretch
            Shoot(cam, centre, fwd, 45f, 3f, "three_quarter_3m");
            Shoot(cam, centre, fwd, 90f, 3f, "side_3m");
            Shoot(cam, centre, fwd, 180f, 3f, "back_3m");

            // The 8 metre test is about the game's own view, so it uses the player's field of view.
            var eye = player.cam != null ? player.cam.GetComponent<Camera>() : null;
            cam.fieldOfView = eye != null ? eye.fieldOfView : 60f;
            Shoot(cam, centre, fwd, 20f, 8f, "front_8m");

            Object.Destroy(cam.gameObject);
            t0 = Time.realtimeSinceStartup;
            step = 2;
        }

        // Forces the carry pose as if the player held something. The crew's animation contract
        // (AC_Crew_Slice, SLICE_ARCHITECTURE) plays it on a "Carry" layer whose weight
        // CrewAnimator, on the player root, sets from PlayerGrab every frame; it is switched off
        // for the shots so the forced weight stays.
        static void StartCarrying()
        {
            crewAnimator = player.GetComponent<CrewAnimator>();
            Check(crewAnimator != null, "the player has a CrewAnimator");
            if (crewAnimator != null) crewAnimator.enabled = false;
            int layer = CarryLayer();
            if (layer >= 0) body.animator.SetLayerWeight(layer, 1f);
            t0 = Time.realtimeSinceStartup;
            step = 3;
        }

        static void OutsideCarrying()
        {
            CheckCarryLayer(true, "while carrying the Carry layer plays " + CarryState);
            var cam = Aim(out Vector3 centre, out Vector3 fwd);
            Shoot(cam, centre, fwd, 0f, 3f, "front_3m_carrying");
            CheckStretch(2.5f);
            Shoot(cam, centre, fwd, 45f, 3f, "three_quarter_3m_carrying");
            Object.Destroy(cam.gameObject);

            if (crewAnimator != null) crewAnimator.enabled = true;
            t0 = Time.realtimeSinceStartup;
            step = 4;
        }

        const string CarryState = "Carry_Idle";
        static CrewAnimator crewAnimator;

        static int CarryLayer()
        {
            var a = body.animator;
            return a != null ? a.GetLayerIndex("Carry") : -1;
        }

        static void CheckCarryLayer(bool carrying, string what)
        {
            var a = body.animator;
            int layer = CarryLayer();
            bool ok = layer >= 0 && (carrying
                ? a.GetLayerWeight(layer) > 0.99f && a.GetCurrentAnimatorStateInfo(layer).IsName(CarryState)
                : a.GetLayerWeight(layer) < 0.01f);
            Check(ok, what);
        }

        // A camera for the outside shots, aimed at the middle of the worn piece, facing the body.
        static Camera Aim(out Vector3 centre, out Vector3 fwd)
        {
            var r = item.GetComponentInChildren<Renderer>();
            centre = r != null ? r.bounds.center : body.transform.position + Vector3.up * 1.1f;
            fwd = body.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            var cam = new GameObject("WearCam").AddComponent<Camera>();
            cam.fieldOfView = 40f;
            return cam;
        }

        static void Shoot(Camera cam, Vector3 centre, Vector3 fwd, float yaw, float dist, string name)
        {
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * fwd;
            cam.transform.position = centre + dir * dist + Vector3.up * 0.15f;
            cam.transform.LookAt(centre);
            if (Physics.Linecast(cam.transform.position, centre, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(player.transform))
                Debug.Log("[Wear] note: the " + name + " view is blocked by " + hit.transform.name);
            ShootCamera(cam, ShotDir + "/" + name + ".png", 1280, 1280);
        }

        static void ToMirror()
        {
            var mirror = Object.FindAnyObjectByType<MirrorSurface>();
            if (mirror == null)
            {
                Debug.Log("[Wear] no mirror in this map, the mirror view is skipped");
                Finish();
                return;
            }

            Vector3 n = mirror.flipNormal ? -mirror.transform.forward : mirror.transform.forward;
            n.y = 0f;
            n.Normalize();
            Vector3 stand = mirror.transform.position + n * (mirror.planeOffset + 1.3f);
            if (Physics.Raycast(stand + Vector3.up * 0.5f, Vector3.down, out var floor, 4f, ~0, QueryTriggerInteraction.Ignore))
                stand.y = floor.point.y;

            // A CharacterController overrides any position set while it is enabled.
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            float feet = cc != null ? cc.center.y - cc.height * 0.5f : 0f;
            player.transform.position = stand + Vector3.up * (0.02f - feet);
            player.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
            if (cc != null) cc.enabled = true;

            Debug.Log("[Wear] player stood 1.3 m in front of " + mirror.name);
            t0 = Time.realtimeSinceStartup;
            step = 5;
        }

        static void Mirror()
        {
            var eye = player.cam != null ? player.cam.GetComponent<Camera>() : Camera.main;
            ShootCamera(eye, ShotDir + "/mirror_first_person.png", 1920, 1080);
            Finish();
        }

        // ---- plumbing ----

        static void ShootCamera(Camera cam, string path, int w, int h)
        {
            if (cam == null) { Check(false, "a camera to shoot " + path); return; }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var saved = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = saved;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[Wear] wrote " + path);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }

        static void Count(string msg, string stack, LogType type)
        {
            if (type == LogType.Warning && msg.StartsWith("[CrewEquip]")) warnings++;
            if ((type == LogType.Error || type == LogType.Exception) && !msg.StartsWith("[Wear]")) errors++;
        }

        static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[Wear] OK    " + what);
            else { Debug.LogError("[Wear] FAIL  " + what); failed = true; }
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == name) return a[i + 1];
            return null;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Count;
            Check(errors == 0, "no error logged during the run (" + errors + ")");
            Debug.Log(failed ? "[Wear] VERDICT: failures above" : "[Wear] VERDICT: all checks passed");
            Debug.Log("[Wear] NOT covered here: the F key. That is input, press it yourself.");

            EditorApplication.ExitPlaymode();
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = savedOptions;
            if (touchedRunInBackground)
            {
                Application.runInBackground = savedRunInBackground;
                touchedRunInBackground = false;
            }
            AssetDatabase.Refresh();

            if (Application.isBatchMode) EditorApplication.Exit(failed ? 2 : 0);
        }
    }
}
#endif
