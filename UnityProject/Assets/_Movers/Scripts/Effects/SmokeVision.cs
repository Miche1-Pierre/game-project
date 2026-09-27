using UnityEngine;

namespace Movers
{
    // What being inside the smoke looks like, from one pair of eyes.
    //
    // This sits on a camera, not on the world, and that is the whole point: the cloud is
    // shared, the blindness is not. Whoever walks into the puff loses their view, including
    // the player who lit the cigarette. When there are four players, four cameras each carry
    // one of these and each one answers for its own head position, with no extra code, and
    // each paints only its own camera's part of the screen: P1 in the smoke does not blind
    // P2's half of a split screen.
    //
    // Drawn with OnGUI, like GameHUD, for the same reason: zero package dependencies, no
    // Canvas, no post-processing stack. Built-in RP fog would have been the other option and
    // it is global, so it would have fogged every player at once. GUI.depth keeps the smoke
    // behind the HUD text: losing your view is the game, losing the contract checklist is
    // just a bug report.
    public class SmokeVision : MonoBehaviour
    {
        [Header("Sampling")]
        // Where the eyes are. Left empty, this transform is used, which is correct when the
        // component sits on the camera.
        public Transform eyes;

        [Header("Feel")]
        // How hard the effect hits at full density. 1 leaves about a fifth of the image coming
        // through, enough to keep walking and to bump into a doorframe, which is the funny
        // failure. Turn it down, not off, if a playtest says it is cruel.
        [Range(0f, 1f)] public float strength = 1f;
        public float thickenSpeed = 3.0f;   // how fast the screen fills when you walk in
        public float clearSpeed = 2.2f;     // and how fast it gives your eyes back
        // Below this the overlay is skipped entirely, so a clean frame costs one distance test.
        public float cutoff = 0.004f;

        [Header("Look")]
        // Mid grey, not white. A white veil over the greybox, whose walls are already almost
        // white, turns the screen into a blank page: no silhouettes, no edges, nothing to steer
        // by, and it reads as a broken renderer rather than as smoke. Grey takes the contrast
        // away and leaves the shapes, which is what being in smoke actually does.
        public Color haze = new Color(0.62f, 0.62f, 0.66f);
        public Color wisps = new Color(0.74f, 0.74f, 0.78f);
        public Color edge = new Color(0.20f, 0.20f, 0.24f);
        public float tilingNear = 2.1f;
        public float tilingFar = 3.9f;
        public float scrollSpeed = 0.014f;
        // Higher draws further back. GameHUD leaves itself at 0, so anything above that ends
        // up under the text.
        public int guiDepth = 5;

        float shown;
        Camera view;

        public float Density => shown;

        Transform Eyes => eyes != null ? eyes : transform;

        void Awake()
        {
            view = GetComponent<Camera>();
        }

        void Update()
        {
            float target = SmokeCloud.Active.Count == 0 ? 0f : SmokeCloud.DensityAtPoint(Eyes.position);
            float speed = target > shown ? thickenSpeed : clearSpeed;
            shown = Mathf.MoveTowards(shown, target, speed * Time.deltaTime);
        }

        void OnGUI()
        {
            if (!HudMode.UseLegacy && view != null) return;   // painted in OnPostRender, under the LumaFlow HUD
            if (Event.current.type != EventType.Repaint) return;
            if (shown <= cutoff) return;

            Rect area;
            if (view != null)
            {
                if (!CrewView.TryGetRect(view, out area)) return;   // this view is not on screen
            }
            else area = new Rect(0f, 0f, Screen.width, Screen.height);

            GUI.depth = guiDepth;
            DrawSmoke(area, shown, Time.time);
        }

        // With the LumaFlow HUD (UICORE) the smoke goes into this camera's own picture instead:
        // UI Toolkit panels are drawn before IMGUI, so an OnGUI veil would cover the HUD, the
        // contract and the key hints included. Painted right after the camera rendered, it is
        // under every panel, the way GUI.depth kept it under the old HUD. The pixel matrix maps
        // onto this camera's viewport, so each half of a split screen gets only its own smoke.
        void OnPostRender()
        {
            if (HudMode.UseLegacy || view == null || shown <= cutoff) return;
            int w = view.pixelWidth, h = view.pixelHeight;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, w, h, 0f);   // GUI convention: origin top left, as in MoversSmokeCLI
            DrawSmoke(new Rect(0f, 0f, w, h), shown, Time.time);
            GL.PopMatrix();
        }

        // The four layers, in one place, so the offline preview tool (MoversSmokeCLI) paints
        // exactly what the player gets instead of a lookalike that can drift away from it.
        // Graphics.DrawTexture rather than GUI.DrawTexture for the same reason: it draws both
        // inside OnGUI and into a RenderTexture, which is what makes the preview possible.
        public void DrawSmoke(Rect area, float amount, float time)
        {
            float a = Mathf.Clamp01(amount) * Mathf.Clamp01(strength);
            if (a <= 0f) return;
            float aspect = area.height > 0f ? area.width / area.height : 1.6f;
            var whole = new Rect(0f, 0f, 1f, 1f);

            // 1. flat haze: the light that no longer reaches you. The four alphas below multiply
            // out to about 22 % of the frame still getting through at full density, which is a
            // doorway you can still find and a crewmate you can still make out, badly. Blind
            // enough to be funny, not blind enough to be a punishment (CLAUDE.md 4).
            Graphics.DrawTexture(area, SmokeTextures.Flat, whole, 0, 0, 0, 0,
                Tint(haze, a * 0.45f));

            // 2 and 3. two layers at different scales, scrolling against each other. One layer
            // reads as a dirty lens; two crossing read as smoke you are standing inside.
            Graphics.DrawTexture(area, SmokeTextures.Noise, new Rect(
                    time * scrollSpeed,
                    -time * scrollSpeed * 0.6f + Mathf.Sin(time * 0.35f) * 0.02f,
                    tilingNear * aspect, tilingNear),
                0, 0, 0, 0, Tint(wisps, a * 0.45f));

            Graphics.DrawTexture(area, SmokeTextures.Noise, new Rect(
                    -time * scrollSpeed * 1.7f,
                    time * scrollSpeed * 0.9f,
                    tilingFar * aspect, tilingFar),
                0, 0, 0, 0, Tint(wisps, a * 0.35f));

            // 4. the edges close in first, so the player feels the cloud arrive before it blinds
            Graphics.DrawTexture(area, SmokeTextures.Vignette, whole, 0, 0, 0, 0,
                Tint(edge, a * 0.60f));
        }

        // Graphics.DrawTexture doubles the colour it is given: its neutral value is 0.5, not 1.
        // Measured, not assumed: drawing (0.42 grey, alpha 0.50) over mid grey returned 0.839
        // instead of the 0.460 that straight alpha blending owes, and halving the colour first
        // returned 0.459. Skip this and every layer lands nearly opaque, which whites the screen
        // out and, worse, gets whiter the darker you make the colours.
        static Color Tint(Color c, float alpha)
        {
            return new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, Mathf.Clamp01(alpha) * 0.5f);
        }
    }
}
