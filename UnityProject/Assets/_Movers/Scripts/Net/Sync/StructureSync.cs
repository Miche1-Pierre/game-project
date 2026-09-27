using UnityEngine;

namespace Movers
{
    // Structure (sys 4, NETCODE_SLICE 4.3 and 11.4): wall fractures, chunk detaches, chunk looks,
    // module states and roof falls, host to client, reliable. The host's DestructibleModule and
    // RoofSection call the static writers at their transitions (IsHost gated at the call site);
    // the client applies through the replica methods (NetFracture, NetDetach, ...), which run no
    // graph, raise nothing and play no sound. A chunk is (module id, DestructibleChunk.Index),
    // valid once NetFracture ran for that module (5.4).
    public sealed class StructureSync : NetSync
    {
        const byte OpModuleFractured = 1, OpChunkDetached = 2, OpChunkLook = 3, OpModuleState = 4,
                   OpRoofFell = 5, OpModuleSnapshot = 6;
        const byte FlagFell = 1, FlagVanished = 2;

        public override NetSyncId Id => NetSyncId.Structure;

        // ---- host writers ----

        public static void ModuleFractured(DestructibleModule m, float healthLeft01)
        {
            uint id = IdOf(m);
            if (id == 0) return;
            var w = NetOut.Reliable(NetSyncId.Structure, OpModuleFractured);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteUnit(healthLeft01);
            NetOut.End(w);
        }

        public static void ChunkDetached(DestructibleModule m, int chunk, bool fell, bool vanished, Vector3 v, Vector3 angular)
        {
            uint id = IdOf(m);
            if (id == 0 || chunk < 0 || chunk > 255) return;
            var w = NetOut.Reliable(NetSyncId.Structure, OpChunkDetached);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteByte((byte)chunk);
            w.WriteByte((byte)((fell ? FlagFell : 0) | (vanished ? FlagVanished : 0)));
            w.WriteVector3Half(v);
            w.WriteVector3Half(angular);
            NetOut.End(w);
        }

        public static void ChunkLook(DestructibleModule m, int chunk, DestructionState s)
        {
            uint id = IdOf(m);
            if (id == 0 || chunk < 0 || chunk > 255) return;
            var w = NetOut.Reliable(NetSyncId.Structure, OpChunkLook);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteByte((byte)chunk);
            w.WriteByte((byte)s);
            NetOut.End(w);
        }

        public static void ModuleState(DestructibleModule m, DestructionState s)
        {
            uint id = IdOf(m);
            if (id == 0) return;
            var w = NetOut.Reliable(NetSyncId.Structure, OpModuleState);
            if (w == null) return;
            w.WriteUInt(id);
            w.WriteByte((byte)s);
            NetOut.End(w);
        }

        // The roof goes on the transform stream on both machines from here on (6).
        public static void RoofFell(RoofSection roof)
        {
            uint id = IdOf(roof);
            if (id == 0) return;
            NetTransforms.Track(id, roof.transform);
            WriteRoofFell(id);
        }

        static void WriteRoofFell(uint id)
        {
            var w = NetOut.Reliable(NetSyncId.Structure, OpRoofFell);
            if (w == null) return;
            w.WriteUInt(id);
            NetOut.End(w);
        }

        // Fractured walls as one record each; a wall that is only marked (intact mesh, Damaged)
        // as its state; every fallen roof. The transform snapshot that follows places the roofs.
        public override void SendSnapshot()
        {
            var modules = DestructibleModule.All;
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                uint id = IdOf(m);
                if (id == 0) continue;
                if (m.IsFractured)
                {
                    m.NetSnapshot(out float left, out ushort attachedMask, out uint looks, out byte fixturesMask);
                    var w = NetOut.Reliable(NetSyncId.Structure, OpModuleSnapshot);
                    if (w == null) return;
                    w.WriteUInt(id);
                    w.WriteUnit(left);
                    w.WriteByte((byte)m.State);
                    w.WriteUShort(attachedMask);
                    w.WriteUInt(looks);
                    w.WriteByte(fixturesMask);
                    NetOut.End(w);
                }
                else if (m.State != DestructionState.Intact) ModuleState(m, m.State);
            }
            var roofs = RoofSection.All;
            for (int i = 0; i < roofs.Count; i++)
            {
                var roof = roofs[i];
                if (roof == null || !roof.HasFallen || !roof.TryGetComponent(out Rigidbody _)) continue;
                uint id = IdOf(roof);
                if (id != 0) WriteRoofFell(id);
            }
        }

        // ---- client ----

        public override void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            // Before SnapshotEnd the records are the snapshot: applied without dust or debris.
            bool silent = !Net.PeerReady;
            uint id = r.ReadUInt();
            switch (op)
            {
                case OpModuleFractured:
                {
                    float left = r.ReadUnit();
                    Module(id)?.NetFracture(left);
                    break;
                }
                case OpChunkDetached:
                {
                    int chunk = r.ReadByte();
                    byte flags = r.ReadByte();
                    Vector3 v = r.ReadVector3Half();
                    Vector3 angular = r.ReadVector3Half();
                    Module(id)?.NetDetach(chunk, (flags & FlagFell) != 0, (flags & FlagVanished) != 0, v, angular, silent);
                    break;
                }
                case OpChunkLook:
                {
                    int chunk = r.ReadByte();
                    var s = (DestructionState)r.ReadByte();
                    Module(id)?.NetChunkLook(chunk, s);
                    break;
                }
                case OpModuleState:
                    Module(id)?.NetState((DestructionState)r.ReadByte());
                    break;
                case OpRoofFell:
                {
                    var go = NetIds.Find(id);
                    if (go == null || !go.TryGetComponent(out RoofSection roof)) break;
                    roof.NetFall(silent);
                    NetTransforms.Track(id, roof.transform);
                    break;
                }
                case OpModuleSnapshot:
                {
                    float left = r.ReadUnit();
                    var s = (DestructionState)r.ReadByte();
                    ushort attachedMask = r.ReadUShort();
                    uint looks = r.ReadUInt();
                    byte fixturesMask = r.ReadByte();
                    Module(id)?.NetApplySnapshot(left, s, attachedMask, looks, fixturesMask);
                    break;
                }
            }
        }

        static uint IdOf(Component c) => c != null ? NetIds.IdOf(c.gameObject) : 0;

        static DestructibleModule Module(uint id)
        {
            var go = NetIds.Find(id);
            return go != null && go.TryGetComponent(out DestructibleModule m) ? m : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => NetSync.Register(new StructureSync());
    }
}
