using UnityEngine;

namespace Movers
{
    // For one crew member: every action available right now, on which key, for what they look
    // at and what they hold. Reads gameplay state only; never reads a key for itself (except
    // "is the throw button held", which changes what releasing it does), never changes
    // anything. Runs every frame per player and allocates nothing.
    //
    // Order is priority: the thing under the crosshair first (a door opens with a box in your
    // arms), then what makes the held object special (smoke, drink, pin, wear), then the
    // pocket, then the plain carry verbs. HintSet keeps the first four.
    public sealed class VerbHints
    {
        readonly CrewMember member;

        // Looked up when the hands change, not every frame.
        MovableObject lastHeld;
        HeldUsable heldUsable;
        EquipItem heldWearable;
        KeyItem heldKey;

        CrewEquip body;
        bool bodyLooked;
        VehicleSeat seat;

        // One ray per player per frame, the same question PlayerGrab asks (its answer is
        // private): what could be picked up under the crosshair.
        static readonly RaycastHit[] hits = new RaycastHit[64];

        public VerbHints(CrewMember member)
        {
            this.member = member;
        }

        public HintSet Compute()
        {
            PlayerInteract interact = member != null ? member.Interact : null;
            Interactable target = interact != null && interact.isActiveAndEnabled ? interact.Target : null;
            return Compose(target, null, true);
        }

        // The rows for a given target and looked-at object, as if the crosshair were on them:
        // the Play-mode tests use it to check every verb without aiming a camera.
        public HintSet ComposeFor(Interactable target, MovableObject lookedAt) => Compose(target, lookedAt, false);

        HintSet Compose(Interactable target, MovableObject lookedAt, bool castRay)
        {
            var set = new HintSet();
            if (member == null || member.Input == null) return set;
            ICrewInputSource src = member.Input.Source;
            if (src is NullInputSource) return set;   // nobody drives this player

            if (member.IsDriving)
            {
                Driving(ref set, src);
                return set;
            }
            seat = null;

            PlayerGrab grab = member.Grab;
            PlayerInteract interact = member.Interact;
            MovableObject held = grab != null && grab.isActiveAndEnabled ? grab.Held : null;
            RefreshHeld(held);

            if (target != null) InteractRow(ref set, target, interact, src);

            if (held != null) Hands(ref set, grab, held, src);
            else if (grab != null && grab.isActiveAndEnabled)
            {
                MovableObject drag = castRay ? grab.DragTarget
                                   : lookedAt != null && grab.WouldDrag(lookedAt) && grab.CanTake(lookedAt) ? lookedAt : null;
                if (drag != null)
                {
                    set.Add(new HintLine(Verb.Drag, InputGlyphs.For(src, CrewButton.Grab)));
                    set.card = Card(drag, false, grab);
                }
                else if (target == null)
                {
                    MovableObject look = castRay ? LookedAt(grab) : lookedAt;
                    if (look != null)
                    {
                        set.card = Card(look, false, grab);
                        if (grab.CanTake(look)) set.Add(new HintLine(Verb.Grab, InputGlyphs.For(src, CrewButton.Grab)));
                    }
                }
                if (IsWearing()) set.Add(new HintLine(Verb.TakeOff, InputGlyphs.For(src, CrewButton.Alt)));
            }
            return set;
        }

        // ---- the action key ----

        void InteractRow(ref HintSet set, Interactable target, PlayerInteract interact, ICrewInputSource src)
        {
            Glyph e = InputGlyphs.For(src, CrewButton.Interact);
            switch (target)
            {
                case HingedPanel p:
                {
                    HingedGroup g = p.Group != null && p.Group.isActiveAndEnabled ? p.Group : null;
                    if (g != null) DoorGroup(ref set, g, e, src);
                    else DoorPanel(ref set, p, e, src);
                    return;
                }
                case HingedGroup group:
                    DoorGroup(ref set, group, e, src);
                    return;
                case GrandmaTalk _:
                    set.Add(new HintLine(Verb.Talk, e));
                    return;
                case VehicleSeat _:
                    set.Add(new HintLine(Verb.Drive, e));
                    return;
                case DeliverPoint _:
                {
                    var contract = GameSession.Current != null ? GameSession.Current.contract : null;
                    var t = contract != null ? contract.Tracker : null;
                    set.Add(new HintLine(Verb.Deliver, e, Gesture.Press, t != null ? t.RemainingLoaded : 0, t != null ? t.Total : 0));
                    return;
                }
            }
            // An Interactable this table does not know yet: its own words, translated if known.
            string words = target.PromptFor(interact);
            if (!string.IsNullOrEmpty(words)) set.Add(new HintLine(Verb.Raw, e, raw: Loc.Prompt(words)));
        }

        void DoorPanel(ref HintSet set, HingedPanel p, Glyph e, ICrewInputSource src)
        {
            DoorVerb v = p.VerbFor(member);
            AddDoorVerb(ref set, v, e, p.promptOpen, p.promptClose);
            // The key in the hands turns in a shut door it fits (KeyItem, on the alt button).
            DoorLock l = p.Lock;
            if (heldKey != null && l != null && !p.IsOpen && p.CanSwing && l.LockableBy(member) && !l.IsLocked)
                set.Add(new HintLine(Verb.Lock, InputGlyphs.For(src, CrewButton.Alt)));
        }

        void DoorGroup(ref HintSet set, HingedGroup g, Glyph e, ICrewInputSource src)
        {
            DoorVerb v = g.VerbFor(member);
            AddDoorVerb(ref set, v, e, g.promptOpen, g.promptClose);
            if (heldKey == null || v != DoorVerb.Open) return;
            var panels = g.panels;
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                DoorLock l = p != null ? p.Lock : null;
                if (l != null && p.CanSwing && l.LockableBy(member))
                {
                    set.Add(new HintLine(Verb.Lock, InputGlyphs.For(src, CrewButton.Alt)));
                    return;
                }
            }
        }

        static void AddDoorVerb(ref HintSet set, DoorVerb v, Glyph e, string promptOpen, string promptClose)
        {
            switch (v)
            {
                case DoorVerb.Close: set.Add(new HintLine(Verb.Close, e, raw: Loc.Prompt(promptClose))); break;
                case DoorVerb.Unlock: set.Add(new HintLine(Verb.Unlock, e)); break;
                case DoorVerb.Locked: set.Add(new HintLine(Verb.Locked, e, warn: true)); break;
                case DoorVerb.Lock: set.Add(new HintLine(Verb.Lock, e)); break;
                default: set.Add(new HintLine(Verb.Open, e, raw: Loc.Prompt(promptOpen))); break;
            }
        }

        // ---- the hands ----

        void Hands(ref HintSet set, PlayerGrab grab, MovableObject held, ICrewInputSource src)
        {
            set.card = Card(held, true, grab);
            Glyph grabKey = InputGlyphs.For(src, CrewButton.Grab);
            Glyph throwKey = InputGlyphs.For(src, CrewButton.Throw);
            if (grab.IsDragging)
            {
                set.Add(new HintLine(Verb.LetGo, grabKey));
                return;
            }

            HeldUsable usable = heldUsable;
            bool modal = usable != null && usable.UsesHoldButton;

            // What makes this object special, on the right button (hold) or the alt button.
            GrenadeItem grenade = usable as GrenadeItem;
            if (grenade != null)
            {
                if (grenade.IsArmed)
                    set.Add(member.Input.Held(CrewButton.Throw)
                        ? new HintLine(Verb.ReleaseThrow, throwKey, Gesture.Release)
                        : new HintLine(Verb.ThrowGrenade, throwKey));
                else set.Add(new HintLine(Verb.PullPin, throwKey, Gesture.Hold));
            }
            else if (usable is CigaretteItem) set.Add(new HintLine(Verb.Smoke, throwKey, Gesture.Hold));
            else if (modal) set.Add(new HintLine(Verb.Raw, throwKey, Gesture.Hold, raw: Loc.T("verb.use")));

            if (heldWearable != null) set.Add(new HintLine(Verb.Wear, InputGlyphs.For(src, CrewButton.Alt)));
            else if (usable != null && usable.UsesAltButton)
            {
                bool empty = usable is BeerItem beer && beer.IsEmpty;
                set.Add(empty ? new HintLine(Verb.EmptyBottle, default, warn: true)
                              : new HintLine(Verb.Drink, InputGlyphs.For(src, CrewButton.Alt), Gesture.Hold));
            }

            if (PlayerPockets.Fits(held))
            {
                int slot = FirstEmptyPocket();
                if (slot >= 0) set.Add(new HintLine(Verb.Pocket, InputGlyphs.PocketSlot(src, slot)));
            }

            // The plain right button: a throw, or on a usable a tap that throws it away.
            if (!modal) { if (held.canThrow) set.Add(new HintLine(Verb.Throw, throwKey)); }
            else if (grenade == null || !grenade.IsArmed) set.Add(new HintLine(Verb.ThrowAway, throwKey, Gesture.Tap));

            set.Add(new HintLine(Verb.Drop, grabKey));

            if (grab.holdOrientation)
            {
                Glyph rotateKey = InputGlyphs.For(src, CrewButton.Rotate);
                set.Add(member.Input.Held(CrewButton.Rotate)
                    ? new HintLine(Verb.Roll, InputGlyphs.Roll(src))
                    : new HintLine(Verb.Rotate, rotateKey, Gesture.Hold));
            }
            set.Add(new HintLine(Verb.Reach, InputGlyphs.For(src, CrewButton.Reach), Gesture.Press, glyph2: InputGlyphs.ReachStick(src)));
        }

        void RefreshHeld(MovableObject held)
        {
            if (ReferenceEquals(held, lastHeld)) return;
            lastHeld = held;
            heldUsable = null;
            heldWearable = null;
            heldKey = null;
            if (held == null) return;
            held.TryGetComponent(out heldUsable);
            held.TryGetComponent(out heldWearable);
            held.TryGetComponent(out heldKey);
        }

        int FirstEmptyPocket()
        {
            PlayerPockets pockets = member.Pockets;
            if (pockets == null || !pockets.isActiveAndEnabled) return -1;
            for (int i = 0; i < PlayerPockets.SlotCount; i++)
                if (pockets.GetItem(i) == null) return i;
            return -1;
        }

        bool IsWearing()
        {
            if (!bodyLooked)
            {
                bodyLooked = true;
                body = member.GetComponentInChildren<CrewEquip>();
            }
            if (body == null) return false;
            for (int s = 0; s <= (int)EquipSlot.Feet; s++)
                if (body.IsWearing((EquipSlot)s)) return true;
            return false;
        }

        // ---- at the wheel ----

        void Driving(ref HintSet set, ICrewInputSource src)
        {
            if (seat == null) seat = member.GetComponentInParent<VehicleSeat>();
            TruckVehicle truck = seat != null ? seat.vehicle : null;
            // The seat's own refusal ("No room to get out here") takes the get-out row's place
            // for its two seconds, as it did on the old HUD: the key was pressed, this is why
            // nothing happened. Loc.Prompt hands back the same string each frame (no rebuild).
            string notice = seat != null ? seat.ActiveNotice : null;
            if (notice != null) set.Add(new HintLine(Verb.Raw, default, raw: Loc.Prompt(notice), warn: true));
            else if (truck != null && !truck.CanDrive) set.Add(new HintLine(Verb.RampMoving, default, warn: true));
            else if (truck != null && truck.Body != null && truck.Body.linearVelocity.magnitude > seat.maxExitSpeed)
                set.Add(new HintLine(Verb.StopToGetOut, default, warn: true));
            else set.Add(new HintLine(Verb.GetOut, InputGlyphs.For(src, CrewButton.Interact)));
            set.Add(new HintLine(Verb.Steer, InputGlyphs.Move(src)));
            set.Add(new HintLine(Verb.Handbrake, InputGlyphs.For(src, CrewButton.Jump), Gesture.Hold));
            set.Add(new HintLine(Verb.Look, InputGlyphs.Look(src)));
        }

        // ---- the card ----

        static TargetCard Card(MovableObject mo, bool held, PlayerGrab grab)
        {
            int heldBy = -1;
            PlayerGrab h = mo.holder;
            if (!held && h != null && h != grab && h.isActiveAndEnabled && h.Held == mo) heldBy = h.Actor;
            return new TargetCard(mo, held, heldBy);
        }

        static MovableObject LookedAt(PlayerGrab grab)
        {
            if (grab.cam == null) return null;
            int n = Physics.RaycastNonAlloc(grab.cam.position, grab.cam.forward, hits, grab.grabRange,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(grab.transform)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; nearest = c; }
            }
            if (nearest == null) return null;
            var mo = nearest.GetComponentInParent<MovableObject>();
            return mo != null && !mo.destroyed ? mo : null;
        }
    }
}
