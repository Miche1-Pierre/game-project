using LumaFlow;
using UnityEngine;
using UnityEngine.UIElements;
using Framework = LumaFlow.LumaFlow;

namespace Movers
{
    // The loading screen: shown when SceneFlow starts loading (LoadingScreenBoot), kept alive
    // across the scene change (DontDestroyOnLoad), gone once the new scene is up.
    //
    //   fade in     0.25 s over the menu or the paused game
    //   loading     the tape follows SceneFlow.Progress (it never goes back), the mover runs
    //               and trips on his little stage, a tip changes every few seconds
    //   finishing   the new scene is active: the tape runs to the end, and if the mover is on
    //               the ground he is given a moment to get up (at most 1.4 s)
    //   fade out    0.45 s, then everything is destroyed: panel, stage, RenderTexture
    //
    // Everything runs on unscaled time. A frame here allocates nothing: the tape is a UiFill,
    // the percentage a cached string, the tip a rebuild every few seconds.
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        public enum Step { FadingIn, Loading, Finishing, FadingOut }

        public static LoadingScreen Active { get; private set; }
        // How many loading screens were shown since Play started, and how the last one went
        // (the Play-mode tests read these).
        public static int Shown { get; private set; }
        public static string LastReport { get; private set; } = "";
        // The last screen's gag, as numbers: how often the mover went down and how much his
        // body moved (RunnerAnimator.PoseMotion, degrees). The tests require both.
        public static int LastTrips { get; private set; }
        public static float LastPoseMotion { get; private set; }
        public static bool LastPoseMeasured { get; private set; }   // false: no humanoid body to measure

        public float fadeIn = 0.25f;
        public float fadeOut = 0.45f;
        public float tipSeconds = 4.5f;
        public float maxGetUpWait = 1.4f;

        public Step Current { get; private set; }
        public string Target { get; private set; }
        public float ShownProgress => shown;
        public LoadingStage Stage => stage;

        readonly LoadingView.Model model = new LoadingView.Model();
        readonly NavigationBlock block = new NavigationBlock();
        UIDocument document;
        PanelSettings panel;
        VisualElement container;
        MountHandle mount;
        LoadingStage stage;
        float stepTime, alpha, shown, nextTip, openedAt;
        int tipIndex = -1;

        static readonly string[] Percents = MakePercents();

        // ---- lifecycle ----

        public static LoadingScreen Show(string target)
        {
            if (Active != null)
            {
                // A new load while the last screen is still fading: the same screen comes back.
                Active.Begin(target);
                return Active;
            }
            var go = new GameObject("LoadingScreen");
            go.SetActive(false);   // so the document has its panel before it first enables
            DontDestroyOnLoad(go);
            var screen = go.AddComponent<LoadingScreen>();
            screen.document = go.AddComponent<UIDocument>();
            screen.panel = MenuPanel.Create("MoversLoadingPanel (runtime)", MenuPanel.LoadingOrder);
            screen.document.panelSettings = screen.panel;
            screen.document.sortingOrder = 0f;
            Active = screen;
            go.SetActive(true);
            screen.Begin(target);
            return screen;
        }

        void Begin(string target)
        {
            MenuText.Ensure();
            Target = target;
            Shown++;
            openedAt = Time.unscaledTime;
            Current = Step.FadingIn;
            stepTime = 0f;
            shown = 0f;
            model.Bar.Set(0f);
            model.Percent.Value = Percents[0];
            model.toGame = target == SceneFlow.GameScene;
            model.players = SceneFlow.RequestedPlayers;
            model.padConnected = GamepadSource.FirstConnected() > 0;

            if (stage == null)
            {
                var stageGo = new GameObject("LoadingStage");
                stageGo.transform.SetParent(transform, false);
                stage = stageGo.AddComponent<LoadingStage>();
                try { stage.Build(LoadingStageSettings.Current, 1024, 512); }
                catch (System.Exception e)
                {
                    // A broken stage costs the picture, never the loading screen.
                    Debug.LogException(e);
                }
                model.window = stage.Texture;
            }
            NextTip();
            Remount();
        }

        void Remount()
        {
            var root = document.rootVisualElement;
            if (root == null) return;
            if (container == null)
            {
                HudPanel.StyleRoot(root);
                container = new VisualElement { name = "loading-root" };
                container.style.position = Position.Absolute;
                container.style.left = 0f;
                container.style.top = 0f;
                container.style.right = 0f;
                container.style.bottom = 0f;
                // Catches the mouse: a click during the load reaches neither the menu nor the game.
                container.pickingMode = PickingMode.Position;
                root.Add(container);
            }
            mount?.Dispose();
            try { mount = Framework.Mount(LoadingView.Build(model), container); }
            catch (System.Exception e) { Debug.LogException(e); }
            SetAlpha(alpha);
        }

        void OnDisable()
        {
            block.Detach();
        }

        void OnDestroy()
        {
            mount?.Dispose();
            mount = null;
            if (panel != null) Destroy(panel);
            if (Active == this) Active = null;
        }

        // ---- the load ----

        // SceneFlow finished: the new scene is active.
        public void Finish(string target)
        {
            if (Current == Step.FadingOut) return;
            Current = Step.Finishing;
            stepTime = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            stepTime += dt;
            block.Attach(container);
            if (stage != null) stage.Tick(dt);

            // The tape: towards SceneFlow's progress, never back, full once the scene is up.
            float target = Current == Step.Finishing || Current == Step.FadingOut ? 1f : Mathf.Max(shown, SceneFlow.Progress);
            shown = Mathf.MoveTowards(shown, target, dt * (target >= 1f ? 1.6f : 0.9f));
            model.Bar.Set(shown);
            model.Percent.Value = Percents[Mathf.Clamp(Mathf.FloorToInt(shown * 100f), 0, 100)];

            if (Time.unscaledTime >= nextTip) NextTip();

            switch (Current)
            {
                case Step.FadingIn:
                    SetAlpha(Mathf.Clamp01(stepTime / Mathf.Max(0.01f, fadeIn)));
                    if (stepTime >= fadeIn) { Current = Step.Loading; stepTime = 0f; }
                    break;
                case Step.Loading:
                    SetAlpha(1f);
                    // A load that failed never reports its end: do not stay up for ever.
                    if (!SceneFlow.IsLoading && stepTime > 1f) Finish(Target);
                    break;
                case Step.Finishing:
                    SetAlpha(1f);
                    bool standing = stage == null || stage.Runner == null || stage.Runner.IsRunning;
                    if ((shown >= 0.999f && standing) || stepTime >= maxGetUpWait)
                    {
                        Current = Step.FadingOut;
                        stepTime = 0f;
                        var runner = stage != null ? stage.Runner : null;
                        LastTrips = runner != null ? runner.Trips : 0;
                        LastPoseMotion = runner != null ? runner.PoseMotion : 0f;
                        LastPoseMeasured = runner != null && runner.CanMeasurePose;
                        LastReport = Target + " in " + (Time.unscaledTime - openedAt).ToString("0.00") + " s, trips " + LastTrips
                                   + ", pose " + (LastPoseMeasured ? LastPoseMotion.ToString("0") + " deg" : "not measured");
                    }
                    break;
                case Step.FadingOut:
                    SetAlpha(1f - Mathf.Clamp01(stepTime / Mathf.Max(0.01f, fadeOut)));
                    if (stepTime >= fadeOut) Destroy(gameObject);
                    break;
            }
        }

        void SetAlpha(float a)
        {
            alpha = a;
            if (container != null) container.style.opacity = a;
        }

        void NextTip()
        {
            nextTip = Time.unscaledTime + tipSeconds;
            var tips = MenuText.Tips;
            for (int tries = 0; tries < tips.Length; tries++)
            {
                tipIndex = tipIndex < 0 ? Random.Range(0, tips.Length) : (tipIndex + 1) % tips.Length;
                if (!tips[tipIndex].twoPlayers || model.players >= 2) break;
            }
            model.Tip.Value = tipIndex;
        }

        static string[] MakePercents()
        {
            var p = new string[101];
            for (int i = 0; i <= 100; i++) p[i] = i + " %";
            return p;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = null;
            Shown = 0;
            LastReport = "";
            LastTrips = 0;
            LastPoseMotion = 0f;
            LastPoseMeasured = false;
        }
    }
}
