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
        public float defaultTimeLimit = 1200f;
        [Tooltip("Seconds between the grandmother's call and the arrival of the lead police car (EscapeMission). In a scene without an EscapeMission, or with policeEndsRun on, the run fails at the end of it.")]
        public float policeCountdown = 75f;
        [Tooltip("The intro card stays up, everyone frozen, until a player presses Interact or Jump (E or Space, X or A on a pad): the job is read, then validated. Off: it also goes by itself after introCardSeconds.")]
        public bool introCardWaitsForKey = true;
        [Tooltip("Seconds the intro card stays up when it does not wait for a key (introCardWaitsForKey off). 0: no card and nobody frozen, whatever the switch above (for a session of Play-mode tests; GAMELOOP test 10 needs the card).")]
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

        [Header("Police and escape")]
        [Tooltip("On: the call ends the run after policeCountdown, as before DEV 2. Off: in a scene with an EscapeMission the police come and the crew flees to the exit.")]
        public bool policeEndsRun = false;
        [Tooltip("Seconds of escape from the arrival of the police. At 0 the house is surrounded: the run fails.")]
        public float fleeTimeLimit = 90f;
        [Tooltip("The mission clock stops at the call: the police are the clock now.")]
        public bool stopClockOnPolice = true;
        [Tooltip("Police cars used, at most the ones parked at the PoliceSpawn.")]
        public int policeCarCount = 2;
        [Tooltip("Seconds between two cars leaving the spawn (a PoliceSpawn can override it).")]
        public float policeStagger = 2f;
        public float policeCruiseKmh = 80f;
        [Tooltip("Speed of a car chasing the truck or catching up: a little over the truck's governor, so it can get ahead.")]
        public float policeChaseKmh = 100f;
        [Tooltip("m/s2")] public float policeAccel = 6f;
        [Tooltip("m/s2")] public float policeBrake = 9f;
        [Tooltip("Metres a car moves sideways to pass what is in its lane, the fleeing truck included.")]
        public float overtakeOffset = 2.5f;
        [Tooltip("Metres ahead of the fleeing truck where a car that got past it stops across the road.")]
        public float roadblockLead = 10f;
        [Tooltip("Degrees a roadblocking car is turned across the lane.")]
        public float roadblockYaw = 70f;
        [Tooltip("A target this far off the route (more than 12 m from it) but this close to a car is chased straight across the fields.")]
        public float offRouteChaseRange = 60f;
        [Tooltip("Metres between a police car and the truck's hull that count as \"stopped by the police\"...")]
        public float interceptRadius = 6f;
        [Tooltip("...while the truck is slower than this (km/h)...")]
        public float interceptTruckMaxKmh = 8f;
        [Tooltip("...for this many seconds in a row: the run fails. The HUD counts them down.")]
        public float interceptSeconds = 4f;
        [Tooltip("A crew member on foot this close to a moving police car is arrested at once...")]
        public float arrestRadius = 1.8f;
        [Tooltip("...when the car moves faster than this (m/s).")]
        public float arrestCarMinSpeed = 2f;
        [Tooltip("Around a car stopped for officerZoneAfterStop, the officers catch whoever stays within this radius (m)...")]
        public float officerZoneRadius = 5f;
        public float officerZoneAfterStop = 1f;
        [Tooltip("...for this many seconds without a break.")]
        public float officerZoneSeconds = 2f;
        [Tooltip("A hit from the crew truck with at least this impulse (N.s) stuns a police car...")]
        public float policeStunImpulse = 15000f;
        [Tooltip("...for this many seconds (no drive, lights stay on).")]
        public float policeStunSeconds = 3f;
        [Tooltip("Paid per arrested crew member at the end of an escape.")]
        public int finePerArrest = 500;
        [Tooltip("Share of what the truck carries that an escape pays: the contract is void, the goods go at a fence's price. Keep a clean delivery the better plan.")]
        [Range(0f, 1f)] public float escapeCargoPayFraction = 0.5f;
        [Tooltip("Free crew members aboard the truck in the exit for the escape to count.")]
        public int minCrewAboard = 1;

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
