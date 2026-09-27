using LumaFlow;

namespace Movers
{
    // Bottom left, always: the two keys a lost player needs, the controls sheet and pause, in
    // this player's device. Small and dim: it is a reminder, not a panel.
    public static class CornerHintsView
    {
        static readonly UiPlace Place = UiPlace.BottomLeft(18f);

        public static Widget Build(PlayerHudModel p) =>
            UiKit.With(Place, new ReactiveBuilder<int>(p.Device, d => d == 2 ? UiKit.Empty : Plate(p)));

        static Widget Plate(PlayerHudModel p)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            var src = p.Source;
            Widget row = new Row(new[]
            {
                UiKit.Key(InputGlyphs.ControlsToggle(src), 26f),
                UiKit.Label(Loc.T("hud.controls"), tx.SmallBold, false),
                UiKit.Gap(6f),
                UiKit.Key(InputGlyphs.For(src, CrewButton.Pause), 26f),
                UiKit.Label(Loc.T("hud.pause"), tx.SmallBold, false),
            }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return new Opacity(UiKit.Skinned(th.Skins.Chip, row, EdgeInsets.Symmetric(10f, 5f)), 0.85f);
        }
    }
}
