using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Her day: picking the next ActivitySpot and playing it out once she is there.
    //
    // An activity has three beats: enter (sit down, kneel, or just settle on the spot), loop
    // (the activity clip with its prop, for a while) and exit (stand up). Seats are entered with
    // her collisions off, since the spot is inside the chair; she slides back to the stand point
    // with them on again. The brain decides when she goes where; this decides what it looks like.
    //
    // Picking: the spot she has not used for the longest time wins, with some randomness, never
    // the one she just left. A spot whose object was taken away is still picked once, so she
    // walks there and finds out ("where is my rocking chair?"); after that she skips it.
    [DisallowMultipleComponent]
    public sealed class GrandmaActivities : MonoBehaviour
    {
        public enum Phase { None, Entering, Looping, Exiting }

        [Tooltip("Scales every activity's duration. The Play-mode tests use 0.15.")]
        public float durationScale = 1f;
        public float seatSlideSeconds = 0.4f;
        public float settleSeconds = 0.3f;
        [Tooltip("Metres the seat may move under her before she is dumped out of it.")]
        public float seatTakenDistance = 0.4f;
        [Tooltip("Seconds into LightFire before the fire catches.")]
        public float igniteAfter = 1.5f;
        [Range(0f, 1f)] public float sayLineChance = 0.6f;

        public GrandmaMover mover;
        public GrandmaAnimation anim;
        public GrandmaProps props;
        public GrandmaSpeech speech;

        public ActivitySpot Current { get; private set; }
        public ActivitySpot Previous { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public bool IsBusy => CurrentPhase != Phase.None;
        public int DoneCount { get; private set; }
        public int KindsDoneMask { get; private set; }

        public int DistinctKindsDone
        {
            get
            {
                int n = 0;
                for (int m = KindsDoneMask; m != 0; m &= m - 1) n++;
                return n;
            }
        }

        // Someone carried off the seat she was sitting in. She is already back on her feet.
        public event Action<ActivitySpot, MovableObject> SeatTaken;

        float phaseLeft;
        float loopTime;
        float igniteAt;
        bool lit;
        bool slidBack;
        bool finishedNaturally;
        Vector3 seatStart;

        void Awake()
        {
            if (mover == null) mover = GetComponent<GrandmaMover>();
            if (anim == null) anim = GetComponent<GrandmaAnimation>();
            if (props == null) props = GetComponent<GrandmaProps>();
            if (speech == null) speech = GetComponent<GrandmaSpeech>();
        }

        public void ResetStats()
        {
            DoneCount = 0;
            KindsDoneMask = 0;
        }

        // The spot to walk to next, or null when there is nothing left to do.
        public ActivitySpot PickNext()
        {
            ActivitySpot best = null;
            float bestScore = float.NegativeInfinity;
            var spots = ActivitySpot.All;
            for (int i = 0; i < spots.Count; i++)
            {
                ActivitySpot s = spots[i];
                if (s == null || s == Previous) continue;
                ActivitySpot.Availability a = s.Check(out _);
                if (a == ActivitySpot.Availability.NotNeeded) continue;
                if (a == ActivitySpot.Availability.Missing && s.commented) continue;
                float idle = Mathf.Min(Time.time - s.lastUsedTime, 300f);
                float score = Mathf.Max(0.05f, s.weight) * (1f + idle / 60f) * UnityEngine.Random.Range(0.6f, 1.4f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best == null && Previous != null && Previous.Check(out _) == ActivitySpot.Availability.Available) best = Previous;
            return best;
        }

        // She is at the spot's stand point and the spot is available.
        public void Begin(ActivitySpot spot)
        {
            if (spot == null) return;
            Current = spot;
            spot.lastUsedTime = Time.time;
            loopTime = 0f;
            lit = false;
            slidBack = false;
            finishedNaturally = false;

            if (spot.Seated)
            {
                MovableObject seat = spot.Seat;
                seatStart = seat != null ? seat.transform.position : Vector3.zero;
                // Collisions stay off while she sits; the mover's stand-in capsule keeps her
                // solid and talkable, and ignores the seat it sits in.
                mover.SlideTo(spot.Anchor, spot.Facing, seatSlideSeconds, false, spot.SeatObject);
            }
            else mover.SlideTo(spot.Anchor, spot.Facing, settleSeconds, true);

            string enter = spot.EnterState;
            bool hasClip = anim != null && anim.Play(enter);
            phaseLeft = string.IsNullOrEmpty(enter) ? 0f : hasClip ? spot.EnterSeconds : 0.3f;
            CurrentPhase = Phase.Entering;

            if (speech != null && UnityEngine.Random.value < sayLineChance) speech.Say(LineFor(spot.kind));
        }

        // Advances the current activity. True once it is over (or was never running).
        public bool Tick(float dt)
        {
            ActivitySpot spot = Current;
            if (CurrentPhase == Phase.None || spot == null)
            {
                CurrentPhase = Phase.None;
                return true;
            }

            phaseLeft -= dt;
            switch (CurrentPhase)
            {
                case Phase.Entering:
                    if (phaseLeft > 0f || mover.IsSliding) break;
                    CurrentPhase = Phase.Looping;
                    if (anim != null) anim.Play(spot.LoopState);
                    if (props != null) props.Show(spot.Prop);
                    phaseLeft = spot.PickDuration() * Mathf.Max(0.01f, durationScale);
                    igniteAt = Mathf.Min(igniteAfter, phaseLeft * 0.5f);
                    break;

                case Phase.Looping:
                    loopTime += dt;
                    if (spot.kind == ActivityKind.LightFire && !lit && loopTime >= igniteAt)
                    {
                        lit = true;
                        if (spot.fire != null) spot.fire.Ignite();
                    }
                    if (spot.Seated && SeatMoved(spot, out MovableObject seat))
                    {
                        DumpOut();
                        try { SeatTaken?.Invoke(spot, seat); } catch (Exception e) { Debug.LogException(e); }
                        break;
                    }
                    if (phaseLeft <= 0f)
                    {
                        finishedNaturally = true;
                        BeginExit();
                    }
                    break;

                case Phase.Exiting:
                    if (phaseLeft > 0f || mover.IsSliding) break;
                    if (spot.Seated && !slidBack)
                    {
                        // Stand_Up ends with her hips over the seat edge: step back onto the floor.
                        slidBack = true;
                        mover.SlideTo(spot.StandPosition, spot.Facing, seatSlideSeconds, true);
                        phaseLeft = seatSlideSeconds;
                        break;
                    }
                    Finish();
                    break;
            }
            return CurrentPhase == Phase.None;
        }

        // Stop what she is doing: stand up properly, or at once.
        public void Interrupt(bool immediate)
        {
            if (CurrentPhase == Phase.None) return;
            if (immediate) { DumpOut(); return; }
            if (CurrentPhase == Phase.Exiting) return;
            BeginExit();
        }

        void BeginExit()
        {
            ActivitySpot spot = Current;
            CurrentPhase = Phase.Exiting;
            if (props != null) props.HideAll();
            string exit = spot.ExitState;
            bool hasClip = anim != null && !string.IsNullOrEmpty(exit) && anim.Play(exit);
            if (!hasClip && anim != null) anim.Play(GrandmaAnimation.Locomotion);
            phaseLeft = hasClip ? spot.ExitSeconds : 0f;
        }

        // Out of the activity in a quarter of a second, no clip.
        void DumpOut()
        {
            ActivitySpot spot = Current;
            if (props != null) props.HideAll();
            if (anim != null) anim.Play(GrandmaAnimation.Locomotion);
            CurrentPhase = Phase.Exiting;
            slidBack = true;
            phaseLeft = 0.25f;
            if (spot != null && (spot.Seated || !mover.CollisionsOn)) mover.SlideTo(spot.StandPosition, spot.Facing, 0.25f, true);
            else mover.EnableCollisions();
        }

        void Finish()
        {
            if (props != null) props.HideAll();
            if (!mover.CollisionsOn) mover.EnableCollisions();
            if (finishedNaturally && Current != null)
            {
                DoneCount++;
                KindsDoneMask |= 1 << (int)Current.kind;
            }
            Previous = Current;
            Current = null;
            CurrentPhase = Phase.None;
        }

        bool SeatMoved(ActivitySpot spot, out MovableObject seat)
        {
            seat = spot.Seat;
            if (seat == null) return false;
            if (!seat.gameObject.activeInHierarchy || seat.holder != null || seat.loaded) return true;
            return (seat.transform.position - seatStart).sqrMagnitude > seatTakenDistance * seatTakenDistance;
        }

        static Line LineFor(ActivityKind kind)
        {
            switch (kind)
            {
                case ActivityKind.SitRockingChair: return Line.Rock;
                case ActivityKind.ReadBook: return Line.Read;
                case ActivityKind.WatchTV: return Line.TV;
                case ActivityKind.DrinkTea: return Line.Tea;
                case ActivityKind.Cook: return Line.Cook;
                case ActivityKind.WaterPlants: return Line.Water;
                case ActivityKind.LightFire: return Line.Fire;
                default: return Line.LookOutside;
            }
        }
    }
}
