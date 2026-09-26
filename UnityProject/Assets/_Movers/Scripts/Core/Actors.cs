namespace Movers
{
    // Who did something, as a plain int. Players are 0..3 (their CrewMember.index). Anything
    // that is not a player uses one of the constants below. Events and damage records carry
    // this instead of an object reference, so "who broke the window" survives the thing being
    // destroyed, and a future netcode can send it as it is.
    public static class Actors
    {
        public const int World = -1;      // nobody in particular: physics, an ownerless chain reaction
        public const int Grandma = 100;   // the owner of the house

        public static bool IsPlayer(int actor) => actor >= 0 && actor < 16;

        public static string Name(int actor)
        {
            if (IsPlayer(actor)) return "P" + (actor + 1);
            if (actor == Grandma) return "Grandma";
            return "world";
        }
    }
}
