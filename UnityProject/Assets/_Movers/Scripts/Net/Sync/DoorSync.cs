using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Doors (sys 6, NETCODE_SLICE 4.3, 11.3): every hinged panel's open state and every lock,
    // polled on the host each frame and sent on change. Nothing in the door code sends: a
    // leaf only has to be registered by the NetIds sweep to be replicated.
    //
    // The client applies with the replica paths: HingedPanel.Command (the leaf then swings
    // locally from the new goal), HingedPanel.Snap during the snapshot (no swing), and
    // DoorLock.locked written directly (no latch, no event). The rattle of a locked door has
    // no record of its own: the replayed DoorLockedRattle jiggles the leaf here.
    public sealed class DoorSync : NetSync
    {
        const byte OpPanel = 1;
        const byte OpLock = 2;

        public override NetSyncId Id => NetSyncId.Doors;

        // Registered panels and locks, in sweep order, with the state last sent (host).
        readonly List<HingedPanel> panels = new List<HingedPanel>();
        readonly List<uint> panelIds = new List<uint>();
        readonly List<bool> panelSent = new List<bool>();
        readonly List<DoorLock> locks = new List<DoorLock>();
        readonly List<uint> lockIds = new List<uint>();
        readonly List<bool> lockSent = new List<bool>();
        readonly Dictionary<uint, HingedPanel> panelById = new Dictionary<uint, HingedPanel>();
        readonly Dictionary<uint, DoorLock> lockById = new Dictionary<uint, DoorLock>();

        System.Action<WorldEvent> rattleListener;

        public override void OnSceneReady()
        {
            Clear();
            HingedPanel[] found = Object.FindObjectsByType<HingedPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                uint id = NetIds.IdOf(found[i].gameObject);
                if (id == 0 || panelById.ContainsKey(id)) continue;
                panelById.Add(id, found[i]);
                panels.Add(found[i]);
                panelIds.Add(id);
                panelSent.Add(found[i].IsOpen);
            }
            IReadOnlyList<DoorLock> all = DoorLock.All;
            for (int i = 0; i < all.Count; i++)
            {
                DoorLock l = all[i];
                uint id = l != null ? NetIds.IdOf(l.gameObject) : 0;
                if (id == 0 || lockById.ContainsKey(id)) continue;
                lockById.Add(id, l);
                locks.Add(l);
                lockIds.Add(id);
                lockSent.Add(l.locked);
            }
            NetLog.Write("DoorSync: " + panels.Count + " panels, " + locks.Count + " locks");

            if (Net.IsClient)
            {
                if (rattleListener == null) rattleListener = OnWorldEvent;
                WorldEvents.Subscribe(rattleListener);
            }
        }

        public override void HostTick()
        {
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p == null || p.IsOpen == panelSent[i]) continue;
                if (!Write(OpPanel, panelIds[i], p.IsOpen)) return;
                panelSent[i] = p.IsOpen;
            }
            for (int i = 0; i < locks.Count; i++)
            {
                DoorLock l = locks[i];
                if (l == null || l.locked == lockSent[i]) continue;
                if (!Write(OpLock, lockIds[i], l.locked)) return;
                lockSent[i] = l.locked;
            }
        }

        public override void SendSnapshot()
        {
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p == null) continue;
                panelSent[i] = p.IsOpen;
                Write(OpPanel, panelIds[i], p.IsOpen);
            }
            for (int i = 0; i < locks.Count; i++)
            {
                DoorLock l = locks[i];
                if (l == null) continue;
                lockSent[i] = l.locked;
                Write(OpLock, lockIds[i], l.locked);
            }
        }

        static bool Write(byte op, uint id, bool on)
        {
            NetWriter w = NetOut.Reliable(NetSyncId.Doors, op);
            if (w == null) return false;
            w.WriteUInt(id);
            w.WriteBool(on);
            NetOut.End(w);
            return true;
        }

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            uint id = r.ReadUInt();
            bool on = r.ReadBool();
            switch (op)
            {
                case OpPanel:
                    if (!panelById.TryGetValue(id, out HingedPanel p) || p == null) return;
                    // Before PeerReady the records are the snapshot: straight there, no swing.
                    if (Net.PeerReady) p.Command(on);
                    else p.Snap(on);
                    break;
                case OpLock:
                    if (lockById.TryGetValue(id, out DoorLock l) && l != null) l.locked = on;
                    break;
            }
        }

        // Client: the host's rattle, replayed. The leaf (or each locked leaf of the group) jiggles
        // in its frame; its sounds come from the host (ImpactAudio).
        void OnWorldEvent(WorldEvent e)
        {
            if (e.type != WorldEventType.DoorLockedRattle || !WorldEventRelay.Replaying) return;
            if (e.subject is HingedPanel panel)
            {
                panel.Jiggle(e.instigator);
                return;
            }
            if (e.subject is HingedGroup group)
            {
                List<HingedPanel> leaves = group.panels;
                for (int i = 0; i < leaves.Count; i++)
                {
                    HingedPanel p = leaves[i];
                    if (p != null && p.CanSwing && p.IsLocked) p.Jiggle(e.instigator);
                }
            }
        }

        public override void OnSessionEnd()
        {
            if (rattleListener != null) WorldEvents.Unsubscribe(rattleListener);
            Clear();
        }

        void Clear()
        {
            panels.Clear();
            panelIds.Clear();
            panelSent.Clear();
            locks.Clear();
            lockIds.Clear();
            lockSent.Clear();
            panelById.Clear();
            lockById.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new DoorSync());
    }
}
