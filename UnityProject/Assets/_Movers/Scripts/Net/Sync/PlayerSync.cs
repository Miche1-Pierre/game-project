using UnityEngine;

namespace Movers
{
    // Players (sys 7): InputPose, PuppetPose, Impulse, Drunk, InputAck. Stub from CORE (NETCODE_SLICE 12): the PLAYERS track fills it.
    public sealed class PlayerSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Players;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new PlayerSync());
    }
}
