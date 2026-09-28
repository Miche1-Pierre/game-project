namespace Movers
{
    // The run, from the arrival at the house to the settlement (ADR-009).
    //   Intro            the crew arrives; the grandmother comes to hand over the keys
    //   ContractStarted  one frame: the keys are handed over (or the crew broke in)
    //   InProgress       the timer runs
    //   Completed        delivered; the settlement is on screen
    //   Failed           time ran out, the police stopped the crew, or (no escape in the scene)
    //                    the grandmother called the police
    public enum SessionState { Intro, ContractStarted, InProgress, Completed, Failed }

    // Values travel on the wire: append only.
    //   Intercepted   the police stopped the truck or surrounded the house (EscapeMission)
    //   CrewArrested  every crew member was arrested
    public enum FailReason { None, TimeUp, PoliceCalled, Other, Intercepted, CrewArrested }

    // Where a run stands against the police, inside InProgress (ADR-013). No new SessionState:
    // the run stays InProgress until it is Completed or Failed.
    //   Job             no police: the contract as usual
    //   PoliceIncoming  she called them: the countdown to the lead car's arrival runs
    //   PoliceHere      they arrived: the crew has the flee time to reach the exit
    public enum MissionPhase : byte { Job = 0, PoliceIncoming = 1, PoliceHere = 2 }

    // Read-only view of the session for any system. Only GameSession writes it.
    public static class Session
    {
        public static SessionState State { get; internal set; } = SessionState.InProgress;
        public static FailReason Failure { get; internal set; } = FailReason.None;
        public static float TimeLeft { get; internal set; }
        public static float TimeLimit { get; internal set; }

        // The police flee (EscapeMission). Bit i of a mask is crew member i.
        public static MissionPhase Phase { get; internal set; }
        public static byte ArrestedMask { get; internal set; }       // arrested, for the rest of the run
        public static byte ArrestPendingMask { get; internal set; }  // an officer zone is about to arrest them
        public static bool Escaped { get; internal set; }            // Completed through the exit checkpoint

        public static bool IsRunning => State == SessionState.ContractStarted || State == SessionState.InProgress;
        public static bool IsOver => State == SessionState.Completed || State == SessionState.Failed;

        public static bool IsArrested(int member) => member >= 0 && member < 8 && (ArrestedMask & (1 << member)) != 0;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // A scene without a GameSession (Tutorial_01) behaves as a contract in progress.
            State = SessionState.InProgress;
            Failure = FailReason.None;
            TimeLeft = 0f;
            TimeLimit = 0f;
            ResetMission();
        }

        // No police, nobody arrested: every run and every scene starts here (GameSession too).
        internal static void ResetMission()
        {
            Phase = MissionPhase.Job;
            ArrestedMask = 0;
            ArrestPendingMask = 0;
            Escaped = false;
        }
    }
}
