using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Facts about the world that more than one system cares about. A publisher says what
    // happened ("a window broke here, P2 did it"); each listener decides what that means for it:
    // the grandmother loses patience, the ledger records a theft, the HUD shows a toast, the log
    // keeps a line. Nobody holds a reference to anybody (03_TECHNICAL/EVENT_SYSTEM.md).
    //
    // Keep it coarse: an event per thing a player or the grandmother would notice, never per
    // frame. Meaning of magnitude and value per type is written next to each entry.
    public enum WorldEventType
    {
        // Sound. loudness says how far it carries (WorldEvents.HearingRadius).
        LoudNoise,              // magnitude unused. Raised for every audible impact, shatter, slam
        Explosion,              // magnitude = blast radius (m). loudness 1

        // Breakage. subject = the MovableObject, GlassPane, HingedPanel or wall module.
        ObjectDamaged,          // a movable reached its broken state (half pay). magnitude = health fraction left
        ObjectDestroyed,        // a movable shattered
        StructureDamaged,       // a wall or element changed state. magnitude = new DestructionState as float
        StructureCollapsed,     // a piece fell for lack of support. magnitude = its mass (kg)
        WindowBroken,           // a pane shattered
        DoorBroken,             // a door leaf or a door wall shattered
        ContractObjectDamaged,  // a required object reached its broken state. value = its contract value
        ContractObjectDestroyed,// a required object shattered. value = its contract value

        // Handling. subject = the MovableObject, instigator = the player.
        ObjectPickedUp,
        ObjectReleased,         // dropped
        ObjectThrown,           // magnitude = release speed (m/s)
        FurnitureMoved,         // released more than 0.5 m from where it was picked up. magnitude = metres
        ItemPocketed,           // value = the item's value
        ItemUnpocketed,
        CargoLoaded,            // entered the truck bed. value = the item's value
        CargoUnloaded,

        // Doors and windows. subject = the HingedPanel or HingedGroup.
        DoorOpened, DoorClosed,
        DoorLockedRattle,       // someone tried a locked door
        DoorUnlocked,
        WindowOpened, WindowClosed,

        // Theft and the owner.
        ItemStolen,             // recorded by the ledger. value = its value, magnitude = 1 if witnessed
        TheftWitnessed,         // the grandmother saw it. value = its value
        KeysHandedOver,         // instigator = the player who received them
        GrandmaNoticed,         // she saw or heard something. magnitude = how bad (patience lost)
        GrandmaMoodChanged,     // magnitude = patience 0..100 after the change
        GrandmaCalledPolice,    // her patience ran out: the session fails
        GrandmaBumped,          // a player walked or pushed into her, or hit her with something

        // The session.
        SessionStateChanged,    // magnitude = (float)SessionState
        ContractDelivered,      // value = the payout

        // Players.
        PlayerKnockedDown,      // an explosion threw them. magnitude = shove speed (m/s)
        PlayerSmoking,          // a puff. subject = the cigarette
        PlayerDrinking,         // a swallow. subject = the beer
    }

    public readonly struct WorldEvent
    {
        public readonly WorldEventType type;
        public readonly Vector3 position;
        public readonly int instigator;           // see Actors
        public readonly float loudness;           // 0..1, 0 = silent
        public readonly float magnitude;          // per type, see WorldEventType
        public readonly int value;                // money, per type
        public readonly UnityEngine.Object subject; // local only, may be null or already destroyed
        public readonly float time;               // Time.time when raised

        public WorldEvent(WorldEventType type, Vector3 position, int instigator = Actors.World,
                          float loudness = 0f, float magnitude = 0f, int value = 0,
                          UnityEngine.Object subject = null)
        {
            this.type = type;
            this.position = position;
            this.instigator = instigator;
            this.loudness = loudness;
            this.magnitude = magnitude;
            this.value = value;
            this.subject = subject;
            this.time = Time.time;
        }

        public MovableObject Item => subject as MovableObject;

        public override string ToString()
        {
            string what = subject != null ? subject.name : "-";
            return string.Format("{0:0.0}s {1} by {2} at ({3:0.0},{4:0.0},{5:0.0}) {6}{7}{8}",
                time, type, Actors.Name(instigator), position.x, position.y, position.z, what,
                value != 0 ? " $" + value : "",
                loudness > 0f ? " loud " + loudness.ToString("0.00") : "");
        }
    }

    public static class WorldEvents
    {
        // Loudness 1 (a grenade) is heard 30 m away in the open. Walls and floors in between are
        // the listener's business: the grandmother dims what she hears through them.
        public const float FullHearingRadius = 30f;
        public static float HearingRadius(float loudness) => Mathf.Max(0f, loudness) * FullHearingRadius;

        static readonly List<Action<WorldEvent>> listeners = new List<Action<WorldEvent>>();
        static Action<WorldEvent>[] snapshot = new Action<WorldEvent>[0];
        static bool dirty;
        static int depth;

        // The last events, newest first, for the debug log and the HUD toasts.
        const int RecentSize = 64;
        static readonly WorldEvent[] recent = new WorldEvent[RecentSize];
        static int recentHead, recentCount;

        public static int RecentCount => recentCount;
        public static WorldEvent GetRecent(int i)   // 0 = newest
        {
            int idx = (recentHead - 1 - i + RecentSize * 2) % RecentSize;
            return recent[idx];
        }

        // Every subscriber unsubscribes in OnDisable or OnDestroy: a scene reload (the debug
        // reset) must not leave dead listeners behind.
        public static void Subscribe(Action<WorldEvent> listener)
        {
            if (listener == null || listeners.Contains(listener)) return;
            listeners.Add(listener);
            dirty = true;
        }

        public static void Unsubscribe(Action<WorldEvent> listener)
        {
            if (listeners.Remove(listener)) dirty = true;
        }

        public static void Raise(in WorldEvent e)
        {
            recent[recentHead] = e;
            recentHead = (recentHead + 1) % RecentSize;
            if (recentCount < RecentSize) recentCount++;

            // A listener may raise another event (a broken door is also a noise): allowed, but
            // a loop is a bug, and it must not take the game down with it.
            if (depth > 16)
            {
                Debug.LogWarning("WorldEvents: nesting deeper than 16, dropped " + e.type);
                return;
            }
            if (Net.IsOnline) WorldEventRelay.OnRaised(e);   // online co-op: forward in raise order (NETCODE_SLICE 8)
            if (dirty)
            {
                snapshot = listeners.ToArray();
                dirty = false;
            }
            var list = snapshot;   // a subscribe or unsubscribe during the loop takes effect next time
            depth++;
            try
            {
                for (int i = 0; i < list.Length; i++)
                {
                    try { list[i](e); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
            finally { depth--; }
        }

        public static void Raise(WorldEventType type, Vector3 position, int instigator = Actors.World,
                                 float loudness = 0f, float magnitude = 0f, int value = 0,
                                 UnityEngine.Object subject = null)
        {
            Raise(new WorldEvent(type, position, instigator, loudness, magnitude, value, subject));
        }

        // Play mode with domain reload off keeps statics between runs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            listeners.Clear();
            snapshot = new Action<WorldEvent>[0];
            dirty = false;
            depth = 0;
            recentHead = 0;
            recentCount = 0;
        }
    }
}
