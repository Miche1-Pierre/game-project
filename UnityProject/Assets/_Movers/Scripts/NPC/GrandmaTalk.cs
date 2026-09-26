namespace Movers
{
    // E on the grandmother: "Talk". During the intro it starts the key handover; afterwards she
    // answers with a line that depends on her mood. The interaction ray hits her
    // CharacterController, or while she sits the mover's SeatedBody capsule (a child), and
    // finds this on her root either way.
    public sealed class GrandmaTalk : Interactable
    {
        public GrandmaBrain brain;

        void Awake()
        {
            if (brain == null) brain = GetComponent<GrandmaBrain>();
        }

        public override string Prompt => "Talk";

        public override bool CanInteract => brain != null && brain.CanTalk;

        public override void Interact(PlayerInteract by)
        {
            if (brain == null) return;
            CrewMember member = by != null ? CrewRoster.Owner(by.transform) : null;
            brain.OnTalk(member != null ? member.index : 0);
        }
    }
}
