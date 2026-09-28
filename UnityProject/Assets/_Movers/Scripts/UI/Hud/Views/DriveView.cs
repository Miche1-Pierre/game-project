using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;
using Container = LumaFlow.Container;

namespace Movers
{
    // At the wheel, bottom centre: the speed in big friendly digits and the load of the truck
    // as a tape measure against its capacity (red when overloaded). The driving keys are the
    // hint rows' (VerbHints knows the seat), so they stay where the eyes already look. Only
    // the driver sees it (PlayerHudModel.Driving reads TruckVehicle.IsAtWheel), not the passenger.
    public static class DriveView
    {
        static readonly UiPlace Place = UiPlace.BottomCenter(22f);

        public static Widget Build(PlayerHudModel p) =>
            UiKit.With(Place, new ReactiveBuilder<bool>(p.Driving, on => on ? Plate(p) : UiKit.Empty));

        static Widget Plate(PlayerHudModel p)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            Widget speed = new Column(new[]
            {
                UiKit.Bound(p.Speed, tx.Sized(44f, true)),
                UiKit.Label(Loc.T("drive.kmh"), tx.CaptionBold, th.creamSoft, false),
            }, 0f, MainAxisAlignment.Center, CrossAxisAlignment.Center);

            Widget cargo = new Column(new[]
            {
                new Row(new[]
                {
                    UiKit.Icon(UiSprites.IconTruck, 22f, th.accent),
                    UiKit.Label(Loc.T("drive.cargo"), tx.SmallBold, false),
                    new ReactiveBuilder<bool>(p.Overloaded, over => over ? UiKit.Chip(Loc.T("drive.over"), th.bad) : UiKit.Empty),
                }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center),
                new ReactiveBuilder<bool>(p.Overloaded, over => UiKit.TapeBar(p.CargoFill, over ? th.bad : th.tapeYellow, 230f, over)),
                UiKit.Bound(p.Cargo, tx.Small, th.creamSoft),
            }, 4f, MainAxisAlignment.Start, CrossAxisAlignment.Start);

            Color line = th.cream;
            line.a = 0.3f;
            Widget divider = new SizedBox(new Container(UiKit.Empty, new BoxDecoration(backgroundColor: line)), 2f, 56f);
            Widget row = new Row(new[] { new SizedBox(speed, 86f), divider, cargo }, 14f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return UiKit.Panel(row, th.Skins.WoodDark, 8f, UiMotion.Drop);
        }
    }
}
