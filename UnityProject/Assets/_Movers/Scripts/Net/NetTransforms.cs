using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The transform stream (NETCODE_SLICE 6). The host sends the poses of tracked bodies at
    // 20 Hz, moved bodies only, and one reliable Settle when a body stops. The client renders
    // them 0.1 s in the past, interpolated, on kinematic replicas (WorldApply). An item carried
    // by the client's own player travels in its camera space instead (AnchoredApply), so it
    // never lags the view.
    public static class NetTransforms
    {
        public const float SendHz = 20f;
        public const float InterpolationDelay = 0.1f;

        const byte OpBatch = 1, OpSettle = 2;
        const byte FlagTeleport = 1, FlagAnchored = 2, FlagAsleep = 4;
        const int MaxEntriesPerRecord = 45;
        const float MoveEpsilon = 0.005f;       // m
        const float TurnEpsilon = 0.5f;         // degrees
        const float AwakeResend = 1f;           // s, while the rigidbody is awake
        const float SettleAfter = 0.25f;        // s without change
        const float TeleportJump = 3f;          // m between two samples
        const float AnchoredDelay = 0.05f;
        const float MaxExtrapolation = 0.1f;
        const float LeaveBlend = 0.15f;
        const float OwnItemRestoreAfter = 0.5f;

        struct Sample { public float time; public Vector3 pos; public Quaternion rot; public bool teleport, settle; }
        struct AnchoredSample { public float time; public int member; public Vector3 p; public Quaternion r; }
        enum Mode : byte { World, Anchored, Hidden }

        sealed class Body
        {
            public uint id;
            public Transform t;
            public Rigidbody rb;
            public MovableObject mo;

            // host
            public Vector3 sentPos;
            public Quaternion sentRot = Quaternion.identity;
            public float sentAt = -99f, lastChange = -99f;
            public bool hasSent, moving, snap, wasAnchored;

            // client
            public readonly List<Sample> buffer = new List<Sample>(8);
            public readonly List<AnchoredSample> anchored = new List<AnchoredSample>(4);
            public float newestApplied = float.MinValue;
            public float anchoredUntil = float.MinValue;   // host time the world stream takes over from the anchored pose
            public Mode mode;
            public float blendStart = -99f;
            public Vector3 blendPos;
            public Quaternion blendRot;
            public Collider[] colliders;
            public bool ignoring;
            public float releasedAt = -1f;
        }

        struct Pending { public Body b; public byte flags; public Vector3 pos; public Quaternion rot; public int member; }

        static readonly Dictionary<uint, Body> bodies = new Dictionary<uint, Body>();
        static readonly List<Body> list = new List<Body>();
        static readonly List<Pending> pending = new List<Pending>(64);
        static float nextSend;

        public static void Track(uint id, Transform t)
        {
            if (id == 0 || t == null) return;
            if (!bodies.TryGetValue(id, out var b))
            {
                b = new Body { id = id };
                bodies[id] = b;
                list.Add(b);
            }
            b.t = t;
            b.rb = t.GetComponent<Rigidbody>();
            b.mo = t.GetComponent<MovableObject>();
            b.sentPos = t.position;
            b.sentRot = t.rotation;
        }

        public static void Untrack(uint id)
        {
            if (!bodies.TryGetValue(id, out var b)) return;
            if (b.ignoring) SetOwnIgnore(b, null, false);
            bodies.Remove(id);
            list.Remove(b);
        }

        public static void Snap(uint id)
        {
            if (bodies.TryGetValue(id, out var b)) b.snap = true;
        }

        public static void Snap(GameObject go)
        {
            if (!Net.IsHost) return;
            uint id = NetIds.IdOf(go);
            if (id != 0) Snap(id);
        }

        internal static void Clear()
        {
            for (int i = 0; i < list.Count; i++) if (list[i].ignoring) SetOwnIgnore(list[i], null, false);
            bodies.Clear();
            list.Clear();
            pending.Clear();
            nextSend = 0f;
        }

        // ---- host ----

        static void PoseOf(Body b, out Vector3 pos, out Quaternion rot)
        {
            if (b.rb != null) { pos = b.rb.position; rot = b.rb.rotation; }
            else { pos = b.t.position; rot = b.t.rotation; }
        }

        static bool AnchoredTo(Body b, out CrewMember member)
        {
            member = null;
            if (b.mo == null || b.mo.holder == null || b.mo.holder.IsDragging) return false;
            member = b.mo.holder.GetComponent<CrewMember>();
            return member != null && !Net.Drives(member) && member.View != null;
        }

        internal static void HostTick()
        {
            float now = NetOut.Now;
            if (now < nextSend) return;
            nextSend = Mathf.Max(nextSend + 1f / SendHz, now - 1f / SendHz);
            pending.Clear();

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var b = list[i];
                if (b.t == null) { bodies.Remove(b.id); list.RemoveAt(i); continue; }
                if (!b.t.gameObject.activeInHierarchy) continue;
                if (b.mo != null && (b.mo.inPocket || b.mo.worn)) { b.snap = true; b.moving = false; continue; }

                PoseOf(b, out var pos, out var rot);
                if (AnchoredTo(b, out var member))
                {
                    var cam = member.View.transform;
                    pending.Add(new Pending
                    {
                        b = b, flags = FlagAnchored, member = member.index,
                        pos = cam.InverseTransformPoint(pos),
                        rot = Quaternion.Inverse(member.transform.rotation) * rot,
                    });
                    b.sentPos = pos; b.sentRot = rot; b.sentAt = now; b.lastChange = now;
                    b.hasSent = b.moving = b.wasAnchored = true;
                    continue;
                }

                bool changed = (pos - b.sentPos).sqrMagnitude > MoveEpsilon * MoveEpsilon ||
                               Quaternion.Angle(rot, b.sentRot) > TurnEpsilon;
                bool asleep = b.rb != null && b.rb.IsSleeping();
                bool awake = b.rb != null && !b.rb.isKinematic && !asleep;
                bool teleport = b.snap || (b.hasSent && (pos - b.sentPos).sqrMagnitude > TeleportJump * TeleportJump);

                if (changed || teleport || b.wasAnchored || (awake && now - b.sentAt >= AwakeResend))
                {
                    byte flags = 0;
                    if (teleport) flags |= FlagTeleport;
                    if (asleep) flags |= FlagAsleep;
                    pending.Add(new Pending { b = b, flags = flags, pos = pos, rot = rot });
                    if (changed || teleport) b.lastChange = now;
                    b.sentPos = pos; b.sentRot = rot; b.sentAt = now;
                    b.hasSent = b.moving = true;
                    b.snap = b.wasAnchored = false;
                }
                else if (b.moving && now - b.lastChange >= SettleAfter)
                {
                    WriteSettle(b, pos, rot, now, asleep ? FlagAsleep : (byte)0);
                    b.moving = false;
                }
            }
            WritePending();
        }

        static void WritePending()
        {
            int i = 0;
            while (i < pending.Count)
            {
                int n = Mathf.Min(MaxEntriesPerRecord, pending.Count - i);
                var w = NetOut.Unreliable(NetSyncId.Transforms, OpBatch);
                if (w == null) break;
                w.WriteByte((byte)n);
                for (int k = 0; k < n; k++)
                {
                    var p = pending[i + k];
                    w.WriteUInt(p.b.id);
                    w.WriteByte(p.flags);
                    if ((p.flags & FlagAnchored) != 0)
                    {
                        w.WriteByte((byte)p.member);
                        w.WriteVector3Half(p.pos);
                        w.WriteRotation(p.rot);
                    }
                    else
                    {
                        w.WriteVector3(p.pos);
                        w.WriteRotation(p.rot);
                    }
                }
                NetOut.End(w);
                i += n;
            }
            pending.Clear();
        }

        static void WriteSettle(Body b, Vector3 pos, Quaternion rot, float sampleTime, byte flags)
        {
            var w = NetOut.Reliable(NetSyncId.Transforms, OpSettle);
            if (w == null) return;
            w.WriteFloat(sampleTime);
            w.WriteUInt(b.id);
            w.WriteByte(flags);
            w.WriteVector3(pos);
            w.WriteRotation(rot);
            NetOut.End(w);
        }

        // Every tracked body as a Settle (7). Pocketed, worn and inactive ones are skipped: their
        // state travels with the Items records.
        internal static void SendSnapshot()
        {
            float now = NetOut.Now;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.t == null || !b.t.gameObject.activeInHierarchy) continue;
                if (b.mo != null && (b.mo.inPocket || b.mo.worn)) continue;
                PoseOf(b, out var pos, out var rot);
                WriteSettle(b, pos, rot, now, FlagTeleport);
                b.sentPos = pos; b.sentRot = rot; b.sentAt = now; b.lastChange = now;
                b.hasSent = true;
                b.moving = b.snap = b.wasAnchored = false;
            }
        }

        // ---- client ----

        internal static void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            if (op == OpBatch)
            {
                float time = NetOut.PeerTime;
                int n = r.ReadByte();
                for (int k = 0; k < n; k++)
                {
                    uint id = r.ReadUInt();
                    byte flags = r.ReadByte();
                    bodies.TryGetValue(id, out var b);
                    if ((flags & FlagAnchored) != 0)
                    {
                        int member = r.ReadByte();
                        var p = r.ReadVector3Half();
                        var q = r.ReadRotation();
                        if (b != null) OnAnchored(b, time, member, p, q);
                    }
                    else
                    {
                        var pos = r.ReadVector3();
                        var rot = r.ReadRotation();
                        if (b != null) OnWorld(b, time, pos, rot, (flags & FlagTeleport) != 0, false);
                    }
                }
            }
            else if (op == OpSettle)
            {
                float time = r.ReadFloat();
                uint id = r.ReadUInt();
                byte flags = r.ReadByte();
                var pos = r.ReadVector3();
                var rot = r.ReadRotation();
                if (!bodies.TryGetValue(id, out var b)) return;
                if (b.buffer.Count > 0 && time < b.buffer[b.buffer.Count - 1].time) return;   // older than the newest sample
                OnWorld(b, time, pos, rot, (flags & FlagTeleport) != 0, true);
            }
        }

        static void OnWorld(Body b, float time, Vector3 pos, Quaternion rot, bool teleport, bool settle)
        {
            if (time <= b.newestApplied) return;   // a late sample after a newer one was shown
            if (b.mode != Mode.World)
            {
                // Mode change: clear, and the first new sample is a teleport. The body holds its
                // current pose until the render time reaches that sample, then jumps (6).
                if (b.mode == Mode.Anchored) b.anchoredUntil = time;
                b.mode = Mode.World;
                b.buffer.Clear();
                teleport = true;
            }
            var s = new Sample { time = time, pos = pos, rot = rot, teleport = teleport, settle = settle };
            int at = b.buffer.Count;
            while (at > 0 && b.buffer[at - 1].time > time) at--;
            b.buffer.Insert(at, s);
            if (b.buffer.Count > 32) b.buffer.RemoveAt(0);
        }

        static void OnAnchored(Body b, float time, int member, Vector3 p, Quaternion q)
        {
            if (b.mode != Mode.Anchored)
            {
                b.mode = Mode.Anchored;
                b.buffer.Clear();
                b.anchored.Clear();
                b.anchoredUntil = float.MinValue;
            }
            if (b.anchored.Count > 0 && time <= b.anchored[b.anchored.Count - 1].time) return;
            b.anchored.Add(new AnchoredSample { time = time, member = member, p = p, r = q });
            if (b.anchored.Count > 8) b.anchored.RemoveAt(0);
        }

        // WorldApply, NetOrder.WorldApply: before CrewInput, every ray and HeldPose.
        internal static void ApplyWorld()
        {
            if (list.Count == 0) return;
            float renderTime = NetOut.PeerNow - InterpolationDelay;
            bool moved = false;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.t == null) continue;
                if (b.mo != null && (b.mo.inPocket || b.mo.worn))
                {
                    // Skip and clear: the pocket and wear apply paths place it (6).
                    if (b.mode != Mode.Hidden) { b.mode = Mode.Hidden; b.buffer.Clear(); b.anchored.Clear(); }
                    continue;
                }
                if (b.mode == Mode.Anchored) continue;
                if (b.anchoredUntil > renderTime) continue;   // AnchoredApply holds it until the world stream starts
                if (b.anchoredUntil > float.MinValue)
                {
                    b.anchoredUntil = float.MinValue;
                    b.anchored.Clear();
                    b.blendStart = Time.unscaledTime;
                    b.blendPos = b.t.position;
                    b.blendRot = b.t.rotation;
                }
                if (b.buffer.Count == 0 || renderTime < b.buffer[0].time) continue;   // hold

                Vector3 pos; Quaternion rot;
                int last = b.buffer.Count - 1;
                int k = 0;
                while (k < last && b.buffer[k + 1].time <= renderTime) k++;
                var a = b.buffer[k];
                if (k < last)
                {
                    var c = b.buffer[k + 1];
                    if (c.teleport) { pos = a.pos; rot = a.rot; }
                    else
                    {
                        float t = Mathf.InverseLerp(a.time, c.time, renderTime);
                        pos = Vector3.Lerp(a.pos, c.pos, t);
                        rot = Quaternion.Slerp(a.rot, c.rot, t);
                    }
                }
                else
                {
                    pos = a.pos; rot = a.rot;
                    // Past the newest sample: at most 0.1 s of extrapolation, then hold.
                    if (k > 0 && !a.settle && !a.teleport)
                    {
                        var prev = b.buffer[k - 1];
                        float span = a.time - prev.time;
                        if (span > 1e-4f && span < 0.5f)
                        {
                            float ahead = Mathf.Min(renderTime - a.time, MaxExtrapolation);
                            pos = a.pos + (a.pos - prev.pos) * (ahead / span);
                        }
                    }
                }
                b.newestApplied = a.time;
                if (k > 0) b.buffer.RemoveRange(0, k);

                if (b.blendStart > 0f)
                {
                    float t = (Time.unscaledTime - b.blendStart) / LeaveBlend;
                    if (t >= 1f) b.blendStart = -99f;
                    else { pos = Vector3.Lerp(b.blendPos, pos, t); rot = Quaternion.Slerp(b.blendRot, rot, t); }
                }
                b.t.SetPositionAndRotation(pos, rot);
                moved = true;
            }
            if (moved) Physics.SyncTransforms();
        }

        // AnchoredApply, NetOrder.AnchoredApply: after PlayerController moved this frame's camera,
        // before HeldPose. The offset is interpolated, the camera is the current one.
        internal static void ApplyAnchored()
        {
            if (list.Count == 0) return;
            float anchoredTime = NetOut.PeerNow - AnchoredDelay;
            float worldTime = NetOut.PeerNow - InterpolationDelay;
            bool moved = false;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.t == null || b.anchored.Count == 0) continue;
                if (b.mode != Mode.Anchored && !(b.anchoredUntil > worldTime)) continue;
                var s = b.anchored[b.anchored.Count - 1];
                Vector3 p = s.p; Quaternion q = s.r;
                for (int k = b.anchored.Count - 1; k > 0; k--)
                {
                    var hi = b.anchored[k];
                    var lo = b.anchored[k - 1];
                    if (lo.time > anchoredTime) { p = lo.p; q = lo.r; continue; }
                    if (hi.time <= anchoredTime) break;
                    float t = Mathf.InverseLerp(lo.time, hi.time, anchoredTime);
                    p = Vector3.Lerp(lo.p, hi.p, t);
                    q = Quaternion.Slerp(lo.r, hi.r, t);
                    break;
                }
                var member = CrewRoster.Get(s.member);
                if (member == null || member.View == null) continue;
                b.t.SetPositionAndRotation(member.View.transform.TransformPoint(p), member.transform.rotation * q);
                moved = true;
            }
            if (moved) Physics.SyncTransforms();
            UpdateOwnItemCollisions();
        }

        // The item the local player carries or drags ignores the local capsule, re-applied every
        // frame (pairs are lost when a collider toggles). Restored once the item no longer
        // overlaps the capsule, or 0.5 s after the release (6).
        static void UpdateOwnItemCollisions()
        {
            var local = CrewRoster.Get(Net.LocalMember);
            CharacterController cc = local != null ? local.GetComponent<CharacterController>() : null;
            float now = Time.unscaledTime;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.mo == null || b.t == null) continue;
                bool wanted = cc != null && b.mo.holder != null && b.mo.holder.gameObject == local.gameObject;
                if (wanted)
                {
                    SetOwnIgnore(b, cc, true);
                    b.releasedAt = -1f;
                    continue;
                }
                if (!b.ignoring) continue;
                if (b.releasedAt < 0f) b.releasedAt = now;
                if (cc == null || now - b.releasedAt >= OwnItemRestoreAfter || !Overlaps(b, cc))
                    SetOwnIgnore(b, cc, false);
            }
        }

        static CharacterController ignoredCapsule;

        static void SetOwnIgnore(Body b, CharacterController cc, bool on)
        {
            if (cc == null) cc = ignoredCapsule;
            if (b.colliders == null && b.t != null) b.colliders = b.t.GetComponentsInChildren<Collider>(true);
            if (cc != null && b.colliders != null)
                for (int k = 0; k < b.colliders.Length; k++)
                    if (b.colliders[k] != null && !b.colliders[k].isTrigger) Physics.IgnoreCollision(cc, b.colliders[k], on);
            if (on) ignoredCapsule = cc;
            b.ignoring = on;
            b.releasedAt = -1f;
        }

        static bool Overlaps(Body b, CharacterController cc)
        {
            if (b.colliders == null) return false;
            var capsule = cc.bounds;
            for (int k = 0; k < b.colliders.Length; k++)
            {
                var c = b.colliders[k];
                if (c != null && c.enabled && !c.isTrigger && c.bounds.Intersects(capsule)) return true;
            }
            return false;
        }

        // The client's apply components live on the scene's net object and die with the scene.
        internal static void AddClientComponents(GameObject host)
        {
            if (!Net.IsClient || host == null) return;
            host.AddComponent<NetWorldApply>();
            host.AddComponent<NetAnchoredApply>();
        }

        sealed class TransformsSync : NetSync
        {
            public override NetSyncId Id => NetSyncId.Transforms;
            public override void HostTick() => NetTransforms.HostTick();
            public override void SendSnapshot() => NetTransforms.SendSnapshot();
            public override void Receive(byte op, NetReader r) => NetTransforms.Receive(op, r);
            public override void OnSessionEnd() => Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            bodies.Clear();
            list.Clear();
            pending.Clear();
            nextSend = 0f;
            ignoredCapsule = null;
            NetSync.Register(new TransformsSync());
        }
    }

    [DefaultExecutionOrder(NetOrder.WorldApply)]
    internal sealed class NetWorldApply : MonoBehaviour
    {
        void Update() { if (Net.IsClient) NetTransforms.ApplyWorld(); }
    }

    [DefaultExecutionOrder(NetOrder.AnchoredApply)]
    internal sealed class NetAnchoredApply : MonoBehaviour
    {
        void Update() { if (Net.IsClient) NetTransforms.ApplyAnchored(); }
    }
}
