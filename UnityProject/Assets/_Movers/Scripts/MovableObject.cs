using UnityEngine;

namespace Movers
{
    // Greybox data component for anything the crew can pick up and move.
    // Prototype-first: one component drives all objects, no per-object subclasses.
    [RequireComponent(typeof(Rigidbody))]
    public class MovableObject : MonoBehaviour
    {
        public string displayName = "Object";
        public int contractValue = 0;        // money paid when delivered
        public float weight = 10f;           // kg; also sets Rigidbody mass and slows the carrier
        public bool requiredForContract = false;
        public bool fragile = false;
        public float breakThreshold = 6f;    // collision impulse magnitude that "breaks" a fragile item

        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public bool loaded = false;  // true while inside the truck cargo trigger
        [HideInInspector] public bool broken = false;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.1f, weight);
        }

        void OnCollisionEnter(Collision c)
        {
            if (fragile && !broken && c.impulse.magnitude > breakThreshold)
            {
                broken = true;
                var r = GetComponentInChildren<Renderer>();
                if (r != null) r.material.color = Color.gray; // greybox: just mark it, no fragmentation yet
            }
        }
    }
}
