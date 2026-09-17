#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Looks at the cigarette smoke without playing the game.
    //
    // Two entry points, because the feature makes two promises and they fail differently.
    // RunProbe answers "is a puff gone after exactly 7 seconds, and does it blind you while it
    // lasts", in numbers, with no graphics device needed. RunPreview answers "what does it
    // look like", by rendering the player camera with the puffs in front of it and painting the
    // real overlay on top, the same DrawSmoke the game calls.
    //
    //   Unity.exe -batchmode -quit -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversSmokeCLI.RunProbe -logFile smoke.log
    //
    // RunPreview needs a graphics device, so it must not be given -nographics.
    //
    // Neither one saves the scene. The puffs they spawn are temporary and removed on the way
    // out, so the scene on disk never learns that any of this happened.
    public static class MoversSmokeCLI
    {
        const string ScenePath = "Assets/_Movers/Scenes/Tutorial_01.unity";
        const string OutDir = "Assets/_Movers/Generated/smoke";
        const string TempPrefix = "TEMP_SmokePreview";

        // A MonoBehaviour gets no OnEnable outside Play mode, so a puff spawned here never
        // announces itself and SmokeCloud.Active stays empty, which would make every reading
        // below a confident zero. The register is public, so the tool keeps it honest itself.
        // Nothing in the game needs this: in Play, OnEnable does the job.
        static SmokeCloud Puff(Vector3 at, Vector3 dir, string name, float age)
        {
            var p = SmokeCloud.Spawn(at, dir);
            p.gameObject.name = name;
            p.Age = age;
            if (!SmokeCloud.Active.Contains(p)) SmokeCloud.Active.Add(p);
            return p;
        }

        // Fixed, so two runs a week apart produce comparable images instead of two random
        // frames of a scrolling texture.
        const float PreviewTime = 12.3f;

        [MenuItem("The Movers/Smoke Probe (numbers)")]
        public static void RunProbe()
        {
            var cam = OpenAndFindEyes();
            if (cam == null) return;

            var puff = Puff(cam.position + cam.forward * 0.55f, cam.forward, TempPrefix + "_probe", 0f);

            Debug.Log("[Smoke] one puff over its life, seen from the smoker's own eyes 0.55 m away");
            Debug.Log("[Smoke]   t(s)   eyes   centre   radius(m)");
            foreach (float t in new[] { 0f, 0.25f, 0.5f, 1f, 2f, 3f, 4f, 5f, 6f, 6.5f, 6.9f, 7f })
            {
                puff.Age = t;
                Debug.Log(string.Format("[Smoke]   {0,5}  {1,5}   {2,5}   {3,5}",
                    t.ToString("F2"),
                    SmokeCloud.DensityAtPoint(cam.position).ToString("F3"),
                    puff.DensityAt(puff.transform.position).ToString("F3"),
                    puff.Radius.ToString("F2")));
            }

            puff.Age = 3f;
            Vector3 c = puff.transform.position;
            Debug.Log("[Smoke] falloff at t=3 s, by distance from the centre of the puff");
            foreach (float d in new[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 4f })
                Debug.Log(string.Format("[Smoke]   {0,4} m  {1}",
                    d.ToString("F1"), puff.DensityAt(c + Vector3.right * d).ToString("F3")));

            // What holding the button actually does: puffs laid down every 0.5 s, all of them
            // still alive, stacking on the same head.
            Cleanup();
            for (int i = 0; i < 3; i++)
                Puff(cam.position + cam.forward * (0.55f + i * 0.25f), cam.forward,
                     TempPrefix + "_stack" + i, 1.4f - i * 0.45f);
            Debug.Log("[Smoke] three overlapping puffs, density at the eyes: "
                      + SmokeCloud.DensityAtPoint(cam.position).ToString("F3")
                      + " (clouds alive: " + SmokeCloud.Active.Count + ")");

            Cleanup();
            Debug.Log("[Smoke] probe done");
        }

        [MenuItem("The Movers/Smoke Preview (images)")]
        public static void RunPreview()
        {
            var eyes = OpenAndFindEyes();
            if (eyes == null) return;
            var cam = eyes.GetComponent<Camera>();
            var vision = eyes.GetComponent<SmokeVision>();
            if (vision == null) vision = eyes.gameObject.AddComponent<SmokeVision>();

            Directory.CreateDirectory(OutDir);

            // The texture the overlay is made of, so a flat-looking result can be blamed on the
            // right thing: a bad noise map or a bad composite, never "somewhere in there".
            File.WriteAllBytes(Path.Combine(OutDir, "smoke_noise_source.png"),
                               SmokeTextures.Noise.EncodeToPNG());

            // A hold of about a second and a half, laid down a few metres ahead instead of on
            // the lens. Standing inside the particles proves nothing about the overlay: the
            // frame is already white. Put the cloud in the room, keep the background fixed,
            // and the three shots below differ by the overlay alone.
            var ages = new[] { 2.4f, 1.9f, 1.4f, 0.9f };
            for (int i = 0; i < ages.Length; i++)
            {
                Vector3 at = eyes.position + eyes.forward * (3.4f + i * 0.45f)
                             + Vector3.up * (0.1f * i) + eyes.right * ((i % 2 == 0) ? 0.25f : -0.25f);
                var p = Puff(at, eyes.forward, TempPrefix + "_" + i, ages[i]);
                p.GetComponent<ParticleSystem>().Simulate(ages[i], false, true);
            }

            Debug.Log("[Smoke] preview: " + SmokeCloud.Active.Count + " puffs ahead of the player, "
                      + "density at the eyes " + SmokeCloud.DensityAtPoint(eyes.position).ToString("F3")
                      + ", at the cloud " + SmokeCloud.DensityAtPoint(
                          eyes.position + eyes.forward * 3.4f).ToString("F3"));

            Shot(cam, vision, 0f, "smoke_world.png", 1280, 720);    // what the others see
            Shot(cam, vision, 0.35f, "smoke_edge.png", 1280, 720);  // the edge of the cloud
            Shot(cam, vision, 1f, "smoke_blind.png", 1280, 720);    // standing in the heart of it

            Cleanup();
            AssetDatabase.Refresh();
            Debug.Log("[Smoke] preview done, images in " + OutDir);
        }

        // ---- helpers ----

        static Transform OpenAndFindEyes()
        {
            if (Object.FindFirstObjectByType<PlayerController>() == null)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("[Smoke] no player in the scene, nothing to look through."); return null; }

            Transform cam = pc.cam;
            if (cam == null)
            {
                var c = pc.GetComponentInChildren<Camera>();
                if (c != null) cam = c.transform;
            }
            if (cam == null) Debug.LogError("[Smoke] the player has no camera.");
            return cam;
        }

        // Renders what that camera sees, then paints the overlay on top at the given strength.
        // The overlay call is the game's own DrawSmoke, so this cannot drift from what plays.
        static void Shot(Camera cam, SmokeVision vision, float density, string file, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var savedTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = savedTarget;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            if (density > 0f)
            {
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, w, h, 0f);   // GUI convention: origin top left
                vision.DrawSmoke(new Rect(0f, 0f, w, h), density, PreviewTime);
                GL.PopMatrix();
            }

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            string path = Path.Combine(OutDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[Smoke] wrote " + path);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }

        static void Cleanup()
        {
            foreach (var c in Object.FindObjectsByType<SmokeCloud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null && c.gameObject.name.StartsWith(TempPrefix))
                {
                    SmokeCloud.Active.Remove(c);   // no OnDisable outside Play either
                    Object.DestroyImmediate(c.gameObject);
                }
        }
    }
}
#endif
