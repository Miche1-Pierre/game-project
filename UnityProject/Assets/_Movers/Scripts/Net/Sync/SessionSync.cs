using UnityEngine;

namespace Movers
{
    // Session (sys 10): state, clock, settlement, ledger. Stub from CORE (NETCODE_SLICE 12): the GAMELOOP-TRUCK track fills it.
    public sealed class SessionSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Session;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new SessionSync());
    }
}
