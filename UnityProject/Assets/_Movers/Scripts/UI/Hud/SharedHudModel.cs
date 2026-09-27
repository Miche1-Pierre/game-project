using System;
using LumaFlow;
using UnityEngine;

namespace Movers
{
    public enum ClockKind { None, KeysFirst, Normal, Low, Police }
    public enum BannerKind { None, Intro, Police, Ready }
    public enum CardKind { None, Intro, End }

    // What every view shows the same: the contract, the clock and the money, the grandmother's
    // mood and words, the toasts, the banner, the intro card and the end screen. Read from the
    // game once per frame by HudRoot; the views rebuild only when one of these states changes.
    //
    // Nothing here allocates in a normal frame: strings are made when the value they show
    // changes (a second of the clock, a dollar of the money), the contract list is compared
    // four times a second through a small signature.
    public sealed class SharedHudModel
    {
        // ---- the contract ----
        public readonly State<int> ContractVersion = new State<int>(0);
        public MovableObject[] Rows = Array.Empty<MovableObject>();
        public ItemLocation[] RowLocation = Array.Empty<ItemLocation>();
        public ItemCondition[] RowCondition = Array.Empty<ItemCondition>();
        public int RowCount;
        public bool HasContract;
        public int DestroyedCount;
        // The board is on screen: there is a contract and the grandmother has handed it over
        // (the run left the Intro, by her keys or by a way in without them). Before that the
        // crew has not talked to her yet, so it has no list (Pierre, feedback 1).
        public readonly State<bool> ContractShown = new State<bool>(false);

        public readonly State<string> Clock = new State<string>("");
        // Kinds are kept as ints: a State<enum> would box in its equality check on some runtimes.
        public readonly State<int> ClockMode = new State<int>((int)ClockKind.None);
        public readonly State<string> Money = new State<string>("");
        public readonly State<bool> MoneyNegative = new State<bool>(false);   // fines above the pay: red, not green
        public readonly State<string> Loaded = new State<string>("");
        public readonly State<string> Stolen = new State<string>("");

        // ---- the grandmother ----
        public readonly State<int> Mood = new State<int>(-1);     // MoodTier, -1 without a grandmother
        public float Patience01 = 1f;
        public readonly State<string> Speech = new State<string>(null);
        public GrandmaMood GrandmaMood { get; private set; }
        public GrandmaSpeech GrandmaSpeech { get; private set; }

        // ---- messages ----
        public readonly State<int> Banner = new State<int>((int)BannerKind.None);
        public readonly State<string> PoliceText = new State<string>("");
        public readonly ToastFeed Toasts = new ToastFeed();

        // ---- full-screen cards ----
        public readonly State<int> Card = new State<int>((int)CardKind.None);
        public readonly State<bool> CardSkippable = new State<bool>(false);
        public readonly State<string> EndCountdown = new State<string>("");
        public Settlement Result { get; private set; }

        // Which devices drive the crew (1 keyboard, 2 pad, 3 both): the cards name both keys.
        public readonly State<int> Devices = new State<int>(1);

        public ContractManager Contract { get; private set; }
        public DeliverPoint Deliver => Contract != null ? Contract.DeliverPoint : null;

        const float ListInterval = 0.25f;
        float nextList, nextFind;
        int[] signature = Array.Empty<int>();
        int shownMoney = int.MinValue, shownLoaded = -1, shownTotal = -1, shownUnseen = -1, shownSeen = -1;
        int shownSecond = -1, shownPolice = -1, shownCountdown = -1, shownBanner = -1;
        bool contractSearched;

        public void Update()
        {
            var session = GameSession.Current;
            FindThings(session);
            UpdateClock(session);
            if (Time.unscaledTime >= nextList)
            {
                nextList = Time.unscaledTime + ListInterval;
                UpdateContract(session);
                Devices.Value = DeviceMask();
            }
            ContractShown.Value = HasContract && Session.State != SessionState.Intro;
            UpdateGrandma();
            UpdateBanner(session);
            UpdateCards(session);
            Toasts.Tick();
        }

        // After a language change: everything made of words is made again.
        public void Relocalize()
        {
            shownMoney = int.MinValue;
            shownLoaded = shownTotal = shownUnseen = shownSeen = -1;
            shownSecond = shownPolice = shownCountdown = shownBanner = -1;
            nextList = 0f;
            ContractVersion.Value++;
        }

        void FindThings(GameSession session)
        {
            if (session != null && session.contract != null) Contract = session.contract;
            else if (Contract == null && !contractSearched)
            {
                contractSearched = true;
                Contract = UnityEngine.Object.FindAnyObjectByType<ContractManager>();
            }
            if ((GrandmaMood == null || GrandmaSpeech == null) && Time.unscaledTime >= nextFind)
            {
                // The grandmother is part of the scene; looked for once a second until found.
                nextFind = Time.unscaledTime + 1f;
                if (GrandmaMood == null) GrandmaMood = UnityEngine.Object.FindAnyObjectByType<GrandmaMood>();
                if (GrandmaSpeech == null)
                {
                    GrandmaSpeech = UnityEngine.Object.FindAnyObjectByType<GrandmaSpeech>();
                    ApplyLanguageToGrandma();
                }
            }
        }

        // Her lines exist in French and English (GrandmaLines): she speaks the game's language.
        public void ApplyLanguageToGrandma()
        {
            if (GrandmaSpeech != null) GrandmaSpeech.french = Loc.French;
        }

        void UpdateClock(GameSession session)
        {
            ClockKind kind;
            if (!HasContract && Contract == null) kind = ClockKind.None;
            else if (Session.State == SessionState.Intro) kind = ClockKind.KeysFirst;
            else if (session != null && session.PoliceCalled) kind = ClockKind.Police;
            else if (Session.TimeLimit <= 0f) kind = ClockKind.None;
            else kind = Session.TimeLeft <= 60f ? ClockKind.Low : ClockKind.Normal;

            if (kind == ClockKind.Police)
            {
                int s = Mathf.CeilToInt(session.PoliceIn);
                if (s != shownPolice)
                {
                    shownPolice = s;
                    Clock.Value = Loc.F("police.short", s);
                }
            }
            else if (kind == ClockKind.KeysFirst) Clock.Value = Loc.T("contract.keysFirst");
            else if (kind != ClockKind.None)
            {
                int s = Mathf.CeilToInt(Session.TimeLeft);
                if (s != shownSecond)
                {
                    shownSecond = s;
                    Clock.Value = Loc.Clock(Session.TimeLeft);
                }
            }
            ClockMode.Value = (int)kind;
        }

        void UpdateContract(GameSession session)
        {
            HasContract = Contract != null;
            if (!HasContract) return;
            ContractTracker t = Contract.Tracker;
            var list = t.Required;

            // The list: rebuilt only when an item's place or state changed.
            int n = list.Count;
            if (signature.Length != n)
            {
                signature = new int[n];
                Rows = new MovableObject[n];
                RowLocation = new ItemLocation[n];
                RowCondition = new ItemCondition[n];
                for (int i = 0; i < n; i++) signature[i] = -1;
            }
            bool changed = RowCount != n;
            for (int i = 0; i < n; i++)
            {
                var m = list[i];
                ItemLocation l = t.LocationOf(m);
                ItemCondition c = t.ConditionOf(m);
                int sig = (int)l * 4 + (int)c;
                if (sig == signature[i] && ReferenceEquals(Rows[i], m)) continue;
                signature[i] = sig;
                Rows[i] = m;
                RowLocation[i] = l;
                RowCondition[i] = c;
                changed = true;
            }
            RowCount = n;
            DestroyedCount = t.DestroyedCount;

            int loaded = t.RemainingLoaded, total = t.Remaining;
            if (loaded != shownLoaded || total != shownTotal)
            {
                shownLoaded = loaded;
                shownTotal = total;
                Loaded.Value = Loc.F("contract.loaded", loaded, total);
            }

            var ledger = session != null ? session.Ledger : null;
            int money = session != null && session.Result != null ? session.Result.Total
                      : session != null ? Settlement.Projected(t, ledger, session.BrokeIn, session.Numbers)
                      : Contract.money;
            if (money != shownMoney)
            {
                shownMoney = money;
                Money.Value = Loc.Money(money);
                MoneyNegative.Value = money < 0;
            }

            int unseen = ledger != null ? ledger.UnseenValue : 0, seen = ledger != null ? ledger.SeenValue : 0;
            if (unseen != shownUnseen || seen != shownSeen)
            {
                shownUnseen = unseen;
                shownSeen = seen;
                Stolen.Value = unseen == 0 && seen == 0 ? ""
                    : seen == 0 ? Loc.F("contract.unseen", Loc.Money(unseen))
                    : Loc.F("contract.unseen", Loc.Money(unseen)) + "  ·  " + Loc.F("contract.seen", Loc.Money(seen));
            }

            if (changed) ContractVersion.Value++;
        }

        void UpdateGrandma()
        {
            var mood = GrandmaMood;
            if (mood == null || !mood.isActiveAndEnabled)
            {
                Mood.Value = -1;
                Speech.Value = null;
                return;
            }
            Patience01 = Mathf.Clamp01(mood.Patience / 100f);
            Mood.Value = (int)mood.Tier;
            var speech = GrandmaSpeech;
            Speech.Value = speech != null && speech.IsSpeaking ? speech.Text : null;
        }

        void UpdateBanner(GameSession session)
        {
            BannerKind b = BannerKind.None;
            if (session != null && session.PoliceCalled && !Session.IsOver)
            {
                b = BannerKind.Police;
                int s = Mathf.CeilToInt(session.PoliceIn);
                if (s != shownBanner)
                {
                    shownBanner = s;
                    PoliceText.Value = Loc.F("banner.police", s);
                }
            }
            else if (Session.State == SessionState.Intro && session != null && session.HasIntro && !session.IntroCardShowing)
                b = BannerKind.Intro;
            else if (Deliver != null && Deliver.CanInteract) b = BannerKind.Ready;
            Banner.Value = (int)b;
        }

        void UpdateCards(GameSession session)
        {
            CardKind card = CardKind.None;
            if (session != null && Session.IsOver && session.Result != null)
            {
                card = CardKind.End;
                Result = session.Result;
                float left = session.Numbers.restartDelay - session.EndAge;
                int s = left > 0f ? Mathf.CeilToInt(left) : 0;
                if (s != shownCountdown)
                {
                    shownCountdown = s;
                    EndCountdown.Value = s > 0 ? "(" + Loc.Int(s) + ")" : "";
                }
            }
            else if (session != null && session.IntroCardShowing)
            {
                card = CardKind.Intro;
                CardSkippable.Value = session.IntroCardAge >= session.Numbers.introCardSkipAfter;
            }
            Card.Value = (int)card;
        }

        // Which devices drive the crew. A scripted player reads the keyboard words; nobody at
        // all (no crew rig) is the keyboard.
        static int DeviceMask()
        {
            int mask = 0;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var input = all[i] != null ? all[i].Input : null;
                if (input == null || input.Source is NullInputSource) continue;
                mask |= LocalDevicesSource.IsPad(input.Source) ? 2 : 1;
            }
            return mask == 0 ? 1 : mask;
        }
    }
}
