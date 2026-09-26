using UnityEngine;

namespace Movers
{
    // Physics grab: the object stays a rigidbody and is dragged toward a hold point.
    // Heavy objects lag and resist (weight matters). This unwieldy feel IS the game.
    //
    // Driven by this player's CrewInput. One object has one carrier (MovableObject.holder):
    // a second player cannot take what the first is holding. What cannot be lifted (canCarry
    // off, or heavier than carryLimitKg) is dragged along the floor instead.
    public class PlayerGrab : MonoBehaviour
    {
        public Transform cam;
        public PlayerController controller;

        public float grabRange = 3f;
        public float holdDistance = 2.2f;    // the reach you start each session with, see currentReach
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

        [Header("Reach")]
        // Scroll with nothing held down: push the object out or pull it in, so you can post it
        // through a doorway from a step back, or hug it to your chest to squeeze past. Holding
        // rotate gives the same wheel to the roll instead, so one wheel does two jobs and
        // neither has to share a key.
        public float minReach = 1.0f;
        public float maxReach = 3.2f;
        public float reachSensitivity = 3.5f;  // a scroll notch is about 0.1, so this is ~0.35 m per notch
        // Heavy things cannot be held at arm's length. The far limit closes in as weight rises,
        // on the same weight factor that already makes them lag and turn slowly.
        public float heavyReachFloor = 1.5f;

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
        float currentReach;

        // Reused by every grab ray, so looking for something to pick up costs no garbage. The
        // buffer keeps only the first hits it gets, in no set order, so it is sized well past
        // what a 3 m ray meets even through a pile of debris: a dropped hit could be the wall
        // that should have hidden the thing behind it.
        readonly RaycastHit[] grabHits = new RaycastHit[64];

        Quaternion PlayerYaw => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        void Awake()
        {
            currentReach = Mathf.Clamp(holdDistance, minReach, maxReach);
            input = CrewSetup.InputOf(gameObject);
            member = GetComponent<CrewMember>();
            if (controller == null) controller = GetComponent<PlayerController>();
        }

        void Start()
        {
            // Added at runtime by the spawner on a scene that did not have one.
            if (member == null) member = GetComponent<CrewMember>();
        }

        void Update()
        {
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
                float carry = held == null ? 1f : Mathf.Clamp(1f - held.weight / 120f, 0.35f, 1f);
                controller.speedMultiplier = dragging ? Mathf.Min(carry, dragWalkMultiplier) : carry;
                controller.jumpMultiplier = held == null
                    ? 1f
                    : Mathf.Clamp(1f - held.weight / 90f, 0.25f, 1f);
            }
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
            float scroll = input.Scroll * reachSensitivity;
            if (Mathf.Abs(scroll) <= 0f) return;
            currentReach = Mathf.Clamp(currentReach + scroll, minReach, maxReach);
        }

        // What the arms can actually manage right now: your chosen reach, capped by the weight.
        float EffectiveReach(float weightFactor)
        {
            return Mathf.Min(currentReach, Mathf.Lerp(heavyReachFloor, maxReach, weightFactor));
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
                if (thrown && !wasDragging && cam != null && inWorld && !body.isKinematic)
                {
                    body.AddForce(cam.forward * throwForce, ForceMode.VelocityChange);
                    // AddForce lands on the next physics step; this is the speed it leaves with.
                    throwSpeed = (body.linearVelocity + cam.forward * throwForce).magnitude;
                }
            }

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
            // Never strand the camera in rotate mode, nor an object with a carrier who is gone
            // (the truck seat switches the grab off). Silent: this also runs while the scene
            // unloads, when the listeners of an event may already be half torn down.
            if (controller != null) controller.lookLocked = false;
            if (!ReferenceEquals(held, null)) Release(false, false);
        }

        void FixedUpdate()
        {
            DropIfGone();
            if (held == null) return;
            held.lastHandledTime = Time.time;   // still in someone's hands: whatever it hits is theirs

            if (dragging)
            {
                DriveDrag();
                return;
            }

            float weightFactor = Mathf.Clamp(maxSoloWeight / Mathf.Max(held.weight, 1f), 0.2f, 1f);

            Vector3 target = cam.position + cam.forward * EffectiveReach(weightFactor);
            // Heavy things hang lower. You cannot hold a fridge at eye level, and a settled
            // offset reads as weight without the object sinking through the floor.
            target.y -= Mathf.Min(held.weight * sagPerKg, maxSag);
            Vector3 toTarget = target - held.rb.worldCenterOfMass;
            Vector3 desired = Vector3.ClampMagnitude(toTarget * followStrength * weightFactor * Grip, maxSpeed);
            held.rb.linearVelocity = desired;

            if (holdOrientation) DriveRotation(weightFactor);
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
