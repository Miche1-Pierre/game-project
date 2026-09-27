using UnityEngine;

namespace Movers
{
    // Audio (sys 11): prop impact sounds. Stub from CORE (NETCODE_SLICE 12): the UI-AUDIO track fills it.
    public sealed class AudioSync : NetSync
    {
        public override NetSyncId Id => NetSyncId.Audio;

        public override void Receive(byte op, NetReader r) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new AudioSync());
    }
}
