using UnityEngine;
using UnityEngine.UIElements;

namespace Movers
{
    // The UI Toolkit panels of the title screen and the loading screen. Each gets its own
    // PanelSettings, made at runtime with the HUD's settings (HudPanel.Configure: scaled with
    // the screen's height against 1080 lines, the empty theme), only its sorting order differs:
    //   the title screen   0     (there is no HUD in the menu scene)
    //   the loading screen 1000  (over the HUD at 10 and the indicators at -5)
    // A panel of its own also means the HUD's navigation block and this one never meet.
    public static class MenuPanel
    {
        public const int MenuOrder = 0;
        public const int LoadingOrder = 1000;

        public static PanelSettings Create(string name, int sortingOrder)
        {
            var p = ScriptableObject.CreateInstance<PanelSettings>();
            p.name = name;
            p.hideFlags = HideFlags.DontSave;
            HudPanel.Configure(p);
            p.sortingOrder = sortingOrder;
            var theme = Resources.Load<ThemeStyleSheet>(HudPanel.ThemeResource);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                theme.hideFlags = HideFlags.DontSave;
            }
            p.themeStyleSheet = theme;
            return p;
        }
    }

    // Switches UI Toolkit's own keyboard and pad navigation off on one panel, the way HudRoot
    // does for the HUD: with the legacy Input Manager its move and submit read every key and
    // every pad, so a button would be pressed twice (once by MenuNav, once by the focus) or by
    // the wrong pad. Pointer events are not navigation: the mouse still clicks.
    public sealed class NavigationBlock
    {
        VisualElement tree;
        readonly EventCallback<NavigationMoveEvent> onMove;
        readonly EventCallback<NavigationSubmitEvent> onSubmit;
        readonly EventCallback<NavigationCancelEvent> onCancel;

        public NavigationBlock()
        {
            onMove = e => Swallow(e);
            onSubmit = e => Swallow(e);
            onCancel = e => Swallow(e);
        }

        public bool Active => tree != null;

        // Safe to call every frame until the panel exists.
        public void Attach(VisualElement anyElementOnThePanel)
        {
            if (tree != null || anyElementOnThePanel == null || anyElementOnThePanel.panel == null) return;
            tree = anyElementOnThePanel.panel.visualTree;
            tree.RegisterCallback(onMove, TrickleDown.TrickleDown);
            tree.RegisterCallback(onSubmit, TrickleDown.TrickleDown);
            tree.RegisterCallback(onCancel, TrickleDown.TrickleDown);
        }

        public void Detach()
        {
            if (tree == null) return;
            tree.UnregisterCallback(onMove, TrickleDown.TrickleDown);
            tree.UnregisterCallback(onSubmit, TrickleDown.TrickleDown);
            tree.UnregisterCallback(onCancel, TrickleDown.TrickleDown);
            tree = null;
        }

        void Swallow(EventBase e)
        {
            var focus = tree != null && tree.panel != null ? tree.panel.focusController : null;
            if (focus != null) focus.IgnoreEvent(e);
            e.StopImmediatePropagation();
        }

        // A clicked button keeps UI Toolkit's focus, and WoodButton draws a focused button lit:
        // dropped once the mouse is released (dropping it during the click would cut the press).
        public void DropStrayFocus()
        {
            if (tree == null || Input.GetMouseButton(0) || Input.GetMouseButton(1)) return;
            var focus = tree.panel != null ? tree.panel.focusController : null;
            if (focus != null && focus.focusedElement is VisualElement f) f.Blur();
        }
    }
}
