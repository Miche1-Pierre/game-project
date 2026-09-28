using UnityEngine;

namespace Movers
{
    // Something the grandmother perceived. GrandmaSenses turns world events and what she sees
    // into these; GrandmaMood prices them (GrandmaMoodTable); GrandmaBrain decides what she does
    // about them. One list, so a playtest note ("she got angry too fast at the noise") maps to
    // one line of the table.
    public enum StimulusKind
    {
        SmallNoise,         // any audible impact, slam or shatter sound she hears
        ObjectDestroyed,    // one of her things shattered (not on the contract)
        ContractDamaged,    // a piece on the moving list got broken
        ContractDestroyed,  // a piece on the moving list shattered
        WindowBroken,
        DoorBroken,
        Explosion,
        Bumped,             // a player pushed into her, or hit her with something
        Smoking,            // a puff, in her sight
        Drinking,           // a swallow, in her sight
        TheftWitnessed,     // she saw one of her non-contract things pocketed, loaded or carried
        CarryingSeen,       // she keeps seeing one of her non-contract things carried about
        BehindSchedule,     // the house is not emptying fast enough
        SeatTaken,          // someone carried off the seat she was sitting in
        // DEV 2 (ADR-013)
        StructureBroken,    // a wall or a structural piece broke through, or part of the house came down
        GardenBroken,       // a hedge, a bush, the mailbox... (garden damage, never a wall)
        ObjectDamaged,      // one of her things cracked (not on the contract)
        RunOver,            // a vehicle hit her hard (GrandmaMover.Knock at runOverMinSpeed or more)
        Count
    }

    public struct Stimulus
    {
        public StimulusKind kind;
        public Vector3 position;
        public int instigator;       // see Actors; World when she cannot tell
        public bool seen;            // she saw it happen (as opposed to only hearing it)
        public bool inside;          // Explosion: under a roof
        public bool fragile;         // ObjectDestroyed: a fragile thing
        public bool collapse;        // StructureBroken: it came down for lack of support (not capped)
        public int value;            // money, when there is one
        public MovableObject item;   // the object concerned, may be null

        public Stimulus(StimulusKind kind, Vector3 position, int instigator)
        {
            this.kind = kind;
            this.position = position;
            this.instigator = instigator;
            seen = false;
            inside = false;
            fragile = false;
            collapse = false;
            value = 0;
            item = null;
        }

        // What she can do about it: react on the spot, go and look, or just turn her head.
        public bool IsOffence => kind == StimulusKind.TheftWitnessed || kind == StimulusKind.CarryingSeen
                              || kind == StimulusKind.Smoking || kind == StimulusKind.Drinking
                              || kind == StimulusKind.Bumped || kind == StimulusKind.SeatTaken
                              || kind == StimulusKind.RunOver;

        public bool IsBreakage => kind == StimulusKind.ObjectDestroyed || kind == StimulusKind.ContractDamaged
                               || kind == StimulusKind.ContractDestroyed || kind == StimulusKind.WindowBroken
                               || kind == StimulusKind.DoorBroken || kind == StimulusKind.Explosion
                               || kind == StimulusKind.StructureBroken || kind == StimulusKind.GardenBroken
                               || kind == StimulusKind.ObjectDamaged;

        // Under GrandmaMood's loss cap (maxLossPerWindow): noise and breakage. Offences always
        // pay full price, and so does part of the house coming down.
        public bool IsCapped => kind == StimulusKind.SmallNoise || (IsBreakage && !collapse);

        // Never dropped from her queue, however busy the frame: a blast, a theft, the truck.
        public bool MustBeHeard => kind == StimulusKind.Explosion || kind == StimulusKind.TheftWitnessed
                                || kind == StimulusKind.RunOver;
    }
}
