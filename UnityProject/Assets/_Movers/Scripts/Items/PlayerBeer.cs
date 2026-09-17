using UnityEngine;

namespace Movers
{
    // The other half of the starting inventory: one beer, and it is a whole beer, not a tap.
    //
    // Hold the drink key with your hands empty and the bottle comes up. Four seconds of
    // drinking empties it and leaves you as drunk as this game gets; a mouthful leaves you
    // slightly wrong. Then it is an empty bottle and the decision is behind you. That finite
    // amount is the design: an endless source would just be a permanently broken player, which
    // is a punishment, and the cigarette is already the item with no cost.
    //
    // What being drunk does lives in Drunkenness, not here. This is a source, not a state.
    // ADR-006 records why the beer was given a function at all, and what would take it back.
    public class PlayerBeer : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        public Transform cam;
        public PlayerGrab grab;
        public Drunkenness drunk;

        [Header("Input")]
        public KeyCode drinkKey = KeyCode.F;

        [Header("The beer")]
        // Seconds of drinking to empty it, and how drunk a full bottle leaves you.
        public float drinkSeconds = 4f;
        public float fullBottleDrunk = 1f;
        [Range(0f, 1f)] public float fill = 1f;

        [Header("Look of the thing in your hand")]
        // Left hand, and higher than it looks like it should be. At -0.24 the bottle fell off
        // the bottom edge of a 16:9 view and the contract panel ate what was left of it, so
        // the player was told they had a beer and never saw one.
        public Vector3 holdPosition = new Vector3(-0.23f, -0.17f, 0.44f);
        public Vector3 drinkPosition = new Vector3(-0.10f, -0.06f, 0.28f);
        public float drinkTilt = -70f;      // degrees, neck toward your face
        public float raiseSpeed = 7f;

        static readonly Color Full = new Color(0.55f, 0.28f, 0.06f);   // amber through brown glass
        static readonly Color Empty = new Color(0.42f, 0.46f, 0.32f);  // just glass

        Transform view;
        Material glass;
        float raised;      // 0 at your hip, 1 at your mouth
        bool drinking;

        public bool IsDrinking => drinking;
        public bool IsEmpty => fill <= 0.001f;

        void Awake()
        {
            if (cam == null)
            {
                var pc = GetComponent<PlayerController>();
                if (pc != null && pc.cam != null) cam = pc.cam;
            }
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (grab == null) grab = GetComponent<PlayerGrab>();

            // The state belongs to the player, so it is made here if nobody else made it.
            // The scene builders add it themselves; this only catches a player wired by hand.
            if (drunk == null) drunk = GetComponent<Drunkenness>();
            if (drunk == null) drunk = gameObject.AddComponent<Drunkenness>();

            if (cam == null)
            {
                Debug.LogWarning("[Beer] no camera found on " + name + ", the bottle stays in the crate.");
                enabled = false;
                return;
            }

            BuildView();
        }

        void Update()
        {
            bool handsFree = grab == null || !grab.IsCarrying;
            drinking = handsFree && !IsEmpty && Input.GetKey(drinkKey);

            // Hands full: the bottle goes down with the cigarette. You are carrying a fridge.
            if (view != null && view.gameObject.activeSelf != handsFree)
                view.gameObject.SetActive(handsFree);

            if (drinking) Drink(Time.deltaTime);

            // The bottle comes up while you drink and drops back when you stop, rather than
            // teleporting to your mouth. The tilt is the only thing anyone watching can see.
            raised = Mathf.MoveTowards(raised, drinking ? 1f : 0f, raiseSpeed * Time.deltaTime);
            if (view != null)
            {
                view.localPosition = Vector3.Lerp(holdPosition, drinkPosition, raised);
                view.localRotation = Quaternion.Euler(drinkTilt * raised, 0f, 0f);
            }

            if (glass != null) glass.color = Color.Lerp(Empty, Full, fill);
        }

        // The swallow itself, kept apart from the key that asks for it: the scripted test drinks
        // without a keyboard (MoversBeerPlaytestCLI), and an empty bottle refuses here rather
        // than in the input handler, which is the one place that cannot be bypassed.
        // Returns how much of the bottle actually went down, 1 being the whole thing.
        public float Drink(float seconds)
        {
            if (drinkSeconds <= 0.01f || IsEmpty) return 0f;
            float taken = Mathf.Min(seconds / drinkSeconds, fill);
            fill -= taken;
            if (drunk != null) drunk.Add(taken * fullBottleDrunk);
            return taken;
        }

        void BuildView()
        {
            var root = new GameObject("BeerView");
            view = root.transform;
            view.SetParent(cam, false);
            view.localPosition = holdPosition;

            glass = Mat(Full);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Beer_Body";
            Kill(body.GetComponent<Collider>());   // never let the viewmodel block the grab ray
            body.transform.SetParent(view, false);
            body.transform.localScale = new Vector3(0.035f, 0.055f, 0.035f);
            body.GetComponent<Renderer>().sharedMaterial = glass;

            var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Beer_Neck";
            Kill(neck.GetComponent<Collider>());
            neck.transform.SetParent(view, false);
            neck.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            neck.transform.localScale = new Vector3(0.014f, 0.025f, 0.014f);
            neck.GetComponent<Renderer>().sharedMaterial = glass;
        }

        static Material Mat(Color albedo)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            return new Material(sh) { color = albedo, hideFlags = HideFlags.HideAndDontSave };
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
