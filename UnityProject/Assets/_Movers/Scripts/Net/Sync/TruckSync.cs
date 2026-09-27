using UnityEngine;

namespace Movers
{
    // Truck (sys 8): state, seats, ramp, cargo. Stub from CORE (NETCODE_SLICE 12): the GAMELOOP-TRUCK track fills it.
    public sealed class TruckSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Truck;
        // Bumped by every SeatEnter / SeatExit; a pose older than it is skipped (9.3).
        public static byte SeatEpoch => 0;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new TruckSync());
    }
}
