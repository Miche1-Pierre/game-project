using System;
using UnityEngine;

namespace Movers
{
    public enum MoodTier { Sweet, Annoyed, Angry, Furious, Police }

    // Her patience, from 100 down to 0. Everything she perceives has a price in the mood table
    // (GrandmaMoodTable); this component charges it, keeps the per-kind cooldowns (a crash is
    // one annoyance, not six), remembers who annoyed her most, and lets her calm down slowly
    // when nothing happens. One catastrophe can only cost so much in a few seconds
    // (maxLossPerWindow, noise and breakage only: offences always pay full price).
    //
    // The ladder is deterministic: at 0 she gives her one last warning of the run (shown for
    // its whole duration, GrandmaLastWarning); only a player's own offence costing
    // warningOffenceMinCost or more, warningGraceSeconds after it began, ends it with the
    // call. None, and she calms down a little. At 0 with the warning used: the call, at once.
    //
    // Every change is a GrandmaMoodChanged world event, so the HUD and the event log follow
    // her without holding a reference to her.
    [DisallowMultipleComponent]
    public sealed class GrandmaMood : MonoBehaviour
    {
        [Tooltip("Assets/_Movers/Data/GrandmaMoodTable.asset. Empty: the code defaults.")]
        public GrandmaMoodTable table;

        static readonly MoodCosts Defaults = new MoodCosts();
        public MoodCosts Costs => table != null && table.costs != null ? table.costs : Defaults;

        public float Patience { get; private set; } = 100f;
        public float LastLossTime { get; private set; } = -99f;
        // Her AI is switched off (Shift+F4): nothing costs anything and she does not recover.
        public bool Frozen { get; set; }

        // Fired when she calls the police: an offence during her last warning, or 0 reached with
        // the run's warnings used up (or none in the table).
        public event Action ReachedZero;
        // Her patience ran out: she warns once before calling. Then either ReachedZero, or
        // WarningSurvived when the crew behaved until the warning ran out.
        public event Action LastWarning;
        public event Action WarningSurvived;

        public bool InLastWarning { get; private set; }
        public float WarningLeft => InLastWarning ? Mathf.Max(0f, warningUntil - Time.time) : 0f;
        // Warnings given this run (lastWarningsPerRun). The brain compares it before and after
        // a batch to know the batch started one.
        public int WarningsGiven { get; private set; }
        float warningUntil, warningStart;
        float windowStart = -99f, windowLoss;

        // Cooldowns per kind and per culprit: players 0..3, then one slot for everyone else.
        const int Slots = 5;
        readonly float[] cooldownUntil = new float[(int)StimulusKind.Count * Slots];
        readonly float[] blame = new float[4];
        float nextRecovery;

        public MoodTier Tier
        {
            get
            {
                var c = Costs;
                if (Patience <= 0f) return InLastWarning ? MoodTier.Furious : MoodTier.Police;
                if (Patience < c.furiousBelow) return MoodTier.Furious;
                if (Patience < c.angryBelow) return MoodTier.Angry;
                if (Patience < c.annoyedBelow) return MoodTier.Annoyed;
                return MoodTier.Sweet;
            }
        }

        static readonly string[] Words = { "Sweet", "Annoyed", "Angry", "Furious", "Calling the police" };
        public static string Word(MoodTier tier) => Words[(int)tier];

        void Awake()
        {
            Patience = Mathf.Clamp(Costs.startPatience, 0f, 100f);
        }

        // The price of one stimulus, right now. Kinds with a cooldown cost nothing while it
        // runs, and asking starts it: call this once per stimulus.
        public float CostOf(in Stimulus s)
        {
            var c = Costs;
            switch (s.kind)
            {
                // Noise is one shared cooldown: two players dropping things is still one racket.
                case StimulusKind.SmallNoise: return Cooled(s.kind, Actors.World, c.smallNoiseCooldown) ? c.smallNoise : 0f;
                case StimulusKind.ObjectDestroyed: return s.fragile ? c.fragileDestroyed : c.objectDestroyed;
                case StimulusKind.ContractDamaged: return c.contractDamaged;
                case StimulusKind.ContractDestroyed: return c.contractDestroyed;
                case StimulusKind.WindowBroken: return c.windowBroken;
                case StimulusKind.DoorBroken: return c.doorBroken;
                case StimulusKind.Explosion: return s.inside ? c.explosionInside : c.explosionOutside;
                case StimulusKind.Bumped: return Cooled(s.kind, s.instigator, c.bumpCooldown) ? c.bumped : 0f;
                case StimulusKind.SeatTaken: return c.bumped;
                case StimulusKind.Smoking: return Cooled(s.kind, s.instigator, c.smokingCooldown) ? c.smokingPuff : 0f;
                case StimulusKind.Drinking: return Cooled(s.kind, s.instigator, c.drinkingCooldown) ? c.drinking : 0f;
                case StimulusKind.TheftWitnessed: return c.theftWitnessed;
                case StimulusKind.CarryingSeen: return c.carryingSeen;
                case StimulusKind.BehindSchedule: return c.behindSchedule;
                case StimulusKind.StructureBroken: return Cooled(s.kind, s.instigator, c.structureCooldown) ? c.structureBroken : 0f;
                case StimulusKind.GardenBroken: return c.gardenBroken;
                case StimulusKind.ObjectDamaged: return c.objectDamaged;
            }
            return 0f;
        }

        bool Cooled(StimulusKind kind, int instigator, float seconds)
        {
            int slot = Actors.IsPlayer(instigator) && instigator < Slots - 1 ? instigator : Slots - 1;
            int i = (int)kind * Slots + slot;
            if (Time.time < cooldownUntil[i]) return false;
            cooldownUntil[i] = Time.time + seconds;
            return true;
        }

        // Takes `cost` (CostOf(s)) off her patience, blames the instigator for it, and returns
        // what was actually lost (less at the bottom of the bar or under the cap, 0 during the
        // warning). The brain is the only caller, once per stimulus.
        public float Apply(float cost, in Stimulus s)
        {
            if (Frozen || cost <= 0f) return 0f;
            var c = Costs;
            int instigator = s.instigator;
            if (InLastWarning)
            {
                // She is waiting for one thing only. Anything else costs nothing now.
                if (!EndsWarning(cost, s)) return 0f;
                // She warned them. This is the offence that makes her pick up the phone.
                InLastWarning = false;
                LastLossTime = Time.time;
                if (Actors.IsPlayer(instigator) && instigator < blame.Length) blame[instigator] += cost;
                if (Net.IsHost) GrandmaSync.SendMood(this);
                WorldEvents.Raise(WorldEventType.GrandmaMoodChanged, transform.position, instigator, 0f, Patience);
                ReachedZero?.Invoke();
                return 0f;
            }
            if (Patience <= 0f) return 0f;

            // One catastrophe at a time: a whole grenade chain in the next room is a few
            // seconds of fury, not a run lost in one blast. Offences are not catastrophes:
            // a theft she saw always costs its price.
            if (!c.capOnlyBreakage || s.IsCapped)
            {
                if (Time.time - windowStart > Mathf.Max(0.5f, c.lossWindow)) { windowStart = Time.time; windowLoss = 0f; }
                float allowed = Mathf.Max(0f, c.maxLossPerWindow - windowLoss);
                cost = Mathf.Min(cost, allowed);
                if (cost <= 0f) return 0f;
                windowLoss += cost;
            }

            float before = Patience;
            Patience = Mathf.Max(0f, Patience - cost);
            float lost = before - Patience;
            LastLossTime = Time.time;
            if (Actors.IsPlayer(instigator) && instigator < blame.Length) blame[instigator] += lost;

            // Decided before the mood goes out, so the client gets the warning with the value.
            bool warn = Patience <= 0f && c.lastWarningSeconds > 0f && WarningsGiven < Mathf.Max(0, c.lastWarningsPerRun);
            if (warn)
            {
                InLastWarning = true;
                WarningsGiven++;
                warningStart = Time.time;
                warningUntil = Time.time + c.lastWarningSeconds;
            }

            if (Net.IsHost) GrandmaSync.SendMood(this);
            WorldEvents.Raise(WorldEventType.GrandmaMoodChanged, transform.position, instigator, 0f, Patience);
            if (warn)
            {
                LastWarning?.Invoke();
                WorldEvents.Raise(WorldEventType.GrandmaLastWarning, transform.position, WorstOffender(), 0f, c.lastWarningSeconds);
            }
            else if (Patience <= 0f) ReachedZero?.Invoke();
            return lost;
        }

        // Only a player's own doing, a real offence (not a noise, not the clock), costing enough,
        // and not in the first seconds: the blast that made her warn cannot make her call too.
        bool EndsWarning(float cost, in Stimulus s)
        {
            var c = Costs;
            return Actors.IsPlayer(s.instigator)
                && s.kind != StimulusKind.SmallNoise && s.kind != StimulusKind.BehindSchedule
                && cost >= c.warningOffenceMinCost
                && Time.time >= warningStart + Mathf.Max(0f, c.warningGraceSeconds);
        }

        // A player she has seen at work near her can pick up a share of the blame for something
        // she only heard ("was that you?").
        public void AddBlame(int player, float amount)
        {
            if (Actors.IsPlayer(player) && player < blame.Length) blame[player] += Mathf.Max(0f, amount);
        }

        public float BlameOf(int player) => Actors.IsPlayer(player) && player < blame.Length ? blame[player] : 0f;

        // The player who cost her the most patience, or Actors.World when nobody has yet.
        public int WorstOffender()
        {
            int best = Actors.World;
            float most = 0f;
            for (int i = 0; i < blame.Length; i++)
                if (blame[i] > most) { most = blame[i]; best = i; }
            return best;
        }

        // Online client (GrandmaSync Mood): the host's patience and warning, no events.
        public void ApplyReplica(float patience, bool inLastWarning)
        {
            if (patience < Patience) LastLossTime = Time.time;
            if (inLastWarning && !InLastWarning)
            {
                warningStart = Time.time;
                warningUntil = Time.time + Costs.lastWarningSeconds;
            }
            Patience = Mathf.Clamp(patience, 0f, 100f);
            InLastWarning = inLastWarning;
        }

        void Update()
        {
            if (Frozen || !Net.HasAuthority) return;
            var c = Costs;
            if (InLastWarning)
            {
                if (Time.time < warningUntil) return;
                // They behaved through the whole warning: she grumbles and lets it go, a bit.
                InLastWarning = false;
                Patience = Mathf.Clamp(c.lastWarningRecover, 1f, 100f);
                LastLossTime = Time.time;
                if (Net.IsHost) GrandmaSync.SendMood(this);
                WorldEvents.Raise(WorldEventType.GrandmaMoodChanged, transform.position, Actors.World, 0f, Patience);
                WarningSurvived?.Invoke();
                return;
            }
            if (Patience <= 0f) return;
            if (Patience >= c.recoveryCap) return;

            // A quiet spell first, then a point at a time. The cap keeps a bad start from
            // being forgotten entirely.
            float calmSince = LastLossTime + c.calmBeforeRecovery;
            if (Time.time < calmSince) return;
            if (nextRecovery < calmSince) nextRecovery = calmSince + c.recoveryInterval;
            if (Time.time < nextRecovery) return;
            nextRecovery += Mathf.Max(0.5f, c.recoveryInterval);

            Patience = Mathf.Min(c.recoveryCap, Patience + c.recoveryAmount);
            if (Net.IsHost) GrandmaSync.SendMood(this);
            WorldEvents.Raise(WorldEventType.GrandmaMoodChanged, transform.position, Actors.World, 0f, Patience);
        }
    }
}
