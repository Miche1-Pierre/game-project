using System;
using UnityEngine;

namespace Movers
{
    // The run, from the arrival at the house to the settlement (ADR-009, SLICE_ARCHITECTURE
    // section 6). One per scene, on _Systems. It is the only writer of Session: every other
    // system reads Session or listens to SessionStateChanged.
    //
    //   Intro            the crew waits at the truck, the house is locked, the clock is stopped.
    //                    It ends when the grandmother hands over the keys, or when a player
    //                    gets in without them: breaking a window, a door or a wall (that costs
    //                    money at the end), or opening a window (that only starts the clock).
    //   ContractStarted  one frame, so listeners can tell "it just started" from "it runs"
    //   InProgress       the clock runs: delivering completes, time or the police fail
    //   Completed/Failed the end screen shows the settlement; every player is frozen
    //
    // The ledger of what the crew took lives here, because it only means something when the
    // run ends: at delivery its provisional entries become thefts.
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }
        // Sessions started since Play: a test tells a reloaded scene from the old one by it.
        public static int StartedCount { get; private set; }

        [Tooltip("The contract of this scene. Found in the scene when empty.")]
        public ContractManager contract;

        [Tooltip("The owner of the house (HouseContents/Characters/Grandma). Set and active: the run opens on the Intro and waits for her keys. Empty: the scene's GrandmaBrain is used if there is one; with none (Tutorial_01) there is nobody to get keys from, and the contract starts at once.")]
        public GameObject grandmother;

        [Tooltip("Optional (Assets/_Movers/Data/GameLoopTuning.asset). Empty uses the code defaults.")]
        public GameLoopTuning tuning;

        [Tooltip("The run fails when the clock reaches 0. Off: the clock stops at 0 and the run goes on, as Tutorial_01's clock always did (ContractManager turns it off for a scene that predates the session).")]
        public bool failOnTimeUp = true;

        public GameLoopNumbers Numbers => tuning != null && tuning.numbers != null ? tuning.numbers : GameLoopNumbers.Defaults;
        public TheftLedger Ledger { get; private set; }
        public Settlement Result { get; private set; }          // null until the run ends
        public bool HasIntro { get; private set; }
        public bool BrokeIn { get; private set; }
        public int BreakInBy { get; private set; } = Actors.World;
        public string BreakInWhat { get; private set; } = "";   // "a window", "a door", "a wall"
        // Who started the contract and, when it was not the keys, how ("opened a window").
        public int StartedBy { get; private set; } = Actors.World;
        public string StartedHow { get; private set; } = "";
        public bool PoliceCalled => policeAt >= 0f;
        public float PoliceIn => PoliceCalled ? Mathf.Max(0f, policeAt - Time.time) : -1f;
        public bool IntroCardShowing => cardShowing;
        // The card is gone and the crew gets its hands back on the next frame (HideIntroCard).
        public bool CrewReleasePending => releaseFrame >= 0;
        public float IntroCardAge => Time.unscaledTime - cardSince;
        public float EndAge => Session.IsOver ? Time.unscaledTime - overSince : 0f;
        public bool CanRestart => Session.IsOver && EndAge >= Numbers.restartDelay;
        MovableObject[] House => contract != null ? contract.allObjects : null;

        const float LedgerScanInterval = 0.5f;

        Action<WorldEvent> onWorldEvent;
        Action<CrewMember> onCrewJoined;
        readonly MutedInputReader skipInput = new MutedInputReader();
        readonly MutedInputReader restartInput = new MutedInputReader();

        bool cardShowing, frozen;
        float cardSince, overSince, nextLedgerScan;
        float policeAt = -1f;
        int startedFrame;
        int releaseFrame = -1;

        void Awake()
        {
            // The old session of a scene being reloaded is not a second session: it is on its way
            // out, and every entry point below ignores it once this one is Current.
            if (Current != null && Current != this && Current.gameObject.scene == gameObject.scene)
                Debug.LogWarning("GameSession: a second session in the scene (" + name + "), the newest one runs.");
            Current = this;
            StartedCount++;

            if (contract == null) contract = SceneLookup.Find<ContractManager>(gameObject.scene);
            Ledger = new TheftLedger(Numbers);
            onWorldEvent = OnWorldEvent;
            onCrewJoined = OnCrewJoined;

            // Left empty in the scene: whoever carries the grandmother's brain. Inactive (she was
            // switched off) still means no intro, below.
            if (grandmother == null)
            {
                var brain = SceneLookup.Find<GrandmaBrain>(gameObject.scene);
                if (brain != null) grandmother = brain.gameObject;
            }

            // Session survives a scene reload (it is static), so every run writes it afresh.
            HasIntro = grandmother != null && grandmother.activeInHierarchy;
            Session.Failure = FailReason.None;
            Session.TimeLimit = contract != null ? contract.timeLimit : Numbers.defaultTimeLimit;
            Session.TimeLeft = Session.TimeLimit;
            Session.State = HasIntro ? SessionState.Intro : SessionState.InProgress;
        }

        void OnEnable()
        {
            WorldEvents.Subscribe(onWorldEvent);
            CrewRoster.Joined += onCrewJoined;
        }

        void OnDisable()
        {
            WorldEvents.Unsubscribe(onWorldEvent);
            CrewRoster.Joined -= onCrewJoined;
            // Never leave the crew frozen behind a session that is gone.
            releaseFrame = -1;
            if (frozen) SetCrewFrozen(false);
        }

        void OnDestroy()
        {
            if (Current != this) return;
            Current = null;
            // What a scene without a session reads (Session's own defaults).
            Session.State = SessionState.InProgress;
            Session.Failure = FailReason.None;
            Session.TimeLeft = 0f;
            Session.TimeLimit = 0f;
        }

        void Start()
        {
            // Every listener subscribed in its OnEnable by now: announce the first state.
            Announce();
            // introCardSeconds 0: no card at all, so nobody is frozen (a test session).
            if (Session.State == SessionState.Intro && Numbers.introCardSeconds > 0f)
            {
                cardShowing = true;
                cardSince = Time.unscaledTime;
                skipInput.Reset();
                SetCrewFrozen(true);
            }
        }

        void Update()
        {
            // Session is static and belongs to the newest run: an old session still alive for a
            // frame during a reload must not tick its clock or read its keys.
            if (Current != this) return;
            if (releaseFrame >= 0 && Time.frameCount >= releaseFrame)
            {
                releaseFrame = -1;
                if (!Session.IsOver) SetCrewFrozen(false);
            }

            switch (Session.State)
            {
                case SessionState.Intro:
                    if (cardShowing) UpdateIntroCard();
                    break;
                case SessionState.ContractStarted:
                    if (Time.frameCount > startedFrame) SetState(SessionState.InProgress);
                    break;
                case SessionState.InProgress:
                    TickClock();
                    break;
                default:
                    // The end screen: E restarts once the delay is over. A key held since the
                    // delivery is not a press (the reader sees edges only).
                    if (restartInput.Pressed(CrewButton.Interact) && CanRestart) SceneReload.Reload();
                    return;
            }
            if (Session.IsOver) return;

            if (PoliceCalled && Time.time >= policeAt)
            {
                Fail(FailReason.PoliceCalled);
                return;
            }
            if (Time.time >= nextLedgerScan)
            {
                nextLedgerScan = Time.time + LedgerScanInterval;
                Ledger.Reconcile(House);
            }
        }

        // ---- what the rest of the game calls ----

        // The DeliverPoint, once everything left on the list is in the truck.
        public bool Deliver(int by, Vector3 at)
        {
            if (!Session.IsRunning || PoliceCalled) return false;
            if (contract == null || !contract.AllRequiredLoaded()) return false;
            Complete(by, at);
            return true;
        }

        // F6: settle now, whatever is loaded.
        public void ForceComplete()
        {
            if (Session.IsOver) return;
            Complete(Actors.World, contract != null && contract.truck != null ? contract.truck.transform.position : transform.position);
        }

        public void Fail(FailReason reason)
        {
            if (Session.IsOver) return;
            HideIntroCard();
            Session.Failure = reason;
            Result = Settlement.ForFailure(reason, Ledger);
            SetState(SessionState.Failed);
        }

        // ---- inside ----

        void Complete(int by, Vector3 at)
        {
            HideIntroCard();
            Ledger.FinalizeAll(House);                 // raises ItemStolen for each item taken
            if (contract != null) contract.Tracker.MarkDelivered();
            Result = Settlement.ForDelivery(contract != null ? contract.Tracker : null, Ledger,
                                            BrokeIn, BreakInWhat, Numbers);
            if (contract != null)
            {
                contract.money = Result.Total;
                contract.complete = true;
            }
            WorldEvents.Raise(WorldEventType.ContractDelivered, at, by, 0f, 0f, Result.Total, this);
            SetState(SessionState.Completed);
        }

        void OnWorldEvent(WorldEvent e)
        {
            if (Current != this || Session.IsOver) return;
            Ledger.Handle(e);
            switch (e.type)
            {
                case WorldEventType.KeysHandedOver:
                    if (Session.State == SessionState.Intro) StartContract(e.instigator, false, "", "");
                    break;
                case WorldEventType.WindowBroken:
                case WorldEventType.DoorBroken:
                case WorldEventType.StructureDamaged:
                case WorldEventType.StructureCollapsed:
                case WorldEventType.WindowOpened:
                    if (Session.State == SessionState.Intro) OnWayInBeforeKeys(e);
                    break;
                case WorldEventType.GrandmaCalledPolice:
                    if (!PoliceCalled) policeAt = Time.time + Numbers.policeCountdown;
                    break;
            }
        }

        // The Intro has no clock, so any way into the house before the keys starts the job:
        // otherwise the crew could load everything with the clock stopped and only then talk to
        // her. Breaking a window, a door or a wall is a break-in, billed at the end (section 6).
        // Opening a window breaks nothing: the clock starts, nothing is billed. Only a player's
        // doing counts: a pane a chain reaction broke, or the grandmother airing her room, is
        // not the crew letting itself in. A wall that is only cracked is not a way in either.
        // First guesses, an open question for Pierre (INTEGRATION.md, report).
        void OnWayInBeforeKeys(in WorldEvent e)
        {
            if (!Actors.IsPlayer(e.instigator)) return;
            switch (e.type)
            {
                case WorldEventType.WindowBroken: StartContract(e.instigator, true, "a window", "broke a window"); break;
                case WorldEventType.DoorBroken: StartContract(e.instigator, true, "a door", "broke a door"); break;
                case WorldEventType.StructureDamaged:
                    if (e.magnitude >= (float)DestructionState.Destroyed) StartContract(e.instigator, true, "a wall", "broke through a wall");
                    break;
                case WorldEventType.StructureCollapsed: StartContract(e.instigator, true, "a wall", "brought part of the house down"); break;
                case WorldEventType.WindowOpened: StartContract(e.instigator, false, "", "opened a window"); break;
            }
        }

        // what: the settlement's word for a break-in; how: the toast's, empty for the keys.
        void StartContract(int by, bool breakIn, string what, string how)
        {
            if (breakIn)
            {
                BrokeIn = true;
                BreakInBy = by;
                BreakInWhat = what;
            }
            StartedBy = by;
            StartedHow = how;
            HideIntroCard();
            startedFrame = Time.frameCount;
            SetState(SessionState.ContractStarted);
        }

        void TickClock()
        {
            if (Session.TimeLimit <= 0f) return;   // no limit on this contract
            Session.TimeLeft = Mathf.Max(0f, Session.TimeLeft - Time.deltaTime);
            if (Session.TimeLeft <= 0f && failOnTimeUp) Fail(FailReason.TimeUp);
        }

        void UpdateIntroCard()
        {
            var n = Numbers;
            float age = IntroCardAge;
            // Read every frame, so the edge is fresh; honoured only after the short delay.
            bool skip = skipInput.Pressed(CrewButton.Interact, CrewButton.Jump) && age >= n.introCardSkipAfter;
            if (skip || age >= n.introCardSeconds) HideIntroCard();
        }

        void HideIntroCard()
        {
            if (!cardShowing) return;
            cardShowing = false;
            // Unmuted next frame, not now. The press that skipped the card is a "down" in
            // CrewInput for the rest of this frame, and PlayerController and PlayerInteract run
            // after the session: unmuted now, Space would also jump and E would also use
            // whatever is under the crosshair. Next frame the key is only held.
            if (!Session.IsOver) releaseFrame = Time.frameCount + 1;
        }

        void SetState(SessionState s)
        {
            Session.State = s;
            if (Session.IsOver)
            {
                overSince = Time.unscaledTime;
                restartInput.Reset();
                releaseFrame = -1;
                SetCrewFrozen(true);
            }
            Announce();
        }

        void Announce()
        {
            WorldEvents.Raise(WorldEventType.SessionStateChanged, transform.position, Actors.World,
                              0f, (float)Session.State, 0, this);
        }

        // Muting is the one switch that freezes a player whatever they drive (ADR-009).
        // Only the crew of this session's scene: during a reload the old scene and the new one
        // can overlap for a moment, and the old, finished run must not freeze the new crew.
        void SetCrewFrozen(bool on)
        {
            frozen = on;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
                if (Mine(all[i])) all[i].Input.Muted = on;
        }

        void OnCrewJoined(CrewMember m)
        {
            if (frozen && Mine(m)) m.Input.Muted = true;
        }

        bool Mine(CrewMember m)
        {
            return m != null && m.Input != null && m.gameObject.scene == gameObject.scene;
        }

        // Test hook, called with SendMessage: brings the clock close to the end.
        void DebugSetTimeLeft(float seconds)
        {
            if (Session.IsRunning) Session.TimeLeft = Mathf.Max(0f, seconds);
        }

        // Test hook, called with SendMessage (tests/skip_intro_card.cs): the card goes and the
        // crew is free at once, so another module's input-driven test is not eaten by the mute.
        // No key was pressed, so there is no press to keep out of gameplay (HideIntroCard's
        // one-frame wait). The run stays in the Intro. Also settles a release still pending.
        void DebugSkipIntroCard()
        {
            if (Session.IsOver || (!cardShowing && releaseFrame < 0)) return;
            cardShowing = false;
            releaseFrame = -1;
            SetCrewFrozen(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            StartedCount = 0;
        }
    }
}
