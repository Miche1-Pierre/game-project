using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The players present (CrewMember, in Core/CrewMember.cs: a MonoBehaviour must live in a
    // file of its own name, or Unity cannot save it in a scene).
    public static class CrewRoster
    {
        static readonly List<CrewMember> members = new List<CrewMember>();

        public static event Action<CrewMember> Joined;
        public static event Action<CrewMember> Left;

        public static IReadOnlyList<CrewMember> All => members;
        public static int Count => members.Count;

        public static CrewMember Get(int index)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null && members[i].index == index) return members[i];
            return null;
        }

        // The crew member whose body owns this transform (a collider, a camera), or null.
        public static CrewMember Owner(Transform t)
        {
            return t != null ? t.GetComponentInParent<CrewMember>() : null;
        }

        public static CrewMember Nearest(Vector3 p, float maxDistance = float.PositiveInfinity)
        {
            CrewMember best = null;
            float bestSq = maxDistance * maxDistance;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m == null) continue;
                float d = (m.Position - p).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = m; }
            }
            return best;
        }

        internal static void Register(CrewMember m)
        {
            if (m == null || members.Contains(m)) return;
            members.Add(m);
            members.Sort((a, b) => a.index.CompareTo(b.index));
            try { Joined?.Invoke(m); } catch (Exception e) { Debug.LogException(e); }
        }

        internal static void Unregister(CrewMember m)
        {
            if (!members.Remove(m)) return;
            try { Left?.Invoke(m); } catch (Exception e) { Debug.LogException(e); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            members.Clear();
            Joined = null;
            Left = null;
        }
    }
}
