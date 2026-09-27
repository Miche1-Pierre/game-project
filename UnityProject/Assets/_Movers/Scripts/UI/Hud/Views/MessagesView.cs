using System;
using System.Collections.Generic;
using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // What is going on, in words. The banner, top centre under the compass tape, says what
    // matters now (get the keys, the police are coming, everything is loaded) with this
    // player's own key in it. The toasts, under the grandmother's panel on the right, tell the
    // story (who pocketed what, what broke, what she saw). The left column is the contract's,
    // so the two never overlap in a half-width split view.
    public static class MessagesView
    {
        public const float BannerWidth = 300f;
        public const float ToastWidth = 380f;

        // Under INDICATORS' compass tape, which takes the top 52 px of a 1080-high view (10 px
        // margin, 34 px tape, 8 px gap) and scales with the view's height, never below 0.6.
        // The HUD panel scales with the screen height, so in this view's units the tape is 52
        // side by side at any resolution, 45 in a stacked 1080p split and 67 in a stacked 720p
        // one (views scaled by the HUD's 0.7 floor). 68 clears all of these without a
        // compile-time link to that track; with the markers off the sign hangs a little lower.
        public const float BannerTop = 68f;
        static readonly UiPlace BannerPlace = UiPlace.TopCenter(BannerTop);
        static readonly UiPlace ToastPlace = new UiPlace(100f, 0f, 100f, 0f, -18f, 136f);

        public static Widget Banner(SharedHudModel m, PlayerHudModel p) =>
            UiKit.With(BannerPlace, new ReactiveBuilder<int>(m.Banner, b => BannerFor((BannerKind)b, m, p)));

        static Widget BannerFor(BannerKind kind, SharedHudModel m, PlayerHudModel p)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            switch (kind)
            {
                case BannerKind.Police:
                {
                    Widget text = UiKit.Bound(m.PoliceText, tx.LabelBold, UiTheme.Hex(0xFFD2C4));
                    Widget row = new Row(new[] { UiKit.Icon(UiSprites.IconWarning, 26f, th.bad), text }, 8f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
                    return UiKit.Skinned(th.Skins.ChipIn(th.moodPolice), row, EdgeInsets.Symmetric(16f, 8f), UiMotion.PulseFast);
                }
                case BannerKind.Intro:
                    return Sign(Loc.T("banner.intro"), UiSprites.IconKey, p, CrewButton.Interact);
                case BannerKind.Ready:
                    return Sign(Loc.T("banner.ready"), UiSprites.IconTruck, p, CrewButton.Interact);
            }
            return UiKit.Empty;
        }

        // A hanging sign with a key: the device follows the player (E on a keyboard, X on a pad).
        static Widget Sign(string words, string icon, PlayerHudModel p, CrewButton key)
        {
            var th = UiKit.Theme;
            return new ReactiveBuilder<int>(p.Device, d =>
            {
                var parts = new List<Widget>(3) { UiKit.Icon(icon, 26f, th.accent) };
                if (d != 2) parts.Add(UiKit.Key(InputGlyphs.For(p.Source, key), 28f));
                parts.Add(UiKit.Wrapped(words, th.Text.BodyBold, BannerWidth - 70f));
                Widget row = new Row(parts, 8f, MainAxisAlignment.Center, CrossAxisAlignment.Center);
                return UiKit.Panel(row, th.Skins.WoodDark, 6f, UiMotion.Drop);
            });
        }

        public static Widget Toasts(SharedHudModel m) =>
            UiKit.With(ToastPlace, new ReactiveBuilder<int>(m.Toasts.Version, _ => Column(m.Toasts)));

        static Widget Column(ToastFeed feed)
        {
            var live = feed.Live;
            if (live.Count == 0) return UiKit.Empty;
            var items = new Widget[live.Count];
            for (int i = 0; i < live.Count; i++)
            {
                var e = live[i];
                Widget toast = UiKit.Toast(e.text, e.icon, e.color, ToastWidth);
                items[i] = new AnimatedOpacity(toast, e.opacity, TimeSpan.FromMilliseconds(ToastFeed.FadeSeconds * 1000f)).WithKey(e.key);
            }
            return new Column(items, 6f, MainAxisAlignment.Start, CrossAxisAlignment.End);
        }
    }
}
