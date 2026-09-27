using UnityEngine;

namespace Movers
{
    // Truck (sys 8): driving state, seat, ramp, cargo totals (NETCODE_SLICE 4.3, 9.6, 11.7). The
    // truck's pose rides the transform stream (it is a Body); this sync carries the rest. The
    // host sends the seat at each change (VehicleSeat hooks) and polls the others; the client
    // writes them into the same components, which do not simulate there. Map01 has one truck.
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
        static VehicleSeat seat;

        // Bumped by every SeatEnter / SeatExit; a pose older than it is skipped (9.3). The host
        // bumps it only for a record that is really written, so both machines count the same
        // records (none while loading, when the scene's reset brings both back to 0).
        public static byte SeatEpoch => seatEpoch;

        // Client: the heading of this machine's chase camera while its player drives, sent with
        // the input (PlayerSync, InputPose chaseYaw). NaN when he is not at the wheel.
        public static float LocalChaseYaw
        {
            get
            {
                if (seat == null || seat.Driver == null || !Net.IsLocal(seat.Driver)) return float.NaN;
                return seat.ChaseYaw;
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

        public static void SeatEnter(int member)
        {
            if (!Net.IsHost || !NetOut.CanSend) return;
            seatEpoch++;
            WriteSeatEnter(member);
        }

        public static void SeatExit(int member, Vector3 rootPosition, float yaw)
        {
            if (!Net.IsHost) return;
            var w = NetOut.Reliable(NetSyncId.Truck, OpSeatExit);
            if (w == null) return;
            seatEpoch++;
            w.WriteByte((byte)member);
            w.WriteVector3(rootPosition);
            w.WriteAngle(yaw);
            w.WriteByte(seatEpoch);
            NetOut.End(w);
        }

        // The driver is gone without being put anywhere (destroyed, scene unloading).
        public static void SeatForgotten(int member, Transform seatPoint)
        {
            if (!Net.IsHost) return;
            if (!NetOut.CanSend || seatPoint == null) return;
            SeatExit(member, seatPoint.position, seatPoint.eulerAngles.y);
        }

        public static void SeatNotice(int member, byte code)
        {
            if (!Net.IsHost) return;
            var w = NetOut.Reliable(NetSyncId.Truck, OpSeatNotice);
            if (w == null) return;
            w.WriteByte((byte)member);
            w.WriteByte(code);
            NetOut.End(w);
        }

        static void WriteSeatEnter(int member)
        {
            var w = NetOut.Reliable(NetSyncId.Truck, OpSeatEnter);
            if (w == null) return;
            w.WriteByte((byte)member);
            w.WriteByte(seatEpoch);
            NetOut.End(w);
        }

        // ---- NetSync ----

        public override void OnSceneReady()
        {
            truck = Object.FindAnyObjectByType<TruckVehicle>();
            seat = null;
            if (truck == null) return;
            foreach (var s in Object.FindObjectsByType<VehicleSeat>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.vehicle == truck) { seat = s; break; }
        }

        public override void OnSessionEnd()
        {
            truck = null;
            seat = null;
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
            if (seat != null && seat.Driver != null) WriteSeatEnter(seat.Driver.index);
            if (truck.ramp != null) SendRamp();
            if (truck.cargo != null) SendCargo();
        }

        public override void OnPeerLeft()
        {
            // The client was at the wheel: out at the door, so P1 can drive.
            if (seat != null && seat.Driver != null && seat.Driver.index == Net.ClientMember) seat.ForceRelease();
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
                    if (seat != null && m != null) seat.ApplyEnter(m);
                    return;
                }
                case OpSeatExit:
                {
                    var m = CrewRoster.Get(r.ReadByte());
                    Vector3 pos = r.ReadVector3();
                    float yaw = r.ReadAngle();
                    seatEpoch = r.ReadByte();
                    if (seat != null && m != null) seat.ApplyExit(m, pos, yaw);
                    return;
                }
                case OpSeatNotice:
                {
                    int member = r.ReadByte();
                    byte code = r.ReadByte();
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
            seat = null;
            NetSync.Register(new TruckSync());
        }
    }
}
