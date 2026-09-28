using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Truck (sys 8): driving state, seats, ramp, cargo totals (NETCODE_SLICE 4.3, 9.6, 11.7). The
    // truck's pose rides the transform stream (it is a Body); this sync carries the rest. The
    // host sends a seat at each change (VehicleSeat hooks) and polls the others; the client
    // writes them into the same components, which do not simulate there. Map01 has one truck.
    //
    // Seats (DEV 2, 6.3): the crew truck's seats are bound in seatIndex order (0 the driver, 1 the
    // passenger), and SeatEnter, SeatExit and SeatNotice end with the seat index (Protocol 2).
    public sealed class TruckSync : NetSync
    {
        const byte OpState = 1, OpSeatEnter = 2, OpSeatExit = 3, OpSeatNotice = 4, OpRamp = 5, OpCargo = 6;
        public const byte NoticeNoRoom = 1;
        const float StateInterval = 1f / 20f;       // 20 Hz while the body is awake
        const float IdleStateInterval = 1f;         // and a keep-alive while it sleeps

        public override NetSyncId Id => NetSyncId.Truck;

        static byte seatEpoch;
        static float remoteChaseYaw = float.NaN;
        static TruckVehicle truck;
        static readonly List<VehicleSeat> seats = new List<VehicleSeat>();

        // Bumped by every SeatEnter / SeatExit; a pose older than it is skipped (9.3). The host
        // bumps it only for a record that is really written, so both machines count the same
        // records (none while loading, when the scene's reset brings both back to 0).
        public static byte SeatEpoch => seatEpoch;

        // Client: the heading of this machine's chase camera while its player sits in the truck
        // (either seat), sent with the input (PlayerSync, InputPose chaseYaw). NaN when he is not
        // seated.
        public static float LocalChaseYaw
        {
            get
            {
                for (int i = 0; i < seats.Count; i++)
                {
                    var s = seats[i];
                    if (s != null && s.Occupant != null && Net.IsLocal(s.Occupant)) return s.ChaseYaw;
                }
                return float.NaN;
            }
        }

        // Host: the chase yaw the client last sent (PlayerSync calls this on each InputPose
        // with the seated flag). VehicleSeat faces the client's player that way getting out.
        public static void SetRemoteChaseYaw(float yaw) { remoteChaseYaw = yaw; }

        public static float RemoteChaseYaw(float fallback) => float.IsNaN(remoteChaseYaw) ? fallback : remoteChaseYaw;

        // Host: last values sent, to send on change.
        float nextState;
        byte sentRamp = 255;
        float sentKg = -1f, sentVolume = -1f;
        int sentCount = -1;

        // ---- host hooks (VehicleSeat) ----

        public static void SeatEnter(int member, int seatIndex)
        {
            if (!Net.IsHost || !NetOut.CanSend) return;
            seatEpoch++;
            WriteSeatEnter(member, seatIndex);
        }

        public static void SeatExit(int member, Vector3 rootPosition, float yaw, int seatIndex)
        {
            if (!Net.IsHost) return;
            var w = NetOut.Reliable(NetSyncId.Truck, OpSeatExit);
            if (w == null) return;
            seatEpoch++;
            w.WriteByte((byte)member);
            w.WriteVector3(rootPosition);
            w.WriteAngle(yaw);
            w.WriteByte(seatEpoch);
            w.WriteByte((byte)seatIndex);
            NetOut.End(w);
        }

        // The occupant is gone without being put anywhere (destroyed, scene unloading).
        public static void SeatForgotten(int member, Transform seatPoint, int seatIndex)
        {
            if (!Net.IsHost) return;
            if (!NetOut.CanSend || seatPoint == null) return;
            SeatExit(member, seatPoint.position, seatPoint.eulerAngles.y, seatIndex);
        }

        public static void SeatNotice(int member, byte code, int seatIndex)
        {
            if (!Net.IsHost) return;
            var w = NetOut.Reliable(NetSyncId.Truck, OpSeatNotice);
            if (w == null) return;
            w.WriteByte((byte)member);
            w.WriteByte(code);
            w.WriteByte((byte)seatIndex);
            NetOut.End(w);
        }

        static void WriteSeatEnter(int member, int seatIndex)
        {
            var w = NetOut.Reliable(NetSyncId.Truck, OpSeatEnter);
            if (w == null) return;
            w.WriteByte((byte)member);
            w.WriteByte(seatEpoch);
            w.WriteByte((byte)seatIndex);
            NetOut.End(w);
        }

        // The crew truck's seat with this index, or null.
        static VehicleSeat SeatAt(int seatIndex)
        {
            for (int i = 0; i < seats.Count; i++)
                if (seats[i] != null && seats[i].seatIndex == seatIndex) return seats[i];
            return null;
        }

        // ---- NetSync ----

        public override void OnSceneReady()
        {
            truck = Object.FindAnyObjectByType<TruckVehicle>();
            seats.Clear();
            if (truck == null) return;
            foreach (var s in Object.FindObjectsByType<VehicleSeat>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.vehicle == truck) seats.Add(s);
            seats.Sort((a, b) => a.seatIndex.CompareTo(b.seatIndex));
        }

        public override void OnSessionEnd()
        {
            truck = null;
            seats.Clear();
            seatEpoch = 0;
            remoteChaseYaw = float.NaN;
            ResetSent();
        }

        void ResetSent()
        {
            nextState = 0f;
            sentRamp = 255;
            sentKg = sentVolume = -1f;
            sentCount = -1;
        }

        public override void HostTick()
        {
            if (truck == null) return;
            float now = Time.unscaledTime;
            if (now >= nextState)
            {
                var body = truck.Body;
                bool awake = body != null && !body.IsSleeping();
                nextState = now + (awake ? StateInterval : IdleStateInterval);
                SendState(false);
            }
            if (truck.ramp != null && truck.ramp.NetState != sentRamp) SendRamp();
            var cargo = truck.cargo;
            if (cargo != null && (!Mathf.Approximately(cargo.LoadedKg, sentKg) || !Mathf.Approximately(cargo.LoadedVolume, sentVolume)
                                  || cargo.LoadedCount != sentCount))
                SendCargo();
        }

        public override void SendSnapshot()
        {
            ResetSent();
            if (truck == null) return;
            SendState(true);
            for (int i = 0; i < seats.Count; i++)
            {
                var s = seats[i];
                if (s != null && s.Occupant != null) WriteSeatEnter(s.Occupant.index, s.seatIndex);
            }
            if (truck.ramp != null) SendRamp();
            if (truck.cargo != null) SendCargo();
        }

        public override void OnPeerLeft()
        {
            // The client sat in the truck: out at the door, whichever seat, so P1 can take it.
            for (int i = 0; i < seats.Count; i++)
            {
                var s = seats[i];
                if (s != null && s.Occupant != null && s.Occupant.index == Net.ClientMember) s.ForceRelease();
            }
            remoteChaseYaw = float.NaN;
        }

        void SendState(bool reliable)
        {
            var w = reliable ? NetOut.Reliable(NetSyncId.Truck, OpState) : NetOut.Unreliable(NetSyncId.Truck, OpState);
            if (w == null) return;
            w.WriteHalf(truck.ForwardSpeed);
            w.WriteHalf(truck.SteerAngle);
            w.WriteSByte((sbyte)Mathf.RoundToInt(Mathf.Clamp(truck.Pedal, -1f, 1f) * 127f));
            w.WriteBool(truck.Handbrake);
            w.WriteVector3Half(truck.Velocity);
            NetOut.End(w);
        }

        void SendRamp()
        {
            var w = NetOut.Reliable(NetSyncId.Truck, OpRamp);
            if (w == null) return;
            sentRamp = truck.ramp.NetState;
            w.WriteByte(sentRamp);
            NetOut.End(w);
        }

        void SendCargo()
        {
            var cargo = truck.cargo;
            var w = NetOut.Reliable(NetSyncId.Truck, OpCargo);
            if (w == null) return;
            sentKg = cargo.LoadedKg;
            sentVolume = cargo.LoadedVolume;
            sentCount = cargo.LoadedCount;
            w.WriteHalf(sentKg);
            w.WriteHalf(sentVolume);
            w.WriteByte((byte)Mathf.Clamp(sentCount, 0, 255));
            NetOut.End(w);
        }

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            switch (op)
            {
                case OpState:
                {
                    float forward = r.ReadHalf();
                    float steer = r.ReadHalf();
                    float pedal = r.ReadSByte() / 127f;
                    bool handbrake = r.ReadBool();
                    Vector3 velocity = r.ReadVector3Half();
                    if (truck != null) truck.ApplyReplica(forward, steer, pedal, handbrake, velocity);
                    return;
                }
                case OpSeatEnter:
                {
                    var m = CrewRoster.Get(r.ReadByte());
                    seatEpoch = r.ReadByte();
                    var seat = SeatAt(r.ReadByte());
                    if (seat == null || m == null) return;
                    // Still seated elsewhere here (a switch of seats): out of that one first.
                    var other = VehicleSeat.Of(m);
                    if (other != null && other != seat) other.ApplyExit(m, m.transform.position, m.transform.eulerAngles.y);
                    seat.ApplyEnter(m);
                    return;
                }
                case OpSeatExit:
                {
                    var m = CrewRoster.Get(r.ReadByte());
                    Vector3 pos = r.ReadVector3();
                    float yaw = r.ReadAngle();
                    seatEpoch = r.ReadByte();
                    var seat = SeatAt(r.ReadByte());
                    if (seat != null && m != null) seat.ApplyExit(m, pos, yaw);
                    return;
                }
                case OpSeatNotice:
                {
                    int member = r.ReadByte();
                    byte code = r.ReadByte();
                    var seat = SeatAt(r.ReadByte());
                    if (seat != null && member == Net.LocalMember) seat.ApplyNotice(code);
                    return;
                }
                case OpRamp:
                {
                    byte state = r.ReadByte();
                    // Before SnapshotEnd the record is the snapshot: snap, silently.
                    if (truck != null && truck.ramp != null) truck.ramp.ApplyReplica(state, !Net.PeerReady);
                    return;
                }
                case OpCargo:
                {
                    float kg = r.ReadHalf();
                    float volume = r.ReadHalf();
                    int count = r.ReadByte();
                    if (truck != null && truck.cargo != null) truck.cargo.ApplyReplica(kg, volume, count);
                    return;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            seatEpoch = 0;
            remoteChaseYaw = float.NaN;
            truck = null;
            seats.Clear();
            NetSync.Register(new TruckSync());
        }
    }
}
