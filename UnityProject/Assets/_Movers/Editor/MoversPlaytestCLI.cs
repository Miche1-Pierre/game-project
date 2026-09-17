using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Runs the game for real, in Play mode, with physics on, and captures what the player sees.
    //
    //   Unity.exe -batchmode -projectPath <path> \
    //             -executeMethod Movers.EditorTools.MoversPlaytestCLI.Run
    //
    // Do NOT pass -quit: the editor must stay alive long enough to play. This script exits
    // by itself when the run is over.
    //
    // PlayerController and PlayerGrab both read Input directly, and batch mode has no input,
    // so the player is driven from here: the controller is disabled and the CharacterController
    // is moved by hand, while the real grab code is invoked through reflection so the captured
    // behaviour is the game's, not a reimplementation of it.
    public static class MoversPlaytestCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Tutorial_01.unity";
        const string OutDir = "Assets/_Movers/Generated/playtest";

        static float t0;
        static CharacterController cc;
        static Transform player, cam;
        static PlayerGrab grab;
        static MethodInfo tryGrab;
        static int shotIndex;
        static string phase = "";
        static System.DateTime startedAt;
        static float lastTick;

        // EditorApplication.update fires only a couple of times per second in batch mode,
        // so Time.deltaTime (a game frame) moves the player almost nowhere. Step on the real
        // elapsed time between ticks instead, clamped so one long hitch cannot teleport him.
        static float RealDelta()
        {
            float now = Time.realtimeSinceStartup;
            float d = lastTick <= 0f ? 0.02f : Mathf.Clamp(now - lastTick, 0.005f, 0.25f);
            lastTick = now;
            return d;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(OutDir);

            // Entering Play mode normally triggers a domain reload, which wipes the static
            // state below and silently unsubscribes this callback. Turning that off is what
            // makes a scripted playtest possible at all.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            startedAt = System.DateTime.UtcNow;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            // Hard deadline. A batch-mode editor that never exits is worse than no capture.
            if ((System.DateTime.UtcNow - startedAt).TotalSeconds > 150)
            {
                Debug.Log("[Playtest] deadline reached, playing=" + EditorApplication.isPlaying +
                          " shots=" + shotIndex);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(shotIndex > 0 ? 0 : 2);
                return;
            }

            if (!EditorApplication.isPlaying) return;

            if (player == null)
            {
                var p = GameObject.Find("Player");
                if (p == null) return;
                player = p.transform;
                cc = p.GetComponent<CharacterController>();
                grab = p.GetComponent<PlayerGrab>();
                cam = grab != null ? grab.cam : null;

                var pc = p.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;          // no input in batch mode
                tryGrab = typeof(PlayerGrab).GetMethod("TryGrab",
                          BindingFlags.NonPublic | BindingFlags.Instance);

                t0 = Time.realtimeSinceStartup;
                Debug.Log("[Playtest] player found, driving it by hand");
                return;
            }

            float t = Time.realtimeSinceStartup - t0;
            var table = GameObject.Find("Table");
            var truck = GameObject.Find("Truck");

            // 0.0 - 1.5 s : settle, look around the far room
            if (t < 1.5f)
            {
                Aim(table != null ? table.transform.position : player.position + player.forward);
                Fall();
                Phase("settle");
                if (t > 1.2f && shotIndex == 0) Shot("01_spawn_looking_at_the_room.png");
                return;
            }

            // 1.5 - 4.0 s : walk up to the table
            if (t < 4.0f)
            {
                if (table != null)
                {
                    Aim(table.transform.position);
                    Walk(table.transform.position, 1.6f);
                }
                Phase("approach");
                if (t > 3.6f && shotIndex == 1) Shot("02_approaching_the_table.png");
                return;
            }

            // 4.0 s : grab it with the game's own code
            if (t < 4.2f)
            {
                if (tryGrab != null && grab != null && shotIndex == 2)
                {
                    tryGrab.Invoke(grab, null);
                    Debug.Log("[Playtest] TryGrab invoked");
                    Shot("03_grabbed.png");
                }
                Fall();
                return;
            }

            // 4.2 s onwards : carry it out along a real route.
            // Straight-line walking fails here, which is the point: the interior door is
            // narrow on purpose (the sofa puzzle). Waypoints take the player through it.
            if (t < 16.0f)
            {
                Vector3[] route = {
                    new Vector3(4.10f, 0f, 6.20f),   // line up with the narrow door
                    new Vector3(4.10f, 0f, 3.80f),   // through it
                    new Vector3(0.50f, 0f, 1.60f),   // across the near room
                    new Vector3(0.00f, 0f, -3.20f),  // out of the front opening
                    new Vector3(0.00f, 0f, -5.20f),  // at the truck
                };
                int i = Mathf.Clamp(Mathf.FloorToInt((t - 4.2f) / 2.3f), 0, route.Length - 1);
                Vector3 wp = route[i];
                Aim(wp + Vector3.up * 1.4f);
                Walk(wp, 0.5f);
                Phase("carry:wp" + i);

                if (t > 6.0f && shotIndex == 3) Shot("04_carrying_through_the_narrow_door.png");
                if (t > 11.0f && shotIndex == 4) Shot("05_crossing_the_near_room.png");
                if (t > 15.2f && shotIndex == 5) Shot("06_reaching_the_truck.png");
                return;
            }

            Debug.Log("[Playtest] done, " + shotIndex + " shots");
            EditorApplication.update -= Tick;
            EditorApplication.Exit(0);
        }

        static void Phase(string p)
        {
            if (p != phase) { phase = p; Debug.Log("[Playtest] phase: " + p); }
        }

        static void Aim(Vector3 target)
        {
            Vector3 flat = target - player.position; flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                player.rotation = Quaternion.Slerp(player.rotation,
                    Quaternion.LookRotation(flat.normalized, Vector3.up), 0.25f);
            if (cam != null)
            {
                Vector3 d = target - cam.position;
                float pitch = -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                cam.localRotation = Quaternion.Slerp(cam.localRotation,
                    Quaternion.Euler(pitch, 0f, 0f), 0.25f);
            }
        }

        static void Walk(Vector3 target, float stopAt)
        {
            Vector3 flat = target - player.position; flat.y = 0f;
            float dt = RealDelta();
            Vector3 step = flat.magnitude > stopAt ? flat.normalized * 3.4f : Vector3.zero;
            cc.Move((step + Vector3.down * 9.8f) * dt);
        }

        static void Fall() { cc.Move(Vector3.down * 9.8f * RealDelta()); }

        static void Shot(string file)
        {
            shotIndex++;
            var c = cam != null ? cam.GetComponent<Camera>() : Camera.main;
            if (c == null) { Debug.Log("[Playtest] no camera for " + file); return; }

            // Diagnostics: a grey frame usually means the camera is buried in a wall,
            // so record where we are and what is directly in front.
            string ahead = "nothing";
            if (Physics.Raycast(c.transform.position, c.transform.forward, out RaycastHit h, 6f))
                ahead = h.collider.name + " @" + h.distance.ToString("F2") + "m";
            Debug.Log("[Playtest] " + file + " player=" + player.position.ToString("F2") +
                      " ahead=" + ahead);

            Capture(c, Path.Combine(OutDir, file));

            // Same instant, seen from behind: the first-person view is often filled by the
            // carried object, and a third-person frame shows the situation instead.
            var tpGO = new GameObject("__thirdperson");
            var tp = tpGO.AddComponent<Camera>();
            tp.CopyFrom(c);
            Vector3 want = c.transform.position - c.transform.forward * 2.6f + Vector3.up * 1.3f;
            // Pull it in if a wall is in the way, otherwise the shot is taken from outside.
            Vector3 back = want - c.transform.position;
            if (Physics.Raycast(c.transform.position, back.normalized, out RaycastHit wall, back.magnitude + 0.3f))
                want = c.transform.position + back.normalized * Mathf.Max(0.6f, wall.distance - 0.3f);
            tp.transform.position = want;
            tp.transform.rotation = Quaternion.LookRotation(
                (c.transform.position + c.transform.forward * 2f - tp.transform.position).normalized, Vector3.up);
            Capture(tp, Path.Combine(OutDir, Path.GetFileNameWithoutExtension(file) + "_wide.png"));
            Object.DestroyImmediate(tpGO);
        }

        static void Capture(Camera c, string path)
        {
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prevTarget = c.targetTexture;
            c.targetTexture = rt;
            c.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            c.targetTexture = prevTarget;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

    }
}
