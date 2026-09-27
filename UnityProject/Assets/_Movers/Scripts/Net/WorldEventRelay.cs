using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Forwards every WorldEvent raised on the host to the client, in raise order (NETCODE_SLICE
    // 8). It is not a listener: WorldEvents.Raise calls OnRaised when Net.IsOnline, so offline
    // the listener list and its count are the baseline's. The client re-raises through
    // WorldEvents.Raise with Replaying set; any other raise on the client is a bug and warns.
    public static class WorldEventRelay
    {
        const byte OpEvent = 1;

        static bool replaying;
        static readonly List<WorldEvent> heldKnockdowns = new List<WorldEvent>();

        public static bool Replaying => replaying;

        internal static void OnRaised(in WorldEvent e)
        {
            if (Net.IsClient)
            {
                if (!replaying && e.type != WorldEventType.SessionStateChanged)
                    Debug.LogWarning("WorldEventRelay: " + e.type + " raised locally on the client (" + e + ")");
                return;
            }
            if (!Net.IsHost || e.type == WorldEventType.SessionStateChanged) return;

            // An explosion raises its knockdowns before itself (Explosion.Run step 5); the client
            // must see the Explosion first, so KnockdownTumble finds it (4.2).
            if (e.type == WorldEventType.PlayerKnockedDown)
            {
                if (NetOut.CanSend) heldKnockdowns.Add(e);
                return;
            }
            Write(e);
            if (e.type == WorldEventType.Explosion) FlushHeld();
        }

        // At Tick, for knockdowns no Explosion followed.
        internal static void FlushHeld()
        {
            if (heldKnockdowns.Count == 0) return;
            for (int i = 0; i < heldKnockdowns.Count; i++) Write(heldKnockdowns[i]);
            heldKnockdowns.Clear();
        }

        static void Write(in WorldEvent e)
        {
            var w = NetOut.Reliable(NetSyncId.Events, OpEvent);
            if (w == null) return;
            w.WriteByte((byte)e.type);
            w.WriteVector3(e.position);
            w.WriteSByte((sbyte)Mathf.Clamp(e.instigator, sbyte.MinValue, sbyte.MaxValue));
            w.WriteUnit(e.loudness);
            w.WriteFloat(e.magnitude);
            w.WriteInt(e.value);
            w.WriteRef(e.subject);
            NetOut.End(w);
        }

        static void Receive(byte op, NetReader r)
        {
            if (op != OpEvent || !Net.IsClient) return;
            var type = (WorldEventType)r.ReadByte();
            var pos = r.ReadVector3();
            int instigator = r.ReadSByte();
            float loudness = r.ReadUnit();
            float magnitude = r.ReadFloat();
            int value = r.ReadInt();
            var subject = NetIds.Resolve(r.ReadRef());
            bool was = replaying;
            replaying = true;
            try { WorldEvents.Raise(new WorldEvent(type, pos, instigator, loudness, magnitude, value, subject)); }
            finally { replaying = was; }
        }

        sealed class EventsSync : NetSync
        {
            public override NetSyncId Id => NetSyncId.Events;
            public override void Receive(byte op, NetReader r) => WorldEventRelay.Receive(op, r);
            public override void OnSessionEnd() => heldKnockdowns.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            replaying = false;
            heldKnockdowns.Clear();
            NetSync.Register(new EventsSync());
        }
    }
}
