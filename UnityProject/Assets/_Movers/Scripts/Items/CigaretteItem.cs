using UnityEngine;

namespace Movers
{
    // A lit cigarette lying in the world. Pick it up like anything else; hold the right button
    // while you are carrying it and it smokes.
    //
    // The smoke leaves the tip, not your face, which is the one real gameplay change from the
    // viewmodel version: the cigarette is wherever your hands put it, so you can hold it out
    // at arm's length and fog a doorway without fogging yourself, or pull it in with the scroll
    // wheel and blind nobody but you. Aiming the smoke is now a thing you can be bad at.
    //
    // What a puff does has not changed and lives in SmokeCloud: 7 seconds, everyone inside it.
    public class CigaretteItem : HeldUsable
    {
        [Header("Smoking")]
        public float cloudLifetime = SmokeCloud.DefaultLifetime;
        public float puffInterval = 0.5f;     // one mouthful every half second while you hold
        public float puffAhead = 0.35f;       // how far past the tip a puff is born
        public float puffScatter = 0.12f;
        public float puffStrength = 1f;

        [Header("The thing itself")]
        public float length = 0.09f;
        // Hand sized, not cigarette sized. The visual is 13 mm across and nobody can put a
        // crosshair on that at three metres, so the object you actually grab is a fist.
        public Vector3 grabBox = new Vector3(0.1f, 0.12f, 0.1f);

        static readonly Color Paper = new Color(0.93f, 0.92f, 0.88f);
        static readonly Color EmberDim = new Color(0.30f, 0.07f, 0.03f);
        static readonly Color EmberLit = new Color(1f, 0.42f, 0.10f);

        Material emberMat;
        Transform ember;
        ParticleSystem wisp;
        float nextPuff;
        bool smoking;

        public bool IsSmoking => smoking;

        void Awake()
        {
            if (transform.childCount == 0) Build();
        }

        public override void OnUseBegin()
        {
            smoking = true;
            SetGlow(1f);
            SetWisp(45f);
            nextPuff = 0f;   // the first puff lands the instant you press, not half a second later
        }

        public override void OnUseHold(float dt)
        {
            if (Time.time < nextPuff) return;
            Puff();
            nextPuff = Time.time + Mathf.Max(0.1f, puffInterval);
        }

        public override void OnUseEnd()
        {
            smoking = false;
            SetGlow(0.25f);
            SetWisp(2.5f);
        }

        public override void OnReleased(bool thrown)
        {
            // Let go mid-drag and the cigarette stops smoking, wherever it lands.
            if (smoking) OnUseEnd();
            base.OnReleased(thrown);
        }

        Vector3 Tip => transform.TransformPoint(Vector3.up * (length * 0.5f));

        void Puff()
        {
            // Away from whoever is holding it. Falling back to the cigarette's own axis keeps
            // this honest if something other than a player is ever holding one.
            Vector3 dir = transform.up;
            if (holder != null && holder.cam != null) dir = holder.cam.forward;

            Vector3 at = Tip + dir * puffAhead + Random.insideUnitSphere * puffScatter;
            SmokeCloud.Spawn(at, dir, cloudLifetime, puffStrength);
        }

        // ---- the object ----

        public static CigaretteItem Create(Vector3 at)
        {
            var go = new GameObject("Cigarette");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);  // lying down

            var item = go.AddComponent<CigaretteItem>();
            var box = go.AddComponent<BoxCollider>();
            box.size = item.grabBox;

            var rb = go.AddComponent<Rigidbody>();
            rb.linearDamping = 0.4f;
            rb.angularDamping = 1.5f;

            var mo = go.AddComponent<MovableObject>();
            mo.displayName = "Cigarette";
            mo.weight = 0.2f;
            mo.contractValue = 0;
            mo.requiredForContract = false;
            mo.fragile = false;

            item.Build();
            return item;
        }

        void Build()
        {
            var paper = ItemArt.Mat(Paper, Color.black);
            ItemArt.Piece(PrimitiveType.Cylinder, transform, "Cigarette_Body",
                          Vector3.zero, new Vector3(0.013f, length * 0.5f, 0.013f), paper);

            emberMat = ItemArt.Mat(EmberDim, EmberLit);
            ember = ItemArt.Piece(PrimitiveType.Cube, transform, "Cigarette_Ember",
                                  Vector3.up * (length * 0.5f), Vector3.one * 0.015f, emberMat);

            BuildWisp();
            SetGlow(0.25f);
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

        void SetGlow(float glow)
        {
            if (emberMat == null) return;
            var c = Color.Lerp(EmberDim, EmberLit, Mathf.Clamp01(glow));
            if (emberMat.HasProperty("_EmissionColor"))
                emberMat.SetColor("_EmissionColor", c * Mathf.Lerp(0.6f, 2.2f, glow));
            emberMat.color = c;
            if (ember != null) ember.localScale = Vector3.one * Mathf.Lerp(0.013f, 0.017f, glow);
        }
    }
}
