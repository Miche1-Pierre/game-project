using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // Stable ids for the game scene (NETCODE_SLICE 5). One id per GameObject, computed on both
    // machines from the post-setup hierarchy by NetIdSweep, plus a kind byte on the wire because
    // one GameObject carries several subjects (a door leaf is HingedPanel + Breakable + DoorLock).
    public enum NetKind : byte
    {
        None = 0, Crew, Movable, Body, Breakable, Glass, Module, Roof, Panel, Group, Lock,
        Usable, Grandma, Spot, Fire, Truck, Seat, Cargo, Ramp, Deliver, Session, House
    }

    public readonly struct NetRef : IEquatable<NetRef>
    {
        public readonly uint id;
        public readonly NetKind kind;

        public NetRef(uint id, NetKind kind)
        {
            this.id = id;
            this.kind = kind;
        }

        public bool IsNone => kind == NetKind.None || (id == 0 && kind != NetKind.Crew);
        public static readonly NetRef None = default;

        public bool Equals(NetRef other) => id == other.id && kind == other.kind;
        public override bool Equals(object obj) => obj is NetRef r && Equals(r);
        public override int GetHashCode() => (int)id * 31 + (int)kind;
        public override string ToString() => kind + ":" + id;
    }

    public static class NetIds
    {
        public const uint SpawnBit = 0x80000000u;

        struct Entry
        {
            public uint id;
            public NetKind kind;
            public int x, y, z;   // sweep-time world position, cm
            public string path;
        }

        static readonly Dictionary<uint, GameObject> byId = new Dictionary<uint, GameObject>();
        static readonly Dictionary<GameObject, uint> byGo = new Dictionary<GameObject, uint>();
        static readonly List<Entry> entries = new List<Entry>();
        static readonly HashSet<uint> tombstones = new HashSet<uint>();
        static readonly List<uint> trackedBodies = new List<uint>();
        static bool ready;
        static ulong digest;
        static uint spawnCounter;

        public static bool Ready => ready;
        public static int Count => entries.Count;
        public static ulong Digest => digest;
        public static int TrackedBodyCount => trackedBodies.Count;

        internal static IReadOnlyList<uint> TrackedBodies => trackedBodies;
        internal static IEnumerable<uint> Tombstones => tombstones;

        public static uint IdOf(GameObject go)
        {
            if (go == null || byGo.Count == 0) return 0;
            return byGo.TryGetValue(go, out uint id) ? id : 0;
        }

        public static GameObject Find(uint id)
        {
            if (id == 0) return null;
            return byId.TryGetValue(id, out var go) && go != null ? go : null;
        }

        public static NetRef RefOf(UnityEngine.Object o)
        {
            if (o == null) return NetRef.None;
            if (o is CrewMember cm) return new NetRef((uint)cm.index, NetKind.Crew);
            if (o is GameObject go)
            {
                uint gid = IdOf(go);
                return gid == 0 ? NetRef.None : new NetRef(gid, PrimaryKind(go));
            }
            if (o is Component c)
            {
                uint cid = IdOf(c.gameObject);
                if (cid == 0) return NetRef.None;
                var kind = KindOf(c);
                return new NetRef(cid, kind != NetKind.None ? kind : PrimaryKind(c.gameObject));
            }
            return NetRef.None;
        }

        public static UnityEngine.Object Resolve(in NetRef r)
        {
            if (r.kind == NetKind.None) return null;
            if (r.kind == NetKind.Crew) return CrewRoster.Get((int)r.id);
            var go = Find(r.id);
            if (go == null) return null;
            var type = TypeOf(r.kind);
            if (type == null) return go;
            var c = go.GetComponent(type);
            return c != null ? (UnityEngine.Object)c : go;
        }

        public static T Resolve<T>(in NetRef r) where T : UnityEngine.Object
        {
            var o = Resolve(r);
            if (o == null) return null;
            if (o is T t) return t;
            GameObject go = o is Component c ? c.gameObject : o as GameObject;
            if (go == null) return null;
            if (typeof(T) == typeof(GameObject)) return go as T;
            if (!typeof(Component).IsAssignableFrom(typeof(T))) return null;
            return go.GetComponent(typeof(T)) as T;
        }

        public static uint NewSpawnId()
        {
            spawnCounter++;
            return SpawnBit | (spawnCounter & 0x7FFFFFFFu);
        }

        public static void RegisterSpawn(uint id, GameObject go)
        {
            if (id == 0 || go == null) return;
            if (byId.TryGetValue(id, out var old) && old != null && old != go)
                Debug.LogWarning("NetIds: spawn id " + id + " reused, was " + old.name + ", now " + go.name);
            if (old != null) byGo.Remove(old);
            byId[id] = go;
            byGo[go] = id;
        }

        public static void Forget(uint id)
        {
            if (!byId.TryGetValue(id, out var go)) return;
            byId.Remove(id);
            if ((object)go != null) byGo.Remove(go);
        }

        public static void DumpTo(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(path, DumpLines());
            }
            catch (Exception e) { Debug.LogWarning("NetIds: cannot write the id dump to " + path + ": " + e.Message); }
        }

        internal static List<string> DumpLines()
        {
            var lines = new List<string>(entries.Count + 1);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                lines.Add(e.id + " " + e.kind + " " + e.x + " " + e.y + " " + e.z + " " + e.path);
            }
            return lines;
        }

        // ---- the sweep (5.1, 5.2) ----

        const ulong FnvOffset = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        static ulong Fnv(ulong h, byte b) { h ^= b; return h * FnvPrime; }

        static ulong FnvInt(ulong h, int v)
        {
            h = Fnv(h, (byte)v); h = Fnv(h, (byte)(v >> 8));
            h = Fnv(h, (byte)(v >> 16)); return Fnv(h, (byte)(v >> 24));
        }


        static ulong FnvString(ulong h, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? "");
            for (int i = 0; i < bytes.Length; i++) h = Fnv(h, bytes[i]);
            return h;
        }

        static uint Fold(ulong h) => (uint)(h ^ (h >> 32)) & 0x7FFFFFFFu;

        internal static void Clear()
        {
            byId.Clear();
            byGo.Clear();
            entries.Clear();
            tombstones.Clear();
            trackedBodies.Clear();
            ready = false;
            digest = 0;
            spawnCounter = 0;
        }

        struct Frame { public Transform t; public int ordinal; }
        static readonly List<Frame> pathStack = new List<Frame>();
        static readonly List<Dictionary<string, int>> countPool = new List<Dictionary<string, int>>();

        internal static void Sweep(Scene scene)
        {
            Clear();
            ulong seed = FnvString(FnvOffset, scene.name);
            var roots = scene.GetRootGameObjects();
            var transforms = new Transform[roots.Length];
            for (int i = 0; i < roots.Length; i++) transforms[i] = roots[i].transform;
            VisitSiblings(transforms, roots.Length, seed, 0, null);

            entries.Sort((a, b) => a.id.CompareTo(b.id));
            ulong h = FnvOffset;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                NetSync.HashInto(ref h, e.id);
                NetSync.HashInto(ref h, (ulong)e.kind);
                NetSync.HashInto(ref h, (ulong)(long)e.x);
                NetSync.HashInto(ref h, (ulong)(long)e.y);
                NetSync.HashInto(ref h, (ulong)(long)e.z);
            }
            NetSync.HashInto(ref h, (ulong)entries.Count);
            digest = h;
            ready = true;
        }

        // Siblings are either the scene roots (parent == null) or a transform's children.
        static void VisitSiblings(Transform[] roots, int count, ulong parentHash, int depth, Transform parent)
        {
            if (countPool.Count <= depth) countPool.Add(new Dictionary<string, int>());
            var counts = countPool[depth];
            counts.Clear();
            // Ordinals are counted before recursing, since deeper levels reuse deeper dictionaries.
            var ordinals = new int[count];
            var kids = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var t = parent == null ? roots[i] : parent.GetChild(i);
                kids[i] = t;
                if (t.gameObject.hideFlags != HideFlags.None) { ordinals[i] = -1; continue; }
                string n = t.name;
                counts.TryGetValue(n, out int seen);
                ordinals[i] = seen;
                counts[n] = seen + 1;
            }
            for (int i = 0; i < count; i++)
            {
                if (ordinals[i] < 0) continue;
                Visit(kids[i], parentHash, ordinals[i], depth);
            }
        }

        static void Visit(Transform t, ulong parentHash, int ordinal, int depth)
        {
            var go = t.gameObject;
            ulong h = FnvInt(FnvString(parentHash, t.name), ordinal);   // h(child) = FNV(h(parent), name, ordinal)
            pathStack.Add(new Frame { t = t, ordinal = ordinal });
            try
            {
                if (go.TryGetComponent(out CrewMember _))
                {
                    // Players are never path ids; only their starting pocket items are (5.1).
                    var pockets = FindPocketsChild(t, out int pocketsOrdinal);
                    if (pockets != null) Visit(pockets, h, pocketsOrdinal, depth + 1);
                    return;
                }

                var kind = PrimaryKind(go);
                if (kind != NetKind.None) Register(go, h, kind);

                if (t.childCount > 0) VisitSiblings(null, t.childCount, h, depth + 1, t);
            }
            finally { pathStack.RemoveAt(pathStack.Count - 1); }
        }

        static Transform FindPocketsChild(Transform crew, out int ordinal)
        {
            ordinal = 0;
            for (int i = 0; i < crew.childCount; i++)
            {
                var c = crew.GetChild(i);
                if (c.gameObject.hideFlags != HideFlags.None || c.name != "Pockets") continue;
                return c;   // the first one: ordinal 0
            }
            return null;
        }

        static void Register(GameObject go, ulong h, NetKind kind)
        {
            uint id = Fold(h);
            int salt = 0;
            while (id == 0 || byId.ContainsKey(id))
            {
                if (id != 0)
                {
                    var other = byId[id];
                    Debug.LogError("NetIds: id clash " + id + " between " + PathOf(other) + " and " + CurrentPath() + ", rehashing the later one");
                }
                h = FnvInt(h, ++salt);
                id = Fold(h);
            }
            byId[id] = go;
            byGo[go] = id;
            var p = go.transform.position;
            entries.Add(new Entry
            {
                id = id, kind = kind, path = CurrentPath(),
                x = Mathf.RoundToInt(p.x * 100f), y = Mathf.RoundToInt(p.y * 100f), z = Mathf.RoundToInt(p.z * 100f),
            });
            if (IsTrackedBody(go)) trackedBodies.Add(id);
        }

        static string CurrentPath()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pathStack.Count; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(pathStack[i].t.name);
                if (pathStack[i].ordinal > 0) sb.Append('#').Append(pathStack[i].ordinal);
            }
            return sb.ToString();
        }

        static string PathOf(GameObject go)
        {
            if (go == null) return "(destroyed)";
            var sb = new StringBuilder(go.name);
            for (var t = go.transform.parent; t != null; t = t.parent) sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        // ---- kinds ----

        static readonly Type[] kindTypes = BuildKindTypes();

        static Type[] BuildKindTypes()
        {
            var t = new Type[(int)NetKind.House + 1];
            t[(int)NetKind.Crew] = typeof(CrewMember);
            t[(int)NetKind.Movable] = typeof(MovableObject);
            t[(int)NetKind.Body] = typeof(Rigidbody);
            t[(int)NetKind.Breakable] = typeof(Breakable);
            t[(int)NetKind.Glass] = typeof(GlassPane);
            t[(int)NetKind.Module] = typeof(DestructibleModule);
            t[(int)NetKind.Roof] = typeof(RoofSection);
            t[(int)NetKind.Panel] = typeof(HingedPanel);
            t[(int)NetKind.Group] = typeof(HingedGroup);
            t[(int)NetKind.Lock] = typeof(DoorLock);
            t[(int)NetKind.Usable] = typeof(HeldUsable);
            t[(int)NetKind.Grandma] = typeof(GrandmaBrain);
            t[(int)NetKind.Spot] = typeof(ActivitySpot);
            t[(int)NetKind.Fire] = typeof(FireplaceFire);
            t[(int)NetKind.Truck] = typeof(TruckVehicle);
            t[(int)NetKind.Seat] = typeof(VehicleSeat);
            t[(int)NetKind.Cargo] = typeof(TruckCargo);
            t[(int)NetKind.Ramp] = typeof(TruckRamp);
            t[(int)NetKind.Deliver] = typeof(DeliverPoint);
            t[(int)NetKind.Session] = typeof(GameSession);
            t[(int)NetKind.House] = typeof(HouseDestruction);
            return t;
        }

        static Type TypeOf(NetKind kind)
        {
            int k = (int)kind;
            return k > 0 && k < kindTypes.Length ? kindTypes[k] : null;
        }

        static NetKind KindOf(Component c)
        {
            if (c is Rigidbody) return NetKind.Body;
            for (int k = (int)NetKind.Movable; k < kindTypes.Length; k++)
            {
                if (k == (int)NetKind.Body) continue;
                if (kindTypes[k].IsInstanceOfType(c)) return (NetKind)k;
            }
            return NetKind.None;
        }

        // The first whitelisted component, in NetKind order (5.1); Body after Movable.
        static NetKind PrimaryKind(GameObject go)
        {
            if (go.TryGetComponent(out MovableObject _)) return NetKind.Movable;
            if (IsBody(go)) return NetKind.Body;
            for (int k = (int)NetKind.Breakable; k < kindTypes.Length; k++)
                if (go.TryGetComponent(kindTypes[k], out _)) return (NetKind)k;
            return NetKind.None;
        }

        // A Rigidbody that is non-kinematic at sweep time, has no MovableObject, and is not owned
        // by HingedPanel (its pivot), TruckRamp or RoofSection. In Map01: the truck and the car.
        static bool IsBody(GameObject go)
        {
            if (!go.TryGetComponent(out Rigidbody rb) || rb.isKinematic) return false;
            if (go.TryGetComponent(out MovableObject _) || go.TryGetComponent(out TruckRamp _) ||
                go.TryGetComponent(out RoofSection _) || go.TryGetComponent(out HingedPanel _)) return false;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).TryGetComponent(out HingedPanel _)) return false;   // a "_Hinge" pivot
            return true;
        }

        // Every MovableObject root and every Body is on the transform stream (6).
        static bool IsTrackedBody(GameObject go)
        {
            if (go.TryGetComponent(out MovableObject _))
            {
                var parent = go.transform.parent;
                return parent == null || parent.GetComponentInParent<MovableObject>(true) == null;
            }
            return IsBody(go);
        }

        // Host, at Tick: registered GameObjects that were destroyed (5.4). Scene ids become
        // tombstones for the next snapshot.
        static readonly List<uint> goneScratch = new List<uint>();
        internal static List<uint> CollectDestroyed()
        {
            goneScratch.Clear();
            foreach (var kv in byId)
                if (kv.Value == null) goneScratch.Add(kv.Key);
            for (int i = 0; i < goneScratch.Count; i++)
            {
                uint id = goneScratch[i];
                Forget(id);
                if ((id & SpawnBit) == 0) tombstones.Add(id);
            }
            return goneScratch;
        }

        // Client: a tracked body only follows the stream (6). Order matters: speculative first,
        // or Unity warns about continuous detection on a kinematic body.
        internal static void MakeReplica(GameObject go)
        {
            if (go == null || !go.TryGetComponent(out Rigidbody rb)) return;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            pathStack.Clear();
            countPool.Clear();
        }
    }

    // Runs the sweep in the first frame of the game scene, after every structural setup (5.2).
    // NetSession adds it at sceneLoaded, online only; offline it never exists.
    [DefaultExecutionOrder(10000)]
    internal sealed class NetIdSweep : MonoBehaviour
    {
        void Start()
        {
            if (!Net.IsOnline) { Destroy(this); return; }
            NetIds.Sweep(gameObject.scene);

            var bodies = NetIds.TrackedBodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var go = NetIds.Find(bodies[i]);
                if (go == null) continue;
                if (Net.IsClient) NetIds.MakeReplica(go);
                NetTransforms.Track(bodies[i], go.transform);
            }
            NetLog.Write("sweep: ids " + NetIds.Count + " digest " + NetIds.Digest.ToString("X16") +
                         " tracked bodies " + NetIds.TrackedBodyCount + " epoch " + Net.Epoch);

            for (int s = 0; s < (int)NetSyncId.Count; s++)
            {
                var sync = NetSync.Get((NetSyncId)s);
                if (sync == null) continue;
                try { sync.OnSceneReady(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            NetSession.OnSweepDone();
        }
    }
}
