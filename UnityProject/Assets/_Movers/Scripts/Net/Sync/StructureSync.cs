using UnityEngine;

namespace Movers
{
    // Structure (sys 4): fractures, chunk detaches, module states, roof falls. Stub from CORE (NETCODE_SLICE 12): the DESTRUCTION track fills it.
    public sealed class StructureSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Structure;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new StructureSync());
    }
}
