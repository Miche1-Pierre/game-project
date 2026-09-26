using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A player's own camera does not draw the inside of that player's head. Goes on the player
    // root, next to CrewAnimator.
    //
    // The eyes sit inside the body's skull, so without this the first-person view shows teeth,
    // shoulders and a pair of arms held out for a box. The body must still exist for everyone
    // else: the other player reads you by it, and the grandmother's mirror is there so you can
    // see what you are wearing.
    //
    // Chosen over the two usual answers:
    //   - a layer per player, culled by its own camera: needs a layer for each player on top
    //     of the slice's six, and every worn piece moved to it and back, since worn things are
    //     parented to the bones and keep their own layer;
    //   - pushing the camera forward out of the face: the arms and the chest still come into
    //     view as soon as you look down, and the eyes stop matching the body.
    // Here, just before each camera culls, the body is switched to "shadows only" if that
    // camera is this player's own, and back otherwise. Everything under the body counts, worn
    // pieces included, and you keep your own shadow on the floor. A camera rendered from inside
    // another one (the mirror renders during the view that sees it) is handled with a small
    // stack, so the mirror shows you and your own view, which resumes after it, still does not.
    //
    // Nothing is carried from one render to the next: the body is whole again as soon as the
    // outermost render ends, and every render of the own camera looks at the body afresh. With
    // only one camera on (solo layouts, a one-player scene) nothing else would ever put it back,
    // and a dressing gown taken off after the first frame would lie on the floor as a shadow.
    //
    // At the wheel the same camera is the truck's chase camera, looking at the driver from
    // outside: the body is left whole then.
    [DisallowMultipleComponent]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        // Left empty: the player's camera and the Animator under this player.
        public Camera eyes;
        public Transform body;

        readonly List<Renderer> scratch = new List<Renderer>(16);
        readonly List<Renderer> madeShadowOnly = new List<Renderer>(16);
        readonly List<Renderer> madeInvisible = new List<Renderer>(4);
        readonly Camera[] stack = new Camera[4];
        int depth;
        bool hidden;
        CrewMember member;

        void OnEnable()
        {
            Resolve();
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
        }

        void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            Show();
            depth = 0;
        }

        void Resolve()
        {
            if (member == null) member = GetComponent<CrewMember>();
            if (eyes == null)
            {
                var pc = GetComponent<PlayerController>();
                if (pc != null && pc.cam != null) eyes = pc.cam.GetComponent<Camera>();
            }
            if (body == null)
            {
                var anim = GetComponentInChildren<Animator>(true);
                if (anim != null) body = anim.transform;
            }
        }

        // Every frame starts from an empty stack and a whole body: a camera that culled but
        // never rendered (it was switched off in between) must not leave either off for good.
        void LateUpdate()
        {
            depth = 0;
            Show();
        }

        void OnCameraPreCull(Camera cam)
        {
            if (depth < stack.Length) stack[depth] = cam;
            depth++;
            Apply(cam);
        }

        void OnCameraPostRender(Camera cam)
        {
            if (depth > 0) depth--;
            // The outermost render is over: whole again until the next one decides.
            if (depth == 0) Show();
            // Back to whichever camera was rendering around the one that just finished.
            else if (depth <= stack.Length) Apply(stack[depth - 1]);
        }

        void Apply(Camera cam)
        {
            if (eyes == null || body == null || member == null) Resolve();
            bool driving = member != null && member.IsDriving;
            bool own = cam != null && cam == eyes && !driving;
            if (own) Hide();
            else Show();
        }

        void Hide()
        {
            if (hidden || body == null) return;
            hidden = true;
            body.GetComponentsInChildren(true, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                var r = scratch[i];
                if (r == null || !r.enabled) continue;
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    madeShadowOnly.Add(r);
                }
                else if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && !r.forceRenderingOff)
                {
                    r.forceRenderingOff = true;
                    madeInvisible.Add(r);
                }
            }
            scratch.Clear();
        }

        void Show()
        {
            if (!hidden) return;
            hidden = false;
            for (int i = 0; i < madeShadowOnly.Count; i++)
                if (madeShadowOnly[i] != null)
                    madeShadowOnly[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            for (int i = 0; i < madeInvisible.Count; i++)
                if (madeInvisible[i] != null) madeInvisible[i].forceRenderingOff = false;
            madeShadowOnly.Clear();
            madeInvisible.Clear();
        }
    }
}
