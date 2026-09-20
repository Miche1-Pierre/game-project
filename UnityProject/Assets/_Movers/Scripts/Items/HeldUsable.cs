using UnityEngine;

namespace Movers
{
    // A carried object that does something while you hold the use button.
    //
    // The starting inventory used to be two viewmodels welded to the camera. They are ordinary
    // physics objects now: you find them by the truck, you pick them up with LMB like a chair,
    // and the only thing that makes them special is this component. Everything the crew can
    // hold goes through one grab system, and nothing needs an inventory.
    //
    // The right button is where it gets interesting. On a sofa it throws, instantly, as it
    // always has. On one of these it is modal: a tap throws, holding it uses the thing. That
    // is the third modal input in the game after the rotate key and the scroll wheel, and it
    // is the last one that fits (ADR-007).
    [RequireComponent(typeof(MovableObject))]
    public abstract class HeldUsable : MonoBehaviour
    {
        // The spawner that put this one in the world, if any. Told when the object is thrown,
        // so a fresh one appears where this one started.
        [HideInInspector] public StartingItemSpawner spawner;

        // false means the right button keeps its ordinary meaning and throws on press. The
        // beer says false: it is drunk with its own key, so there is nothing to hold for.
        public virtual bool UsesHoldButton => true;

        protected PlayerGrab holder;
        public bool IsHeld => holder != null;

        public virtual void OnPickedUp(PlayerGrab by) { holder = by; }

        public virtual void OnReleased(bool thrown)
        {
            holder = null;
            // Dropped is not discarded. Put it down and it is still your cigarette; throw it
            // away and the crew van has another one.
            if (thrown) Discard();
        }

        public virtual void OnUseBegin() { }
        public virtual void OnUseHold(float dt) { }
        public virtual void OnUseEnd() { }

        // This one is spent as far as the spawner is concerned. The object itself lives on
        // unless something else destroys it: a flicked cigarette really is lying there.
        protected void Discard()
        {
            if (spawner == null) return;
            spawner.NotifyDiscarded(this);
            spawner = null;
        }
    }
}
