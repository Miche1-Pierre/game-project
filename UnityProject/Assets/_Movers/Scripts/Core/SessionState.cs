namespace Movers
{
    // The run, from the arrival at the house to the settlement (ADR-009).
    //   Intro            the crew arrives; the grandmother comes to hand over the keys
    //   ContractStarted  one frame: the keys are handed over (or the crew broke in)
    //   InProgress       the timer runs
    //   Completed        delivered; the settlement is on screen
    //   Failed           time ran out, or the grandmother called the police
    public enum SessionState { Intro, ContractStarted, InProgress, Completed, Failed }

    public enum FailReason { None, TimeUp, PoliceCalled, Other }

    // Read-only view of the session for any system. Only GameSession writes it.
    public static class Session
    {
        public static SessionState State { get; internal set; } = SessionState.InProgress;
        public static FailReason Failure { get; internal set; } = FailReason.None;
        public static float TimeLeft { get; internal set; }
        public static float TimeLimit { get; internal set; }

        public static bool IsRunning => State == SessionState.ContractStarted || State == SessionState.InProgress;
        public static bool IsOver => State == SessionState.Completed || State == SessionState.Failed;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // A scene without a GameSession (Tutorial_01) behaves as a contract in progress.
            State = SessionState.InProgress;
            Failure = FailReason.None;
            TimeLeft = 0f;
            TimeLimit = 0f;
        }
    }
}
