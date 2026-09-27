using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // What the player can do right now, in two places (Pierre, feedback 1: the panel next to
    // the crosshair sat on the object all the time):
    //   centre      the crosshair and, just under it, one small tag with the first action
    //               ("[E] Ouvrir"): the eyes are there, so the main key stays there.
    //   top right   under the grandmother's panel (PlayerHudView's right column): a paper tag
    //               with the card naming the object (its worth, its weight, whose it is) and
    //               the other "[key] verb" rows. Only with the key hints on (options).
    // Without a crosshair (at the wheel) there is no centre tag: every row goes top right.
    public static class HintsView
    {
        public const float MaxWidth = 340f;
        public const float KeySize = 30f;
        public const float PromptKeySize = 28f;

        static readonly UiPlace CrossPlace = UiPlace.Center();
        // Centred under the crosshair, clear of the dot.
        static readonly UiPlace PromptPlace = new UiPlace(50f, 50f, 50f, 0f, 0f, 22f);
        public const string PanelName = "hud-hints";
        public const string PromptName = "hud-prompt";
        static readonly UiName HintName = new UiName(PanelName);
        static readonly UiName PromptProbe = new UiName(PromptName);
        static readonly EdgeInsets Below = new EdgeInsets(0f, 8f, 0f, 0f);
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

        // ---- centre: the one action ----

        public static Widget Prompt(PlayerHudModel p) =>
            UiKit.With(PromptPlace, PromptProbe, new ReactiveBuilder<bool>(p.Crosshair, on => on
                ? new ReactiveBuilder<HintSet>(p.Hints, set => PromptFor(set))
                : UiKit.Empty));

        static Widget PromptFor(in HintSet set)
        {
            if (set.Count == 0) return UiKit.Empty;
            return UiKit.Tag(UiKit.HintRow(set[0], PromptKeySize, UiKit.Theme.Text.BodyBold), 3f);
        }

        // ---- top right: the object and the other actions ----

        // Unplaced: PlayerHudView's right column puts it under the grandmother.
        public static Widget Details(PlayerHudModel p) =>
            UiKit.With(HintName, new ReactiveBuilder<bool>(p.Crosshair, centre =>
                new ReactiveBuilder<HintSet>(p.Hints, set => Panel(set, centre ? 1 : 0))));

        // first: the rows the centre does not show (it shows row 0 when there is a crosshair).
        static Widget Panel(in HintSet set, int first)
        {
            if (set.IsEmpty) return UiKit.Empty;
            var th = UiKit.Theme;
            bool full = GameSettings.HintsShown;
            var parts = new List<Widget>(HintSet.Max + 1);
            if (!set.card.IsEmpty && full) parts.Add(Card(set.card, th));
            // Key hints off: only the first action, and only when the centre does not show it.
            int end = full ? set.Count : Mathf.Min(1, set.Count);
            for (int i = first; i < end; i++) parts.Add(UiKit.HintRow(set[i], KeySize, th.Text.BodyBold));
            if (parts.Count == 0) return UiKit.Empty;
            Widget column = new Column(parts, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
            return new Padding(ConstrainedBox.AtMost(UiKit.Tag(column, 4f), width: MaxWidth), Below);
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
