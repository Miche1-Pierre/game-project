using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;
using Container = LumaFlow.Container;

namespace Movers
{
    // The contract, top left: a cardboard box lid with packing tape, the clock on a little
    // wooden tag, each thing on the list with where it is, then what is loaded, what handing
    // the truck over now would pay, and what the crew has of hers so far.
    public static class ContractBoardView
    {
        public const float Width = 310f;
        static readonly UiPlace Place = UiPlace.TopLeft(18f);

        public static Widget Build(SharedHudModel m) =>
            UiKit.With(Place, new ReactiveBuilder<int>(m.ContractVersion, _ => Board(m)));

        static Widget Board(SharedHudModel m)
        {
            if (!m.HasContract) return UiKit.Empty;
            var th = UiKit.Theme;
            var tx = th.Text;

            var rows = new List<Widget>(m.RowCount);
            for (int i = 0; i < m.RowCount; i++) rows.Add(Row(m.Rows[i], m.RowLocation[i], m.RowCondition[i], th));

            Widget header = new Row(new[]
            {
                UiKit.Icon(UiSprites.IconBox, 30f),
                new Expanded(UiKit.Label(Loc.T("contract.title").ToUpperInvariant(), tx.LabelBold, false)),
                ClockTag(m),
            }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center);

            Widget footer = new Column(new Widget[]
            {
                new Row(new[]
                {
                    UiKit.Icon(UiSprites.IconTruck, 22f, th.woodDark),
                    new Expanded(UiKit.Bound(m.Loaded, tx.SmallBold)),
                    m.DestroyedCount > 0
                        ? UiKit.Chip(Loc.F("contract.destroyed", m.DestroyedCount), th.bad)
                        : UiKit.Empty,
                }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center),
                new Row(new[]
                {
                    UiKit.Icon(UiSprites.IconMoney, 24f, th.goodInk),
                    new Expanded(UiKit.Label(Loc.T(Session.IsOver ? "contract.payoutFinal" : "contract.payout"), tx.Small, false)),
                    new ReactiveBuilder<bool>(m.MoneyNegative, neg => UiKit.Bound(m.Money, tx.LabelBold, neg ? th.badInk : th.goodInk)),
                }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center),
                new ReactiveBuilder<string>(m.Stolen, s => string.IsNullOrEmpty(s) ? UiKit.Empty : new Row(new[]
                {
                    UiKit.Icon(UiSprites.IconPocket, 22f, th.warnInk),
                    UiKit.Label(Loc.T("contract.stolen"), tx.SmallBold, th.warnInk, false),
                    new Expanded(UiKit.Label(s, tx.Small)),
                }, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Center)),
            }, 4f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch);

            Widget body = new Column(new[]
            {
                header,
                Divider(th),
                new Column(rows, 2f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch),
                Divider(th),
                footer,
            }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch);

            return new SizedBox(UiKit.Card(body, 8f), Width);
        }

        static Widget Row(MovableObject item, ItemLocation loc, ItemCondition cond, UiTheme th)
        {
            var tx = th.Text;
            string icon;
            Color color;
            string word;
            if (cond == ItemCondition.Destroyed) { icon = UiSprites.IconCross; color = th.badInk; word = Loc.T("state.destroyed"); }
            else
            {
                switch (loc)
                {
                    case ItemLocation.Loaded: icon = UiSprites.IconTruck; color = th.goodInk; word = Loc.T("state.loaded"); break;
                    case ItemLocation.Delivered: icon = UiSprites.IconCheck; color = th.infoInk; word = Loc.T("state.delivered"); break;
                    case ItemLocation.Pocketed: icon = UiSprites.IconPocket; color = th.warnInk; word = Loc.T("state.pocketed"); break;
                    case ItemLocation.Worn: icon = UiSprites.IconPocket; color = th.warnInk; word = Loc.T("state.worn"); break;
                    default: icon = UiSprites.IconBox; color = th.inkSoft; word = Loc.T("state.missing"); break;
                }
            }
            Widget state = cond == ItemCondition.Damaged && cond != ItemCondition.Destroyed
                ? new Row(new[] { UiKit.Label(word, tx.CaptionBold, color, false), UiKit.Chip(Loc.T("state.damaged"), th.warn) }, 4f, MainAxisAlignment.End, CrossAxisAlignment.Center)
                : UiKit.Label(word, tx.CaptionBold, color, false);
            return new Row(new[]
            {
                UiKit.Icon(icon, 18f, color),
                new Expanded(UiKit.Label(Loc.Item(item != null ? item.displayName : null), tx.Small, false, 1)),
                state,
            }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        // The clock on a dark wooden tag; it pulses in the last minute and when the police come.
        static Widget ClockTag(SharedHudModel m) => new ReactiveBuilder<int>(m.ClockMode, k =>
        {
            var kind = (ClockKind)k;
            if (kind == ClockKind.None) return UiKit.Empty;
            var th = UiKit.Theme;
            Color c = kind == ClockKind.Low || kind == ClockKind.Police ? UiTheme.Hex(0xFF8A6A)
                    : kind == ClockKind.KeysFirst ? th.creamSoft : th.cream;
            UiProbe motion = kind == ClockKind.Low || kind == ClockKind.Police ? UiMotion.PulseFast : null;
            Widget content = new Row(new[]
            {
                UiKit.Icon(UiSprites.IconClock, 20f, c),
                UiKit.Bound(m.Clock, kind == ClockKind.KeysFirst ? th.Text.CaptionBold : th.Text.LabelBold, c),
            }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return UiKit.Skinned(th.Skins.WoodDark, content, EdgeInsets.Symmetric(10f, 4f), motion);
        });

        static Widget Divider(UiTheme th)
        {
            Color c = th.ink;
            c.a = 0.25f;
            return new SizedBox(new Container(UiKit.Empty, new BoxDecoration(backgroundColor: c, borderRadius: BorderRadius.All(1f))), null, 2f);
        }
    }
}
