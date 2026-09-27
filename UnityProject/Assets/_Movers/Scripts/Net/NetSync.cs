using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The state stream (NETCODE_SLICE 4, 7). Each system writes one NetSync under Net/Sync/ and
    // owns the op numbers inside its NetSyncId. NetOut batches the records into the two NGO
    // named messages; nothing here references NGO (NetSession does the sending).
    public enum NetSyncId : byte
    {
        Control = 0, Events = 1, Spawns = 2, Items = 3, Structure = 4, Props = 5, Doors = 6,
        Players = 7, Truck = 8, Grandma = 9, Session = 10, Audio = 11, Transforms = 12, Count = 13
    }   // order = snapshot order

    public abstract class NetSync
    {
        public abstract NetSyncId Id { get; }
        public virtual void OnSceneReady() { }              // both roles, online, right after the NetIds sweep; subscribe here
        public virtual void HostTick() { }                  // host, every frame at NetOrder.Tick while Net.PeerReady
        public virtual void ClientTick() { }                // client, every frame at NetOrder.Tick
        public virtual void SendSnapshot() { }              // host, once per Ready, writes records via NetOut.Reliable
        public abstract void Receive(byte op, NetReader r); // a record of this sync from the peer
        public virtual void Digest(ref ulong hash) { }      // optional, manual desync hunting
        public virtual void OnPeerLeft() { }                // host: the client is gone
        public virtual void OnSessionEnd() { }              // both: leave or reload; unsubscribe and clear caches here

        // Slots indexed by Id. Overwrites, never clears, no reset of its own: the order of the
        // RuntimeInitializeOnLoadMethod calls that register the syncs does not matter.
        static readonly NetSync[] slots = new NetSync[(int)NetSyncId.Count];

        public static void Register(NetSync sync)
        {
            if (sync == null) return;
            int i = (int)sync.Id;
            if (i < 0 || i >= slots.Length) { Debug.LogError("NetSync: bad id " + sync.Id); return; }
            slots[i] = sync;
        }

        public static NetSync Get(NetSyncId id)
        {
            int i = (int)id;
            return i >= 0 && i < slots.Length ? slots[i] : null;
        }

        public static void HashInto(ref ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (byte)(value >> (i * 8));
                hash *= 1099511628211UL;
            }
        }

        // ---- the per-frame loop, run by NetSession.LateUpdate at NetOrder.Tick ----
        // Order within a frame (section 12): HostTick of every sync (the transform stream is the
        // last slot), held knockdowns, Despawn detection, then the flush.
        internal static void RunTick()
        {
            if (!Net.IsOnline) return;
            if (Net.IsHost)
            {
                if (Net.PeerReady)
                    for (int i = 0; i < slots.Length; i++) Safe(slots[i], true);
                WorldEventRelay.FlushHeld();
                NetSpawns.PollDespawns();
            }
            else
            {
                for (int i = 0; i < slots.Length; i++) Safe(slots[i], false);
            }
            NetOut.Flush();
            NetStats.Tick();
        }

        static void Safe(NetSync s, bool host)
        {
            if (s == null) return;
            try { if (host) s.HostTick(); else s.ClientTick(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        internal static void ForEach(Action<NetSync> action)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                try { action(slots[i]); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    public static class NetOut
    {
        public const int MaxRecord = 1000;          // payload bytes; larger: LogError and drop
        public const int MaxBatch = 1100;           // a named message never grows past this (4.1)
        const int BatchHeader = 5;                  // epoch u8 | time f32
        const int RecordHeader = 4;                 // sys u8 | op u8 | len u16
        const int MaxReliablePerFrame = 8;

        sealed class Channel
        {
            public readonly bool reliable;
            public readonly NetWriter record = new NetWriter(1200);
            public readonly NetWriter batch = new NetWriter(1200);
            public readonly Queue<byte[]> queue = new Queue<byte[]>();
            public bool recordOpen, batchOpen;
            public NetSyncId sys;
            public byte op;
            public Channel(bool reliable) { this.reliable = reliable; }
        }

        static readonly Channel rel = new Channel(true);
        static readonly Channel unrel = new Channel(false);
        static readonly NetReader reader = new NetReader();
        static float clockStart;

        public static bool CanSend => Net.IsOnline && Net.PeerConnected && (Net.PeerReady || InSnapshot);

        // This machine's session clock (the hostTime / clientTime written in batches).
        public static float Now => Time.unscaledTime - clockStart;
        // The sender's clock of the batch being dispatched right now.
        public static float PeerTime { get; private set; }
        // The peer's clock now, estimated from the batches received (6: minimum offset over 2 s).
        public static float PeerNow => NetClock.PeerNow;

        public static NetWriter Reliable(NetSyncId sys, byte op) => Open(rel, sys, op);
        public static NetWriter Unreliable(NetSyncId sys, byte op) => Open(unrel, sys, op);

        static NetWriter Open(Channel ch, NetSyncId sys, byte op)
        {
            if (sys == NetSyncId.Control ? !Net.IsOnline : !CanSend) return null;
            if (ch.recordOpen) RollBack(ch);
            ch.record.Clear();
            ch.record.WriteByte((byte)sys);
            ch.record.WriteByte(op);
            ch.record.WriteUShort(0);
            ch.recordOpen = true;
            ch.sys = sys;
            ch.op = op;
            return ch.record;
        }

        static void RollBack(Channel ch)
        {
            Debug.LogError("NetOut: record " + ch.sys + "/" + ch.op + " was never closed with End, dropped");
            ch.recordOpen = false;
            ch.record.Clear();
        }

        public static void End(NetWriter w)
        {
            if (w == null) return;
            var ch = w == rel.record ? rel : w == unrel.record ? unrel : null;
            if (ch == null || !ch.recordOpen) { Debug.LogError("NetOut.End without an open record"); return; }
            ch.recordOpen = false;
            int payload = w.Length - RecordHeader;
            if (payload > MaxRecord)
            {
                NetStats.DroppedOversize++;
                Debug.LogError("NetOut: record " + ch.sys + "/" + ch.op + " is " + payload + " B, over " + MaxRecord + ", dropped");
                return;
            }
            w.PatchUShort(2, (ushort)payload);
            if (ch.batchOpen && ch.batch.Length + w.Length > MaxBatch) CloseBatch(ch);
            if (!ch.batchOpen)
            {
                // The epoch is read when the batch opens, the time at its first record (4.1).
                ch.batch.Clear();
                ch.batch.WriteByte(Net.Epoch);
                ch.batch.WriteFloat(Now);
                ch.batchOpen = true;
            }
            ch.batch.Append(w.Buffer, 0, w.Length);
        }

        static void CloseBatch(Channel ch)
        {
            if (!ch.batchOpen) return;
            ch.batchOpen = false;
            if (ch.batch.Length <= BatchHeader) return;
            var msg = new byte[ch.batch.Length];
            Buffer.BlockCopy(ch.batch.Buffer, 0, msg, 0, msg.Length);
            ch.queue.Enqueue(msg);
        }

        internal static void CloseBatches()
        {
            CloseBatch(rel);
            CloseBatch(unrel);
        }

        public static void FlushNow() { Flush(false); }

        internal static void Flush() { Flush(true); }

        static void Flush(bool throttle)
        {
            if (rel.recordOpen) RollBack(rel);
            if (unrel.recordOpen) RollBack(unrel);
            CloseBatches();
            while (unrel.queue.Count > 0) SendOne(unrel.queue.Dequeue(), false);
            int budget = throttle && Net.IsHost ? MaxReliablePerFrame : int.MaxValue;
            while (rel.queue.Count > 0 && budget-- > 0) SendOne(rel.queue.Dequeue(), true);
        }

        static void SendOne(byte[] msg, bool reliable)
        {
            NetStats.CountOut(msg.Length);
            NetSession.Send(reliable, msg, msg.Length);
        }

        internal static bool InSnapshot { get; set; }   // NetSession: SnapshotBegin .. SnapshotEnd

        internal static void Dispatch(byte[] buf, int len)
        {
            NetStats.CountIn(len);
            if (buf == null || len < BatchHeader) return;
            byte epoch = buf[0];
            reader.Reset(buf, 1, 4);
            PeerTime = reader.ReadFloat();
            NetClock.OnBatch(PeerTime);
            int pos = BatchHeader;
            while (pos + RecordHeader <= len)
            {
                var sys = (NetSyncId)buf[pos];
                byte op = buf[pos + 1];
                int plen = buf[pos + 2] | (buf[pos + 3] << 8);
                int start = pos + RecordHeader;
                if (start + plen > len) { Debug.LogError("NetOut: truncated record " + sys + "/" + op); return; }
                pos = start + plen;
                if (sys != NetSyncId.Control && epoch != Net.Epoch) continue;   // stale world traffic
                var sync = NetSync.Get(sys);
                if (sync == null) continue;   // unknown system: skipped by its length
                reader.Reset(buf, start, plen);
                try { sync.Receive(op, reader); }
                catch (Exception e) { Debug.LogError("NetOut: record " + sys + "/" + op + " failed: " + e); }
            }
        }

        internal static void StartClock() { clockStart = Time.unscaledTime; NetClock.Reset(); NetStats.Reset(); }

        internal static void ResetQueues()
        {
            rel.recordOpen = unrel.recordOpen = false;
            rel.batchOpen = unrel.batchOpen = false;
            rel.queue.Clear();
            unrel.queue.Clear();
            InSnapshot = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ResetQueues();
            PeerTime = 0f;
            clockStart = 0f;
        }
    }

    // The peer's clock, from the time written in each batch: offset = the minimum of
    // (receive time - batch time) over the last 2 s, smoothed (NETCODE_SLICE 6).
    static class NetClock
    {
        struct Sample { public float at, offset; }
        static readonly List<Sample> samples = new List<Sample>();
        static float offset, lastUpdate;
        static bool has;

        public static float PeerNow => NetOut.Now - offset;
        public static bool HasEstimate => has;

        public static void OnBatch(float peerTime)
        {
            float now = NetOut.Now;
            samples.Add(new Sample { at = now, offset = now - peerTime });
            float min = float.MaxValue;
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (now - samples[i].at > 2f) { samples.RemoveAt(i); continue; }
                if (samples[i].offset < min) min = samples[i].offset;
            }
            if (!has) { offset = min; has = true; lastUpdate = now; return; }
            float dt = Mathf.Max(0f, now - lastUpdate);
            lastUpdate = now;
            // A lower minimum (a faster packet) is taken at once; a higher one slowly.
            offset = min < offset ? min : Mathf.Lerp(offset, min, Mathf.Clamp01(dt * 1f));
        }

        public static void Reset()
        {
            samples.Clear();
            offset = 0f;
            has = false;
        }
    }

    // Bandwidth and timing counters (4.1). Bytes handed to NGO plus 80 B per message for the
    // NGO, UTP, DTLS and UDP headers; written to the net log every 5 s.
    static class NetStats
    {
        public const int OverheadPerMessage = 80;
        public static int DroppedOversize;

        static float windowStart, logStart;
        static int outBytes, inBytes, outMsgs, inMsgs;         // current 1 s window
        static int logOut, logIn;                              // current 5 s window
        static float peakOut, peakIn;
        static bool loadWindow;
        static float longestFrame, lastLongest;
        static float steadyOut, steadyIn;                      // the last 5 s window, bytes/s

        // Read-only, for NetTestBot's check 9 (bytes/s): the 1 s peaks since Reset, and the
        // rates of the last 5 s window written to the NETSTATS line.
        public static float PeakOut => peakOut;
        public static float PeakIn => peakIn;
        public static float SteadyOut => steadyOut;
        public static float SteadyIn => steadyIn;

        public static void CountOut(int len) { outBytes += len + OverheadPerMessage; outMsgs++; }
        public static void CountIn(int len) { inBytes += len + OverheadPerMessage; inMsgs++; }

        // Between LoadScene / Reload and PeerReady: the longest frame (3.2, 14 check 2).
        public static void BeginLoadWindow() { loadWindow = true; longestFrame = 0f; }
        public static void EndLoadWindow()
        {
            if (!loadWindow) return;
            loadWindow = false;
            lastLongest = longestFrame;
            NetLog.Write("NETSTATS longest frame load..ready " + (longestFrame * 1000f).ToString("0") + " ms");
        }
        public static float LastLongestFrame => lastLongest;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (loadWindow) longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            if (now - windowStart >= 1f)
            {
                float span = now - windowStart;
                peakOut = Mathf.Max(peakOut, outBytes / span);
                peakIn = Mathf.Max(peakIn, inBytes / span);
                logOut += outBytes;
                logIn += inBytes;
                outBytes = inBytes = outMsgs = inMsgs = 0;
                windowStart = now;
            }
            if (now - logStart >= 5f)
            {
                float span = now - logStart;
                steadyOut = logOut / span;
                steadyIn = logIn / span;
                NetLog.Write("NETSTATS " + (Net.IsHost ? "host" : "client") +
                             " out " + (logOut / span / 1024f).ToString("0.0") + " KB/s (peak " + (peakOut / 1024f).ToString("0.0") +
                             ") in " + (logIn / span / 1024f).ToString("0.0") + " KB/s (peak " + (peakIn / 1024f).ToString("0.0") +
                             ") rtt " + (Net.RoundTrip * 1000f).ToString("0") + " ms oversize " + DroppedOversize);
                logOut = logIn = 0;
                logStart = now;
            }
        }

        public static void Reset()
        {
            windowStart = logStart = Time.unscaledTime;
            outBytes = inBytes = outMsgs = inMsgs = logOut = logIn = 0;
            peakOut = peakIn = steadyOut = steadyIn = 0f;
            loadWindow = false;
            longestFrame = lastLongest = 0f;
            DroppedOversize = 0;
        }
    }
}
