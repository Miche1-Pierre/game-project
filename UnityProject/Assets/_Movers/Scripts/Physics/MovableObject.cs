using UnityEngine;

namespace Movers
{
    // Data for anything the crew can pick up and move, and its physical profile (ADR-009).
    // Prototype-first: one component drives all objects, no per-object subclasses. What an
    // object can do is data here; what the player does with it is in PlayerGrab and friends.
    [RequireComponent(typeof(Rigidbody))]
    public class MovableObject : MonoBehaviour
    {
        public string displayName = "Object";
        public int contractValue = 0;        // money: paid when delivered on the contract, its worth when stolen
        public float weight = 10f;           // kg; also sets Rigidbody mass and slows the carrier
        public bool requiredForContract = false;
        public bool fragile = false;
        public float breakThreshold = 6f;    // legacy (no Breakable): collision impulse that "breaks" a fragile item

        [Header("Physical profile")]
        public bool canCarry = true;         // false: too heavy or fixed to lift; the grab pushes or drags it instead
        public bool canPush = true;
        public bool canThrow = true;
        public bool canBeLoaded = true;      // counts when it is in the truck
        public bool pocketable = false;      // fits in a pocket: small valuables, the crew's own items
        // Everything in the house is the grandmother's. Taking one of hers that is not on the
        // contract out of the house is theft. The crew's own items (cigarette, beer) are not hers.
        public bool ownedByGrandma = true;

        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public bool loaded = false;  // true while inside the truck cargo trigger
        [HideInInspector] public bool broken = false;
        // True once a Breakable has shattered it. The object is then inactive, never null, so
        // the contract list keeps its reference and can bill the crew for it.
        [HideInInspector] public bool destroyed = false;

        // Runtime bookkeeping, written by the player code. Not saved.
        [System.NonSerialized] public PlayerGrab holder;            // who carries it now; one carrier at a time
        [System.NonSerialized] public int lastHandler = Actors.World;
        [System.NonSerialized] public float lastHandledTime = -99f;
        [System.NonSerialized] public int lastThrownBy = Actors.World;
        [System.NonSerialized] public float lastThrownTime = -99f;
        [System.NonSerialized] public bool inPocket;                // riding in a player's pocket (inactive)
        [System.NonSerialized] public bool worn;                    // worn on a body

        public float Mass => rb != null ? rb.mass : Mathf.Max(0.1f, weight);

        // Who handled it within the last few seconds, for "who broke that": a vase thrown by P2
        // that breaks a window a second later was P2's doing.
        public int RecentHandler(float withinSeconds)
        {
            if (Time.time - lastThrownTime <= withinSeconds) return lastThrownBy;
            if (Time.time - lastHandledTime <= withinSeconds) return lastHandler;
            return Actors.World;
        }

        float volume = -1f;
        // Cubic metres, from its colliders' bounds, measured once.
        public float Volume
        {
            get
            {
                if (volume < 0f)
                {
                    volume = 0f;
                    foreach (var c in GetComponentsInChildren<Collider>(true))
                    {
                        if (c.isTrigger) continue;
                        var s = c.bounds.size;
                        volume += s.x * s.y * s.z;
                    }
                }
                return volume;
            }
        }

        public bool CanBreak => TryGetComponent(out Breakable _);
        public BreakMaterial Material => TryGetComponent(out Breakable b) ? b.material : BreakMaterial.Wood;
        public float Durability => TryGetComponent(out Breakable b) ? b.MaxHealth : 0f;
        public bool CanOpen => GetComponentInChildren<Interactable>(true) != null;
        // Hers and not on the list: taking it out of the house is theft.
        public bool IsTheftTarget => ownedByGrandma && !requiredForContract;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.1f, weight);
        }

        void OnCollisionEnter(Collision c)
        {
            // With destruction in the scene, Breakable owns breakage: it weighs the hit, marks
            // the object broken at half health and shatters it at zero. This older rule stays
            // for scenes without it (Tutorial_01), where a fragile object just turns grey.
            if (TryGetComponent(out Breakable _)) return;

            if (fragile && !broken && c.impulse.magnitude > breakThreshold)
            {
                broken = true;
                var r = GetComponentInChildren<Renderer>();
                if (r != null) r.material.color = Color.gray; // greybox: just mark it, no fragmentation yet
            }
        }
    }
}
