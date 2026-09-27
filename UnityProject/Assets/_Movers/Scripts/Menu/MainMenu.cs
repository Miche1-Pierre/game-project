using LumaFlow;
using UnityEngine;
using UnityEngine.UIElements;
using Framework = LumaFlow.LumaFlow;

namespace Movers
{
    // The title screen of the MainMenu scene. Goes on _Menu with the scene's UIDocument; the
    // living landscape behind it (MenuLand, the truck, the crew) runs on its own.
    //
    //   Jouer seul / Jouer à deux   SceneFlow.LoadGame(1 or 2): the loading screen takes over
    //   Jouer en ligne              host or join by code (NETCODE_SLICE 3.3); NetSession loads
    //   Options                     volumes, look, language, split layout, key hints
    //   Contrôles                   the controls sheet, keyboard or gamepad
    //   Quitter                     SceneFlow.Quit
    //
    // Keyboard, any gamepad or the mouse: MenuNav reads the keys and pads (UI Toolkit's own
    // navigation is off on this panel), the mouse clicks the same wooden buttons. While the
    // mouse drives, no row is lit by the keys, so only one button ever looks selected.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenu : MonoBehaviour
    {
        public static MainMenu Active { get; private set; }

        [Tooltip("Seconds between two checks for a plugged-in gamepad (the check allocates).")]
        public float padCheckSeconds = 1f;

        public MainMenuModel Model => model;
        public MenuPage Page => model.Page;

        readonly MainMenuModel model = new MainMenuModel();
        readonly MenuNav nav = new MenuNav();   // plain C#: it checks the pad axes on its first Update
        readonly NavigationBlock block = new NavigationBlock();
        UIDocument document;
        VisualElement container;
        MountHandle mount;
        PanelSettings panel;
        System.Action<int> onActivate;
        System.Action<int, int> onChange;
        System.Action<string> onSubmitCode;
        Vector3 lastMouse;
        float nextPadCheck;
        bool focusFieldPending, wasTyping;

        void Awake()
        {
            MenuText.Ensure();
            NetText.Ensure();
            document = GetComponent<UIDocument>();
            // A panel of its own, made here: nothing to set up in the scene but the document.
            if (document.panelSettings == null)
            {
                panel = MenuPanel.Create("MoversMenuPanel (runtime)", MenuPanel.MenuOrder);
                document.panelSettings = panel;
            }
            onActivate = Activate;
            onChange = Change;
            onSubmitCode = _ => Connect();
        }

        void OnEnable()
        {
            Active = this;
            // Back from a game: whatever it left behind (a paused world, a locked cursor).
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            GameSettings.Changed += OnSettingsChanged;
            nav.Reset();
            lastMouse = Input.mousePosition;
            model.SetPad(GamepadSource.FirstConnected() > 0);
            nextPadCheck = Time.unscaledTime + padCheckSeconds;
            Mount();
        }

        void OnDisable()
        {
            GameSettings.Changed -= OnSettingsChanged;
            block.Detach();
            mount?.Dispose();
            mount = null;
            container?.RemoveFromHierarchy();
            container = null;
            if (Active == this) Active = null;
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        void Mount()
        {
            var root = document.rootVisualElement;
            if (root == null || mount != null) return;
            HudPanel.StyleRoot(root);
            container = new VisualElement { name = "menu-root", pickingMode = PickingMode.Ignore };
            container.style.position = Position.Absolute;
            container.style.left = 0f;
            container.style.top = 0f;
            container.style.right = 0f;
            container.style.bottom = 0f;
            root.Add(container);
            try
            {
                mount = Framework.Mount(MainMenuView.Build(model, onActivate, onChange, onSubmitCode), container);
                for (int i = 0; i < container.childCount; i++) container[i].pickingMode = PickingMode.Ignore;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        void Update()
        {
            if (mount == null) Mount();
            block.Attach(container);
            // Typing a code: the field keeps its focus, and only Esc is read (it leaves the page).
            bool typing = model.Page == MenuPage.Joining && model.CodeFocus.IsFocused.Value;
            if (!typing) block.DropStrayFocus();
            PollNet();
            if (model.Leaving) return;
            if (focusFieldPending && (model.Page != MenuPage.Joining || model.CodeFocus.RequestFocus())) focusFieldPending = false;
            if (typing)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    BlurField();
                    UiAudio.Click();
                    Back();
                }
                wasTyping = true;
                return;
            }
            if (wasTyping)
            {
                wasTyping = false;
                nav.Reset();   // the keys typed into the field are not menu moves
            }

            if (Time.unscaledTime >= nextPadCheck)
            {
                nextPadCheck = Time.unscaledTime + padCheckSeconds;
                model.SetPad(GamepadSource.FirstConnected() > 0);
            }

            // The mouse moved or clicked: it drives now, the keys' selection hides.
            Vector3 mouse = Input.mousePosition;
            if ((mouse - lastMouse).sqrMagnitude > 9f || Input.GetMouseButtonDown(0))
            {
                nav.MouseUsed();
                model.SetShowSelection(false);
                model.SetDevice(MenuDevice.Keyboard);
            }
            lastMouse = mouse;

            nav.Update();
            if (!nav.AnyKey) return;
            model.SetDevice(nav.LastDevice);

            // After the mouse, the first key (but Back) only brings the selection back, where
            // it was: nothing is changed or pressed that the player could not see lit.
            if (!model.ShowSelection && !nav.Back)
            {
                model.SetShowSelection(true);
                UiAudio.Hover();
                return;
            }

            if (nav.Vertical != 0)
            {
                model.Step(-nav.Vertical);
                UiAudio.Hover();
            }
            if (nav.Horizontal != 0 && (model.Page == MenuPage.Options || model.Page == MenuPage.Controls))
            {
                UiAudio.Click();
                Change(model.Selected, nav.Horizontal);
            }
            if (nav.Tab != 0 && model.Page == MenuPage.Controls)
            {
                UiAudio.Click();
                Change(0, nav.Tab);
            }
            if (nav.Submit)
            {
                UiAudio.Click();
                Activate(model.Selected);
            }
            else if (nav.Back) Back();
        }

        // ---- actions (the buttons call these too) ----

        void Activate(int row)
        {
            if (model.Leaving) return;
            switch (model.Page)
            {
                case MenuPage.Title:
                    switch ((MainMenuModel.TitleRow)row)
                    {
                        case MainMenuModel.TitleRow.Solo: StartGame(1); break;
                        case MainMenuModel.TitleRow.Duo: StartGame(2); break;
                        case MainMenuModel.TitleRow.Online: Show(MenuPage.Online); break;
                        case MainMenuModel.TitleRow.Options: Show(MenuPage.Options); break;
                        case MainMenuModel.TitleRow.Controls: Show(MenuPage.Controls); break;
                        case MainMenuModel.TitleRow.Quit: SceneFlow.Quit(); break;
                    }
                    break;
                case MenuPage.Options:
                    if (row == (int)MainMenuModel.OptionRow.Back) Back();
                    else Change(row, 1);
                    break;
                case MenuPage.Online: ActivateOnline((MainMenuModel.OnlineRow)row); break;
                case MenuPage.Hosting: ActivateHosting((MainMenuModel.HostRow)row); break;
                case MenuPage.Joining: ActivateJoining((MainMenuModel.JoinRow)row); break;
                default:
                    Back();
                    break;
            }
        }

        // Left/right on a row. On the options page it changes that row's value; on the
        // controls page it switches the device the sheet is drawn for. Silent: a wooden button
        // clicks by itself, and the keys play their click in Update.
        void Change(int row, int delta)
        {
            if (model.Leaving || delta == 0) return;
            if (model.Page == MenuPage.Controls)
            {
                model.SetControlsTab(delta < 0 ? MenuDevice.Keyboard : MenuDevice.Gamepad);
                return;
            }
            if (model.Page != MenuPage.Options || row == (int)MainMenuModel.OptionRow.Back) return;
            if (!MenuOptions.Change((MainMenuModel.OptionRow)row, delta)) return;
            if (model.ShowSelection) model.Select(row);
            model.Touch();
        }

        void Back()
        {
            if (model.Page == MenuPage.Options) Show(MenuPage.Title, (int)MainMenuModel.TitleRow.Options);
            else if (model.Page == MenuPage.Controls) Show(MenuPage.Title, (int)MainMenuModel.TitleRow.Controls);
            else if (model.Page == MenuPage.Online) Show(MenuPage.Title, (int)MainMenuModel.TitleRow.Online);
            else if (model.Page == MenuPage.Hosting)
            {
                NetSession.Cancel();
                Show(MenuPage.Online, (int)MainMenuModel.OnlineRow.Host);
            }
            else if (model.Page == MenuPage.Joining)
            {
                BlurField();
                NetSession.Cancel();
                Show(MenuPage.Online, (int)MainMenuModel.OnlineRow.Join);
            }
            else if (model.Selected != (int)MainMenuModel.TitleRow.Quit) model.Select((int)MainMenuModel.TitleRow.Quit);
        }

        public void Show(MenuPage page, int select = 0)
        {
            UiAudio.Open();
            nav.Reset();
            model.ShowPage(page, select);
        }

        // Starts the game with 1 or 2 players: the loading screen (SceneFlow's LoadingStarted)
        // covers the menu and survives into the house.
        public void StartGame(int players)
        {
            if (model.Leaving || SceneFlow.IsLoading) return;
            model.Leave();
            SceneFlow.LoadGame(players);
        }

        // ---- online (NETCODE_SLICE 3.3) ----

        void ActivateOnline(MainMenuModel.OnlineRow row)
        {
            switch (row)
            {
                case MainMenuModel.OnlineRow.Host:
                    NetSession.HostRelay();
                    Show(MenuPage.Hosting);
                    break;
                case MainMenuModel.OnlineRow.HostDirect:
                    if (!Debug.isDebugBuild) return;
                    NetSession.HostDirect();
                    Show(MenuPage.Hosting);
                    break;
                case MainMenuModel.OnlineRow.Join:
                    // With the keys, the field takes them at once; a pad starts on "Coller".
                    bool keys = model.Device == MenuDevice.Keyboard;
                    Show(MenuPage.Joining, (int)(keys ? MainMenuModel.JoinRow.Connect : MainMenuModel.JoinRow.Paste));
                    focusFieldPending = keys;
                    break;
                default: Back(); break;
            }
        }

        void ActivateHosting(MainMenuModel.HostRow row)
        {
            switch (row)
            {
                case MainMenuModel.HostRow.Copy:
                    if (string.IsNullOrEmpty(model.JoinCode)) return;
                    GUIUtility.systemCopyBuffer = model.JoinCode;
                    model.SetCopied();
                    break;
                case MainMenuModel.HostRow.Start:
                    if (Net.PeerConnected) NetSession.StartGame();
                    break;
                default: Back(); break;
            }
        }

        void ActivateJoining(MainMenuModel.JoinRow row)
        {
            switch (row)
            {
                case MainMenuModel.JoinRow.Field: model.CodeFocus.RequestFocus(); break;
                case MainMenuModel.JoinRow.Paste:
                    string clip = GUIUtility.systemCopyBuffer;
                    if (!string.IsNullOrEmpty(clip)) model.Code.Value = clip.Trim();
                    break;
                case MainMenuModel.JoinRow.Connect: Connect(); break;
                default: Back(); break;
            }
        }

        // "Se connecter", or Enter in the field. Ignored while an attempt is on its way.
        void Connect()
        {
            if (model.Leaving || model.Page != MenuPage.Joining) return;
            var s = NetSession.Status;
            if (s == NetStatus.Connecting || s == NetStatus.Lobby || s == NetStatus.Loading) return;
            string code = model.Code.Value;
            if (string.IsNullOrWhiteSpace(code)) return;
            NetSession.Join(code);
        }

        // Reads NetSession's static state (never creates a session). Offline it stays Idle and
        // changes nothing. The load itself is NetSession's: here the menu only stops answering.
        void PollNet()
        {
            NetStatus status = NetSession.Status;
            model.SetNet(status, NetSession.Error, NetSession.JoinCode, Net.PeerConnected);
            if (status == NetStatus.Loading && !model.Leaving) model.Leave();
            if (model.Page == MenuPage.Title)
            {
                string message = NetSession.TakeMenuMessage();
                if (message != null) model.SetMessage(message);
            }
        }

        void BlurField()
        {
            var focus = container != null && container.panel != null ? container.panel.focusController : null;
            if (focus != null && focus.focusedElement is VisualElement f) f.Blur();
        }

        void OnSettingsChanged()
        {
            // The language may have changed: every word is rebuilt.
            MenuText.Ensure();
            model.Touch();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = null;
        }
    }
}
