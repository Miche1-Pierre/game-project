using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The players online (NETCODE_SLICE 9.2 to 9.4). One per game scene, added by CrewSpawner
    // online only; offline it never exists.
    //
    //   Update (-520, before CrewInput at -500):
    //     host    plays out the client's InputPose packets on the client's clock, 35 ms behind:
    //             P2's pose (interpolated, at most 0.1 s extrapolated) and its input changes
    //             (into RemoteInputSource), so a press and the camera it was aimed with land
    //             in the same host frame;
    //     client  poses the P1 puppet from PuppetPose, 0.1 s behind like the transform stream.
    //     Then one Physics.SyncTransforms, so the capsules and every ray see the new poses.
    //   LateUpdate (-520, before CameraShake and ViewOffset): samples the local body, so no
    //     render-only offset is ever sent. PlayerSync writes at Tick.
    //
    // A member at the truck's wheel is never posed (the seat places it), on both machines.
    [DefaultExecutionOrder(NetOrder.PlayerDriver)]
    [DisallowMultipleComponent]
    public sealed class NetPlayerDriver : MonoBehaviour
    {
        const float PlayoutDelay = 0.035f;
        const float MaxExtrapolation = 0.1f;
        const int MaxQueued = 90;              // 1.5 s of packets; past that the oldest goes at once
        const float SendMin = 1f / 60f;
        const float SendIdle = 0.1f;
        static readonly uint PauseBit = 1u << (int)CrewButton.Pause;

        public static NetPlayerDriver Active { get; private set; }

        // ---- host: the client's packets ----
        readonly List<InputPacket> queue = new List<InputPacket>();
        readonly Stack<InputPacket> pool = new Stack<InputPacket>();
        InputPacket current;
        bool hasNewest, hasChangeSeq;
        ushort newestSeq, lastChangeSeq;
        uint lastQueuedHeld;
        public bool HasAck { get; private set; }
        public ushort AckSeq { get; private set; }
        public bool HasChaseYaw => current != null;
        public float ChaseYaw => current != null ? current.chaseYaw : 0f;

        // ---- host: P1's pose for PuppetPose ----
        public NetPose HostPose { get; private set; }
        public bool HostPoseValid { get; private set; }

        // ---- client: the P1 puppet ----
        struct PoseSample { public float time; public NetPose pose; }
        readonly List<PoseSample> puppet = new List<PoseSample>(32);

        // ---- client: what this machine sends ----
        readonly InputPacket outgoing = new InputPacket();
        readonly List<InputChange> unacked = new List<InputChange>(InputPacket.MaxChanges);
        ushort seq;
        uint lastHeld;
        float lastChangeAt;
        Vector2 lookTotal;
        float scrollTotal, rollTotal;
        bool sampled;
        float lastSendAt = -99f;
        uint sentHeld;
        Vector2 sentLook;
        float sentScroll, sentRoll;
        byte sentFlags;
        NetPose sentPose;

        void Awake()
        {
            if (!Net.IsOnline) { Destroy(this); return; }
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            if (!Net.IsOnline) return;
            bool moved = Net.IsHost ? PlayOut() : PosePuppet();
            if (moved) Physics.SyncTransforms();
        }

        void LateUpdate()
        {
            if (!Net.IsOnline) return;
            if (Net.IsHost) SampleHost();
            else SampleClient();
        }

        // =====================================================================
        // Host
        // =====================================================================

        internal InputPacket RentPacket() => pool.Count > 0 ? pool.Pop() : new InputPacket();

        // PlayerSync, in NGO's receive stage: a packet from the client, newest first wins.
        internal void OnInputPose(InputPacket p)
        {
            if (hasNewest && !Newer(p.seq, newestSeq)) { pool.Push(p); return; }
            hasNewest = true;
            newestSeq = p.seq;
            queue.Add(p);
        }

        bool PlayOut()
        {
            var p2 = CrewRoster.Get(Net.ClientMember);
            if (p2 == null || Net.Drives(p2) || !Net.PeerReady) return false;
            var remote = p2.Input != null ? p2.Input.Source as RemoteInputSource : null;

            float play = NetOut.PeerNow - PlayoutDelay;
            while (queue.Count > 0 && (queue[0].clientTime <= play || queue.Count > MaxQueued))
            {
                var p = queue[0];
                queue.RemoveAt(0);
                Consume(p, remote);
                if (current != null) pool.Push(current);
                current = p;
            }
            if (current == null || current.seated || p2.IsDriving || p2.Controller == null) return false;
            if (SeatOlder(current.seatEpoch, TruckSync.SeatEpoch)) return false;

            NetPose pose = current.pose;
            if (queue.Count > 0) pose = Lerp(current.pose, queue[0].pose, Mathf.InverseLerp(current.clientTime, queue[0].clientTime, play));
            else pose.position += pose.velocity * Mathf.Clamp(play - current.clientTime, 0f, MaxExtrapolation);
            p2.Controller.ApplyNetPose(pose);
            return true;
        }

        void Consume(InputPacket p, RemoteInputSource remote)
        {
            if (remote != null)
            {
                for (int i = 0; i < p.changeCount; i++)
                {
                    var c = p.changes[i];
                    if (hasChangeSeq && !Newer(c.frameSeq, lastChangeSeq)) continue;   // seen in an earlier packet
                    hasChangeSeq = true;
                    lastChangeSeq = c.frameSeq;
                    lastQueuedHeld = c.held;
                    remote.QueueChange(c.frameSeq, c.dtMs, c.held);
                }
                // More changes than a packet carries were lost: the held bits it reports win.
                if (p.held != lastQueuedHeld)
                {
                    lastQueuedHeld = p.held;
                    remote.QueueChange(p.seq, 0, p.held);
                }
                remote.SetState(p.move, p.lookTotal, p.scrollTotal, p.rollTotal);
                remote.SetPaused(p.paused);
            }
            HasAck = true;
            AckSeq = p.seq;
        }

        void SampleHost()
        {
            var p1 = CrewRoster.Get(Net.HostMember);
            HostPoseValid = p1 != null && p1.Controller != null && !p1.IsDriving;
            if (HostPoseValid) HostPose = p1.Controller.NetPoseNow();
        }

        // The client left (3.5): P2 becomes an idle body the host simulates.
        internal void OnPeerLeft()
        {
            var p2 = CrewRoster.Get(Net.ClientMember);
            if (p2 != null && p2.Input != null)
            {
                if (p2.Input.Source is RemoteInputSource remote) remote.Stop();
                p2.Input.SetSource(new NullInputSource());
            }
            if (p2 != null && p2.Grab != null) p2.Grab.Release(false);
            for (int i = 0; i < queue.Count; i++) pool.Push(queue[i]);
            queue.Clear();
            current = null;
        }

        // =====================================================================
        // Client
        // =====================================================================

        internal void OnPuppetPose(int member, float hostTime, in NetPose pose)
        {
            if (member != Net.HostMember) return;
            if (puppet.Count > 0 && hostTime <= puppet[puppet.Count - 1].time) return;
            puppet.Add(new PoseSample { time = hostTime, pose = pose });
            if (puppet.Count > 32) puppet.RemoveAt(0);
        }

        bool PosePuppet()
        {
            var p1 = CrewRoster.Get(Net.HostMember);
            if (p1 == null || p1.Controller == null) return false;
            if (p1.IsDriving) { puppet.Clear(); return false; }
            if (puppet.Count == 0) return false;

            float render = NetOut.PeerNow - NetTransforms.InterpolationDelay;
            if (render < puppet[0].time) return false;   // hold until the first sample is due
            int k = 0;
            while (k < puppet.Count - 1 && puppet[k + 1].time <= render) k++;
            NetPose pose = puppet[k].pose;
            if (k < puppet.Count - 1)
                pose = Lerp(pose, puppet[k + 1].pose, Mathf.InverseLerp(puppet[k].time, puppet[k + 1].time, render));
            else
                pose.position += pose.velocity * Mathf.Clamp(render - puppet[k].time, 0f, MaxExtrapolation);
            if (k > 0) puppet.RemoveRange(0, k);
            p1.Controller.ApplyNetPose(pose);
            return true;
        }

        // Every frame of the local body, counted only once the snapshot is in (3.6): from the
        // Reload or LoadScene until then nothing is sent.
        void SampleClient()
        {
            var m = CrewRoster.Get(Net.LocalMember);
            if (m == null || m.Input == null || m.Controller == null || !Net.PeerReady) { sampled = false; return; }

            seq++;
            var input = m.Input;
            var raw = input.RawFrame;
            uint held = raw.held & ~PauseBit;
            float now = NetOut.Now;
            if (held != lastHeld)
            {
                int dtMs = Mathf.Clamp(Mathf.RoundToInt((now - lastChangeAt) * 1000f), 0, ushort.MaxValue);
                if (unacked.Count >= InputPacket.MaxChanges) unacked.RemoveAt(0);
                unacked.Add(new InputChange { frameSeq = seq, dtMs = (ushort)dtMs, held = held });
                lastChangeAt = now;
                lastHeld = held;
            }
            // Only the look that turns a held object goes to the host (its own look is ours);
            // the wheel is a roll while Rotate is held, a reach otherwise (9.2).
            if (m.Controller.lookLocked) lookTotal += input.LookDelta;
            if (input.Held(CrewButton.Rotate)) rollTotal += input.RollDelta;
            else scrollTotal += input.Scroll;

            var o = outgoing;
            o.seq = seq;
            o.clientTime = now;
            o.held = held;
            o.move = raw.move;
            o.lookTotal = lookTotal;
            o.scrollTotal = scrollTotal;
            o.rollTotal = rollTotal;
            o.paused = HudPauseMenu.OpenCount > 0;
            o.seated = m.IsDriving;
            o.seatEpoch = TruckSync.SeatEpoch;
            // At the wheel the local camera is the chase view: its yaw is where the driver looks.
            o.chaseYaw = m.View != null ? m.View.transform.eulerAngles.y : m.transform.eulerAngles.y;
            o.pose = m.Controller.NetPoseNow();
            sampled = true;
        }

        // PlayerSync.ClientTick: at most 60 Hz, 10 Hz while nothing changes.
        internal void SendIfDue()
        {
            if (!sampled) return;
            float now = Time.unscaledTime;
            if (now - lastSendAt < SendMin) return;
            var o = outgoing;
            byte flags = o.Flags;
            bool changed = o.held != sentHeld || o.lookTotal != sentLook || o.scrollTotal != sentScroll
                           || o.rollTotal != sentRoll || flags != sentFlags || o.move != Vector2.zero
                           || (o.pose.position - sentPose.position).sqrMagnitude > 1e-6f
                           || Quaternion.Angle(o.pose.camLocalRotation, sentPose.camLocalRotation) > 0.1f
                           || Mathf.Abs(Mathf.DeltaAngle(o.pose.yaw, sentPose.yaw)) > 0.1f
                           || o.pose.crouching != sentPose.crouching || o.pose.throwHeld != sentPose.throwHeld;
            if (!changed && now - lastSendAt < SendIdle) return;

            o.changeCount = unacked.Count;
            for (int i = 0; i < unacked.Count; i++) o.changes[i] = unacked[i];
            if (!PlayerSync.WriteInputPose(o)) return;
            lastSendAt = now;
            sentHeld = o.held;
            sentLook = o.lookTotal;
            sentScroll = o.scrollTotal;
            sentRoll = o.rollTotal;
            sentFlags = flags;
            sentPose = o.pose;
        }

        // The host has applied every change up to ack: they need not travel again.
        internal void OnInputAck(ushort ack)
        {
            for (int i = unacked.Count - 1; i >= 0; i--)
                if (!Newer(unacked[i].frameSeq, ack)) unacked.RemoveAt(i);
        }

        // =====================================================================

        // Sequence numbers wrap at 16 bits.
        static bool Newer(ushort a, ushort b) => (short)(a - b) > 0;
        static bool SeatOlder(byte a, byte b) => (sbyte)(a - b) < 0;

        static NetPose Lerp(in NetPose a, in NetPose b, float t)
        {
            var p = a;
            p.position = Vector3.Lerp(a.position, b.position, t);
            p.yaw = Mathf.LerpAngle(a.yaw, b.yaw, t);
            p.camLocalRotation = Quaternion.Slerp(a.camLocalRotation, b.camLocalRotation, t);
            p.height = Mathf.Lerp(a.height, b.height, t);
            p.velocity = Vector3.Lerp(a.velocity, b.velocity, t);
            return p;
        }
    }

    // One held-button change: the client frame it happened in, the milliseconds since the
    // previous change, and the held bits after it.
    internal struct InputChange
    {
        public ushort frameSeq;
        public ushort dtMs;
        public uint held;
    }

    // Players InputPose (sys 7, op 1), 9.2:
    // seq u16 | clientTime f32 | held u16 | n u8 | n x (frameSeq u16, dtMs u16, held u16) |
    // move 2 x s8 | rotateLookTotal 2 x f32 | scrollTotal f32 | rollTotal f32 | flags u8 |
    // seatEpoch u8 | chaseYaw angle | pose
    internal sealed class InputPacket
    {
        public const int MaxChanges = 16;
        const byte FlagPaused = 1, FlagSeated = 2;

        public ushort seq;
        public float clientTime;
        public uint held;
        public int changeCount;
        public readonly InputChange[] changes = new InputChange[MaxChanges];
        public Vector2 move;
        public Vector2 lookTotal;
        public float scrollTotal, rollTotal;
        public bool paused, seated;
        public byte seatEpoch;
        public float chaseYaw;
        public NetPose pose;

        public byte Flags => (byte)((paused ? FlagPaused : 0) | (seated ? FlagSeated : 0));

        public void Write(NetWriter w)
        {
            w.WriteUShort(seq);
            w.WriteFloat(clientTime);
            w.WriteUShort((ushort)held);
            int n = Mathf.Min(changeCount, MaxChanges);
            w.WriteByte((byte)n);
            for (int i = changeCount - n; i < changeCount; i++)
            {
                w.WriteUShort(changes[i].frameSeq);
                w.WriteUShort(changes[i].dtMs);
                w.WriteUShort((ushort)changes[i].held);
            }
            w.WriteSByte(Axis(move.x));
            w.WriteSByte(Axis(move.y));
            w.WriteFloat(lookTotal.x);
            w.WriteFloat(lookTotal.y);
            w.WriteFloat(scrollTotal);
            w.WriteFloat(rollTotal);
            w.WriteByte(Flags);
            w.WriteByte(seatEpoch);
            w.WriteAngle(chaseYaw);
            w.WritePose(pose);
        }

        public void Read(NetReader r)
        {
            seq = r.ReadUShort();
            clientTime = r.ReadFloat();
            held = r.ReadUShort();
            changeCount = Mathf.Min(r.ReadByte(), MaxChanges);
            for (int i = 0; i < changeCount; i++)
                changes[i] = new InputChange { frameSeq = r.ReadUShort(), dtMs = r.ReadUShort(), held = r.ReadUShort() };
            move = new Vector2(r.ReadSByte() / 127f, r.ReadSByte() / 127f);
            lookTotal = new Vector2(r.ReadFloat(), r.ReadFloat());
            scrollTotal = r.ReadFloat();
            rollTotal = r.ReadFloat();
            byte flags = r.ReadByte();
            paused = (flags & FlagPaused) != 0;
            seated = (flags & FlagSeated) != 0;
            seatEpoch = r.ReadByte();
            chaseYaw = r.ReadAngle();
            pose = r.ReadPose();
        }

        static sbyte Axis(float v) => (sbyte)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 127f);
    }
}
