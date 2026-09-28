using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Physics grab: the object stays a rigidbody and is dragged toward a hold point.
    // Heavy objects lag and resist (weight matters). This unwieldy feel IS the game.
    //
    // Driven by this player's CrewInput. One object has one carrier (MovableObject.holder):
    // a second player cannot take what the first is holding. What cannot be lifted (canCarry
    // off, or heavier than carryLimitKg) is dragged along the floor instead.
    //
    // What you lift is in your arms (hot-fix of 2026-09-27): held where the hands can hold it,
    // by holds worked out from its size (CarryGrip), and never further than maxHoldDistance.
    public class PlayerGrab : MonoBehaviour
    {
        public Transform cam;
        public PlayerController controller;

        public float grabRange = 3f;
        public float followStrength = 10f;   // lowered from 14: more lag reads as more weight
        public float maxSpeed = 9f;
        public float throwForce = 6f;
        public float maxSoloWeight = 60f; // above this the object gets very sluggish (needs 2 players, later)

        [Header("Weight feel")]
        // Playtest 2026-09-17: the carry read as slightly too floaty. The cause is that
        // FixedUpdate assigns linearVelocity outright, which cancels gravity and inertia, so
        // the object hangs at eye level with no mass. Rather than rewrite the control model
        // (the carry itself was judged correct), heavy objects now hang lower and lag more.
        public float sagPerKg = 0.006f;        // hold point drops this much per kg
        public float maxSag = 0.7f;            // a fridge ends up near the floor, not through it
        public float carriedLinearDamping = 6f;
        public float carriedAngularDamping = 2.5f;  // was 6, which froze all sway

        [Header("Reach: in the arms")]
        // Hot-fix of 2026-09-27: things floated between the hands. The wheel slid them 1 to 3.2 m
        // out while the arms end at 0.6 m, so the hands stopped in the air short of them. What you
        // carry is now in your arms: at chest height, its holds (CarryGrip) where the hands are,
        // and the wheel bends or stretches the arms instead of sliding it down a rail. A small thing
        // rides about 0.5 m out, a sofa on end about 1.3 m, and no centre goes further than
        // maxHoldDistance from the eyes. Holding rotate still gives the wheel to the roll.
        public float maxHoldDistance = 1.5f;
        public float holdDrop = 0.22f;             // how far under the eyes things ride, before their sag
        public float seeOverClearance = 0.08f;     // a tall thing rides low enough to see over, when the floor allows
        public float bentGripDepth = 0.15f;        // the holds' depth in front of the eyes, arms bent
        public float stretchedGripDepth = 0.42f;   // and arms stretched out
        public float nearClearance = 0.12f;        // its near face stays this far in front of the eyes
        [Range(0f, 1f)] public float startExtension = 0.5f;   // 0 arms bent, 1 stretched: how a session starts
        public float extensionSensitivity = 3f;    // per scroll unit: a notch (about 0.1) is 0.3 of the way
        // Heavy things cannot be held at arm's length: the stretch closes in as weight rises, on the
        // same weight factor that makes them lag and turn slowly.
        [Range(0f, 1f)] public float heavyExtensionMax = 0.35f;
        public float oneHandSide = 0.12f;          // a thing in the right hand alone rides to the right
        // In your arms it goes where you go: the share of your own velocity it keeps up with by
        // itself, from the heaviest (whose lag still reads as weight) to the lightest (all of it).
        [Range(0f, 1f)] public float heavyFollowThrough = 0.6f;
        // Left past the arms' reach (it lagged on a turn, or caught on a door frame), it is hauled
        // back this much harder, in m/s per metre over, so the hands stay on it.
        public float leashStrength = 12f;
        // Pushed back toward you by this much (it met a wall, a door, the truck), it stops you
        // walking on into it: your own capsule passes through what you carry, so nothing else would,
        // and the eyes would end up inside it.
        public float jamDistance = 0.15f;
        // Caught on something (a door frame, the stair rail) and left this far past the arms'
        // reach for gripLossSeconds, you lose your grip, as a drag does (dragSlack), instead of
        // holding it metres away. A thing just grabbed from across the room is given time to come in.
        public float carrySlack = 0.35f;
        public float gripLossSeconds = 0.3f;

        [Header("Reach: small usables")]
        // The cigarette, the beer and the grenade are drawn in the right hand (HeldPose) while
        // their body stays out on the old rail, now capped like everything else: the smoke clouds
        // form past its tip (ADR-005, where 1 m out was measured never to blind the smoker) and a
        // grenade leaves from there.
        public float usableMinReach = 1.0f;
        public float usableStartReach = 1.3f;
        public float reachSensitivity = 3.5f;  // a scroll notch is about 0.1, so this is ~0.35 m per notch

        [Header("Rotate the held object")]
        // GREYBOX_SPEC names one emergent problem: the sofa is wider than the interior door.
        // With no way to turn it, that problem has exactly one answer (walk around), which is
        // not a problem, it is a wall. Holding rotate (R, LB on a pad) hands the look to the object.
        public float rotateSensitivity = 3.5f;   // degrees per unit of look delta
        public float rollSensitivity = 300f;     // a scroll notch is about 0.1, so this is ~30 deg per notch
        // No 90 degree snap key on purpose. The obvious pair, Q and E, cannot be used: E is
        // the action key, so a quarter turn at the truck could deliver the contract by accident.
        // The free turn below is enough to clear a door. Revisit if it reads fiddly.
        public float rotateStrength = 14f;       // how hard the object is driven to its target orientation
        public float maxAngularSpeed = 6f;       // rad/s, before the weight factor
        // false restores the pre-rotation carry: the object just hangs and sways freely.
        // Kept as a switch because the free-hanging carry is what playtest 001 validated.
        public bool holdOrientation = true;

        [Header("Use what you carry")]
        // Under this many seconds the right button is a throw, over it, it is a use. Long
        // enough not to fire on a normal click, short enough that holding feels immediate.
        public float useHoldThreshold = 0.18f;

        [Header("Too heavy to lift: drag")]
        // Above this the grab drags instead of lifting, like an object with canCarry off. The
        // heaviest thing in the house today is 180 kg, so nothing carried today turns into a
        // drag until someone decides where the line is (a design question, see the report).
        public float carryLimitKg = 250f;
        // The drag pulls the object's centre toward a point on the floor in front of you, held at
        // the distance it had when you took it, never lifting it: gravity and friction stay the
        // object's. Weaker and slower than the carry, and heavier is slower still.
        public float dragFollowStrength = 6f;
        public float dragMaxSpeed = 2.5f;
        public float dragFullSpeedKg = 60f;      // at or under this, the drag is at full strength
        // You walk slower pulling a wardrobe than carrying a chair: the walk is capped at this
        // share of walkSpeed while dragging (sprint still multiplies it).
        public float dragWalkMultiplier = 0.35f;
        // Left further behind than this (it caught on a door frame, or you sprinted off), you
        // lose your grip on it instead of pulling it through the wall.
        public float dragSlack = 1.0f;

        HeldUsable heldUsable;
        float rmbDownAt;
        bool usingHeld;
        // True only for a right-button press that started while the current usable was already
        // in your hands. A press that began before it got there (grabbed with the button down,
        // or taken out of a pocket mid-press) is neither a use nor a throw of it: without this,
        // a grenade taken out while the button happened to be down would arm on the spot.
        bool rmbPressIsOurs;

        MovableObject held;
        bool dragging;
        float dragDistance;
        Vector3 pickedUpAt;

        CrewInput input;
        CrewMember member;
        ICrewInputSource lastSource;
        MovableObject lookedAt;   // under the crosshair this frame, hands empty only

        public bool IsCarrying => held != null;
        public bool IsDragging => held != null && dragging;

        // Written by Drunkenness, 0 when sober. It loosens the hold rather than dropping it:
        // the object lags further behind the hold point and takes longer to come round. You
        // keep the sofa, you just stop being good at it.
        [HideInInspector] public float carrySlop = 0f;
        float Grip => 1f - Mathf.Clamp01(carrySlop) * 0.55f;

        // What is in your hands right now. PlayerEquip needs it to know whether the thing you
        // are carrying is something you could put on instead.
        public MovableObject Held => held;

        // Lifted into the arms, as opposed to dragged or held on the rail as a small usable: the
        // hands go on its holds (FirstPersonHands, CrewCarryIK).
        public bool HoldsInArms => held != null && !dragging && heldUsable == null;
        public CarryGrip.Style GripStyle => gripStyle;
        public bool GripStyleKnown => gripStyleKnown;

        FirstPersonHands Hands
        {
            get
            {
                if (hands == null) hands = GetComponent<FirstPersonHands>();
                return hands;
            }
        }

        // Knocked down or tumbling: the body's bones lie on the floor while the eyes stay up.
        bool BodyDown
        {
            get
            {
                if (crewAnimator == null) crewAnimator = GetComponent<CrewAnimator>();
                if (tumble == null) tumble = GetComponent<KnockdownTumble>();
                return (crewAnimator != null && crewAnimator.IsDown) || (tumble != null && tumble.IsTumbling);
            }
        }

        // The crew body whose arms hold what is carried; none on the tutorial's capsule, so it is
        // looked for again only now and then.
        Animator Body
        {
            get
            {
                if (body == null && Time.frameCount >= nextBodyLookup)
                {
                    body = GetComponentInChildren<Animator>(true);
                    nextBodyLookup = Time.frameCount + 120;
                }
                return body;
            }
        }

        // The object the grab would drag rather than lift if you pressed it now, or null. The
        // prompt shows "[LMB] Drag" on it.
        public MovableObject DragTarget =>
            held == null && lookedAt != null && WouldDrag(lookedAt) && CanTake(lookedAt) ? lookedAt : null;

        // This player's number for events and "who did it" (Actors): the CrewMember index.
        public int Actor => member != null ? member.index : 0;

        float savedLinearDamping;
        float savedAngularDamping;
        float savedMaxAngularVelocity;
        RigidbodyConstraints savedConstraints;   // a dragged object's own, given back on release

        // The held object orientation, expressed relative to the player yaw. Turning your body
        // turns the object with you (a sofa held sideways stays sideways as you walk to the
        // door); looking up and down does not, because tipping cargo with the camera is not
        // how carrying reads.
        Quaternion heldLocalRotation = Quaternion.identity;
        bool rotating;

        // Kept across grabs on purpose: how far out you hold things is a stance, not a per
        // object setting, and resetting it every pickup would undo the player mid-manoeuvre.
        float extension;      // 0 arms bent .. 1 stretched, for a thing in the arms
        float usableReach;    // metres out on the rail, for a small usable

        // How the hands hold the thing in the arms (CarryGrip), chosen by the carry and read by
        // the hands so both agree.
        CarryGrip.Style gripStyle;
        bool gripStyleKnown;
        FirstPersonHands hands;       // whose arms the holds are kept within reach of, with no body
        Animator body;
        int nextBodyLookup;
        CrewAnimator crewAnimator;
        KnockdownTumble tumble;
        CharacterController capsule;
        RigidbodyInterpolation savedInterpolation;
        // The held thing's colliders, which our own capsule lets through, and those of things let
        // go that still overlap it (given back once apart, RestoreCapsule).
        readonly List<Collider> heldColliders = new List<Collider>(8);
        readonly List<Collider> letGo = new List<Collider>(16);
        bool jammed;   // what is in the arms is pushed back at you: no walking on into it
        float overSeconds;     // how long it has been past carrySlack
        bool reachedHands;     // it has been in the hands since it was taken
        float heldSince;

        // Reused by every grab ray, so looking for something to pick up costs no garbage. The
        // buffer keeps only the first hits it gets, in no set order, so it is sized well past
        // what a 3 m ray meets even through a pile of debris: a dropped hit could be the wall
        // that should have hidden the thing behind it.
        readonly RaycastHit[] grabHits = new RaycastHit[64];

        Quaternion PlayerYaw => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        void Awake()
        {
            extension = Mathf.Clamp01(startExtension);
            usableReach = Mathf.Clamp(usableStartReach, usableMinReach, maxHoldDistance);
            input = CrewSetup.InputOf(gameObject);
            member = GetComponent<CrewMember>();
            if (controller == null) controller = GetComponent<PlayerController>();
            capsule = GetComponent<CharacterController>();
        }

        void Start()
        {
            // Added at runtime by the spawner on a scene that did not have one.
            if (member == null) member = GetComponent<CrewMember>();
        }

        void Update()
        {
            // Online client: the host decides what is in the hands (Items Flags). The ray stays
            // for the Drag prompt, on the local player only (9.5).
            if (!Net.HasAuthority)
            {
                lookedAt = held == null && Net.IsLocal(member) ? LookedAtMovable() : null;
                ReplicaUpdate();
                return;
            }

            DropIfGone();
            WatchSource();

            lookedAt = held == null ? LookedAtMovable() : null;

            if (input.Down(CrewButton.Grab))
            {
                if (held == null) { if (lookedAt != null) Hold(lookedAt); }
                else Release(false);
            }
            HandleUseInput();
            HandleRotateInput();
            HandleReachInput();

            // carrying or dragging something heavy slows you down and flattens your jump
            if (controller != null)
            {
                CarryMultipliers(held, dragging, dragWalkMultiplier, out controller.speedMultiplier, out controller.jumpMultiplier);
                if (jammed && held != null && input.Move.y > 0f) controller.speedMultiplier = 0f;
            }
        }

        // What the load does to the walk and the jump. One place, shared by the carry and the
        // online client's replica, so the client never walks at full speed with a fridge.
        public static void CarryMultipliers(MovableObject held, bool dragging, float dragWalkMultiplier,
                                            out float speed, out float jump)
        {
            float carry = held == null ? 1f : Mathf.Clamp(1f - held.weight / 120f, 0.35f, 1f);
            speed = dragging ? Mathf.Min(carry, dragWalkMultiplier) : carry;
            jump = held == null
                ? 1f
                : Mathf.Clamp(1f - held.weight / 90f, 0.25f, 1f);
        }

        // ---- online client replica (NETCODE_SLICE 9.5) ----

        // The hands as the host has them: no physics, no events, only what the view and the
        // walk read. The look goes to the object with the same rule as the carry.
        void ReplicaUpdate()
        {
            // A replica held object the host broke (Props Destroyed) goes before its Flags record.
            if (held != null && (!held.gameObject.activeInHierarchy || held.destroyed)) ClearReplica();
            rotating = held != null && !dragging && holdOrientation && input.Held(CrewButton.Rotate);
            if (controller != null)
            {
                controller.lookLocked = rotating;
                CarryMultipliers(held, dragging, dragWalkMultiplier, out controller.speedMultiplier, out controller.jumpMultiplier);
                // The host's jam is not sent: a load in the arms pushed right up to the eyes stops
                // the walk on here too, or the client walks its camera into it.
                if (held != null && !dragging && heldUsable == null && cam != null && input.Move.y > 0f
                    && held.gameObject.activeInHierarchy
                    && CarryGrip.See(held, held.transform.position, held.transform.rotation, cam.position, CarryGrip.Frame(cam)).min.z < nearClearance * 0.5f)
                    controller.speedMultiplier = 0f;
            }
        }

        // Items Flags on the client: this player holds mo (or drags it). Sets the fields the
        // view reads and nothing else: no damping, no usable callbacks, no event.
        public void SetReplicaHeld(MovableObject mo, bool drag)
        {
            if (mo == null) { ClearReplica(); return; }
            if (held != null && held != mo && held.holder == this) held.holder = null;
            held = mo;
            dragging = drag;
            heldUsable = mo.GetComponent<HeldUsable>();
            mo.holder = this;
        }

        public void ClearReplica()
        {
            if (held != null && held.holder == this) held.holder = null;
            held = null;
            heldUsable = null;
            dragging = false;
            rotating = false;
            if (controller != null) controller.lookLocked = false;
        }

        // The keyboard moved to the other player (F1) in the middle of a hold. The new source
        // starts with nothing pressed, so no Up will ever close the press that was running:
        // the use is called off here instead. Called off, not ended: ending a grenade's use
        // throws it, and a key swap must never throw anything.
        void WatchSource()
        {
            var s = input.Source;
            if (ReferenceEquals(s, lastSource)) return;
            lastSource = s;
            rmbPressIsOurs = false;
            if (usingHeld)
            {
                usingHeld = false;
                if (heldUsable != null) heldUsable.OnUseCancelled();
            }
        }

        // The right button, and the only place its two meanings are decided.
        //
        // On an ordinary object it throws the instant you press it, exactly as it always has:
        // a sofa has nothing to offer a long press and a delayed throw would feel broken. On a
        // HeldUsable it is modal, a tap throws and a hold uses, and the throw moves to the
        // release because that is the only moment the two can be told apart. Holding it and
        // letting go never throws: you put the cigarette out, you do not flick it away. (The
        // grenade is the exception it chooses for itself: its OnUseEnd throws it.)
        // Something you are dragging is not thrown: the button does nothing then.
        void HandleUseInput()
        {
            if (held == null || dragging || heldUsable == null || !heldUsable.UsesHoldButton)
            {
                if (usingHeld) usingHeld = false;   // the object left our hands mid-use
                if (held != null && !dragging && input.Down(CrewButton.Throw)) Release(held.canThrow);
                return;
            }

            if (input.Down(CrewButton.Throw))
            {
                rmbDownAt = Time.time;
                usingHeld = false;
                rmbPressIsOurs = true;
            }
            if (!rmbPressIsOurs) return;

            if (input.Held(CrewButton.Throw) && Time.time - rmbDownAt >= useHoldThreshold)
            {
                if (!usingHeld)
                {
                    usingHeld = true;
                    heldUsable.OnUseBegin();
                    if (heldUsable == null) return;   // the use itself let go of the object
                }
                heldUsable.OnUseHold(Time.deltaTime);
                if (heldUsable == null) return;
            }

            if (input.Up(CrewButton.Throw))
            {
                rmbPressIsOurs = false;
                if (usingHeld)
                {
                    // Cleared before the call, not after: OnUseEnd is allowed to call
                    // Release(true) on us (the grenade throws itself that way), and Release
                    // must then see a use that is already over, or it would end it twice.
                    usingHeld = false;
                    heldUsable.OnUseEnd();
                }
                else Release(held.canThrow);
            }
        }

        void HandleRotateInput()
        {
            rotating = held != null && !dragging && holdOrientation && input.Held(CrewButton.Rotate);
            if (controller != null) controller.lookLocked = rotating;
            if (!rotating) return;

            // Axes are those of the player-yaw frame heldLocalRotation lives in:
            // up = yaw it, right = tip it away from you, forward = roll it like a barrel.
            Vector2 look = input.LookDelta;
            float mx = look.x * rotateSensitivity;
            float my = look.y * rotateSensitivity;
            float roll = input.RollDelta * rollSensitivity;

            if (Mathf.Abs(mx) > 0f) Turn(mx, Vector3.up);
            if (Mathf.Abs(my) > 0f) Turn(my, Vector3.right);
            if (Mathf.Abs(roll) > 0f) Turn(roll, Vector3.forward);
        }

        void HandleReachInput()
        {
            if (held == null || rotating || dragging) return;   // while rotating, the wheel belongs to the roll
            float scroll = input.Scroll;
            if (Mathf.Abs(scroll) <= 0f) return;
            if (heldUsable != null)
            {
                usableReach = Mathf.Clamp(usableReach + scroll * reachSensitivity, usableMinReach, maxHoldDistance);
                return;
            }
            // From where the arms are now: past a heavy thing's limit the stance would move with
            // nothing to show for it.
            float cap = ExtensionCap(WeightFactor(held));
            extension = Mathf.Clamp(Mathf.Min(extension, cap) + scroll * extensionSensitivity, 0f, cap);
        }

        float WeightFactor(MovableObject mo)
        {
            return Mathf.Clamp(maxSoloWeight / Mathf.Max(mo.weight, 1f), 0.2f, 1f);
        }

        // How far the arms can stretch with this much weight in them.
        float ExtensionCap(float weightFactor)
        {
            return Mathf.Lerp(heavyExtensionMax, 1f, weightFactor);
        }

        void Turn(float degrees, Vector3 axis)
        {
            heldLocalRotation = Quaternion.AngleAxis(degrees, axis) * heldLocalRotation;
        }

        // The grab under the crosshair, as a click does it. Public for the editor playtest,
        // which drives the player without a keyboard.
        public bool TryGrab()
        {
            var mo = LookedAtMovable();
            return mo != null && Hold(mo);
        }

        // Too heavy or not meant to be lifted: the grab drags it along the floor instead.
        public bool WouldDrag(MovableObject mo)
        {
            return mo != null && (!mo.canCarry || mo.weight > carryLimitKg);
        }

        // Whether this player could take it at all right now.
        public bool CanTake(MovableObject mo)
        {
            if (mo == null || mo.destroyed || mo.inPocket || mo.worn) return false;
            if (WouldDrag(mo) && !mo.canPush) return false;
            return !HeldByOther(mo);
        }

        bool HeldByOther(MovableObject mo)
        {
            var h = mo.holder;
            // A stale holder (destroyed, switched off, or no longer holding it) does not count.
            return h != null && h != this && h.isActiveAndEnabled && h.Held == mo;
        }

        // What the crosshair is on, within reach. The nearest solid thing still wins, so a wall
        // still hides the chair behind it. Two things no longer count as solid: triggers (the
        // truck cargo zone is one, and it used to swallow the ray, so nothing already loaded in
        // the truck could be picked back up) and the player's own body, including anything worn.
        MovableObject LookedAtMovable()
        {
            if (cam == null) return null;

            // DefaultRaycastLayers, like the plain Raycast this replaced: the Ignore Raycast
            // layer stays ignored.
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, grabHits, grabRange,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = grabHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (grabHits[i].distance < best)
                {
                    best = grabHits[i].distance;
                    nearest = c;
                }
            }
            if (nearest == null) return null;

            var mo = nearest.GetComponentInParent<MovableObject>();
            return mo != null && !mo.destroyed ? mo : null;
        }

        // Puts an object in your hands exactly as a successful grab does, without the ray.
        // PlayerPockets uses it to hand you what you just took out of a pocket. Refuses when
        // the hands are already full, when someone else is holding it, or when the object is
        // not really there any more. An object too heavy to lift is taken as a drag.
        public bool Hold(MovableObject mo)
        {
            if (!Net.HasAuthority) return false;   // online client: the host holds, Items Flags tell
            DropIfGone();
            if (!ReferenceEquals(held, null)) return false;
            if (mo == null || !mo.gameObject.activeInHierarchy || !CanTake(mo)) return false;
            // MovableObject fills rb in its Awake; an object built inactive may not have run it.
            if (mo.rb == null) mo.rb = mo.GetComponent<Rigidbody>();
            if (mo.rb == null) return false;

            held = mo;
            dragging = WouldDrag(mo);
            savedLinearDamping = held.rb.linearDamping;
            savedAngularDamping = held.rb.angularDamping;
            savedMaxAngularVelocity = held.rb.maxAngularVelocity;
            held.rb.useGravity = true;
            if (dragging)
            {
                // Held at the distance it lay at, on the floor: its own damping and friction
                // are what make it heavy, so the carry's damping is not applied.
                Vector3 flat = held.rb.worldCenterOfMass - transform.position;
                flat.y = 0f;
                dragDistance = Mathf.Clamp(flat.magnitude, 0.6f, grabRange);
                // It slides, it does not tumble (see TippingAxes).
                savedConstraints = held.rb.constraints;
                held.rb.constraints = savedConstraints | TippingAxes(held.rb);
            }
            else
            {
                held.rb.linearDamping = carriedLinearDamping;
                held.rb.angularDamping = carriedAngularDamping;
                // the default cap would silently eat the turn on the fastest objects
                held.rb.maxAngularVelocity = Mathf.Max(savedMaxAngularVelocity, maxAngularSpeed);
                // Right in front of the eyes, which move every frame: drawn between two physics
                // steps, not stepping at 50 Hz under a smooth camera with the hands on it.
                savedInterpolation = held.rb.interpolation;
                held.rb.interpolation = RigidbodyInterpolation.Interpolate;
                // Held against your chest, it would fight your own capsule, and the capsule would
                // walk into it.
                IgnoreOwnCapsule(held);
                gripStyleKnown = false;
                overSeconds = 0f;
                reachedHands = false;
                heldSince = Time.time;
            }
            // pick the object up as it lies, do not snap it to a pose
            heldLocalRotation = Quaternion.Inverse(PlayerYaw) * held.rb.rotation;

            usingHeld = false;
            rmbPressIsOurs = false;

            mo.holder = this;
            mo.lastHandler = Actor;
            mo.lastHandledTime = Time.time;
            pickedUpAt = mo.transform.position;

            // Most things are not usable and this stays null, which is what puts the
            // right button back to its plain throw.
            heldUsable = held.GetComponent<HeldUsable>();
            if (heldUsable != null) heldUsable.OnPickedUp(this);

            // Last, once the hands are fully set up: a listener may react by making us let go.
            WorldEvents.Raise(WorldEventType.ObjectPickedUp, pickedUpAt, Actor, 0f, 0f, 0, mo);
            return true;
        }

        public void Release(bool thrown)
        {
            Release(thrown, true);
        }

        // announce false: the object is not being let go of in the world, it is going into a
        // pocket or onto the body, and those have their own events.
        public void Release(bool thrown, bool announce)
        {
            if (!Net.HasAuthority) return;   // online client: the host lets go, Items Flags tell
            // ReferenceEquals, not ==: an object destroyed in our hands compares equal to null
            // and would otherwise leave its usable and our damping bookkeeping stranded here.
            if (ReferenceEquals(held, null)) return;

            // Our side is cleared first. A usable may call Release again from inside its own
            // callbacks below (the grenade throws itself from OnUseEnd); by then the hands are
            // already empty, so the second call is a no-op instead of a double release.
            MovableObject mo = held;
            HeldUsable usable = heldUsable;
            bool wasUsing = usingHeld;
            bool wasDragging = dragging;
            held = null;
            heldUsable = null;
            usingHeld = false;
            rmbPressIsOurs = false;
            rotating = false;
            dragging = false;
            if (controller != null) controller.lookLocked = false;

            // A shattered object is inactive and a destroyed one is gone: give back what the
            // carry borrowed only if there is still a body to give it back to, and only throw
            // a body that is actually in the world.
            Rigidbody body = mo != null ? mo.rb : null;
            bool inWorld = mo != null && mo.gameObject.activeInHierarchy && !mo.destroyed;
            float throwSpeed = 0f;
            if (body != null)
            {
                body.linearDamping = savedLinearDamping;
                body.angularDamping = savedAngularDamping;
                body.maxAngularVelocity = savedMaxAngularVelocity;
                if (wasDragging) body.constraints = savedConstraints;
                else body.interpolation = savedInterpolation;
                if (thrown && !wasDragging && cam != null && inWorld && !body.isKinematic)
                {
                    body.AddForce(cam.forward * throwForce, ForceMode.VelocityChange);
                    // AddForce lands on the next physics step; this is the speed it leaves with.
                    throwSpeed = (body.linearVelocity + cam.forward * throwForce).magnitude;
                }
            }

            // Our capsule and the thing collide again once they are apart.
            letGo.AddRange(heldColliders);
            heldColliders.Clear();

            if (mo != null)
            {
                if (mo.holder == this) mo.holder = null;
                mo.lastHandledTime = Time.time;
                if (thrown)
                {
                    mo.lastThrownBy = Actor;
                    mo.lastThrownTime = Time.time;
                }
                if (announce && inWorld) Announce(mo, thrown && throwSpeed > 0f, throwSpeed);
            }

            if (usable != null)
            {
                if (wasUsing) usable.OnUseEnd();
                usable.OnReleased(thrown);
            }
        }

        void Announce(MovableObject mo, bool thrown, float speed)
        {
            Vector3 at = mo.transform.position;
            if (thrown) WorldEvents.Raise(WorldEventType.ObjectThrown, at, Actor, 0f, speed, 0, mo);
            else WorldEvents.Raise(WorldEventType.ObjectReleased, at, Actor, 0f, 0f, 0, mo);

            // Where it left the hands against where it was taken: a chair put back where it
            // stood moved nothing, a sofa across the room did.
            float moved = Vector3.Distance(at, pickedUpAt);
            if (moved > 0.5f) WorldEvents.Raise(WorldEventType.FurnitureMoved, at, Actor, 0f, moved, 0, mo);
        }

        // The object can leave our hands without us letting go: the destruction system shatters
        // it (it goes inactive), a pocket takes it, or something destroys it outright. Checked
        // at the top of Update and FixedUpdate so the carry never drives a body that is gone.
        void DropIfGone()
        {
            if (ReferenceEquals(held, null)) return;
            if (held != null && held.rb != null && held.gameObject.activeInHierarchy && !held.destroyed) return;
            Release(false);
        }

        void OnDisable()
        {
            if (!Net.HasAuthority) { ClearReplica(); return; }
            // Never strand the camera in rotate mode, nor an object with a carrier who is gone
            // (the truck seat switches the grab off). Silent: this also runs while the scene
            // unloads, when the listeners of an event may already be half torn down.
            if (controller != null) controller.lookLocked = false;
            if (!ReferenceEquals(held, null)) Release(false, false);
        }

        void FixedUpdate()
        {
            if (!Net.HasAuthority) return;   // the client's replicas are kinematic: nothing to drive
            DropIfGone();
            RestoreCapsule();
            bool wasJammed = jammed;
            jammed = false;
            if (held == null) return;
            held.lastHandledTime = Time.time;   // still in someone's hands: whatever it hits is theirs

            if (dragging)
            {
                DriveDrag();
                return;
            }

            float weightFactor = WeightFactor(held);
            bool inArms = heldUsable == null;
            Vector3 target = inArms ? CarryTarget(weightFactor) : UsableTarget();
            Vector3 toTarget = target - held.rb.worldCenterOfMass;
            if (inArms)
            {
                // Pushed back, and not just lagging: a heavy thing trails you but keeps your pace,
                // a blocked one stops. Once jammed, it stays so until it is nearly back in place.
                Vector3 ahead = PlayerYaw * Vector3.forward;
                float back = Vector3.Dot(toTarget, ahead);
                float yours = controller != null ? Vector3.Dot(controller.Velocity, ahead) : 0f;
                float its = Vector3.Dot(held.rb.linearVelocity, ahead);
                jammed = wasJammed ? back > jamDistance * 0.5f : back > jamDistance && its < yours * 0.5f;
            }
            // It is carried, so it goes where you go: most of your own velocity by itself, the
            // pull catching up the rest. What is left to catch up (a start, a turn) is the weight.
            Vector3 carried = controller != null
                ? controller.Velocity * Mathf.Lerp(heavyFollowThrough, 1f, weightFactor)
                : Vector3.zero;
            Vector3 desired = carried + toTarget * followStrength * weightFactor * Grip;
            if (inArms)
            {
                float over = Overreach();
                if (over > 0f) desired += toTarget.normalized * (over * leashStrength);
                if (LostGrip(over)) return;
            }
            held.rb.linearVelocity = Vector3.ClampMagnitude(desired, maxSpeed);

            if (holdOrientation) DriveRotation(weightFactor);
        }

        // Past the arms' reach by more than carrySlack for gripLossSeconds, or its centre past
        // maxHoldDistance by as much: it is let go. Only once it has been in the hands, or after
        // a second and a half if it never got there.
        bool LostGrip(float over)
        {
            if (over <= 0.02f) reachedHands = true;
            bool far = over > carrySlack
                       || (held.rb.worldCenterOfMass - cam.position).magnitude > maxHoldDistance + carrySlack;
            if (!far || (!reachedHands && Time.time - heldSince < 1.5f))
            {
                overSeconds = 0f;
                return false;
            }
            overSeconds += Time.fixedDeltaTime;
            if (overSeconds < gripLossSeconds) return false;
            Release(false);
            return true;
        }

        // A small usable's body, out on the rail in front of the eyes.
        Vector3 UsableTarget()
        {
            Vector3 target = cam.position + cam.forward * Mathf.Clamp(usableReach, usableMinReach, maxHoldDistance);
            // Heavy things hang lower. You cannot hold a fridge at eye level, and a settled
            // offset reads as weight without the object sinking through the floor.
            target.y -= Mathf.Min(held.weight * sagPerKg, maxSag);
            return target;
        }

        // Where the centre of mass of a thing in the arms is driven to: under the eyes at chest
        // height (lower if it is tall, so you see over it), its holds as far out as the wheel has
        // the arms stretched, then pulled in until every hold is within the arms' reach and the
        // centre within maxHoldDistance of the eyes. Worked out as it is turned now, so turning a
        // sofa end-on walks it out and turning it back brings it in.
        Vector3 CarryTarget(float weightFactor)
        {
            Rigidbody rb = held.rb;
            Vector3 eyes = cam.position;
            Quaternion look = CarryGrip.Frame(cam);
            Quaternion turn = rb.rotation;
            Vector3 rootFromCom = rb.position - rb.worldCenterOfMass;

            // Measured with its centre of mass on the eyes.
            var seen = CarryGrip.See(held, eyes + rootFromCom, turn, eyes, look);
            CarryGrip.Decide(seen, ref gripStyle, gripStyleKnown);
            gripStyleKnown = true;
            CarryGrip.Holds(seen, gripStyle, out var l, out var r);
            float holdDepth = Depth(l, r, eyes, look);

            float ext = Mathf.Min(extension, ExtensionCap(weightFactor));
            // Arms bent, the near face still clears the eyes.
            float bent = Mathf.Max(bentGripDepth, nearClearance + holdDepth - seen.min.z);
            float gripDepth = Mathf.Lerp(bent, Mathf.Max(bent, stretchedGripDepth), ext);
            float side = gripStyle == CarryGrip.Style.OneHand ? oneHandSide : 0f;
            float down = Mathf.Max(holdDrop, seen.max.y + seeOverClearance);

            Vector3 target = eyes + look * new Vector3(side, -down, gripDepth - holdDepth);
            // Heavy things hang lower: the weight feel of playtest 001, kept.
            target.y -= Mathf.Min(held.weight * sagPerKg, maxSag);
            target = AboveFloor(target, turn, rootFromCom);
            target = WithinReach(target, turn, rootFromCom, eyes, look);

            Vector3 fromEyes = target - eyes;
            if (fromEyes.magnitude > maxHoldDistance) target = eyes + fromEyes.normalized * maxHoldDistance;
            // The floor has the last word: the reach and the cap can both pull it lower.
            return AboveFloor(target, turn, rootFromCom);
        }

        // The holds' depth along the eyes' line.
        static float Depth(in CarryGrip.Hold l, in CarryGrip.Hold r, Vector3 eyes, Quaternion look)
        {
            Quaternion toView = Quaternion.Inverse(look);
            float z = 0f;
            int n = 0;
            if (r.used) { z += (toView * (r.point - eyes)).z; n++; }
            if (l.used) { z += (toView * (l.point - eyes)).z; n++; }
            return n > 0 ? z / n : 0f;
        }

        // Never through the floor you stand on: a tall thing lowered to be seen over stops short of
        // it (a wardrobe cannot be seen over), and a sagging one is lifted clear.
        Vector3 AboveFloor(Vector3 target, Quaternion turn, Vector3 rootFromCom)
        {
            if (controller == null) return target;
            float floor = transform.position.y + controller.FeetHeight + 0.03f;
            // Seen from the world's own axes, its span is its span in the world.
            var world = CarryGrip.See(held, target + rootFromCom, turn, Vector3.zero, Quaternion.identity);
            if (world.min.y < floor) target.y += floor - world.min.y;
            return target;
        }

        // Pulled in toward the shoulders until every hold is within the arms' reach: a heavy
        // thing's sag, a wide one's span or the floor can put them out of it.
        Vector3 WithinReach(Vector3 target, Quaternion turn, Vector3 rootFromCom, Vector3 eyes, Quaternion look)
        {
            var arms = CarryGrip.Arms.Of(Body, BodyDown, Hands, cam, look);
            for (int i = 0; i < 3; i++)
            {
                var seen = CarryGrip.See(held, target + rootFromCom, turn, eyes, look);
                CarryGrip.Holds(seen, gripStyle, out var l, out var r);
                float over = 0f;
                Vector3 pull = Vector3.zero;
                Stretch(r, 1f, arms, ref over, ref pull);
                Stretch(l, -1f, arms, ref over, ref pull);
                if (over <= 0.005f) break;
                // Up and in along the eyes' axes only: a thing in both hands stays centred.
                Vector3 local = Quaternion.Inverse(look) * pull;
                if (gripStyle != CarryGrip.Style.OneHand) local.x = 0f;
                if (local.sqrMagnitude < 1e-8f) break;
                target += look * (local.normalized * over);
            }
            return target;
        }

        // How far past the arms' reach its holds are right now, 0 within it.
        float Overreach()
        {
            Vector3 eyes = cam.position;
            Quaternion look = CarryGrip.Frame(cam);
            var arms = CarryGrip.Arms.Of(Body, BodyDown, Hands, cam, look);
            var seen = CarryGrip.See(held, held.rb.position, held.rb.rotation, eyes, look);
            CarryGrip.Holds(seen, gripStyle, out var l, out var r);
            float over = 0f;
            Vector3 pull = Vector3.zero;
            Stretch(r, 1f, arms, ref over, ref pull);
            Stretch(l, -1f, arms, ref over, ref pull);
            return over;
        }

        // One hold against its shoulder: the largest excess so far, and the way to pull it in.
        static void Stretch(in CarryGrip.Hold h, float side, in CarryGrip.Arms arms, ref float over, ref Vector3 pull)
        {
            if (!h.used) return;
            Vector3 toShoulder = arms.Shoulder(side) - h.point;
            float excess = toShoulder.magnitude - arms.reach;
            if (excess <= 0f) return;
            over = Mathf.Max(over, excess);
            pull += toShoulder.normalized * excess;
        }

        // The thing in the arms and our own capsule pass through each other. IgnoreCollision
        // only takes colliders that are switched on, and forgets a pair when either is switched
        // off, so both are checked.
        void IgnoreOwnCapsule(MovableObject mo)
        {
            if (capsule == null || !capsule.enabled || !capsule.gameObject.activeInHierarchy) return;
            mo.GetComponentsInChildren(false, heldColliders);
            for (int i = heldColliders.Count - 1; i >= 0; i--)
            {
                var c = heldColliders[i];
                if (c == null || !c.enabled || c.isTrigger) { heldColliders.RemoveAt(i); continue; }
                letGo.Remove(c);
                Physics.IgnoreCollision(capsule, c, true);
            }
        }

        // Things let go collide with our capsule again once they are clear of it: turned back
        // on while they overlap, the capsule would shove the thing away or stand on it.
        void RestoreCapsule()
        {
            if (letGo.Count == 0) return;
            if (capsule == null || !capsule.enabled || !capsule.gameObject.activeInHierarchy)
            {
                letGo.Clear();   // switched off: the pairs went with it
                return;
            }
            Bounds us = capsule.bounds;
            for (int i = letGo.Count - 1; i >= 0; i--)
            {
                var c = letGo[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) { letGo.RemoveAt(i); continue; }
                if (c.bounds.Intersects(us)) continue;
                Physics.IgnoreCollision(capsule, c, false);
                letGo.RemoveAt(i);
            }
        }

        // The drag: the same velocity steering as the carry, flattened. Only the horizontal
        // velocity is set, so the object keeps falling, sliding and catching on things like the
        // heavy thing it is; it is never lifted and never turned by hand.
        void DriveDrag()
        {
            Vector3 anchor = transform.position + PlayerYaw * Vector3.forward * dragDistance;
            Vector3 com = held.rb.worldCenterOfMass;
            Vector3 to = anchor - com;
            to.y = 0f;

            // Caught on something and left behind: you lose your grip, you do not drag it
            // through the wall.
            if (FlatDistance(com) > dragDistance + dragSlack)
            {
                Release(false);
                return;
            }

            float strength = Mathf.Clamp(dragFullSpeedKg / Mathf.Max(held.weight, 1f), 0.15f, 1f);
            Vector3 desired = Vector3.ClampMagnitude(to * dragFollowStrength * strength * Grip, dragMaxSpeed);
            Vector3 v = held.rb.linearVelocity;
            held.rb.linearVelocity = new Vector3(desired.x, v.y, desired.z);
        }

        // The drag freezes the object's tipping for as long as it lasts (Hold sets it, Release
        // gives the object its own constraints back). The steering above sets the centre of
        // mass's speed outright, so the floor's friction on the bottom turns part of every push
        // into a tipping spin, and with the centre of mass pinned gravity cannot tip it back:
        // unfrozen, a 0.6 m crate went to 45 deg with a 4.5 cm hop in 0.3 s and was then held
        // standing on an edge (integration E3). Zeroing the spin each step was not enough (it
        // crept to 19 deg: gravity's way back was zeroed too). Rotation freezes act in the
        // body's inertia frame (measured in Unity 6), so the two inertia axes lying flattest
        // are frozen and the one nearest world up, the object's turn as it lies, stays free.
        static RigidbodyConstraints TippingAxes(Rigidbody rb)
        {
            Quaternion frame = rb.rotation * rb.inertiaTensorRotation;
            float x = Mathf.Abs(Vector3.Dot(frame * Vector3.right, Vector3.up));
            float y = Mathf.Abs(Vector3.Dot(frame * Vector3.up, Vector3.up));
            float z = Mathf.Abs(Vector3.Dot(frame * Vector3.forward, Vector3.up));
            if (y >= x && y >= z) return RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            if (x >= z) return RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            return RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        }

        float FlatDistance(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // Same control model as the carry: steer the angular velocity straight at the target
        // orientation rather than apply torque. Heavy objects turn slower, for the same reason
        // they lag behind the hold point.
        void DriveRotation(float weightFactor)
        {
            Quaternion delta = (PlayerYaw * heldLocalRotation) * Quaternion.Inverse(held.rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            bool usable = axis.sqrMagnitude > 0.0001f
                          && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x)
                          && Mathf.Abs(angle) > 0.05f;
            if (!usable)
            {
                held.rb.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 spin = axis.normalized * (angle * Mathf.Deg2Rad * rotateStrength * weightFactor * Grip);
            held.rb.angularVelocity = Vector3.ClampMagnitude(spin, maxAngularSpeed * weightFactor);
        }
    }
}
