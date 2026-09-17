#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Drinks the beer without a keyboard, and checks what it does to the player.
    //
    // The sibling of MoversSmokePlaytestCLI, and the same shape: none of this exists outside
    // Play mode, because the bottle is built in Awake and drunkenness is a clock.
    //
    //   Unity.exe -batchmode -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversBeerPlaytestCLI.RunPlaytest -logFile beer.log
    //
    // Do NOT pass -quit: the editor has to stay alive long enough to play. It shortens the
    // sobering clock so the run takes ten seconds instead of half a minute, and says so; the
    // mechanism under test is the same, only the constant differs.
    public static class MoversBeerPlaytestCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Tutorial_01.unity";
        const string ShotDir = "Assets/_Movers/Generated/beer";
        const string ShotPath = ShotDir + "/beer_gameview.png";
        const float TestSoberSeconds = 5f;

        static float t0;
        static int step;
        static bool failed;
        static float shippedSoberSeconds;
        static Vector3 swaySample;
        static System.DateTime startedAt;

        static bool savedOptionsEnabled;
        static EnterPlayModeOptions savedOptions;
        static bool savedRunInBackground;
        static bool touchedRunInBackground;

        [MenuItem("The Movers/Beer Playtest (enters Play for ~12s)")]
        public static void RunPlaytest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[BeerPlay] already in Play mode, nothing done.");
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
            step = 0;
            failed = false;
            touchedRunInBackground = false;
            startedAt = System.DateTime.UtcNow;

            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if ((System.DateTime.UtcNow - startedAt).TotalSeconds > 60)
            {
                Debug.LogError("[BeerPlay] deadline reached at step " + step);
                failed = true;
                Finish();
                return;
            }

            if (!EditorApplication.isPlaying) return;

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            var beer = player.GetComponent<PlayerBeer>();
            var drunk = player.GetComponent<Drunkenness>();
            var grab = player.GetComponent<PlayerGrab>();

            if (step == 0)
            {
                t0 = Time.realtimeSinceStartup;

                // An unfocused editor freezes the player loop while this callback keeps
                // ticking, which would report a working feature as a dead one.
                savedRunInBackground = Application.runInBackground;
                touchedRunInBackground = true;
                Application.runInBackground = true;

                Check(beer != null, "PlayerBeer is on the player");
                Check(drunk != null, "Drunkenness is on the player");
                if (beer == null || drunk == null) { Finish(); return; }

                var view = player.cam != null ? player.cam.Find("BeerView") : null;
                Check(view != null, "the bottle exists in the hand");
                if (view != null)
                {
                    Check(view.Find("Beer_Body") != null, "it has a body");
                    Check(view.Find("Beer_Neck") != null, "it has a neck");
                    foreach (var col in view.GetComponentsInChildren<Collider>())
                        Check(false, "the viewmodel must carry no collider, found one on " + col.name);
                }

                Check(Mathf.Approximately(beer.fill, 1f), "the bottle starts full");
                Check(drunk.Amount <= 0.001f, "the player starts sober");
                Check(player.lookSway == Vector3.zero && player.moveDrift == 0f,
                      "a sober player is not swayed at all");

                shippedSoberSeconds = drunk.soberSeconds;
                drunk.soberSeconds = TestSoberSeconds;
                Debug.Log("[BeerPlay] sobering clock shortened to " + TestSoberSeconds
                          + " s for this run, the shipped value is " + shippedSoberSeconds + " s");
                step = 1;
                return;
            }

            float t = Time.realtimeSinceStartup - t0;

            // Drink the whole bottle through the game's own method, not by setting fields.
            if (step == 1 && t > 0.5f)
            {
                float taken = beer.Drink(beer.drinkSeconds);
                Debug.Log("[BeerPlay] drank " + taken.ToString("F2") + " of the bottle, fill now "
                          + beer.fill.ToString("F2") + ", drunk " + drunk.Amount.ToString("F2"));
                Check(beer.IsEmpty, "a full drink empties the bottle");
                Check(drunk.Amount > 0.95f, "and leaves the player as drunk as this gets");
                Check(beer.Drink(1f) <= 0f, "an empty bottle gives nothing back");
                step = 2;
                return;
            }

            if (step == 2 && t > 1.2f)
            {
                Debug.Log("[BeerPlay] sway " + player.lookSway.ToString("F2")
                          + " drift " + player.moveDrift.ToString("F1")
                          + " grip slop " + (grab != null ? grab.carrySlop.ToString("F2") : "n/a"));
                Check(player.lookSway.magnitude > 0.5f, "the view has started to wander");
                Check(Mathf.Abs(player.moveDrift) > 0.5f, "and you no longer walk where you point");
                Check(grab == null || grab.carrySlop > 0.1f, "the grip has gone loose");
                swaySample = player.lookSway;
                Directory.CreateDirectory(ShotDir);   // neither of these will make it for us

                // Two pictures, because neither one is enough on its own. ScreenCapture takes
                // the Game view, HUD and all, and silently does nothing in batch mode where
                // there is no Game view. A camera render has no HUD but works headless, and it
                // is the one that answers the question the first version got wrong: is the
                // bottle actually inside the frame at a real aspect ratio.
                ScreenCapture.CaptureScreenshot(ShotPath);
                ShootCamera(player.cam.GetComponent<Camera>(), ShotDir + "/beer_camera.png", 1920, 1080);
                step = 3;
                return;
            }

            // The wander has to keep moving. A constant offset would read as a broken camera.
            if (step == 3 && t > 2.2f)
            {
                Check((player.lookSway - swaySample).magnitude > 0.2f,
                      "the wander keeps moving instead of sitting at an offset");
                step = 4;
                return;
            }

            if (step == 4 && t > TestSoberSeconds + 1.5f)
            {
                Debug.Log("[BeerPlay] after the clock: drunk " + drunk.Amount.ToString("F3")
                          + " sway " + player.lookSway.ToString("F2")
                          + " drift " + player.moveDrift.ToString("F2"));
                Check(drunk.Amount <= 0.001f, "the player sobers up on the clock");
                Check(player.lookSway == Vector3.zero, "and is left with no sway at all");
                Check(player.moveDrift == 0f, "and walks straight again");
                Check(grab == null || grab.carrySlop <= 0.001f, "and has their grip back");
                Check(beer.IsEmpty, "the bottle stays empty, it does not refill");

                drunk.soberSeconds = shippedSoberSeconds;
                Finish();
            }
        }

        static void ShootCamera(Camera cam, string path, int w, int h)
        {
            if (cam == null) return;
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
            Debug.Log("[BeerPlay] wrote " + path);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }

        static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[BeerPlay] OK    " + what);
            else { Debug.LogError("[BeerPlay] FAIL  " + what); failed = true; }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Debug.Log(failed ? "[BeerPlay] VERDICT: failures above" : "[BeerPlay] VERDICT: all checks passed");

            EditorApplication.ExitPlaymode();
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = savedOptions;
            if (touchedRunInBackground)
            {
                Application.runInBackground = savedRunInBackground;
                touchedRunInBackground = false;
            }
            AssetDatabase.Refresh();

            if (Application.isBatchMode)
                EditorApplication.Exit(failed ? 2 : 0);
        }
    }
}
#endif
