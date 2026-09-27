using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Grandma (sys 9, NETCODE_SLICE 4.3, 11.6). The host runs her mind; the client shows it.
    // - Polled at HostTick, sent on change: Flags (state and flags, her body's collisions),
    //   Mood (also on the warning edges), Activity, Prop, Fire.
    // - Hooked at the transition, where polling would miss a repeat: Mood (right before each
    //   GrandmaMoodChanged, so the value lands before the event), Anim, Speech, Hush.
    // - Her root is on the transform stream (tracked in OnSceneReady, both roles).
    // The client applies through replica methods that raise nothing and draw no Random.
    public sealed class GrandmaSync : NetSync
    {
        const byte OpFlags = 1, OpMood = 2, OpActivity = 3, OpProp = 4, OpAnim = 5, OpSpeech = 6, OpHush = 7, OpFire = 8;
        // Snapshot only: an animator layer at a point of its clip, the line up with its time left.
        const byte OpAnimSnapshot = 9, OpSpeechSnapshot = 10;

        const byte BitKeys = 1, BitPolice = 2, BitAI = 4, BitSessionOver = 8, BitCollisions = 16, BitSeated = 32;
        const int Unsent = -1;

        public override NetSyncId Id => NetSyncId.Grandma;

        static GrandmaSync instance;

        GrandmaBrain brain;
        GrandmaMover mover;
        GrandmaMood mood;
        GrandmaActivities activities;
        GrandmaAnimation anim;
        GrandmaSpeech speech;
        GrandmaProps props;
        readonly List<FireplaceFire> fires = new List<FireplaceFire>();

        // Host: what the client last got, to send on change only.
        int sentState = Unsent, sentFlags = Unsent, sentPatience = Unsent, sentWarning = Unsent;
        uint sentSpot;
        int sentPhase = Unsent, sentProp = Unsent;
        readonly List<bool> sentLit = new List<bool>();
        readonly List<float> sentLitUntil = new List<float>();

        // Host: the line she is saying, for the snapshot.
        Line lastLine;
        int lastVariant = Unsent;
        string lastArg;

        // ---------------------------------------------------------------- scene

        public override void OnSceneReady()
        {
            instance = this;
            ForgetSent();
            brain = Object.FindAnyObjectByType<GrandmaBrain>();
            fires.Clear();
            fires.AddRange(Object.FindObjectsByType<FireplaceFire>(FindObjectsInactive.Include));
            if (brain == null) return;
            mover = brain.mover != null ? brain.mover : brain.GetComponent<GrandmaMover>();
            mood = brain.mood != null ? brain.mood : brain.GetComponent<GrandmaMood>();
            activities = brain.activities != null ? brain.activities : brain.GetComponent<GrandmaActivities>();
            anim = brain.anim != null ? brain.anim : brain.GetComponent<GrandmaAnimation>();
            speech = brain.speech != null ? brain.speech : brain.GetComponent<GrandmaSpeech>();
            props = brain.props != null ? brain.props : brain.GetComponent<GrandmaProps>();
            NetTransforms.Track(NetIds.IdOf(brain.gameObject), brain.transform);
        }

        public override void OnSessionEnd()
        {
            if (instance == this) instance = null;
            brain = null;
            mover = null;
            mood = null;
            activities = null;
            anim = null;
            speech = null;
            props = null;
            fires.Clear();
            ForgetSent();
        }

        void ForgetSent()
        {
            sentState = sentFlags = sentPatience = sentWarning = sentPhase = sentProp = Unsent;
            sentSpot = 0;
            sentLit.Clear();
            sentLitUntil.Clear();
            lastVariant = Unsent;
            lastArg = null;
        }

        // ---------------------------------------------------------------- host

        public override void HostTick()
        {
            if (brain == null) return;
            WriteFlags(false);
            WriteMood(false);
            WriteActivity(false);
            WriteProp(false);
            for (int i = 0; i < fires.Count; i++) WriteFire(i, false);
        }

        public override void SendSnapshot()
        {
            if (brain == null) return;
            WriteFlags(true);
            WriteMood(true);
            WriteActivity(true);
            WriteProp(true);
            for (int i = 0; i < fires.Count; i++) WriteFire(i, true);

            if (anim != null)
            {
                for (int layer = 0; layer < anim.LayerCount; layer++)
                {
                    if (!anim.CurrentState(layer, out int hash, out float t)) continue;
                    var w = NetOut.Reliable(NetSyncId.Grandma, OpAnimSnapshot);
                    if (w == null) return;
                    w.WriteByte((byte)layer);
                    w.WriteInt(hash);
                    w.WriteFloat(t);
                    NetOut.End(w);
                }
            }

            if (speech != null && speech.IsSpeaking && lastVariant != Unsent)
            {
                var w = NetOut.Reliable(NetSyncId.Grandma, OpSpeechSnapshot);
                if (w == null) return;
                w.WriteByte((byte)lastLine);
                w.WriteByte((byte)lastVariant);
                w.WriteString(lastArg);
                w.WriteFloat(speech.SpeakingUntil - Time.time);
                NetOut.End(w);
            }
        }

        int Flags()
        {
            int f = 0;
            if (brain.KeysGiven) f |= BitKeys;
            if (brain.PoliceCalled) f |= BitPolice;
            if (brain.AIEnabled) f |= BitAI;
            if (brain.SessionOver) f |= BitSessionOver;
            if (mover != null && mover.CollisionsOn) f |= BitCollisions;
            if (mover != null && mover.SeatedBodyOn) f |= BitSeated;
            return f;
        }

        void WriteFlags(bool force)
        {
            int state = (int)brain.State, flags = Flags();
            if (!force && state == sentState && flags == sentFlags) return;
            var w = NetOut.Reliable(NetSyncId.Grandma, OpFlags);
            if (w == null) return;
            w.WriteByte((byte)state);
            w.WriteByte((byte)flags);
            NetOut.End(w);
            sentState = state;
            sentFlags = flags;
        }

        void WriteMood(bool force)
        {
            if (mood == null) return;
            int patience = Mathf.RoundToInt(Mathf.Clamp(mood.Patience, 0f, 100f) * 100f);
            int warning = mood.InLastWarning ? 1 : 0;
            if (!force && patience == sentPatience && warning == sentWarning) return;
            var w = NetOut.Reliable(NetSyncId.Grandma, OpMood);
            if (w == null) return;
            w.WriteUShort((ushort)patience);
            w.WriteBool(warning != 0);
            NetOut.End(w);
            sentPatience = patience;
            sentWarning = warning;
        }

        void WriteActivity(bool force)
        {
            if (activities == null) return;
            uint spot = activities.Current != null ? NetIds.IdOf(activities.Current.gameObject) : 0;
            int phase = (int)activities.CurrentPhase;
            if (!force && spot == sentSpot && phase == sentPhase) return;
            var w = NetOut.Reliable(NetSyncId.Grandma, OpActivity);
            if (w == null) return;
            w.WriteUInt(spot);
            w.WriteByte((byte)phase);
            NetOut.End(w);
            sentSpot = spot;
            sentPhase = phase;
        }

        void WriteProp(bool force)
        {
            if (props == null) return;
            int prop = (int)props.Shown;
            if (!force && prop == sentProp) return;
            var w = NetOut.Reliable(NetSyncId.Grandma, OpProp);
            if (w == null) return;
            w.WriteByte((byte)prop);
            NetOut.End(w);
            sentProp = prop;
        }

        // A fire is sent when it lights or goes out, and when she lights it again while it burns
        // (its time left jumps).
        void WriteFire(int i, bool force)
        {
            while (sentLit.Count <= i) { sentLit.Add(false); sentLitUntil.Add(-1f); }
            var fire = fires[i];
            if (fire == null) return;
            bool lit = fire.IsLit;
            float left = fire.SecondsLeft;
            float until = lit ? Time.time + left : -1f;
            if (!force && lit == sentLit[i] && (!lit || Mathf.Abs(until - sentLitUntil[i]) < 1f)) return;
            uint id = NetIds.IdOf(fire.gameObject);
            if (id == 0) return;
            var w = NetOut.Reliable(NetSyncId.Grandma, OpFire);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteBool(lit);
            w.WriteUShort((ushort)Mathf.Clamp(Mathf.CeilToInt(left), 0, ushort.MaxValue));
            NetOut.End(w);
            sentLit[i] = lit;
            sentLitUntil[i] = until;
        }

        // ---------------------------------------------------------------- host hooks (Net.IsHost at the call site)

        // GrandmaMood, right before each GrandmaMoodChanged: the value arrives before the event.
        public static void SendMood(GrandmaMood m)
        {
            var s = instance;
            if (s == null || m == null || m != s.mood) return;
            s.WriteMood(true);
        }

        // GrandmaAnimation.CrossFade, after its early-outs: every real cross-fade.
        public static void SendAnim(int layer, int hash)
        {
            var w = NetOut.Reliable(NetSyncId.Grandma, OpAnim);
            if (w == null) return;
            w.WriteByte((byte)layer);
            w.WriteInt(hash);
            NetOut.End(w);
        }

        // GrandmaSpeech.Say, after the variant pick: the client formats it in its own language.
        public static void SendSpeech(Line line, int variant, string arg)
        {
            var s = instance;
            if (s != null)
            {
                s.lastLine = line;
                s.lastVariant = variant;
                s.lastArg = arg;
            }
            var w = NetOut.Reliable(NetSyncId.Grandma, OpSpeech);
            if (w == null) return;
            w.WriteByte((byte)line);
            w.WriteByte((byte)Mathf.Clamp(variant, 0, 255));
            w.WriteString(arg);
            NetOut.End(w);
        }

        public static void SendHush()
        {
            var w = NetOut.Reliable(NetSyncId.Grandma, OpHush);
            if (w == null) return;
            NetOut.End(w);
        }

        // ---------------------------------------------------------------- client

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient || brain == null) return;
            switch (op)
            {
                case OpFlags:
                {
                    var state = (GrandmaState)r.ReadByte();
                    int f = r.ReadByte();
                    brain.ApplyReplica(state, (f & BitKeys) != 0, (f & BitPolice) != 0, (f & BitAI) != 0, (f & BitSessionOver) != 0);
                    if (mover != null) mover.SetReplicaCollisions((f & BitCollisions) != 0, (f & BitSeated) != 0);
                    break;
                }
                case OpMood:
                {
                    float patience = r.ReadUShort() / 100f;
                    bool warning = r.ReadBool();
                    if (mood != null) mood.ApplyReplica(patience, warning);
                    break;
                }
                case OpActivity:
                {
                    uint id = r.ReadUInt();
                    var phase = (GrandmaActivities.Phase)r.ReadByte();
                    var spot = id != 0 ? NetIds.Resolve<ActivitySpot>(new NetRef(id, NetKind.Spot)) : null;
                    if (activities != null) activities.ApplyReplica(spot, spot != null ? phase : GrandmaActivities.Phase.None);
                    break;
                }
                case OpProp:
                {
                    var prop = (GrandmaProp)r.ReadByte();
                    if (props == null) break;
                    if (prop == GrandmaProp.None || prop == GrandmaProp.Auto) props.HideAll();
                    else props.Show(prop);
                    break;
                }
                case OpAnim:
                {
                    int layer = r.ReadByte();
                    int hash = r.ReadInt();
                    if (anim != null) anim.ApplyCrossFade(hash, layer);
                    break;
                }
                case OpAnimSnapshot:
                {
                    int layer = r.ReadByte();
                    int hash = r.ReadInt();
                    float t = r.ReadFloat();
                    if (anim != null) anim.Play(hash, layer, t);
                    break;
                }
                case OpSpeech:
                {
                    var line = (Line)r.ReadByte();
                    int variant = r.ReadByte();
                    string arg = r.ReadString();
                    if (speech != null && (int)line < (int)Line.Count) speech.SayVariant(line, variant, arg);
                    break;
                }
                case OpSpeechSnapshot:
                {
                    var line = (Line)r.ReadByte();
                    int variant = r.ReadByte();
                    string arg = r.ReadString();
                    float left = r.ReadFloat();
                    if (speech != null && (int)line < (int)Line.Count && left > 0f) speech.SayVariant(line, variant, arg, left);
                    break;
                }
                case OpHush:
                    if (speech != null) speech.Hush();
                    break;
                case OpFire:
                {
                    uint id = r.ReadUInt();
                    bool lit = r.ReadBool();
                    float left = r.ReadUShort();
                    var fire = NetIds.Resolve<FireplaceFire>(new NetRef(id, NetKind.Fire));
                    if (fire == null) break;
                    if (lit) fire.Ignite(left);
                    else fire.Extinguish();
                    break;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            instance = null;
            NetSync.Register(new GrandmaSync());
        }
    }
}
