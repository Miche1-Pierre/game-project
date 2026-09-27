using System;
using UnityEngine.UIElements;
using LumaNative = LumaFlow.Native;
using LumaWidget = LumaFlow.Widget;

namespace Movers
{
    // The one bridge between LumaFlow widgets and the UI Toolkit styles LumaFlow does not
    // expose (a 9-slice sprite background, a font face, a text shadow, a transform).
    //
    // A probe is an invisible, zero-size native element placed as the first child of a
    // LumaFlow Stack. When it reaches a panel it styles its parent, the Stack's own element,
    // and everything under that Stack inherits the font, the colour and the shadow. LumaFlow
    // never resets those properties on a Stack, so they hold across rebuilds.
    //
    // A probe object is created once and reused: its factory delegate is then equal from one
    // build to the next, LumaFlow keeps the same native element, and a rebuild costs nothing
    // here. Only a different probe (another skin) makes a new element, which restyles.
    public abstract class UiProbe
    {
        readonly Func<VisualElement> factory;
        static readonly EventCallback<AttachToPanelEvent> OnAttach = e => Handle(e.target, true);
        static readonly EventCallback<DetachFromPanelEvent> OnDetach = e => Handle(e.target, false);

        protected UiProbe() { factory = Create; }

        // The LumaFlow widget to put first in a Stack.
        public LumaWidget Widget => new LumaNative(factory);

        VisualElement Create()
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore, focusable = false, userData = this };
            e.style.position = Position.Absolute;
            e.style.width = 0f;
            e.style.height = 0f;
            e.RegisterCallback(OnAttach);
            e.RegisterCallback(OnDetach);
            return e;
        }

        static void Handle(IEventHandler target, bool attached)
        {
            if (!(target is VisualElement e) || !(e.userData is UiProbe probe) || e.parent == null) return;
            if (attached) probe.Attached(e.parent);
            else probe.Detached(e.parent);
        }

        // The parent (the styled element) arrived on a panel.
        protected abstract void Attached(VisualElement target);

        // It left the panel. Styles stay: another probe may already have restyled the parent.
        protected virtual void Detached(VisualElement target) { }
    }
}
