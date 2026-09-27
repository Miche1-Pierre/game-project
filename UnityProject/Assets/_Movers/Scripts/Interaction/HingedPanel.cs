using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A door leaf, a window sash, a garage door: anything that swings on one of its edges.
    //
    // Put it on the panel itself, in the editor, in its CLOSED pose. At startup it measures the
    // panel, finds the hinge edge, and slips an empty "<panel>_Hinge" object between the panel
    // and its parent, standing exactly on that edge. Opening is then one rotation of that object,
    // wherever the panel's own pivot is (kit pivots sit at the bottom centre, which is the wrong
    // place to swing a door from). A leaf exported with its pivot ON the hinge line (the window
    // sashes) says so with hingeAtPivot, and turns about that pivot instead: a sash 7 to 10 cm
    // thick hinged at the middle of its thickness digs its outer corner into the frame.
    //
    // The hinge object carries a kinematic Rigidbody moved with MoveRotation, so a door swung
    // into a chair shoves the chair and a garage door opening under a crate tips it over. It
    // does not shove the player: a CharacterController only moves when its own Move is called,
    // so a door closed on you passes through you, and you step out of it.
    //
    // The feel is decoupled from the damage (03_TECHNICAL/SLICE_ARCHITECTURE.md, A3 option B):
    //   - Snappy: the leaf cruises at `speed` and slows over the last `settleAngle` degrees down
    //     to `settleSpeed`, so it reads as a pushed door coming to rest. 95 degrees in 0.46 s.
    //   - Gentle: what it hits, it hits at the speed of its far edge. Before each physics step
    //     of a swing it looks ahead, one physics step at a time out to LookAheadSeconds, for
    //     something movable in the leaf's path. If there is, the rest of that swing runs at
    //     pushEdgeSpeed at the far edge: the door slows down and shoves the vase, it does not
    //     smash it. 1.8 m/s is under the 2.2 m/s at which glass starts to break. Whether a
    //     slammed door should break things is Pierre's open call (ADR-008): pushEdgeSpeed 0
    //     turns the brake off and the swing speed decides.
    //   - The hinge body uses speculative continuous collision: at 240 degrees per second the tip
    //     of a door moves 11 cm per physics step, enough to skip a cup or throw it violently out.
    //
    // A panel can belong to a HingedGroup (the two sashes of a window, a door and its frame).
    // Looking at it then shows the group's prompt and E works the whole group.
    //
    // A DoorLock next to it stops players (not SetOpen: the grandmother has keys). E on a shut
    // door always opens it when it is unlocked, key or no key; turning a key is the key's own
    // gesture (TurnKey, from KeyItem). Opening and closing raise DoorOpened / DoorClosed
    // (WindowOpened / WindowClosed for a window leaf) on WorldEvents, with who did it.
    //
    // Fixed things only. A panel under a simulated Rigidbody (a wardrobe door on a movable
    // wardrobe) would put one body inside another; that is refused with a warning.
    public class HingedPanel : Interactable
    {
        // Which edge of the panel, in the panel's own local space, carries the hinge.
        // Left = the -X edge, Right = the +X edge (both turn about the panel's up axis),
        // Top = the +Y edge (turns about the panel's right axis, like a garage door or a hatch).
        // With hingeAtPivot, only the axis is taken from it (up for Left and Right, right for
        // Top): the hinge line runs through the panel's origin, the free edge is the far side.
        public enum Hinge { Left, Right, Top }

        [Header("Hinge")]
        public Hinge hinge = Hinge.Left;
        [Tooltip("The panel's own origin is on the hinge line (leaves exported with their pivot on the hinge, like the window sashes).")]
        public bool hingeAtPivot = false;
        // Degrees from shut to open. The sign picks which way it swings, unless outwardHint is
        // set: then only the size is used and the way is worked out from the hint.
        public float openAngle = 90f;
        // Optional. The free edge swings towards this transform's forward (away from it if
        // swingAwayFromHint). A kit wall's forward is its outside, so the wall a door sits in
        // is the natural hint: doors open outwards, a garage door opens inwards (swing away).
        // Read once at startup, in the closed pose, so the panel itself is a valid hint too.
        public Transform outwardHint;
        public bool swingAwayFromHint = false;
        public bool startOpen = false;
        // A window leaf: raises WindowOpened / WindowClosed instead of the door events.
        public bool isWindow = false;

        [Header("Feel")]
        [Tooltip("Cruise speed, degrees per second. Far edge speed = reach x speed / 57 (m/s).")]
        public float speed = 240f;
        [Tooltip("Degrees before the stop over which the swing slows down. 0 = constant speed.")]
        public float settleAngle = 20f;
        [Tooltip("Degrees per second at the very end of the swing.")]
        public float settleSpeed = 60f;
        [Tooltip("Far edge speed (m/s) when something movable is in the way. Glass breaks above 2.2, china above 2.8. 0 = never brake (a slammed door hits at full speed).")]
        public float pushEdgeSpeed = 1.8f;

        [Header("Prompts")]
        public string promptOpen = "Open";
        public string promptClose = "Close";

        [Header("Sound")]
        // Played at the free edge when a closing panel arrives shut. Soft by default: a door
        // clicking shut, not a slam. A garage door wants Thud and more volume.
        public ImpactAudio.Kind shutSound = ImpactAudio.Kind.Wood;
        [Range(0f, 1f)] public float shutVolume = 0.35f;

        // How far ahead the brake looks, in seconds of swing at the current speed. Four physics
        // steps: enough to slow down before the contact, short enough not to brake for a chair
        // the leaf will never reach.
        const float LookAheadSeconds = 0.08f;
        const int MaxLookSamples = 8;
        // A rattled leaf moves its far edge this far (m) and back, three times in 0.3 s.
        const float RattleEdge = 0.025f;
        const float RattleSeconds = 0.3f;
        const float RattleShakes = 3f;
        const float RattleVolume = 0.5f;

        Transform pivot;
        Rigidbody pivotRb;
        Quaternion restLocal;       // the pivot's local rotation when the panel is shut
        Vector3 axis;               // hinge axis, in the pivot's own space
        Vector3 freeEdge;           // middle of the free edge, in the pivot's own space
        Bounds measured;            // the panel's box in its own local space, measured shut
        Vector3 boxOffset;          // centre of that box from the hinge, in the hinge's unscaled frame
        Vector3 boxHalf;            // half size of that box, in metres
        GlassPane[] glassOnly;      // the panes, when the panel is nothing but glass (a casement)
        float openSign = 1f;        // resolved from outwardHint when there is one
        bool hintResolved;          // openSign came from the hint: kept even if the hint is destroyed later
        float reach;                // metres from the hinge line to the farthest corner of the panel
        float angle;                // current opening in degrees, 0 = shut
        float lastSwingTime = float.NegativeInfinity;   // Time.fixedTime of the last step that turned it
        bool isOpen;
        int commandedBy = Actors.World;   // who set the current goal: the shut sound names them

        // The swing in progress: where it goes, whether it met something, how many steps so far.
        float swingGoal = float.NaN;
        bool braking;
        float brakeAngle = float.NaN;
        int swingSteps;

        float rattleEnd = -1f;      // Time.fixedTime at which the rattle stops, -1 when still
        float rattleDegrees;

        static readonly List<MeshFilter> filterBuffer = new List<MeshFilter>();
        static readonly List<Collider> colliderBuffer = new List<Collider>();
        static readonly List<MeshRenderer> rendererBuffer = new List<MeshRenderer>();
        static readonly Collider[] overlapBuffer = new Collider[32];

        public bool IsOpen => isOpen;
        public bool IsMoving => pivot != null && angle != (isOpen ? OpenAngleSigned : 0f);
        // The object that actually turns. null until the panel has been seated, and after a
        // failed setup.
        public Transform Pivot => pivot;

        // The opening right now, in degrees, signed like FullOpenAngle. 0 = shut.
        public float Angle => angle;
        // The signed opening when fully open. Its sign is the way the panel swings.
        public float FullOpenAngle => OpenAngleSigned;

        // Metres per second of the fastest point of the panel (its far edge) at cruise speed.
        // The peak of any swing: HingeCollisionRelay uses it to ignore the panel's own hits.
        public float EdgeSpeed => reach * Mathf.Max(1f, speed) * Mathf.Deg2Rad;

        // Seconds the last completed swing took (physics time), for tuning and tests.
        public float LastSwingSeconds { get; private set; }
        // The current or last swing met something movable and slowed to pushEdgeSpeed.
        public bool Braking => braking;
        // Where the leaf was (degrees, signed like Angle) when that swing started braking. NaN
        // when it did not. A test compares it with where the leaf would touch the object.
        public float BrakeAngle => brakeAngle;

        // The lock next to it, or null. Looked up on demand: a lock can be added after this
        // panel woke up (HouseInteractionSetup adds them in its Start).
        public DoorLock Lock => TryGetComponent(out DoorLock l) ? l : null;
        public bool IsLocked
        {
            get
            {
                DoorLock l = Lock;
                return l != null && l.IsLocked;
            }
        }

        // Turning now, or turned during the last physics step. The step after a swing ends can
        // still report a contact the swing made, so it counts too. Read by HingeCollisionRelay.
        internal bool SwungRecently => IsMoving || rattleEnd > 0f || Time.fixedTime - lastSwingTime <= Time.fixedDeltaTime * 1.5f;

        // The group this panel is worked through, or null. Set by HingedGroup itself.
        public HingedGroup Group { get; internal set; }

        // This panel on its own, group or no group: seated, switched on, and still in one piece.
        public bool CanSwing => pivot != null && isActiveAndEnabled && !IsWrecked;

        // A group that has been switched off hands the panel back its own prompt and key.
        HingedGroup ActiveGroup => Group != null && Group.isActiveAndEnabled ? Group : null;

        public override string Prompt => PromptFor(Viewer());

        public override string PromptFor(PlayerInteract by)
        {
            HingedGroup g = ActiveGroup;
            if (g != null) return g.PromptFor(by);
            CrewMember who = CrewOf(by);
            DoorVerb verb = VerbFor(who);
            // The key holder also reads what the key's own button does here: "Open   [F] Lock".
            if (verb == DoorVerb.Open)
            {
                DoorLock l = Lock;
                if (l != null && l.LockableBy(who)) return DoorLock.WithLockHint(promptOpen, who);
            }
            return DoorLock.Label(verb, promptOpen, promptClose);
        }

        public override bool CanInteract
        {
            get
            {
                HingedGroup g = ActiveGroup;
                return g != null ? g.CanInteract : CanSwing;
            }
        }

        public override void Interact(PlayerInteract by)
        {
            HingedGroup g = ActiveGroup;
            if (g != null) { g.Interact(by); return; }

            CrewMember who = CrewOf(by);
            int actor = ActorOf(who);
            switch (VerbFor(who))
            {
                case DoorVerb.Unlock: Lock.Unlock(actor); break;
                case DoorVerb.Locked: Rattle(actor); break;
                case DoorVerb.Close: SetOpen(false, actor); break;
                default: SetOpen(true, actor); break;
            }
        }

        // What E does on this panel alone, for this player (null: nobody in particular, no keys).
        // Never Lock: a shut, unlocked door opens for everyone, the key holder included, or the
        // one carrying the key could never walk through. Locking is TurnKey.
        public DoorVerb VerbFor(CrewMember who)
        {
            if (isOpen) return DoorVerb.Close;
            DoorLock l = Lock;
            if (l == null || !l.IsLocked) return DoorVerb.Open;
            return l.OpensFor(who) ? DoorVerb.Unlock : DoorVerb.Locked;
        }

        // The key in this player's hands, turned in this door (KeyItem, on the Alt button):
        // locks it when it is unlocked, unlocks it when it is locked. Only a shut door, and only
        // a key that fits. Goes through the group, like E. false: nothing for the key to do here.
        public bool TurnKey(CrewMember who)
        {
            HingedGroup g = ActiveGroup;
            if (g != null) return g.TurnKey(who);
            DoorLock l = Lock;
            if (l == null || isOpen || !CanSwing || !l.LockableBy(who)) return false;
            int actor = ActorOf(who);
            if (l.IsLocked) l.Unlock(actor);
            else l.Lock(actor);
            return true;
        }

        // A shattered pane, or a panel the destruction system has finished off, has nothing
        // left to swing. Looked up on demand rather than cached, because the destruction
        // components may be added to the panel after this one woke up. A casement is made of
        // panes and nothing else, so it is wrecked once the last of them has gone.
        public bool IsWrecked
        {
            get
            {
                if (TryGetComponent(out GlassPane glass) && glass.IsBroken) return true;
                if (TryGetComponent(out Breakable breakable) && breakable.IsDestroyed) return true;
                if (glassOnly != null)
                {
                    for (int i = 0; i < glassOnly.Length; i++)
                    {
                        GlassPane pane = glassOnly[i];
                        if (pane != null && !pane.IsBroken) return false;
                    }
                    return true;
                }
                return false;
            }
        }

        // The signed opening, live: the size follows the inspector while playing, the direction
        // was settled once from the hint. Latched rather than re-testing the hint, so a hint that
        // is destroyed later (the wall blown up, or the panel being its own hint) cannot flip the
        // panel back to the raw sign and swing it the other way, through the wall.
        float OpenAngleSigned => hintResolved ? openSign * Mathf.Abs(openAngle) : openAngle;

        void Awake()
        {
            // Checked before the hinge object exists, since that object is itself a Rigidbody.
            // A kinematic body on the panel is fine (it simply rides the hinge); a simulated one
            // above it would fight the hinge for the transform.
            var body = GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                Debug.LogWarning("[HingedPanel] " + name + " sits on a Rigidbody. Hinges are for fixed " +
                                 "panels (doors, windows, the garage), this one stays shut.");
                enabled = false;
                return;
            }
            isOpen = startOpen;
            Seat();
        }

        // For panels set up from code (HouseInteractionSetup). On a panel that is already
        // seated, this moves the hinge object to the edge asked for (set hingeAtPivot before
        // calling it). Called on an inactive object, before its Awake, it only stores the values
        // and Awake seats the hinge once. Null prompts keep the current ones.
        public void Configure(Hinge hinge, float openAngle, Transform outwardHint,
                              string promptOpen = null, string promptClose = null)
        {
            this.hinge = hinge;
            this.openAngle = openAngle;
            this.outwardHint = outwardHint;
            if (promptOpen != null) this.promptOpen = promptOpen;
            if (promptClose != null) this.promptClose = promptClose;
            if (pivot != null) Seat();
        }

        // Kept for older callers. A player goes through Interact, which knows who pressed.
        public void Toggle() { SetOpen(!isOpen, Actors.World); }
        public void Toggle(int instigator) { SetOpen(!isOpen, instigator); }

        // Reversing mid-swing is fine: the panel just turns back from wherever it is.
        //
        // Lock-agnostic on purpose. Every player path comes through Interact, with the player's
        // index; this bare form is how the grandmother works doors (she has the keys, and her
        // code was written against it), so it is recorded as hers. Anything else that opens a
        // door from code says who it is with the overload.
        public void SetOpen(bool open) { SetOpen(open, Actors.Grandma); }

        public void SetOpen(bool open, int instigator)
        {
            // Online, only the host works doors; the client applies DoorSync's Command.
            if (!Net.HasAuthority) return;
            if (!Command(open, instigator)) return;
            Announce(open, isWindow, EventPoint(), instigator, this);
        }

        // Sets the goal without a word on the bus: the group announces once for all its panels.
        // true when the goal changed. Ungated, like Jiggle: it is also the online client's apply
        // path for the host's doors (DoorSync).
        internal bool Command(bool open, int instigator = Actors.World)
        {
            if (IsWrecked) return false;
            bool changed = isOpen != open;
            isOpen = open;
            if (changed) commandedBy = instigator;
            return changed;
        }

        // A player tried the door and it is locked: the leaf jiggles in its frame and the latch
        // clacks. Heard, and seen from across the room, which is the point.
        public void Rattle(int instigator)
        {
            // The client jiggles from the replayed DoorLockedRattle instead (DoorSync).
            if (!Net.HasAuthority) return;
            if (!Jiggle(instigator)) return;
            DoorLock.AnnounceRattle(EventPoint(), instigator, this);
        }

        internal bool Jiggle(int instigator = Actors.World)
        {
            if (pivot == null || !CanSwing) return false;
            rattleDegrees = RattleEdge / Mathf.Max(0.2f, reach) * Mathf.Rad2Deg;
            rattleEnd = Time.fixedTime + RattleSeconds;
            Vector3 edge = pivot.TransformPoint(freeEdge);
            ImpactAudio.Play(ImpactAudio.Kind.Pin, edge, RattleVolume, instigator);
            ImpactAudio.Play(ImpactAudio.Kind.Thud, edge, RattleVolume * 0.6f, instigator);
            return true;
        }

        // Straight to open or shut, no swing, no sound: the online snapshot (DoorSync) applies
        // the host's doors this way, like Seat does at startup. Lock-agnostic and wreck-agnostic:
        // it copies the host's state as it is.
        internal void Snap(bool open)
        {
            isOpen = open;
            rattleEnd = -1f;
            braking = false;
            brakeAngle = float.NaN;
            if (pivot == null) return;
            angle = isOpen ? OpenAngleSigned : 0f;
            swingGoal = angle;
            pivot.localRotation = restLocal * Quaternion.AngleAxis(angle, axis);
            if (pivotRb != null) pivotRb.rotation = pivot.rotation;
        }

        internal static void Announce(bool open, bool window, Vector3 at, int instigator, Object subject)
        {
            WorldEventType type = window
                ? (open ? WorldEventType.WindowOpened : WorldEventType.WindowClosed)
                : (open ? WorldEventType.DoorOpened : WorldEventType.DoorClosed);
            WorldEvents.Raise(type, at, instigator, 0f, 0f, 0, subject);
        }

        // The middle of the panel when shut: where "the door" is, for the events.
        internal Vector3 EventPoint()
        {
            return TryGetClosedBounds(null, out Bounds b) ? b.center : transform.position;
        }

        // The panel's box in its SHUT pose, whatever pose it is in right now, in the local space
        // of `space` (world space when null). HingedGroup uses it to know where the window or
        // the door sits on the wall around it.
        public bool TryGetClosedBounds(Transform space, out Bounds bounds)
        {
            bounds = default;
            if (pivot == null || transform.parent != pivot) return false;

            Transform p = pivot.parent;
            Matrix4x4 shut = (p != null ? p.localToWorldMatrix : Matrix4x4.identity)
                           * Matrix4x4.TRS(pivot.localPosition, restLocal, pivot.localScale)
                           * Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
            if (space != null) shut = space.worldToLocalMatrix * shut;

            bool any = false;
            Encapsulate(ref bounds, ref any, shut, measured);
            return any;
        }

        // The panel's box, in world space, at a given opening (signed like FullOpenAngle): what
        // the brake tests ahead of the swing, and what a test checks against the wall.
        public bool TryGetSwingBox(float opening, out Vector3 centre, out Vector3 halfExtents, out Quaternion rotation)
        {
            centre = default;
            halfExtents = default;
            rotation = Quaternion.identity;
            if (pivot == null) return false;
            rotation = ParentRotation() * restLocal * Quaternion.AngleAxis(opening, axis);
            centre = pivot.position + rotation * boxOffset;
            halfExtents = boxHalf;
            return true;
        }

        void FixedUpdate()
        {
            if (pivot == null || pivotRb == null) return;

            float goal = isOpen ? OpenAngleSigned : 0f;
            bool rattling = rattleEnd > 0f;
            if (angle == goal && !rattling) return;   // at rest: nothing to do, which is almost always

            float dt = Time.fixedDeltaTime;
            bool arrivedShut = false;
            if (angle != goal)
            {
                if (goal != swingGoal)
                {
                    // A new swing (or a reversed one): it has met nothing yet.
                    swingGoal = goal;
                    braking = false;
                    brakeAngle = float.NaN;
                    swingSteps = 0;
                }
                angle = Mathf.MoveTowards(angle, goal, StepSpeed(goal) * dt);
                swingSteps++;
                lastSwingTime = Time.fixedTime;
                if (angle == goal)
                {
                    LastSwingSeconds = swingSteps * dt;
                    arrivedShut = !isOpen;
                }
            }

            float offset = 0f;
            if (rattling)
            {
                float left = rattleEnd - Time.fixedTime;
                if (left <= 0f) rattleEnd = -1f;   // this step puts the leaf back exactly
                else offset = RattleOffset(left);
            }

            pivotRb.MoveRotation(ParentRotation() * restLocal * Quaternion.AngleAxis(angle + offset, axis));

            if (arrivedShut)
                ImpactAudio.Play(shutSound, pivot.TransformPoint(freeEdge), shutVolume, commandedBy);
        }

        // Degrees per second for this step: cruise, settle near the stop, brake for what is in
        // the way. The brake is decided once per swing and holds until the swing ends or turns.
        float StepSpeed(float goal)
        {
            float v = Mathf.Max(1f, speed);
            float remaining = Mathf.Abs(goal - angle);
            if (settleAngle > 0f && remaining < settleAngle)
            {
                float slow = Mathf.Clamp(settleSpeed, 1f, v);
                v = Mathf.Lerp(slow, v, remaining / settleAngle);
            }

            if (pushEdgeSpeed > 0f && reach > 0.01f)
            {
                float push = Mathf.Max(1f, pushEdgeSpeed / reach * Mathf.Rad2Deg);
                if (!braking && v > push && SomethingAhead(goal, v))
                {
                    braking = true;
                    brakeAngle = angle;
                }
                if (braking && v > push) v = push;
            }
            return v;
        }

        // The path the leaf will sweep in the next LookAheadSeconds, looked at one physics step
        // apart, starting with the very next step. Each look is the leaf's box thickened by one
        // step of arc at the far edge (the thickness is the box's local z, by construction of
        // the hinge axes), so two looks in a row overlap and leave no gap between them: a cup
        // standing right behind a shut door is seen before the first step, not hit at cruise.
        // Near the hinge the thickening is more than the arc, which only brakes a little early.
        bool SomethingAhead(float goal, float v)
        {
            float dt = Time.fixedDeltaTime;
            float step = v * dt;
            int samples = Mathf.Clamp(Mathf.CeilToInt(LookAheadSeconds / dt - 0.001f), 1, MaxLookSamples);
            float pad = reach * step * Mathf.Deg2Rad;
            for (int i = 1; i <= samples; i++)
            {
                float opening = Mathf.MoveTowards(angle, goal, step * i);
                if (MovableIn(opening, pad)) return true;
                if (opening == goal) break;
            }
            return false;
        }

        // Something that physics moves (a dynamic body, awake or asleep) in the leaf's box at
        // this opening, thickened by `pad` metres on each face. The house is static and the crew
        // and the grandmother are character controllers, so only what could be shoved counts.
        bool MovableIn(float opening, float pad)
        {
            if (!TryGetSwingBox(opening, out Vector3 c, out Vector3 half, out Quaternion rot)) return false;
            half.z += pad;
            int n = Physics.OverlapBoxNonAlloc(c, half, overlapBuffer, rot, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                Rigidbody body = overlapBuffer[i].attachedRigidbody;
                overlapBuffer[i] = null;
                if (!found && body != null && !body.isKinematic && body != pivotRb) found = true;
            }
            return found;
        }

        // Towards the open side only: a shut leaf can give a little into the room, not through
        // its own frame. Dies away over the rattle.
        float RattleOffset(float left)
        {
            float t = 1f - left / RattleSeconds;
            float towardsOpen = OpenAngleSigned < 0f ? -1f : 1f;
            return Mathf.Abs(Mathf.Sin(t * Mathf.PI * RattleShakes)) * (1f - t) * rattleDegrees * towardsOpen;
        }

        Quaternion ParentRotation()
        {
            Transform p = pivot.parent;
            return p != null ? p.rotation : Quaternion.identity;
        }

        // Builds the hinge object on the hinge edge of the closed panel, or moves it there again
        // when Configure changes the edge. Safe to call more than once.
        void Seat()
        {
            Transform parent = transform.parent;
            if (pivot != null)
            {
                // Back to shut and off the old hinge, so the measurement sees the closed pose.
                pivot.localRotation = restLocal;
                parent = pivot.parent;
                transform.SetParent(parent, true);
            }

            Unbatch();
            FindGlassOnly();

            if (!TryMeasure(out Bounds b))
            {
                Debug.LogWarning("[HingedPanel] " + name + " has no mesh and no collider to measure, it cannot swing.");
                if (pivot != null) transform.SetParent(pivot, true);
                return;
            }
            measured = b;

            Vector3 c = b.center;
            Vector3 hingeLocal, freeLocal;
            if (hingeAtPivot)
            {
                // The hinge line runs through the origin; the free edge is the side of the box
                // farther from it, whichever way the leaf was modelled.
                hingeLocal = Vector3.zero;
                if (hinge == Hinge.Top)
                {
                    axis = Vector3.right;
                    freeLocal = new Vector3(c.x, Mathf.Abs(b.min.y) > Mathf.Abs(b.max.y) ? b.min.y : b.max.y, c.z);
                }
                else
                {
                    axis = Vector3.up;
                    freeLocal = new Vector3(Mathf.Abs(b.min.x) > Mathf.Abs(b.max.x) ? b.min.x : b.max.x, c.y, c.z);
                }
            }
            else
            {
                switch (hinge)
                {
                    case Hinge.Right:
                        hingeLocal = new Vector3(b.max.x, c.y, c.z);
                        freeLocal = new Vector3(b.min.x, c.y, c.z);
                        axis = Vector3.up;
                        break;
                    case Hinge.Top:
                        hingeLocal = new Vector3(c.x, b.max.y, c.z);
                        freeLocal = new Vector3(c.x, b.min.y, c.z);
                        axis = Vector3.right;
                        break;
                    default:
                        hingeLocal = new Vector3(b.min.x, c.y, c.z);
                        freeLocal = new Vector3(b.max.x, c.y, c.z);
                        axis = Vector3.up;
                        break;
                }
            }

            if (pivot == null)
            {
                var go = new GameObject(name + "_Hinge");
                go.layer = gameObject.layer;
                pivot = go.transform;
                pivot.SetParent(parent, false);
                // The body goes on, and turns kinematic, before the panel moves in: a body that
                // is not kinematic with a concave MeshCollider under it is an error in PhysX.
                pivotRb = go.AddComponent<Rigidbody>();
                pivotRb.isKinematic = true;
                pivotRb.useGravity = false;
                pivotRb.interpolation = RigidbodyInterpolation.Interpolate;
                // The only continuous mode a kinematic body has. A fast leaf would otherwise
                // step over a cup, or land inside it and fling it out.
                pivotRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                go.AddComponent<HingeCollisionRelay>().owner = this;
            }

            // Same rotation as the panel, so the panel's local axes are the hinge's local axes.
            pivot.SetPositionAndRotation(transform.TransformPoint(hingeLocal), transform.rotation);
            pivotRb.position = pivot.position;
            pivotRb.rotation = pivot.rotation;
            restLocal = pivot.localRotation;
            freeEdge = pivot.InverseTransformPoint(transform.TransformPoint(freeLocal));
            reach = MeasureReach(b);
            // The box the brake sweeps ahead: the measured box, kept relative to the hinge in
            // metres, so it can be posed at any opening without touching the transforms.
            boxOffset = Quaternion.Inverse(pivot.rotation) * (transform.TransformPoint(b.center) - pivot.position);
            Vector3 s = transform.lossyScale;
            boxHalf = Vector3.Scale(b.extents, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            transform.SetParent(pivot, true);

            ResolveSign();

            // Straight to the current state, no swing: a window that starts open is just open.
            angle = isOpen ? OpenAngleSigned : 0f;
            swingGoal = angle;
            pivot.localRotation = restLocal * Quaternion.AngleAxis(angle, axis);
            pivotRb.rotation = pivot.rotation;
        }

        // Of the two ways to swing by |openAngle|, keep the one that takes the free edge further
        // along the hint. Worked out by trying both rather than from a handedness rule, so it
        // holds for any hinge, any panel rotation and mirrored panels alike.
        void ResolveSign()
        {
            openSign = openAngle < 0f ? -1f : 1f;
            hintResolved = false;
            if (outwardHint == null) return;
            float size = Mathf.Abs(openAngle);
            if (size < 0.01f) return;

            Vector3 towards = outwardHint.forward * (swingAwayFromHint ? -1f : 1f);
            Quaternion shut = ParentRotation() * restLocal;
            Vector3 plus = shut * (Quaternion.AngleAxis(size, axis) * freeEdge);
            Vector3 minus = shut * (Quaternion.AngleAxis(-size, axis) * freeEdge);
            openSign = Vector3.Dot(plus, towards) >= Vector3.Dot(minus, towards) ? 1f : -1f;
            hintResolved = true;
        }

        // How far the farthest corner of the (shut) panel is from the hinge line, in world metres.
        // Times the angular speed, that is the fastest the panel ever hits anything.
        float MeasureReach(Bounds local)
        {
            Vector3 origin = pivot.position;
            Vector3 line = pivot.TransformDirection(axis);
            float best = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 r = transform.TransformPoint(Corner(local, i)) - origin;
                r -= line * Vector3.Dot(r, line);
                best = Mathf.Max(best, r.magnitude);
            }
            return best;
        }

        // Is every visible part of this panel a pane of glass? Then it is a casement, and the
        // panes are kept so IsWrecked can tell when the last one has gone. A door with a glass
        // window in it is not glass only: it keeps swinging with its window broken, and so does
        // a sash (a wooden frame with panes in it).
        void FindGlassOnly()
        {
            glassOnly = null;
            int glass = 0, other = 0;
            GetComponentsInChildren(false, rendererBuffer);
            for (int i = 0; i < rendererBuffer.Count; i++)
            {
                MeshRenderer mr = rendererBuffer[i];
                if (mr == null || !mr.enabled) continue;
                if (mr.TryGetComponent(out GlassPane _)) glass++;
                else other++;
            }
            rendererBuffer.Clear();
            if (glass > 0 && other == 0) glassOnly = GetComponentsInChildren<GlassPane>(false);
        }

        // The panel's bounds in its own local space, from every active mesh under it. Mesh.bounds
        // is available even when the mesh is not readable, so FBX imports need no Read/Write.
        // No mesh at all (an invisible stand-in): the colliders are measured instead.
        bool TryMeasure(out Bounds local)
        {
            local = default;
            bool any = false;
            Matrix4x4 toLocal = transform.worldToLocalMatrix;

            GetComponentsInChildren(false, filterBuffer);
            for (int i = 0; i < filterBuffer.Count; i++)
            {
                MeshFilter mf = filterBuffer[i];
                if (mf == null || mf.sharedMesh == null) continue;
                // A statically batched filter holds the combined mesh of half the house.
                if (mf.TryGetComponent(out MeshRenderer mr) && mr.isPartOfStaticBatch) continue;
                Encapsulate(ref local, ref any, toLocal * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
            }
            filterBuffer.Clear();
            if (any) return true;

            GetComponentsInChildren(false, colliderBuffer);
            for (int i = 0; i < colliderBuffer.Count; i++)
            {
                Collider col = colliderBuffer[i];
                if (col == null || !col.enabled) continue;
                Encapsulate(ref local, ref any, toLocal, col.bounds);   // world box, into local
            }
            colliderBuffer.Clear();
            return any;
        }

        static void Encapsulate(ref Bounds into, ref bool any, Matrix4x4 m, Bounds b)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(Corner(b, i));
                if (!any) { into = new Bounds(p, Vector3.zero); any = true; }
                else into.Encapsulate(p);
            }
        }

        // Corner i (0 to 7) of a box.
        static Vector3 Corner(Bounds b, int i)
        {
            Vector3 c = b.center, e = b.extents;
            return new Vector3(c.x + ((i & 1) == 0 ? -e.x : e.x),
                               c.y + ((i & 2) == 0 ? -e.y : e.y),
                               c.z + ((i & 4) == 0 ? -e.z : e.z));
        }

        // Kit pieces are marked Static, and static batching bakes them into one combined mesh
        // when the scene loads. A baked renderer is drawn where it was baked, forever: turn its
        // transform and the collider follows while the picture stays behind. So a baked panel
        // gets a plain copy of its renderer (the original mesh is still on its MeshCollider,
        // which batching never touches) and the baked one is switched off. The clean fix is to
        // untick Static on the panel in the editor, which the warning says.
        void Unbatch()
        {
            GetComponentsInChildren(false, rendererBuffer);
            for (int i = 0; i < rendererBuffer.Count; i++)
            {
                MeshRenderer mr = rendererBuffer[i];
                if (mr == null || !mr.enabled || !mr.isPartOfStaticBatch) continue;

                Mesh source = mr.TryGetComponent(out MeshCollider mc) ? mc.sharedMesh : null;
                if (source == null)
                {
                    Debug.LogWarning("[HingedPanel] " + mr.name + " is statically batched and has no mesh " +
                                     "to copy: it will not move. Untick Static on it in the editor.");
                    continue;
                }

                var copy = new GameObject(mr.name + "_Swing");
                copy.layer = mr.gameObject.layer;
                copy.transform.SetParent(mr.transform, false);
                copy.AddComponent<MeshFilter>().sharedMesh = source;
                var r = copy.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                r.shadowCastingMode = mr.shadowCastingMode;
                r.receiveShadows = mr.receiveShadows;
                mr.enabled = false;
                Debug.LogWarning("[HingedPanel] " + mr.name + " was statically batched, drew a movable copy. " +
                                 "Untick Static on it in the editor.");
            }
            rendererBuffer.Clear();
        }

        // No cleanup of the hinge object on purpose. When the panel shatters, the hinge it leaves
        // behind is an empty kinematic body with no collider, which costs nothing. Destroying it
        // from here would also destroy the panel whenever only this component is removed.
    }
}
