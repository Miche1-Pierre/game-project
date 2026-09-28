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
    //
    // A leaf's record also says which side it opens to once someone has opened it (a door
    // swings away from whoever opens it: HingedPanel.SwingAwayFrom) and how far the house lets
    // it go there, so both screens swing it the same way and stop it at the same place. Two
    // bytes: the state (bit 0 open, bit 1 the negative side, bit 2 a side was picked), then the
    // cap in half degrees (0: none). A lock is one byte, 1 or 0.
    public sealed class DoorSync : NetSync
    {
        const byte OpPanel = 1;
        const byte OpLock = 2;

        public override NetSyncId Id => NetSyncId.Doors;

        // Registered panels and locks, in sweep order, with the state last sent (host).
        readonly List<HingedPanel> panels = new List<HingedPanel>();
        readonly List<uint> panelIds = new List<uint>();
        readonly List<int> panelSent = new List<int>();
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
                panelSent.Add(RecordOf(found[i]));
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
                if (p == null) continue;
                int record = RecordOf(p);
                if (record == panelSent[i]) continue;
                if (!WritePanel(panelIds[i], record)) return;
                panelSent[i] = record;
            }
            for (int i = 0; i < locks.Count; i++)
            {
                DoorLock l = locks[i];
                if (l == null || l.locked == lockSent[i]) continue;
                if (!WriteLock(lockIds[i], l.locked)) return;
                lockSent[i] = l.locked;
            }
        }

        public override void SendSnapshot()
        {
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p == null) continue;
                panelSent[i] = RecordOf(p);
                WritePanel(panelIds[i], panelSent[i]);
            }
            for (int i = 0; i < locks.Count; i++)
            {
                DoorLock l = locks[i];
                if (l == null) continue;
                lockSent[i] = l.locked;
                WriteLock(lockIds[i], l.locked);
            }
        }

        // The state in the low byte, the cap in half degrees in the next one.
        static int RecordOf(HingedPanel p)
        {
            int state = p.IsOpen ? 1 : 0;
            if (p.FullOpenAngle < 0f) state |= 2;
            if (p.SideChosen) state |= 4;
            int cap = Mathf.Clamp(Mathf.RoundToInt(p.SideCap * 2f), 0, 255);
            return state | (cap << 8);
        }

        static bool WritePanel(uint id, int record)
        {
            NetWriter w = NetOut.Reliable(NetSyncId.Doors, OpPanel);
            if (w == null) return false;
            w.WriteUInt(id);
            w.WriteByte((byte)(record & 0xFF));
            w.WriteByte((byte)(record >> 8));
            NetOut.End(w);
            return true;
        }

        static bool WriteLock(uint id, bool locked)
        {
            NetWriter w = NetOut.Reliable(NetSyncId.Doors, OpLock);
            if (w == null) return false;
            w.WriteUInt(id);
            w.WriteBool(locked);
            NetOut.End(w);
            return true;
        }

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            uint id = r.ReadUInt();
            byte state = r.ReadByte();
            bool on = (state & 1) != 0;
            switch (op)
            {
                case OpPanel:
                    byte cap = r.ReadByte();
                    if (!panelById.TryGetValue(id, out HingedPanel p) || p == null) return;
                    // The side first: the swing (or the snap) goes to the side the host picked.
                    if ((state & 4) != 0) p.ApplySide((state & 2) != 0 ? -1f : 1f, cap * 0.5f);
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
