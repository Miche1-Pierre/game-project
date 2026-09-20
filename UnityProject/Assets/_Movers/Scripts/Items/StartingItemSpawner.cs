using UnityEngine;

namespace Movers
{
    // The spot by the truck where the crew keeps its smokes and its beer.
    //
    // It lays one item down when the game starts and lays a fresh one down whenever the last
    // one is thrown away or broken. That is all the "inventory" this game has: a place, not a
    // slot. Walk back to the van if you want another.
    //
    // Nothing is serialised into the scene except this empty transform, so the scene files stay
    // clean and the item is built from code at Play like everything else in the greybox.
    public class StartingItemSpawner : MonoBehaviour
    {
        public enum Kind { Cigarette, Beer }

        public Kind kind = Kind.Cigarette;
        // Long enough that a fresh one does not pop into view in the same breath as the throw.
        public float respawnDelay = 0.6f;
        // Items are laid on the floor rather than dropped from wherever the transform sits.
        public bool snapToGround = true;

        HeldUsable current;
        float dueAt;

        public HeldUsable Current => current;

        void Start() { Spawn(); }

        void Update()
        {
            // current goes null two ways: thrown (NotifyDiscarded clears it and the object
            // lives on as litter) or destroyed (a broken bottle). Both mean: put another one out.
            if (current == null && Time.time >= dueAt) Spawn();
        }

        public void NotifyDiscarded(HeldUsable who)
        {
            if (current != who) return;
            current = null;
            dueAt = Time.time + Mathf.Max(0f, respawnDelay);
        }

        void Spawn()
        {
            Vector3 at = snapToGround ? ItemArt.GroundUnder(transform.position) : transform.position;

            switch (kind)
            {
                case Kind.Beer: current = BeerItem.Create(at); break;
                default: current = CigaretteItem.Create(at); break;
            }

            current.spawner = this;
            current.transform.SetParent(transform.parent, true);   // keep the hierarchy tidy
            dueAt = 0f;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Beer
                ? new Color(0.85f, 0.55f, 0.15f, 0.9f)
                : new Color(0.95f, 0.95f, 0.9f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.12f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.3f);
        }
    }
}
