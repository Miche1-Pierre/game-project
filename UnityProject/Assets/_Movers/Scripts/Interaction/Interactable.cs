using UnityEngine;

namespace Movers
{
    // Anything the action key can be used on: a door, a window, the garage.
    //
    // PlayerInteract finds one of these under the crosshair (on the collider that was hit or on
    // any of its parents), shows its prompt, and calls Interact when the key goes down. That is
    // the whole contract. A new kind of thing to use is one small subclass, not a new key.
    public abstract class Interactable : MonoBehaviour
    {
        // The verb shown under the crosshair, after the key: "[E] Open". Read every frame the
        // thing is looked at, so it can change with the state ("Open", then "Close").
        public virtual string Prompt => "Use";

        // The verb for one player in particular. Two players can look at the same door and be
        // offered different things: the one with the key reads "Unlock", the other "Locked".
        // PlayerInteract should draw PromptFor(itself). Anything that does not care who is
        // looking just shows Prompt.
        public virtual string PromptFor(PlayerInteract by) => Prompt;

        // false hides the prompt and ignores the key. A shattered window is still something you
        // can look at, it is no longer something you can open.
        public virtual bool CanInteract => true;

        // Does a hit at this point (world space) count as looking at this thing? Almost always
        // yes. The exception is a frame that is much bigger than what it frames: a window's
        // collider is a whole stretch of wall, and only the part around the window should offer
        // to open it.
        public virtual bool Covers(Vector3 hitPoint) => true;

        public abstract void Interact(PlayerInteract by);

        // Who Prompt is for, when nobody said. A drawer that reads Prompt (not PromptFor) must
        // still show the verb that player's E will do, or a key holder reads "Locked" and E
        // unlocks. So the viewer is the one crew member whose crosshair rests on this thing.
        // Nobody, or two players on it at once: nobody in particular, the verb for empty hands.
        protected PlayerInteract Viewer()
        {
            PlayerInteract found = null;
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                PlayerInteract pi = m != null ? m.Interact : null;
                if (pi == null || !pi.isActiveAndEnabled || !IsAimedAt(pi.Target)) continue;
                if (found != null) return null;
                found = pi;
            }
            return found;
        }

        // The crosshair rests on `target`: does that count as aiming at this? A group also
        // answers for the panels it works.
        protected virtual bool IsAimedAt(Interactable target) => target == this;

        // The crew member working the key, or null (a test, a script, a scene without the crew
        // components). Events and damage records want the index, never the object.
        protected static CrewMember CrewOf(PlayerInteract by)
        {
            return by != null ? CrewRoster.Owner(by.transform) : null;
        }

        protected static int ActorOf(CrewMember who)
        {
            return who != null ? who.index : Actors.World;
        }
    }
}
