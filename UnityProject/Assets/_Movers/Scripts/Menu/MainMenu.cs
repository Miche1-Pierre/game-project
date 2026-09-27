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
        Vector3 lastMouse;
        float nextPadCheck;

        void Awake()
        {
            MenuText.Ensure();
            document = GetComponent<UIDocument>();
            // A panel of its own, made here: nothing to set up in the scene but the document.
            if (document.panelSettings == null)
            {
                panel = MenuPanel.Create("MoversMenuPanel (runtime)", MenuPanel.MenuOrder);
                document.panelSettings = panel;
            }
            onActivate = Activate;
            onChange = Change;
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
                mount = Framework.Mount(MainMenuView.Build(model, onActivate, onChange), container);
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
            block.DropStrayFocus();
            if (model.Leaving) return;

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
            if (nav.Horizontal != 0 && model.Page != MenuPage.Title)
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
                        case MainMenuModel.TitleRow.Options: Show(MenuPage.Options); break;
                        case MainMenuModel.TitleRow.Controls: Show(MenuPage.Controls); break;
                        case MainMenuModel.TitleRow.Quit: SceneFlow.Quit(); break;
                    }
                    break;
                case MenuPage.Options:
                    if (row == (int)MainMenuModel.OptionRow.Back) Back();
                    else Change(row, 1);
                    break;
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
