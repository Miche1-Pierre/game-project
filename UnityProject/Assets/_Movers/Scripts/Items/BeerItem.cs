using UnityEngine;

namespace Movers
{
    // A bottle of beer lying in the world. Pick it up like anything else, hold the alt button
    // (F, Y on a pad) while you are carrying it, and throw it when you are done with it: it
    // breaks.
    //
    // The right button keeps its ordinary meaning here, so UsesHoldButton is false. There is
    // nothing to hold it for: the beer has its own button, and a bottle that needed a long
    // press to throw would be a rule with one user.
    //
    // The alt button also wears things. PlayerEquip owns it and decides: a wearable in your
    // hands is put on, anything else that uses the alt button (this) gets OnAltHold while the
    // button is held. The bottle never reads a key itself, so it drinks for whoever holds it.
    //
    // What being drunk does lives in Drunkenness, on the player. This is only the source.
    public class BeerItem : HeldUsable
    {
        public override bool UsesHoldButton => false;
        public override bool UsesAltButton => true;

        [Header("Drinking")]
        public float drinkSeconds = 4f;       // of holding, to empty it
        // One PlayerDrinking event per mouthful, not per frame: the grandmother hears a swallow,
        // not a stream. The first one lands the moment the bottle tips.
        public float swallowSeconds = 0.6f;
        public float fullBottleDrunk = 1f;
        [Range(0f, 1f)] public float fill = 1f;

        [Header("Breaking")]
        // Glass. It breaks on anything you would call a throw, full or empty: losing a full
        // beer to a bad throw is the funnier of the two outcomes, and one rule beats two.
        public float breakImpulse = 1.6f;
        public float splatSize = 0.22f;

        [Header("The thing itself")]
        public Vector3 grabBox = new Vector3(0.1f, 0.26f, 0.1f);

        static readonly Color Full = new Color(0.55f, 0.28f, 0.06f);   // amber through brown glass
        static readonly Color Empty = new Color(0.42f, 0.46f, 0.32f);  // just glass
        static readonly Color Splat = new Color(0.24f, 0.30f, 0.18f);

        Material glass;
        Drunkenness drunk;
        bool broken;
        float untilSwallow;

        public bool IsEmpty => fill <= 0.001f;
        public bool IsDrinking { get; private set; }

        void Awake()
        {
            Build();
        }

        public override void OnPickedUp(PlayerGrab by)
        {
            base.OnPickedUp(by);
            drunk = by != null ? by.GetComponent<Drunkenness>() : null;
            // A player who can pick a bottle up can get drunk. Nothing else needs to install it.
            if (drunk == null && by != null) drunk = by.gameObject.AddComponent<Drunkenness>();
        }

        public override void OnReleased(bool thrown)
        {
            IsDrinking = false;
            base.OnReleased(thrown);
        }

        // Called every frame the alt button is held with this in the hands.
        public override void OnAltHold(float dt)
        {
            bool was = IsDrinking;
            IsDrinking = IsHeld && !IsEmpty;
            if (!IsDrinking) return;
            if (!was) untilSwallow = 0f;

            untilSwallow -= dt;
            if (untilSwallow <= 0f)
            {
                untilSwallow = Mathf.Max(0.1f, swallowSeconds);
                WorldEvents.Raise(WorldEventType.PlayerDrinking, transform.position, HolderActor, 0f, 0f, 0, this);
            }
            Drink(dt);
        }

        public override void OnAltRelease()
        {
            IsDrinking = false;
        }

        void Update()
        {
            if (Net.IsHost) NetHostTick();
            if (glass != null) glass.color = Color.Lerp(Empty, Full, fill);
        }

        // The swallow itself, kept apart from the button that asks for it so the scripted test
        // can drink without a keyboard, and so an empty bottle refuses in the one place that
        // cannot be bypassed. Returns how much went down, 1 being the whole bottle.
        public float Drink(float seconds)
        {
            if (drinkSeconds <= 0.01f || IsEmpty) return 0f;
            float taken = Mathf.Min(seconds / drinkSeconds, fill);
            fill -= taken;
            if (drunk != null) drunk.Add(taken * fullBottleDrunk);
            return taken;
        }

        void OnCollisionEnter(Collision c)
        {
            if (!Net.HasAuthority) return;   // online client: the host breaks it (BeerSplat, then the despawn)
            if (broken || IsHeld) return;   // it cannot break in your hand
            if (c.impulse.magnitude < breakImpulse) return;
            Break(c.GetContact(0).point, c.GetContact(0).normal);
        }

        // Breaking is a destroy, not a fragmentation: GREYBOX_SPEC keeps that off the list, so
        // the bottle leaves one flat mark on the floor and nothing else. It is enough to read.
        public void Break(Vector3 at, Vector3 normal)
        {
            if (broken) return;
            broken = true;

            SplatFx(at, normal, splatSize);
            if (Net.IsHost) ItemSync.SendBeerSplat(at, normal, splatSize);

            Discard();   // no-op if it was already thrown away, which is the usual path
            Destroy(gameObject);
        }

        // The mark a broken bottle leaves on the floor. Also played on the online client, from
        // the host's break (Items BeerSplat).
        public static void SplatFx(Vector3 at, Vector3 normal, float size)
        {
            var splat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            splat.name = "Beer_Splat";
            ItemArt.Kill(splat.GetComponent<Collider>());
            splat.transform.position = at + normal * 0.01f;
            splat.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            splat.transform.localScale = new Vector3(size, 0.004f, size);
            splat.GetComponent<Renderer>().sharedMaterial = ItemArt.Mat(Splat, Color.black);
        }

        // ---- online (NETCODE_SLICE 11.2) ----

        const float NetStateEvery = 0.2f;   // 5 Hz while drinking
        bool netSentDrinking;
        float netSentFill = 1f;
        float netNextState;

        // Host: the drinking edges, and the level while it goes down.
        void NetHostTick()
        {
            bool edge = IsDrinking != netSentDrinking;
            bool level = Mathf.Abs(fill - netSentFill) >= 1f / 255f && Time.unscaledTime >= netNextState;
            if (!edge && !level) return;
            if (!ItemSync.SendBeer(this, IsDrinking, fill)) return;
            netSentDrinking = IsDrinking;
            netSentFill = fill;
            netNextState = Time.unscaledTime + NetStateEvery;
        }

        // Client: the host's state (Items Beer). The swallows and the drunkenness stay there.
        public void NetApply(bool drinking, float level)
        {
            IsDrinking = drinking;
            fill = Mathf.Clamp01(level);
        }

        // ---- the object ----

        public static BeerItem Create(Vector3 at)
        {
            var go = new GameObject("Beer");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var item = go.AddComponent<BeerItem>();
            // AddComponent on the item brings MovableObject with it (HeldUsable requires it),
            // and MovableObject brings a Rigidbody. Asking for either one again returns null,
            // which is what the first version did and why it threw. Fetch, do not add.
            var mo = go.GetComponent<MovableObject>();
            var rb = go.GetComponent<Rigidbody>();

            var box = go.AddComponent<BoxCollider>();
            box.size = item.grabBox;
            box.center = new Vector3(0f, item.grabBox.y * 0.5f - 0.11f, 0f);

            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.8f;

            mo.displayName = "Beer";
            mo.weight = 0.6f;
            mo.contractValue = 0;
            mo.requiredForContract = false;
            // Breaking is this component's job, not MovableObject's: the generic fragile
            // flag marks an item grey and keeps it, and a bottle that survives its own
            // smash reads worse than one that simply goes.
            mo.fragile = false;
            // The crew's own: it fits in a pocket, and taking it home is not theft.
            mo.pocketable = true;
            mo.ownedByGrandma = false;
            rb.mass = Mathf.Max(0.1f, mo.weight);

            item.Build();
            return item;
        }

        void Build()
        {
            // Awake fires the moment AddComponent runs, so the factory and Awake both
            // reach here on the same object. Building twice would double every piece.
            if (transform.childCount > 0) return;

            glass = ItemArt.Mat(Full, Color.black);
            ItemArt.Piece(PrimitiveType.Cylinder, transform, "Beer_Body",
                          Vector3.zero, new Vector3(0.035f, 0.055f, 0.035f), glass);
            ItemArt.Piece(PrimitiveType.Cylinder, transform, "Beer_Neck",
                          new Vector3(0f, 0.08f, 0f), new Vector3(0.014f, 0.025f, 0.014f), glass);
        }
    }
}
