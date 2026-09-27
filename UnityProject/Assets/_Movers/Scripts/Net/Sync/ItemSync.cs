using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Items (sys 3), NETCODE_SLICE 4.3, 9.5 and 11.2. All h>c, reliable.
    //   1 Flags       id, holder s8, bits (dragging, inPocket, worn, loaded)   polled, on change
    //   2 Pockets     member, 4 x id, outOfPocketSlot s8                       polled, on change
    //   3 Worn        member, 6 x id (EquipSlot order)                         polled, on change
    //   4 Cigarette   id, smoking, phase unit                                  edges, 4 Hz while smoking
    //   5 Beer        id, drinking, fill unit                                  edges, 5 Hz while drinking
    //   6 SmokePuff   id, smoker, cloud centre, dir half3, lifetime, strength, mouth, wisps
    //   7 BeerSplat   at, normal half3, size half
    //   8 PocketHint  member, code, slot                                       the host's Hint for P2
    // The host polls in this order, so on the client the hands are right before a pocket or a
    // body takes the object. The client writes the same fields the view reads, through the
    // items' NetApply paths: no events, no physics.
    public sealed class ItemSync : NetSync
    {
        const byte OpFlags = 1, OpPockets = 2, OpWorn = 3, OpCigarette = 4, OpBeer = 5,
                   OpSmokePuff = 6, OpBeerSplat = 7, OpPocketHint = 8;
        public const byte HintHandsFull = 1, HintPocketEmpty = 2;
        const byte BitDragging = 1, BitInPocket = 2, BitWorn = 4, BitLoaded = 8;
        const int Members = 2;
        const int WornSlots = 6;   // EquipSlot: Head, Face, Chest, Hands, Waist, Feet
        const float RescanEvery = 1f;   // the host's spawns join the flag poll within a second

        public override NetSyncId Id => NetSyncId.Items;

        static readonly List<MovableObject> movables = new List<MovableObject>(160);
        static readonly Dictionary<MovableObject, ushort> sentFlags = new Dictionary<MovableObject, ushort>(160);
        static readonly uint[,] sentPockets = new uint[Members, PlayerPockets.SlotCount + 1];
        static readonly uint[,] sentWorn = new uint[Members, WornSlots];
        static readonly bool[] pocketsSent = new bool[Members], wornSent = new bool[Members];
        static readonly CrewEquip[] bodies = new CrewEquip[Members];
        static readonly MovableObject[] pocketScratch = new MovableObject[PlayerPockets.SlotCount];
        static float nextScan;

        // =====================================================================
        // Host hooks (items call these behind Net.IsHost)
        // =====================================================================

        public static bool SendCigarette(CigaretteItem c, bool smoking, float phase)
        {
            uint id = c != null ? NetIds.IdOf(c.gameObject) : 0;
            if (id == 0) return false;
            var w = NetOut.Reliable(NetSyncId.Items, OpCigarette);
            if (w == null) return false;
            w.WriteUInt(id);
            w.WriteBool(smoking);
            w.WriteUnit(phase);
            NetOut.End(w);
            return true;
        }

        public static bool SendBeer(BeerItem b, bool drinking, float fill)
        {
            uint id = b != null ? NetIds.IdOf(b.gameObject) : 0;
            if (id == 0) return false;
            var w = NetOut.Reliable(NetSyncId.Items, OpBeer);
            if (w == null) return false;
            w.WriteUInt(id);
            w.WriteBool(drinking);
            w.WriteUnit(fill);
            NetOut.End(w);
            return true;
        }

        public static void SendSmokePuff(CigaretteItem c, int smoker, Vector3 at, Vector3 dir, float lifetime, float strength,
                                         bool hasMouth, Vector3 mouth, int wisps)
        {
            var w = NetOut.Reliable(NetSyncId.Items, OpSmokePuff);
            if (w == null) return;
            w.WriteUInt(c != null ? NetIds.IdOf(c.gameObject) : 0);
            w.WriteSByte((sbyte)Mathf.Clamp(smoker, -1, 127));
            w.WriteVector3(at);
            w.WriteVector3Half(dir);
            w.WriteByte((byte)Mathf.Clamp(Mathf.RoundToInt(lifetime * 10f), 0, 255));    // 0.1 s steps
            w.WriteByte((byte)Mathf.Clamp(Mathf.RoundToInt(strength * 100f), 0, 255));   // 0.01 steps
            w.WriteVector3(mouth);
            w.WriteByte((byte)(hasMouth ? Mathf.Clamp(wisps, 1, 255) : 0));
            NetOut.End(w);
        }

        public static void SendBeerSplat(Vector3 at, Vector3 normal, float size)
        {
            var w = NetOut.Reliable(NetSyncId.Items, OpBeerSplat);
            if (w == null) return;
            w.WriteVector3(at);
            w.WriteVector3Half(normal);
            w.WriteHalf(size);
            NetOut.End(w);
        }

        // Only the other machine's player: the host's own bar already shows it.
        public static void SendPocketHint(int member, byte code, int slot)
        {
            if (member == Net.LocalMember) return;
            var w = NetOut.Reliable(NetSyncId.Items, OpPocketHint);
            if (w == null) return;
            w.WriteByte((byte)member);
            w.WriteByte(code);
            w.WriteByte((byte)Mathf.Clamp(slot, 0, 255));
            NetOut.End(w);
        }

        // =====================================================================
        // Host polling
        // =====================================================================

        public override void OnSceneReady()
        {
            Clear();
            Scan();
        }

        public override void HostTick()
        {
            if (Time.unscaledTime >= nextScan) Scan();
            PollFlags(false);
            for (int m = 0; m < Members; m++) PollPockets(m, false);
            for (int m = 0; m < Members; m++) PollWorn(m, false);
        }

        public override void SendSnapshot()
        {
            Scan();
            PollFlags(true);
            for (int m = 0; m < Members; m++) PollPockets(m, true);
            for (int m = 0; m < Members; m++) PollWorn(m, true);
            foreach (var c in Object.FindObjectsByType<CigaretteItem>(FindObjectsInactive.Include))
                if (c.IsSmoking) SendCigarette(c, true, c.SmokePhase);
            foreach (var b in Object.FindObjectsByType<BeerItem>(FindObjectsInactive.Include))
                if (b.IsDrinking || b.fill < 1f) SendBeer(b, b.IsDrinking, b.fill);
        }

        // Every MovableObject, pocketed and worn ones included (they are switched off).
        static void Scan()
        {
            nextScan = Time.unscaledTime + RescanEvery;
            movables.Clear();
            movables.AddRange(Object.FindObjectsByType<MovableObject>(FindObjectsInactive.Include));
            for (int m = 0; m < Members; m++)
            {
                var member = CrewRoster.Get(m);
                bodies[m] = member != null ? member.GetComponentInChildren<CrewEquip>(true) : null;
            }
        }

        static void PollFlags(bool snapshot)
        {
            for (int i = movables.Count - 1; i >= 0; i--)
            {
                var mo = movables[i];
                if (mo == null) { movables.RemoveAt(i); continue; }
                uint id = NetIds.IdOf(mo.gameObject);
                if (id == 0) continue;

                int holder = -1;
                var h = mo.holder;
                bool held = h != null && h.isActiveAndEnabled && h.Held == mo;
                if (held) holder = h.Actor;
                byte bits = 0;
                if (held && h.IsDragging) bits |= BitDragging;
                if (mo.inPocket) bits |= BitInPocket;
                if (mo.worn) bits |= BitWorn;
                if (mo.loaded) bits |= BitLoaded;
                ushort state = (ushort)(((holder + 1) & 0xFF) << 8 | bits);

                sentFlags.TryGetValue(mo, out ushort sent);   // never sent: the scene's default, 0
                if (snapshot ? state == 0 : state == sent) continue;
                var w = NetOut.Reliable(NetSyncId.Items, OpFlags);
                if (w == null) return;
                w.WriteUInt(id);
                w.WriteSByte((sbyte)holder);
                w.WriteByte(bits);
                NetOut.End(w);
                sentFlags[mo] = state;
            }
        }

        static void PollPockets(int member, bool snapshot)
        {
            var m = CrewRoster.Get(member);
            var pockets = m != null ? m.Pockets : null;
            if (pockets == null) return;
            bool changed = snapshot || !pocketsSent[member];
            uint outSlot = (uint)(pockets.OutOfPocketSlot + 1);
            for (int s = 0; s < PlayerPockets.SlotCount; s++)
            {
                var item = pockets.GetItem(s);
                if (sentPockets[member, s] != (item != null ? NetIds.IdOf(item.gameObject) : 0)) changed = true;
            }
            if (sentPockets[member, PlayerPockets.SlotCount] != outSlot) changed = true;
            if (!changed) return;

            var w = NetOut.Reliable(NetSyncId.Items, OpPockets);
            if (w == null) return;
            w.WriteByte((byte)member);
            for (int s = 0; s < PlayerPockets.SlotCount; s++)
            {
                var item = pockets.GetItem(s);
                uint id = item != null ? NetIds.IdOf(item.gameObject) : 0;
                w.WriteUInt(id);
                sentPockets[member, s] = id;
            }
            w.WriteSByte((sbyte)pockets.OutOfPocketSlot);
            NetOut.End(w);
            sentPockets[member, PlayerPockets.SlotCount] = outSlot;
            pocketsSent[member] = true;
        }

        static void PollWorn(int member, bool snapshot)
        {
            var body = bodies[member];
            if (body == null) return;
            bool changed = snapshot || !wornSent[member];
            for (int s = 0; s < WornSlots && !changed; s++)
                if (sentWorn[member, s] != WornId(body, s)) changed = true;
            if (!changed) return;

            var w = NetOut.Reliable(NetSyncId.Items, OpWorn);
            if (w == null) return;
            w.WriteByte((byte)member);
            for (int s = 0; s < WornSlots; s++)
            {
                uint id = WornId(body, s);
                w.WriteUInt(id);
                sentWorn[member, s] = id;
            }
            NetOut.End(w);
            wornSent[member] = true;
        }

        static uint WornId(CrewEquip body, int slot)
        {
            var item = body.WornIn((EquipSlot)slot);
            return item != null ? NetIds.IdOf(item.gameObject) : 0;
        }

        // =====================================================================
        // Client
        // =====================================================================

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            switch (op)
            {
                case OpFlags: ApplyFlags(r.ReadUInt(), r.ReadSByte(), r.ReadByte()); return;
                case OpPockets: ApplyPockets(r); return;
                case OpWorn: ApplyWorn(r); return;
                case OpCigarette:
                {
                    var c = Find<CigaretteItem>(r.ReadUInt());
                    bool smoking = r.ReadBool();
                    float phase = r.ReadUnit();
                    if (c != null) c.NetApply(smoking, phase);
                    return;
                }
                case OpBeer:
                {
                    var b = Find<BeerItem>(r.ReadUInt());
                    bool drinking = r.ReadBool();
                    float fill = r.ReadUnit();
                    if (b != null) b.NetApply(drinking, fill);
                    return;
                }
                case OpSmokePuff: ApplySmokePuff(r); return;
                case OpBeerSplat:
                {
                    var at = r.ReadVector3();
                    var normal = r.ReadVector3Half();
                    float size = r.ReadHalf();
                    BeerItem.SplatFx(at, normal, size);
                    return;
                }
                case OpPocketHint:
                {
                    var m = CrewRoster.Get(r.ReadByte());
                    byte code = r.ReadByte();
                    int slot = r.ReadByte();
                    if (m != null && m.Pockets != null) m.Pockets.NetHint(code, slot);
                    return;
                }
            }
        }

        static void ApplyFlags(uint id, int holder, byte bits)
        {
            var mo = Find<MovableObject>(id);
            if (mo == null) return;
            mo.loaded = (bits & BitLoaded) != 0;
            var member = holder >= 0 ? CrewRoster.Get(holder) : null;
            var want = member != null ? member.Grab : null;
            var had = mo.holder;
            if (had != null && had != want && had.Held == mo) had.ClearReplica();
            if (want != null) want.SetReplicaHeld(mo, (bits & BitDragging) != 0);
            else mo.holder = null;
        }

        static void ApplyPockets(NetReader r)
        {
            var m = CrewRoster.Get(r.ReadByte());
            for (int s = 0; s < PlayerPockets.SlotCount; s++) pocketScratch[s] = Find<MovableObject>(r.ReadUInt());
            int outSlot = r.ReadSByte();
            if (m != null && m.Pockets != null) m.Pockets.NetApply(pocketScratch, outSlot);
            System.Array.Clear(pocketScratch, 0, pocketScratch.Length);
        }

        static void ApplyWorn(NetReader r)
        {
            int member = r.ReadByte();
            var m = CrewRoster.Get(member);
            var body = m != null ? m.GetComponentInChildren<CrewEquip>(true) : null;
            for (int s = 0; s < WornSlots; s++)
            {
                var want = Find<EquipItem>(r.ReadUInt());
                if (body == null) continue;
                var slot = (EquipSlot)s;
                var now = body.WornIn(slot);
                if (now == want) continue;
                if (now != null) body.Unequip(slot);
                if (want != null) body.Equip(want);
            }
        }

        // The mouth of the local smoker is this machine's own camera, not the host's copy of it.
        static void ApplySmokePuff(NetReader r)
        {
            var c = Find<CigaretteItem>(r.ReadUInt());
            int smoker = r.ReadSByte();
            var at = r.ReadVector3();
            var dir = r.ReadVector3Half();
            float lifetime = r.ReadByte() * 0.1f;
            float strength = r.ReadByte() * 0.01f;
            var mouth = r.ReadVector3();
            int wisps = r.ReadByte();
            if (c == null) { SmokeCloud.Spawn(at, dir, lifetime, strength); return; }
            var local = smoker == Net.LocalMember ? CrewRoster.Get(smoker) : null;
            if (wisps > 0 && local != null && local.View != null) mouth = local.View.transform.TransformPoint(c.mouthOffset);
            c.PlayPuffFx(at, dir, lifetime, strength, wisps > 0, mouth, wisps);
        }

        static T Find<T>(uint id) where T : Component
        {
            var go = NetIds.Find(id);
            return go != null ? go.GetComponent<T>() : null;
        }

        // =====================================================================

        public override void OnSessionEnd() { Clear(); }

        static void Clear()
        {
            movables.Clear();
            sentFlags.Clear();
            System.Array.Clear(sentPockets, 0, sentPockets.Length);
            System.Array.Clear(sentWorn, 0, sentWorn.Length);
            System.Array.Clear(pocketsSent, 0, pocketsSent.Length);
            System.Array.Clear(wornSent, 0, wornSent.Length);
            System.Array.Clear(bodies, 0, bodies.Length);
            nextScan = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Clear();
            NetSync.Register(new ItemSync());
        }
    }
}
