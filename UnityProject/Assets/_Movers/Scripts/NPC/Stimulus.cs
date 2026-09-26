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
            value = 0;
            item = null;
        }

        // What she can do about it: react on the spot, go and look, or just turn her head.
        public bool IsOffence => kind == StimulusKind.TheftWitnessed || kind == StimulusKind.CarryingSeen
                              || kind == StimulusKind.Smoking || kind == StimulusKind.Drinking
                              || kind == StimulusKind.Bumped || kind == StimulusKind.SeatTaken;

        public bool IsBreakage => kind == StimulusKind.ObjectDestroyed || kind == StimulusKind.ContractDamaged
                               || kind == StimulusKind.ContractDestroyed || kind == StimulusKind.WindowBroken
                               || kind == StimulusKind.DoorBroken || kind == StimulusKind.Explosion;
    }
}
