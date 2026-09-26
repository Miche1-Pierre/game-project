using UnityEngine;

namespace Movers
{
    // Every number of the game loop in one place, so a playtest note ("the fine is too soft")
    // maps to one edit. They are first guesses, not decisions: ADR-009 says every number is a
    // hypothesis until two people have played it.
    [System.Serializable]
    public sealed class GameLoopNumbers
    {
        [Header("Session")]
        [Tooltip("Seconds of the job when the scene has no ContractManager to say (its timeLimit wins).")]
        public float defaultTimeLimit = 600f;
        [Tooltip("Seconds between the grandmother's call and the end of the run. Long enough to hear her make the call and read it on screen, too short to finish the job.")]
        public float policeCountdown = 8f;
        [Tooltip("Seconds the intro card stays up with everyone frozen, unless a player skips it. 0: no card and nobody frozen (for a session of Play-mode tests; GAMELOOP test 10 needs the card).")]
        public float introCardSeconds = 6f;
        [Tooltip("Seconds before the intro card can be skipped, so a key still down from the last run does not skip it.")]
        public float introCardSkipAfter = 1f;
        [Tooltip("Seconds before E restarts from the end screen, so a player still pressing E to deliver reads the settlement first.")]
        public float restartDelay = 3f;

        [Header("Settlement")]
        [Tooltip("Share of its value a damaged object is still worth: delivered on the list, or taken (her things sell for it, and a fine for one she saw is on it).")]
        [Range(0f, 1f)] public float damagedPayFraction = 0.5f;
        [Tooltip("Share of its value the client bills for an object on the list that was destroyed.")]
        public float destroyedBillFraction = 1f;
        [Tooltip("Share of its value an item of hers pays when nobody saw it taken.")]
        public float unseenTheftPayFraction = 1f;
        [Tooltip("Fine, as a share of its value, for an item she saw taken. The item itself is confiscated.")]
        public float witnessedFineFraction = 1f;
        [Tooltip("What getting in by breaking a window or a door, instead of waiting for her keys, costs at the settlement.")]
        public int breakInCost = 250;

        [Header("Pockets")]
        [Tooltip("An object of the house this light or lighter...")]
        public float pocketMaxKg = 1.5f;
        [Tooltip("...and no side longer than this (m) fits in a pocket.")]
        public float pocketMaxSide = 0.35f;

        public static readonly GameLoopNumbers Defaults = new GameLoopNumbers();
    }

    // The optional asset (Assets/_Movers/Data/GameLoopTuning.asset). Without one, GameSession
    // uses the code defaults above, so a scene never breaks for want of it.
    [CreateAssetMenu(menuName = "Movers/Game Loop Tuning", fileName = "GameLoopTuning")]
    public sealed class GameLoopTuning : ScriptableObject
    {
        public GameLoopNumbers numbers = new GameLoopNumbers();
    }
}
