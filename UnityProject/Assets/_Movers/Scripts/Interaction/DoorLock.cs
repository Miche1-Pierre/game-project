using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // What the action key does on a door, for one player. The prompt and the action both come
    // from it, so the words under the crosshair can never promise something E does not do.
    // Lock is never E's: it is the key's own gesture (KeyItem), shown after "Open".
    public enum DoorVerb { Open, Close, Unlock, Lock, Locked }

    // A lock on a hinged leaf (put it next to the HingedPanel). The house starts locked while
    // the grandmother has not handed the keys over (ADR-009): the crew arrives, rattles the
    // front door, and has to go and talk to her. Or break a window, which is the other story.
    //
    // A lock only stops players. HingedPanel.SetOpen stays lock-agnostic: the grandmother has
    // her own keys, and code that opens a door (her, a script) means it. A shut, locked leaf
    // answers E with a rattle; a player carrying a KeyItem with the same keyId (in the hands
    // or in a pocket) gets "Unlock" instead. E on a shut, unlocked door is always "Open", key
    // or no key. Locking takes the key in the hands and its own button (Alt, F on the
    // keyboard): the prompt then reads "Open   [F] Lock". The hands are the deliberate
    // gesture: a key in the pocket never locks anything behind your back.
    //
    // The house opens once the run is on, however it started: KeysHandedOver unlocks it with
    // the player who got the keys, and the session reaching InProgress unlocks whatever is
    // still locked (a break-in starts the run without keys; the sofa still has to leave by
    // the front door). Only locks on the house key: a shed with its own key stays shut.
    //
    // A wrecked leaf is open for good: a lock on a door that is not there locks nothing.
    //
    // HouseInteractionSetup puts one on every exterior door (keyId "house"). Place one by hand
    // for anything else: a shed with its own key, a bedroom she keeps shut.
    [DisallowMultipleComponent]
    public sealed class DoorLock : MonoBehaviour
    {
        // The key the grandmother hands over: KeysHandedOver opens every lock that takes it.
        public const string HouseKey = "house";

        public const string PromptLocked = "Locked";
        public const string PromptUnlock = "Unlock";
        public const string PromptLock = "Lock";

        // The Alt button's name, as PLAYER's ButtonLabels writes it (F on the keyboard, Y on a
        // pad). Repeated here because that table is not a contract; keep the two in step.
        const string KeyboardAlt = "F";
        const string PadAlt = "Y";

        // How far the rattle of a locked door carries (WorldEvents.HearingRadius: 9 m). Trying
        // a locked door is meant to be heard from the porch.
        const float RattleLoudness = 0.3f;
        const float LatchVolume = 0.35f;

        public string keyId = HouseKey;
        // Locked whatever the session. Without it, the lock is still shut at the start of a
        // session that begins with the intro (Session.State == Intro when this Start runs).
        public bool lockedAtStart = false;

        // The live state. Set at Start from lockedAtStart and the session, then by keys.
        [System.NonSerialized] public bool locked;

        HingedPanel panel;

        static readonly List<DoorLock> all = new List<DoorLock>();

        // The last "Open   [F] Lock" built, so a prompt read every frame is not a new string
        // every frame (PlayerInteract rebuilds its label whenever the string changes).
        static string hintVerb, hintButton, hintText;

        // Every lock in the scene, enabled or not.
        public static IReadOnlyList<DoorLock> All => all;

        public HingedPanel Panel => panel;

        // Locked and still there to be locked.
        public bool IsLocked => locked && (panel == null || !panel.IsWrecked);

        void Awake()
        {
            panel = GetComponent<HingedPanel>();
            if (panel == null)
                Debug.LogWarning("[DoorLock] " + name + " has no HingedPanel next to it: it locks nothing. " +
                                 "Put the lock on the leaf itself.");
            if (!all.Contains(this)) all.Add(this);
        }

        void Start()
        {
            // Start, not Awake: GameSession has set the session up by now, whatever the order.
            if (lockedAtStart || Session.State == SessionState.Intro) locked = true;
        }

        void OnEnable() { WorldEvents.Subscribe(OnWorldEvent); }
        void OnDisable() { WorldEvents.Unsubscribe(OnWorldEvent); }
        void OnDestroy() { all.Remove(this); }

        void OnWorldEvent(WorldEvent e)
        {
            if (keyId != HouseKey) return;
            switch (e.type)
            {
                // The grandmother gives the house keys to one player; the house opens for the crew.
                case WorldEventType.KeysHandedOver:
                    Unlock(e.instigator);
                    break;
                // InProgress, not ContractStarted: GameSession raises ContractStarted from inside
                // KeysHandedOver, possibly before this lock has heard the keys, and the unlock
                // must be the player's. By InProgress (the next frame) only a run started without keys (a
                // break-in) still finds the house locked. Nobody turned a key: the world did it.
                case WorldEventType.SessionStateChanged:
                    if (Mathf.RoundToInt(e.magnitude) == (int)SessionState.InProgress) Unlock(Actors.World);
                    break;
            }
        }

        // This player has a key for it, in the hands or on the body.
        public bool OpensFor(CrewMember who) => who != null && KeyItem.Carries(who, keyId);

        // This player holds a key for it in the hands: the gesture that can lock it.
        public bool LockableBy(CrewMember who) => who != null && KeyItem.Holds(who, keyId);

        public void Unlock(int instigator)
        {
            if (!locked) return;
            locked = false;
            Vector3 at = EventPoint();
            ImpactAudio.Play(ImpactAudio.Kind.Pin, at, LatchVolume, instigator);
            WorldEvents.Raise(WorldEventType.DoorUnlocked, at, instigator, 0f, 0f, 0, panel != null ? (Object)panel : this);
        }

        // No event: the bus has no DoorLocked (the contracts are frozen). The latch is heard,
        // and ImpactAudio turns that into a LoudNoise for anyone listening.
        public void Lock(int instigator)
        {
            if (locked) return;
            locked = true;
            ImpactAudio.Play(ImpactAudio.Kind.Pin, EventPoint(), LatchVolume, instigator);
        }

        // The door was tried and did not give. The leaf jiggles in its frame (HingedPanel does
        // that), the latch clacks, and the grandmother may hear it.
        internal static void AnnounceRattle(Vector3 at, int instigator, Object subject)
        {
            WorldEvents.Raise(WorldEventType.DoorLockedRattle, at, instigator, RattleLoudness, 0f, 0, subject);
        }

        Vector3 EventPoint()
        {
            if (panel != null && panel.TryGetClosedBounds(null, out Bounds b)) return b.center;
            return transform.position;
        }

        // ---- the whole house at once (the handover, the debug key) ----

        public static int UnlockAll(string keyId, int instigator)
        {
            int n = 0;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                DoorLock l = all[i];
                if (l == null || l.keyId != keyId || !l.locked) continue;
                l.Unlock(instigator);
                n++;
            }
            return n;
        }

        public static int LockAll(string keyId, int instigator)
        {
            int n = 0;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                DoorLock l = all[i];
                if (l == null || l.keyId != keyId || l.locked) continue;
                l.Lock(instigator);
                n++;
            }
            return n;
        }

        public static bool AnyLocked(string keyId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                DoorLock l = all[i];
                if (l != null && l.keyId == keyId && l.IsLocked) return true;
            }
            return false;
        }

        public static string Label(DoorVerb verb, string promptOpen, string promptClose)
        {
            switch (verb)
            {
                case DoorVerb.Close: return promptClose;
                case DoorVerb.Unlock: return PromptUnlock;
                case DoorVerb.Lock: return PromptLock;
                case DoorVerb.Locked: return PromptLocked;
                default: return promptOpen;
            }
        }

        // "Open   [F] Lock": E's verb, then the key's own button, for the player holding a key
        // that fits this shut, unlocked door.
        public static string WithLockHint(string verb, CrewMember who)
        {
            bool pad = who != null && who.Input != null && !(who.Input.Source is KeyboardMouseSource);
            string button = pad ? PadAlt : KeyboardAlt;
            if (!ReferenceEquals(verb, hintVerb) || !ReferenceEquals(button, hintButton))
            {
                hintVerb = verb;
                hintButton = button;
                hintText = verb + "   [" + button + "] " + PromptLock;
            }
            return hintText;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all.Clear();
            hintVerb = hintButton = hintText = null;
        }
    }
}
