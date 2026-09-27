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
    //   MoveScale    float, the speed multiplier of the Locomotion and Crouch states: past the
    //                ground speed a clip was authored at, its cycle plays faster instead of the
    //                feet sliding (the walk is 4.5 m/s, the sprint 7.2, Crew_Run was made at 3.7)
    //   Crouch       bool
    //   Grounded     bool, false once the capsule has been off the ground for airDelay (a jump,
    //                a fall), not for the hundredths a stair step takes: the base layer's Air
    //                state, Crew_CrouchIdle with the body lifted by its hip drop, so the knees
    //                come up instead of the head going down; landing blends back through it
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
    //
    // After the Animator, the body crouches as low as the eyes went. The capsule goes from 1.80
    // to 1.00 m and the eyes to 0.80 (GREYBOX_SPEC), while Crew_CrouchIdle only lowers the hips
    // by 22 cm: the head stayed half a metre above the view and the shadow barely crouched. So
    // the hips go down and back, the spine folds until the head is at the eyes, the head comes
    // back up to look where you look, and the legs fold (LimbIK) to keep the feet where the clip
    // put them. It follows the capsule (PlayerController.CrouchAmount), so it cannot drift from
    // what the player really does. The arms are FirstPersonHands' business, after this.
    // The body always animates (AlwaysAnimate): your own forearms are drawn on its bones even
    // when nobody sees the body itself.
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

        [Header("Gait")]
        // The ground speeds the clips were authored at (author_clips.py: Crew_Run 3.71 m/s,
        // Crew_CrouchWalk 1.39 m/s), the top thresholds of the two blend trees.
        public float runClipSpeed = 3.7f;
        public float crouchWalkClipSpeed = 1.39f;
        // A sprint at 7.2 m/s would need the run cycle at 1.95: capped, the rest slides a little.
        public float maxMoveScale = 1.8f;

        [Header("Jump")]
        [Tooltip("Seconds off the ground before the body tucks: a step down a stair is not a jump.")]
        public float airDelay = 0.12f;
        [Tooltip("Metres the body rises in the Air state: Crew_CrouchIdle's hip drop, so the feet come up.")]
        public float airLift = 0.22f;
        public float airBlendSeconds = 0.12f;
        public float landSeconds = 0.05f;     // the lift goes at once on landing, the pose blends out: the knees give

        [Header("Crouch")]
        // A deep squat sat back over the heels: with these the spine folds about 57 degrees to
        // bring the head to the eyes (0.80 m), measured 2026-09-27.
        public float crouchHipDrop = 0.36f;
        public float crouchHipBack = 0.22f;
        [Tooltip("Metres from the head bone up to the eyes, standing (measured 0.068 on the crew body).")]
        public float eyeAboveHead = 0.07f;
        public float maxSpineFlex = 60f;
        [Tooltip("Share of the spine's fold the neck and head give back, so the face looks where you look.")]
        public float headCounter = 0.7f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MoveScaleId = Animator.StringToHash("MoveScale");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
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
                  PWear = 64, PWave = 128, PKnocked = 256, PMoveScale = 512, PGrounded = 1024;

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
        float airWeight;
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
        public float AirWeight => airWeight;
        public bool IsDown { get; private set; }
        public Animator Body => animator;

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
            // Posed after the Animator every frame (the crouch, the arms), and read for your own
            // forearms: a culled body would stop being rewritten and the adjustments would pile up.
            if (animator != null && animator.transform != transform) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
            bool crouched = walking && controller.crouching;
            if (hasSpeed)
            {
                Vector3 v = walking ? controller.Velocity : Vector3.zero;
                v.y = 0f;
                animator.SetFloat(SpeedId, v.magnitude, speedDampSeconds, dt);
                if ((has & PMoveScale) != 0)
                {
                    float clip = crouched ? crouchWalkClipSpeed : runClipSpeed;
                    float speed = animator.GetFloat(SpeedId);
                    animator.SetFloat(MoveScaleId, clip > 0.01f ? Mathf.Clamp(speed / clip, 1f, maxMoveScale) : 1f);
                }
            }
            if (hasCrouch) animator.SetBool(CrouchId, crouched);

            // Lying on the floor or getting up: no arms held out for a box, no cigarette.
            var baseNow = animator.GetCurrentAnimatorStateInfo(0);
            IsDown = baseNow.tagHash == DownTag || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).tagHash == DownTag);

            // Off the ground: a jump at once (it leaves the ground climbing), a fall after airDelay.
            bool airborne = walking && !IsDown && (has & PGrounded) != 0
                            && (controller.AirTime > airDelay || (controller.AirTime > 0f && controller.Velocity.y > 1f));
            if ((has & PGrounded) != 0) animator.SetBool(GroundedId, !airborne);
            float airSeconds = airborne ? airBlendSeconds : landSeconds;
            airWeight = Mathf.MoveTowards(airWeight, airborne ? 1f : 0f, airSeconds > 0.001f ? dt / airSeconds : 1f);

            DriveHands(dt, driving);
            FireTriggers(driving);
            HoldBodyWhileDown(dt);
            // Paused (timeScale 0) the Animator does not rewrite the pose: posing it again would
            // pile the crouch up frame after frame.
            if (dt > 0f) Posture(walking);

            if (carryLayer >= 0)
            {
                // A cigarette, a bottle or a grenade is held in one hand (FirstPersonHands puts
                // the body's right hand on it): the other arm hangs, it does not carry a box.
                bool inHand = pose != null && (pose.InHand || pose.HoldsSmall);
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

        // ---- after the Animator: the tuck and the crouch ----

        void Posture(bool walking)
        {
            Transform body = animator.transform;
            if (body == transform) return;
            // The tuck: the Air state plays the crouch, the body rises by its hip drop.
            if (!float.IsNaN(PlacedAtLocalY))
            {
                var p = body.localPosition;
                body.localPosition = new Vector3(p.x, PlacedAtLocalY + airWeight * airLift, p.z);
            }
            if (walking && !IsDown && animator.isHuman) CrouchAsLowAsTheEyes();
        }

        void CrouchAsLowAsTheEyes()
        {
            float k = controller.CrouchAmount;
            if (k <= 0.001f || controller.cam == null) return;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform lShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || spine == null || head == null || lThigh == null || lShin == null || lFoot == null
                || rThigh == null || rShin == null || rFoot == null) return;

            Vector3 up = transform.up, fwd = transform.forward, right = transform.right;
            Vector3 leftFoot = lFoot.position, rightFoot = rFoot.position;

            // Down and back, as a deep crouch sits back over the heels.
            hips.position += (-fwd * crouchHipBack - up * crouchHipDrop) * k;

            // The spine folds forward about its base until the head is at the eyes.
            Vector3 d = head.position - spine.position;
            float f = Vector3.Dot(d, fwd), u = Vector3.Dot(d, up);
            float r = Mathf.Sqrt(f * f + u * u);
            float want = Vector3.Dot(controller.cam.position - spine.position, up) - eyeAboveHead;
            if (r > 1e-3f && u > want)
            {
                float now = Mathf.Atan2(u, f) * Mathf.Rad2Deg;
                float then = Mathf.Asin(Mathf.Clamp(want / r, -1f, 1f)) * Mathf.Rad2Deg;
                float fold = Mathf.Clamp(now - then, 0f, maxSpineFlex * k);
                spine.rotation = Quaternion.AngleAxis(fold, right) * spine.rotation;
                float back = -fold * headCounter * 0.5f;
                if (neck != null) neck.rotation = Quaternion.AngleAxis(back, right) * neck.rotation;
                head.rotation = Quaternion.AngleAxis(back, right) * head.rotation;
            }

            // The knees go forward and a little out; the feet stay where the clip put them.
            LimbIK.Solve(lThigh, lShin, lFoot, leftFoot, fwd - right * 0.35f, 1f);
            LimbIK.Solve(rThigh, rShin, rFoot, rightFoot, fwd + right * 0.35f, 1f);
        }

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
                if (h == MoveScaleId && type == AnimatorControllerParameterType.Float) has |= PMoveScale;
                if (h == GroundedId && type == AnimatorControllerParameterType.Bool) has |= PGrounded;
            }
            carryLayer = animator.GetLayerIndex("Carry");
            carryWeight = carryLayer >= 0 ? animator.GetLayerWeight(carryLayer) : 0f;
            actionsLayer = animator.GetLayerIndex("Actions");
            actionsWeight = actionsLayer >= 0 ? animator.GetLayerWeight(actionsLayer) : 0f;
        }
    }
}
