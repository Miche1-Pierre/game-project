using UnityEngine;

namespace Movers
{
    // Items (sys 3): flags, pockets, worn, cigarette, beer, smoke puffs, splats, hints. Stub from CORE (NETCODE_SLICE 12): the PLAYERS track fills it.
    public sealed class ItemSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Items;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new ItemSync());
    }
}
