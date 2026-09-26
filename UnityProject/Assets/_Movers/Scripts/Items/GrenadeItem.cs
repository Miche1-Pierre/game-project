using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A hand grenade from the cellar. Pick it up like anything else.
    //
    // Hold the right button and the pin comes out: 3.5 seconds, blinking red and beeping
    // faster as it runs out. Let go of the button and you throw it, hard. A quick tap throws it
    // the way you throw anything else, pin still in, and a grenade with its pin in is a dud:
    // it bounces and lies there.
    //
    // The fuse does not run in the grenade's own Update. A grenade in a pocket is an inactive
    // object and inactive objects do not update, so every armed grenade is ticked by one hidden
    // GrenadeFuses object instead. Pull the pin, pocket it, and it goes off in your pocket, on
    // time, wherever you are standing. That is a funny failure, and it is kept on purpose.
    //
    // What a blast does (damage, forces, flash, sound) lives in Explosion. This is only the
    // thing that asks for one, and remembers who pulled the pin: the blast, everything it
    // breaks and every grenade it sets off are that player's doing (Actors, WorldEvents).
    public class GrenadeItem : HeldUsable
    {
        public override bool UsesHoldButton => true;

        [Header("Fuse")]
        public float fuseSeconds = 3.5f;
        public float radius = 6.5f;
        public float power = 1f;

        [Header("Chain reaction")]
        // A grenade inside this fraction of another blast's radius goes off too, a beat later
        // and a different beat for each one, so a crate of them is a string of bangs rather
        // than one big one.
        [Range(0f, 1f)] public float chainReach = 0.8f;
        public float chainFuseMin = 0.15f;
        public float chainFuseMax = 0.45f;

        [Header("Throw")]
        // Added on top of PlayerGrab's own throw when the button comes up with the pin out.
        // A grenade lobbed at sofa speed lands at your feet, which is funny exactly once.
        public float throwForward = 7f;
        public float throwUp = 1.5f;

        [Header("Blink")]
        public float blinkSlowest = 0.55f;   // seconds between blinks with the whole fuse left
        public float blinkFastest = 0.07f;   // and in the last instant
        public float blinkOnTime = 0.05f;
        public float beepVolume = 0.6f;

        [Header("The thing itself")]
        // Hand sized, like the cigarette: the visual is 7 cm across and the crosshair needs a fist.
        public Vector3 grabBox = new Vector3(0.1f, 0.13f, 0.1f);

        // From the pivot (the middle of the body) down to the bottom of the grab box, which is
        // the bottom of the body: what GrenadeCrate needs to stand one on a surface.
        public const float RestHeight = BodyHalfHeight;

        const float BodyRadius = 0.033f;
        const float BodyHalfHeight = 0.041f;

        static readonly Color Olive = new Color(0.33f, 0.37f, 0.18f);
        static readonly Color BlinkAlbedo = new Color(0.80f, 0.14f, 0.08f);
        static readonly Color BlinkEmission = new Color(2.2f, 0.22f, 0.10f);
        static readonly Color DarkMetal = new Color(0.17f, 0.17f, 0.16f);
        static readonly Color RingMetal = new Color(0.72f, 0.72f, 0.70f);

        // ---- every armed grenade, ticked from one place ----

        static readonly List<GrenadeItem> Armed = new List<GrenadeItem>();
        static readonly List<GrenadeItem> tickBuffer = new List<GrenadeItem>();
        static GrenadeFuses fuses;

        // Shared by every grenade: only the body blinks, so only the body needs its own material.
        static Material darkMetalMat;
        static Material ringMat;
        static Mesh ringMesh;

        // Statics survive a play-mode exit when the domain reload is disabled, which the
        // playtest CLI does on purpose. Without this a second run would tick the first run's
        // grenades. The shared materials and mesh are HideAndDontSave and fine to keep.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Armed.Clear();
            tickBuffer.Clear();
            fuses = null;
        }

        public static int ArmedCount => Armed.Count;

        // ---- this one ----

        MovableObject movable;
        Material bodyMat;
        Transform pin;
        bool armed;
        bool exploded;
        bool listed;          // in Armed; tracked by reference, see Unlist
        int armedFrame = -1;  // the frame the clock started; it does not tick in that frame
        float fuseLeft;
        int armedBy = Actors.World;   // who pulled the pin, or whose blast lit this one
        float blinkTimer;
        float litLeft;
        bool litShown;

        public bool IsArmed => armed;
        public bool IsPinOut => pin == null || !pin.gameObject.activeSelf;
        // True from the instant it goes off. Destroy only lands at the end of the frame, so for
        // the rest of that frame the object still exists: PlayerPockets asks this rather than
        // trust a non-null reference and hand you a grenade that has already gone off.
        public bool HasExploded => exploded;
        // Seconds to the bang, or a negative number while the pin is in.
        public float FuseLeft => armed ? fuseLeft : -1f;
        // Who the blast will be blamed on (see Actors).
        public int ArmedBy => armedBy;

        MovableObject Movable => movable != null ? movable : (movable = GetComponent<MovableObject>());

        void Awake()
        {
            movable = GetComponent<MovableObject>();
            Build();
            // Subscribed for the whole life of the object, not while enabled: a grenade sitting
            // inactive in a pocket is still a grenade, and a blast next to you sets it off.
            Explosion.Detonated += OnNearbyDetonation;
        }

        void OnDestroy()
        {
            Explosion.Detonated -= OnNearbyDetonation;
            Unlist();
            // The body material is this grenade's own (it blinks alone) and HideAndDontSave, so
            // nothing would ever collect it: a crate of them per Play would pile up in the editor.
            // The shared metal materials and the ring mesh are made once and kept.
            ItemArt.Kill(bodyMat);
            bodyMat = null;
        }

        // ---- the right button ----

        // Held past the tap threshold: the pin comes out and the clock starts. Holding on does
        // nothing more, the grenade is already as dangerous as it gets.
        public override void OnUseBegin()
        {
            if (exploded || armed) return;
            var member = holder != null ? CrewRoster.Owner(holder.transform) : null;
            armedBy = member != null ? member.index : Actors.World;
            PullPin(true);
            Arm(fuseSeconds, armedBy);
        }

        // Button up after a hold: throw it, if it is live and still in the hands that pulled
        // the pin. Let go some other way (dropped with the left button, pocketed) and it stays
        // wherever it went, still ticking.
        public override void OnUseEnd()
        {
            if (!armed || exploded) return;

            var by = holder;
            var mo = Movable;
            if (by == null || mo == null || by.Held != mo) return;

            Transform aim = by.cam;
            by.Release(true);   // PlayerGrab's own throw, which also ends this hold cleanly

            var rb = mo.rb;
            if (rb != null && aim != null && !rb.isKinematic && gameObject.activeInHierarchy)
                rb.AddForce(aim.forward * throwForward + Vector3.up * throwUp, ForceMode.VelocityChange);
        }

        // ---- fuse ----

        // Starts the clock, or shortens it if it is already running. Public so a debug key or
        // another system can light one without a player.
        public void Arm(float seconds)
        {
            Arm(seconds, armedBy);
        }

        // The same, saying who lit it. A grenade already ticking keeps its first owner.
        public void Arm(float seconds, int by)
        {
            if (!armed && !exploded) armedBy = by;
            // Play only: out of Play there is no clock to run it, and the clock object it would
            // create could end up saved into the scene.
            if (exploded || !Application.isPlaying) return;
            seconds = Mathf.Max(0f, seconds);

            if (armed)
            {
                fuseLeft = Mathf.Min(fuseLeft, seconds);
                return;
            }

            armed = true;
            armedFrame = Time.frameCount;
            fuseLeft = seconds;
            blinkTimer = 0f;   // the first blink and beep land on its first tick
            if (!listed)
            {
                Armed.Add(this);
                listed = true;
            }
            EnsureFuses();
        }

        // Goes off now. The blast itself is Explosion's.
        public void Explode()
        {
            if (exploded) return;
            exploded = true;
            armed = false;
            Unlist();

            // In someone's hands: let go first, so the carry never reaches for a destroyed body.
            // Only if it really is this grenade they are holding: a holder left stale by some
            // future path must not make the player drop the sofa.
            if (holder != null && holder.Held == Movable) holder.Release(false);
            holder = null;

            Vector3 at = transform.position;   // in a pocket, that is where the player stands
            Discard();
            // Out of the world before the blast, not just after it. Destroy only happens at the
            // end of the frame, and until then the blast's overlap would find this grenade's own
            // collider: push it, maybe break it into debris at ground zero, and take a slot in
            // the blast's hit buffer, all for an object that is already gone. Safe to switch off:
            // the hands let go above and the fuse list dropped it in Unlist.
            gameObject.SetActive(false);
            Destroy(gameObject);
            Explosion.Detonate(at, radius, power, armedBy);
        }

        // Another grenade, or anything else, went off. Close enough, in the open and the pin is
        // in: this one goes too, a moment later. One already ticking keeps its own clock.
        void OnNearbyDetonation(Vector3 at, float blastRadius, float blastPower)
        {
            if (this == null || exploded || armed) return;
            float reach = blastRadius * chainReach;
            if ((transform.position - at).sqrMagnitude > reach * reach) return;
            if (BehindCover(at)) return;

            PullPin(false);   // no pin sound: a dozen at once is noise, the beeps say enough
            // A chain reaction belongs to whoever started it.
            Arm(Random.Range(chainFuseMin, Mathf.Max(chainFuseMin, chainFuseMax)), Explosion.CurrentInstigator);
        }

        // A floor, a wall or a shut door between the blast and this grenade keeps its pin in.
        // Without it a grenade on the ground floor lit the cellar crate through the concrete,
        // which nobody watching could read. The blast is announced after the damage, so a wall
        // that gave way is already gone and lets the chain through. Loose things are not cover
        // (furniture, the crate, other grenades, debris, a crewmate): they are what the blast
        // throws about. Both ends are raised a hand's width, for the reason Explosion raises its
        // own cover rays: a grenade lies on the floor, and a ray skimming it would catch any
        // threshold or seam on the way.
        const float CoverRaise = 0.1f;
        static readonly RaycastHit[] coverHits = new RaycastHit[16];

        bool BehindCover(Vector3 blast)
        {
            Vector3 from = blast + Vector3.up * CoverRaise;
            Vector3 to = transform.position + Vector3.up * CoverRaise - from;
            float dist = to.magnitude;
            if (dist < 0.05f) return false;

            int n = Physics.RaycastNonAlloc(from, to / dist, coverHits, dist, DestructionLayers.QueryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = coverHits[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (col is CharacterController) continue;   // in a pocket, the ray ends inside its owner
                var body = col.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (col.GetComponentInParent<DebrisPiece>() != null) continue;   // settled debris goes kinematic
                return true;
            }
            return false;
        }

        void PullPin(bool withSound)
        {
            if (pin != null && pin.gameObject.activeSelf)
            {
                pin.gameObject.SetActive(false);
                if (withSound) ImpactAudio.Play(ImpactAudio.Kind.Pin, transform.position, 1f, armedBy);
            }
        }

        void Tick(float dt)
        {
            fuseLeft -= dt;

            // Faster as it runs out, on absolute time left rather than a fraction of the fuse,
            // so a chain-lit grenade with a quarter second left blinks like one that has a
            // quarter second left.
            blinkTimer -= dt;
            if (blinkTimer <= 0f)
            {
                float k = Mathf.Clamp01(fuseLeft / Mathf.Max(0.01f, fuseSeconds));
                float interval = Mathf.Lerp(blinkFastest, blinkSlowest, k);
                blinkTimer = Mathf.Max(0.02f, interval);
                litLeft = Mathf.Min(blinkOnTime, blinkTimer * 0.6f);
                ImpactAudio.Play(ImpactAudio.Kind.Beep, transform.position, beepVolume, armedBy);
            }
            litLeft -= dt;
            SetLit(litLeft > 0f);

            if (fuseLeft <= 0f) Explode();
        }

        void SetLit(bool on)
        {
            if (on == litShown || bodyMat == null) return;
            litShown = on;
            // The albedo changes too, so the blink still reads if the emissive variant of the
            // Standard shader was stripped from a build.
            bodyMat.color = on ? BlinkAlbedo : Olive;
            if (bodyMat.HasProperty("_EmissionColor"))
                bodyMat.SetColor("_EmissionColor", on ? BlinkEmission : Color.black);
        }

        // By reference: a destroyed object compares equal to every other destroyed object, so
        // List.Remove could take the wrong entry out.
        void Unlist()
        {
            if (!listed) return;
            listed = false;
            for (int i = Armed.Count - 1; i >= 0; i--)
                if (ReferenceEquals(Armed[i], this)) Armed.RemoveAt(i);
        }

        // Hidden from the hierarchy, but an ordinary scene object otherwise: it goes with the
        // scene. Not DontSave: that flag would also keep it alive past a scene load and past the
        // end of Play, and a leftover clock would tick every fuse a second time.
        static void EnsureFuses()
        {
            if (fuses != null || !Application.isPlaying) return;
            var go = new GameObject("GrenadeFuses") { hideFlags = HideFlags.HideInHierarchy };
            fuses = go.AddComponent<GrenadeFuses>();
        }

        // Only one clock may tick. A stray second one (a leftover from a run the statics were
        // reset under) removes itself instead of doubling every fuse's speed.
        internal static bool IsTheClock(GrenadeFuses clock)
        {
            return ReferenceEquals(fuses, clock);
        }

        // Called once a frame by GrenadeFuses. Walks a copy, because a grenade that goes off
        // can light others (they join Armed) and leaves the list itself.
        //
        // A grenade never ticks in the frame it was armed. The copy already keeps the ones a
        // blast lights from this loop, but a blast from anywhere else (a debug call, a future
        // gas bottle) can land earlier in the frame, and after a long frame (a big explosion is
        // one) deltaTime can be longer than the shortest chain fuse: without this, a crate of
        // grenades could all go off in the frame of the first bang.
        internal static void TickAll(float dt)
        {
            for (int i = Armed.Count - 1; i >= 0; i--)
                if (Armed[i] == null) Armed.RemoveAt(i);   // destroyed without OnDestroy running
            if (Armed.Count == 0) return;

            int frame = Time.frameCount;
            tickBuffer.Clear();
            for (int i = 0; i < Armed.Count; i++) tickBuffer.Add(Armed[i]);
            for (int i = 0; i < tickBuffer.Count; i++)
            {
                var g = tickBuffer[i];
                if (g == null || !g.armed || g.exploded || g.armedFrame == frame) continue;
                g.Tick(dt);
            }
            tickBuffer.Clear();
        }

        // ---- the object ----

        public static GrenadeItem Create(Vector3 at)
        {
            var go = new GameObject("Grenade");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);   // standing up

            var item = go.AddComponent<GrenadeItem>();
            // AddComponent on the item brings MovableObject with it (HeldUsable requires it),
            // and MovableObject brings a Rigidbody. Fetch, do not add (see CigaretteItem).
            var mo = go.GetComponent<MovableObject>();
            var rb = go.GetComponent<Rigidbody>();

            // Its bottom is the bottom of the body, so a grenade stands where it is put.
            var box = go.AddComponent<BoxCollider>();
            box.size = item.grabBox;
            box.center = new Vector3(0f, item.grabBox.y * 0.5f - BodyHalfHeight, 0f);

            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.6f;
            // Small and thrown hard: at 13 m/s it covers a quarter metre per physics step, more
            // than a wall is thick. The swept test keeps it on the right side of the wall.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            mo.displayName = "Grenade";
            mo.weight = 0.6f;
            mo.contractValue = 0;
            mo.requiredForContract = false;
            // The crew's, not hers (like BeerItem and CigaretteItem): without this the
            // grandmother saw every grenade pocketed or carried in front of her as a theft.
            mo.ownedByGrandma = false;
            mo.fragile = false;
            // MovableObject.Awake has already run and set the mass from the old weight.
            rb.mass = Mathf.Max(0.1f, mo.weight);

            item.movable = mo;
            item.Build();
            return item;
        }

        void Build()
        {
            // Awake fires the moment AddComponent runs, so the factory and Awake both
            // reach here on the same object. Building twice would double every piece.
            if (transform.childCount > 0) return;

            bodyMat = ItemArt.Mat(Olive, Color.black);
            // ItemArt only turns emission on for a glowing colour. The blink needs it on from
            // the start, dark until the first blink.
            if (bodyMat.HasProperty("_EmissionColor"))
            {
                bodyMat.EnableKeyword("_EMISSION");
                bodyMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                bodyMat.SetColor("_EmissionColor", Color.black);
            }

            if (darkMetalMat == null) darkMetalMat = ItemArt.Mat(DarkMetal, Color.black);
            if (ringMat == null) ringMat = ItemArt.Mat(RingMetal, Color.black);

            ItemArt.Piece(PrimitiveType.Sphere, transform, "Grenade_Body", Vector3.zero,
                          new Vector3(BodyRadius * 2f, BodyHalfHeight * 2f, BodyRadius * 2f), bodyMat);

            ItemArt.Piece(PrimitiveType.Cylinder, transform, "Grenade_Fuse",
                          new Vector3(0f, 0.050f, 0f), new Vector3(0.022f, 0.011f, 0.022f), darkMetalMat);

            // The spoon: from the top of the fuse down the side of the body.
            var lever = ItemArt.Piece(PrimitiveType.Cube, transform, "Grenade_Lever",
                                      new Vector3(0f, 0.033f, 0.023f), new Vector3(0.013f, 0.055f, 0.004f), darkMetalMat);
            lever.localRotation = Quaternion.Euler(-23.7f, 0f, 0f);

            // The pin and its ring, on the side opposite the thumb. Hidden, not destroyed, when
            // pulled: IsPinOut reads it, and nothing else needs it back.
            var pinGo = new GameObject("Grenade_Pin");
            pin = pinGo.transform;
            pin.SetParent(transform, false);
            pin.localPosition = new Vector3(0f, 0.050f, 0f);

            var shaft = ItemArt.Piece(PrimitiveType.Cylinder, pin, "Grenade_PinShaft",
                                      new Vector3(-0.016f, 0f, 0f), new Vector3(0.004f, 0.012f, 0.004f), ringMat);
            shaft.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var ring = new GameObject("Grenade_Ring");
            ring.transform.SetParent(pin, false);
            ring.transform.localPosition = new Vector3(-0.037f, 0f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // stands up, around the shaft's axis
            ring.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            ring.AddComponent<MeshRenderer>().sharedMaterial = ringMat;
        }

        // A small torus, built once. No primitive is a ring, and a flat disc reads as a coin.
        static Mesh RingMesh()
        {
            if (ringMesh != null) return ringMesh;

            const int seg = 16, side = 6;
            const float R = 0.010f, r = 0.0016f;
            var v = new Vector3[seg * side];
            var n = new Vector3[seg * side];
            var t = new int[seg * side * 6];

            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j < side; j++)
                {
                    float b = j * Mathf.PI * 2f / side;
                    Vector3 normal = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v[i * side + j] = radial * R + normal * r;
                    n[i * side + j] = normal;
                }
            }

            int k = 0;
            for (int i = 0; i < seg; i++)
            {
                int i1 = (i + 1) % seg;
                for (int j = 0; j < side; j++)
                {
                    int j1 = (j + 1) % side;
                    int a = i * side + j, b = i1 * side + j, c = i1 * side + j1, d = i * side + j1;
                    // Clockwise seen from outside the tube, which is Unity's front face.
                    t[k++] = a; t[k++] = d; t[k++] = c;
                    t[k++] = a; t[k++] = c; t[k++] = b;
                }
            }

            ringMesh = new Mesh { name = "Grenade_Ring", hideFlags = HideFlags.HideAndDontSave };
            ringMesh.vertices = v;
            ringMesh.normals = n;
            ringMesh.triangles = t;
            ringMesh.RecalculateBounds();
            return ringMesh;
        }
    }

    // The one clock every armed grenade runs on, pocketed ones included. Created on demand by
    // the first grenade armed, hidden from the hierarchy, and gone with the scene.
    //
    // It shares this file because it is only ever added from code and has no meaning apart
    // from the grenade. The cost is that it has no script asset of its own, so if someone
    // reveals the hidden object the Inspector shows no script for it. It can move to its own
    // GrenadeFuses.cs unchanged.
    [AddComponentMenu("")]
    public sealed class GrenadeFuses : MonoBehaviour
    {
        void Update()
        {
            if (!GrenadeItem.IsTheClock(this))
            {
                Destroy(gameObject);
                return;
            }
            GrenadeItem.TickAll(Time.deltaTime);
        }
    }
}
