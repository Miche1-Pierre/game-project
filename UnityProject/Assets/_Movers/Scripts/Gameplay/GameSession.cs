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
    // The police (ADR-013). In a scene with an EscapeMission, her call does not end the run: it
    // stays InProgress while Session.Phase goes Job, PoliceIncoming (the countdown to the lead
    // car's arrival, looting allowed, the mission clock stopped), then PoliceHere (fleeTimeLimit
    // to reach the exit, then the house is surrounded). EscapeMission decides arrests,
    // interception and the exit, and reports here: this class still writes every field of
    // Session. Without an EscapeMission (Tutorial_01) the call fails the run after the countdown.
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
        // Seconds to the police's arrival (the lead car), 0 once they are here, -1 before the call.
        public float PoliceIn => PoliceCalled ? Mathf.Max(0f, policeAt - Time.time) : -1f;
        // The scene has a flee for the police call (EscapeMission) and the tuning wants it.
        public bool HasEscape => mission != null && mission.isActiveAndEnabled && !Numbers.policeEndsRun;
        public EscapeMission Mission => mission;
        public bool IntroCardShowing => cardShowing;
        // The card is gone and the crew gets its hands back on the next frame (HideIntroCard).
        public bool CrewReleasePending => releaseFrame >= 0;
        // The session keeps this member frozen: closing a pause menu must not give the controls
        // back (HudPauseMenu, RemoteInputSource). An arrested member stays frozen for the run.
        public bool Holds(CrewMember m) =>
            Session.IsOver || IntroCardShowing || CrewReleasePending || (m != null && Session.IsArrested(m.index));
        // PoliceHere: seconds of escape left before the house is surrounded, else -1 (EscapeMission).
        public float FleeLeft => Session.Phase == MissionPhase.PoliceHere && fleeUntil >= 0f ? Mathf.Max(0f, fleeUntil - Time.time) : -1f;
        // Seconds before the police stop the truck while that warning is on, else -1 (EscapeMission).
        public float InterceptLeft => interceptUntil >= 0f ? Mathf.Max(0f, interceptUntil - Time.time) : -1f;
        // The police are the clock now: the mission timer waits (both machines).
        bool ClockStopped => Session.Phase != MissionPhase.Job && Numbers.stopClockOnPolice;
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
        float fleeUntil = -1f, interceptUntil = -1f;
        bool surrounded;
        EscapeMission mission;
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
            mission = SceneLookup.Find<EscapeMission>(gameObject.scene);
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
            Session.ResetMission();
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
            Session.ResetMission();
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
            // Online client (NETCODE_SLICE 11.7): the host runs the session. Here only the display
            // clock ticks; the E that restarts travels in the raw input frame to the host.
            if (!Net.HasAuthority)
            {
                TickReplicaClock();
                return;
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

            if (TickPolice()) return;
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
            if (!Net.HasAuthority) return false;
            if (!Session.IsRunning || PoliceCalled) return false;
            if (contract == null || !contract.AllRequiredLoaded()) return false;
            Complete(by, at);
            return true;
        }

        // F6: settle now, whatever is loaded.
        public void ForceComplete()
        {
            if (!Net.HasAuthority || Session.IsOver) return;
            Complete(Actors.World, contract != null && contract.truck != null ? contract.truck.transform.position : transform.position);
        }

        public void Fail(FailReason reason)
        {
            if (!Net.HasAuthority || Session.IsOver) return;
            HideIntroCard();
            Session.Failure = reason;
            Result = Settlement.ForFailure(reason, Ledger, reason == FailReason.Intercepted && surrounded);
            if (Net.IsHost) SessionSync.SendSettlement(this);
            SetState(SessionState.Failed);
        }

        // ---- the police flee: EscapeMission reports here (host) ----

        // A crew member on foot caught by a car or an officer zone: frozen for the rest of the run
        // (Holds keeps the mute through pauses), fined at the end. Everyone arrested fails.
        public void Arrest(CrewMember m)
        {
            if (!Net.HasAuthority || Session.IsOver || m == null || m.index < 0 || m.index > 7) return;
            if (Session.IsArrested(m.index)) return;
            byte bit = (byte)(1 << m.index);
            Session.ArrestedMask |= bit;
            Session.ArrestPendingMask &= (byte)~bit;
            if (m.Grab != null) m.Grab.Release(false);
            if (m.Input != null) m.Input.Muted = true;
            if (Net.IsHost) SessionSync.SendState(this);
            WorldEvents.Raise(WorldEventType.CrewArrested, m.transform.position, m.index, 0f, 0f, 0, m);
            if (AllCrewArrested()) Fail(FailReason.CrewArrested);
        }

        // The members an officer zone is about to arrest (the HUD's "CAUGHT" ring). Sent on change.
        public void SetArrestPending(byte mask)
        {
            if (!Net.HasAuthority || Session.IsOver) return;
            mask &= (byte)~Session.ArrestedMask;
            if (mask == Session.ArrestPendingMask) return;
            Session.ArrestPendingMask = mask;
            if (Net.IsHost) SessionSync.SendState(this);
        }

        // The "BLOCKED!" warning: on with the seconds left before the truck is stopped, off with
        // a negative value. Only the edges are sent; each machine counts down by itself.
        public void SetInterceptWarning(float secondsLeft)
        {
            if (!Net.HasAuthority || Session.IsOver) return;
            bool on = secondsLeft >= 0f;
            if (on == interceptUntil >= 0f) return;
            interceptUntil = on ? Time.time + secondsLeft : -1f;
            if (Net.IsHost) SessionSync.SendState(this);
        }

        // surrounded: the flee time ran out; else a car stopped the truck.
        public void FailIntercepted(bool surrounded)
        {
            if (!Net.HasAuthority || Session.IsOver) return;
            this.surrounded = surrounded;
            var truck = mission != null ? mission.Truck : null;
            WorldEvents.Raise(WorldEventType.TruckIntercepted, truck != null ? truck.transform.position : transform.position,
                              Actors.World, 0f, surrounded ? 1f : 0f, 0, truck);
            Fail(FailReason.Intercepted);
        }

        // The truck reached the exit with crew aboard. aboard: bit i, member i was aboard (and
        // free). The others are fined like the arrested ones.
        public void CompleteEscape(int by, Vector3 at, int aboard)
        {
            if (!Net.HasAuthority || Session.IsOver) return;
            HideIntroCard();
            Ledger.FinalizeAll(House);
            int fined = 0;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (!Mine(m) || m.index < 0 || m.index > 7) continue;
                if (Session.IsArrested(m.index) || (aboard & (1 << m.index)) == 0) fined++;
            }
            Result = Settlement.ForEscape(contract != null ? contract.Tracker : null, Ledger, BrokeIn, BreakInWhat,
                                          aboard, fined, Numbers);
            if (contract != null) contract.money = Result.Total;
            Session.Escaped = true;
            if (Net.IsHost) SessionSync.SendSettlement(this);
            WorldEvents.Raise(WorldEventType.EscapeReached, at, by, 0f, 0f, Result.Total,
                              mission != null ? mission.Truck : null);
            SetState(SessionState.Completed);
        }

        // ---- inside ----

        // Her call. With a flee in the scene the police come and the run goes on; without one
        // it fails at the end of the countdown (TickPolice).
        void CallPolice()
        {
            if (!HasEscape)
            {
                policeAt = Time.time + Numbers.policeCountdown;
                if (Net.IsHost) SessionSync.SendState(this);
                return;
            }
            // Called before the keys: the job starts now, so the flee runs InProgress (the HUD
            // past "keys first", nobody frozen by the card).
            if (Session.State == SessionState.Intro) StartContract(Actors.World, false, "", "");
            policeAt = Time.time + mission.Countdown(Numbers);
            SetPhase(MissionPhase.PoliceIncoming);
        }

        // true: the run just ended.
        bool TickPolice()
        {
            if (!PoliceCalled) return false;
            switch (Session.Phase)
            {
                case MissionPhase.Job:
                    if (Time.time < policeAt) return false;
                    Fail(FailReason.PoliceCalled);   // no flee in this scene
                    return true;
                case MissionPhase.PoliceIncoming:
                    if (Time.time >= policeAt) PoliceArrive();
                    return false;
                default:
                    if (fleeUntil < 0f || Time.time < fleeUntil) return false;
                    FailIntercepted(true);
                    return true;
            }
        }

        // The lead car is at the house, on time by construction (EscapeMission sends it early
        // enough): the escape time starts.
        void PoliceArrive()
        {
            fleeUntil = Time.time + (mission != null ? mission.FleeTime(Numbers) : Numbers.fleeTimeLimit);
            SetPhase(MissionPhase.PoliceHere);
            var lead = mission != null ? mission.LeadCarBody : null;
            WorldEvents.Raise(WorldEventType.PoliceArrived, lead != null ? lead.position : transform.position,
                              Actors.World, 0f, 0f, 0, lead);
        }

        void SetPhase(MissionPhase p)
        {
            if (Session.Phase == p) return;
            Session.Phase = p;
            if (Net.IsHost) SessionSync.SendState(this);
        }

        bool AllCrewArrested()
        {
            var all = CrewRoster.All;
            bool any = false;
            for (int i = 0; i < all.Count; i++)
            {
                if (!Mine(all[i])) continue;
                any = true;
                if (!Session.IsArrested(all[i].index)) return false;
            }
            return any;
        }

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
            if (Net.IsHost) SessionSync.SendSettlement(this);
            WorldEvents.Raise(WorldEventType.ContractDelivered, at, by, 0f, 0f, Result.Total, this);
            SetState(SessionState.Completed);
        }

        void OnWorldEvent(WorldEvent e)
        {
            if (!Net.HasAuthority) return;   // the client's session is a replica (ApplyReplica)
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
                    if (!PoliceCalled) CallPolice();
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
            if (Session.TimeLimit <= 0f || ClockStopped) return;   // no limit on this contract, or the police
            Session.TimeLeft = Mathf.Max(0f, Session.TimeLeft - Time.deltaTime);
            if (Session.TimeLeft <= 0f && failOnTimeUp) Fail(FailReason.TimeUp);
        }

        void UpdateIntroCard()
        {
            // Online, the card stays up until the client has the world (NETCODE_SLICE 3.3).
            if (Net.IsHost && Net.PeerConnected && !Net.PeerReady) return;
            var n = Numbers;
            float age = IntroCardAge;
            // Read every frame, so the edge is fresh; honoured only after the short delay. The
            // card waits for that press (Pierre, feedback 1: the job is read, then validated),
            // unless the tuning turns the wait off.
            bool skip = skipInput.Pressed(CrewButton.Interact, CrewButton.Jump) && age >= n.introCardSkipAfter;
            bool timedOut = !n.introCardWaitsForKey && age >= n.introCardSeconds;
            if (skip || timedOut) HideIntroCard();
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
            if (Net.IsHost) SessionSync.SendState(this);
        }

        void SetState(SessionState s)
        {
            Session.State = s;
            if (Session.IsOver)
            {
                // No warning outlives the run (the HUD reads them from the State sent below).
                Session.ArrestPendingMask = 0;
                interceptUntil = -1f;
                overSince = Time.unscaledTime;
                restartInput.Reset();
                releaseFrame = -1;
                SetCrewFrozen(true);
            }
            if (Net.IsHost) SessionSync.SendState(this);
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
        // An arrested member stays muted whatever the card or the end screen do.
        void SetCrewFrozen(bool on)
        {
            frozen = on;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
                if (Mine(all[i])) all[i].Input.Muted = on || Session.IsArrested(all[i].index);
        }

        void OnCrewJoined(CrewMember m)
        {
            if ((frozen || (m != null && Session.IsArrested(m.index))) && Mine(m)) m.Input.Muted = true;
        }

        bool Mine(CrewMember m)
        {
            return m != null && m.Input != null && m.gameObject.scene == gameObject.scene;
        }

        // Test hook, called with SendMessage: brings the clock close to the end.
        void DebugSetTimeLeft(float seconds)
        {
            if (Net.HasAuthority && Session.IsRunning) Session.TimeLeft = Mathf.Max(0f, seconds);
        }

        // Test hook, called with SendMessage: the grandmother's call, as if her patience ran out.
        void DebugCallPolice()
        {
            if (Net.HasAuthority && !Session.IsOver && !PoliceCalled)
                WorldEvents.Raise(WorldEventType.GrandmaCalledPolice, transform.position, Actors.Grandma);
        }

        // Test hook, called with SendMessage (tests/skip_intro_card.cs): the card goes and the
        // crew is free at once, so another module's input-driven test is not eaten by the mute.
        // No key was pressed, so there is no press to keep out of gameplay (HideIntroCard's
        // one-frame wait). The run stays in the Intro. Also settles a release still pending.
        void DebugSkipIntroCard()
        {
            if (!Net.HasAuthority || Session.IsOver || (!cardShowing && releaseFrame < 0)) return;
            cardShowing = false;
            releaseFrame = -1;
            SetCrewFrozen(false);
            if (Net.IsHost) SessionSync.SendState(this);
        }

        // ---- online client replica (NETCODE_SLICE 11.7) ----

        // The settlement's word for a break-in and the toast's for the way in, as SessionSync
        // sends them (an enum on the wire). Keep in step with StartContract's strings: an unknown
        // one travels as "".
        internal static readonly string[] BreakInWords = { "", "a window", "a door", "a wall" };
        internal static readonly string[] StartedHowWords =
            { "", "broke a window", "broke a door", "broke through a wall", "brought part of the house down", "opened a window" };

        // The host's clock between two Clock records. It never fails the run: the host does.
        void TickReplicaClock()
        {
            if (!Session.IsRunning || Session.TimeLimit <= 0f || ClockStopped) return;
            Session.TimeLeft = Mathf.Max(0f, Session.TimeLeft - Time.deltaTime);
        }

        internal void ApplyClock(float timeLeft)
        {
            if (Net.HasAuthority) return;
            Session.TimeLeft = Mathf.Max(0f, timeLeft);
        }

        internal void ApplyResult(Settlement result)
        {
            if (Net.HasAuthority || result == null) return;
            Result = result;
        }

        // The host's State record, written into the same fields the host has, in the host's
        // order: the card first (HideIntroCard), then the state (SetState). Local times are
        // stamped here. SessionStateChanged is raised once per transition, never forwarded.
        internal void ApplyReplica(in SessionReplica r)
        {
            if (Net.HasAuthority || Current != this) return;
            // The flee first: an arrest or a warning changes no SessionState, so it must be
            // written before the early return below.
            ApplyMissionReplica(r);
            HasIntro = r.hasIntro;
            BrokeIn = r.brokeIn;
            BreakInBy = r.breakInBy;
            BreakInWhat = r.breakInWhat ?? "";
            StartedBy = r.startedBy;
            StartedHow = r.startedHow ?? "";
            policeAt = r.policeRemaining >= 0f ? Time.time + r.policeRemaining : -1f;
            Session.Failure = r.failure;
            Session.TimeLimit = r.timeLimit;
            Session.TimeLeft = r.timeLeft;

            if (r.cardShowing && !cardShowing && !Session.IsOver)
            {
                cardShowing = true;
                cardSince = Time.unscaledTime;
                SetCrewFrozen(true);
            }
            else if (!r.cardShowing) HideIntroCard();

            if (r.state == Session.State) return;
            if (r.state == SessionState.Completed && contract != null)
            {
                // An escape delivers nothing: the contract is void (as on the host).
                if (!r.escaped)
                {
                    contract.Tracker.MarkDelivered();      // from the replicated loaded flags
                    contract.complete = true;
                }
                if (Result != null) contract.money = Result.Total;
            }
            if (r.state == SessionState.ContractStarted) startedFrame = Time.frameCount;
            SetState(r.state);
        }

        void ApplyMissionReplica(in SessionReplica r)
        {
            byte newlyArrested = (byte)(r.arrestedMask & ~Session.ArrestedMask);
            Session.Phase = r.phase;
            Session.ArrestedMask = r.arrestedMask;
            Session.ArrestPendingMask = r.pendingMask;
            Session.Escaped = r.escaped;
            fleeUntil = r.fleeSeconds >= 0f ? Time.time + r.fleeSeconds : -1f;
            interceptUntil = r.interceptTenths != SessionReplica.NoIntercept ? Time.time + r.interceptTenths * 0.1f : -1f;
            // The client drives its own body: the arrest stops it here (Net.LocalMember), and
            // Holds keeps it stopped through the pause menu.
            for (int i = 0; i < 8; i++)
            {
                if ((newlyArrested & (1 << i)) == 0) continue;
                var m = CrewRoster.Get(i);
                if (m != null && m.Input != null) m.Input.Muted = true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            StartedCount = 0;
        }
    }
}
