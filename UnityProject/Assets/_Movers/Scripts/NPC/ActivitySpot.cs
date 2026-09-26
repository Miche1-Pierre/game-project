using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    public enum ActivityKind { SitRockingChair, ReadBook, DrinkTea, Cook, WaterPlants, LightFire, WatchTV, LookOutside }

    // What she holds during an activity. Auto = the kind's usual prop.
    public enum GrandmaProp { Auto, None, Book, Cup, Match, WateringCan, Spoon, Keys }

    // A place where the grandmother does something: her rocking chair, the stove, a flower bed.
    // Placed in the scene (INTEGRATION.md), read by GrandmaActivities.
    //
    // The transform is her pose during the activity: its position is where her feet go, its
    // forward the way she faces. For a seat that point is inside the chair, so she first walks
    // to a stand point in front of it (standPoint, or standOff metres ahead of the spot) and
    // covers the last bit with her body's collisions off.
    //
    // A spot depends on the objects that make it. When one of them has been carried more than
    // a metre from where it stood, loaded, pocketed or smashed, the spot is missing and she
    // comments on it the first time she finds out.
    public sealed class ActivitySpot : MonoBehaviour
    {
        public enum Availability { Available, Missing, NotNeeded }

        public ActivityKind kind;
        [Tooltip("How she names it: \"my rocking chair\". Empty: the first required object's name.")]
        public string label = "";

        [Header("Approach")]
        [Tooltip("Where she walks to first. Empty: standOff metres in front of the spot.")]
        public Transform standPoint;
        public float standOff = 0f;

        [Header("What makes the spot")]
        [Tooltip("The first one is the seat for sitting kinds: moving it while she sits dumps her.")]
        public GameObject[] requires = new GameObject[0];
        [Tooltip("LightFire only: the fire this spot lights.")]
        public FireplaceFire fire;

        [Header("Overrides (empty or zero: the kind's defaults)")]
        public string enterState = "";
        public string loopState = "";
        public string exitState = "";
        public Vector2 durationRange = Vector2.zero;
        public GrandmaProp prop = GrandmaProp.Auto;
        [Tooltip("How much more often than the others she picks this one.")]
        public float weight = 1f;

        // Runtime, for GrandmaActivities.
        [System.NonSerialized] public float lastUsedTime = -999f;
        [System.NonSerialized] public bool commented;

        Vector3[] startPositions;
        MovableObject[] movables;

        static readonly List<ActivitySpot> all = new List<ActivitySpot>();
        public static IReadOnlyList<ActivitySpot> All => all;

        // Defaults per kind: the Animator states (AC_Grandma_Slice, SLICE_ARCHITECTURE section
        // 13), how long entering and leaving take, how long she stays and what she holds.
        struct Defaults
        {
            public string enter, loop, exit;
            public float enterSeconds, exitSeconds;
            public Vector2 duration;
            public GrandmaProp prop;
            public bool seated;
        }

        static Defaults For(ActivityKind k)
        {
            switch (k)
            {
                case ActivityKind.SitRockingChair: return SitDefaults("Sit_Idle", new Vector2(20f, 35f), GrandmaProp.None);
                case ActivityKind.ReadBook: return SitDefaults("Sit_Read", new Vector2(20f, 35f), GrandmaProp.Book);
                case ActivityKind.WatchTV: return SitDefaults("Sit_Idle", new Vector2(25f, 40f), GrandmaProp.None);
                case ActivityKind.DrinkTea: return StandDefaults("Stand_Drink", new Vector2(10f, 16f), GrandmaProp.Cup);
                case ActivityKind.Cook: return StandDefaults("Stand_Cook", new Vector2(15f, 25f), GrandmaProp.Spoon);
                case ActivityKind.WaterPlants: return StandDefaults("Stand_Water", new Vector2(8f, 14f), GrandmaProp.WateringCan);
                case ActivityKind.LightFire:
                    return new Defaults
                    {
                        enter = "Kneel_Down", loop = "Kneel_LightFire", exit = "Kneel_Up",
                        enterSeconds = 2f, exitSeconds = 2.5f, duration = new Vector2(6f, 10f),
                        prop = GrandmaProp.Match
                    };
                default: return StandDefaults("", new Vector2(6f, 12f), GrandmaProp.None);   // LookOutside: her idle
            }
        }

        static Defaults SitDefaults(string loop, Vector2 duration, GrandmaProp prop) => new Defaults
        {
            enter = "Sit_Down", loop = loop, exit = "Stand_Up",
            enterSeconds = 1.6f, exitSeconds = 2f, duration = duration, prop = prop, seated = true
        };

        static Defaults StandDefaults(string loop, Vector2 duration, GrandmaProp prop) => new Defaults
        {
            enter = "", loop = loop, exit = "", enterSeconds = 0f, exitSeconds = 0f, duration = duration, prop = prop
        };

        public bool Seated => For(kind).seated;
        public string EnterState => string.IsNullOrEmpty(enterState) ? For(kind).enter : enterState;
        public string LoopState => string.IsNullOrEmpty(loopState) ? For(kind).loop : loopState;
        public string ExitState => string.IsNullOrEmpty(exitState) ? For(kind).exit : exitState;
        public float EnterSeconds => For(kind).enterSeconds;
        public float ExitSeconds => For(kind).exitSeconds;
        public GrandmaProp Prop => prop == GrandmaProp.Auto ? For(kind).prop : prop;

        public float PickDuration()
        {
            Vector2 d = durationRange.y > 0f ? durationRange : For(kind).duration;
            return Random.Range(Mathf.Min(d.x, d.y), Mathf.Max(d.x, d.y));
        }

        public Vector3 Anchor => transform.position;
        public Vector3 Facing
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            }
        }

        public Vector3 StandPosition => standPoint != null ? standPoint.position : Anchor + Facing * standOff;

        // The seat she sits in, for the "someone carried my chair off with me in it" check.
        public MovableObject Seat => Seated && movables != null && movables.Length > 0 ? movables[0] : null;
        // The seat as a thing with colliders (movable or not), for her seated stand-in to ignore.
        public GameObject SeatObject => Seated && requires != null && requires.Length > 0 ? requires[0] : null;

        public string Label
        {
            get
            {
                if (!string.IsNullOrEmpty(label)) return label;
                if (requires != null && requires.Length > 0 && requires[0] != null) return requires[0].name;
                return kind.ToString();
            }
        }

        void Awake()
        {
            int n = requires != null ? requires.Length : 0;
            startPositions = new Vector3[n];
            movables = new MovableObject[n];
            for (int i = 0; i < n; i++)
            {
                if (requires[i] == null) continue;
                startPositions[i] = requires[i].transform.position;
                movables[i] = requires[i].GetComponent<MovableObject>();
            }
        }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        // Can she do it now? `missing` names the object that is gone, for her comment.
        public Availability Check(out GameObject missing)
        {
            missing = null;
            if (kind == ActivityKind.LightFire)
            {
                // No fireplace left (it broke) counts as missing; a fire already burning just
                // means there is nothing to do here.
                if (fire == null || !fire.isActiveAndEnabled) { missing = fire != null ? fire.gameObject : null; return Availability.Missing; }
                if (fire.hearth != null && !fire.hearth.activeInHierarchy) { missing = fire.hearth; return Availability.Missing; }
                if (fire.IsLit) return Availability.NotNeeded;
            }

            int n = requires != null ? requires.Length : 0;
            for (int i = 0; i < n; i++)
            {
                GameObject go = requires[i];
                if (go == null) continue;   // never assigned, or destroyed for good: ignore
                if (!go.activeInHierarchy) { missing = go; return Availability.Missing; }
                MovableObject mo = i < movables.Length ? movables[i] : null;
                if (mo == null) continue;
                if (mo.destroyed || mo.loaded || mo.inPocket || mo.holder != null) { missing = go; return Availability.Missing; }
                if ((mo.transform.position - startPositions[i]).sqrMagnitude > 1f) { missing = go; return Availability.Missing; }
            }
            return Availability.Available;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.8f, 0.9f);
            Vector3 p = transform.position + Vector3.up * 0.05f;
            Gizmos.DrawWireSphere(p, 0.2f);
            Gizmos.DrawLine(p, p + Facing * 0.6f);
            Gizmos.color = new Color(1f, 0.5f, 0.8f, 0.4f);
            Gizmos.DrawLine(p, StandPosition + Vector3.up * 0.05f);
            Gizmos.DrawWireCube(StandPosition + Vector3.up * 0.05f, new Vector3(0.3f, 0.02f, 0.3f));
        }
    }
}
