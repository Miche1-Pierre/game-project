using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The host's source for P2 online: the client's input, played out on the host's timeline
    // (NETCODE_SLICE 9.1, 9.2). NetPlayerDriver feeds it before CrewInput polls (-520 < -500).
    //
    // Held-button changes keep their client spacing: a queued change is released only once
    // the host time since the previous one reaches its dtMs, and one change at most per poll,
    // so every Down and every Up gets its own frame and a 0.3 s hold stays a hold (the tap or
    // hold of the right button) even after a burst of loss. Over 0.5 s of backlog, the waiting
    // changes go one per poll. Deltas arrive as running totals: the difference from the last
    // applied total is this frame's, so a lost packet loses no motion.
    //
    // Never reports Pause: the client's Esc opens only the client's own menu. While that menu
    // is open P2 is muted here the way HudPauseMenu mutes a local player (mute only if not
    // muted, unmute only what we muted), and on close the held buttons are taken as the new
    // baseline (CrewInput.ResyncHeld), so no edge fires either way.
    public sealed class RemoteInputSource : ICrewInputSource
    {
        const float MaxBacklog = 0.5f;
        static readonly uint PauseBit = 1u << (int)CrewButton.Pause;

        struct Change { public ushort frameSeq; public float dt; public uint held; public float queuedAt; }

        readonly CrewInput input;
        readonly Queue<Change> changes = new Queue<Change>();
        uint held;             // what the host plays now
        uint latestHeld;       // the newest change taken, applied or not (used while paused)
        float lastAppliedAt = float.NegativeInfinity;
        Vector2 move;
        Vector2 lookTotal, lookApplied;
        float scrollTotal, scrollApplied, rollTotal, rollApplied;
        bool hasTotals;
        bool paused, mutedByUs;

        public RemoteInputSource() { }
        public RemoteInputSource(CrewInput input) { this.input = input; }

        public string Label => "Remote";
        public bool IsGamepad { get; set; }
        public bool Paused => paused;

        // ---- fed by NetPlayerDriver, from the played-out InputPose packets ----

        public void QueueChange(ushort frameSeq, int dtMs, uint newHeld)
        {
            changes.Enqueue(new Change
            {
                frameSeq = frameSeq, dt = dtMs * 0.001f, held = newHeld & ~PauseBit, queuedAt = Time.unscaledTime,
            });
        }

        public void SetState(Vector2 newMove, Vector2 newLookTotal, float newScrollTotal, float newRollTotal)
        {
            move = newMove;
            if (!hasTotals)
            {
                // The first packet of a scene: nothing before it is ours to apply.
                lookApplied = newLookTotal;
                scrollApplied = newScrollTotal;
                rollApplied = newRollTotal;
                hasTotals = true;
            }
            lookTotal = newLookTotal;
            scrollTotal = newScrollTotal;
            rollTotal = newRollTotal;
        }

        public void SetPaused(bool on)
        {
            if (on == paused) return;
            paused = on;
            if (input == null) return;
            if (on)
            {
                if (!input.Muted) { input.Muted = true; mutedByUs = true; }
                return;
            }
            held = latestHeld;
            input.ResyncHeld();
            if (!mutedByUs) return;
            mutedByUs = false;
            // The session may have frozen the crew meanwhile: it keeps them (as HudPauseMenu).
            var s = GameSession.Current;
            bool sessionHolds = Session.IsOver || (s != null && (s.IntroCardShowing || s.CrewReleasePending));
            if (!sessionHolds) input.Muted = false;
        }

        // The client left: give back a mute we set, and forget everything queued.
        public void Stop()
        {
            if (paused && mutedByUs && input != null) input.Muted = false;
            paused = mutedByUs = false;
            changes.Clear();
            held = latestHeld = 0;
            move = Vector2.zero;
        }

        public void Poll(ref CrewInputFrame frame, float dt)
        {
            frame = default;
            float now = Time.unscaledTime;
            if (changes.Count > 0)
            {
                var c = changes.Peek();
                bool due = now - lastAppliedAt >= c.dt || now - c.queuedAt >= MaxBacklog;
                if (paused)
                {
                    // Taken, not played: the menu's own presses never reach the host's P2.
                    while (changes.Count > 0) latestHeld = changes.Dequeue().held;
                }
                else if (due)
                {
                    changes.Dequeue();
                    held = latestHeld = c.held;
                    lastAppliedAt = now;
                }
            }

            // The totals are always consumed, so nothing moved during a pause is applied after it.
            Vector2 look = lookTotal - lookApplied;
            float scroll = scrollTotal - scrollApplied;
            float roll = rollTotal - rollApplied;
            lookApplied = lookTotal;
            scrollApplied = scrollTotal;
            rollApplied = rollTotal;

            frame.held = held & ~PauseBit;
            if (paused) return;
            frame.move = move;
            frame.lookDelta = look;
            frame.scroll = scroll;
            frame.rollDelta = roll;
        }
    }
}
