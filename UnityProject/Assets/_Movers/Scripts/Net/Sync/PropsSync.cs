using UnityEngine;

namespace Movers
{
    // Props (sys 5): breakables, glass, explosion FX, sounds, grenades. Stub from CORE (NETCODE_SLICE 12): the DESTRUCTION track fills it.
    public sealed class PropsSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Props;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new PropsSync());
    }
}
