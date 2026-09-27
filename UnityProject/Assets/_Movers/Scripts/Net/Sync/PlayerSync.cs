using UnityEngine;

namespace Movers
{
    // Players (sys 7), NETCODE_SLICE 4.3 and 9. The wire only: NetPlayerDriver plays the poses
    // and the input out.
    //   1 InputPose   c>h U  the client's frame and pose (InputPacket), 60 Hz, 10 Hz idle
    //   2 PuppetPose  h>c U  member, pose; P1 at 20 Hz, never while seated
    //   3 Impulse     h>c R  member, velocityChange: a shove on the body the client drives
    //   4 Drunk       h>c R  member, amount unit; on change of 1/255, at most 5 Hz
    //   5 InputAck    h>c U  the newest client frameSeq applied, 20 Hz
    public sealed class PlayerSync : NetSync
    {
        const byte OpInputPose = 1, OpPuppetPose = 2, OpImpulse = 3, OpDrunk = 4, OpInputAck = 5;
        const float PuppetEvery = 1f / 20f;
        const float AckEvery = 1f / 20f;
        const float DrunkEvery = 1f / 5f;

        public override NetSyncId Id => NetSyncId.Players;

        static float nextPuppet, nextAck, nextDrunk;
        static float sentDrunk = -1f;
        static Drunkenness p2Drunk;

        // ---- gameplay hooks ----

        // PlayerController.AddImpulse on the host, for a body it does not drive (9.6).
        public static void SendImpulse(int member, Vector3 velocityChange)
        {
            var w = NetOut.Reliable(NetSyncId.Players, OpImpulse);
            if (w == null) return;
            w.WriteByte((byte)member);
            w.WriteVector3(velocityChange);
            NetOut.End(w);
        }

        // Host: where the client's driver looks from the wheel, for his exit facing (9.6).
        public static bool TryGetChaseYaw(int member, out float yaw)
        {
            var d = NetPlayerDriver.Active;
            yaw = 0f;
            if (!Net.IsHost || member != Net.ClientMember || d == null || !d.HasChaseYaw) return false;
            yaw = d.ChaseYaw;
            return true;
        }

        internal static bool WriteInputPose(InputPacket p)
        {
            var w = NetOut.Unreliable(NetSyncId.Players, OpInputPose);
            if (w == null) return false;
            p.Write(w);
            NetOut.End(w);
            return true;
        }

        // ---- ticks ----

        public override void HostTick()
        {
            var d = NetPlayerDriver.Active;
            if (d == null) return;
            float now = Time.unscaledTime;
            if (now >= nextPuppet)
            {
                nextPuppet = now + PuppetEvery;
                if (d.HostPoseValid) WritePuppetPose(d.HostPose);
            }
            if (now >= nextAck && d.HasAck)
            {
                nextAck = now + AckEvery;
                var w = NetOut.Unreliable(NetSyncId.Players, OpInputAck);
                if (w != null) { w.WriteUShort(d.AckSeq); NetOut.End(w); }
            }
            if (now >= nextDrunk)
            {
                float a = DrunkAmount();
                if (Mathf.Abs(a - sentDrunk) >= 1f / 255f && WriteDrunk(a)) nextDrunk = now + DrunkEvery;
            }
        }

        public override void ClientTick()
        {
            var d = NetPlayerDriver.Active;
            if (d != null) d.SendIfDue();
        }

        public override void SendSnapshot()
        {
            var d = NetPlayerDriver.Active;
            if (d != null && d.HostPoseValid) WritePuppetPose(d.HostPose);
            WriteDrunk(DrunkAmount());
        }

        static void WritePuppetPose(in NetPose pose)
        {
            var w = NetOut.Unreliable(NetSyncId.Players, OpPuppetPose);
            if (w == null) return;
            w.WriteByte((byte)Net.HostMember);
            w.WritePose(pose);
            NetOut.End(w);
        }

        // The host's P2 drunkenness is the truth; the client's body sways with it (9.6).
        static float DrunkAmount()
        {
            if (p2Drunk == null)
            {
                var p2 = CrewRoster.Get(Net.ClientMember);
                if (p2 != null) p2Drunk = p2.GetComponent<Drunkenness>();
            }
            return p2Drunk != null ? p2Drunk.Amount : 0f;
        }

        static bool WriteDrunk(float amount)
        {
            var w = NetOut.Reliable(NetSyncId.Players, OpDrunk);
            if (w == null) return false;
            w.WriteByte((byte)Net.ClientMember);
            w.WriteUnit(amount);
            NetOut.End(w);
            sentDrunk = Mathf.Round(Mathf.Clamp01(amount) * 255f) / 255f;
            return true;
        }

        // ---- receive ----

        public override void Receive(byte op, NetReader r)
        {
            var d = NetPlayerDriver.Active;
            switch (op)
            {
                case OpInputPose:
                {
                    // Nothing from the client counts before its snapshot is sent (3.6).
                    if (!Net.IsHost || !Net.PeerReady || d == null) return;
                    var p = d.RentPacket();
                    p.Read(r);
                    d.OnInputPose(p);
                    return;
                }
                case OpPuppetPose:
                {
                    if (!Net.IsClient) return;
                    int member = r.ReadByte();
                    var pose = r.ReadPose();
                    if (d != null) d.OnPuppetPose(member, NetOut.PeerTime, pose);
                    return;
                }
                case OpImpulse:
                {
                    if (!Net.IsClient) return;
                    var m = CrewRoster.Get(r.ReadByte());
                    var v = r.ReadVector3();
                    if (m != null && m.Controller != null && Net.Drives(m)) m.Controller.AddImpulse(v);
                    return;
                }
                case OpDrunk:
                {
                    if (!Net.IsClient) return;
                    var m = CrewRoster.Get(r.ReadByte());
                    float a = r.ReadUnit();
                    if (m != null) Drunkenness.NetSetAmount(m.gameObject, a);
                    return;
                }
                case OpInputAck:
                    if (Net.IsClient && d != null) d.OnInputAck(r.ReadUShort());
                    return;
            }
        }

        public override void OnPeerLeft()
        {
            var d = NetPlayerDriver.Active;
            if (d != null) d.OnPeerLeft();
        }

        public override void OnSessionEnd() { Reset(); }

        static void Reset()
        {
            nextPuppet = nextAck = nextDrunk = 0f;
            sentDrunk = -1f;
            p2Drunk = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Reset();
            NetSync.Register(new PlayerSync());
        }
    }
}
