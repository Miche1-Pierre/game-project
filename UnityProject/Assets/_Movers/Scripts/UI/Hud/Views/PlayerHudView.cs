using LumaFlow;

namespace Movers
{
    // One player's whole HUD, laid over their viewport. Every region is its own reactive
    // island, so a change in the pockets never rebuilds the contract:
    //
    //   top left      the contract, once the     top right   one column: the grandmother,
    //                 grandmother handed it over             what the player looks at or holds
    //   top centre    the banner                             (the card and the key hints), then
    //   centre        crosshair, one key prompt              the toasts
    //   bottom left   controls and pause keys    bottom right pockets (and how tipsy)
    //   bottom centre the truck, while driving   in the world her words, "deliver here"
    //   over it all   the controls sheet, the pause menu
    public static class PlayerHudView
    {
        static readonly UiPlace RightPlace = UiPlace.TopRight(18f);

        public static Widget Build(SharedHudModel shared, PlayerHudModel p) =>
            new Theme(UiKit.Theme.Luma, SizedBox.Expand(new Stack(new[]
            {
                WorldLabelsView.Deliver(p),
                WorldLabelsView.Speech(shared, p),
                ContractBoardView.Build(shared),
                MessagesView.Banner(shared, p),
                RightColumn(shared, p),
                HintsView.Crosshair(p),
                HintsView.Prompt(p),
                DriveView.Build(p),
                PocketsView.Build(p),
                CornerHintsView.Build(p),
                HudControlsSheet.Overlay(p),
                PauseView.Build(p),
            })));

        // Each part rebuilds alone; the column only moves the parts under one that grows. The
        // key hints and the toasts carry their own gap above them, so an empty part leaves none.
        static Widget RightColumn(SharedHudModel shared, PlayerHudModel p) =>
            UiKit.With(RightPlace, new Column(new[]
            {
                GrandmaView.Build(shared, p),
                HintsView.Details(p),
                MessagesView.Toasts(shared),
            }, 0f, MainAxisAlignment.Start, CrossAxisAlignment.End));
    }

    // Over every view: the intro card and the end screen.
    public static class ScreenHudView
    {
        public static Widget Build(SharedHudModel shared) =>
            new Theme(UiKit.Theme.Luma, SizedBox.Expand(CardsView.Build(shared)));
    }
}
