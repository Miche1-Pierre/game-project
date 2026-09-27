using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // Audio (sys 11, NETCODE_SLICE 4.3): the host's prop impacts (PropImpactSound), played again
    // on the client, whose replicated bodies are kinematic and never collide on their own. Sent
    // unreliable: a lost knock is not worth a resend. The per-item cooldown is PropImpactSound's.
    public sealed class AudioSync : NetSync
    {
        const byte OpPropImpact = 1;

        // Impact volumes reach a little over 1 (heavy things, PropImpactSound.weight); pitches
        // stay within 0.75..1.3. Both fit one byte in these ranges.
        const float VolumeRange = 1.5f;
        const float PitchMin = 0.5f, PitchRange = 1f;

        public override NetSyncId Id => NetSyncId.Audio;

        // Host: one PropImpactSound play, with the pitch it actually used.
        public static void PropImpact(SfxKind kind, Vector3 point, float volume, float pitch)
        {
            if (!Net.IsHost) return;
            var w = NetOut.Unreliable(NetSyncId.Audio, OpPropImpact);
            if (w == null) return;
            w.WriteByte((byte)kind);
            w.WriteVector3(point);
            w.WriteUnit(volume / VolumeRange);
            w.WriteUnit((pitch - PitchMin) / PitchRange);
            NetOut.End(w);
        }

        public override void Receive(byte op, NetReader r)
        {
            if (op != OpPropImpact || !Net.IsClient) return;
            var kind = (SfxKind)r.ReadByte();
            Vector3 point = r.ReadVector3();
            float volume = r.ReadUnit() * VolumeRange;
            float pitch = PitchMin + r.ReadUnit() * PitchRange;
            AudioDirector.PlayAt(kind, point, SoundPreset.Impact, volume, pitch);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new AudioSync());
    }
}
