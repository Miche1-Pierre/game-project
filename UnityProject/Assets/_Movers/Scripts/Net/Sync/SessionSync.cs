using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // GameSession's public state as the State record carries it (NETCODE_SLICE 4.3, Session op 1).
    public struct SessionReplica
    {
        public SessionState state;
        public FailReason failure;
        public float timeLimit, timeLeft;
        public bool hasIntro, cardShowing, brokeIn;
        public int breakInBy, startedBy;
        public string breakInWhat, startedHow;
        public float policeRemaining;               // seconds to the police's arrival; -1 = no police called
        // The flee (ADR-013), protocol 2.
        public MissionPhase phase;
        public byte arrestedMask, pendingMask;
        public byte interceptTenths;                // tenths of a second before interception; NoIntercept = no warning
        public float fleeSeconds;                   // escape time left in PoliceHere; -1 = none
        public bool escaped;

        public const byte NoIntercept = 255;

        public static SessionReplica Of(GameSession s)
        {
            return new SessionReplica
            {
                state = Session.State,
                failure = Session.Failure,
                timeLimit = Session.TimeLimit,
                timeLeft = Session.TimeLeft,
                hasIntro = s.HasIntro,
                cardShowing = s.IntroCardShowing,
                brokeIn = s.BrokeIn,
                breakInBy = s.BreakInBy,
                breakInWhat = s.BreakInWhat,
                startedBy = s.StartedBy,
                startedHow = s.StartedHow,
                policeRemaining = s.PoliceIn,
                phase = Session.Phase,
                arrestedMask = Session.ArrestedMask,
                pendingMask = Session.ArrestPendingMask,
                interceptTenths = Tenths(s.InterceptLeft),
                fleeSeconds = s.FleeLeft,
                escaped = Session.Escaped,
            };
        }

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)state);
            w.WriteByte((byte)failure);
            w.WriteFloat(timeLimit);
            w.WriteFloat(timeLeft);
            byte flags = 0;
            if (hasIntro) flags |= 1;
            if (cardShowing) flags |= 2;
            if (brokeIn) flags |= 4;
            if (escaped) flags |= 8;
            w.WriteByte(flags);
            w.WriteSByte((sbyte)breakInBy);
            w.WriteByte(Code(GameSession.BreakInWords, breakInWhat));
            w.WriteSByte((sbyte)startedBy);
            w.WriteByte(Code(GameSession.StartedHowWords, startedHow));
            w.WriteFloat(policeRemaining);
            w.WriteByte((byte)phase);
            w.WriteByte(arrestedMask);
            w.WriteByte(pendingMask);
            w.WriteByte(interceptTenths);
            w.WriteFloat(fleeSeconds);
        }

        public static SessionReplica Read(NetReader r)
        {
            var s = new SessionReplica();
            s.state = (SessionState)r.ReadByte();
            s.failure = (FailReason)r.ReadByte();
            s.timeLimit = r.ReadFloat();
            s.timeLeft = r.ReadFloat();
            byte flags = r.ReadByte();
            s.hasIntro = (flags & 1) != 0;
            s.cardShowing = (flags & 2) != 0;
            s.brokeIn = (flags & 4) != 0;
            s.escaped = (flags & 8) != 0;
            s.breakInBy = r.ReadSByte();
            s.breakInWhat = Word(GameSession.BreakInWords, r.ReadByte());
            s.startedBy = r.ReadSByte();
            s.startedHow = Word(GameSession.StartedHowWords, r.ReadByte());
            s.policeRemaining = r.ReadFloat();
            s.phase = (MissionPhase)r.ReadByte();
            s.arrestedMask = r.ReadByte();
            s.pendingMask = r.ReadByte();
            s.interceptTenths = r.ReadByte();
            s.fleeSeconds = r.ReadFloat();
            return s;
        }

        static byte Tenths(float seconds) =>
            seconds < 0f ? NoIntercept : (byte)Mathf.Clamp(Mathf.RoundToInt(seconds * 10f), 0, NoIntercept - 1);

        static byte Code(string[] words, string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            for (int i = 1; i < words.Length; i++)
                if (words[i] == s) return (byte)i;
            Debug.LogWarning("SessionSync: '" + s + "' has no wire code, sent as empty");
            return 0;
        }

        static string Word(string[] words, byte code) => code < words.Length ? words[code] : "";
    }

    // Session (sys 10): state, clock, settlement, ledger (NETCODE_SLICE 4.3, 11.7). The host
    // sends at every GameSession transition (hooks) and polls the clock and the ledger; the
    // client writes them into its GameSession and TheftLedger, which never simulate online.
    public sealed class SessionSync : NetSync
    {
        const byte OpState = 1, OpClock = 2, OpSettlementLine = 3, OpLedgerPut = 4,
                   OpSettlementEnd = 5, OpLedgerDrop = 6, OpLedgerFinal = 7;
        const float ClockInterval = 0.5f;           // 2 Hz
        const float LedgerInterval = 0.25f;         // at most 4 Hz

        struct SentEntry
        {
            public int thief, value;
            public TheftRoute route;
            public bool witnessed, damaged;
            public bool Same(in SentEntry o) =>
                thief == o.thief && value == o.value && route == o.route && witnessed == o.witnessed && damaged == o.damaged;
        }

        public override NetSyncId Id => NetSyncId.Session;

        // Host: what the client has of the ledger, to send only the differences.
        readonly Dictionary<uint, SentEntry> sent = new Dictionary<uint, SentEntry>();
        readonly HashSet<uint> present = new HashSet<uint>();
        readonly List<uint> gone = new List<uint>();
        TheftLedger sentLedger;
        int sentVersion = -1;
        bool sentFinal;
        float nextClock, nextLedger;
        // Client: the settlement lines received so far, until SettlementEnd.
        readonly List<Settlement.Line> lines = new List<Settlement.Line>();

        static SessionSync Instance => NetSync.Get(NetSyncId.Session) as SessionSync;

        // ---- host hooks (GameSession) ----

        public static void SendState(GameSession s)
        {
            if (!Net.IsHost || s == null || s != GameSession.Current) return;
            var w = NetOut.Reliable(NetSyncId.Session, OpState);
            if (w == null) return;
            SessionReplica.Of(s).Write(w);
            NetOut.End(w);
        }

        // The end of the run: the ledger as it stands, LedgerFinal, then every line and
        // SettlementEnd, all before the end State that SetState writes right after.
        public static void SendSettlement(GameSession s)
        {
            var sync = Instance;
            if (!Net.IsHost || sync == null || s == null || s != GameSession.Current) return;
            sync.SendLedger(s.Ledger, true);
            var result = s.Result;
            if (result == null) return;
            for (int i = 0; i < result.Lines.Count; i++)
            {
                var line = result.Lines[i];
                var w = NetOut.Reliable(NetSyncId.Session, OpSettlementLine);
                if (w == null) return;
                w.WriteString(line.label);
                w.WriteString(line.detail);
                w.WriteInt(line.amount);
                NetOut.End(w);
            }
            var end = NetOut.Reliable(NetSyncId.Session, OpSettlementEnd);
            if (end == null) return;
            // Flags (protocol 2): 1 completed, 2 escaped.
            end.WriteByte((byte)((result.Completed ? 1 : 0) | (result.Escaped ? 2 : 0)));
            end.WriteByte((byte)result.Failure);
            end.WriteInt(result.Total);
            NetOut.End(end);
        }

        // ---- NetSync ----

        public override void HostTick()
        {
            var s = GameSession.Current;
            if (s == null) return;
            float now = Time.unscaledTime;
            if (now >= nextClock)
            {
                nextClock = now + ClockInterval;
                if (Session.IsRunning)
                {
                    var w = NetOut.Unreliable(NetSyncId.Session, OpClock);
                    if (w != null) { w.WriteFloat(Session.TimeLeft); NetOut.End(w); }
                }
            }
            if (now >= nextLedger)
            {
                nextLedger = now + LedgerInterval;
                SendLedger(s.Ledger, false);
            }
        }

        public override void SendSnapshot()
        {
            var s = GameSession.Current;
            if (s == null) return;
            ResetLedger();
            SendLedger(s.Ledger, true);
            if (s.Result != null) SendSettlement(s);
            SendState(s);
        }

        public override void OnSessionEnd()
        {
            ResetLedger();
            lines.Clear();
            nextClock = nextLedger = 0f;
        }

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            var s = GameSession.Current;
            switch (op)
            {
                case OpState:
                {
                    var replica = SessionReplica.Read(r);
                    if (s != null) s.ApplyReplica(replica);
                    return;
                }
                case OpClock:
                {
                    float left = r.ReadFloat();
                    if (s != null) s.ApplyClock(left);
                    return;
                }
                case OpSettlementLine:
                    lines.Add(new Settlement.Line { label = r.ReadString() ?? "", detail = r.ReadString() ?? "", amount = r.ReadInt() });
                    return;
                case OpSettlementEnd:
                {
                    byte flags = r.ReadByte();
                    var failure = (FailReason)r.ReadByte();
                    r.ReadInt();   // the total: the sum of the lines, rebuilt by FromReplica
                    if (s != null) s.ApplyResult(Settlement.FromReplica((flags & 1) != 0, (flags & 2) != 0, failure, lines));
                    lines.Clear();
                    return;
                }
                case OpLedgerPut:
                {
                    var item = Movable(r.ReadUInt());
                    int thief = r.ReadSByte();
                    var route = (TheftRoute)r.ReadByte();
                    int value = r.ReadInt();
                    bool witnessed = r.ReadBool();
                    if (s != null && item != null) s.Ledger.ApplyPut(item, thief, route, value, witnessed);
                    return;
                }
                case OpLedgerDrop:
                {
                    var item = Movable(r.ReadUInt());
                    if (s != null && item != null) s.Ledger.ApplyDrop(item);
                    return;
                }
                case OpLedgerFinal:
                    if (s != null) s.Ledger.ApplyFinal();
                    return;
            }
        }

        static MovableObject Movable(uint id)
        {
            var go = NetIds.Find(id);
            return go != null && go.TryGetComponent(out MovableObject m) ? m : null;
        }

        // ---- the ledger, as differences (one entry per loot item, up to about 120) ----

        void ResetLedger()
        {
            sent.Clear();
            sentLedger = null;
            sentVersion = -1;
            sentFinal = false;
        }

        void SendLedger(TheftLedger ledger, bool force)
        {
            if (ledger == null) return;
            if (ledger != sentLedger) { ResetLedger(); sentLedger = ledger; }
            if (!force && ledger.Version == sentVersion) return;
            // Nothing is marked sent unless the record really went (NetOut may refuse).
            if (!NetOut.CanSend) return;
            sentVersion = ledger.Version;

            present.Clear();
            var entries = ledger.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var en = entries[i];
                if (en.item == null) continue;
                uint id = NetIds.IdOf(en.item.gameObject);
                if (id == 0) continue;
                present.Add(id);
                var now = new SentEntry { thief = en.thief, value = en.value, route = en.route, witnessed = en.witnessed, damaged = en.Damaged };
                if (sent.TryGetValue(id, out var was) && was.Same(now)) continue;
                var w = NetOut.Reliable(NetSyncId.Session, OpLedgerPut);
                if (w == null) return;
                w.WriteUInt(id);
                w.WriteSByte((sbyte)en.thief);
                w.WriteByte((byte)en.route);
                w.WriteInt(en.value);
                w.WriteBool(en.witnessed);
                NetOut.End(w);
                sent[id] = now;
            }

            gone.Clear();
            foreach (var id in sent.Keys)
                if (!present.Contains(id)) gone.Add(id);
            for (int i = 0; i < gone.Count; i++)
            {
                var w = NetOut.Reliable(NetSyncId.Session, OpLedgerDrop);
                if (w == null) return;
                w.WriteUInt(gone[i]);
                NetOut.End(w);
                sent.Remove(gone[i]);
            }

            if (ledger.IsFinal && !sentFinal)
            {
                var w = NetOut.Reliable(NetSyncId.Session, OpLedgerFinal);
                if (w == null) return;
                NetOut.End(w);
                sentFinal = true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new SessionSync());
    }
}
