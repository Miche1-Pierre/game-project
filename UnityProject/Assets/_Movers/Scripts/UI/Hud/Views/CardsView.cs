using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using Column = LumaFlow.Column;
using Container = LumaFlow.Container;

namespace Movers
{
    // The two full-screen cards, over every view: the intro, a sheet of paper that states the
    // job and stays until a player presses the key it names ("Appuie sur [E] pour continuer"),
    // and the end screen, the settlement line by line with Rejouer and Menu. Both name the keys
    // of every device the crew holds (E and X in a mixed crew).
    public static class CardsView
    {
        public const float SheetWidth = 900f;
        static readonly UiPlace Place = UiPlace.Center();

        public static Widget Build(SharedHudModel m) =>
            new ReactiveBuilder<int>(m.Card, k => (CardKind)k == CardKind.Intro ? Intro(m)
                                              : (CardKind)k == CardKind.End ? End(m)
                                              : UiKit.Empty);

        static Widget Frame(Widget sheet)
        {
            var th = UiKit.Theme;
            return new Stack(new[] { UiPlace.Fill.Widget, th.Skins.Scrim.Widget, UiKit.With(Place, sheet) });
        }

        // ---- intro ----

        static Widget Intro(SharedHudModel m)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            int count = m.Contract != null ? m.Contract.Tracker.Total : 0;

            Widget logo = th.HasSprite(UiSprites.LogoTheMovers)
                ? UiKit.With(UiMotion.Hang, UiKit.Picture(UiSprites.LogoTheMovers, 440f))
                : UiKit.With(UiMotion.Hang, UiKit.Label(Loc.T("intro.title"), tx.Display, th.wood, false));

            Widget body = new Column(new[]
            {
                Paragraph(UiSprites.IconTruck, Loc.F("intro.job", count > 0 ? count.ToString() : ""), th.woodDark),
                Paragraph(UiSprites.IconPocket, Loc.T("intro.steal"), th.goodInk),
                Paragraph(UiSprites.IconGrandmaAngry, Loc.T("intro.patience"), th.badInk),
            }, 12f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch);

            Widget cta = UiKit.Sign(Loc.T("intro.cta"), tx.LabelBold, UiMotion.Drop);
            // Shown once a key can close the card (a second after it opened), then breathing:
            // nothing else on the card moves, so the eye finds what to do.
            Widget start = new ReactiveBuilder<bool>(m.CardSkippable, ok => ok
                ? UiKit.With(UiMotion.Pop, UiKit.With(UiMotion.PulseSlow, new ReactiveBuilder<int>(m.Devices, PressToContinue)))
                : UiKit.Gap(PromptKey));

            Widget sheet = new Column(new[]
            {
                logo,
                UiKit.Label(Loc.T("intro.subtitle"), tx.Title, th.inkSoft, false),
                UiKit.Gap(4f),
                SizedBox.ExpandWidth(body),   // the paragraphs take the sheet's width and wrap in it
                UiKit.Gap(4f),
                cta,
                start,
            }, 12f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return Frame(new SizedBox(UiKit.Card(sheet, 16f, th.Skins.CardboardBig, UiMotion.Pop), SheetWidth));
        }

        static Widget Paragraph(string icon, string text, Color tint)
        {
            var th = UiKit.Theme;
            return new Row(new[] { UiKit.Icon(icon, 40f, tint), new Expanded(UiKit.Label(text, th.Text.Label)) },
                           14f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
        }

        const float PromptKey = 36f;

        // "Appuie sur [E] / [X] pour continuer": the interact key of each device the crew holds
        // (Jump closes the card too, GameSession; one key is enough to name).
        static Widget PressToContinue(int devices)
        {
            var th = UiKit.Theme;
            var style = th.Text.LabelBold;
            var parts = new List<Widget>(5) { UiKit.Label(Loc.T("intro.press"), style, false) };
            if ((devices & 1) != 0) parts.Add(UiKit.Key(InputGlyphs.ForKeyboard(CrewButton.Interact), PromptKey));
            if ((devices & 3) == 3) parts.Add(UiKit.Label("/", style, false));
            if ((devices & 2) != 0) parts.Add(UiKit.Key(InputGlyphs.ForPad(CrewButton.Interact), PromptKey));
            parts.Add(UiKit.Label(Loc.T("intro.continue"), style, false));
            return new Row(parts, 10f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
        }

        // ---- end screen ----

        static Widget End(SharedHudModel m)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            Settlement s = m.Result;
            if (s == null) return UiKit.Empty;

            string title = Loc.T(SettlementText.IsEscape(s) ? "end.escaped" : s.Completed ? "end.complete" : "end.failed");
            Color titleColor = s.Completed ? th.goodInk : th.badInk;
            string reason = s.Completed ? null : Loc.T(ReasonKey(s));

            var lines = new List<Widget>(s.Lines.Count * 2);
            for (int i = 0; i < s.Lines.Count; i++) lines.Add(Line(s.Lines[i], th));

            Widget total = new Row(new[]
            {
                new Expanded(UiKit.Label(Loc.T("end.total"), tx.Title, false)),
                UiKit.With(UiMotion.Pop, UiKit.Label(Loc.Money(s.Total), tx.Sized(40f, true), Amount(s.Total, th), false)),
            }, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);

            var devices = m.Devices.Value;
            Widget buttons = new Row(new[]
            {
                UiKit.Button(Loc.T("end.replay"), Replay, true, true, DeviceGlyph(devices, CrewButton.Interact), 260f),
                UiKit.Button(Loc.T("end.menu"), ToMenu, false, true, DeviceGlyph(devices, CrewButton.Pause), 200f),
            }, 18f, MainAxisAlignment.Center, CrossAxisAlignment.Center);

            var parts = new List<Widget>(8)
            {
                UiKit.With(UiMotion.Drop, UiKit.Label(title, tx.Sized(54f, true), titleColor, false)),
            };
            if (reason != null) parts.Add(UiKit.Label(reason, tx.Label, th.inkSoft, false));
            parts.Add(UiKit.Gap(4f));
            parts.Add(new Column(lines, 8f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch));
            parts.Add(Rule(th));
            parts.Add(total);
            parts.Add(buttons);
            parts.Add(new Row(new[] { UiKit.Bound(m.EndCountdown, tx.Small, th.inkSoft) }, 0f, MainAxisAlignment.Center, CrossAxisAlignment.Center));
            Widget sheet = new Column(parts, 10f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch);
            return Frame(new SizedBox(UiKit.Card(sheet, 16f, th.Skins.CardboardBig, UiMotion.Pop), SheetWidth));
        }

        // Why the run failed, one line under the title.
        public static string ReasonKey(Settlement s)
        {
            switch (s.Failure)
            {
                case FailReason.TimeUp: return "end.timeUp";
                case FailReason.PoliceCalled: return "end.police";
                case FailReason.Intercepted: return SettlementText.IsSurrounded(s) ? "end.surrounded" : "end.intercepted";
                case FailReason.CrewArrested: return "end.arrested";
                default: return "end.stopped";
            }
        }

        static Widget Line(Settlement.Line line, UiTheme th)
        {
            var tx = th.Text;
            string label = SettlementText.Label(line.label);
            string detail = SettlementText.Detail(line.detail);
            Widget words = string.IsNullOrEmpty(detail)
                ? UiKit.Label(label, tx.Label)
                : new Column(new[] { UiKit.Label(label, tx.Label), UiKit.Label(detail, tx.Small, th.inkSoft) }, 2f,
                             MainAxisAlignment.Start, CrossAxisAlignment.Stretch);
            string amount = line.amount == 0 ? "-" : Loc.SignedMoney(line.amount);
            return new Row(new[]
            {
                new Expanded(words),
                UiKit.Label(amount, tx.LabelBold, Amount(line.amount, th), false),
            }, 12f, MainAxisAlignment.Start, CrossAxisAlignment.Start);
        }

        // "Rejouer" plays this job again, in this scene. SceneFlow only knows the house (and
        // brings the loading screen); any other map with a session (Tutorial_01) reloads itself,
        // the way E/X on this card does through GameSession, so both routes land in one place.
        // Online (NETCODE_SLICE 3.6, 10): the host reloads for both through the funnel, the
        // client asks the host for it.
        static void Replay()
        {
            if (Net.IsOnline)
            {
                if (Net.IsHost) NetSession.ReloadForBoth();
                else NetSession.RequestReplay();
                return;
            }
            if (SceneManager.GetActiveScene().name == SceneFlow.GameScene) SceneFlow.ReloadGame();
            else SceneReload.Reload();
        }

        // Online the host's "Menu" ends the run for both, the client's leaves alone.
        static void ToMenu()
        {
            if (Net.IsOnline) NetSession.LeaveToMenu();
            else SceneFlow.LoadMenu();
        }

        static Color Amount(int v, UiTheme th) => v > 0 ? th.goodInk : v < 0 ? th.badInk : th.inkSoft;

        // The key of the first device present: the keyboard's when there is one.
        static Glyph DeviceGlyph(int devices, CrewButton b) =>
            (devices & 1) != 0 ? InputGlyphs.ForKeyboard(b) : InputGlyphs.ForPad(b);

        static Widget Rule(UiTheme th)
        {
            Color c = th.woodDark;
            c.a = 0.5f;
            return new SizedBox(new Container(UiKit.Empty, new BoxDecoration(backgroundColor: c, borderRadius: BorderRadius.All(1.5f))), null, 3f);
        }
    }
}
