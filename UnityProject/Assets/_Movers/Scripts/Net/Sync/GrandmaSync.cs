using UnityEngine;

namespace Movers
{
    // Grandma (sys 9): flags, mood, activity, prop, animation, speech, fire. Stub from CORE (NETCODE_SLICE 12): the GRANDMA track fills it.
    public sealed class GrandmaSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Grandma;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new GrandmaSync());
    }
}
