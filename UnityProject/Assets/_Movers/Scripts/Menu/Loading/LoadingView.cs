using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // What the loading screen shows, from UICORE's kit, on warm paper: a wooden sign saying
    // where the truck is going, the running mover in a wooden frame, the tape measure pulled
    // out as the scene loads, who plays on what, and a tip on a luggage label that changes
    // every few seconds.
    //
    // Built once; the tape (UiFill) and the percentage (a State<string>) move without a
    // rebuild, and only the tip's own branch rebuilds when the tip changes.
    public static class LoadingView
    {
        public const float WindowWidth = 900f;
        public const float WindowHeight = 450f;

        public sealed class Model
        {
            public readonly State<int> Tip = new State<int>(0);
            public readonly State<string> Percent = new State<string>("0 %");
            public readonly UiFill Bar = new UiFill();
            public Texture window;
            public bool toGame = true;
            public int players = 2;
            public bool padConnected;
            public bool online;
            public int localMember = -1;   // online: the player on this machine
        }

        static readonly UiPlace CenterPlace = UiPlace.Center(0f, 0f);
        static readonly UiName RootName = new UiName("loading-screen");
        static readonly UiTransform SignTilt = new UiTransform(-1.5f);
        static readonly WidgetKey[] TipKeys = MakeKeys(MenuText.Tips.Length);

        static WidgetKey[] MakeKeys(int n)
        {
            var k = new WidgetKey[n];
            for (int i = 0; i < n; i++) k[i] = new WidgetKey("tip" + i);
            return k;
        }

        public static Widget Build(Model m)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            var sk = th.Skins;

            Widget sign = UiKit.With(SignTilt, UiKit.TitleSign(Loc.T(m.toGame ? "load.toHouse" : "load.toMenu"), tx.Sized(38f, true), UiMotion.Drop));
            Widget sub = UiKit.Label(Loc.T(m.toGame ? "load.toHouseSub" : "load.toMenuSub"), tx.LabelBold, th.inkSoft, false);

            Widget picture = m.window != null
                ? (Widget)new Image(m.window, WindowWidth, WindowHeight, ImageFit.Cover)
                : new SizedBox(UiKit.Empty, WindowWidth, WindowHeight);
            Widget window = UiKit.Skinned(sk.Wood, picture, sk.Wood.Padding(6f));

            Widget bar = new Row(new[]
            {
                UiKit.Icon(UiSprites.IconTruck, 40f),
                UiKit.TapeBar(m.Bar, th.accent, 640f),
                new SizedBox(UiKit.Bound(m.Percent, tx.LabelBold), 72f),
            }, 12f, MainAxisAlignment.Center, CrossAxisAlignment.Center);

            var parts = new List<Widget>(6) { sign, sub, UiKit.Gap(4f), window, UiKit.Gap(2f), bar };
            if (m.toGame) parts.Add(Crew(m));
            parts.Add(UiKit.Gap(4f));
            parts.Add(new ReactiveBuilder<int>(m.Tip, i => Tip(m, i)));

            Widget column = new Column(parts, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return new Stack(new[] { UiPlace.Fill.Widget, RootName.Widget, sk.Paper.Widget, UiKit.With(CenterPlace, column) });
        }

        // "[J1] Clavier et souris   [J2] Manette", or "[J2] pas de manette" in warning ink.
        static Widget Crew(Model m)
        {
            if (m.online) return OnlineCrew(m);
            var th = UiKit.Theme;
            var tx = th.Text;
            var parts = new List<Widget>(6)
            {
                UiKit.Chip(Loc.T("load.p1"), CrewColor(0)),
                UiKit.Label(Loc.T("menu.keyboard"), tx.SmallBold, th.ink, false),
            };
            if (m.players >= 2)
            {
                parts.Add(UiKit.Gap(20f));
                parts.Add(UiKit.Chip(Loc.T("load.p2"), CrewColor(1)));
                parts.Add(m.padConnected
                    ? UiKit.Label(Loc.T("menu.gamepad"), tx.SmallBold, th.ink, false)
                    : UiKit.Label(Loc.T("load.noPad"), tx.SmallBold, th.warnInk, false));
            }
            return new Row(parts, 8f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
        }

        // Online, one player per PC: "[Toi] Clavier et souris   [J2 en ligne]" on the host,
        // "[J1 en ligne]   [Toi] Clavier et souris" on the client.
        static Widget OnlineCrew(Model m)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            var parts = new List<Widget>(5);
            for (int i = 0; i < 2; i++)
            {
                if (i > 0) parts.Add(UiKit.Gap(20f));
                if (i == m.localMember)
                {
                    parts.Add(UiKit.Chip(Loc.T("load.you"), CrewColor(i)));
                    parts.Add(UiKit.Label(Loc.T("menu.keyboard"), tx.SmallBold, th.ink, false));
                }
                else parts.Add(UiKit.Chip(Loc.T(i == 0 ? "load.remote1" : "load.remote2"), CrewColor(i)));
            }
            return new Row(parts, 8f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
        }

        // The crew's colours (CrewSpawner's defaults: red P1, blue P2).
        static Color CrewColor(int i) => i == 0 ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.2f, 0.45f, 0.95f);

        static Widget Tip(Model m, int index)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            var tips = MenuText.Tips;
            var tip = tips[Mathf.Clamp(index, 0, tips.Length - 1)];
            var parts = new List<Widget>(5) { UiKit.Icon(tip.icon, 36f) };
            if (tip.hasButton)
            {
                parts.Add(UiKit.Key(InputGlyphs.ForKeyboard(tip.button), 30f));
                if (m.players >= 2 && m.padConnected) parts.Add(UiKit.Key(InputGlyphs.ForPad(tip.button), 30f));
            }
            parts.Add(UiKit.Wrapped(Loc.T(tip.key), tx.LabelBold, 640f));
            Widget body = new Column(new[]
            {
                UiKit.Label(Loc.T("load.tip").ToUpperInvariant(), tx.SmallBold, th.warnInk, false),
                new Row(parts, 12f, MainAxisAlignment.Start, CrossAxisAlignment.Center),
            }, 4f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
            // Keyed by the tip: a new tip is a new label, so it pops in again.
            return new SizedBox(UiKit.Tag(body, 10f, UiMotion.Pop, th.Skins.TagBig), 840f).WithKey(TipKeys[Mathf.Clamp(index, 0, TipKeys.Length - 1)]);
        }
    }
}
