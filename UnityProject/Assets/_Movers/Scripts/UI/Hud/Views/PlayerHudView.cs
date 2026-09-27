using LumaFlow;

namespace Movers
{
    // One player's whole HUD, laid over their viewport. Every region is its own reactive
    // island, so a change in the pockets never rebuilds the contract:
    //
    //   top left      the contract              top right   the grandmother, then the toasts
    //   top centre    the banner                centre      crosshair and key hints
    //   bottom left   controls and pause keys   bottom right pockets (and how tipsy)
    //   bottom centre the truck, while driving  in the world her words, "deliver here"
    //   over it all   the controls sheet, the pause menu
    public static class PlayerHudView
    {
        public static Widget Build(SharedHudModel shared, PlayerHudModel p) =>
            new Theme(UiKit.Theme.Luma, SizedBox.Expand(new Stack(new[]
            {
                WorldLabelsView.Deliver(p),
                WorldLabelsView.Speech(shared, p),
                ContractBoardView.Build(shared),
                GrandmaView.Build(shared, p),
                MessagesView.Toasts(shared),
                MessagesView.Banner(shared, p),
                HintsView.Crosshair(p),
                HintsView.Hints(p),
                DriveView.Build(p),
                PocketsView.Build(p),
                CornerHintsView.Build(p),
                HudControlsSheet.Overlay(p),
                PauseView.Build(p),
            })));
    }

    // Over every view: the intro card and the end screen.
    public static class ScreenHudView
    {
        public static Widget Build(SharedHudModel shared) =>
            new Theme(UiKit.Theme.Luma, SizedBox.Expand(CardsView.Build(shared)));
    }
}
