using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // What the other player sees while you smoke or drink: the cigarette between your fingers,
    // the bottle in your fist, going to your mouth with the hand (Crew_Smoke, Crew_Drink). Goes
    // on the player root, next to HeldPose.
    //
    // The real cigarette cannot be in two places. For its owner it sits in the hand or at the
    // lips, in front of a camera that is inside the body's head (HeldPose); its rigidbody stays
    // out on the carry. From the other side of the room neither place is a hand. So, while the
    // thing is in your hand (HeldPose.HoldsSmall, where FirstPersonHands puts the body's right
    // hand too) or at your mouth (HeldPose.InHand), every camera but yours:
    //   - does not draw the real item (its renderers are switched off just for that camera);
    //   - draws a copy of it parented to the body's right-hand socket (HandSockets), so it goes
    //     wherever the animated hand goes.
    // Your own camera never draws the copy: it is part of the body, and FirstPersonBody already
    // keeps your own body out of your own view. The mirror is another camera, so in the mirror
    // you see yourself smoking with the cigarette in your hand.
    //
    // Chosen over the two alternatives:
    //   - moving the real object into the hand: its physics would have to follow an animated
    //     bone, and its owner would lose the cigarette at the lips (the body is hidden from them);
    //   - IK of the body's arm to the real object: the object is where the first-person view
    //     wants it, 12 cm from the eyes or out at the carry point, not where an arm can hold it
    //     to the mouth, and the pose would stop being the authored clip.
    // A copy costs two renderers that share the item's meshes and materials, so the ember
    // glows and the bottle empties on both at once, with no code to keep them in step.
    //
    // The copy's grip is authored with the clips (tools/blender/author_clips.py, CIG and BEER,
    // in the socket frame: x the thumb, y out of the palm, z along the fingers, metres).
    // Before HeldPose (20): the copy is made from the item's pieces where they sit on it, and
    // HeldPose's Update moves them to the lips for the rest of the frame.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class HandHeldProp : MonoBehaviour
    {
        // author_clips.py CIG and BEER: the item's centre, and its +Y axis (the lit tip, the neck).
        static readonly Vector3 CigaretteCentre = new Vector3(0.012f, -0.012f, 0.062f);
        static readonly Vector3 CigaretteAxis = new Vector3(0f, -1f, 0f);
        static readonly Vector3 BottleCentre = new Vector3(-0.02f, 0.022f, 0f);
        static readonly Vector3 BottleAxis = new Vector3(1f, 0f, 0f);

        HeldPose pose;
        CrewMember member;
        HandSockets sockets;

        MovableObject copied;        // the item the copy was made from
        Transform copy;
        ParticleSystem copyWisp;
        readonly List<Renderer> real = new List<Renderer>(8);
        readonly List<Renderer> scratch = new List<Renderer>(8);
        bool realHidden;
        bool shown;
        // Cameras render inside each other (a mirror renders while the view that sees it does):
        // after an inner one, the outer one's choice is made again.
        readonly Camera[] stack = new Camera[4];
        int depth;

        public bool Showing => shown;
        public Transform Copy => copy;

        void Awake()
        {
            pose = GetComponent<HeldPose>();
            member = GetComponent<CrewMember>();
        }

        void Start()
        {
            // The body's hand sockets, the ones the grandmother's props use. Added here on a crew
            // player, whose body has none; no body (the tutorial's capsule), no copy.
            var body = GetComponentInChildren<Animator>(true);
            if (body == null || !body.isHuman) { enabled = false; return; }
            sockets = GetComponent<HandSockets>();
            if (sockets == null) sockets = gameObject.AddComponent<HandSockets>();
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
            ShowReal();
            if (copy != null) copy.gameObject.SetActive(false);
            shown = false;
            depth = 0;
        }

        void OnDestroy()
        {
            if (copy != null) Destroy(copy.gameObject);
        }

        void Update()
        {
            if (pose == null || sockets == null || sockets.Right == null) return;
            // A new thing in the hands: copy it now, while its pieces are at home on it.
            MovableObject current = pose.Current;
            if (current != null && !ReferenceEquals(current, copied) && pose.PiecesAtHome) Build(current);
        }

        void LateUpdate()
        {
            if (pose == null || sockets == null || sockets.Right == null) return;
            bool want = (pose.InHand || pose.HoldsSmall) && pose.Current != null && ReferenceEquals(pose.Current, copied)
                        && copy != null && !(member != null && member.IsDriving);

            if (want != shown)
            {
                shown = want;
                if (copy != null) copy.gameObject.SetActive(want);
                if (!want) ShowReal();
            }
            if (shown && copyWisp != null)
            {
                var em = copyWisp.emission;
                em.enabled = pose.Mode == HeldPose.Use.Smoke;
            }
        }

        // ---- per camera: the real item is drawn for its owner only ----------------------

        void OnCameraPreCull(Camera cam)
        {
            if (depth < stack.Length) stack[depth] = cam;
            depth++;
            Apply(cam);
        }

        void OnCameraPostRender(Camera cam)
        {
            if (depth > 0) depth--;
            // Back to drawn between renders; inside another render, back to that camera's choice.
            if (depth == 0) ShowReal();
            else if (depth <= stack.Length) Apply(stack[depth - 1]);
        }

        void Apply(Camera cam)
        {
            bool own = member != null && cam == member.View && !member.IsDriving;
            if (!shown || own) ShowReal();
            else HideReal();
        }

        void HideReal()
        {
            if (realHidden) return;
            realHidden = true;
            for (int i = 0; i < real.Count; i++)
                if (real[i] != null) real[i].forceRenderingOff = true;
        }

        void ShowReal()
        {
            if (!realHidden) return;
            realHidden = false;
            for (int i = 0; i < real.Count; i++)
                if (real[i] != null) real[i].forceRenderingOff = false;
        }

        // ---- the copy -------------------------------------------------------------------

        void Build(MovableObject item)
        {
            ShowReal();
            if (copy != null) Destroy(copy.gameObject);
            copy = null;
            copyWisp = null;
            copied = item;
            real.Clear();
            item.GetComponentsInChildren(true, real);

            bool cigarette = item.GetComponent<CigaretteItem>() != null;
            bool bottle = item.GetComponent<BeerItem>() != null;
            if (!cigarette && !bottle) return;   // nothing else is ever held to the mouth

            var root = new GameObject("HandHeld_" + item.name).transform;
            // Pieces are read in the item's own space, so wherever the item lies this frame.
            root.SetParent(sockets.Right, false);
            // Sized in metres whatever the scale of the bones above (as GrandmaProps does).
            float s = sockets.Right.lossyScale.x;
            if (s > 1e-4f) root.localScale = Vector3.one / s;
            Vector3 centre = cigarette ? CigaretteCentre : BottleCentre;
            Vector3 axis = cigarette ? CigaretteAxis : BottleAxis;
            root.localPosition = centre / Mathf.Max(1e-4f, s);
            root.localRotation = Quaternion.FromToRotation(Vector3.up, axis);

            // One renderer per mesh piece of the item, where it sits on the item, sharing its
            // mesh and materials.
            Transform itemRoot = item.transform;
            scratch.Clear();
            item.GetComponentsInChildren(true, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                var mr = scratch[i] as MeshRenderer;
                if (mr == null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var piece = new GameObject(mr.name).transform;
                piece.SetParent(root, false);
                piece.localPosition = itemRoot.InverseTransformPoint(mr.transform.position);
                piece.localRotation = Quaternion.Inverse(itemRoot.rotation) * mr.transform.rotation;
                piece.localScale = mr.transform.lossyScale;
                piece.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = piece.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                // No shadow: FirstPersonBody turns shadowless body renderers off for the owner's
                // camera, so the owner never sees the copy, not even as a shadow beside the real one.
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            scratch.Clear();

            if (cigarette) copyWisp = BuildWisp(root, item.GetComponent<CigaretteItem>().length * 0.5f);
            root.gameObject.SetActive(false);
            copy = root;
        }

        // The lit tip's thread of smoke, on the copy: the real cigarette's is hidden with it.
        // The same thread as the real one (CigaretteItem.BuildTipWisp). The copy is switched on
        // and off with the drag, so it starts again by itself each time it shows.
        static ParticleSystem BuildWisp(Transform parent, float tip)
        {
            var ps = CigaretteItem.BuildTipWisp(parent, Vector3.up * tip, "HandHeld_Wisp", true);
            var emission = ps.emission;
            emission.rateOverTime = CigaretteItem.WispDragRate;
            return ps;
        }
    }
}
