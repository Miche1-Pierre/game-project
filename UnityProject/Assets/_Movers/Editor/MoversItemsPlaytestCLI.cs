#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // The starting items, end to end, in Play mode: laid out, used, thrown, replaced.
    //
    // Replaces the two separate smoke and beer playtests. They tested viewmodels welded to the
    // camera, and there are none any more: both items are objects now (ADR-007), which means
    // one scene, one Play cycle and one set of checks covers them both.
    //
    //   Unity.exe -batchmode -screen-width 1920 -screen-height 1080 \
    //     -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversItemsPlaytestCLI.RunPlaytest -logFile items.log
    //
    // Do NOT pass -quit, it exits by itself. It drives the items through their own public API
    // rather than through a keyboard, so one thing is deliberately NOT covered: whether a tap
    // of the right button throws and a hold smokes. That is input timing, only a human can
    // press it, and it is the first thing to check by hand.
    public static class MoversItemsPlaytestCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Tutorial_01.unity";
        const string ShotDir = "Assets/_Movers/Generated/items";
        const float TestSoberSeconds = 5f;

        static float t0;
        static int step;
        static bool failed;
        static System.DateTime startedAt;

        static StartingItemSpawner cigSpawner, beerSpawner;
        static CigaretteItem cig;
        static BeerItem beer;
        static GameObject thrownCigarette;
        static GameObject brokenBottle;
        static PlayerController player;
        static PlayerGrab grab;
        static Drunkenness drunk;
        static float shippedSober;

        static bool savedOptionsEnabled;
        static EnterPlayModeOptions savedOptions;
        static bool savedRunInBackground;
        static bool touchedRunInBackground;

        [MenuItem("The Movers/Items Playtest (enters Play for ~15s)")]
        public static void RunPlaytest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Items] already in Play mode, nothing done.");
                return;
            }

            if (Object.FindFirstObjectByType<PlayerController>() == null)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            t0 = 0f; step = 0; failed = false; touchedRunInBackground = false;
            cigSpawner = null; beerSpawner = null; cig = null; beer = null; thrownCigarette = null; brokenBottle = null;
            startedAt = System.DateTime.UtcNow;

            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if ((System.DateTime.UtcNow - startedAt).TotalSeconds > 90)
            {
                Debug.LogError("[Items] deadline reached at step " + step);
                failed = true;
                Finish();
                return;
            }

            if (!EditorApplication.isPlaying) return;
            float t = step == 0 ? 0f : Time.realtimeSinceStartup - t0;

            switch (step)
            {
                case 0: Setup(); break;
                case 1: if (t > 0.5f) LightIt(); break;
                case 2: Smoking(t); break;
                case 3: if (t > 2.0f) ThrowIt(); break;
                case 4: if (t > 3.0f) CheckRespawn(); break;
                case 5: if (t > 3.4f) DrinkIt(); break;
                case 6: if (t > 3.8f) BreakIt(); break;
                case 7: if (t > 4.6f) CheckBeerRespawn(); break;
                case 8: if (t > 11.0f) LastLook(); break;
            }
        }

        static void Setup()
        {
            player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) return;

            // Set once, not on every retry below, or the wait never expires.
            if (t0 <= 0f)
            {
                t0 = Time.realtimeSinceStartup;
                // An unfocused editor freezes the player loop while this callback keeps
                // ticking, which reports a working feature as a dead one.
                savedRunInBackground = Application.runInBackground;
                touchedRunInBackground = true;
                Application.runInBackground = true;
            }

            foreach (var s in Object.FindObjectsByType<StartingItemSpawner>(FindObjectsSortMode.None))
            {
                if (s.kind == StartingItemSpawner.Kind.Cigarette) cigSpawner = s;
                else beerSpawner = s;
            }

            cig = cigSpawner != null ? cigSpawner.Current as CigaretteItem : null;
            beer = beerSpawner != null ? beerSpawner.Current as BeerItem : null;

            // The items are laid out in Start, which has not necessarily run on the tick that
            // first sees the player. Wait for them rather than call them missing.
            bool ready = cig != null && beer != null;
            if (!ready && Time.realtimeSinceStartup - t0 < 3f) return;

            grab = player.GetComponent<PlayerGrab>();
            drunk = player.GetComponent<Drunkenness>();
            Check(grab != null, "the player can grab things");
            Check(drunk != null, "the player has a head that beer can turn");
            Check(cigSpawner != null, "there is a cigarette spot by the truck");
            Check(beerSpawner != null, "there is a beer spot by the truck");
            Check(cig != null, "a cigarette was laid out at Play");
            Check(beer != null, "a beer was laid out with it");
            if (!ready || grab == null || drunk == null) { Finish(); return; }

            Check(cig.GetComponent<MovableObject>() != null, "the cigarette is an ordinary movable object");
            Check(cig.GetComponent<Collider>() != null, "and it has something you can actually click");
            Check(beer.GetComponent<MovableObject>() != null, "the beer is one too");
            Check(cig.UsesHoldButton, "the cigarette answers to a held right button");
            Check(beer.UsesHoldButton == false, "the beer leaves the right button alone, it has its own key");

            shippedSober = drunk.soberSeconds;
            drunk.soberSeconds = TestSoberSeconds;
            Debug.Log("[Items] sobering clock shortened to " + TestSoberSeconds
                      + " s for this run, the shipped value is " + shippedSober + " s");

            // The clock the steps below measure against starts now, not when the scene loaded.
            t0 = Time.realtimeSinceStartup;
            step = 1;
        }

        static void LightIt()
        {
            cig.OnPickedUp(grab);
            cig.OnUseBegin();
            Check(cig.IsHeld, "the cigarette knows who is holding it");
            Check(cig.IsSmoking, "and it lights when you hold the button");
            step = 2;
        }

        static void Smoking(float t)
        {
            cig.OnUseHold(Time.deltaTime);
            if (t < 2.0f) return;

            cig.OnUseEnd();
            Debug.Log("[Items] after 1.5 s of smoking: " + SmokeCloud.Active.Count + " puff(s) alive");
            Check(SmokeCloud.Active.Count >= 2, "holding the button lays down a puff every half second");
            if (SmokeCloud.Active.Count > 0)
            {
                Vector3 c = SmokeCloud.Active[0].transform.position;
                Check(SmokeCloud.DensityAtPoint(c) > 0.5f, "and the puffs are thick where they land");
                Check(Vector3.Distance(c, cig.transform.position) < 2.5f,
                      "the smoke comes off the cigarette, not off the player");
            }
            Check(!cig.IsSmoking, "it stops when you let go");
            Directory.CreateDirectory(ShotDir);
            ShootCamera(player.cam.GetComponent<Camera>(), ShotDir + "/items_camera.png", 1920, 1080);
            step = 3;
        }

        static void ThrowIt()
        {
            thrownCigarette = cig.gameObject;
            cig.OnReleased(true);
            Check(!cig.IsHeld, "letting go releases it");
            Check(cigSpawner.Current == null, "throwing it away frees the spot by the truck");
            Check(thrownCigarette != null, "the thrown one is still lying where it landed");
            step = 4;
        }

        static void CheckRespawn()
        {
            var fresh = cigSpawner.Current;
            Check(fresh != null, "a new cigarette turns up at the van");
            Check(fresh == null || fresh.gameObject != thrownCigarette, "and it is a new one, not the old one moved back");
            Check(thrownCigarette != null, "the thrown one is still in the world, as litter");
            step = 5;
        }

        static void DrinkIt()
        {
            beer.OnPickedUp(grab);
            float taken = beer.Drink(beer.drinkSeconds);
            Debug.Log("[Items] drank " + taken.ToString("F2") + ", fill " + beer.fill.ToString("F2")
                      + ", drunk " + drunk.Amount.ToString("F2"));
            Check(beer.IsEmpty, "a full drink empties the bottle");
            Check(drunk.Amount > 0.95f, "and leaves the player as drunk as this gets");
            Check(beer.Drink(1f) <= 0f, "an empty bottle gives nothing back");
            step = 6;
        }

        static void BreakIt()
        {
            Check(player.lookSway.magnitude > 0.5f, "the drunk view wanders");
            Check(grab.carrySlop > 0.1f, "and the grip has gone loose");

            brokenBottle = beer.gameObject;
            beer.OnReleased(true);
            beer.Break(beer.transform.position, Vector3.up);
            step = 7;
        }

        static void CheckBeerRespawn()
        {
            // Checked a beat later than the break: Destroy only takes effect at the end of the
            // frame, so asking straight away would always answer "still there".
            Check(brokenBottle == null, "the bottle is gone once it breaks");
            Check(GameObject.Find("Beer_Splat") != null, "and it leaves one flat mark on the floor");
            Check(beerSpawner.Current != null, "a new beer turns up at the van");
            step = 8;
        }

        static void LastLook()
        {
            Debug.Log("[Items] at the end: " + SmokeCloud.Active.Count + " puff(s), drunk "
                      + drunk.Amount.ToString("F3") + ", sway " + player.lookSway.ToString("F2"));
            Check(SmokeCloud.Active.Count == 0, "every puff has run out its 7 seconds");
            Check(GameObject.Find("SmokeCloud") == null, "and no cloud object is left behind");
            Check(drunk.Amount <= 0.001f, "the player sobers up on the clock");
            Check(player.lookSway == Vector3.zero, "and is left with no sway at all");
            Check(player.moveDrift == 0f, "and walks straight again");

            drunk.soberSeconds = shippedSober;
            Finish();
        }

        // ---- plumbing ----

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
            Debug.Log("[Items] wrote " + path);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }

        static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[Items] OK    " + what);
            else { Debug.LogError("[Items] FAIL  " + what); failed = true; }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Debug.Log(failed ? "[Items] VERDICT: failures above" : "[Items] VERDICT: all checks passed");
            Debug.Log("[Items] NOT covered here: tap to throw versus hold to smoke. That is input timing, press it yourself.");

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
