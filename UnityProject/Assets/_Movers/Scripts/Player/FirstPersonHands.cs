using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Your own forearms and hands, seen from inside your head. Goes on the player root, next to
    // HeldPose (CrewSetup adds it).
    //
    // FirstPersonBody keeps the whole crew body out of its owner's view, so until now a player
    // saw nothing of themselves: things floated in front of the eyes. These are a separate pair
    // of low-poly forearms (sleeve in the crew colour, bare hands) under the player's camera,
    // drawn by that camera only. Every other camera, the other half of the split screen and the
    // grandmother's mirror included, keeps seeing the full body and never these (the same
    // per-camera switch FirstPersonBody and HandHeldProp use, with the same small stack for a
    // camera rendered inside another).
    //
    // What they do, all in the picture only (nothing here touches physics or the carry):
    //   at rest       both hands low in the corners of the view, swaying with the walk and
    //                 lagging a little behind the look;
    //   grabbing      a snatch forward when the button finds nothing, a reach toward what it did;
    //   carrying      a palm on each side of the carried thing, the arms stretched toward it and
    //                 stopped at arm's length (the carry holds things at 1 to 3 m, out of reach);
    //   a small usable (cigarette, beer, grenade) is held in the right hand, close in front
    //                 (HeldPose draws the item there, the hand closes on it): the cigarette goes
    //                 to the mouth for each drag, the bottle tips back, the grenade winds up and
    //                 the left hand yanks the pin;
    //   throwing      a swing through, one hand for a small thing, both for a big one;
    //   pocketing     a dip to the hip.
    //
    // The rig (the hands' parent under the camera) carries the sway, and HeldPose uses it as the
    // frame of its in-hand poses, so the held item sways with the hand holding it.
    //
    // With a crew body (every player of the house), these arms ARE the body's arms: one skeleton
    // for your own view, your shadow and the other player's view, so none of them can disagree.
    //   - The body's Animator moves the arms (idle, walk, run, the crouch, the jump, and the
    //     Actions layer's throw, pocket, wear, smoke and drink).
    //   - What the hands hold or reach for (the carry, the small usable in the right hand, the
    //     pin, the snatch) is worked out here as before, then put on the body's arms with LimbIK:
    //     your shadow holds the cigarette where you see it held.
    //   - These forearms, hands and upper arms are then laid on the body's bones, for your own
    //     camera only (the body itself is hidden from it, FirstPersonBody).
    // So at rest the hands hang by the thighs, out of view unless you look down, and they swing
    // with the run as the shadow does; they come into view when they hold or reach. The throw
    // and pocket gestures below are the Actions layer's clips on the body then, not a second
    // animation here. A player with no body (the tutorial's capsule) keeps the arms in the view's
    // corners, as before.
    //
    // LateUpdate after CameraShake (1000) and ViewOffset (1010): the camera already has this
    // frame's picture offsets, so the hands and the item HeldPose draws at render agree. It also
    // comes after CrewAnimator, which has crouched the body, so the arms start from the final torso.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1020)]
    public sealed class FirstPersonHands : MonoBehaviour
    {
        const string RigName = "FirstPersonHands";

        [Header("Look")]
        public Color skin = new Color(0.86f, 0.64f, 0.5f);

        [Header("Arms (the view's own space, metres)")]
        public Vector3 shoulder = new Vector3(0.17f, -0.27f, -0.1f);   // the right one, the left mirrors it
        public float upperArmLength = 0.3f;
        public float forearmLength = 0.3f;
        [Tooltip("Where the elbows go when the arm bends: down, out and a little back (right arm, the left mirrors it).")]
        public Vector3 elbowPole = new Vector3(0.5f, -1f, -0.3f);

        [Header("At rest")]
        [Tooltip("The right wrist across the view (-1..1), the left one mirrored.")]
        public Vector2 restScreen = new Vector2(0.6f, -0.95f);
        public float restDepth = 0.4f;
        public Vector2 restSideMetres = new Vector2(0.1f, 0.22f);   // min, max
        public Vector2 restDropMetres = new Vector2(0.12f, 0.26f);

        [Header("Motion")]
        public float followSeconds = 0.07f;
        public float bobMetres = 0.011f;
        public float strideMetres = 1.5f;
        public float walkSwingMetres = 0.012f;
        public float lookLagMaxDegrees = 4f;
        public float lookLagSeconds = 0.012f;    // degrees of lag per degree-per-second of turn
        [Tooltip("Metres in front of the eyes within which a wall pushes the hands back and down, so they do not sink into it.")]
        public float wallReach = 0.6f;
        public float maxWallPull = 0.22f;

        [Header("Gestures (seconds)")]
        public float throwSeconds = 0.42f;
        public float snatchSeconds = 0.38f;
        public float pocketSeconds = 0.45f;

        [Header("On the body")]
        [Tooltip("Seconds for a hand to go from the body's animation to what it holds, and back.")]
        public float reachSeconds = 0.14f;
        public float letGoSeconds = 0.2f;

        enum Gesture { None, Throw, Shove, Snatch, Pocket }

        // One hand's target for this frame, in the rig's space.
        struct Target
        {
            public Vector3 wrist;
            public Quaternion rotation;
            public Vector4 fingers;   // index, middle, ring, little: 0 straight, 1 closed
            public float thumb;
            public bool exact;        // on an item HeldPose draws: no lag, no keeping in view
        }

        sealed class Hand
        {
            public FirstPersonArm arm;
            public float side;
            public Vector3 wrist, velocity;
            public Quaternion rotation = Quaternion.identity;
            public Vector4 fingers = new Vector4(0.4f, 0.4f, 0.4f, 0.4f);
            public float thumb = 0.3f;
            public float exact;   // 0..1, how locked to the item it is
            public bool placed;
            // On a body: its arm, and how far what the hand does overrides the body's animation.
            public Transform upper, lower, end;
            public float weight;
            // The hand bone's axes turned into FirstPersonArm's hand space (+Z along the fingers,
            // +Y the back of the hand), read the way HandSockets reads them: the fingers toward
            // the bone's "_end" child, the palm the bone's +X on the right and -X on the left.
            public Quaternion boneToHand = Quaternion.identity;
        }

        CrewMember member;
        PlayerGrab grab;
        HeldPose pose;
        CrewInput input;
        PlayerController controller;
        CrewAnimator crew;
        KnockdownTumble tumble;
        Camera eyes;
        Animator body;

        Transform rig;
        readonly Hand right = new Hand { side = 1f };
        readonly Hand left = new Hand { side = -1f };
        readonly List<Renderer> renderers = new List<Renderer>(32);
        readonly List<Mesh> meshes = new List<Mesh>(32);
        Material sleeveMat, cuffMat, skinMat;
        Color builtColor;

        // Per camera: shown to its own camera only.
        readonly Camera[] stack = new Camera[4];
        int depth;
        bool visible;
        bool showing;

        // The view this frame.
        float tanV = 0.577f, tanH = 1.026f;

        // Rig motion.
        float stride, bobWeight;
        float lagYaw, lagPitch;
        float lastYaw, lastPitch;
        bool lookKnown;
        float wallPull;

        // What is in the hands.
        MovableObject lastHeld;
        bool lastSmall;
        Bounds heldBounds;          // in the held object's own space
        MovableObject boundsOf;

        Gesture gesture;
        float gestureClock = -1f;

        readonly RaycastHit[] wallHits = new RaycastHit[16];
        readonly List<Renderer> boundsScratch = new List<Renderer>(16);
        readonly List<Collider> colliderScratch = new List<Collider>(8);

        public Transform Rig => rig;
        public bool Showing => showing;
        // The hands are the body's arms (a humanoid crew body), not the view's own.
        public bool OnBody => body != null && right.end != null && left.end != null;
        // How far each hand is doing something else than the body's animation (0..1).
        public float RightWeight => right.weight;
        public float LeftWeight => left.weight;

        void Awake()
        {
            Resolve();
        }

        void Resolve()
        {
            if (member == null) member = GetComponent<CrewMember>();
            if (grab == null) grab = GetComponent<PlayerGrab>();
            if (pose == null) pose = GetComponent<HeldPose>();
            if (input == null) input = GetComponent<CrewInput>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (crew == null) crew = GetComponent<CrewAnimator>();
            if (tumble == null) tumble = GetComponent<KnockdownTumble>();
            if (eyes == null)
            {
                if (member != null && member.View != null) eyes = member.View;
                else if (controller != null && controller.cam != null) eyes = controller.cam.GetComponent<Camera>();
            }
        }

        // The humanoid body under this player, and its two arms. CrewSpawner may have swapped the
        // body before anything woke, so it is looked up here, not remembered from the scene.
        void ResolveBody()
        {
            body = null;
            right.upper = right.lower = right.end = null;
            left.upper = left.lower = left.end = null;
            var a = GetComponentInChildren<Animator>(true);
            if (a == null || !a.isHuman || a.avatar == null || a.transform == transform) return;
            right.upper = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            right.lower = a.GetBoneTransform(HumanBodyBones.RightLowerArm);
            right.end = a.GetBoneTransform(HumanBodyBones.RightHand);
            left.upper = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            left.lower = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            left.end = a.GetBoneTransform(HumanBodyBones.LeftHand);
            if (right.upper != null && right.lower != null && right.end != null
                && left.upper != null && left.lower != null && left.end != null) body = a;
            if (body == null) return;
            right.boneToHand = BoneToHand(right.end, 1f);
            left.boneToHand = BoneToHand(left.end, -1f);
        }

        static Quaternion BoneToHand(Transform hand, float side)
        {
            Vector3 finger = Vector3.back;
            for (int i = 0; i < hand.childCount; i++)
            {
                Transform c = hand.GetChild(i);
                if (c.name.EndsWith("_end") && c.localPosition.sqrMagnitude > 1e-6f) { finger = c.localPosition.normalized; break; }
            }
            Vector3 back = Vector3.ProjectOnPlane(Vector3.right * -side, finger);
            if (back.sqrMagnitude < 1e-6f) back = Vector3.ProjectOnPlane(Vector3.up, finger);
            return Quaternion.LookRotation(finger, back.normalized);
        }

        void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
            WorldEvents.Subscribe(OnWorldEvent);
        }

        void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            WorldEvents.Unsubscribe(OnWorldEvent);
            SetVisible(false);
            depth = 0;
            right.placed = left.placed = false;
            right.weight = left.weight = 0f;
            if (pose != null && pose.handsFrame == rig) pose.handsFrame = null;
        }

        void Start()
        {
            Resolve();
            Build();
        }

        void OnDestroy()
        {
            if (rig != null) Destroy(rig.gameObject);
            for (int i = 0; i < meshes.Count; i++) ItemArt.Kill(meshes[i]);
            meshes.Clear();
            ItemArt.Kill(sleeveMat);
            ItemArt.Kill(cuffMat);
            ItemArt.Kill(skinMat);
        }

        void Build()
        {
            if (rig != null || eyes == null) return;
            // A player copied from another (CrewSpawner clones the first) may have brought that
            // player's rig along under its camera.
            var old = eyes.transform.Find(RigName);
            if (old != null) Destroy(old.gameObject);

            rig = new GameObject(RigName).transform;
            rig.SetParent(eyes.transform, false);

            builtColor = member != null ? member.color : new Color(0.85f, 0.2f, 0.2f);
            sleeveMat = Matte(builtColor);
            cuffMat = Matte(Color.Lerp(builtColor, Color.black, 0.22f));
            skinMat = Matte(skin);

            ResolveBody();
            right.arm = BuildArm(right);
            left.arm = BuildArm(left);
            visible = false;
            if (pose != null) pose.handsFrame = rig;
        }

        // Sized on the body's own bones when there is one: they are laid on them.
        FirstPersonArm BuildArm(Hand h)
        {
            float fore = 0f, upper = 0f;
            if (OnBody)
            {
                fore = Vector3.Distance(h.lower.position, h.end.position);
                upper = Vector3.Distance(h.upper.position, h.lower.position);
            }
            return new FirstPersonArm(rig, h.side, sleeveMat, cuffMat, skinMat, meshes, renderers, fore, upper);
        }

        static Material Matte(Color c)
        {
            var m = ItemArt.Mat(c, Color.black);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
            return m;
        }

        // ---- per camera ----

        void OnCameraPreCull(Camera cam)
        {
            if (depth < stack.Length) stack[depth] = cam;
            depth++;
            Apply(cam);
        }

        void OnCameraPostRender(Camera cam)
        {
            if (depth > 0) depth--;
            if (depth == 0) SetVisible(false);
            else if (depth <= stack.Length) Apply(stack[depth - 1]);
        }

        void Apply(Camera cam)
        {
            SetVisible(showing && cam != null && cam == eyes);
        }

        void SetVisible(bool on)
        {
            if (on == visible) return;
            visible = on;
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].forceRenderingOff = !on;
        }

        // ---- events ----

        void OnWorldEvent(WorldEvent e)
        {
            if (member == null || e.instigator != member.index) return;
            // On a body the throw and the pocket are the Actions layer's clips, seen on its arm.
            if (OnBody) return;
            switch (e.type)
            {
                case WorldEventType.ObjectThrown:
                    // The hands were already emptied by the throw: what they held is the last frame's.
                    Begin(lastSmall ? Gesture.Throw : Gesture.Shove);
                    break;
                case WorldEventType.ItemPocketed:
                case WorldEventType.ItemUnpocketed:
                    Begin(Gesture.Pocket);
                    break;
            }
        }

        void Begin(Gesture g)
        {
            gesture = g;
            gestureClock = 0f;
        }

        // ---- the look sampled before the picture offsets (ViewOffset, CameraShake) go on ----

        void Update()
        {
            if (eyes == null) return;
            float dt = Time.deltaTime;
            float yaw = transform.eulerAngles.y;
            float pitch = eyes.transform.localEulerAngles.x;
            if (lookKnown && dt > 1e-5f)
            {
                float yawSpeed = Mathf.DeltaAngle(lastYaw, yaw) / dt;
                float pitchSpeed = Mathf.DeltaAngle(lastPitch, pitch) / dt;
                float k = 1f - Mathf.Exp(-dt * 12f);
                lagYaw = Mathf.Lerp(lagYaw, Mathf.Clamp(-yawSpeed * lookLagSeconds, -lookLagMaxDegrees, lookLagMaxDegrees), k);
                lagPitch = Mathf.Lerp(lagPitch, Mathf.Clamp(-pitchSpeed * lookLagSeconds, -lookLagMaxDegrees, lookLagMaxDegrees), k);
            }
            lastYaw = yaw;
            lastPitch = pitch;
            lookKnown = true;
        }

        // ---- the pose ----

        void LateUpdate()
        {
            // Hidden between renders, whatever the last one left (as FirstPersonBody does).
            depth = 0;
            SetVisible(false);

            if (rig == null) { Resolve(); Build(); if (rig == null) return; }
            if (pose != null && pose.handsFrame != rig) pose.handsFrame = rig;   // switched off and on again
            if (member != null && member.color != builtColor) Recolour(member.color);

            bool driving = member != null && member.IsDriving;
            showing = !driving && eyes != null && eyes.isActiveAndEnabled && rig.gameObject.activeInHierarchy;
            // On a body the arms are posed even when this player's own view is off (online, the
            // other machine's player has no camera here): the body everyone else sees still
            // holds what it holds. Only the drawing of these arms needs the view.
            bool posing = showing || (!driving && eyes != null && OnBody);
            if (!posing)
            {
                right.placed = left.placed = false;
                right.weight = left.weight = 0f;
                lookKnown = false;
                return;
            }

            float dt = Time.deltaTime;
            float fov = eyes.fieldOfView * 0.5f * Mathf.Deg2Rad;
            tanV = Mathf.Tan(fov);
            tanH = tanV * Mathf.Max(0.1f, eyes.aspect);

            MovableObject held = grab != null && grab.isActiveAndEnabled ? grab.Held : null;
            bool small = held != null && pose != null && pose.HoldsSmall && ReferenceEquals(pose.Current, held);

            MoveRig(dt, held);

            // Grabbing at nothing: a snatch at the air.
            if (input != null && held == null && lastHeld == null && input.Down(CrewButton.Grab)) Begin(Gesture.Snatch);
            if (gestureClock >= 0f)
            {
                gestureClock += dt;
                if (gestureClock >= GestureSeconds(gesture)) { gestureClock = -1f; gesture = Gesture.None; }
            }

            if (OnBody) PoseOnBody(dt, held, small);
            else PoseInView(dt, held, small);

            lastHeld = held;
            lastSmall = small;
        }

        // ---- with a body: what the hands do goes on its arms, and the arms are drawn on it ----

        void PoseOnBody(float dt, MovableObject held, bool small)
        {
            Target r = Rest(1f), l = Rest(-1f);
            float rWant = 0f, lWant = 0f;
            bool rEased = false, lEased = false;   // the gesture's envelope already eases it
            // Lying on the floor, getting up or tumbling: the Animator's fall, nothing held out.
            bool free = (crew == null || !crew.IsDown) && (tumble == null || !tumble.IsTumbling);
            if (free)
            {
                if (held != null && !small)
                {
                    Carry(held, out l, out r);
                    rWant = lWant = 1f;
                }
                else if (small && pose.TryGetPicture(eyes, out Vector3 at, out Quaternion turn))
                {
                    r = InHand(at, turn);
                    rWant = 1f;
                    float tug = pose.PinTug;
                    if (tug > 0f) { l = PinYank(at); lWant = tug; lEased = true; }
                }
                if (gestureClock >= 0f && gesture == Gesture.Snatch)
                {
                    r = SnatchTarget();
                    rWant = Envelope(0.12f);
                    rEased = true;
                }
            }

            Follow(right, r, rWant, rEased, dt);
            Follow(left, l, lWant, lEased, dt);
            // Paused, the Animator does not rewrite the arms: solving again would add up.
            if (dt > 0f)
            {
                PutOnBody(right);
                PutOnBody(left);
            }
            LayOnBody(right);
            LayOnBody(left);
        }

        // The hand's target, sprung as before, and how much it overrides the animation. A reach
        // starts from where the body's hand really is.
        void Follow(Hand h, Target t, float want, bool eased, float dt)
        {
            bool wasFree = h.weight <= 0f || !h.placed;
            if (eased) h.weight = Mathf.Clamp01(want);
            else
            {
                float seconds = want > h.weight ? reachSeconds : letGoSeconds;
                h.weight = Mathf.MoveTowards(h.weight, want, seconds > 0.001f ? dt / seconds : 1f);
            }
            h.exact = Mathf.MoveTowards(h.exact, t.exact ? 1f : 0f, dt / 0.15f);

            float step = dt * 7f;
            h.fingers = Vector4.MoveTowards(h.fingers, t.fingers, step);
            h.thumb = Mathf.MoveTowards(h.thumb, t.thumb, step);

            if (wasFree)
            {
                h.wrist = rig.InverseTransformPoint(h.end.position);
                h.rotation = Quaternion.Inverse(rig.rotation) * h.end.rotation * BoneToHand(h);
                h.velocity = Vector3.zero;
                h.placed = true;
            }
            if (h.weight <= 0f || dt <= 0f) return;

            float follow = gestureClock >= 0f ? 0.035f : followSeconds;
            Vector3 sprung = Vector3.SmoothDamp(h.wrist, t.wrist, ref h.velocity, follow, Mathf.Infinity, dt);
            Quaternion turned = Quaternion.Slerp(h.rotation, t.rotation, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, follow * 0.8f)));
            // On an item HeldPose draws, the hand is exactly where the item is.
            h.wrist = Vector3.Lerp(sprung, t.wrist, h.exact);
            h.rotation = Quaternion.Slerp(turned, t.rotation, h.exact);
            if (h.exact >= 1f) h.velocity = Vector3.zero;
        }

        // The body's arm to the hand's target, by h.weight: the elbow down, out and a little back,
        // in the body's frame (not the view's, or looking up would lift the elbows).
        void PutOnBody(Hand h)
        {
            if (h.weight <= 0f) return;
            Vector3 target = rig.TransformPoint(h.wrist);
            Quaternion turn = rig.rotation * h.rotation * Quaternion.Inverse(BoneToHand(h));
            Vector3 pole = transform.TransformDirection(new Vector3(elbowPole.x * h.side, elbowPole.y, elbowPole.z));
            Quaternion animated = h.end.rotation;
            LimbIK.Solve(h.upper, h.lower, h.end, target, pole, h.weight);
            h.end.rotation = Quaternion.Slerp(animated, turn, h.weight);
        }

        // These arms on the body's bones, in the rig's space. On an item the hand stays on it even
        // where the body's shorter arm falls a few centimetres short.
        void LayOnBody(Hand h)
        {
            if (h.arm == null) return;
            Vector3 shoulderAt = rig.InverseTransformPoint(h.upper.position);
            Vector3 elbowAt = rig.InverseTransformPoint(h.lower.position);
            Vector3 wristAt = rig.InverseTransformPoint(h.end.position);
            Quaternion handAt = Quaternion.Inverse(rig.rotation) * h.end.rotation * BoneToHand(h);
            float k = h.exact * h.weight;
            if (k > 0f)
            {
                wristAt = Vector3.Lerp(wristAt, h.wrist, k);
                handAt = Quaternion.Slerp(handAt, h.rotation, k);
            }
            h.arm.Curl(h.fingers.x, h.fingers.y, h.fingers.z, h.fingers.w, h.thumb);
            h.arm.Place(wristAt, handAt, elbowAt);
            h.arm.PlaceUpper(shoulderAt, elbowAt, handAt * Vector3.up);
        }

        static Quaternion BoneToHand(Hand h) => h.boneToHand;

        // ---- without a body: the arms live in the view, as they did ----

        void PoseInView(float dt, MovableObject held, bool small)
        {
            Target r = Rest(1f), l = Rest(-1f);
            if (held != null && !small)
            {
                Carry(held, out l, out r);
            }
            else if (small && pose.TryGetPicture(eyes, out Vector3 at, out Quaternion turn))
            {
                r = InHand(at, turn);
                float tug = pose.PinTug;
                if (tug > 0f) l = Blend(l, PinYank(at), tug);
            }
            ApplyGesture(ref r, ref l);

            right.exact = Mathf.MoveTowards(right.exact, r.exact ? 1f : 0f, dt / 0.15f);
            left.exact = Mathf.MoveTowards(left.exact, l.exact ? 1f : 0f, dt / 0.15f);
            Drive(right, r, dt);
            Drive(left, l, dt);
        }

        void Recolour(Color c)
        {
            builtColor = c;
            if (sleeveMat != null) sleeveMat.color = c;
            if (cuffMat != null) cuffMat.color = Color.Lerp(c, Color.black, 0.22f);
        }

        // The rig: the walk's bob, the look's lag and a wall's push, all in one small offset.
        void MoveRig(float dt, MovableObject held)
        {
            Vector3 v = controller != null ? controller.Velocity : Vector3.zero;
            v.y = 0f;
            float speed = v.magnitude;
            bobWeight = Mathf.MoveTowards(bobWeight, Mathf.Clamp01(speed / 4.5f), dt * 3f);
            // One cycle per two steps.
            stride += speed * dt / Mathf.Max(0.3f, strideMetres) * Mathf.PI * 2f;
            if (stride > 1000f) stride -= Mathf.PI * 200f;

            Vector3 offset = new Vector3(Mathf.Sin(stride) * bobMetres * 0.6f,
                                         -Mathf.Abs(Mathf.Cos(stride)) * bobMetres, 0f) * bobWeight;
            offset.y += Mathf.Sin(Time.time * 1.7f) * 0.0025f;   // breathing

            float pull = WallDistancePull(held);
            wallPull = Mathf.MoveTowards(wallPull, pull, dt * 1.5f);
            offset += new Vector3(0f, -wallPull * 0.45f, -wallPull * 0.8f);

            rig.localPosition = offset;
            rig.localRotation = Quaternion.Euler(lagPitch, lagYaw, -lagYaw * 0.5f);
        }

        float WallDistancePull(MovableObject held)
        {
            Transform eye = eyes.transform;
            int n = Physics.SphereCastNonAlloc(eye.position, 0.09f, eye.forward, wallHits, wallReach,
                                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = wallReach;
            Transform heldT = held != null ? held.transform : null;
            for (int i = 0; i < n; i++)
            {
                var c = wallHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (heldT != null && c.transform.IsChildOf(heldT)) continue;
                // Started inside it: SphereCast reports those at distance 0 with no point. Not a wall ahead.
                if (wallHits[i].distance <= 0f) continue;
                if (wallHits[i].distance < nearest) nearest = wallHits[i].distance;
            }
            return Mathf.Min(maxWallPull, wallReach - nearest);
        }

        // ---- targets ----

        // A point across the view (sx, sy in -1..1) at a depth, in metres in the view's own space,
        // kept within [min, max] metres to the side and below: a very wide or very narrow view
        // (split screen, stacked) must not pull the arm off the body. Public: HeldPose places the
        // things held in the hand the same way.
        public static Vector3 InView(Camera cam, float sx, float sy, float z, Vector2 side, Vector2 drop)
        {
            float tv = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float th = tv * Mathf.Max(0.1f, cam.aspect);
            float x = Mathf.Clamp(Mathf.Abs(sx) * z * th, side.x, side.y) * Mathf.Sign(sx);
            float y = Mathf.Clamp(Mathf.Abs(sy) * z * tv, drop.x, drop.y) * (sy > 0f ? 1f : -1f);
            return new Vector3(x, y, z);
        }

        Target Rest(float s)
        {
            Vector3 w = InView(eyes, restScreen.x * s, restScreen.y, restDepth, restSideMetres, restDropMetres);
            // The hands swing a little against each other with the walk.
            w.z += Mathf.Sin(stride) * walkSwingMetres * bobWeight * s;
            return new Target
            {
                wrist = w,
                // Relaxed: fingers forward and a little in, palms turned in and down.
                rotation = Quaternion.LookRotation(new Vector3(-0.15f * s, 0.35f, 1f), new Vector3(0.85f * s, 0.5f, 0f)),
                fingers = new Vector4(0.28f, 0.33f, 0.4f, 0.46f),
                thumb = 0.2f,
            };
        }

        // A palm on each side of the carried thing, as the eyes see it: a little below its middle,
        // on its near half. Out of reach (the usual case) the arms stretch toward those points.
        void Carry(MovableObject held, out Target l, out Target r)
        {
            if (!ReferenceEquals(boundsOf, held)) { heldBounds = LocalBounds(held); boundsOf = held; }

            Matrix4x4 toRig = rig.worldToLocalMatrix * held.transform.localToWorldMatrix;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = -min;
            Vector3 c = heldBounds.center, e = heldBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = toRig.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            float y = Mathf.Lerp(min.y, max.y, 0.35f);
            float z = Mathf.Lerp(min.z, max.z, 0.3f);
            r = OnSide(new Vector3(max.x, y, z), 1f);
            l = OnSide(new Vector3(min.x, y, z), -1f);
        }

        Target OnSide(Vector3 contact, float s)
        {
            Vector3 sh = Shoulder(s);
            Vector3 toward = contact - sh;
            bool reached = toward.magnitude <= Reach;
            Vector3 along = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.forward;
            Vector3 outward = new Vector3(s, 0f, 0f);
            Vector3 up = outward - along * Vector3.Dot(outward, along);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.up;
            Quaternion rot = Quaternion.LookRotation(along, up.normalized);
            float curl = reached ? 0.35f : 0.18f;
            return new Target
            {
                wrist = contact - rot * FirstPersonArm.PalmPoint(s),
                rotation = rot,
                fingers = new Vector4(curl, curl, curl + 0.05f, curl + 0.1f),
                thumb = 0.15f,
            };
        }

        // The right hand on the small usable HeldPose draws (the item's pose, in world space).
        Target InHand(Vector3 atWorld, Quaternion turnWorld)
        {
            Vector3 p = rig.InverseTransformPoint(atWorld);
            Quaternion q = Quaternion.Inverse(rig.rotation) * turnWorld;
            Vector3 axis = q * Vector3.up;

            var cigarette = pose.HeldCigarette;
            if (cigarette != null)
            {
                // Between the index and middle fingers near the filter, the lit end out of the
                // back of the hand. The fingers point forward while it is held (palm down, the
                // cigarette standing up), across the mouth to the left while it is at the lips.
                const float pinchCurl = 0.14f;
                Vector3 grip = p + axis * (-cigarette.length * 0.5f + CigaretteItem.PinchFromFilter);
                Vector3 want = Vector3.Slerp(new Vector3(-0.3f, 0.2f, 1f).normalized,
                                             new Vector3(-1f, 0.35f, 0.1f).normalized, pose.AtLips);
                Vector3 fingers = want - axis * Vector3.Dot(want, axis);
                if (fingers.sqrMagnitude < 1e-6f) fingers = Vector3.forward - axis * Vector3.Dot(Vector3.forward, axis);
                Quaternion rot = Quaternion.LookRotation(fingers.normalized, axis);
                return new Target
                {
                    wrist = grip - rot * FirstPersonArm.PinchPoint(1f, pinchCurl),
                    rotation = rot,
                    fingers = new Vector4(pinchCurl, pinchCurl, 0.55f, 0.65f),
                    thumb = 0.35f,
                    exact = true,
                };
            }

            // A fist round the bottle's body or the grenade, the thumb toward the neck or the fuse,
            // the back of the hand toward the eyes and out to the right.
            bool bottle = pose.HeldBeer != null;
            float radius = bottle ? 0.035f : 0.033f;
            Vector3 centre = p + q * new Vector3(0f, bottle ? -0.005f : 0f, 0f);
            Vector3 outward = new Vector3(0.8f, -0.3f, -0.5f);
            Vector3 y = outward - axis * Vector3.Dot(outward, axis);
            if (y.sqrMagnitude < 1e-6f) y = Vector3.right - axis * Vector3.Dot(Vector3.right, axis);
            y.Normalize();
            Vector3 x = -axis;
            Quaternion fist = Quaternion.LookRotation(Vector3.Cross(x, y), y);
            float close = bottle ? 0.62f : 0.66f;
            return new Target
            {
                wrist = centre - fist * FirstPersonArm.FistPoint(1f, radius),
                rotation = fist,
                fingers = new Vector4(close, close, close + 0.04f, close + 0.08f),
                thumb = 0.55f,
                exact = true,
            };
        }

        // The left hand at the grenade's pin, for the yank.
        Target PinYank(Vector3 grenadeWorld)
        {
            Vector3 g = rig.InverseTransformPoint(grenadeWorld);
            Vector3 at = g + new Vector3(-0.07f, 0.035f, -0.01f);
            Quaternion rot = Quaternion.LookRotation(new Vector3(0.9f, 0.25f, 0.35f), new Vector3(-0.2f, 1f, 0f));
            return new Target
            {
                wrist = at - rot * FirstPersonArm.PinchPoint(-1f, 0.5f),
                rotation = rot,
                fingers = new Vector4(0.55f, 0.6f, 0.7f, 0.75f),
                thumb = 0.6f,
            };
        }

        // The right hand snatching at the air in front, closing as it goes.
        Target SnatchTarget()
        {
            float close = Mathf.SmoothStep(0f, 1f, gestureClock / 0.16f);
            return new Target
            {
                wrist = new Vector3(0.07f, -0.12f, 0.5f),
                rotation = Quaternion.LookRotation(new Vector3(-0.15f, 0.05f, 1f), new Vector3(0.3f, 1f, 0f)),
                fingers = Vector4.one * Mathf.Lerp(0.02f, 0.9f, close),
                thumb = Mathf.Lerp(0.05f, 0.8f, close),
            };
        }

        float GestureSeconds(Gesture g)
        {
            switch (g)
            {
                case Gesture.Throw:
                case Gesture.Shove: return throwSeconds;
                case Gesture.Snatch: return snatchSeconds;
                case Gesture.Pocket: return pocketSeconds;
            }
            return 0f;
        }

        // Quick out, slower back.
        float Envelope(float attack)
        {
            float total = GestureSeconds(gesture);
            float t = gestureClock;
            if (t < attack) return Mathf.SmoothStep(0f, 1f, t / attack);
            return 1f - Mathf.SmoothStep(0f, 1f, (t - attack) / Mathf.Max(0.01f, total - attack));
        }

        void ApplyGesture(ref Target r, ref Target l)
        {
            if (gestureClock < 0f) return;
            switch (gesture)
            {
                case Gesture.Throw:
                {
                    var swing = new Target
                    {
                        wrist = new Vector3(0.09f, -0.1f, 0.5f),
                        rotation = Quaternion.LookRotation(new Vector3(-0.1f, -0.35f, 1f), new Vector3(0f, 1f, 0.3f)),
                        fingers = new Vector4(0.05f, 0.05f, 0.08f, 0.1f),
                        thumb = 0.1f,
                    };
                    r = Blend(r, swing, Envelope(0.07f));
                    break;
                }
                case Gesture.Shove:
                {
                    float k = Envelope(0.08f);
                    for (int i = 0; i < 2; i++)
                    {
                        float s = i == 0 ? 1f : -1f;
                        var push = new Target
                        {
                            wrist = new Vector3(0.13f * s, -0.15f, 0.5f),
                            rotation = Quaternion.LookRotation(new Vector3(-0.1f * s, 1f, 0.35f), new Vector3(0f, 0.2f, -1f)),
                            fingers = new Vector4(0.05f, 0.05f, 0.05f, 0.08f),
                            thumb = 0.1f,
                        };
                        if (i == 0) r = Blend(r, push, k); else l = Blend(l, push, k);
                    }
                    break;
                }
                case Gesture.Snatch:
                    r = Blend(r, SnatchTarget(), Envelope(0.12f));
                    break;
                case Gesture.Pocket:
                {
                    var dip = new Target
                    {
                        wrist = new Vector3(0.24f, -0.5f, 0.1f),
                        rotation = Quaternion.LookRotation(new Vector3(0f, -1f, 0.3f), new Vector3(1f, 0f, 0f)),
                        fingers = new Vector4(0.6f, 0.6f, 0.65f, 0.7f),
                        thumb = 0.5f,
                    };
                    r = Blend(r, dip, Envelope(0.15f));
                    break;
                }
            }
        }

        static Target Blend(Target a, Target b, float k)
        {
            k = Mathf.Clamp01(k);
            return new Target
            {
                wrist = Vector3.Lerp(a.wrist, b.wrist, k),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, k),
                fingers = Vector4.Lerp(a.fingers, b.fingers, k),
                thumb = Mathf.Lerp(a.thumb, b.thumb, k),
                exact = a.exact && k < 0.5f,
            };
        }

        // ---- the arm ----

        float Reach => upperArmLength + forearmLength - 0.02f;

        Vector3 Shoulder(float s) => new Vector3(shoulder.x * s, shoulder.y, shoulder.z);

        void Drive(Hand h, Target t, float dt)
        {
            if (h.arm == null) return;
            Vector3 sh = Shoulder(h.side);

            Vector3 goal = t.wrist;
            if (!t.exact) goal = KeepInView(goal);
            // Stopped at arm's length: the hand still points where it wanted to be.
            Vector3 d = goal - sh;
            if (d.magnitude > Reach) goal = sh + d.normalized * Reach;

            if (!h.placed || dt <= 0f)
            {
                h.wrist = goal;
                h.velocity = Vector3.zero;
                h.rotation = t.rotation;
                h.fingers = t.fingers;
                h.thumb = t.thumb;
                h.placed = true;
            }
            else
            {
                float follow = gestureClock >= 0f ? 0.035f : followSeconds;
                Vector3 sprung = Vector3.SmoothDamp(h.wrist, goal, ref h.velocity, follow, Mathf.Infinity, dt);
                Quaternion turned = Quaternion.Slerp(h.rotation, t.rotation, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, follow * 0.8f)));
                // Locked on an item the hand is exactly where the item is drawn; the spring keeps
                // following so letting go is smooth.
                h.wrist = Vector3.Lerp(sprung, goal, h.exact);
                h.rotation = Quaternion.Slerp(turned, t.rotation, h.exact);
                if (h.exact >= 1f) h.velocity = Vector3.zero;
                float step = dt * 7f;
                h.fingers = Vector4.MoveTowards(h.fingers, t.fingers, step);
                h.thumb = Mathf.MoveTowards(h.thumb, t.thumb, step);
            }

            h.arm.Curl(h.fingers.x, h.fingers.y, h.fingers.z, h.fingers.w, h.thumb);
            Vector3 pole = new Vector3(elbowPole.x * h.side, elbowPole.y, elbowPole.z);
            h.arm.Place(h.wrist, h.rotation, LimbIK.Joint(sh, h.wrist, upperArmLength, forearmLength, pole));
        }

        // The wrist stays where the hand shows: within the sides of the view and not below its
        // bottom edge (the hand reaches up and forward from the wrist).
        Vector3 KeepInView(Vector3 p)
        {
            float z = Mathf.Max(p.z, 0.15f);
            float mx = z * tanH * 0.9f;
            p.x = Mathf.Clamp(p.x, -mx, mx);
            p.y = Mathf.Max(p.y, -z * tanV * 1.02f);
            return p;
        }

        // The held object's box in its own space, from its renderers (or its colliders).
        Bounds LocalBounds(MovableObject mo)
        {
            Transform root = mo.transform;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            mo.GetComponentsInChildren(false, boundsScratch);
            for (int i = 0; i < boundsScratch.Count; i++)
            {
                var r = boundsScratch[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Bounds lb = r.localBounds;
                Matrix4x4 m = toRoot * r.localToWorldMatrix;
                Grow(ref b, ref any, m, lb);
            }
            boundsScratch.Clear();
            if (!any)
            {
                mo.GetComponentsInChildren(false, colliderScratch);
                for (int i = 0; i < colliderScratch.Count; i++)
                {
                    var c = colliderScratch[i];
                    if (c == null || c.isTrigger) continue;
                    Bounds wb = c.bounds;
                    Grow(ref b, ref any, toRoot, wb);
                }
                colliderScratch.Clear();
            }
            if (!any) b = new Bounds(Vector3.zero, Vector3.one * 0.3f);
            return b;
        }

        static void Grow(ref Bounds b, ref bool any, Matrix4x4 m, Bounds box)
        {
            Vector3 c = box.center, e = box.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
    }
}
