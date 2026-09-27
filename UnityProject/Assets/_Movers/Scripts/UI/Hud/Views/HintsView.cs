using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // The crosshair, and just below and right of it a paper tag with what the player can do
    // right now: a card naming the object (its worth, its weight, whose it is), then up to
    // four "[key] verb" rows. Next to the aim, because that is where the eyes are; small,
    // because it must not hide what you aim at.
    public static class HintsView
    {
        public const float MaxWidth = 340f;
        public const float KeySize = 30f;

        static readonly UiPlace CrossPlace = UiPlace.Center();
        static readonly UiPlace HintPlace = new UiPlace(50f, 50f, 0f, 0f, 30f, 26f);
        public const string PanelName = "hud-hints";
        static readonly UiName HintName = new UiName(PanelName);
        static UiSkin dot;

        public static Widget Crosshair(PlayerHudModel p) =>
            UiKit.With(CrossPlace, new ReactiveBuilder<bool>(p.Crosshair, on => on ? Dot() : UiKit.Empty));

        static Widget Dot()
        {
            var th = UiKit.Theme;
            if (th.HasSprite(UiSprites.CrosshairDot)) return UiKit.Icon(UiSprites.CrosshairDot, 12f);
            if (dot == null) dot = new UiSkin(null, th.cream, th.shadow, 1.5f, 5f);
            return new SizedBox(UiKit.Skinned(dot, UiKit.Empty), 7f, 7f);
        }

        public static Widget Hints(PlayerHudModel p) =>
            UiKit.With(HintPlace, HintName, new ReactiveBuilder<HintSet>(p.Hints, set => Panel(set)));

        static Widget Panel(in HintSet set)
        {
            if (set.IsEmpty) return UiKit.Empty;
            var th = UiKit.Theme;
            bool full = GameSettings.HintsShown;
            var parts = new List<Widget>(HintSet.Max + 1);
            if (!set.card.IsEmpty && full) parts.Add(Card(set.card, th));
            int rows = full ? set.Count : Mathf.Min(1, set.Count);
            for (int i = 0; i < rows; i++) parts.Add(UiKit.HintRow(set[i], KeySize, th.Text.BodyBold));
            if (parts.Count == 0) return UiKit.Empty;
            Widget column = new Column(parts, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
            return ConstrainedBox.AtMost(UiKit.Tag(column, 4f), width: MaxWidth);
        }

        // "Porcelain Vase   $180   2 kg   Hers" with the flags that matter to a thief.
        static Widget Card(in TargetCard card, UiTheme th)
        {
            var tx = th.Text;
            MovableObject mo = card.item;
            if (mo == null) return UiKit.Empty;
            var chips = new List<Widget>(4);
            if (mo.contractValue > 0) chips.Add(UiKit.Chip(Loc.Money(mo.contractValue), th.goodInk));
            chips.Add(UiKit.Chip(Loc.F("card.kg", Mathf.Max(1, Mathf.RoundToInt(mo.weight))), th.woodDark));
            if (mo.requiredForContract) chips.Add(UiKit.Chip(Loc.T("card.onList"), th.infoInk));
            else if (mo.IsTheftTarget) chips.Add(UiKit.Chip(Loc.T("card.hers"), th.warnInk));
            else if (!mo.ownedByGrandma) chips.Add(UiKit.Chip(Loc.T("card.crew"), th.woodDark));
            if (mo.fragile) chips.Add(UiKit.Chip(Loc.T("card.fragile"), th.badInk));
            if (card.heldBy >= 0) chips.Add(UiKit.Chip(Loc.F("card.heldBy", ToastFeed.Who(card.heldBy)), th.inkSoft));

            Widget name = new Row(new[]
            {
                UiKit.Icon(UiKit.IconOf(mo), 24f, th.woodDark),
                UiKit.Label(Loc.Item(mo.displayName), tx.LabelBold, false, 1),
            }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return new Column(new[] { name, new Row(chips, 4f, MainAxisAlignment.Start, CrossAxisAlignment.Center) },
                              4f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
        }
    }
}
