using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Objects the host creates after the sweep (NETCODE_SLICE 5.4): the van respawns, the F9
    // grenade, thrown litter. The host announces each one; the client builds it with the same
    // factory, registers the id and makes it a kinematic replica. Despawns need no hook: the
    // host polls its registered objects at Tick.
    public enum NetSpawnKind : byte { Cigarette = 0, Beer = 1, Grenade = 2 }

    public static class NetSpawns
    {
        const byte OpSpawn = 1, OpDespawn = 2;

        static readonly Func<Vector3, Quaternion, GameObject>[] factories = new Func<Vector3, Quaternion, GameObject>[3];

        struct Live { public uint id; public GameObject go; public NetSpawnKind kind; }
        static readonly List<Live> live = new List<Live>();

        public static void RegisterFactory(NetSpawnKind kind, Func<Vector3, Quaternion, GameObject> factory)
        {
            int k = (int)kind;
            if (k >= 0 && k < factories.Length) factories[k] = factory;
        }

        // The items' own Create methods, used when a track registered nothing else.
        static GameObject DefaultFactory(NetSpawnKind kind, Vector3 at, Quaternion rot)
        {
            Component c = null;
            switch (kind)
            {
                case NetSpawnKind.Cigarette: c = CigaretteItem.Create(at); break;
                case NetSpawnKind.Beer: c = BeerItem.Create(at); break;
                case NetSpawnKind.Grenade: c = GrenadeItem.Create(at); break;
            }
            if (c == null) return null;
            c.transform.rotation = rot;
            return c.gameObject;
        }

        public static void Announce(GameObject go, NetSpawnKind kind)
        {
            if (!Net.IsHost || !NetIds.Ready || go == null) return;
            if (NetIds.IdOf(go) != 0) return;   // already announced
            uint id = NetIds.NewSpawnId();
            NetIds.RegisterSpawn(id, go);
            NetTransforms.Track(id, go.transform);
            live.Add(new Live { id = id, go = go, kind = kind });
            WriteSpawn(id, kind, go.transform);
        }

        static void WriteSpawn(uint id, NetSpawnKind kind, Transform t)
        {
            var w = NetOut.Reliable(NetSyncId.Spawns, OpSpawn);
            if (w == null) return;
            w.WriteByte((byte)kind);
            w.WriteUInt(id);
            w.WriteVector3(t.position);
            w.WriteRotation(t.rotation);
            NetOut.End(w);
        }

        static void WriteDespawn(uint id)
        {
            var w = NetOut.Reliable(NetSyncId.Spawns, OpDespawn);
            if (w == null) return;
            w.WriteUInt(id);
            NetOut.End(w);
        }

        // Host, at Tick, after the syncs and the transform stream.
        internal static void PollDespawns()
        {
            if (!NetIds.Ready) return;
            var gone = NetIds.CollectDestroyed();
            for (int i = 0; i < gone.Count; i++)
            {
                uint id = gone[i];
                NetTransforms.Untrack(id);
                for (int k = live.Count - 1; k >= 0; k--) if (live[k].id == id) live.RemoveAt(k);
                WriteDespawn(id);
            }
        }

        static void SendSnapshot()
        {
            for (int i = 0; i < live.Count; i++)
                if (live[i].go != null) WriteSpawn(live[i].id, live[i].kind, live[i].go.transform);
            foreach (uint id in NetIds.Tombstones) WriteDespawn(id);
        }

        static void Receive(byte op, NetReader r)
        {
            if (!Net.IsClient) return;
            if (op == OpSpawn)
            {
                var kind = (NetSpawnKind)r.ReadByte();
                uint id = r.ReadUInt();
                var pos = r.ReadVector3();
                var rot = r.ReadRotation();
                if (NetIds.Find(id) != null) return;
                int k = (int)kind;
                if (k < 0 || k >= factories.Length) return;
                var go = factories[k] != null ? factories[k](pos, rot) : DefaultFactory(kind, pos, rot);
                if (go == null) { Debug.LogError("NetSpawns: no factory built " + kind); return; }
                go.transform.SetPositionAndRotation(pos, rot);
                NetIds.RegisterSpawn(id, go);
                NetIds.MakeReplica(go);
                NetTransforms.Track(id, go.transform);
            }
            else if (op == OpDespawn)
            {
                uint id = r.ReadUInt();
                var go = NetIds.Find(id);
                NetTransforms.Untrack(id);
                NetIds.Forget(id);
                if (go != null) UnityEngine.Object.Destroy(go);
            }
        }

        sealed class SpawnsSync : NetSync
        {
            public override NetSyncId Id => NetSyncId.Spawns;
            public override void SendSnapshot() => NetSpawns.SendSnapshot();
            public override void Receive(byte op, NetReader r) => NetSpawns.Receive(op, r);
            public override void OnSessionEnd() => live.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            live.Clear();
            NetSync.Register(new SpawnsSync());
        }
    }
}
