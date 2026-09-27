using UnityEngine;

namespace Movers
{
    // The components that make a player object a crew member, in one place, so the house
    // (CrewSpawner), the tutorial (GreyboxBootstrap) and an older scene nobody set up all end
    // with the same player. Scenes saved before the split screen have none of these; Unity
    // does not add a RequireComponent to an object that was saved without it, so the check
    // has to happen at runtime.
    public static class CrewSetup
    {
        // This player's input, created on the spot when the scene has none. Every player
        // component asks through here in its Awake, whichever of them wakes first.
        public static CrewInput InputOf(GameObject player)
        {
            if (player.TryGetComponent(out CrewInput input)) return input;
            return player.AddComponent<CrewInput>();
        }

        // Everything a crew member carries besides the controller and the grab, which the
        // scene authors. Safe to call twice. The body parts are only added when there is a body
        // (the tutorial player is a bare capsule).
        public static CrewMember Ensure(GameObject player)
        {
            InputOf(player);
            if (!player.TryGetComponent(out CrewMember member)) member = player.AddComponent<CrewMember>();
            if (!player.TryGetComponent(out CrewHUD _)) player.AddComponent<CrewHUD>();
            if (player.TryGetComponent(out PlayerGrab _))
            {
                // The alt button (the beer, wearing) lives there; older scenes never had it.
                if (!player.TryGetComponent(out PlayerEquip _)) player.AddComponent<PlayerEquip>();
                // So does the action button. The tutorial's saved player was built before it
                // existed, and without it the delivery board at the truck (an Interactable)
                // never shows "[E] Deliver" to it.
                if (!player.TryGetComponent(out PlayerInteract _)) player.AddComponent<PlayerInteract>();
                // Using what you hold, seen from inside your head: the cigarette to the lips, the
                // bottle tipped back, the grenade wound up (CHARACTERS).
                if (!player.TryGetComponent(out HeldPose _)) player.AddComponent<HeldPose>();
            }
            // A blast that throws you tumbles your view to the floor and back (CHARACTERS).
            if (!player.TryGetComponent(out KnockdownTumble _)) player.AddComponent<KnockdownTumble>();

            var body = player.GetComponentInChildren<Animator>(true);
            if (body != null)
            {
                if (!player.TryGetComponent(out CrewAnimator _)) player.AddComponent<CrewAnimator>();
                if (!player.TryGetComponent(out FirstPersonBody _)) player.AddComponent<FirstPersonBody>();
                // The cigarette or bottle in the body's hand, for everyone else's camera (CHARACTERS).
                if (!player.TryGetComponent(out HandHeldProp _)) player.AddComponent<HandHeldProp>();
            }

            member.Resolve();
            return member;
        }

        // Whether this object's command is the one registered on this key right now.
        public static bool OwnsDebugKey(KeyCode key, object owner)
        {
            var all = DebugCommands.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].key == key && all[i].run != null && ReferenceEquals(all[i].run.Target, owner)) return true;
            return false;
        }

        // The layer every crew capsule lives on (SLICE_ARCHITECTURE, layers), when the project
        // has it. Only the root: the capsule is the one collider a player has.
        public static void PutOnCrewLayer(GameObject player)
        {
            int layer = LayerMask.NameToLayer("Crew");
            if (layer >= 0) player.layer = layer;
        }
    }
}
