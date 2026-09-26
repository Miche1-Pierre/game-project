using UnityEngine;

namespace Movers
{
    // How the players' cameras share the screen. Goes on _Systems next to CrewSpawner.
    //
    // Side by side, not stacked: the carry happens in the lower half of the view (a heavy
    // object sags up to 0.7 m) and the stairs need vertical field, so each view keeps the full
    // height and gets a wider vertical FOV to win back some of the width it lost (75 gives
    // about 69 degrees across in an 8:9 half, against 91 full screen at 60).
    //
    // F2 cycles split, solo P1, solo P2. Solo switches the other camera off, which also halves
    // the rendering while one person tests alone, and puts the one AudioListener on the
    // camera being watched. In split it stays on P1.
    //
    // One player (the tutorial) is left exactly as the scene has it: full screen, its own FOV.
    [DisallowMultipleComponent]
    public sealed class SplitScreen : MonoBehaviour
    {
        public enum Layout { Split, SoloFirst, SoloSecond }

        public Layout layout = Layout.Split;
        // Vertical FOV of each half in split. Full screen views keep the FOV their camera was
        // authored with (60 on the house player).
        public float splitFieldOfView = 75f;

        const string Owner = "PLAYER (view)";
        const int MaxViews = 4;

        // The FOV each camera had before this touched it, to give it back in solo.
        readonly Camera[] seen = new Camera[MaxViews];
        readonly float[] authoredFov = new float[MaxViews];
        int seenCount;

        // A player joined or left: the views are laid out again at the end of the frame, not
        // inside the roster event. When the scene is torn down (leaving Play, a reload) every
        // player leaves in no set order, possibly before this is switched off, and re-laying
        // cameras and ears on a dying scene is at best wasted and at worst console noise. A
        // torn-down scene has no end of frame, so the flag simply dies with it.
        bool dirty;

        // What F2 did, shown at the end of the frame: SliceDebug toasts the key's own label
        // right after running the command, which would bury "Solo P1" in the same frame.
        string resultToast;

        public Layout Current => layout;

        void OnEnable()
        {
            CrewRoster.Joined += OnRosterChanged;
            CrewRoster.Left += OnRosterChanged;
            DebugCommands.Register(KeyCode.F2, false, "layout: split / solo P1 / solo P2", Cycle, Owner);
        }

        void OnDisable()
        {
            CrewRoster.Joined -= OnRosterChanged;
            CrewRoster.Left -= OnRosterChanged;
            dirty = false;
            // Same care as CrewSpawner: after a reload F2 may already be the new scene's.
            if (CrewSetup.OwnsDebugKey(KeyCode.F2, this)) DebugCommands.Unregister(Owner);
        }

        void Start()
        {
            dirty = false;
            Apply();
        }

        void OnRosterChanged(CrewMember m) { dirty = true; }

        void LateUpdate()
        {
            if (dirty)
            {
                dirty = false;
                Apply();
            }
            if (resultToast != null)
            {
                DebugCommands.Toast(resultToast);
                resultToast = null;
            }
        }

        // F2.
        public void Cycle()
        {
            int n = CrewRoster.Count;
            if (n < 2) { layout = Layout.Split; Apply(); return; }
            layout = layout == Layout.Split ? Layout.SoloFirst
                   : layout == Layout.SoloFirst ? Layout.SoloSecond
                   : Layout.Split;
            Apply();
            resultToast = layout == Layout.Split ? "Split screen" : layout == Layout.SoloFirst ? "Solo P1" : "Solo P2";
        }

        public void SetLayout(Layout l)
        {
            layout = l;
            Apply();
        }

        public void Apply()
        {
            var roster = CrewRoster.All;
            int n = 0;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].View != null) n++;
            if (n == 0) return;

            // Which roster slot is watched in solo; the rest are switched off.
            int solo = -1;
            if (n > 1 && layout == Layout.SoloFirst) solo = 0;
            if (n > 1 && layout == Layout.SoloSecond) solo = 1;

            Camera ears = null;
            int slot = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var m = roster[i];
                if (m == null || m.View == null) continue;
                Camera cam = m.View;
                float fov = AuthoredFov(cam);

                if (n == 1)
                {
                    // One player: full screen with the FOV the scene gave it, as before the
                    // split existed (also what P1 gets back if P2 leaves).
                    cam.enabled = true;
                    cam.rect = new Rect(0f, 0f, 1f, 1f);
                    cam.fieldOfView = fov;
                    ears = cam;
                }
                else if (solo >= 0)
                {
                    bool shown = slot == solo;
                    cam.enabled = shown;
                    cam.rect = new Rect(0f, 0f, 1f, 1f);
                    cam.fieldOfView = fov;
                    if (shown) ears = cam;
                }
                else
                {
                    cam.enabled = true;
                    cam.rect = SplitRect(slot, n);
                    cam.fieldOfView = splitFieldOfView;
                    if (slot == 0) ears = cam;
                }
                slot++;
            }

            PutEarsOn(ears);
        }

        // Two players side by side, P1 on the left. Three or four: a 2 x 2 grid, P1 top left.
        static Rect SplitRect(int slot, int count)
        {
            if (count == 2) return new Rect(slot * 0.5f, 0f, 0.5f, 1f);
            float x = (slot % 2) * 0.5f;
            float y = slot < 2 ? 0.5f : 0f;
            return new Rect(x, y, 0.5f, 0.5f);
        }

        float AuthoredFov(Camera cam)
        {
            for (int i = 0; i < seenCount; i++)
                if (seen[i] == cam) return authoredFov[i];
            if (seenCount < MaxViews)
            {
                seen[seenCount] = cam;
                authoredFov[seenCount] = cam.fieldOfView;
                seenCount++;
            }
            return cam.fieldOfView;
        }

        // Exactly one AudioListener, on the camera that owns the sound. The old one is switched
        // off before the new one exists, so there is never a frame with two listening (Unity
        // warns about that every frame it happens).
        static void PutEarsOn(Camera cam)
        {
            if (cam == null) return;
            var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            AudioListener keep = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject == cam.gameObject && keep == null) { keep = all[i]; continue; }
                all[i].enabled = false;
                Destroy(all[i]);
            }
            if (keep == null) keep = cam.gameObject.AddComponent<AudioListener>();
            keep.enabled = true;
        }
    }
}
