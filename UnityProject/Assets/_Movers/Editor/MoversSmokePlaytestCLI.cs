#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // The half of the cigarette that only exists while the game is running.
    //
    // MoversSmokeCLI can measure the maths and paint the overlay without Play mode, but two
    // things only happen for real when Update ticks: PlayerCigarette builds the thing in your
    // hand in Awake, and a puff deletes itself 7 seconds later. This enters Play mode, watches
    // one puff live and die on the wall clock, and leaves again.
    //
    //   Unity.exe -batchmode -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversSmokePlaytestCLI.RunPlaytest -logFile smoke.log
    //
    // Do NOT pass -quit: the editor has to stay alive long enough to play. In batch mode it
    // exits by itself; in a human editor it just leaves Play mode and gives the session back.
    public static class MoversSmokePlaytestCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Tutorial_01.unity";
        const string ShotPath = "Assets/_Movers/Generated/smoke/smoke_gameview.png";

        static float t0;
        static float spawnedAt;
        static int step;
        static bool failed;
        static System.DateTime startedAt;

        // Entering Play mode normally reloads the domain, which wipes the statics below and
        // silently unsubscribes the callback, so the test would never run. Restored on the way
        // out: this is a project setting that lives in git, not ours to change permanently.
        static bool savedOptionsEnabled;
        static EnterPlayModeOptions savedOptions;
        static bool savedRunInBackground;
        static bool touchedRunInBackground;

        [MenuItem("The Movers/Smoke Playtest (enters Play for ~10s)")]
        public static void RunPlaytest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SmokePlay] already in Play mode, nothing done.");
                return;
            }

            if (Object.FindFirstObjectByType<PlayerController>() == null)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            t0 = 0f;
            spawnedAt = 0f;
            touchedRunInBackground = false;
            step = 0;
            failed = false;
            startedAt = System.DateTime.UtcNow;

            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            // Hard deadline. An editor stuck in Play mode because a check never fired is a
            // worse outcome than a test that reports nothing.
            if ((System.DateTime.UtcNow - startedAt).TotalSeconds > 60)
            {
                Debug.LogError("[SmokePlay] deadline reached at step " + step);
                failed = true;
                Finish();
                return;
            }

            if (!EditorApplication.isPlaying) return;

            if (step == 0)
            {
                var player = Object.FindFirstObjectByType<PlayerController>();
                if (player == null) return;
                t0 = Time.realtimeSinceStartup;

                // Without this, an editor that is not the focused window freezes the player
                // loop: no Update, so no puff ever ages and no screen ever fills, while this
                // callback keeps ticking on the wall clock and reports a feature that works as
                // one that does nothing. It cost one confusing run to find. Restored on the way
                // out, because it is a project setting.
                savedRunInBackground = Application.runInBackground;
                touchedRunInBackground = true;
                Application.runInBackground = true;

                var cig = player.GetComponent<PlayerCigarette>();
                var eyes = player.cam;
                var vision = eyes != null ? eyes.GetComponent<SmokeVision>() : null;

                Check(cig != null, "PlayerCigarette is on the player");
                Check(vision != null, "SmokeVision is on the camera");

                // What Awake built: the thing in your hand, with an ember and a wisp.
                var view = eyes != null ? eyes.Find("CigaretteView") : null;
                Check(view != null, "the cigarette view exists in the hand");
                if (view != null)
                {
                    Check(view.gameObject.activeSelf, "the cigarette is out (hands are empty)");
                    Check(view.Find("Cigarette_Body") != null, "it has a body");
                    Check(view.Find("Cigarette_Ember") != null, "it has an ember");
                    Check(view.Find("Cigarette_Wisp") != null, "it smoulders");
                    foreach (var col in view.GetComponentsInChildren<Collider>())
                        Check(false, "the viewmodel must carry no collider, found one on " + col.name);
                }

                step = 1;
                return;
            }

            float t = Time.realtimeSinceStartup - t0;

            // 1 s in: light one, at the eyes, exactly where a drag puts it.
            if (step == 1 && t > 1f)
            {
                var player = Object.FindFirstObjectByType<PlayerController>();
                var eyes = player.cam;
                SmokeCloud.Spawn(eyes.position + eyes.forward * 0.55f, eyes.forward);
                spawnedAt = Time.realtimeSinceStartup;
                Debug.Log("[SmokePlay] puff lit, clouds alive: " + SmokeCloud.Active.Count);
                Check(SmokeCloud.Active.Count == 1, "the puff registered itself");
                step = 2;
                return;
            }

            if (step < 2) return;
            float age = Time.realtimeSinceStartup - spawnedAt;
            var vis = Object.FindFirstObjectByType<SmokeVision>();
            if (vis == null) { Check(false, "SmokeVision is alive to read"); Finish(); return; }

            if (step == 2 && age > 1.2f)
            {
                Debug.Log("[SmokePlay] t+1.2s  screen " + vis.Density.ToString("F2"));
                // Measured, and lower than the static maths predicts on purpose. A puff rises
                // at 0.35 m/s and is pushed away from the mouth as it is exhaled, so it leaves
                // your own face while it grows: one puff peaks near 0.7, not 1.0. Holding the
                // button is what blinds you outright, because the puffs stack (0.99 measured
                // with six of them alive). Raise these and you are testing a cloud that stands
                // still, which is not the one that ships.
                Check(vis.Density > 0.5f, "one puff takes most of the screen by 1.2 s");
                // The one thing no offline render can show: the overlay and the HUD in the same
                // frame. The smoke has to take the view and leave the contract checklist alone.
                ScreenCapture.CaptureScreenshot(ShotPath);
                Debug.Log("[SmokePlay] game view capture requested: " + ShotPath);
                step = 3;
                return;
            }

            if (step == 3 && age > 4f)
            {
                Debug.Log("[SmokePlay] t+4.0s  screen " + vis.Density.ToString("F2")
                          + ", clouds " + SmokeCloud.Active.Count);
                Check(vis.Density > 0.3f, "it is still in the way at 4 s, thinner as it drifts up");
                step = 4;
                return;
            }

            if (step == 4 && age > 6.9f)
            {
                Debug.Log("[SmokePlay] t+6.9s  screen " + vis.Density.ToString("F2")
                          + ", clouds " + SmokeCloud.Active.Count);
                Check(vis.Density < 0.15f, "it has thinned out just before 7 s");
                step = 5;
                return;
            }

            if (step == 5 && age > 7.6f)
            {
                Debug.Log("[SmokePlay] t+7.6s  screen " + vis.Density.ToString("F2")
                          + ", clouds " + SmokeCloud.Active.Count);
                Check(SmokeCloud.Active.Count == 0, "the puff deleted itself after 7 s");
                Check(vis.Density <= 0.001f, "the screen is clear again");
                Check(GameObject.Find("SmokeCloud") == null, "no cloud object left in the scene");
                Finish();
            }
        }

        static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[SmokePlay] OK    " + what);
            else { Debug.LogError("[SmokePlay] FAIL  " + what); failed = true; }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Debug.Log(failed ? "[SmokePlay] VERDICT: failures above" : "[SmokePlay] VERDICT: all checks passed");

            EditorApplication.ExitPlaymode();
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = savedOptions;
            if (touchedRunInBackground)
            {
                Application.runInBackground = savedRunInBackground;
                touchedRunInBackground = false;
            }
            AssetDatabase.Refresh();   // so the game view capture shows up in the project

            if (Application.isBatchMode)
                EditorApplication.Exit(failed ? 2 : 0);
        }
    }
}
#endif
