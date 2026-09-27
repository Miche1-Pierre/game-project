using LumaFlow;

namespace Movers
{
    // Things pinned to the world in this player's view: the grandmother's speech bubble over
    // her head, and "Deliver here" over the truck's board once everything is loaded. Their
    // anchors are moved every frame by HudRoot (no rebuild); the content rebuilds only when
    // what she says changes.
    public static class WorldLabelsView
    {
        public const float BubbleWidth = 380f;

        public static Widget Speech(SharedHudModel m, PlayerHudModel p) =>
            new Stack(new[]
            {
                p.SpeechAnchor.Widget,
                new ReactiveBuilder<string>(m.Speech, s => string.IsNullOrEmpty(s) ? UiKit.Empty : UiKit.Speech(s, BubbleWidth)),
            });

        public static Widget Deliver(PlayerHudModel p)
        {
            var th = UiKit.Theme;
            Widget sign = UiKit.Sign(Loc.T("deliver.here"), th.Text.LabelBold, UiMotion.PulseSlow);
            return new Stack(new[] { p.DeliverAnchor.Widget, sign });
        }
    }
}
