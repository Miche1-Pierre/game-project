using System;
using UnityEngine;

namespace Movers
{
    public enum GrandmaState { Intro, GiveKeys, Routine, PerformActivity, Observe, Investigate, React, Confront, CallPolice }

    // Counters for the debug overlay and the Play-mode tests.
    [Serializable]
    public struct GrandmaStats
    {
        public int observations, investigations, reactions, confrontations, theftsWitnessed, bumps;
    }

    // The grandmother's mind: a small state machine (ADR-009, SLICE_ARCHITECTURE section 8).
    //
    //   Intro            she waits on the front porch for the crew to come and talk
    //   GiveKeys         her two or three lines, a house rule, the keys: KeysHandedOver
    //   Routine          walking to her next activity
    //   PerformActivity  sitting, reading, cooking... (GrandmaActivities)
    //   Observe          a small noise: she turns to look
    //   Investigate      she heard something break: she goes to see, and suspects whoever is near
    //   React            she saw it: she faces the culprit, says so, shakes her fist
    //   Confront         at the end of her patience she follows the worst offender and scolds
    //                    him, and he drops what of hers he is holding
    //   CallPolice       patience 0: GrandmaCalledPolice, once, and the run fails
    //
    // Senses feed her Stimulus values, the mood table prices them, and this decides what she
    // does. She announces the facts other systems care about as world events (KeysHandedOver,
    // TheftWitnessed, GrandmaNoticed, GrandmaBumped, GrandmaCalledPolice); nobody calls her.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GrandmaMover), typeof(GrandmaSenses), typeof(GrandmaMood))]
    [RequireComponent(typeof(GrandmaActivities), typeof(GrandmaAnimation), typeof(GrandmaSpeech))]
    public sealed class GrandmaBrain : MonoBehaviour
    {
        [Header("Intro")]
        public bool startWithIntro = true;
        public float greetRange = 8f;
        public float greetInterval = 12f;

        [Header("Timing, seconds")]
        public float observeSeconds = 2f;
        public float reactSeconds = 2.8f;
        public float investigateTimeout = 30f;
        public float lookAroundSeconds = 3.5f;
        public float pauseBetweenActivities = 1.2f;
        public float talkCooldown = 3f;
        [Tooltip("Metres from a spot's stand point that still count as there. Further: her way was blocked.")]
        public float arriveTolerance = 1f;
        [Tooltip("Seconds on one walk before she gives up on it and picks something else (the longest walk in the house takes about 70 s).")]
        public float routineWalkGiveUp = 120f;

        [Header("Investigate")]
        [Tooltip("Breakage she only heard, costing at least this much patience, sends her to look. Less: she turns her head.")]
        public float investigateMinCost = 4f;
        [Tooltip("Blame a player she finds near a noise gets, even if the noise had no known culprit.")]
        public float suspectBlame = 4f;
        public float suspectRange = 7f;

        [Header("Confront")]
        public float confrontReach = 1.6f;
        public float confrontGiveUp = 20f;
        public float confrontCooldown = 25f;

        public GrandmaMover mover;
        public GrandmaSenses senses;
        public GrandmaMood mood;
        public GrandmaActivities activities;
        public GrandmaAnimation anim;
        public GrandmaSpeech speech;
        public GrandmaProps props;

        public GrandmaState State { get; private set; } = GrandmaState.Intro;
        public float StateTime => Time.time - stateStart;
        public bool AIEnabled { get; private set; } = true;
        public bool KeysGiven { get; private set; }
        public bool PoliceCalled { get; private set; }
        public int ConfrontTarget { get; private set; } = Actors.World;
        public ActivitySpot RoutineTarget { get; private set; }
        public Stimulus LastStimulus { get; private set; }
        public float LastStimulusTime { get; private set; } = -99f;
        public GrandmaStats Stats => stats;
        public bool CanTalk => AIEnabled && !sessionOver && State != GrandmaState.GiveKeys;
        public bool SessionOver => sessionOver;
        public bool HasWitnessed(MovableObject item) => senses != null && senses.HasWitnessed(item);
        // Where she waits for the crew: her position and facing when the scene starts.
        public Vector3 IntroPosition => introPosition;
        public Vector3 IntroFacing => introFacing;

        const int QueueSize = 16;
        readonly Stimulus[] queue = new Stimulus[QueueSize];
        readonly Stimulus[] batch = new Stimulus[QueueSize];
        int queued;

        GrandmaStats stats;
        float stateStart;
        Vector3 introPosition;
        Vector3 introFacing;
        int talker;
        int step;
        float stepUntil;
        float nextGreet;
        float lastTalk = -99f;
        Vector3 focusPoint;
        int focusPlayer = Actors.World;
        float stateUntil;
        bool moveIssued;
        // When the current walk was ordered. Not StateTime: Routine loops through many walks
        // (wander, retry, next spot) without leaving the state.
        float moveIssuedAt;
        float introRetryAt;
        bool looking;
        float lookStart;
        bool scolded;
        float confrontReadyAt;
        float nextRepath;
        float nextPhoneLine;
        float routinePause;
        int routineFails;
        bool wandering;
        float nextConfrontCheck;
        float lastBreakInHeard = -99f;
        bool policePending, sessionOver, sessionOverPending, startRoutinePending;
        SessionState endedAs;

        Action<Stimulus> onPerceived;
        Action onPatienceGone, onLastWarning, onWarningSurvived;
        Action<ActivitySpot, MovableObject> onSeatTaken;
        Action<CrewMember> onBlocked;
        Action<WorldEvent> onWorldEvent;

        void Awake()
        {
            if (mover == null) mover = GetComponent<GrandmaMover>();
            if (senses == null) senses = GetComponent<GrandmaSenses>();
            if (mood == null) mood = GetComponent<GrandmaMood>();
            if (activities == null) activities = GetComponent<GrandmaActivities>();
            if (anim == null) anim = GetComponent<GrandmaAnimation>();
            if (speech == null) speech = GetComponent<GrandmaSpeech>();
            if (props == null) props = GetComponent<GrandmaProps>();

            onPerceived = Enqueue;
            onPatienceGone = () => policePending = true;
            onLastWarning = OnLastWarning;
            onWarningSurvived = () => speech.Say(Line.LastChance);
            onSeatTaken = OnSeatTaken;
            onBlocked = OnBlocked;
            onWorldEvent = OnWorldEvent;
        }

        void OnEnable()
        {
            if (senses != null) senses.Perceived += onPerceived;
            if (mood != null) { mood.ReachedZero += onPatienceGone; mood.LastWarning += onLastWarning; mood.WarningSurvived += onWarningSurvived; }
            if (activities != null) activities.SeatTaken += onSeatTaken;
            if (mover != null) mover.BlockedBy += onBlocked;
            WorldEvents.Subscribe(onWorldEvent);
        }

        void OnDisable()
        {
            if (senses != null) senses.Perceived -= onPerceived;
            if (mood != null) { mood.ReachedZero -= onPatienceGone; mood.LastWarning -= onLastWarning; mood.WarningSurvived -= onWarningSurvived; }
            if (activities != null) activities.SeatTaken -= onSeatTaken;
            if (mover != null) mover.BlockedBy -= onBlocked;
            WorldEvents.Unsubscribe(onWorldEvent);
        }

        void Start()
        {
            introPosition = transform.position;
            introFacing = mover.Forward;
            if (startWithIntro) Enter(GrandmaState.Intro);
            else
            {
                KeysGiven = true;
                Enter(GrandmaState.Routine);
            }
        }

        // ---------------------------------------------------------------- public orders

        // GrandmaTalk: a player pressed E on her.
        public void OnTalk(int player)
        {
            if (!Net.HasAuthority) return;   // online client: the host's P2 talks to her
            if (!CanTalk || Time.time - lastTalk < talkCooldown) return;
            lastTalk = Time.time;
            // The handover is what the whole intro waits for, so it wins over whatever lesser
            // thing she is busy with before it: turning to a rattled door, grumbling about a
            // bump on the porch, going to look at a noise.
            if (!KeysGiven && !PoliceCalled && Rank(State) < Rank(GrandmaState.GiveKeys))
            {
                talker = Actors.IsPlayer(player) ? player : 0;
                anim.StopUpper();
                mover.ClearFace();
                Enter(GrandmaState.GiveKeys);   // stops the mover
                return;
            }
            if (State == GrandmaState.CallPolice) { speech.Say(Line.OnThePhone); return; }
            bool grumpy = State == GrandmaState.Confront || mood.Tier >= MoodTier.Angry;
            speech.Say(grumpy ? Line.ChatGrumpy : Line.Chat);
        }

        // Shift+F4. Off: she stands still, perceives nothing and her patience is frozen.
        public void SetAIEnabled(bool on)
        {
            if (AIEnabled == on) return;
            AIEnabled = on;
            if (senses != null) senses.asleep = !on;
            if (mood != null) mood.Frozen = !on;
            if (!on)
            {
                queued = 0;
                mover.Stop();
                mover.ClearFace();
                activities.Interrupt(true);
                anim.StopUpper();
                speech.Hush();
            }
            else Resume();
        }

        // Debug and tests: back to waiting on the porch for someone to talk to her.
        public void BeginIntro()
        {
            KeysGiven = false;
            Enter(GrandmaState.Intro);
        }

        // Debug and tests: straight to her routine, as if the keys had been handed over (no event).
        public void SkipIntro()
        {
            KeysGiven = true;
            if (State == GrandmaState.Intro || State == GrandmaState.GiveKeys) Enter(GrandmaState.Routine);
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            // Online client: the host runs her mind; here she only walks as she is streamed.
            if (!Net.HasAuthority) { anim.SetSpeed(mover.CurrentSpeed); return; }
            float dt = Time.deltaTime;
            // An activity being left (standing up) finishes whatever she is doing now.
            if (activities.IsBusy && (State != GrandmaState.PerformActivity || !AIEnabled || sessionOver)) activities.Tick(dt);
            anim.SetSpeed(mover.CurrentSpeed);
            if (!AIEnabled) return;

            HandleSession();
            if (sessionOver) return;

            ProcessQueue();
            if (policePending)
            {
                policePending = false;
                Enter(GrandmaState.CallPolice);
            }
            speech.pitchBoost = (int)mood.Tier * 0.07f;

            // At the end of her patience she drops what she is doing and goes after the worst
            // offender, even if nothing new happened.
            if ((State == GrandmaState.Routine || State == GrandmaState.PerformActivity) && Time.time >= nextConfrontCheck)
            {
                nextConfrontCheck = Time.time + 1f;
                TryConfront();
            }

            switch (State)
            {
                case GrandmaState.Intro: TickIntro(); break;
                case GrandmaState.GiveKeys: TickGiveKeys(); break;
                case GrandmaState.Routine: TickRoutine(); break;
                case GrandmaState.PerformActivity: TickActivity(dt); break;
                case GrandmaState.Observe: TickObserve(); break;
                case GrandmaState.Investigate: TickInvestigate(); break;
                case GrandmaState.React: TickReact(); break;
                case GrandmaState.Confront: TickConfront(); break;
                case GrandmaState.CallPolice: TickPolice(); break;
            }
        }

        void Enter(GrandmaState next)
        {
            if (State == GrandmaState.PerformActivity && next != GrandmaState.PerformActivity) activities.Interrupt(false);
            State = next;
            stateStart = Time.time;
            moveIssued = false;
            looking = false;

            switch (next)
            {
                case GrandmaState.Intro:
                    mover.ClearFace();
                    break;
                case GrandmaState.GiveKeys:
                    mover.Stop();
                    step = 0;
                    stepUntil = 0f;
                    break;
                case GrandmaState.Routine:
                    // RoutineTarget survives an interruption: after turning to look at a
                    // noise she carries on to where she was going.
                    routineFails = 0;
                    wandering = false;
                    mover.ClearFace();
                    break;
                case GrandmaState.React:
                    mover.Stop();
                    break;
                case GrandmaState.Observe:
                    mover.Stop();
                    mover.Face(focusPoint);
                    stats.observations++;
                    stateUntil = Time.time + observeSeconds;
                    if (UnityEngine.Random.value < 0.4f) speech.Say(Line.SmallNoise);
                    break;
                case GrandmaState.CallPolice:
                    mover.Stop();
                    if (!PoliceCalled)
                    {
                        PoliceCalled = true;
                        speech.Say(Line.Police);
                        anim.PlayUpper(GrandmaAnimation.ShakeFist);
                        WorldEvents.Raise(WorldEventType.GrandmaCalledPolice, transform.position, mood.WorstOffender());
                    }
                    nextPhoneLine = Time.time + 6f;
                    break;
            }
        }

        // Whatever she was interrupted by is over: back to what she should be doing.
        void Resume()
        {
            mover.ClearFace();
            anim.StopUpper();
            if (PoliceCalled) { Enter(GrandmaState.CallPolice); return; }
            if (!KeysGiven) { Enter(GrandmaState.Intro); return; }
            if (TryConfront()) return;
            Enter(GrandmaState.Routine);
        }

        // Her patience just ran out: she says it loud and goes after the worst offender, but
        // she does not call yet (GrandmaMood.LastWarning).
        void OnLastWarning()
        {
            if (!AIEnabled || sessionOver || PoliceCalled) return;
            speech.Say(Line.LastWarning);
            anim.PlayUpper(GrandmaAnimation.Point);
            confrontReadyAt = 0f;
            if (State != GrandmaState.Confront) TryConfront();
        }

        // Below the last threshold, and not just after the previous telling-off.
        bool TryConfront()
        {
            if (mood.Patience >= mood.Costs.furiousBelow || Time.time < confrontReadyAt) return false;
            int target = mood.WorstOffender();
            if (target == Actors.World || CrewRoster.Get(target) == null) return false;
            ConfrontTarget = target;
            scolded = false;
            nextRepath = 0f;
            stats.confrontations++;
            speech.Say(Line.Confront);
            anim.PlayUpper(GrandmaAnimation.Point);
            Enter(GrandmaState.Confront);
            return true;
        }

        float Speed => mood.Tier >= MoodTier.Angry ? mover.hurrySpeed : mover.walkSpeed;

        // ---------------------------------------------------------------- stimuli

        void Enqueue(Stimulus s)
        {
            if (!AIEnabled || sessionOver || queued >= QueueSize) return;
            queue[queued++] = s;
        }

        // Every stimulus is priced; the worst one of the frame decides what she does.
        void ProcessQueue()
        {
            if (queued == 0) return;
            // Pricing raises world events; a listener may make her perceive more. Those wait
            // for the next frame instead of changing the batch under the loop.
            int n = queued;
            Array.Copy(queue, batch, n);
            queued = 0;

            int best = -1;
            float bestScore = 0f, bestLost = 0f;
            for (int i = 0; i < n; i++)
            {
                Stimulus s = batch[i];
                float cost = mood.CostOf(s);
                float lost = cost > 0f ? mood.Apply(cost, s.instigator, s.position) : 0f;

                if (s.kind == StimulusKind.TheftWitnessed)
                {
                    stats.theftsWitnessed++;
                    WorldEvents.Raise(WorldEventType.TheftWitnessed, s.position, s.instigator, 0f, 0f, s.value, s.item);
                }
                if (s.kind == StimulusKind.Bumped && cost > 0f)
                {
                    stats.bumps++;
                    WorldEvents.Raise(WorldEventType.GrandmaBumped, transform.position, s.instigator, 0f, 0f, 0, s.item);
                }
                if (lost > 0f && s.kind != StimulusKind.SmallNoise && s.kind != StimulusKind.BehindSchedule)
                    WorldEvents.Raise(WorldEventType.GrandmaNoticed, s.position, s.instigator, 0f, lost, s.value, s.item);
                if (!KeysGiven && (s.kind == StimulusKind.WindowBroken || s.kind == StimulusKind.DoorBroken))
                    lastBreakInHeard = Time.time;

                float score = Priority(s, lost);
                if (score > bestScore) { bestScore = score; best = i; bestLost = lost; }
            }
            Stimulus chosen = best >= 0 ? batch[best] : default;
            if (best < 0 || PoliceCalled || policePending) return;
            LastStimulus = chosen;
            LastStimulusTime = Time.time;
            Respond(chosen, bestLost);
        }

        float Priority(in Stimulus s, float lost)
        {
            switch (s.kind)
            {
                case StimulusKind.TheftWitnessed: return 100f;
                case StimulusKind.SeatTaken: return 60f;
                case StimulusKind.BehindSchedule: return lost > 0f ? 15f : 0f;
                case StimulusKind.SmallNoise: return lost > 0f ? 5f : 0f;
            }
            if (lost <= 0f) return 0f;   // on cooldown: already dealt with
            if (s.IsOffence) return 60f + lost;
            if (s.IsBreakage) return (s.seen ? 50f : 40f) + lost;
            return 10f;
        }

        static int Rank(GrandmaState s)
        {
            switch (s)
            {
                case GrandmaState.CallPolice: return 100;
                case GrandmaState.Confront: return 50;
                case GrandmaState.GiveKeys: return 45;
                case GrandmaState.React: return 40;
                case GrandmaState.Investigate: return 30;
                case GrandmaState.Observe: return 10;
                case GrandmaState.PerformActivity: return 5;
                default: return 0;
            }
        }

        void Respond(in Stimulus s, float lost)
        {
            if (s.kind == StimulusKind.BehindSchedule)
            {
                if (Rank(State) < Rank(GrandmaState.React)) speech.Say(Line.TooSlow);
                return;
            }

            bool smallThing = s.kind == StimulusKind.SmallNoise || (s.IsBreakage && !s.seen && lost < investigateMinCost);
            if (smallThing)
            {
                if (State == GrandmaState.Routine || State == GrandmaState.Intro)
                {
                    focusPoint = s.position;
                    Enter(GrandmaState.Observe);
                }
                else if (State == GrandmaState.PerformActivity && UnityEngine.Random.value < 0.3f) speech.Say(Line.SmallNoise);
                return;
            }

            if (s.IsBreakage && !s.seen)
            {
                if (Rank(GrandmaState.Investigate) < Rank(State)) return;
                focusPoint = s.position;
                focusPlayer = Actors.World;
                stats.investigations++;
                speech.Say(GrandmaLines.For(s), GrandmaLines.NameOf(s.item));
                Enter(GrandmaState.Investigate);
                return;
            }

            if (Rank(GrandmaState.React) < Rank(State)) return;
            BeginReact(s);
        }

        void BeginReact(in Stimulus s)
        {
            focusPoint = s.position;
            focusPlayer = Actors.IsPlayer(s.instigator) ? s.instigator : Actors.World;
            stats.reactions++;
            float said = speech.Say(GrandmaLines.For(s), GrandmaLines.NameOf(s.item));
            anim.PlayUpper(GrandmaAnimation.AngerFor(s.kind));
            Enter(GrandmaState.React);
            stateUntil = Time.time + Mathf.Max(reactSeconds, said);
        }

        void OnSeatTaken(ActivitySpot spot, MovableObject seat)
        {
            int who = seat != null ? seat.RecentHandler(3f) : Actors.World;
            if (who == Actors.World && seat != null && seat.holder != null)
            {
                CrewMember m = CrewRoster.Owner(seat.holder.transform);
                if (m != null) who = m.index;
            }
            Enqueue(new Stimulus(StimulusKind.SeatTaken, seat != null ? seat.transform.position : transform.position, who) { seen = true, item = seat });
        }

        void OnBlocked(CrewMember member)
        {
            if (AIEnabled && !speech.IsSpeaking) speech.Say(Line.ExcuseMe);
        }

        // ---------------------------------------------------------------- states

        void TickIntro()
        {
            if (activities.IsBusy) return;
            Vector3 d = introPosition - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.8f * 0.8f)
            {
                // A failed walk is retried, but not every frame: a path search per frame for a
                // porch she cannot reach would be wasted work.
                bool retry = mover.Status == GrandmaMover.MoveStatus.Failed && Time.time >= introRetryAt;
                if (!moveIssued || retry)
                {
                    mover.MoveTo(introPosition, mover.walkSpeed, 0.3f);
                    moveIssued = true;
                    introRetryAt = Time.time + 2f;
                }
                return;
            }

            // Waiting on the porch, calling out to the crew when she sees them coming.
            if (mover.Status == GrandmaMover.MoveStatus.Moving) mover.Stop();
            Vector3 look = introPosition + introFacing * 5f;
            if (Time.time >= nextGreet)
            {
                for (int p = 0; p < 4; p++)
                {
                    CrewMember m = CrewRoster.Get(p);
                    if (m == null || !senses.Sees(p) || (m.Position - transform.position).sqrMagnitude > greetRange * greetRange) continue;
                    speech.Say(Line.Greeting);
                    nextGreet = Time.time + greetInterval;
                    look = m.Position;
                    break;
                }
            }
            CrewMember near = CrewRoster.Nearest(transform.position, greetRange);
            mover.Face(near != null ? near.Position : look);
        }

        void TickGiveKeys()
        {
            CrewMember m = CrewRoster.Get(talker);
            if (m != null) mover.Face(m.Position);
            if (Time.time < stepUntil) return;
            switch (step++)
            {
                case 0:
                    anim.PlayUpper(GrandmaAnimation.Talk);
                    stepUntil = Time.time + speech.Say(Line.Intro1) + 0.2f;
                    break;
                case 1:
                    stepUntil = Time.time + speech.Say(Line.Intro2) + 0.2f;
                    break;
                case 2:
                    stepUntil = Time.time + speech.Say(Line.HouseRule) + 0.2f;
                    break;
                case 3:
                    // Give_Keys: the hand comes forward, the keys change hands at its middle
                    // (the Handoff frame, 24 of 48 at 24 fps).
                    anim.StopUpper();
                    anim.Play(GrandmaAnimation.GiveKeys);
                    if (props != null) props.Show(GrandmaProp.Keys);
                    speech.Say(Line.HereAreTheKeys);
                    stepUntil = Time.time + 1f;
                    break;
                case 4:
                    if (props != null) props.HideAll();
                    KeysGiven = true;   // before the event: GameSession may answer within the call
                    WorldEvents.Raise(WorldEventType.KeysHandedOver, transform.position, talker);
                    stepUntil = Time.time + 1f;
                    break;
                default:
                    speech.Say(Line.OffYouGo);
                    Enter(GrandmaState.Routine);
                    routinePause = Time.time + 1.5f;
                    break;
            }
        }

        void TickRoutine()
        {
            if (activities.IsBusy || Time.time < routinePause) return;

            if (!moveIssued)
            {
                if (RoutineTarget == null) RoutineTarget = activities.PickNext();
                wandering = RoutineTarget == null || routineFails >= 3;
                Vector3 goal = wandering ? WanderPoint() : RoutineTarget.StandPosition;
                mover.MoveTo(goal, Speed, 0.25f);
                moveIssued = true;
                moveIssuedAt = Time.time;
                return;
            }

            switch (mover.Status)
            {
                case GrandmaMover.MoveStatus.Arrived:
                    if (wandering)
                    {
                        // A little walk, then her spots again: one of them may be free by now.
                        moveIssued = false;
                        routineFails = 0;
                        routinePause = Time.time + 1f;
                        return;
                    }
                    if (RoutineTarget != null && !CloseTo(RoutineTarget.StandPosition))
                    {
                        // The end of a partial path: something stands between her and the spot.
                        BlockedFrom(RoutineTarget);
                        return;
                    }
                    ArriveAtSpot(RoutineTarget);
                    break;
                case GrandmaMover.MoveStatus.Failed:
                case GrandmaMover.MoveStatus.Idle:
                    if (RoutineTarget != null) RoutineTarget.lastUsedTime = Time.time;   // try the others first
                    RoutineTarget = null;
                    routineFails++;
                    moveIssued = false;
                    routinePause = Time.time + 0.5f;
                    break;
                default:
                    // Lost somewhere on this one walk: give it up and pick again. Measured on
                    // the walk itself, so the re-issue happens once, and the mover keeps its
                    // stuck detection, detours and door waits in between (MoveTo resets them).
                    if (Time.time - moveIssuedAt > routineWalkGiveUp)
                    {
                        if (RoutineTarget != null) RoutineTarget.lastUsedTime = Time.time;   // try the others first
                        RoutineTarget = null;
                        moveIssued = false;
                    }
                    break;
            }
        }

        void ArriveAtSpot(ActivitySpot spot)
        {
            moveIssued = false;
            if (spot == null) return;
            ActivitySpot.Availability a = spot.Check(out GameObject missing);
            RoutineTarget = null;
            if (a == ActivitySpot.Availability.Available)
            {
                activities.Begin(spot);
                Enter(GrandmaState.PerformActivity);
                return;
            }
            if (a == ActivitySpot.Availability.Missing && !spot.commented && missing != null)
            {
                spot.commented = true;
                MovableObject mo = missing.GetComponent<MovableObject>();
                bool packed = mo != null && mo.requiredForContract;
                speech.Say(packed ? Line.MissingPacked : Line.Missing, GrandmaLines.NameOf(missing));
                routinePause = Time.time + 2.5f;
            }
            spot.lastUsedTime = Time.time;
        }

        bool CloseTo(Vector3 point)
        {
            Vector3 d = point - transform.position;
            float dy = d.y;
            d.y = 0f;
            return d.sqrMagnitude <= arriveTolerance * arriveTolerance && Mathf.Abs(dy) < 1.2f;
        }

        // She could not get to a spot: a grumble, and another spot first.
        void BlockedFrom(ActivitySpot spot)
        {
            spot.lastUsedTime = Time.time;
            RoutineTarget = null;
            routineFails++;
            moveIssued = false;
            speech.Say(Line.Blocked);
            routinePause = Time.time + 1.5f;
        }

        // Somewhere to potter about when there is nothing to do: one of her spots, any of them.
        Vector3 WanderPoint()
        {
            var spots = ActivitySpot.All;
            if (spots.Count > 0)
            {
                ActivitySpot s = spots[UnityEngine.Random.Range(0, spots.Count)];
                if (s != null) return s.StandPosition;
            }
            Vector2 r = UnityEngine.Random.insideUnitCircle * 6f;
            return transform.position + new Vector3(r.x, 0f, r.y);
        }

        void TickActivity(float dt)
        {
            if (!activities.Tick(dt)) return;
            Enter(GrandmaState.Routine);
            routinePause = Time.time + UnityEngine.Random.Range(0.4f, pauseBetweenActivities);
        }

        void TickObserve()
        {
            if (Time.time >= stateUntil) Resume();
        }

        void TickReact()
        {
            // Standing up first if she was sitting: the telling-off starts once she is up.
            if (activities.IsBusy) { stateUntil = Time.time + reactSeconds; return; }
            CrewMember m = focusPlayer != Actors.World ? CrewRoster.Get(focusPlayer) : null;
            mover.Face(m != null ? m.Position : focusPoint);
            if (Time.time >= stateUntil) Resume();
        }

        void TickInvestigate()
        {
            if (activities.IsBusy) return;
            if (!moveIssued)
            {
                mover.MoveTo(focusPoint, mover.hurrySpeed, 1.5f);
                moveIssued = true;
            }

            // Whoever she finds near the scene is the suspect.
            for (int p = 0; p < 4; p++)
            {
                if (!senses.Sees(p)) continue;
                CrewMember m = CrewRoster.Get(p);
                if (m == null) continue;
                if ((m.Position - transform.position).sqrMagnitude > suspectRange * suspectRange
                    && (m.Position - focusPoint).sqrMagnitude > suspectRange * suspectRange) continue;
                mood.AddBlame(p, suspectBlame);
                focusPlayer = p;
                focusPoint = m.Position;
                stats.reactions++;
                float said = speech.Say(Line.Suspect);
                anim.PlayUpper(GrandmaAnimation.Point);
                Enter(GrandmaState.React);
                stateUntil = Time.time + Mathf.Max(reactSeconds, said);
                return;
            }

            if (!looking)
            {
                bool there = mover.Status != GrandmaMover.MoveStatus.Moving;
                if (there || StateTime > investigateTimeout)
                {
                    mover.Stop();
                    looking = true;
                    lookStart = Time.time;
                }
                return;
            }

            // Looking about: towards the noise, then left and right.
            float t = Time.time - lookStart;
            Vector3 toward = focusPoint - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.25f) toward = mover.Forward;
            mover.Face(transform.position + Quaternion.Euler(0f, Mathf.Sin(t * 1.8f) * 70f, 0f) * toward.normalized * 3f);
            if (t < lookAroundSeconds) return;
            speech.Say(Line.NothingThere);
            Resume();
        }

        void TickConfront()
        {
            if (activities.IsBusy) return;
            CrewMember m = CrewRoster.Get(ConfrontTarget);
            if (m == null) { EndConfront(); return; }
            Vector3 d = m.Position - transform.position;
            d.y = 0f;

            if (!scolded)
            {
                if (d.magnitude > confrontReach)
                {
                    if (StateTime > confrontGiveUp)
                    {
                        speech.Say(Line.GiveUp);
                        EndConfront();
                        return;
                    }
                    if (Time.time >= nextRepath)
                    {
                        nextRepath = Time.time + 0.6f;
                        mover.MoveTo(m.Position, mover.hurrySpeed, confrontReach * 0.8f);
                    }
                    return;
                }

                // Caught up: she makes him put her things down.
                mover.Stop();
                scolded = true;
                stateUntil = Time.time + 3f;
                MovableObject held = m.Held;
                if (held != null && held.IsTheftTarget && m.Grab != null)
                {
                    m.Grab.Release(false);
                    speech.Say(Line.DropIt);
                }
                else speech.Say(Line.ChatGrumpy);
                anim.PlayUpper(GrandmaAnimation.HandsOnHips);
            }

            mover.Face(m.Position);
            if (Time.time >= stateUntil) EndConfront();
        }

        void EndConfront()
        {
            confrontReadyAt = Time.time + confrontCooldown;
            ConfrontTarget = Actors.World;
            anim.StopUpper();
            mover.ClearFace();
            Enter(GrandmaState.Routine);
        }

        void TickPolice()
        {
            CrewMember near = CrewRoster.Nearest(transform.position, 20f);
            if (near != null) mover.Face(near.Position);
            if (Time.time < nextPhoneLine) return;
            nextPhoneLine = Time.time + 7f;
            speech.Say(Line.OnThePhone);
        }

        // ---------------------------------------------------------------- the session

        void OnWorldEvent(WorldEvent e)
        {
            if (!Net.HasAuthority) return;
            if (e.type != WorldEventType.SessionStateChanged) return;
            var st = (SessionState)Mathf.RoundToInt(e.magnitude);
            if (st == SessionState.ContractStarted || st == SessionState.InProgress)
            {
                // The run started without her keys: F8, or a break-in.
                if (!KeysGiven) startRoutinePending = true;
            }
            else if (st == SessionState.Completed || st == SessionState.Failed)
            {
                sessionOverPending = true;
                endedAs = st;
            }
        }

        // Online client (GrandmaSync Flags): the host's state and flags, no side effects.
        public void ApplyReplica(GrandmaState state, bool keysGiven, bool policeCalled, bool aiEnabled, bool isSessionOver)
        {
            if (State != state) stateStart = Time.time;
            State = state;
            KeysGiven = keysGiven;
            PoliceCalled = policeCalled;
            AIEnabled = aiEnabled;
            sessionOver = isSessionOver;
        }

        void HandleSession()
        {
            if (startRoutinePending)
            {
                startRoutinePending = false;
                if (!KeysGiven)
                {
                    KeysGiven = true;
                    if (props != null) props.HideAll();
                    anim.StopUpper();
                    if (Time.time - lastBreakInHeard < 6f) speech.Say(Line.BreakIn);
                    if (State == GrandmaState.Intro || State == GrandmaState.GiveKeys) Enter(GrandmaState.Routine);
                }
            }
            if (sessionOverPending && !sessionOver)
            {
                sessionOverPending = false;
                sessionOver = true;
                if (senses != null) senses.asleep = true;
                mover.Stop();
                activities.Interrupt(false);
                if (endedAs == SessionState.Completed) speech.Say(Line.Goodbye);
                else if (!PoliceCalled) speech.Say(Line.TooSlow);
            }
        }
    }
}
