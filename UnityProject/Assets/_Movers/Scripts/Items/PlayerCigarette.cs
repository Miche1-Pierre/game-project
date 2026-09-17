using UnityEngine;

namespace Movers
{
    // The cigarette the crew starts the job with.
    //
    // GREYBOX_SPEC lists it in the starting inventory as deliberately absurd and useful for
    // nothing. This makes it do something: hold the right mouse button and it smokes, hard,
    // and the cloud it leaves blinds anyone standing in it, including you. That is a design
    // change, not an implementation detail, and it is recorded in ADR-005.
    //
    // Input, and why there is no conflict: the right button throws what you are carrying
    // (PlayerGrab), and it only throws when something is in your hands. The cigarette only
    // lights when your hands are free. One button, two jobs, split by whether you are holding
    // a sofa, which is the same trick the scroll wheel already plays with the rotate key.
    //
    // Everything visible here is built at Play, so the component is the whole install: add it
    // to the player and there is a cigarette in the scene. No prefab, no art (CLAUDE.md 13).
    public class PlayerCigarette : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        public Transform cam;
        public PlayerGrab grab;

        [Header("Input")]
        public int smokeButton = 1;          // right mouse, hold

        [Header("Smoke")]
        // The spec number: a puff is gone, completely, 7 seconds after it leaves the mouth.
        public float cloudLifetime = SmokeCloud.DefaultLifetime;
        public float puffInterval = 0.5f;    // one mouthful every half second while you hold
        public float puffForward = 0.55f;    // how far in front of the eyes a puff is born
        public float puffScatter = 0.15f;    // so a stationary player does not stack rings
        public float puffStrength = 1f;

        [Header("Look of the thing in your hand")]
        public Vector3 holdPosition = new Vector3(0.24f, -0.16f, 0.40f);
        public Vector3 holdTilt = new Vector3(72f, -8f, 0f);
        public float length = 0.09f;

        // Deliberately not implemented: a cigarette that burns down and runs out. The greybox
        // question is whether the smoke is funny, and an item that can be spent answers a
        // different question. Set a budget here when that one is worth asking.

        Transform view;
        Transform ember;
        Material emberMat;
        ParticleSystem tip;
        float nextPuff;
        bool smoking;

        public bool IsSmoking => smoking;

        void Awake()
        {
            if (cam == null)
            {
                var pc = GetComponent<PlayerController>();
                if (pc != null && pc.cam != null) cam = pc.cam;
            }
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (grab == null) grab = GetComponent<PlayerGrab>();

            if (cam == null)
            {
                Debug.LogWarning("[Cigarette] no camera found on " + name + ", the cigarette stays in the packet.");
                enabled = false;
                return;
            }

            // The blindness belongs to the eyes, not to the cigarette: a player with no
            // cigarette still has to suffer someone else's. The scene builders add this
            // themselves, so this line only catches a player wired by hand.
            if (cam.GetComponent<SmokeVision>() == null) cam.gameObject.AddComponent<SmokeVision>();

            BuildView();
        }

        void Update()
        {
            bool handsFree = grab == null || !grab.IsCarrying;
            smoking = handsFree && Input.GetMouseButton(smokeButton);

            // Hands full: the cigarette goes behind your ear, out of frame. It also stops the
            // fridge from being delivered with a lit cigarette floating through it.
            if (view != null && view.gameObject.activeSelf != handsFree)
            {
                view.gameObject.SetActive(handsFree);
                if (!handsFree) nextPuff = 0f;
            }

            if (!smoking)
            {
                SetTipRate(2.5f);
                SetEmber(0.25f);
                nextPuff = 0f;
                return;
            }

            SetTipRate(45f);
            SetEmber(1f);

            // The first puff lands the instant you press, and the rest follow the cadence.
            // Waiting half a second for the first one makes the button feel broken.
            if (Time.time >= nextPuff)
            {
                Puff();
                nextPuff = Time.time + Mathf.Max(0.1f, puffInterval);
            }
        }

        void Puff()
        {
            Vector3 at = cam.position + cam.forward * puffForward - cam.up * 0.07f;
            at += Random.insideUnitSphere * puffScatter;
            SmokeCloud.Spawn(at, cam.forward, cloudLifetime, puffStrength);
        }

        // ---- the thing in your hand ----

        void BuildView()
        {
            var root = new GameObject("CigaretteView");
            view = root.transform;
            view.SetParent(cam, false);
            view.localPosition = holdPosition;
            view.localRotation = Quaternion.Euler(holdTilt);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Cigarette_Body";
            Kill(body.GetComponent<Collider>());   // never let the viewmodel block the grab ray
            body.transform.SetParent(view, false);
            body.transform.localScale = new Vector3(0.013f, length * 0.5f, 0.013f);
            body.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.93f, 0.92f, 0.88f), Color.black);

            var tipAt = Vector3.up * (length * 0.5f);

            var emberGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            emberGO.name = "Cigarette_Ember";
            Kill(emberGO.GetComponent<Collider>());
            emberGO.transform.SetParent(view, false);
            emberGO.transform.localPosition = tipAt;
            emberGO.transform.localScale = Vector3.one * 0.015f;
            emberMat = Mat(new Color(0.25f, 0.06f, 0.03f), new Color(1f, 0.35f, 0.08f));
            emberGO.GetComponent<Renderer>().sharedMaterial = emberMat;
            ember = emberGO.transform;

            BuildTipWisp(tipAt);
            SetEmber(0.25f);
        }

        // The thin trail that says the thing is lit. It simulates in world space, so walking
        // leaves the wisp behind you instead of dragging it along your face.
        void BuildTipWisp(Vector3 tipAt)
        {
            var go = new GameObject("Cigarette_Wisp");
            go.transform.SetParent(view, false);
            go.transform.localPosition = tipAt;

            tip = go.AddComponent<ParticleSystem>();
            tip.Stop();

            var main = tip.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.9f, 0.92f, 0.45f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.maxParticles = 90;

            var emission = tip.emission;
            emission.rateOverTime = 2.5f;

            var shape = tip.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            var size = tip.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(2.6f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(1f, 1.4f)));

            var col = tip.colorOverLifetime;
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

            tip.Play();
        }

        void SetTipRate(float rate)
        {
            if (tip == null) return;
            var emission = tip.emission;
            emission.rateOverTime = rate;
        }

        void SetEmber(float glow)
        {
            if (emberMat == null) return;
            var c = Color.Lerp(new Color(0.30f, 0.07f, 0.03f), new Color(1f, 0.42f, 0.10f), Mathf.Clamp01(glow));
            if (emberMat.HasProperty("_EmissionColor")) emberMat.SetColor("_EmissionColor", c * Mathf.Lerp(0.6f, 2.2f, glow));
            emberMat.color = c;
            if (ember != null) ember.localScale = Vector3.one * Mathf.Lerp(0.013f, 0.017f, glow);
        }

        static Material Mat(Color albedo, Color emission)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { color = albedo, hideFlags = HideFlags.HideAndDontSave };
            if (emission != Color.black && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
