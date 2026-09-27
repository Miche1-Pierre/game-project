using UnityEngine;

namespace Movers
{
    // Moves the crew body with the player, so the other player can read what you are doing:
    // walking, crouching behind the sofa, carrying something, smoking, drinking, throwing,
    // stuffing something in a pocket, putting on her dressing gown, getting blown off your feet.
    // Goes on the player root.
    //
    // It stands the body on the floor (its feet where the capsule's feet are, minus the skin the
    // CharacterController hovers on) and feeds the Animator what the controller built for the
    // slice expects (SLICE_ARCHITECTURE, animation contract, and CHARACTERS' Actions layer):
    //   Speed        float, m/s over the ground
    //   Crouch       bool
    //   layer "Carry", whose weight follows whether the hands are full (and are not busy holding
    //                something to the mouth, which the Actions layer shows instead)
    //   Smoking, Drinking            bool, while the held cigarette or bottle is being used
    //   SmokeTime, DrinkTime         float 0..1, the Motion Time of Crew_Smoke and Crew_Drink:
    //                                the drag and the gulps of the item itself (HeldPose), so
    //                                the hand is at the mouth when the ember glows
    //   Throw, Pocket, Wear, Wave    triggers, one-shots of the Actions layer
    //   KnockedDown                  trigger, the base layer's Crew_KnockedDown then Crew_GetUp
    //   layer "Actions", upper-body override whose weight rises while one of its states plays
    // Each is only written if the controller has it, so an older controller, or a body with no
    // controller at all, is left alone without a warning per frame.
    //
    // What happened comes from WorldEvents (ObjectThrown, ItemPocketed, ItemUnpocketed,
    // PlayerKnockedDown, PlayerSmoking, PlayerDrinking, all filtered to this player) and from the
    // state of what is held (HeldPose). Putting something on has no event: the body's worn set
    // is watched instead (CrewEquip).
    //
    // Seated in the truck (IsDriving), or with the walking controller switched off for any other
    // reason, the body is not walking: Speed and Crouch go to rest. A switched-off
    // CharacterController keeps reporting the last velocity it moved at, so reading it there
    // would play a run cycle in the driver's seat.
    [DisallowMultipleComponent]
    public sealed class CrewAnimator : MonoBehaviour
    {
        public Animator animator;          // left empty: the one under this player
        public bool standBodyOnFloor = true;
        public float speedDampSeconds = 0.1f;
        public float carryBlendSeconds = 0.2f;

        [Header("Actions layer")]
        public float actionBlendIn = 0.15f;
        public float actionBlendOut = 0.25f;
        // A PlayerSmoking or PlayerDrinking event keeps the pose on this long, but only for a use
        // HeldPose does not drive (a scripted drink, an item used from somewhere else). The item
        // in the hands is followed by its own state: its events are ignored here, or the late
        // breath after the button went up would hold an empty hand at the mouth for a second.
        public float eventHoldSeconds = 0.7f;

        [Header("Knocked down")]
        [Tooltip("Shove speed (m/s) from which the body is thrown off its feet, as KnockdownTumble's view.")]
        public float knockDownSpeed = 5.5f;
        public float uprightSeconds = 0.3f;   // the body turning back to the player's heading after getting up

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int SmokingId = Animator.StringToHash("Smoking");
        static readonly int DrinkingId = Animator.StringToHash("Drinking");
        static readonly int SmokeTimeId = Animator.StringToHash("SmokeTime");
        static readonly int DrinkTimeId = Animator.StringToHash("DrinkTime");
        static readonly int ThrowId = Animator.StringToHash("Throw");
        static readonly int PocketId = Animator.StringToHash("Pocket");
        static readonly int WearId = Animator.StringToHash("Wear");
        static readonly int WaveId = Animator.StringToHash("Wave");
        static readonly int KnockedDownId = Animator.StringToHash("KnockedDown");
        static readonly int EmptyState = Animator.StringToHash("Empty");
        static readonly int DownTag = Animator.StringToHash("Down");

        // Which parameters the bound controller has, one bit each.
        const int PSmoking = 1, PDrinking = 2, PSmokeTime = 4, PDrinkTime = 8, PThrow = 16, PPocket = 32,
                  PWear = 64, PWave = 128, PKnocked = 256;

        PlayerController controller;
        PlayerGrab grab;
        CrewMember member;
        HeldPose pose;
        CrewEquip equip;
        RuntimeAnimatorController bound;
        bool hasSpeed, hasCrouch;
        int has;
        int carryLayer = -1, actionsLayer = -1;
        float carryWeight, actionsWeight;
        float smokedUntil = -1f, drankUntil = -1f;
        HeldPose.Use lastWanted;
        int wornCount = -1;

        // Triggers raised by events, set on the Animator in LateUpdate (events can arrive
        // before the controller is bound).
        bool wantThrow, wantPocket, wantWear, wantWave, wantKnock;
        Vector3 knockCentre;

        // While the body lies on the floor and gets up, it keeps the heading it fell with: the
        // player can look round, the body stays where it fell. Then it turns back to the
        // player's heading over uprightSeconds.
        Quaternion bodyRest = Quaternion.identity;
        bool bodyRestKnown;
        bool holdingYaw, sawDown;
        float knockClock;
        Quaternion heldWorldRotation;
        float uprightClock = -1f;
        Quaternion uprightFrom;

        // The body's local height this component last stood it at, or NaN before it did. Lets
        // a test tell "placed at Play" from "saved there in the scene".
        public float PlacedAtLocalY { get; private set; } = float.NaN;

        public float ActionsWeight => actionsWeight;
        public float CarryWeight => carryWeight;
        public bool IsDown { get; private set; }

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            grab = GetComponent<PlayerGrab>();
            member = GetComponent<CrewMember>();
            pose = GetComponent<HeldPose>();
        }

        void OnEnable() { WorldEvents.Subscribe(OnWorldEvent); }
        void OnDisable() { WorldEvents.Unsubscribe(OnWorldEvent); }

        void Start()
        {
            // Added at runtime by the spawner on a scene that did not have one.
            if (member == null) member = GetComponent<CrewMember>();
            if (pose == null) pose = GetComponent<HeldPose>();
            Resolve();
            // PlayerController has measured its capsule by now (Awake).
            StandBodyOnFloor();
        }

        // Puts the body's feet on the floor line under the capsule. Done once at Start; public
        // so a test (or a body swapped at runtime) can ask for it again.
        public void StandBodyOnFloor()
        {
            if (!standBodyOnFloor || animator == null || controller == null || animator.transform == transform) return;
            var t = animator.transform;
            var p = t.localPosition;
            float y = controller.FeetHeight;
            t.localPosition = new Vector3(p.x, y, p.z);
            PlacedAtLocalY = y;
        }

        // A wave of the hand (Crew_Wave), for whoever wants the crew to greet: the title screen,
        // an emote if one is ever decided.
        public void Wave() { wantWave = true; }

        void Resolve()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null && equip == null) equip = animator.GetComponent<CrewEquip>();
            if (animator != null && !bodyRestKnown && animator.transform != transform)
            {
                bodyRest = animator.transform.localRotation;
                bodyRestKnown = true;
            }
        }

        void OnWorldEvent(WorldEvent e)
        {
            if (member == null) return;
            switch (e.type)
            {
                case WorldEventType.ObjectThrown:
                    if (e.instigator == member.index) wantThrow = true;
                    break;
                case WorldEventType.ItemPocketed:
                case WorldEventType.ItemUnpocketed:
                    if (e.instigator == member.index) wantPocket = true;
                    break;
                case WorldEventType.PlayerSmoking:
                    if (e.instigator == member.index && !InHands(e.subject)) smokedUntil = Time.time + eventHoldSeconds;
                    break;
                case WorldEventType.PlayerDrinking:
                    if (e.instigator == member.index && !InHands(e.subject)) drankUntil = Time.time + eventHoldSeconds;
                    break;
                case WorldEventType.PlayerKnockedDown:
                    if (ReferenceEquals(e.subject, member) && e.magnitude >= knockDownSpeed)
                    {
                        wantKnock = true;
                        knockCentre = e.position;
                    }
                    break;
            }
        }

        void LateUpdate()
        {
            if (animator == null) { Resolve(); if (animator == null) return; }
            if (!animator.isActiveAndEnabled || !animator.isInitialized) return;
            if (animator.runtimeAnimatorController != bound) Bind();
            if (bound == null) return;

            float dt = Time.deltaTime;
            bool driving = member != null && member.IsDriving;
            bool walking = controller != null && controller.isActiveAndEnabled && !driving;
            if (hasSpeed)
            {
                Vector3 v = walking ? controller.Velocity : Vector3.zero;
                v.y = 0f;
                animator.SetFloat(SpeedId, v.magnitude, speedDampSeconds, dt);
            }
            if (hasCrouch) animator.SetBool(CrouchId, walking && controller.crouching);

            // Lying on the floor or getting up: no arms held out for a box, no cigarette.
            var baseNow = animator.GetCurrentAnimatorStateInfo(0);
            IsDown = baseNow.tagHash == DownTag || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).tagHash == DownTag);

            DriveHands(dt, driving);
            FireTriggers(driving);
            HoldBodyWhileDown(dt);

            if (carryLayer >= 0)
            {
                bool inHand = pose != null && pose.InHand;
                float target = grab != null && grab.IsCarrying && !inHand && !IsDown ? 1f : 0f;
                float step = carryBlendSeconds > 0.001f ? dt / carryBlendSeconds : 1f;
                carryWeight = Mathf.MoveTowards(carryWeight, target, step);
                animator.SetLayerWeight(carryLayer, carryWeight);
            }
            if (actionsLayer >= 0)
            {
                // Up while one of its states plays or is being entered, down once it is back on
                // Empty. Its states blend among themselves; this only fades the layer as a whole.
                bool busy = !IsDown && !driving && (Busy(animator.GetCurrentAnimatorStateInfo(actionsLayer))
                            || (animator.IsInTransition(actionsLayer) && Busy(animator.GetNextAnimatorStateInfo(actionsLayer))));
                float target = busy ? 1f : 0f;
                float seconds = target > actionsWeight ? actionBlendIn : actionBlendOut;
                actionsWeight = Mathf.MoveTowards(actionsWeight, target, seconds > 0.001f ? dt / seconds : 1f);
                animator.SetLayerWeight(actionsLayer, actionsWeight);
            }
        }

        static bool Busy(AnimatorStateInfo s) => s.shortNameHash != EmptyState;

        // Whether an event's subject (the cigarette, the beer) is the thing HeldPose is following
        // in this player's hands, whose use it reports itself.
        bool InHands(Object subject)
        {
            if (pose == null || !pose.isActiveAndEnabled) return false;
            MovableObject current = pose.Current;
            var c = subject as Component;
            return current != null && c != null && c.gameObject == current.gameObject;
        }

        // The held item's use, as the Actions layer's bools and clocks.
        void DriveHands(float dt, bool driving)
        {
            // The intent, not the blended picture: the hand starts down the moment the button is up.
            HeldPose.Use use = pose != null ? pose.Wanted : HeldPose.Use.None;
            // The held use just stopped: nothing from it may keep the hand up (an event that got
            // in before HeldPose knew the item, for instance).
            if (lastWanted == HeldPose.Use.Smoke && use != HeldPose.Use.Smoke) smokedUntil = -1f;
            if (lastWanted == HeldPose.Use.Drink && use != HeldPose.Use.Drink) drankUntil = -1f;
            lastWanted = use;
            bool smoking = !driving && (use == HeldPose.Use.Smoke || Time.time < smokedUntil);
            bool drinking = !driving && (use == HeldPose.Use.Drink || Time.time < drankUntil);
            if ((has & PSmoking) != 0) animator.SetBool(SmokingId, smoking);
            if ((has & PDrinking) != 0) animator.SetBool(DrinkingId, drinking);
            if (pose != null && (has & PSmokeTime) != 0 && use == HeldPose.Use.Smoke) animator.SetFloat(SmokeTimeId, pose.SmokePhase);
            if (pose != null && (has & PDrinkTime) != 0 && use == HeldPose.Use.Drink) animator.SetFloat(DrinkTimeId, pose.DrinkPhase);

            // Putting a piece on or taking one off: the count of worn slots changes.
            if (equip != null && equip.isActiveAndEnabled)
            {
                int n = 0;
                for (int i = 0; i <= (int)EquipSlot.Feet; i++)
                    if (equip.IsWearing((EquipSlot)i)) n++;
                if (wornCount >= 0 && n != wornCount) wantWear = true;
                wornCount = n;
            }
        }

        void FireTriggers(bool driving)
        {
            if (wantKnock && !driving && (has & PKnocked) != 0)
            {
                animator.SetTrigger(KnockedDownId);
                // Crew_KnockedDown falls on its back, so the body faces the blast to be thrown
                // away from it, and keeps that heading while it lies there (HoldBodyWhileDown).
                // Worked out here, not when the event came: the blast's own Explosion event is
                // raised in the same frame, before or after this player's.
                Vector3 away = AwayFromBlast(knockCentre);
                if (animator.transform != transform && away.sqrMagnitude > 1e-4f)
                {
                    holdingYaw = true;
                    sawDown = false;
                    knockClock = 0f;
                    uprightClock = -1f;
                    heldWorldRotation = Quaternion.LookRotation(-away, Vector3.up) * bodyRest;
                }
            }
            if (!driving)
            {
                if (wantThrow && (has & PThrow) != 0) animator.SetTrigger(ThrowId);
                if (wantPocket && (has & PPocket) != 0) animator.SetTrigger(PocketId);
                if (wantWear && (has & PWear) != 0) animator.SetTrigger(WearId);
                if (wantWave && (has & PWave) != 0) animator.SetTrigger(WaveId);
            }
            wantKnock = wantThrow = wantPocket = wantWear = wantWave = false;
        }

        void HoldBodyWhileDown(float dt)
        {
            Transform body = animator.transform;
            if (body == transform) return;
            if (holdingYaw)
            {
                body.rotation = heldWorldRotation;
                knockClock += dt;
                if (IsDown) sawDown = true;
                // Up again (or the controller never went down, half a second on): turn back.
                else if (sawDown || knockClock > 0.5f)
                {
                    holdingYaw = false;
                    uprightFrom = body.localRotation;
                    uprightClock = 0f;
                }
                return;
            }
            if (uprightClock >= 0f)
            {
                uprightClock += dt;
                float k = Mathf.SmoothStep(0f, 1f, uprightClock / Mathf.Max(0.01f, uprightSeconds));
                body.localRotation = Quaternion.Slerp(uprightFrom, bodyRest, k);
                if (k >= 1f) uprightClock = -1f;
            }
        }

        Vector3 AwayFromBlast(Vector3 victimCentre)
        {
            for (int i = 0; i < WorldEvents.RecentCount && i < 16; i++)
            {
                var e = WorldEvents.GetRecent(i);
                if (Time.time - e.time > 0.25f) break;
                if (e.type != WorldEventType.Explosion) continue;
                Vector3 d = victimCentre - e.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) return d.normalized;
                break;
            }
            // No blast on record (a debug knock): thrown backwards.
            Vector3 back = -transform.forward;
            back.y = 0f;
            return back.normalized;
        }

        // Once per controller: what it offers is read here, never per frame (the parameter list
        // is a fresh array every time it is asked for).
        void Bind()
        {
            bound = animator.runtimeAnimatorController;
            hasSpeed = hasCrouch = false;
            has = 0;
            carryLayer = actionsLayer = -1;
            if (bound == null) return;

            var ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                int h = ps[i].nameHash;
                var type = ps[i].type;
                if (h == SpeedId && type == AnimatorControllerParameterType.Float) hasSpeed = true;
                if (h == CrouchId && type == AnimatorControllerParameterType.Bool) hasCrouch = true;
                if (h == SmokingId && type == AnimatorControllerParameterType.Bool) has |= PSmoking;
                if (h == DrinkingId && type == AnimatorControllerParameterType.Bool) has |= PDrinking;
                if (h == SmokeTimeId && type == AnimatorControllerParameterType.Float) has |= PSmokeTime;
                if (h == DrinkTimeId && type == AnimatorControllerParameterType.Float) has |= PDrinkTime;
                if (h == ThrowId && type == AnimatorControllerParameterType.Trigger) has |= PThrow;
                if (h == PocketId && type == AnimatorControllerParameterType.Trigger) has |= PPocket;
                if (h == WearId && type == AnimatorControllerParameterType.Trigger) has |= PWear;
                if (h == WaveId && type == AnimatorControllerParameterType.Trigger) has |= PWave;
                if (h == KnockedDownId && type == AnimatorControllerParameterType.Trigger) has |= PKnocked;
            }
            carryLayer = animator.GetLayerIndex("Carry");
            carryWeight = carryLayer >= 0 ? animator.GetLayerWeight(carryLayer) : 0f;
            actionsLayer = animator.GetLayerIndex("Actions");
            actionsWeight = actionsLayer >= 0 ? animator.GetLayerWeight(actionsLayer) : 0f;
        }
    }
}
