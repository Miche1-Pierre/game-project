using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;
using Container = LumaFlow.Container;

namespace Movers
{
    // One player's whole HUD, laid over their viewport. Every region is its own reactive
    // island, so a change in the pockets never rebuilds the contract:
    //
    //   top left      the contract, once the     top right   one column: the grandmother,
    //                 grandmother handed it over             what the player looks at or holds
    //   top centre    the banner                             (the card and the key hints), then
    //   centre        crosshair, one key prompt,             the toasts
    //                 the CAUGHT ring
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
                Caught(p),
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

        // ---- centre: about to be arrested ----

        const float CaughtRingSize = 150f;
        // The ring's centre on the crosshair: the column hangs from the ring's top edge.
        static readonly UiPlace CaughtPlace = new UiPlace(50f, 50f, 50f, 0f, 0f, -CaughtRingSize * 0.5f);

        static Widget Caught(PlayerHudModel p) =>
            UiKit.With(CaughtPlace, new ReactiveBuilder<bool>(p.Caught, on => on ? CaughtRing(p) : UiKit.Empty));

        // A red ring round the crosshair that closes in (PlayerHudModel.CaughtScale), and the word.
        static Widget CaughtRing(PlayerHudModel p)
        {
            var th = UiKit.Theme;
            Color inside = th.bad;
            inside.a = 0.12f;
            Widget ring = new SizedBox(new Container(UiKit.Empty, new BoxDecoration(
                backgroundColor: inside, borderRadius: BorderRadius.All(CaughtRingSize * 0.5f), border: Border.All(th.bad, 6f))),
                CaughtRingSize, CaughtRingSize);
            Widget word = UiKit.With(UiMotion.PulseFast, UiKit.Chip(Loc.T("hud.caught"), th.bad));
            return new Column(new[] { UiKit.With(p.CaughtScale, ring), word }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }
    }

    // Scales the element that contains it about its centre, set from a model each frame like
    // UiFill's width: a shrinking ring moves without a rebuild.
    public sealed class UiScale : UiProbe
    {
        UnityEngine.UIElements.VisualElement element;
        float value = 1f, shown = -1f;

        protected override void Attached(UnityEngine.UIElements.VisualElement target)
        {
            element = target;
            shown = -1f;
            Write();
        }

        protected override void Detached(UnityEngine.UIElements.VisualElement target)
        {
            if (element == target) element = null;
        }

        public void Set(float scale)
        {
            value = scale;
            Write();
        }

        void Write()
        {
            if (element == null || Mathf.Abs(value - shown) < 0.002f) return;
            shown = value;
            element.style.scale = new UnityEngine.UIElements.Scale(new Vector3(value, value, 1f));
        }
    }

    // Over every view: the intro card and the end screen.
    public static class ScreenHudView
    {
        public static Widget Build(SharedHudModel shared) =>
            new Theme(UiKit.Theme.Luma, SizedBox.Expand(CardsView.Build(shared)));
    }
}
