using UnityEngine;

namespace Movers
{
    // A lit cigarette lying in the world. Pick it up like anything else; hold the right button
    // while you are carrying it and you smoke it.
    //
    // Smoking is a drag, repeated while the button is held (SmokeTimeline, 1.67 s): the filter
    // comes to the lips (HeldPose draws it there in first person, Crew_Smoke on the body the
    // other player sees), the ember brightens while you draw, and a moment after the drag you
    // blow the smoke out. Let go of the button mid-drag and you still breathe out what you took
    // in, as long as the cigarette is still in your hands.
    //
    // Two things leave on the exhale, on purpose:
    //   the breath   a stream of small wisps out of the smoker's mouth, blown towards the cloud.
    //                Pure picture: it is what "really smoking" looks like.
    //   the clouds   the SmokeCloud volumes that blind, formed where they always were: just past
    //                the tip of the cigarette as the carry holds it. HeldPose moves only the
    //                picture to the lips, never the rigidbody, so the tip is still out at your
    //                reach. You still fog a doorway at arm's length, the wheel still aims it,
    //                and standing still you never smoke yourself blind (a smoker's own eyes stay
    //                at 0.00 density at every reach: _work/smoke_sim/sim_fix.py).
    //
    // What a puff does has not changed and lives in SmokeCloud: 7 seconds, everyone inside it.
    //
    // The look (Build): speckled filter, white paper, an ember that glows and lights the fingers
    // on each drag, grey ash that grows and drops off, and a thin curling thread off the tip.
    public class CigaretteItem : HeldUsable
    {
        [Header("Smoking")]
        public float cloudLifetime = SmokeCloud.DefaultLifetime;
        // One drag lays down this many clouds, a beat apart, as one long breath out. Three per
        // 1.67 s cycle keeps the old density (a cloud every half second while you hold).
        public int puffsPerExhale = 3;
        public float puffSpacing = 0.05f;       // of the cycle, between two clouds of one breath
        public float puffAhead = 0.35f;         // how far past the tip the first cloud of a breath forms
        public float exhaleStep = 0.15f;        // and each next one of the same breath, further on
        public float puffScatter = 0.08f;
        public float puffStrength = 1f;
        // The mouth, in the smoker's camera space: a little under and in front of the eyes. The
        // camera sits inside the body's head, so this is the body's mouth for everyone watching.
        public Vector3 mouthOffset = new Vector3(0f, -0.085f, 0.09f);
        // A breath left over from a drag is dropped if it could not come out by then: the
        // cigarette spent that time switched off in a pocket.
        const float LateBreathExpiry = 1f;

        [Header("The thing itself")]
        public float length = 0.09f;
        public float radius = 0.0055f;
        // Hand sized, not cigarette sized. The visual is 11 mm across and nobody can put a
        // crosshair on that at three metres, so the object you actually grab is a fist.
        public Vector3 grabBox = new Vector3(0.1f, 0.12f, 0.1f);

        [Header("Burning (looks only)")]
        public float ashMax = 0.012f;          // metres of ash before it drops off
        public float ashPerSecond = 0.0003f;   // lying there or held
        public float ashPerDrag = 0.004f;      // per second, at the peak of a drag
        public float lightRange = 0.45f;       // the ember's glow on the fingers, at the peak of a drag
        public float lightIntensity = 1.3f;

        // Where the fingers pinch it, from the filter end (HeldPose, FirstPersonHands).
        public const float PinchFromFilter = 0.016f;
        const float FilterLength = 0.024f;
        const float BandLength = 0.0016f;
        const float EmberLength = 0.0035f;
        const float CharLength = 0.0012f;
        const float AshFresh = 0.0015f;        // what is left on the tip right after it drops

        static readonly Color Paper = new Color(0.96f, 0.95f, 0.92f);
        static readonly Color FilterBase = new Color(0.86f, 0.53f, 0.24f);
        static readonly Color FilterDark = new Color(0.56f, 0.30f, 0.12f);
        static readonly Color FilterLight = new Color(0.97f, 0.74f, 0.44f);
        static readonly Color Band = new Color(0.84f, 0.70f, 0.38f);
        static readonly Color Char = new Color(0.12f, 0.09f, 0.07f);
        static readonly Color AshBase = new Color(0.58f, 0.57f, 0.55f);
        static readonly Color AshDark = new Color(0.34f, 0.33f, 0.32f);
        static readonly Color AshLight = new Color(0.78f, 0.77f, 0.75f);
        static readonly Color EmberDim = new Color(0.30f, 0.07f, 0.03f);
        static readonly Color EmberLit = new Color(1f, 0.42f, 0.10f);
        static readonly Color GlowLight = new Color(1f, 0.48f, 0.18f);

        const float RestGlow = 0.25f;       // lying there, lit
        const float HeldGlow = 0.4f;        // between drags

        // Shared by every cigarette: only the ember glows on its own.
        static Material paperMat, filterMat, bandMat, charMat, ashMat;

        Material emberMat;
        Transform ember;
        Transform paperPiece, charPiece, ashPiece;
        Light emberLight;
        ParticleSystem ashFall;
        float ash = AshFresh * 3f;
        bool ashDrop;
        ParticleSystem wisp;
        ExhaleStream exhale;
        bool smoking;
        float phase;          // 0..1 in the current drag (SmokeTimeline)
        int puffsDone;        // clouds of this drag's breath already out
        float glow = RestGlow;

        // A breath still to come out after the button went up mid-drag. It belongs to the one
        // who drew it: their hands, their mouth, their name on the PlayerSmoking events, all
        // captured when the drag ended. HolderActor at that later moment could be someone else.
        PlayerGrab lateSmoker;
        Transform lateMouth;
        int lateActor;
        int latePuffs;
        float nextLatePuff;

        public bool IsSmoking => smoking;
        // Where the current drag is, 0..1 (SmokeTimeline). Meaningful while IsSmoking.
        public float SmokePhase => phase;
        public float Glow => glow;

        void Awake()
        {
            Build();
        }

        void OnDestroy()
        {
            if (exhale != null) exhale.Dispose();
        }

        // Pocketed (PlayerPockets switches the object off) or taken out of the world: a breath
        // still pending is dropped, so it never comes out minutes later when the cigarette does.
        void OnDisable()
        {
            CancelLateBreath();
        }

        public override void OnUseBegin()
        {
            smoking = true;
            phase = 0f;
            puffsDone = 0;
            CancelLateBreath();
            SetWisp(WispDragRate);
        }

        public override void OnUseHold(float dt)
        {
            if (!smoking) return;
            phase += dt / Mathf.Max(0.2f, SmokeTimeline.CycleSeconds);
            if (phase >= 1f)
            {
                // A new drag. A frame long enough to skip a whole breath still lets it all out.
                while (puffsDone < puffsPerExhale) Puff(puffsDone++, MouthCamera, HolderActor);
                phase = Mathf.Repeat(phase, 1f);
                puffsDone = 0;
            }
            SetGlow(Mathf.Lerp(HeldGlow, 1f, SmokeTimeline.Draw(phase)));

            while (puffsDone < puffsPerExhale && phase >= SmokeTimeline.Exhale + puffsDone * puffSpacing)
                Puff(puffsDone++, MouthCamera, HolderActor);
        }

        public override void OnUseEnd()
        {
            if (!smoking) return;
            smoking = false;
            // Drew and did not breathe out yet: the breath still comes, a beat later, from the
            // one who drew it.
            if (phase >= SmokeTimeline.Lips && puffsDone < puffsPerExhale && MouthCamera != null)
            {
                lateSmoker = holder;
                lateMouth = MouthCamera;
                lateActor = HolderActor;
                latePuffs = puffsPerExhale - puffsDone;
                nextLatePuff = Time.time + 0.25f;
            }
            SetGlow(RestGlow);
            SetWisp(WispRestRate);
        }

        // The keyboard went to the other player mid-drag: the smoker lowers the cigarette.
        public override void OnUseCancelled()
        {
            if (smoking) OnUseEnd();
        }

        public override void OnReleased(bool thrown)
        {
            // Let go mid-drag and the cigarette stops smoking, wherever it lands.
            if (smoking) OnUseEnd();
            // Flicked away: the ash goes first.
            if (thrown && ash > AshFresh * 2f) ashDrop = true;
            base.OnReleased(thrown);
        }

        void Update()
        {
            Burn(Time.deltaTime);
            if (latePuffs <= 0 || Time.time < nextLatePuff) return;
            // The clouds form past the tip, so the breath only comes out while the smoker still
            // has it: dropped, handed over or pocketed mid-drag, it is lost. Overdue by more than
            // a second means this object was switched off meanwhile: also lost.
            if (holder == null || holder != lateSmoker || lateMouth == null
                || Time.time - nextLatePuff > LateBreathExpiry)
            {
                CancelLateBreath();
                return;
            }
            Puff(puffsPerExhale - latePuffs, lateMouth, lateActor);
            latePuffs--;
            nextLatePuff = Time.time + puffSpacing * SmokeTimeline.CycleSeconds;
            if (latePuffs <= 0) CancelLateBreath();
        }

        void CancelLateBreath()
        {
            latePuffs = 0;
            lateSmoker = null;
            lateMouth = null;
        }

        Transform MouthCamera => holder != null ? holder.cam : null;

        Vector3 Tip => transform.TransformPoint(Vector3.up * (length * 0.5f));

        // One cloud of a breath. index 0 is the first of the breath, and each next one forms a
        // little further on, so a breath reads as a stream rather than a ball.
        void Puff(int index, Transform mouthCam, int actor)
        {
            // The cloud: past the tip, away from the smoker, exactly where the carried cigarette
            // put it before it was smoked for real. Falling back to the cigarette's own axis keeps
            // this honest if nobody's camera is known (a scripted test).
            Vector3 dir = mouthCam != null ? mouthCam.forward : transform.up;
            Vector3 at = Tip + dir * (puffAhead + index * exhaleStep) + Random.insideUnitSphere * puffScatter;
            SmokeCloud.Spawn(at, dir, cloudLifetime, puffStrength);

            // The breath: out of the mouth and blown at the cloud, so the two read as one exhale.
            if (mouthCam != null)
            {
                if (exhale == null) exhale = new ExhaleStream();
                exhale.Emit(mouthCam.TransformPoint(mouthOffset), at, index == 0 ? 7 : 4);
            }
            WorldEvents.Raise(WorldEventType.PlayerSmoking, at, actor, 0f, 0f, 0, this);
        }

        // ---- the object ----

        public static CigaretteItem Create(Vector3 at)
        {
            var go = new GameObject("Cigarette");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);  // lying down

            var item = go.AddComponent<CigaretteItem>();
            // AddComponent on the item brings MovableObject with it (HeldUsable requires it),
            // and MovableObject brings a Rigidbody. Asking for either one again returns null,
            // which is what the first version did and why it threw. Fetch, do not add.
            var mo = go.GetComponent<MovableObject>();
            var rb = go.GetComponent<Rigidbody>();

            var box = go.AddComponent<BoxCollider>();
            box.size = item.grabBox;

            rb.linearDamping = 0.4f;
            rb.angularDamping = 1.5f;

            mo.displayName = "Cigarette";
            mo.weight = 0.2f;
            mo.contractValue = 0;
            mo.requiredForContract = false;
            mo.fragile = false;
            // The crew's own: it fits in a pocket, and taking it home is not theft.
            mo.pocketable = true;
            mo.ownedByGrandma = false;
            // MovableObject.Awake has already run and set the mass from the old weight.
            rb.mass = Mathf.Max(0.1f, mo.weight);

            item.Build();
            return item;
        }

        // The cigarette, filter end first, along +Y: a speckled cork filter with its gold band,
        // white paper, a charred line, the glowing ember and the grey ash on the tip. The total
        // length never changes (the tip is where the smoke and the clouds start), so as the ash
        // grows the paper gives way to it, and when the ash drops the paper is whole again:
        // nobody smokes one long enough to see it never gets shorter.
        //
        // Every burning piece sits under one child, Cigarette_Model: HeldPose moves the item's
        // direct children to draw it in the hand and puts back where they were when it was picked
        // up, so the pieces that change inside it must not be direct children.
        void Build()
        {
            // Awake fires the moment AddComponent runs, so the factory and Awake both
            // reach here on the same object. Building twice would double every piece.
            if (transform.childCount > 0) return;

            EnsureSharedMaterials();
            var model = new GameObject("Cigarette_Model").transform;
            model.SetParent(transform, false);

            float bottom = -length * 0.5f;
            Segment(model, "Cigarette_Filter", bottom, bottom + FilterLength, radius, filterMat);
            Segment(model, "Cigarette_Band", bottom + FilterLength - BandLength * 0.5f,
                    bottom + FilterLength + BandLength * 0.5f, radius * 1.03f, bandMat);
            paperPiece = Segment(model, "Cigarette_Paper", bottom + FilterLength, 0f, radius, paperMat);
            charPiece = Segment(model, "Cigarette_Char", 0f, CharLength, radius * 0.99f, charMat);
            emberMat = ItemArt.Mat(EmberDim, EmberLit);
            ember = Segment(model, "Cigarette_Ember", 0f, EmberLength, radius * 0.97f, emberMat);
            ashPiece = Segment(model, "Cigarette_Ash", 0f, AshFresh, radius * 0.93f, ashMat);

            // The ember's own light: a warm point on the fingers that swells on each drag.
            var lightGo = new GameObject("Cigarette_Glow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * (length * 0.5f);
            emberLight = lightGo.AddComponent<Light>();
            emberLight.type = LightType.Point;
            emberLight.color = GlowLight;
            emberLight.shadows = LightShadows.None;
            emberLight.renderMode = LightRenderMode.Auto;

            wisp = BuildTipWisp(transform, Vector3.up * (length * 0.5f), "Cigarette_Wisp", false);
            wisp.Play();
            BuildAshFall();
            LayoutBurn();
            SetGlow(RestGlow);
        }

        static void EnsureSharedMaterials()
        {
            if (paperMat == null) paperMat = Matte(ItemArt.Mat(Paper, Color.black), 0.18f);
            if (filterMat == null)
            {
                filterMat = Matte(ItemArt.Mat(Color.white, Color.black), 0.25f);
                filterMat.mainTexture = ItemArt.Speckle(64, FilterBase, FilterDark, FilterLight, 0.22f, 0.1f, 311);
            }
            if (bandMat == null) bandMat = Matte(ItemArt.Mat(Band, Color.black), 0.55f);
            if (charMat == null) charMat = Matte(ItemArt.Mat(Char, Color.black), 0.05f);
            if (ashMat == null)
            {
                ashMat = Matte(ItemArt.Mat(Color.white, Color.black), 0.02f);
                ashMat.mainTexture = ItemArt.Speckle(32, AshBase, AshDark, AshLight, 0.28f, 0.2f, 97);
            }
        }

        static Material Matte(Material m, float gloss)
        {
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            return m;
        }

        // A cylinder from y0 to y1 along the cigarette.
        static Transform Segment(Transform parent, string name, float y0, float y1, float r, Material mat)
        {
            var t = ItemArt.Piece(PrimitiveType.Cylinder, parent, name, Vector3.zero, Vector3.one, mat);
            PlaceSegment(t, y0, y1, r);
            var mr = t.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 11 mm: a shadow is noise
            return t;
        }

        static void PlaceSegment(Transform t, float y0, float y1, float r)
        {
            // Unity's cylinder is 2 high and 1 across.
            float h = Mathf.Max(0.0002f, y1 - y0);
            t.localPosition = new Vector3(0f, (y0 + y1) * 0.5f, 0f);
            t.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
        }

        // Paper, char, ember and ash for the current length of ash.
        void LayoutBurn()
        {
            if (paperPiece == null || ember == null || ashPiece == null || charPiece == null) return;
            float top = length * 0.5f;
            float ashStart = top - ash;
            float emberStart = ashStart - EmberLength;
            float charStart = emberStart - CharLength;
            float paperStart = -length * 0.5f + FilterLength;
            PlaceSegment(paperPiece, paperStart, charStart, radius);
            PlaceSegment(charPiece, charStart, emberStart, radius * 0.99f);
            // The ember swells a hair when drawn on (glow, from SetGlow).
            PlaceSegment(ember, emberStart, ashStart, radius * Mathf.Lerp(0.95f, 1.02f, glow));
            PlaceSegment(ashPiece, ashStart, top, radius * 0.93f);
        }

        // The ash grows while it burns, faster on a drag, and drops off when it is long.
        void Burn(float dt)
        {
            if (dt <= 0f) return;
            float drawing = smoking ? SmokeTimeline.Draw(phase) : 0f;
            ash += dt * (ashPerSecond + ashPerDrag * drawing);
            if (ash >= ashMax) ashDrop = true;
            LayoutBurn();
        }

        // In LateUpdate: HeldPose has drawn the cigarette where its owner sees it by then (its
        // Update), so the flakes fall from the tip in the hand, not from the carry point.
        void LateUpdate()
        {
            if (!ashDrop) return;
            ashDrop = false;
            if (ashFall != null && ashPiece != null)
            {
                Vector3 at = ashPiece.position;
                var p = new ParticleSystem.EmitParams();
                int n = 3 + Mathf.RoundToInt(ash / ashMax * 5f);
                for (int i = 0; i < n; i++)
                {
                    p.position = at + Random.insideUnitSphere * radius;
                    p.velocity = Random.insideUnitSphere * 0.08f + Vector3.down * 0.05f;
                    p.startSize = Random.Range(0.003f, 0.0075f);
                    p.startLifetime = Random.Range(0.9f, 1.5f);
                    ashFall.Emit(p, 1);
                }
            }
            ash = AshFresh;
            LayoutBurn();
        }

        void BuildAshFall()
        {
            var go = new GameObject("Cigarette_AshFall");
            go.transform.SetParent(transform, false);
            ashFall = go.AddComponent<ParticleSystem>();
            ashFall.Stop();
            var main = ashFall.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.61f, 0.59f, 1f), new Color(0.4f, 0.39f, 0.38f, 1f));
            main.gravityModifier = 0.35f;
            main.maxParticles = 24;
            var emission = ashFall.emission;
            emission.enabled = false;
            var col = ashFall.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = SmokeTextures.ParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ashFall.Play();
        }

        // Rates of the lit tip's thread: lying or held, and on a drag.
        public const float WispRestRate = 16f;
        public const float WispDragRate = 28f;

        // The thin thread of smoke off a lit tip: small soft wisps rising slowly, curled by noise,
        // widening and fading as they go. It says the thing is lit, and it is how you spot one on
        // the ground. Shared with the copy the other player sees in the body's hand (HandHeldProp).
        public static ParticleSystem BuildTipWisp(Transform parent, Vector3 localPosition, string name, bool playOnAwake)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = playOnAwake;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.80f, 0.82f, 0.88f, 0.30f), new Color(0.72f, 0.74f, 0.80f, 0.22f));
            main.gravityModifier = 0f;
            main.maxParticles = 140;

            var emission = ps.emission;
            emission.rateOverTime = WispRestRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.002f;

            // Rising, slowly: warm smoke off an ember, not a jet.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);
            vel.y = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);

            // The curl: fine noise, so the thread bends and loops as it climbs.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.12f);
            noise.frequency = 2.4f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.45f);
            noise.octaveCount = 2;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 3.6f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.06f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            SmokeTextures.UseWispSheet(ps);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = SmokeTextures.WispMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        void SetWisp(float rate)
        {
            if (wisp == null) return;
            var emission = wisp.emission;
            emission.rateOverTime = rate;
        }

        void SetGlow(float value)
        {
            glow = Mathf.Clamp01(value);
            if (emberMat == null) return;
            var c = Color.Lerp(EmberDim, EmberLit, glow);
            if (emberMat.HasProperty("_EmissionColor"))
                emberMat.SetColor("_EmissionColor", c * Mathf.Lerp(0.8f, 3f, glow));
            emberMat.color = c;
            if (emberLight != null)
            {
                // Barely there lying on the floor, a warm flare on the fingers at the end of a drag.
                float k = glow * glow;
                emberLight.intensity = Mathf.Lerp(0.08f, lightIntensity, k);
                emberLight.range = Mathf.Lerp(0.18f, lightRange, glow);
            }
        }

        // The breath you see leave the mouth: a dense, rolling puff blown at the spot where the
        // cloud forms, swelling and thinning out as it reaches it. Pure picture, no volume (the
        // clouds are the volume). It is not a child of the cigarette on purpose: the cigarette is
        // hidden from the other player's camera while it is at your lips (HandHeldProp), and the
        // breath must not be.
        sealed class ExhaleStream
        {
            // Seconds a wisp takes to reach the cloud, and dies on arrival: short enough to read
            // as blown, long enough not to read as a jet (2.5 m in about a second).
            const float FlightMin = 0.85f, FlightMax = 1.2f;

            readonly ParticleSystem ps;

            public ExhaleStream()
            {
                var go = new GameObject("Cigarette_Exhale");
                ps = go.AddComponent<ParticleSystem>();
                ps.Stop();

                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(FlightMin, FlightMax);   // set per wisp in Emit
                main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.86f, 0.86f, 0.9f, 0.75f), new Color(0.76f, 0.77f, 0.82f, 0.6f));
                main.gravityModifier = -0.03f;
                main.maxParticles = 160;

                var emission = ps.emission;
                emission.enabled = false;

                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.45f), new Keyframe(0.35f, 1.6f), new Keyframe(1f, 4.2f)));

                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);

                // Rolls and curls as it spreads.
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = new ParticleSystem.MinMaxCurve(0.3f);
                noise.frequency = 1.3f;
                noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.6f);
                noise.octaveCount = 2;
                noise.damping = true;
                noise.quality = ParticleSystemNoiseQuality.Medium;

                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.07f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);

                SmokeTextures.UseWispSheet(ps);
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.sharedMaterial = SmokeTextures.WispMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                ps.Play();
            }

            // A breath of `count` wisps from the mouth to the cloud at `to`, each arriving as it
            // fades, twice as many as it used to be: a mouthful is thick.
            public void Emit(Vector3 from, Vector3 to, int count)
            {
                if (ps == null) return;
                Vector3 path = to - from;
                var p = new ParticleSystem.EmitParams();
                int n = count * 2;
                for (int i = 0; i < n; i++)
                {
                    float flight = Random.Range(FlightMin, FlightMax);
                    p.position = from + Random.insideUnitSphere * 0.012f;
                    // Some fall short, some drift wide: a breath spreads, it is not a pipe.
                    p.velocity = path * (Random.Range(0.62f, 1f) / flight) + Random.insideUnitSphere * 0.28f;
                    p.startLifetime = flight;
                    ps.Emit(p, 1);
                }
            }

            public void Dispose()
            {
                if (ps != null) Object.Destroy(ps.gameObject);
            }
        }
    }
}
