using System;
using UnityEngine;

namespace Movers
{
    // The breakage facts destruction tells the rest of the game (WorldEvents, SLICE_ARCHITECTURE
    // section 5), raised from one place so every breakable says them the same way. Loudness is
    // always 0 here: the sound of the break is its own LoudNoise, raised by ImpactAudio.
    // Online, only the host raises them; the client hears them forwarded (NETCODE_SLICE 8), so
    // every method is a no-op there, a safety net behind the gates of the callers.
    internal static class DestructionEvents
    {
        // A movable reached its broken state (half pay).
        internal static void PropDamaged(MovableObject mo, float healthLeft01, int instigator)
        {
            if (!Net.HasAuthority) return;
            if (mo == null) return;
            Vector3 at = mo.transform.position;
            WorldEvents.Raise(WorldEventType.ObjectDamaged, at, instigator, 0f, healthLeft01, mo.contractValue, mo);
            if (mo.requiredForContract)
                WorldEvents.Raise(WorldEventType.ContractObjectDamaged, at, instigator, 0f, healthLeft01, mo.contractValue, mo);
        }

        // A movable shattered.
        internal static void PropDestroyed(MovableObject mo, int instigator)
        {
            if (!Net.HasAuthority) return;
            if (mo == null) return;
            Vector3 at = mo.transform.position;
            WorldEvents.Raise(WorldEventType.ObjectDestroyed, at, instigator, 0f, 0f, mo.contractValue, mo);
            if (mo.requiredForContract)
                WorldEvents.Raise(WorldEventType.ContractObjectDestroyed, at, instigator, 0f, 0f, mo.contractValue, mo);
        }

        // A wall, a wall module or a structural element changed state.
        internal static void Structure(UnityEngine.Object subject, Vector3 at, DestructionState state, int instigator)
        {
            if (!Net.HasAuthority) return;
            WorldEvents.Raise(WorldEventType.StructureDamaged, at, instigator, 0f, (float)state, 0, subject);
        }

        // Something fell because nothing held it up any more.
        internal static void Collapsed(UnityEngine.Object subject, Vector3 at, float massKg, int instigator)
        {
            if (!Net.HasAuthority) return;
            WorldEvents.Raise(WorldEventType.StructureCollapsed, at, instigator, 0f, massKg, 0, subject);
        }

        internal static void Window(GlassPane pane, Vector3 at, int instigator)
        {
            if (!Net.HasAuthority) return;
            WorldEvents.Raise(WorldEventType.WindowBroken, at, instigator, 0f, 0f, 0, pane);
        }

        internal static void Door(UnityEngine.Object subject, Vector3 at, int instigator)
        {
            if (!Net.HasAuthority) return;
            WorldEvents.Raise(WorldEventType.DoorBroken, at, instigator, 0f, 0f, 0, subject);
        }

        // The kit's own names: door walls are EXT_DOOR_*, INT_DOOR_*, GARAGE_DOOR_*, VERANDA_DOOR_*
        // (the prefixes HouseInteractionSetup uses), and leaves are *Door_Leaf*.
        internal static bool IsDoor(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return objectName.IndexOf("Door_Leaf", StringComparison.Ordinal) >= 0
                   || objectName.StartsWith("EXT_DOOR", StringComparison.Ordinal)
                   || objectName.StartsWith("INT_DOOR", StringComparison.Ordinal)
                   || objectName.StartsWith("GARAGE_DOOR", StringComparison.Ordinal)
                   || objectName.StartsWith("VERANDA_DOOR", StringComparison.Ordinal);
        }
    }
}
