using System;
using LumaFlow;
using UnityEngine;

namespace Movers
{
    // A wooden button: planed board at rest, lighter with a mustard edge under the mouse or
    // when a gamepad has it selected, pushed in while pressed, greyed when disabled. It plays
    // the UI sounds itself (UiAudio.Hover on the way in, Click on press), so every button of
    // the game sounds the same without each screen remembering to.
    //
    // A LumaFlow Pressable with a states builder: it rebuilds its face only when the hover,
    // press or focus state changes.
    public sealed class WoodButton : StatefulWidget<WoodButton.State>
    {
        public readonly string label;
        public readonly Action onPressed;
        public readonly bool selected;
        public readonly bool enabled;
        public readonly Glyph glyph;
        public readonly float minWidth;
        // A small key-cap button (the options' arrows): the plank's thick ends would leave no
        // room for a one-letter label.
        public readonly bool compact;

        public WoodButton(string label, Action onPressed, bool selected, bool enabled, Glyph glyph, float minWidth, bool compact = false)
        {
            this.compact = compact;
            this.label = label ?? "";
            this.onPressed = onPressed;
            this.selected = selected;
            this.enabled = enabled;
            this.glyph = glyph;
            this.minWidth = minWidth;
        }

        public sealed class State : WidgetState
        {
            WidgetStates last;
            Func<WidgetStates, Widget> face;
            Action press;

            WoodButton Config => (WoodButton)Widget;

            protected override void InitState()
            {
                face = Face;
                press = Press;
            }

            public override Widget Build(BuildContext context) =>
                new Pressable(face, press, Config.enabled, semanticsLabel: Config.label);

            void Press()
            {
                var c = Config;
                if (!c.enabled) return;
                UiAudio.Click();
                c.onPressed?.Invoke();
            }

            Widget Face(WidgetStates states)
            {
                bool hovered = (states & WidgetStates.Hovered) != 0;
                if (hovered && (last & WidgetStates.Hovered) == 0 && Config.enabled) UiAudio.Hover();
                last = states;

                var c = Config;
                var skins = UiTheme.Current.Skins;
                var tx = UiTheme.Current.Text;
                if (c.compact) return Compact(c, states, hovered);
                UiSkin skin = !c.enabled ? skins.ButtonDisabled
                    : (states & WidgetStates.Pressed) != 0 ? skins.ButtonPressed
                    : hovered || c.selected || (states & WidgetStates.Focused) != 0 ? skins.ButtonHover
                    : skins.ButtonNormal;

                Widget text = UiKit.Label(c.label, tx.LabelBold, false);
                Widget content = c.glyph.IsNone
                    ? new Center(text)
                    : new Row(new[] { UiKit.Key(c.glyph, 28f), text }, 10f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
                return ConstrainedBox.AtLeast(UiKit.Skinned(skin, content, skin.Padding(10f)), width: c.minWidth);
            }

            static Widget Compact(WoodButton c, WidgetStates states, bool hovered)
            {
                var th = UiTheme.Current;
                bool pressed = (states & WidgetStates.Pressed) != 0;
                var style = th.Text.Sized(th.textLabel, true);
                Widget cap = UiKit.KeyFace(th.Skins.KeyCap, c.label, 30f, style);
                // Under the mouse the cap gives a small bounce, so it is clear what a click presses.
                return hovered && !pressed ? UiKit.With(UiMotion.Pop, cap) : cap;
            }
        }
    }
}
