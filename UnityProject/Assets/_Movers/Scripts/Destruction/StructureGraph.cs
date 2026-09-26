using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Something the structure graph can make fall: a wall module or one of its chunks, a whole
    // element (a fence, a chimney stack), a roof section.
    internal interface IStructurePart
    {
        // The graph found this node without support. Make it fall, take it out of the graph
        // (MarkRemoved), and return the mass that fell, in kg (0 if nothing did).
        float Release(int node, int part, in DamageEvent cause);

        // This node lost something it rested on but still stands. A wall module may break up
        // into its chunks here, so the part over the hole falls and the rest stays. True when
        // the node was replaced by new ones.
        bool SplitForSupport(int node);

        string Describe(int part);
    }

    // What holds what up in the house (ADR-009, A5a section 3.7).
    //
    // One node per wall module, per chunk of a broken wall, per whole element, per roof
    // section and per floor. Edges come from touching boxes, found once at setup:
    //   RestsOn  the upper one's bottom sits on the lower one's top, over a real footprint;
    //   Lateral  they touch side by side.
    // A node stands if a path leads from it to an anchor (the foundation, anything on the
    // ground) going down through RestsOn edges, with a small budget of sideways steps:
    //   - inside one wall, a chunk may hang from its neighbours, a couple of steps, unless it is
    //     Fractured (then it only passes load straight down);
    //   - between two pieces, only roofs, upper floor slabs and the end chunks of a broken-up
    //     wall may hang sideways, one step: a roof section held by the next one, a slab held by
    //     the walls around it, an end chunk keyed into the wall or corner beside it. Nothing but
    //     a slab hangs from a slab. A whole wall never hangs from the wall beside it, so a wall
    //     with nothing under it falls.
    // Floors and stairs never fall; an upper slab with nothing left around it just stops
    // passing support on (a floor may span a gap: an accepted greybox compromise).
    //
    // Nothing is checked every frame: after a break (the end of a blast, a collision), the
    // neighbours of what was removed are searched once, and whatever cannot reach an anchor goes
    // to the collapse queue, lowest first, at most 40 a physics step.
    public sealed class StructureGraph
    {
        public enum NodeKind : byte { Module, Chunk, Element, Roof, Floor, Fixed }

        enum Dir : byte { Down, Up, Side }

        [Flags]
        enum F : ushort
        {
            Anchor = 1,          // needs nothing under it
            NeverFalls = 2,      // floors and stairs: may lose support, never fall
            Hangs = 4,           // may be held sideways by another piece (roofs, upper slabs)
            Removed = 8,         // out of the world: broken off, fallen, destroyed
            Falling = 16,        // found unsupported, waiting in the collapse queue
            Replaced = 32,       // a wall module whose chunks took over
            Blocked = 64,        // a Fractured chunk: holds vertically only
            SelfSupported = 128, // unsupported as built: made an anchor so the house stands at load
        }

        public static StructureGraph Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }

        public static void Install(StructureGraph graph) { Current = graph; }
        public static void Uninstall(StructureGraph graph) { if (Current == graph) Current = null; }

        // ---- tuning (HouseDestruction copies its inspector values here) ----

        public float contactTolerance = 0.05f;   // m: "sits on" when bottom and top are this close
        public float minFootprint = 0.15f;       // m: less overlap than this is a sliver, not a seat (kit modules overlap 0.1 m at joins)
        public float chunkTolerance = 0.06f;
        public float chunkMinFootprint = 0.1f;
        public int chunkLateralHops = 2;         // inside one wall (A5a: a lintel spans one missing chunk)
        public int hangingHops = 1;              // between pieces, roofs and upper slabs only

        // ---- storage ----

        readonly List<Bounds> bounds = new List<Bounds>(512);
        readonly List<NodeKind> kinds = new List<NodeKind>(512);
        readonly List<F> flags = new List<F>(512);
        readonly List<IStructurePart> owners = new List<IStructurePart>(512);
        readonly List<int> parts = new List<int>(512);
        readonly List<int> groups = new List<int>(512);
        readonly List<int> heads = new List<int>(512);
        readonly List<int> edgeTo = new List<int>(2048);
        readonly List<int> edgeNext = new List<int>(2048);
        readonly List<Dir> edgeDir = new List<Dir>(2048);
        int live;
        int selfSupported;

        // Search scratch, grown with the graph, never shrunk.
        int[] visit = new int[0];
        int[] queue = new int[0];
        int[] parent = new int[0];
        int visitGen;

        // Resolve scratch.
        readonly List<int> pendingRemoved = new List<int>(64);
        readonly List<int> pendingInstigator = new List<int>(64);
        readonly List<int> pendingWeakened = new List<int>(16);
        readonly List<int> pendingWeakenedInstigator = new List<int>(16);
        readonly List<int> candidates = new List<int>(128);
        readonly List<int> candidateInstigator = new List<int>(128);
        int[] checkedPass = new int[0];
        int[] lostPass = new int[0];
        int[] quietPass = new int[0];
        int pass;
        readonly List<int> fallingScratch = new List<int>(64);
        readonly List<int> fallingInstigator = new List<int>(64);
        readonly List<int> splitNodes = new List<int>(16);

        public readonly CollapseQueue Queue = new CollapseQueue();

        int States => (chunkLateralHops + 1) * (hangingHops + 1);

        // ---- reading ----

        public int NodeCount => bounds.Count;
        public int LiveCount => live;
        public int EdgeCount => edgeTo.Count;
        public int SelfSupportedCount => selfSupported;
        public int QueuedCount => Queue.Count;

        public bool IsValid(int n) => n >= 0 && n < bounds.Count;
        public NodeKind KindOf(int n) => kinds[n];
        public Bounds BoundsOf(int n) => bounds[n];
        public bool IsAnchor(int n) => IsValid(n) && (flags[n] & F.Anchor) != 0;
        public bool IsSelfSupported(int n) => IsValid(n) && (flags[n] & F.SelfSupported) != 0;
        public bool IsLive(int n) => IsValid(n) && (flags[n] & (F.Removed | F.Replaced)) == 0;
        public bool IsFalling(int n) => IsValid(n) && (flags[n] & F.Falling) != 0;
        public bool IsBlocked(int n) => IsValid(n) && (flags[n] & F.Blocked) != 0;
        // An upper floor slab: never falls, passes support on only while something holds it.
        public bool IsCarrierSlab(int n) => IsValid(n) && IsCarrier(n);
        internal IStructurePart OwnerOf(int n) => owners[n];
        internal int PartOf(int n) => parts[n];
        // The component behind a node (a DestructibleModule, a Breakable, a RoofSection), or null
        // for floors, stairs and plinths. With PartIndexOf, the chunk index inside a wall (-1 for
        // a whole piece). For the overlay and the tests.
        public UnityEngine.Object OwnerObjectOf(int n) => IsValid(n) ? owners[n] as UnityEngine.Object : null;
        public int PartIndexOf(int n) => IsValid(n) ? parts[n] : -1;

        public string Describe(int n)
        {
            if (!IsValid(n)) return "-";
            var o = owners[n];
            string who = o != null && !(o is UnityEngine.Object uo && uo == null) ? o.Describe(parts[n]) : "?";
            return who + " [" + kinds[n] + (IsAnchor(n) ? ", anchor" : "") + (IsSelfSupported(n) ? " (as built)" : "") +
                   (IsBlocked(n) ? ", vertical only" : "") + "]";
        }

        // Neighbours by direction: what n rests on, what rests on n, what touches its sides.
        // Dead neighbours are left out.
        public void GetNeighbours(int n, List<int> down, List<int> up, List<int> side)
        {
            down?.Clear(); up?.Clear(); side?.Clear();
            if (!IsValid(n)) return;
            for (int e = heads[n]; e >= 0; e = edgeNext[e])
            {
                int v = edgeTo[e];
                if (Dead(v)) continue;
                var list = edgeDir[e] == Dir.Down ? down : edgeDir[e] == Dir.Up ? up : side;
                list?.Add(v);
            }
        }

        // ---- building ----

        internal int AddNode(Bounds b, NodeKind kind, IStructurePart owner, int part, bool anchor, bool neverFalls,
                             bool hangs, int group = -1)
        {
            int id = bounds.Count;
            bounds.Add(b);
            kinds.Add(kind);
            owners.Add(owner);
            parts.Add(part);
            groups.Add(group < 0 ? id : group);
            heads.Add(-1);
            F f = 0;
            if (anchor) f |= F.Anchor;
            if (neverFalls) f |= F.NeverFalls;
            if (hangs) f |= F.Hangs;
            flags.Add(f);
            live++;
            return id;
        }

        void Link(int from, int to, Dir d)
        {
            for (int e = heads[from]; e >= 0; e = edgeNext[e])
                if (edgeTo[e] == to && edgeDir[e] == d) return;
            edgeTo.Add(to);
            edgeDir.Add(d);
            edgeNext.Add(heads[from]);
            heads[from] = edgeTo.Count - 1;
        }

        void AddRestsOn(int upper, int lower)
        {
            Link(upper, lower, Dir.Down);
            Link(lower, upper, Dir.Up);
        }

        void AddLateral(int a, int b)
        {
            Link(a, b, Dir.Side);
            Link(b, a, Dir.Side);
        }

        // 1: a rests on b. 2: b rests on a. 3: they touch side by side. 0: apart.
        static int Contact(in Bounds a, in Bounds b, float tol, float minFoot)
        {
            float gap = tol * 2f;
            if (a.min.x > b.max.x + gap || b.min.x > a.max.x + gap) return 0;
            if (a.min.y > b.max.y + gap || b.min.y > a.max.y + gap) return 0;
            if (a.min.z > b.max.z + gap || b.min.z > a.max.z + gap) return 0;
            float fx = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
            float fz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
            if (Mathf.Min(fx, fz) >= minFoot)
            {
                if (Mathf.Abs(a.min.y - b.max.y) <= tol) return 1;
                if (Mathf.Abs(b.min.y - a.max.y) <= tol) return 2;
            }
            return 3;
        }

        bool IsCarrier(int n) => kinds[n] == NodeKind.Floor && (flags[n] & F.Anchor) == 0;

        // Once, at setup: every pair of touching boxes, through a 3 m spatial hash (the kit grid).
        // A piece rests on an upper floor slab only when nothing else is under its middle: the
        // slab carries the interior walls that stand on it (often on the joint of two slabs, or
        // on the corner of a wall below), it does not hold up an outside wall that stands on the
        // wall below.
        public void BuildAdjacency()
        {
            const float cell = 3f;
            int n = bounds.Count;
            var cells = new Dictionary<Vector3Int, List<int>>();
            var cellsOf = new List<Vector3Int>[n];
            float pad = contactTolerance * 2f;
            for (int i = 0; i < n; i++)
            {
                Bounds b = bounds[i];
                Vector3Int lo = Cell(b.min - Vector3.one * pad, cell), hi = Cell(b.max + Vector3.one * pad, cell);
                var mine = new List<Vector3Int>();
                for (int x = lo.x; x <= hi.x; x++)
                for (int y = lo.y; y <= hi.y; y++)
                for (int z = lo.z; z <= hi.z; z++)
                {
                    var k = new Vector3Int(x, y, z);
                    if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<int>();
                    list.Add(i);
                    mine.Add(k);
                }
                cellsOf[i] = mine;
            }

            var seen = new int[n];
            for (int i = 0; i < n; i++) seen[i] = -1;
            var onCarrier = new List<Vector2Int>();
            for (int i = 0; i < n; i++)
            {
                var mine = cellsOf[i];
                for (int c = 0; c < mine.Count; c++)
                {
                    var list = cells[mine[c]];
                    for (int k = 0; k < list.Count; k++)
                    {
                        int j = list[k];
                        if (j <= i || seen[j] == i) continue;
                        seen[j] = i;
                        int contact = Contact(bounds[i], bounds[j], contactTolerance, minFootprint);
                        if (contact == 0) continue;
                        bool ci = IsCarrier(i), cj = IsCarrier(j);
                        if (ci != cj)
                        {
                            int up = ci ? j : i, slab = ci ? i : j;
                            if (Seat(bounds[up], bounds[slab], contactTolerance))
                            {
                                onCarrier.Add(new Vector2Int(up, slab));
                                continue;
                            }
                        }
                        if (contact == 3) { AddLateral(i, j); continue; }
                        int upper = contact == 1 ? i : j, lower = contact == 1 ? j : i;
                        AddRestsOn(upper, lower);
                    }
                }
            }
            // Decided before any slab edge is added, so only real walls count as "under it".
            var heldElsewhere = new bool[onCarrier.Count];
            for (int i = 0; i < onCarrier.Count; i++) heldElsewhere[i] = MiddleIsHeld(onCarrier[i].x);
            for (int i = 0; i < onCarrier.Count; i++)
            {
                var p = onCarrier[i];
                if (heldElsewhere[i]) AddLateral(p.x, p.y);
                else AddRestsOn(p.x, p.y);
            }
        }

        // Sits on the slab's top over any real overlap: an interior wall on the joint of two
        // slabs overlaps each by only a few centimetres.
        static bool Seat(in Bounds upper, in Bounds slab, float tol)
        {
            float fx = Mathf.Min(upper.max.x, slab.max.x) - Mathf.Max(upper.min.x, slab.min.x);
            float fz = Mathf.Min(upper.max.z, slab.max.z) - Mathf.Max(upper.min.z, slab.min.z);
            return Mathf.Abs(upper.min.y - slab.max.y) <= tol && Mathf.Min(fx, fz) >= tol;
        }

        // Does something it rests on reach under its middle?
        bool MiddleIsHeld(int n)
        {
            Vector3 c = bounds[n].center;
            float tol = contactTolerance;
            for (int e = heads[n]; e >= 0; e = edgeNext[e])
            {
                if (edgeDir[e] != Dir.Down) continue;
                Bounds b = bounds[edgeTo[e]];
                if (c.x >= b.min.x - tol && c.x <= b.max.x + tol && c.z >= b.min.z - tol && c.z <= b.max.z + tol) return true;
            }
            return false;
        }

        static Vector3Int Cell(Vector3 p, float size)
        {
            return new Vector3Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size), Mathf.FloorToInt(p.z / size));
        }

        // The kit as built must stand. Anything that cannot reach an anchor at load (a piece the
        // box rules cannot explain, a roof on a wall a few centimetres too low) is made an anchor
        // and listed, rather than falling the moment the level starts.
        public int AnchorUnsupportedAsBuilt(List<int> anchored)
        {
            anchored?.Clear();
            int count = 0;
            for (int i = 0; i < bounds.Count; i++)
            {
                if (Dead(i) || (flags[i] & F.Anchor) != 0) continue;
                if (Search(i, false) >= 0) continue;
                flags[i] |= F.Anchor | F.SelfSupported;
                anchored?.Add(i);
                count++;
            }
            selfSupported += count;
            return count;
        }

        // ---- chunks take over from their wall ----

        internal struct ChunkSpec
        {
            public Bounds bounds;
            public bool anchor, bottom, top, border;
            // Reaches an end of its wall: keyed into the wall or corner beside it, it may hang
            // from that one step, like a roof section.
            public bool side;
        }

        // The wall's node dies and its chunks become nodes: edges inside the wall from the
        // sidecar pairs (a pair one above the other rests, side by side is lateral), edges to
        // the outside from the boxes of the chunks that reach the wall's faces. A support the
        // wall had and no chunk box explains is given to the nearest bottom (or top) chunk, so
        // breaking a wall up never loses a support by rounding.
        //
        // The carrier rule of BuildAdjacency survives the split: an outside wall that stood on
        // the wall below was only beside the upper floor slab it also touches, and its bottom
        // chunks stay beside it. Seated on the slab by their boxes alone, they would be held up
        // by any wall still standing around the slab, and no upper storey could ever fall.
        internal void ReplaceNode(int module, IStructurePart owner, List<ChunkSpec> specs, List<Vector2Int> pairs,
                                  List<int> nodesOut)
        {
            nodesOut.Clear();
            if (!IsValid(module) || (flags[module] & (F.Removed | F.Replaced)) != 0) return;
            bool wasFalling = (flags[module] & F.Falling) != 0;
            int fallingBy = wasFalling ? Queue.InstigatorOf(module) : Actors.World;
            flags[module] |= F.Replaced;
            live--;

            for (int i = 0; i < specs.Count; i++)
            {
                int id = AddNode(specs[i].bounds, NodeKind.Chunk, owner, i, specs[i].anchor, false, specs[i].side, module);
                nodesOut.Add(id);
                // A wall already on its way down (queued, then hit before its turn) goes down
                // chunk by chunk instead: the queue holds the chunks now, not the dead wall node.
                if (wasFalling)
                {
                    flags[id] |= F.Falling;
                    Queue.Add(this, id, fallingBy);
                }
            }

            for (int p = 0; p < pairs.Count; p++)
            {
                int a = pairs[p].x, b = pairs[p].y;
                if (a < 0 || b < 0 || a >= nodesOut.Count || b >= nodesOut.Count) continue;
                Vector3 ca = specs[a].bounds.center, cb = specs[b].bounds.center;
                float dy = ca.y - cb.y;
                float dh = new Vector2(ca.x - cb.x, ca.z - cb.z).magnitude;
                if (Mathf.Abs(dy) > 0.7f * dh)
                {
                    if (dy > 0f) AddRestsOn(nodesOut[a], nodesOut[b]);
                    else AddRestsOn(nodesOut[b], nodesOut[a]);
                }
                else AddLateral(nodesOut[a], nodesOut[b]);
            }

            for (int e = heads[module]; e >= 0; e = edgeNext[e])
            {
                int x = edgeTo[e];
                if ((flags[x] & (F.Removed | F.Replaced)) != 0) continue;
                Dir d = edgeDir[e];
                // The wall rested on this slab (an inside wall built on the floor) or it did not.
                bool besideSlab = d != Dir.Down && IsCarrier(x);
                bool linked = false;
                for (int k = 0; k < specs.Count; k++)
                {
                    if (!specs[k].border) continue;
                    int contact = Contact(specs[k].bounds, bounds[x], chunkTolerance, chunkMinFootprint);
                    if (contact == 0) continue;
                    if (contact == 1 && besideSlab) contact = 3;
                    int node = nodesOut[k];
                    if (contact == 1) { AddRestsOn(node, x); linked |= d == Dir.Down; }
                    else if (contact == 2) { AddRestsOn(x, node); linked |= d == Dir.Up; }
                    else { AddLateral(node, x); linked |= d == Dir.Side; }
                }
                if (!linked && d != Dir.Side) LinkNearest(x, d, specs, nodesOut);
            }
        }

        void LinkNearest(int x, Dir d, List<ChunkSpec> specs, List<int> nodesOut)
        {
            Bounds bx = bounds[x];
            Vector2 cx = new Vector2(bx.center.x, bx.center.z);
            int best = -1;
            float bestD = float.MaxValue;
            for (int k = 0; k < specs.Count; k++)
            {
                if (d == Dir.Down ? !specs[k].bottom : !specs[k].top) continue;
                Bounds b = specs[k].bounds;
                float dist = (new Vector2(b.center.x, b.center.z) - cx).sqrMagnitude;
                if (dist < bestD) { bestD = dist; best = k; }
            }
            if (best < 0) return;
            if (d == Dir.Down) AddRestsOn(nodesOut[best], x);
            else AddRestsOn(x, nodesOut[best]);
        }

        // ---- breaking ----

        // Out of the world (broken off, fallen, destroyed). What it held is checked at the next
        // Resolve.
        public void MarkRemoved(int n, int instigator)
        {
            if (!IsValid(n) || (flags[n] & (F.Removed | F.Replaced)) != 0) return;
            flags[n] = (flags[n] | F.Removed) & ~F.Falling;
            live--;
            pendingRemoved.Add(n);
            pendingInstigator.Add(instigator);
        }

        // A chunk went Fractured: it still holds what is above it, nothing beside it. What comes
        // down because of that is blamed on whoever wore it down.
        public void MarkWeakened(int n, int instigator = Actors.World)
        {
            if (!IsValid(n) || (flags[n] & F.Blocked) != 0) return;
            flags[n] |= F.Blocked;
            pendingWeakened.Add(n);
            pendingWeakenedInstigator.Add(instigator);
        }

        public bool HasPending => pendingRemoved.Count > 0 || pendingWeakened.Count > 0;

        // Checks everything the last breaks could have unsettled and queues what lost its
        // support. Returns how many nodes started to fall.
        public int ResolvePending()
        {
            if (!HasPending) return 0;
            fallingScratch.Clear();
            fallingInstigator.Clear();
            int n = Resolve(fallingScratch, fallingInstigator);
            for (int i = 0; i < fallingScratch.Count; i++) Queue.Add(this, fallingScratch[i], fallingInstigator[i]);
            return n;
        }

        int Resolve(List<int> fallingOut, List<int> instigatorOut)
        {
            pass++;
            EnsureResolveScratch();
            candidates.Clear();
            candidateInstigator.Clear();

            for (int i = 0; i < pendingRemoved.Count; i++)
                EnqueueDependents(pendingRemoved[i], pendingInstigator[i], true);
            for (int i = 0; i < pendingWeakened.Count; i++)
            {
                int w = pendingWeakened[i];
                int who = pendingWeakenedInstigator[i];
                Enqueue(w, who);
                EnqueueDependents(w, who, false);
            }
            pendingRemoved.Clear();
            pendingInstigator.Clear();
            pendingWeakened.Clear();
            pendingWeakenedInstigator.Clear();

            int fell = 0;
            for (int q = 0; q < candidates.Count; q++)
            {
                int n = candidates[q];
                int who = candidateInstigator[q];
                EnsureResolveScratch();
                if (Dead(n) || (flags[n] & F.Anchor) != 0) continue;
                if (checkedPass[n] == pass) continue;
                checkedPass[n] = pass;

                // A wall that lost part of what it stood on is judged chunk by chunk.
                if (kinds[n] == NodeKind.Module && lostPass[n] == pass && owners[n] != null && SplitSafely(n))
                {
                    EnsureResolveScratch();
                    for (int k = 0; k < splitNodes.Count; k++) Enqueue(splitNodes[k], who);
                    continue;
                }

                if (Search(n, false) >= 0) continue;

                if ((flags[n] & F.NeverFalls) != 0)
                {
                    // It stays where it is, but it no longer holds anything up. Once per pass:
                    // two unsupported slabs side by side would otherwise wake each other forever.
                    if (quietPass[n] != pass)
                    {
                        quietPass[n] = pass;
                        EnqueueDependents(n, who, true);
                    }
                    continue;
                }

                flags[n] |= F.Falling;
                fallingOut.Add(n);
                instigatorOut.Add(who);
                fell++;
                EnqueueDependents(n, who, true);
            }
            return fell;
        }

        bool SplitSafely(int n)
        {
            splitNodes.Clear();
            int before = bounds.Count;
            bool split;
            try { split = owners[n].SplitForSupport(n); }
            catch (Exception e) { Debug.LogException(e); return false; }
            if (!split) return false;
            for (int i = before; i < bounds.Count; i++) if (groups[i] == n) splitNodes.Add(i);
            return true;
        }

        void Enqueue(int n, int instigator)
        {
            candidates.Add(n);
            candidateInstigator.Add(instigator);
        }

        // What leaned on n: what rests on it, what touches its sides. Each one is (re)checked.
        void EnqueueDependents(int n, int instigator, bool lostSupport)
        {
            for (int e = heads[n]; e >= 0; e = edgeNext[e])
            {
                if (edgeDir[e] == Dir.Down) continue;
                int d = edgeTo[e];
                if (Dead(d)) continue;
                checkedPass[d] = 0;
                if (lostSupport && edgeDir[e] == Dir.Up) lostPass[d] = pass;
                Enqueue(d, instigator);
            }
        }

        void EnsureResolveScratch()
        {
            int n = bounds.Count;
            if (checkedPass.Length < n)
            {
                Array.Resize(ref checkedPass, Mathf.NextPowerOfTwo(n + 1));
                Array.Resize(ref lostPass, checkedPass.Length);
                Array.Resize(ref quietPass, checkedPass.Length);
            }
        }

        // ---- support search ----

        public bool IsSupported(int n)
        {
            if (!IsValid(n) || Dead(n)) return false;
            return Search(n, false) >= 0;
        }

        // For the overlay: how many steps from n down to its anchor, and how many of them were
        // sideways. False when it has no support.
        public bool Explain(int n, out int steps, out int lateralHops)
        {
            steps = 0;
            lateralHops = 0;
            if (!IsValid(n) || Dead(n)) return false;
            int found = Search(n, true);
            if (found < 0) return false;
            int s = States;
            int r = found % s;
            lateralHops = r / (hangingHops + 1) + r % (hangingHops + 1);
            int start = n * s;
            for (int cur = found; cur != start && steps < 10000; cur = parent[cur]) steps++;
            return true;
        }

        bool Dead(int n) => (flags[n] & (F.Removed | F.Falling | F.Replaced)) != 0;

        // Breadth first over (node, sideways steps inside a wall, sideways steps between pieces).
        // Returns the state where it reached an anchor, or -1.
        int Search(int start, bool trace)
        {
            int s = States;
            int need = bounds.Count * s;
            if (visit.Length < need)
            {
                int size = Mathf.NextPowerOfTwo(need + 1);
                visit = new int[size];
                queue = new int[size];
                parent = new int[size];
                visitGen = 0;
            }
            if (++visitGen == int.MaxValue)
            {
                Array.Clear(visit, 0, visit.Length);
                visitGen = 1;
            }
            int hangStates = hangingHops + 1;
            int head = 0, tail = 0;
            int first = start * s;
            visit[first] = visitGen;
            queue[tail++] = first;
            while (head < tail)
            {
                int st = queue[head++];
                int u = st / s;
                int r = st % s;
                int a = r / hangStates, b = r % hangStates;
                F fu = flags[u];
                if ((fu & F.Anchor) != 0) return st;
                for (int e = heads[u]; e >= 0; e = edgeNext[e])
                {
                    Dir d = edgeDir[e];
                    if (d == Dir.Up) continue;
                    int v = edgeTo[e];
                    F fv = flags[v];
                    if ((fv & (F.Removed | F.Falling | F.Replaced)) != 0) continue;
                    int na = a, nb = b;
                    if (d == Dir.Side)
                    {
                        bool inWall = groups[u] == groups[v] && kinds[u] == NodeKind.Chunk && kinds[v] == NodeKind.Chunk;
                        if (inWall)
                        {
                            if (((fu | fv) & F.Blocked) != 0 || a >= chunkLateralHops) continue;
                            na = a + 1;
                        }
                        else
                        {
                            if ((fu & F.Hangs) == 0 || b >= hangingHops) continue;
                            // A floor holds up what is built on it, never what merely touches its
                            // edge: only another slab may hang from a slab. Without this, the
                            // end chunk of a wall kept beside the slab (ReplaceNode) would hang
                            // from it and hold the rest of the wall up.
                            if (kinds[v] == NodeKind.Floor && kinds[u] != NodeKind.Floor) continue;
                            nb = b + 1;
                        }
                    }
                    int ns = v * s + na * hangStates + nb;
                    if (visit[ns] == visitGen) continue;
                    visit[ns] = visitGen;
                    if (trace) parent[ns] = st;
                    queue[tail++] = ns;
                }
            }
            return -1;
        }
    }

    // Pieces that lost their support, waiting to fall: lowest first, each with a small random
    // delay (0 to 0.3 s) so a wall comes down as a crumble and not as one block, and at most 40
    // per frame (the caller's budget) so a big collapse is spread over a few frames instead of
    // one spike.
    public sealed class CollapseQueue
    {
        struct Item
        {
            public int node;
            public int instigator;
            public float at;
            public float height;
        }

        struct Fallen
        {
            public UnityEngine.Object subject;
            public Vector3 at;
            public float mass;
            public int instigator;
        }

        public float maxStagger = 0.3f;

        readonly List<Item> items = new List<Item>(64);
        readonly List<Item> ready = new List<Item>(64);
        readonly List<Fallen> fallen = new List<Fallen>(8);

        public int Count => items.Count;
        public int ReleasedTotal { get; private set; }

        // Who the node was queued for, World if it is not waiting.
        internal int InstigatorOf(int node)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].node == node) return items[i].instigator;
            return Actors.World;
        }

        internal void Add(StructureGraph g, int node, int instigator)
        {
            float h = g.BoundsOf(node).min.y;
            var it = new Item { node = node, instigator = instigator, at = Time.time + UnityEngine.Random.Range(0f, maxStagger), height = h };
            int i = items.Count;
            while (i > 0 && items[i - 1].height > h) i--;
            items.Insert(i, it);
        }

        public void Clear()
        {
            items.Clear();
            ready.Clear();
            fallen.Clear();
        }

        // Releases what is due, lowest first, at most 'max' of them. One StructureCollapsed per
        // piece of the house per call, not per chunk: a wall coming down is one fact, not twelve.
        public int Tick(StructureGraph g, float now, int max)
        {
            if (items.Count == 0 || max <= 0) return 0;
            ready.Clear();
            int write = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (ready.Count < max && it.at <= now) ready.Add(it);
                else items[write++] = it;
            }
            items.RemoveRange(write, items.Count - write);

            fallen.Clear();
            for (int i = 0; i < ready.Count; i++) Release(g, ready[i]);
            ReleasedTotal += ready.Count;
            for (int i = 0; i < fallen.Count; i++)
            {
                var f = fallen[i];
                try { DestructionEvents.Collapsed(f.subject, f.at, f.mass, f.instigator); }
                catch (Exception e) { Debug.LogException(e); }
            }
            fallen.Clear();
            int released = ready.Count;
            ready.Clear();
            return released;
        }

        void Release(StructureGraph g, Item it)
        {
            int n = it.node;
            if (!g.IsValid(n) || !g.IsLive(n)) return;
            var owner = g.OwnerOf(n);
            var subject = owner as UnityEngine.Object;
            if (owner == null || subject == null)
            {
                g.MarkRemoved(n, it.instigator);
                return;
            }
            Bounds b = g.BoundsOf(n);
            var cause = new DamageEvent(b.center, Vector3.down, 0f, 0f, 0f, DamageType.Fall, it.instigator);
            float kg = 0f;
            try { kg = owner.Release(n, g.PartOf(n), cause); }
            catch (Exception e) { Debug.LogException(e); }
            // Whatever the owner did, the node is out of the structure now.
            if (g.IsLive(n)) g.MarkRemoved(n, it.instigator);
            if (kg <= 0f) return;

            for (int i = 0; i < fallen.Count; i++)
            {
                if (fallen[i].subject != subject) continue;
                var f = fallen[i];
                f.mass += kg;
                fallen[i] = f;
                return;
            }
            fallen.Add(new Fallen { subject = subject, at = b.center, mass = kg, instigator = it.instigator });
        }
    }
}
