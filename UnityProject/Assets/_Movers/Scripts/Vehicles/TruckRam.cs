using System;
using UnityEngine;

namespace Movers
{
    // The truck as a ram (03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md 6.4): a look-ahead sweep, host
    // only, that applies the collision's energy to what is in the truck's path before the physics
    // contact, so a fence or a wall that gives way lets the truck through, and flings small props
    // out of the way. Lives inside TruckVehicle, like CrewBumper.
    [Serializable]
    public sealed class TruckRam
    {
        // True: the ram sweep hit or flung this collider recently, so its own collision callback
        // must not count the same hit again (ImpactDamage.TryMeasure).
        public static bool Handled(Collider c) => false;
    }
}
