using LumaFlow;
using UnityEngine;
using Column = LumaFlow.Column;

namespace Movers
{
    // The grandmother, top right: her face for her mood, her name and the mood word, and her
    // patience as a tape measure running from green to red. It shakes when her mood changes
    // for the worse, so the one number the crew must watch catches the eye by itself. Unplaced:
    // it heads PlayerHudView's right column, above the key hints and the toasts.
    public static class GrandmaView
    {
        public const float Width = 290f;

        // One entrance per mood, so a change of mood replays it (a shared probe would not).
        static readonly UiMotion[] Entrances =
        {
            new UiMotion(MotionKind.PopIn, 0.3f), new UiMotion(MotionKind.Wobble, 0.45f),
            new UiMotion(MotionKind.Wobble, 0.5f), new UiMotion(MotionKind.Wobble, 0.55f),
            new UiMotion(MotionKind.Pulse, 0.5f),
        };

        public static Widget Build(SharedHudModel m, PlayerHudModel p) =>
            new ReactiveBuilder<int>(m.Mood, tier => tier < 0 ? UiKit.Empty : Meter((MoodTier)tier, p));

        static Widget Meter(MoodTier tier, PlayerHudModel p)
        {
            var th = UiKit.Theme;
            var tx = th.Text;
            Color c = th.Mood(tier);
            Widget text = new Column(new[]
            {
                new Row(new[]
                {
                    new Expanded(UiKit.Label(Loc.T("grandma.name"), tx.LabelBold, false)),
                    UiKit.Chip(MoodWord(tier), c),
                }, 6f, MainAxisAlignment.Start, CrossAxisAlignment.Center),
                UiKit.TapeBar(p.PatienceFill, c, Width - 124f),
                UiKit.Label(Loc.T("grandma.patience"), tx.Caption, th.creamSoft, false),
            }, 4f, MainAxisAlignment.Start, CrossAxisAlignment.Stretch);

            Widget row = new Row(new[] { UiKit.Icon(UiTheme.MoodIcon(tier), 58f), new Expanded(text) },
                                 10f, MainAxisAlignment.Start, CrossAxisAlignment.Center);
            return new SizedBox(UiKit.Panel(row, th.Skins.WoodDark, 8f, Entrances[(int)tier]), Width);
        }

        public static string MoodWord(MoodTier tier)
        {
            switch (tier)
            {
                case MoodTier.Sweet: return Loc.T("mood.sweet");
                case MoodTier.Annoyed: return Loc.T("mood.annoyed");
                case MoodTier.Angry: return Loc.T("mood.angry");
                case MoodTier.Furious: return Loc.T("mood.furious");
                default: return Loc.T("mood.police");
            }
        }
    }
}
