using UnityEngine;

namespace Movers
{
    // Doors (sys 6): panels and locks, polled on the host. Stub from CORE (NETCODE_SLICE 12): the INTERACTION track fills it.
    public sealed class DoorSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Doors;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new DoorSync());
    }
}
