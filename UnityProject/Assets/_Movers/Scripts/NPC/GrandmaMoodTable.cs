using UnityEngine;

namespace Movers
{
    // What costs the grandmother how much patience. Every number is a first guess (ADR-009:
    // "every number is a hypothesis"); a playtest note should map to one field here. Costs are
    // in patience points out of 100.
    //
    // Softened on 2026-09-26 after Pierre's first look: "she should not call the police right
    // away, she is a bit deaf; with the grenades it gets hot, it must not be too hard". So she
    // hears less far, every cost is about half, she calms down sooner and higher, one
    // catastrophe can only take so much in a few seconds, and at zero she gives a last warning
    // before she picks up the phone.
    //
    // Made deterministic on 2026-09-28 (DEV 2, ADR-013): the cap no longer swallows offences,
    // the warning comes once per run and only a player's own offence ends it.
    [System.Serializable]
    public sealed class MoodCosts
    {
        [Header("Patience")]
        public float startPatience = 100f;
        [Tooltip("Mood words: Sweet above, then Annoyed, Angry, Furious.")]
        public float annoyedBelow = 70f;
        public float angryBelow = 40f;
        [Tooltip("Below this she confronts the worst offender.")]
        public float furiousBelow = 20f;

        [Header("Noise")]
        public float smallNoise = 0.5f;
        [Tooltip("Seconds before another noise costs anything: a crash is one annoyance, not six.")]
        public float smallNoiseCooldown = 6f;

        [Header("Breakage she perceives")]
        public float objectDestroyed = 4f;
        public float fragileDestroyed = 5f;
        public float contractDamaged = 3f;
        public float contractDestroyed = 6f;
        public float windowBroken = 4f;
        public float doorBroken = 8f;
        public float explosionInside = 12f;
        public float explosionOutside = 4f;
        [Tooltip("One of her own things cracked (not on the list; those are contractDamaged). 0: she does not mind.")]
        public float objectDamaged = 1.5f;
        [Tooltip("A wall, a fence or a structural piece broken through, or part of the house brought down. Capped like breakage, except a collapse, which pays full price.")]
        public float structureBroken = 6f;
        [Tooltip("Seconds per culprit before another broken wall costs anything: a truck through a fence and the wall behind it is one annoyance.")]
        public float structureCooldown = 3f;
        [Tooltip("A hedge, a bush, the mailbox, a garden post... Garden damage, never a wall.")]
        public float gardenBroken = 1.5f;

        [Header("The crew's behaviour")]
        public float bumped = 2f;
        public float bumpCooldown = 2f;
        public float smokingPuff = 1f;
        public float smokingCooldown = 10f;
        public float drinking = 1.5f;
        public float drinkingCooldown = 8f;
        public float theftWitnessed = 15f;
        [Tooltip("Seeing one of her non-contract things carried about, per carryingInterval seconds.")]
        public float carryingSeen = 2f;
        public float carryingInterval = 8f;
        [Tooltip("Seconds of seeing her non-contract thing carried before it counts as a theft she witnessed.")]
        public float carryWitnessSeconds = 2f;

        [Header("Schedule")]
        [Tooltip("Cost per check while the loaded share of the list lags the elapsed share of the time by more than scheduleGrace.")]
        public float behindSchedule = 2f;
        public float scheduleInterval = 90f;
        [Range(0f, 1f)] public float scheduleGrace = 0.15f;

        [Header("Recovery")]
        public float recoveryAmount = 2f;
        public float recoveryInterval = 6f;
        public float calmBeforeRecovery = 12f;
        [Tooltip("She never calms down past this again.")]
        public float recoveryCap = 80f;

        [Header("A bit deaf")]
        [Tooltip("Share of the normal hearing radius (WorldEvents.HearingRadius) she hears at. 1 = sharp ears.")]
        [Range(0.2f, 1f)] public float hearing = 0.6f;
        [Tooltip("The same share for explosions. 1: she hears a grenade 30 m away, deaf or not.")]
        [Range(0.2f, 1f)] public float explosionHearing = 1f;

        [Header("How loud breakage is when the raiser gave no loudness (WorldEvents.HearingRadius)")]
        public float objectDestroyedLoudness = 0.5f;
        public float objectDamagedLoudness = 0.4f;
        public float contractDamagedLoudness = 0.4f;
        public float contractDestroyedLoudness = 0.5f;
        public float windowBrokenLoudness = 0.6f;
        public float doorBrokenLoudness = 0.7f;
        public float structureBrokenLoudness = 0.8f;
        public float gardenBrokenLoudness = 0.5f;

        [Header("Leniency")]
        [Tooltip("The most patience she can lose within lossWindow seconds: a grenade chain is one catastrophe, not the end of the run.")]
        public float maxLossPerWindow = 20f;
        public float lossWindow = 10f;
        [Tooltip("On: the cap above only holds back noise and breakage, offences (theft, bumps, smoking...) always pay full price. Off: everything is capped (the old rule).")]
        public bool capOnlyBreakage = true;
        [Tooltip("At zero she warns first. A player's offence during these seconds and she calls the police; none, and she gets lastWarningRecover back. 0 = call at once.")]
        public float lastWarningSeconds = 25f;
        public float lastWarningRecover = 10f;
        [Tooltip("Warnings she gives in one run. Used up: patience 0 calls the police at once.")]
        public int lastWarningsPerRun = 1;
        [Tooltip("During the warning only a player's own offence costing at least this much ends it with the call. Anything cheaper, a noise, the schedule, or nobody's doing costs nothing.")]
        public float warningOffenceMinCost = 2f;
        [Tooltip("Seconds after the warning began before an offence can end it: what made her warn cannot also make her call.")]
        public float warningGraceSeconds = 3f;
    }

    // The asset form of the table. Create one under Assets/_Movers/Data/ and assign it to
    // GrandmaMood; without an asset the code defaults above apply.
    [CreateAssetMenu(menuName = "Movers/Grandma Mood Table", fileName = "GrandmaMoodTable")]
    public sealed class GrandmaMoodTable : ScriptableObject
    {
        public MoodCosts costs = new MoodCosts();
    }
}
