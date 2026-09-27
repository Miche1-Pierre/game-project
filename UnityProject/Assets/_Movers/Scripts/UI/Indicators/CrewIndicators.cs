using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // Where the others are, in each player's own view (Pierre: "to know where the characters
    // are"): a compass tape at the top of the view, a ring with the distance over what is in view
    // (walls included), a token with an arrow at the edge for what is not.
    //
    //   the other player     his colour and his number
    //   the grandmother      her mood's colour and face; the house keys on her during the intro
    //   the delivery board   the truck on the tape; an arrow while you carry something; beating
    //                        once everything is loaded
    //
    // Drawn with UI Toolkit on one overlay panel of its own, one box per player viewport. The
    // markers move every frame, so they are retained elements moved by style.translate, not
    // LumaFlow widgets rebuilt per frame (LumaFlow is for UI whose state changes, the HUD's
    // panels). The Target Indicators package (Jake Manfre) gives each view its compass; see
    // CrewIndicatorView.
    //
    // UICORE's HUD draws over the markers, so they keep out from under its corner panels (the
    // contract card, the grandmother's panel, the toasts, the pockets), measured each frame by
    // HudCorners.
    //
    // Goes on _Systems of Map01 (integration/03_scene_setup.cs). Hidden during the intro card and
    // the end screen. Shift+F2 cycles all / tape only / off.
    [DefaultExecutionOrder(1100)]   // after CameraShake (1000): the cameras have their final pose
    [DisallowMultipleComponent]
    public sealed class CrewIndicators : MonoBehaviour
    {
        public enum Mode { All, TapeOnly, Off }

        [Tooltip("Assets/_Movers/UI/Indicators/IndicatorArt.asset: the UIART sprites, Fredoka, the panel. Empty: drawn shapes and the theme's font.")]
        public IndicatorArt art;
        [Tooltip("Assets/_Movers/Data/IndicatorTuning.asset. Empty: the code defaults.")]
        public IndicatorTuning tuning;
        [Tooltip("Shift+F2 cycles it in play.")]
        public Mode mode = Mode.All;

        public IndicatorSettings Settings => tuning != null && tuning.settings != null ? tuning.settings : IndicatorSettings.Defaults;

        // The one in the running scene, for the tests and for HUD pieces that make room for the tape.
        public static CrewIndicators Current { get; private set; }

        // Another system can hide the markers for a moment (a cutscene, a photo). Counted, so two
        // systems hiding them do not undo each other.
        public int HideRequests { get; set; }

        // Pixels (OnGUI) the tape takes at the top of this view: TopCenter content (toasts, the
        // intro banner) belongs below it.
        public static float ReservedTop(Rect view)
        {
            var c = Current;
            if (c == null || !c.isActiveAndEnabled || c.mode == Mode.Off) return 0f;
            IndicatorSettings s = c.Settings;
            if (!s.showTape) return 0f;
            float k = Mathf.Clamp(view.height / 1080f, 0.6f, 1.6f);
            return (s.tapeTop + s.tapeHeight + 8f) * k;
        }

        const string Owner = "INDICATORS";
        // Shift+F2, beside F2 (the split layout, SplitScreen): a view key nobody else registers.
        // Shift+F7 was both this track's and AUDIO's first choice; DebugCommands hands a key to
        // whoever registers last, so two owners of one key lose it by turns. AUDIO is on Shift+F8.
        const KeyCode CycleKey = KeyCode.F2;

        readonly IndicatorTargets targets = new IndicatorTargets();
        readonly HudCorners hud = new HudCorners();
        readonly List<CrewIndicatorView> views = new List<CrewIndicatorView>(4);
        GameObject documentObject;
        UIDocument document;
        PanelSettings runtimePanel;
        ThemeStyleSheet runtimeTheme;
        VisualElement layer;
        bool rosterDirty = true;
        bool shown = true;
        bool started;

        void OnEnable()
        {
            Current = this;
            CrewRoster.Joined += OnRoster;
            CrewRoster.Left += OnRoster;
            DebugCommands.Register(CycleKey, true, "indicators: all / tape only / off", Cycle, Owner);
            rosterDirty = true;
            if (started) CreateLayer();
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            CrewRoster.Joined -= OnRoster;
            CrewRoster.Left -= OnRoster;
            // After a reload Shift+F2 may already be the new scene's: only take back our own.
            if (CrewSetup.OwnsDebugKey(CycleKey, this)) DebugCommands.Unregister(Owner);
            DisposeViews();
            DestroyLayer();
            hud.Clear();
        }

        void Start()
        {
            started = true;
            targets.Find(gameObject.scene);
            CreateLayer();
        }

        void OnDestroy()
        {
            if (runtimePanel != null) Destroy(runtimePanel);
            if (runtimeTheme != null) Destroy(runtimeTheme);
        }

        void OnRoster(CrewMember m) { rosterDirty = true; }

        // Shift+F2.
        public void Cycle()
        {
            mode = mode == Mode.All ? Mode.TapeOnly : mode == Mode.TapeOnly ? Mode.Off : Mode.All;
            DebugCommands.Toast(mode == Mode.All ? "Indicators: all" : mode == Mode.TapeOnly ? "Indicators: tape only" : "Indicators: off");
        }

        void LateUpdate() { Tick(); }

        // One update of every view. Public for the allocation test (tests/18_no_garbage.cs), which
        // runs it many times in a frame: it must not allocate once warm.
        public void Tick()
        {
            if (layer == null) return;
            bool show = ShouldShow();
            SetShown(show);
            if (!show) return;

            IndicatorSettings s = Settings;
            if (rosterDirty) SyncViews(s);
            targets.Refresh(s);
            HudCorners corners = null;
            if (s.keepOutOfHud)
            {
                hud.Update(Time.unscaledTime);
                corners = hud;
            }
            bool markers = mode == Mode.All;
            float time = Time.time;
            for (int i = 0; i < views.Count; i++)
            {
                views[i].Sync(targets);
                views[i].Tick(corners, markers, time);
            }
        }

        bool ShouldShow()
        {
            if (mode == Mode.Off || HideRequests > 0 || SceneFlow.IsLoading) return false;
            var session = GameSession.Current;
            if (session != null && session.gameObject.scene == gameObject.scene && session.IntroCardShowing) return false;
            return !Session.IsOver;
        }

        void SetShown(bool on)
        {
            if (on == shown) return;
            shown = on;
            layer.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---- the panel ----

        // One overlay panel for the markers, drawn under the HUD's and the menus' panels
        // (IndicatorSettings.panelSortingOrder), 1 panel unit = 1 screen pixel.
        void CreateLayer()
        {
            if (layer != null) return;
            PanelSettings panel = art != null ? art.panelSettings : null;
            if (panel == null)
            {
                if (runtimePanel == null)
                {
                    runtimePanel = ScriptableObject.CreateInstance<PanelSettings>();
                    runtimePanel.name = "CrewIndicators (runtime)";
                    runtimePanel.scaleMode = PanelScaleMode.ConstantPixelSize;
                    runtimePanel.scale = 1f;
                    runtimePanel.sortingOrder = Settings.panelSortingOrder;
                    runtimePanel.clearColor = false;
                    // A panel with no theme warns in the console; an empty one is enough here,
                    // every element is styled inline.
                    if (art != null && art.theme != null) runtimePanel.themeStyleSheet = art.theme;
                    else
                    {
                        runtimeTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                        runtimeTheme.hideFlags = HideFlags.DontSave;
                        runtimePanel.themeStyleSheet = runtimeTheme;
                    }
                }
                panel = runtimePanel;
            }

            // Inactive while it is set up: UIDocument builds its panel in OnEnable.
            documentObject = new GameObject("CrewIndicators UI");
            documentObject.SetActive(false);
            documentObject.transform.SetParent(transform, false);
            document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            documentObject.SetActive(true);

            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            layer = IndicatorUi.Fill(new VisualElement { name = "crew-indicators" });
            // The text every label inherits: Fredoka when the art has it, else Unity's built-in
            // runtime font, so the distances show even with no theme font at all.
            Font face = art != null && art.semiBold != null ? art.semiBold : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (face != null) layer.style.unityFontDefinition = new StyleFontDefinition(face);
            root.Add(layer);
            shown = true;
            rosterDirty = true;
        }

        void DestroyLayer()
        {
            if (layer != null) layer.RemoveFromHierarchy();
            layer = null;
            if (documentObject != null) Destroy(documentObject);
            documentObject = null;
            document = null;
        }

        // ---- views ----

        // One view per crew member with a camera. Allocates: on roster changes only.
        void SyncViews(IndicatorSettings s)
        {
            rosterDirty = false;
            for (int i = views.Count - 1; i >= 0; i--)
            {
                CrewIndicatorView v = views[i];
                if (v.owner != null && v.owner.isActiveAndEnabled && v.owner.View != null) continue;
                v.Dispose();
                views.RemoveAt(i);
            }
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m == null || m.View == null || FindView(m.index) != null) continue;
                views.Add(new CrewIndicatorView(m, transform, layer, art, s));
            }
        }

        void DisposeViews()
        {
            for (int i = 0; i < views.Count; i++) views[i].Dispose();
            views.Clear();
        }

        CrewIndicatorView FindView(int crewIndex)
        {
            for (int i = 0; i < views.Count; i++)
                if (views[i].owner != null && views[i].owner.index == crewIndex) return views[i];
            return null;
        }

        // ---- for tests and debug ----

        public int ViewCount => views.Count;
        public bool IsShown => layer != null && shown;

        // The next Tick walks UICORE's HUD again (normally twice a second): lets the allocation
        // test count the walk too, inside one frame.
        public void RescanHudNextTick() { hud.Invalidate(); }

        // This player's viewport and tape, in OnGUI pixels. False when he has no view on screen.
        public bool TryGetView(int crewIndex, out Rect view, out Rect tape)
        {
            view = tape = default;
            CrewIndicatorView v = FindView(crewIndex);
            if (v == null || !v.Shown) return false;
            view = v.ViewRect;
            tape = v.TapeRect;
            return view.width > 0f;
        }

        // What this player's view shows of each target (see IndicatorSnapshot). Appends; returns
        // how many.
        public int GetSnapshots(int crewIndex, List<IndicatorSnapshot> into)
        {
            CrewIndicatorView v = FindView(crewIndex);
            return v != null ? v.Snapshots(into) : 0;
        }

        // The HUD corner panels this player's markers keep clear of, as last measured, in OnGUI
        // pixels. Appends; returns how many (0 without UICORE's HUD or with keepOutOfHud off).
        public int GetHudRects(int crewIndex, List<Rect> into)
        {
            CrewIndicatorView v = FindView(crewIndex);
            return v != null ? v.HudRects(into) : 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }
    }
}
