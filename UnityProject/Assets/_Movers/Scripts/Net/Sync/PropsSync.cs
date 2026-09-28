using UnityEngine;

namespace Movers
{
    // Props (sys 5, NETCODE_SLICE 4.3 and 11.5): breakables, glass, the blast's picture, sounds,
    // grenades and the dust and shake of big hits (ImpactFx, DEV 2), host to client. The host's
    // Breakable, GlassPane, Explosion, ImpactAudio, GrenadeItem and ImpactFeedback call the static
    // writers at their transitions (IsHost gated at the call site);
    // the client applies through replica methods that raise nothing. Reliable records of one
    // blast keep their order (4.2): the Breakable and Glass transitions, then ExplosionFx.
    public sealed class PropsSync : NetSync
    {
        const byte OpBreakable = 1, OpGlass = 2, OpExplosionFx = 3, OpSound = 4, OpGrenade = 5, OpImpactFx = 6;
        const byte GrenadeArmed = 1, GrenadePinOut = 2;
        const float MaxFuseSeconds = 65f;
        // m. The largest dust puff an ImpactFx carries; its size byte is a share of this.
        const float ImpactFxMaxSize = 4f;

        public override NetSyncId Id => NetSyncId.Props;

        // ---- host writers ----

        // Damaged: id and state. Destroyed adds the hit and the velocity the debris inherits,
        // because the client's replica body is kinematic and has none of its own.
        public static void BreakableState(Breakable b, DestructionState state, in DamageEvent e, Vector3 inherit)
        {
            uint id = IdOf(b);
            if (id == 0) return;
            var w = NetOut.Reliable(NetSyncId.Props, OpBreakable);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteByte((byte)state);
            if (state == DestructionState.Destroyed)
            {
                w.WriteVector3(e.position);
                w.WriteVector3(e.ImpulseVector);
                w.WriteVector3Half(inherit);
                w.WriteSByte(Instigator(e.instigator));
            }
            NetOut.End(w);
        }

        // Damaged (cracked): id and state. Destroyed (broken) adds the hit.
        public static void GlassState(GlassPane pane, DestructionState state, in DamageEvent e)
        {
            uint id = IdOf(pane);
            if (id == 0) return;
            var w = NetOut.Reliable(NetSyncId.Props, OpGlass);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteByte((byte)state);
            if (state == DestructionState.Destroyed)
            {
                w.WriteVector3(e.position);
                w.WriteVector3(e.ImpulseVector);
                w.WriteSByte(Instigator(e.instigator));
            }
            NetOut.End(w);
        }

        public static void ExplosionFx(Vector3 position, float radius, float power, int instigator)
        {
            var w = NetOut.Reliable(NetSyncId.Props, OpExplosionFx);
            if (w == null) return;
            w.WriteVector3(position);
            w.WriteHalf(radius);
            w.WriteHalf(power);
            w.WriteSByte(Instigator(instigator));
            NetOut.End(w);
        }

        // Unreliable: a lost crunch is not worth a resend.
        public static void Sound(ImpactAudio.Kind kind, Vector3 position, float volume)
        {
            var w = NetOut.Unreliable(NetSyncId.Props, OpSound);
            if (w == null) return;
            w.WriteByte((byte)kind);
            w.WriteVector3(position);
            w.WriteUnit(volume);
            NetOut.End(w);
        }

        // Unreliable, like a sound: the dust and shake of a hit that removed nothing, or (size 0)
        // only the shake of a big hit whose removals reach the client as ChunkDetached
        // (ImpactFeedback, DEV 2 3.9). strength01 is ImpactFeedback.Strength01; size in metres.
        public static void ImpactFx(Vector3 position, float strength01, float size)
        {
            var w = NetOut.Unreliable(NetSyncId.Props, OpImpactFx);
            if (w == null) return;
            w.WriteVector3(position);
            w.WriteUnit(strength01);
            // A real size never rounds down to 0, which means "shake only".
            w.WriteUnit(size > 0f ? Mathf.Max(1f / 255f, size / ImpactFxMaxSize) : 0f);
            NetOut.End(w);
        }

        public static void Grenade(GrenadeItem g)
        {
            uint id = IdOf(g);
            if (id == 0) return;
            var w = NetOut.Reliable(NetSyncId.Props, OpGrenade);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteByte((byte)((g.IsArmed ? GrenadeArmed : 0) | (g.IsPinOut ? GrenadePinOut : 0)));
            float left = g.IsArmed ? Mathf.Clamp(g.FuseLeft, 0f, MaxFuseSeconds) : 0f;
            w.WriteUShort((ushort)Mathf.RoundToInt(left * 1000f));
            w.WriteSByte(Instigator(g.ArmedBy));
            NetOut.End(w);
        }

        // Every piece that is not intact (already broken ones as a state only: they are simply
        // inactive on the client), and every armed grenade.
        public override void SendSnapshot()
        {
            var none = default(DamageEvent);
            var breakables = Object.FindObjectsByType<Breakable>(FindObjectsInactive.Include);
            for (int i = 0; i < breakables.Length; i++)
            {
                var b = breakables[i];
                if (b != null && b.State != DestructionState.Intact) BreakableState(b, b.State, none, Vector3.zero);
            }
            var panes = Object.FindObjectsByType<GlassPane>(FindObjectsInactive.Include);
            for (int i = 0; i < panes.Length; i++)
            {
                var p = panes[i];
                if (p != null && p.State != DestructionState.Intact) GlassState(p, p.State, none);
            }
            var grenades = GrenadeItem.ArmedList;
            for (int i = 0; i < grenades.Count; i++)
                if (grenades[i] != null && grenades[i].IsArmed) Grenade(grenades[i]);
        }

        // ---- client ----

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            // Before SnapshotEnd the records are the snapshot: applied without debris or sound.
            bool silent = !Net.PeerReady;
            switch (op)
            {
                case OpBreakable:
                {
                    var go = NetIds.Find(r.ReadUInt());
                    var state = (DestructionState)r.ReadByte();
                    DamageEvent e = default;
                    Vector3 inherit = Vector3.zero;
                    if (state == DestructionState.Destroyed) e = ReadHit(r, out inherit, true);
                    if (go != null && go.TryGetComponent(out Breakable b)) b.NetApply(state, e, inherit, silent);
                    break;
                }
                case OpGlass:
                {
                    var go = NetIds.Find(r.ReadUInt());
                    var state = (DestructionState)r.ReadByte();
                    DamageEvent e = default;
                    if (state == DestructionState.Destroyed) e = ReadHit(r, out _, false);
                    if (go != null && go.TryGetComponent(out GlassPane p)) p.NetApply(state, e, silent);
                    break;
                }
                case OpExplosionFx:
                {
                    Vector3 at = r.ReadVector3();
                    float radius = r.ReadHalf();
                    float power = r.ReadHalf();
                    r.ReadSByte();   // instigator: the client's picture does not need it
                    if (!silent) Explosion.PlayCosmetic(at, radius, power);
                    break;
                }
                case OpSound:
                {
                    var kind = (ImpactAudio.Kind)r.ReadByte();
                    Vector3 at = r.ReadVector3();
                    float volume = r.ReadUnit();
                    if (!silent) ImpactAudio.PlayFromNet(kind, at, volume);
                    break;
                }
                case OpImpactFx:
                {
                    Vector3 at = r.ReadVector3();
                    float strength = r.ReadUnit();
                    float size = r.ReadUnit() * ImpactFxMaxSize;
                    if (!silent) ImpactFeedback.PlayFromNet(at, strength, size);
                    break;
                }
                case OpGrenade:
                {
                    var go = NetIds.Find(r.ReadUInt());
                    byte flags = r.ReadByte();
                    float fuseLeft = r.ReadUShort() / 1000f;
                    int armedBy = r.ReadSByte();
                    if (go != null && go.TryGetComponent(out GrenadeItem g))
                        g.NetApply((flags & GrenadeArmed) != 0, (flags & GrenadePinOut) != 0, fuseLeft, armedBy);
                    break;
                }
            }
        }

        static DamageEvent ReadHit(NetReader r, out Vector3 inherit, bool withInherit)
        {
            Vector3 point = r.ReadVector3();
            Vector3 impulse = r.ReadVector3();
            inherit = withInherit ? r.ReadVector3Half() : Vector3.zero;
            int instigator = r.ReadSByte();
            return new DamageEvent(point, impulse, 0f, impulse.magnitude, 0f, DamageType.Impact, instigator);
        }

        static sbyte Instigator(int instigator) => (sbyte)Mathf.Clamp(instigator, sbyte.MinValue, sbyte.MaxValue);

        static uint IdOf(Component c) => c != null ? NetIds.IdOf(c.gameObject) : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new PropsSync());
    }
}
