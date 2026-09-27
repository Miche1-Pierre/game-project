using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // What using a held thing looks like from inside your own head. Goes on the player root,
    // next to PlayerGrab.
    //
    //   smoking   the cigarette comes up to the lips for each drag and down by the chest between
    //             drags (SmokeTimeline), its ember at the bottom of the view;
    //   drinking  the bottle rises to the lips and tips further as it empties, a gulp at a time,
    //             and the head tips back a little with it;
    //   a grenade whose pin you are pulling is yanked once (the pin) and drawn back by the ear,
    //             the wind-up of the throw that follows when the button comes up. Only for the
    //             press that pulled the pin: a grenade armed by a blast or by the other player
    //             is carried like anything else when you pick it up.
    //
    // Only the picture moves: the item's child renderers, placed just before this player's own
    // camera culls and put back after the renders. The rigidbody is never touched, so the carry
    // is exactly the one playtest 001 validated (ADR-007), in use or not:
    //   - the cigarette's smoke still forms past its tip, out at your reach (CigaretteItem);
    //   - a grenade is still thrown from the carry point, as far as before, and one dropped
    //     with the left button still falls from there;
    //   - nothing is steered into the CharacterController's capsule.
    // Placing the picture every frame, rather than steering a body, is also what keeps a
    // cigarette 12 cm from the eye steady: physics runs at 50 Hz, the camera at the frame rate.
    // Every other camera sees the real item on the carry, and during a drag or a drink not even
    // that: HandHeldProp shows the other player a copy in the body's hand instead.
    //
    // Update runs after PlayerGrab's (order 20 against 0), so a use that began this frame is
    // drawn this frame, and after HandHeldProp's (10), which copies the pieces while they are
    // still at home on the item.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class HeldPose : MonoBehaviour
    {
        public enum Use { None, Smoke, Drink, WindUp }

        [Header("Smoking (camera space, metres: the camera is the eyes)")]
        public Vector3 lips = new Vector3(0f, -0.07f, 0.12f);          // where the filter goes
        public Vector3 smokeTipDirection = new Vector3(0.08f, -0.05f, 1f);
        public Vector3 aside = new Vector3(0.16f, -0.21f, 0.40f);      // between drags, by the chest
        public Vector3 asideTipDirection = new Vector3(0.25f, 0.6f, 0.65f);

        [Header("Drinking")]
        public Vector3 bottleLips = new Vector3(0f, -0.075f, 0.10f);   // where the bottle's lip goes
        public float tiltFull = 18f;          // degrees the bottom is raised over the neck, full
        public float tiltEmpty = 58f;         // and empty: the last drops need the bottle upended
        public float gulpTilt = 7f;
        public float lookUpFull = 4f;         // degrees the view tips back, full bottle
        public float lookUpEmpty = 11f;
        public float gulpLookUp = 1.5f;

        [Header("Grenade: the pin, then the wind-up")]
        public Vector3 windUp = new Vector3(0.22f, -0.03f, 0.55f);
        public Vector3 pinTug = new Vector3(-0.07f, 0.03f, -0.04f);   // the yank of the pin, towards the other hand
        public float pinTugSeconds = 0.2f;
        public float tremble = 0.004f;        // metres: the hand is not quite steady with a live one in it

        [Header("Timing")]
        public float blendIn = 0.22f;         // seconds from the carry to the use pose
        public float blendOut = 0.3f;         // and back
        public float releaseFade = 0.15f;     // the picture catching up with the body when you let go
        [Tooltip("The eyes' near clip plane is lowered to this at Start: the lips are 12 cm from the eyes, and the scene's 0.3 m would cut the cigarette off.")]
        public float nearClip = 0.05f;

        // The beer's neck top above its root (BeerItem.Build: Beer_Neck at 0.08, 0.025 half high).
        const float BottleNeckTop = 0.105f;

        PlayerGrab grab;
        PlayerController controller;
        CrewMember member;
        CrewInput input;
        Camera eyes;

        MovableObject item;
        CigaretteItem cigarette;
        BeerItem beer;
        GrenadeItem grenade;
        Use mode;
        float weight;
        float drinkClock;
        bool wasArmed;
        // The grenade in the hands was armed by this player's own right-button press, and that
        // press is still held: the one case the wind-up is for.
        bool windUpPress;
        float pinClock = -1f;

        // The pieces of the item in the hands, and of the one that just left them (drawn catching
        // up with its body for releaseFade).
        readonly Pieces held = new Pieces();
        readonly Pieces fade = new Pieces();
        float fadeClock = -1f;
        Vector3 fadeOffset;
        Quaternion fadeTurn = Quaternion.identity;
        Vector3 lastPicturePosition;
        Quaternion lastPictureRotation = Quaternion.identity;
        int lastPictureFrame = -10;

        readonly Camera[] stack = new Camera[4];
        int depth;

        // What is going on in the hands, for the body (CrewAnimator) and the copy in its hand
        // (HandHeldProp).
        public Use Mode => weight > 0f ? mode : Use.None;
        // What the hands are doing right now, without the blend: the button is up, this is None
        // even while the picture is still easing back to the carry.
        public Use Wanted { get; private set; }
        public float Weight => weight;
        public MovableObject Current => item;
        // Between the lips and the body: the thing is at your mouth, not in front of you.
        public bool InHand => (mode == Use.Smoke || mode == Use.Drink) && weight >= 0.6f;
        // 0..1 through the clip that shows it on the body: the drag, or four gulps of a drink.
        public float SmokePhase => cigarette != null ? cigarette.SmokePhase : 0f;
        public float DrinkPhase => beer != null ? Mathf.Repeat(drinkClock / (4f * Mathf.Max(0.1f, beer.swallowSeconds)), 1f) : 0f;
        // For the tests: where this player's own camera last drew the item, and the lips.
        public Vector3 LastPicturePosition => lastPicturePosition;
        public Quaternion LastPictureRotation => lastPictureRotation;
        public int LastPictureFrame => lastPictureFrame;
        // False while the held item's pieces are drawn somewhere else than on it (between this
        // Update and the end of the renders). HandHeldProp copies them only when they are home.
        public bool PiecesAtHome => !held.Displaced;
        public Vector3 LipsWorld => eyes != null ? eyes.transform.TransformPoint(lips) : transform.position;

        void Awake()
        {
            grab = GetComponent<PlayerGrab>();
            controller = GetComponent<PlayerController>();
            member = GetComponent<CrewMember>();
            input = GetComponent<CrewInput>();
        }

        void Start()
        {
            ResolveEyes();
            if (eyes != null && eyes.nearClipPlane > nearClip) eyes.nearClipPlane = nearClip;
        }

        void ResolveEyes()
        {
            if (member == null) member = GetComponent<CrewMember>();
            if (member != null && member.View != null) eyes = member.View;
            else if (controller != null && controller.cam != null) eyes = controller.cam.GetComponent<Camera>();
        }

        void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
        }

        void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            depth = 0;
            held.Clear();
            fade.Clear();
            fadeClock = -1f;
            item = null;
            cigarette = null;
            beer = null;
            grenade = null;
            weight = 0f;
            mode = Use.None;
            Wanted = Use.None;
            windUpPress = false;
        }

        // ---- what is in the hands -------------------------------------------------------

        void Update()
        {
            if (grab == null) return;
            MovableObject held = grab.Held;
            if (held != null && !held.gameObject.activeInHierarchy) held = null;
            if (!ReferenceEquals(held, item)) Switch(held);

            float dt = Time.deltaTime;
            WatchGrenade(dt);
            Use want = WantedUse();
            Wanted = want;
            if (want != Use.None) mode = want;
            float target = want != Use.None ? 1f : 0f;
            float seconds = target > weight ? blendIn : blendOut;
            weight = Mathf.MoveTowards(weight, target, seconds > 0.001f ? dt / seconds : 1f);
            if (weight <= 0f) mode = Use.None;

            if (beer != null && beer.IsDrinking) drinkClock += dt;
            else if (weight <= 0f) drinkClock = 0f;

            if (fadeClock >= 0f) { fadeClock += dt; if (fadeClock >= releaseFade) { fadeClock = -1f; fade.Clear(); } }

            // The head tips back with the bottle, only in the picture (ViewOffset).
            if (mode == Use.Drink && weight > 0.001f && eyes != null && !(member != null && member.IsDriving))
            {
                float up = Mathf.Lerp(lookUpFull, lookUpEmpty, 1f - beer.fill) + gulpLookUp * Gulp();
                ViewOffset.For(eyes).Add(Vector3.zero, Quaternion.identity, Quaternion.Euler(-up * Eased, 0f, 0f));
            }

            // Drawn at the lips now, not only in this player's render: the particle systems start
            // their frame right after Update (PreLateUpdate), and the cigarette's lit-tip wisp must
            // rise from the tip you see, not from the rigidbody out on the carry. The pieces have
            // no collider, so nothing else notices; every render still places them for its camera.
            if (eyes != null && eyes.isActiveAndEnabled && (weight > 0f || fadeClock >= 0f)) Draw(eyes);
        }

        Use WantedUse()
        {
            if (item == null || grab == null || grab.IsDragging || (member != null && member.IsDriving)) return Use.None;
            if (cigarette != null && cigarette.IsSmoking) return Use.Smoke;
            if (beer != null && beer.IsDrinking) return Use.Drink;
            if (windUpPress) return Use.WindUp;
            return Use.None;
        }

        // The wind-up belongs to one press: the pin comes out in these hands (GrenadeItem arms
        // itself from PlayerGrab's Update, which runs before this one), pulled by this player,
        // with the right button down. It ends when the button comes up (the throw) or the
        // grenade goes. A grenade already live when it reached the hands never winds up: the
        // arming was not seen here.
        void WatchGrenade(float dt)
        {
            if (input == null) input = GetComponent<CrewInput>();
            bool armed = grenade != null && grenade.IsArmed && !grenade.HasExploded;
            bool pressing = input != null && input.Held(CrewButton.Throw);
            if (armed && !wasArmed)
            {
                pinClock = 0f;
                windUpPress = pressing && (member == null || grenade.ArmedBy == member.index);
            }
            wasArmed = armed;
            if (!armed || !pressing) windUpPress = false;
            if (pinClock >= 0f) { pinClock += dt; if (pinClock > pinTugSeconds) pinClock = -1f; }
        }

        void Switch(MovableObject next)
        {
            // Outside a render every piece is at home (the last render put them back), so both
            // sets can be read and swapped here safely.
            // The one leaving the hands keeps being drawn where it was for a moment, catching up
            // with its body, instead of popping from the lips to the end of your arms.
            fade.Clear();
            fadeClock = -1f;
            if (item != null && weight > 0.01f && item.gameObject.activeInHierarchy
                && Time.frameCount - lastPictureFrame <= 2)
            {
                fade.Remember(item.transform);
                fadeClock = 0f;
                fadeOffset = lastPicturePosition - item.transform.position;
                fadeTurn = lastPictureRotation * Quaternion.Inverse(item.transform.rotation);
            }

            item = next;
            cigarette = next != null ? next.GetComponent<CigaretteItem>() : null;
            beer = next != null ? next.GetComponent<BeerItem>() : null;
            grenade = next != null ? next.GetComponent<GrenadeItem>() : null;
            weight = 0f;
            mode = Use.None;
            drinkClock = 0f;
            // Already live when it reached the hands: no arming seen, no wind-up.
            wasArmed = grenade != null && grenade.IsArmed;
            windUpPress = false;
            pinClock = -1f;
            held.Remember(next != null ? next.transform : null);
        }

        // ---- the pose -------------------------------------------------------------------

        float Eased => Mathf.SmoothStep(0f, 1f, weight);

        // One swallow's worth of tip, 0..1, in step with BeerItem's PlayerDrinking events (one
        // every swallowSeconds from the moment it tips).
        float Gulp()
        {
            if (beer == null) return 0f;
            float u = Mathf.Repeat(drinkClock / Mathf.Max(0.1f, beer.swallowSeconds), 1f);
            return u < 0.4f ? Mathf.Sin(Mathf.PI * u / 0.4f) : 0f;
        }

        // The use pose of the item's root, in world space, seen from `cam`.
        void Pose(Transform cam, out Vector3 position, out Quaternion rotation)
        {
            Quaternion cr = cam.rotation;
            switch (mode)
            {
                case Use.Smoke:
                {
                    float half = cigarette != null ? cigarette.length * 0.5f : 0.045f;
                    float at = SmokeTimeline.AtLips(SmokePhase);
                    Vector3 lipsDir = smokeTipDirection.normalized;
                    Vector3 asideDir = asideTipDirection.normalized;
                    // The filter on the lips, the tip out: the root is half a length along the tip.
                    Vector3 local = Vector3.Lerp(aside, lips + lipsDir * half, at);
                    Quaternion r = Quaternion.Slerp(Quaternion.FromToRotation(Vector3.up, asideDir),
                                                    Quaternion.FromToRotation(Vector3.up, lipsDir), at);
                    position = cam.TransformPoint(local);
                    rotation = cr * r;
                    return;
                }
                case Use.Drink:
                {
                    float fill = beer != null ? beer.fill : 1f;
                    float tilt = (Mathf.Lerp(tiltFull, tiltEmpty, 1f - fill) + gulpTilt * Gulp()) * Mathf.Deg2Rad;
                    // The bottle's +Y runs from its body to its neck: towards the face and down,
                    // with the bottom raised `tilt` over the neck.
                    Vector3 neck = new Vector3(0f, -Mathf.Sin(tilt), -Mathf.Cos(tilt));
                    position = cam.TransformPoint(bottleLips - neck * BottleNeckTop);
                    rotation = cr * Quaternion.FromToRotation(Vector3.up, neck);
                    return;
                }
                case Use.WindUp:
                {
                    Vector3 local = windUp;
                    if (pinClock >= 0f) local += pinTug * Mathf.Sin(Mathf.PI * Mathf.Clamp01(pinClock / pinTugSeconds));
                    float t = Time.time * 9f;
                    local += new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * tremble);
                    position = cam.TransformPoint(local);
                    rotation = cr * Quaternion.Euler(-25f, 15f, 0f);
                    return;
                }
            }
            position = item != null ? item.transform.position : cam.position;
            rotation = item != null ? item.transform.rotation : cr;
        }

        // ---- the picture ----------------------------------------------------------------

        void OnCameraPreCull(Camera cam)
        {
            if (depth < stack.Length) stack[depth] = cam;
            depth++;
            Draw(cam);
        }

        void OnCameraPostRender(Camera cam)
        {
            if (depth > 0) depth--;
            if (depth == 0) { held.PutBack(); fade.PutBack(); }
            else if (depth <= stack.Length) Draw(stack[depth - 1]);
        }

        void Draw(Camera cam)
        {
            bool own = cam != null && cam == eyes && !(member != null && member.IsDriving);
            if (!own) { held.PutBack(); fade.PutBack(); return; }

            if (fadeClock >= 0f && fade.Root != null)
            {
                float k = 1f - Mathf.SmoothStep(0f, 1f, fadeClock / Mathf.Max(0.01f, releaseFade));
                Transform ft = fade.Root;
                fade.Place(ft.position + fadeOffset * k, Quaternion.Slerp(Quaternion.identity, fadeTurn, k) * ft.rotation);
            }

            if (item == null || weight <= 0f) { held.PutBack(); return; }
            Transform root = item.transform;
            Pose(cam.transform, out Vector3 pos, out Quaternion rot);
            float w = Eased;
            Vector3 p = Vector3.Lerp(root.position, pos, w);
            Quaternion r = Quaternion.Slerp(root.rotation, rot, w);
            held.Place(p, r);
            lastPicturePosition = p;
            lastPictureRotation = r;
            lastPictureFrame = Time.frameCount;
        }

        // An item's child pieces (the renderers; the collider is on the root) and where they sit
        // on it, so its picture can be drawn somewhere else and put back.
        sealed class Pieces
        {
            readonly List<Transform> transforms = new List<Transform>(8);
            readonly List<Vector3> positions = new List<Vector3>(8);
            readonly List<Quaternion> rotations = new List<Quaternion>(8);
            bool displaced;

            public Transform Root { get; private set; }
            public bool Displaced => displaced;

            public void Remember(Transform root)
            {
                Clear();
                Root = root;
                if (root == null) return;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform c = root.GetChild(i);
                    transforms.Add(c);
                    positions.Add(c.localPosition);
                    rotations.Add(c.localRotation);
                }
            }

            // Draws the pieces as if the root stood at (position, rotation).
            public void Place(Vector3 position, Quaternion rotation)
            {
                if (Root == null) return;
                Vector3 s = Root.lossyScale;
                for (int i = 0; i < transforms.Count; i++)
                {
                    Transform t = transforms[i];
                    if (t == null) continue;
                    t.SetPositionAndRotation(position + rotation * Vector3.Scale(s, positions[i]), rotation * rotations[i]);
                }
                displaced = true;
            }

            public void PutBack()
            {
                if (!displaced) return;
                displaced = false;
                for (int i = 0; i < transforms.Count; i++)
                {
                    Transform t = transforms[i];
                    if (t == null) continue;
                    t.localPosition = positions[i];
                    t.localRotation = rotations[i];
                }
            }

            public void Clear()
            {
                PutBack();
                transforms.Clear();
                positions.Clear();
                rotations.Clear();
                Root = null;
            }
        }
    }
}
