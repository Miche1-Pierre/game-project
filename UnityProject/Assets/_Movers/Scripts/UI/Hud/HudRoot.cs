using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using LumaFlow;
using UnityEngine;
using UnityEngine.UIElements;
using Framework = LumaFlow.LumaFlow;

namespace Movers
{
    // The in-game HUD on LumaFlow: one UIDocument over the whole screen, one container per
    // crew member laid exactly on that member's camera viewport (ViewportGUI.RectFor), and a
    // screen layer above them for the intro card and the end screen. It replaces every
    // gameplay OnGUI (HudMode), keeps the debug overlays on OnGUI, and survives a scene reload
    // (it is a scene object; HudBootstrap makes one in every gameplay scene).
    //
    // Once per frame, after the cameras moved (LateUpdate, late order), it reads the game into
    // the models and moves the world-pinned labels. The views rebuild only when a model state
    // changes; a normal frame allocates nothing here.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2000)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudRoot : MonoBehaviour
    {
        public static HudRoot Active { get; private set; }

        // Each player's view container is named with this and the player's name; UiZoom finds
        // its frame by it.
        public const string ViewNamePrefix = "hud-view-";

        [Tooltip("Layouts are designed for a view this many panel units wide; a narrower or shorter view is scaled down, never below minScale.")]
        public Vector2 designSize = new Vector2(900f, 1000f);
        [Range(0.5f, 1f)] public float minScale = 0.7f;

        sealed class View
        {
            public PlayerHudModel model;
            public VisualElement container;
            public MountHandle mount;
            public bool visible = true;
            public Vector2 origin;        // panel units, top left of the viewport
            public Vector2 size;          // panel units, before the HUD scale
            public float scale = 1f;
            public bool failed;
        }

        UIDocument document;
        VisualElement root, viewLayer, screenLayer;
        MountHandle screenMount;
        readonly SharedHudModel shared = new SharedHudModel();
        readonly List<View> views = new List<View>(4);
        readonly MutedInputReader endKeys = new MutedInputReader();
        bool built, started, subscribed, sharedFailed;
        int shownCard = -1;
        System.Action<string> onNotice;
        bool noticeSubscribed;

        // UI Toolkit's own keyboard and pad navigation, switched off on this panel (see
        // BlockNavigation). Cached delegates: registered once per panel, no garbage.
        VisualElement navTree;
        EventCallback<NavigationMoveEvent> onNavMove;
        EventCallback<NavigationSubmitEvent> onNavSubmit;
        EventCallback<NavigationCancelEvent> onNavCancel;

        // What this component's LateUpdate cost, smoothed, and its worst frame (the UI frame
        // test). The widget rebuilds happen inside it, so it covers LumaFlow's work too; the
        // UI Toolkit panel's own layout and render are Unity's and show in the Profiler.
        public float LastUpdateMs { get; private set; }
        public float MaxUpdateMs { get; private set; }
        public int FramesMeasured { get; private set; }
        // Frames in which the HUD allocated managed memory, and how much in all (editor and
        // development builds only). A still frame should allocate nothing.
        public int FramesWithAlloc { get; private set; }
        public long AllocBytes { get; private set; }

        public void ResetStats()
        {
            MaxUpdateMs = 0f;
            FramesMeasured = FramesWithAlloc = 0;
            AllocBytes = 0;
        }
        public SharedHudModel Shared => shared;
        public int ViewCount => views.Count;

        // ---- lifecycle ----

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (document.panelSettings == null) document.panelSettings = HudPanel.Settings;
        }

        void OnEnable()
        {
            HudMode.Acquire();
            Active = this;
            if (started) Build();
        }

        void Start()
        {
            started = true;
            Build();
        }

        void OnDisable()
        {
            Teardown();
            HudMode.Release();
            if (Active == this) Active = null;
        }

        void Build()
        {
            if (built) return;
            root = document.rootVisualElement;
            if (root == null) return;
            built = true;
            HudPanel.StyleRoot(root);
            BlockNavigation();

            viewLayer = Layer("hud-views");
            screenLayer = Layer("hud-screen");
            root.Add(viewLayer);
            root.Add(screenLayer);
            screenLayer.style.display = DisplayStyle.None;

            shared.Toasts.Subscribe();
            CrewRoster.Joined += OnJoined;
            CrewRoster.Left += OnLeft;
            GameSettings.Changed += OnSettingsChanged;
            subscribed = true;
            // Online only: NetSession's notices ("your partner left") become toasts.
            if (Net.IsOnline)
            {
                NetText.Ensure();
                if (onNotice == null) onNotice = OnNetNotice;
                NetSession.Notice += onNotice;
                noticeSubscribed = true;
            }

            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++) Add(crew[i]);
            try
            {
                screenMount = Framework.Mount(ScreenHudView.Build(shared), screenLayer);
                IgnorePicking(screenLayer);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);   // the cards are lost, the players' HUDs still run
            }
        }

        void Teardown()
        {
            UnblockNavigation();
            if (subscribed)
            {
                shared.Toasts.Unsubscribe();
                CrewRoster.Joined -= OnJoined;
                CrewRoster.Left -= OnLeft;
                GameSettings.Changed -= OnSettingsChanged;
                subscribed = false;
            }
            if (noticeSubscribed)
            {
                NetSession.Notice -= onNotice;
                noticeSubscribed = false;
            }
            for (int i = 0; i < views.Count; i++) Release(views[i]);
            views.Clear();
            screenMount?.Dispose();
            screenMount = null;
            viewLayer?.RemoveFromHierarchy();
            screenLayer?.RemoveFromHierarchy();
            viewLayer = screenLayer = null;
            built = false;
            shownCard = -1;
        }

        static VisualElement Layer(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0f;
            e.style.top = 0f;
            e.style.right = 0f;
            e.style.bottom = 0f;
            return e;
        }

        // LumaFlow's host covers its container; only real controls should catch the mouse.
        static void IgnorePicking(VisualElement container)
        {
            for (int i = 0; i < container.childCount; i++) container[i].pickingMode = PickingMode.Ignore;
        }

        void OnNetNotice(string key)
        {
            if (!string.IsNullOrEmpty(key)) shared.Toasts.Push(Loc.T(key), UiSprites.IconWarning, UiTheme.Current.warn);
        }

        // ---- the crew ----

        void OnJoined(CrewMember m)
        {
            if (built && isActiveAndEnabled) Add(m);
        }

        void OnLeft(CrewMember m)
        {
            for (int i = views.Count - 1; i >= 0; i--)
            {
                if (views[i].model.member != m) continue;
                Release(views[i]);
                views.RemoveAt(i);
            }
        }

        void Add(CrewMember m)
        {
            if (m == null) return;
            for (int i = 0; i < views.Count; i++) if (views[i].model.member == m) return;
            var v = new View { model = new PlayerHudModel(m) };
            v.container = new VisualElement { name = ViewNamePrefix + m.DisplayName, pickingMode = PickingMode.Ignore };
            v.container.style.position = Position.Absolute;
            v.container.style.overflow = Overflow.Hidden;
            v.container.style.transformOrigin = new TransformOrigin(0f, 0f);
            viewLayer.Add(v.container);
            try
            {
                v.mount = Framework.Mount(PlayerHudView.Build(shared, v.model), v.container);
                IgnorePicking(v.container);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                v.failed = true;
            }
            views.Add(v);
            // Sorted by player, so P1's view is laid first and its pause menu is under P2's.
            views.Sort((a, b) => a.model.Index.CompareTo(b.model.Index));
            SyncView(v, true);
        }

        static void Release(View v)
        {
            v.model.Dispose();
            v.mount?.Dispose();
            v.mount = null;
            v.container?.RemoveFromHierarchy();
        }

        void OnSettingsChanged()
        {
            shared.Relocalize();
            shared.ApplyLanguageToGrandma();
            for (int i = 0; i < views.Count; i++)
            {
                views[i].model.Relocalize();
                views[i].mount?.Rebuild();
            }
            screenMount?.Rebuild();
        }

        // ---- per frame ----

        void LateUpdate()
        {
            if (!built)
            {
                Build();
                if (!built) return;
            }
            if (navTree == null) BlockNavigation();   // the panel may come after Build
            long t0 = Stopwatch.GetTimestamp();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long mono0 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
#endif

            try
            {
                shared.Update();
                UpdateScreenLayer();
            }
            catch (System.Exception e)
            {
                if (!sharedFailed) Debug.LogException(e);
                sharedFailed = true;
            }
            for (int i = 0; i < views.Count; i++)
            {
                View v = views[i];
                try
                {
                    SyncView(v, false);
                    v.model.Update(shared, v.visible);
                    if (v.visible) PlaceWorldLabels(v);
                }
                catch (System.Exception e)
                {
                    // One player's HUD failing must not take the other's down, nor log every
                    // frame: reported once per view.
                    if (!v.failed) Debug.LogException(e);
                    v.failed = true;
                }
            }

            DropStrayFocus();

            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            LastUpdateMs = LastUpdateMs <= 0f ? (float)ms : Mathf.Lerp(LastUpdateMs, (float)ms, 0.1f);
            if (ms > MaxUpdateMs) MaxUpdateMs = (float)ms;
            FramesMeasured++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long grew = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - mono0;
            if (grew > 0)
            {
                FramesWithAlloc++;
                AllocBytes += grew;
            }
#endif
        }

        // ---- UI Toolkit navigation, off ----

        // The menus are driven by each player's own device (HudPauseMenu), never by UI
        // Toolkit's navigation. That one is shared: with the legacy Input Manager its move and
        // submit read every keyboard key and every pad (Horizontal, Vertical, Submit), and with
        // nothing focused it focuses the first button it finds. P2's stick and A could then
        // press P1's "Reprendre", or "Rejouer" on the end screen. So the three navigation
        // events are stopped on this panel before anything sees them, and the focus
        // controller is told to ignore them. The mouse still clicks: pointer events are not
        // navigation.
        void BlockNavigation()
        {
            var panel = root != null ? root.panel : null;
            if (panel == null || navTree != null) return;
            navTree = panel.visualTree;
            if (onNavMove == null)
            {
                onNavMove = e => Swallow(e);
                onNavSubmit = e => Swallow(e);
                onNavCancel = e => Swallow(e);
            }
            navTree.RegisterCallback(onNavMove, TrickleDown.TrickleDown);
            navTree.RegisterCallback(onNavSubmit, TrickleDown.TrickleDown);
            navTree.RegisterCallback(onNavCancel, TrickleDown.TrickleDown);
        }

        // The panel belongs to the PanelSettings asset and outlives this scene: the callbacks
        // must leave with the HUD, or every reload would add three more.
        void UnblockNavigation()
        {
            if (navTree == null) return;
            navTree.UnregisterCallback(onNavMove, TrickleDown.TrickleDown);
            navTree.UnregisterCallback(onNavSubmit, TrickleDown.TrickleDown);
            navTree.UnregisterCallback(onNavCancel, TrickleDown.TrickleDown);
            navTree = null;
        }

        void Swallow(EventBase e)
        {
            var focus = navTree != null && navTree.panel != null ? navTree.panel.focusController : null;
            if (focus != null) focus.IgnoreEvent(e);
            e.StopImmediatePropagation();
        }

        // A button the mouse clicked keeps UI Toolkit's focus. Nothing can press it through
        // the focus any more (navigation is off), but a focused element is still a state to
        // leave behind, so it is dropped once the mouse is released: dropping it while the
        // button is held would end the press and hide button_wood_pressed during the click.
        void DropStrayFocus()
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) return;
            var focus = root.focusController;
            var focused = focus != null ? focus.focusedElement : null;
            if (focused != null) focused.Blur();
        }

        void UpdateScreenLayer()
        {
            int card = shared.Card.Value;
            if (card != shownCard)
            {
                shownCard = card;
                screenLayer.style.display = card != (int)CardKind.None ? DisplayStyle.Flex : DisplayStyle.None;
                if (card == (int)CardKind.End)
                {
                    endKeys.Reset();
                    var result = shared.Result;
                    if (result != null && result.Completed && result.Total > 0) UiAudio.Money();
                    else UiAudio.Error();
                    // The end screen's buttons are for the mouse too.
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }
            }
            // Pause (Esc, Start) on the end screen goes back to the menu; E/X is the session's
            // own "play again".
            // Online the host's leave ends the run for both, the client's leaves alone.
            if (card == (int)CardKind.End && endKeys.Pressed(CrewButton.Pause))
            {
                if (Net.IsOnline) NetSession.LeaveToMenu();
                else SceneFlow.LoadMenu();
            }
        }

        // Lays the container on the camera's viewport, in panel units. A view shorter or
        // narrower than the design size gets the whole HUD scaled down (stacked split screen).
        void SyncView(View v, bool force)
        {
            Camera cam = v.model.member != null ? v.model.member.View : null;
            bool visible = cam != null && cam.isActiveAndEnabled;
            if (visible != v.visible || force)
            {
                v.visible = visible;
                v.container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!visible || root.panel == null) return;

            Rect gui = ViewportGUI.RectFor(cam);
            Vector2 tl = RuntimePanelUtils.ScreenToPanel(root.panel, gui.position);
            Vector2 br = RuntimePanelUtils.ScreenToPanel(root.panel, gui.max);
            Vector2 size = br - tl;
            if (size.x < 1f || size.y < 1f) return;
            if (!force && (tl - v.origin).sqrMagnitude < 0.01f && (size - v.size).sqrMagnitude < 0.01f) return;

            v.origin = tl;
            v.size = size;
            v.scale = Mathf.Clamp(Mathf.Min(size.x / designSize.x, size.y / designSize.y), minScale, 1f);
            var s = v.container.style;
            s.left = tl.x;
            s.top = tl.y;
            s.width = size.x / v.scale;
            s.height = size.y / v.scale;
            s.scale = new Scale(new Vector3(v.scale, v.scale, 1f));
        }

        // The panel rect of a player's view as laid out now (tests compare it with the camera).
        public bool TryGetViewRect(CrewMember m, out Rect panelRect, out float scale)
        {
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].model.member != m) continue;
                panelRect = new Rect(views[i].origin, views[i].size);
                scale = views[i].scale;
                return views[i].visible;
            }
            panelRect = default;
            scale = 1f;
            return false;
        }

        public PlayerHudModel ModelOf(CrewMember m)
        {
            for (int i = 0; i < views.Count; i++) if (views[i].model.member == m) return views[i].model;
            return null;
        }

        // ---- labels pinned to the world ----

        void PlaceWorldLabels(View v)
        {
            Camera cam = v.model.member.View;
            Vector2 local = default;

            // Her words, over her head, kept inside the view.
            GrandmaSpeech speech = shared.GrandmaSpeech;
            UiAnchor bubble = v.model.SpeechAnchor;
            bool talk = speech != null && shared.Speech.Value != null && !v.model.pause.IsOpen;
            if (talk)
            {
                Vector3 head = speech.transform.position + Vector3.up * speech.headHeight;
                talk = (cam.transform.position - head).sqrMagnitude <= speech.bubbleRange * speech.bubbleRange
                       && Project(v, cam, head, out local);
                if (talk)
                {
                    Vector2 box = bubble.Size;
                    float w = v.size.x / v.scale, h = v.size.y / v.scale;
                    local.x = Mathf.Clamp(local.x, box.x * 0.5f + 8f, Mathf.Max(box.x * 0.5f + 8f, w - box.x * 0.5f - 8f));
                    local.y = Mathf.Clamp(local.y - 12f, box.y + 8f, Mathf.Max(box.y + 8f, h - 8f));
                    bubble.MoveTo(local);
                }
            }
            bubble.Show(talk);

            // "Deliver here" over the board, once everything is loaded.
            DeliverPoint dp = shared.Deliver;
            UiAnchor sign = v.model.DeliverAnchor;
            bool ready = dp != null && shared.Banner.Value == (int)BannerKind.Ready && !v.model.pause.IsOpen
                         && Project(v, cam, dp.transform.position + Vector3.up * 0.9f, out local);
            if (ready) sign.MoveTo(local);
            sign.Show(ready);
        }

        // A world point in the view's own (scaled) coordinates. False behind the camera or
        // outside its viewport.
        bool Project(View v, Camera cam, Vector3 world, out Vector2 local)
        {
            local = default;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return false;
            Rect px = cam.pixelRect;
            if (sp.x < px.xMin - 40f || sp.x > px.xMax + 40f || sp.y < px.yMin || sp.y > px.yMax + 200f) return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
            local = (panelPoint - v.origin) / v.scale;
            return true;
        }
    }
}
