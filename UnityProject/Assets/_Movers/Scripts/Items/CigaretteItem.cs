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
        // Hand sized, not cigarette sized. The visual is 13 mm across and nobody can put a
        // crosshair on that at three metres, so the object you actually grab is a fist.
        public Vector3 grabBox = new Vector3(0.1f, 0.12f, 0.1f);

        static readonly Color Paper = new Color(0.93f, 0.92f, 0.88f);
        static readonly Color EmberDim = new Color(0.30f, 0.07f, 0.03f);
        static readonly Color EmberLit = new Color(1f, 0.42f, 0.10f);

        const float RestGlow = 0.25f;       // lying there, lit
        const float HeldGlow = 0.4f;        // between drags

        Material emberMat;
        Transform ember;
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
            SetWisp(8f);
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
            SetWisp(2.5f);
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
            base.OnReleased(thrown);
        }

        void Update()
        {
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

        void Build()
        {
            // Awake fires the moment AddComponent runs, so the factory and Awake both
            // reach here on the same object. Building twice would double every piece.
            if (transform.childCount > 0) return;

            var paper = ItemArt.Mat(Paper, Color.black);
            ItemArt.Piece(PrimitiveType.Cylinder, transform, "Cigarette_Body",
                          Vector3.zero, new Vector3(0.013f, length * 0.5f, 0.013f), paper);

            emberMat = ItemArt.Mat(EmberDim, EmberLit);
            ember = ItemArt.Piece(PrimitiveType.Cube, transform, "Cigarette_Ember",
                                  Vector3.up * (length * 0.5f), Vector3.one * 0.015f, emberMat);

            BuildWisp();
            SetGlow(RestGlow);
        }

        // The thin trail that says the thing is lit, and the only way to spot one on the ground.
        void BuildWisp()
        {
            var go = new GameObject("Cigarette_Wisp");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * (length * 0.5f);

            wisp = go.AddComponent<ParticleSystem>();
            wisp.Stop();

            var main = wisp.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.8f, 0.84f, 0.45f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.maxParticles = 90;

            var emission = wisp.emission;
            emission.rateOverTime = 2.5f;

            var shape = wisp.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            var size = wisp.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(2.6f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(1f, 1.4f)));

            var col = wisp.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = SmokeTextures.ParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            wisp.Play();
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
                emberMat.SetColor("_EmissionColor", c * Mathf.Lerp(0.6f, 2.2f, glow));
            emberMat.color = c;
            if (ember != null) ember.localScale = Vector3.one * Mathf.Lerp(0.013f, 0.017f, glow);
        }

        // The breath you see leave the mouth: a short stream of small wisps, blown at the spot
        // where the cloud forms and thinning out as they reach it. Pure picture, no volume (the
        // clouds are the volume). It is not a child of the cigarette on purpose: the cigarette is
        // hidden from the other player's camera while it is at your lips (HandHeldProp), and the
        // breath must not be.
        sealed class ExhaleStream
        {
            // Seconds a wisp takes to reach the cloud, and dies on arrival: short enough to read
            // as blown, long enough not to read as a jet (2.5 m in about a second).
            const float FlightMin = 0.85f, FlightMax = 1.15f;

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
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.78f, 0.82f, 0.55f));
                main.gravityModifier = -0.03f;
                main.maxParticles = 64;

                var emission = ps.emission;
                emission.enabled = false;

                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.4f), new Keyframe(1f, 3.2f)));

                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);

                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.sharedMaterial = SmokeTextures.ParticleMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                ps.Play();
            }

            // count wisps from the mouth to the cloud at `to`, each arriving as it fades.
            public void Emit(Vector3 from, Vector3 to, int count)
            {
                if (ps == null) return;
                Vector3 path = to - from;
                var p = new ParticleSystem.EmitParams();
                for (int i = 0; i < count; i++)
                {
                    float flight = Random.Range(FlightMin, FlightMax);
                    p.position = from + Random.insideUnitSphere * 0.015f;
                    // Some fall short: a breath spreads, it is not a pipe.
                    p.velocity = path * (Random.Range(0.7f, 1f) / flight) + Random.insideUnitSphere * 0.2f;
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
