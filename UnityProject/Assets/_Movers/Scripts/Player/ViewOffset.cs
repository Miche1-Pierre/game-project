using UnityEngine;

namespace Movers
{
    // Moves what a player's camera shows without moving where the player is looking from: the
    // head tipped back over a bottle, the view tumbling to the floor after a blast.
    //
    // Same contract as CameraShake, extended to position: the offset is put on in LateUpdate and
    // taken off again right after the camera has rendered, so the picture moves and the game does
    // not. PlayerGrab aims the carry from the camera, PlayerInteract casts from it and SmokeVision
    // samples at it; none of them ever sees the offset. PlayerController rewrites the rotation
    // every Update, but it only writes the position when the crouch changes, which is why taking
    // the position back off is not optional here.
    //
    // Runs after CameraShake (1000), so whichever of the two gets OnPostRender first, this one's
    // offset is always the outermost and always comes off: a shake left on for a frame is a few
    // degrees, a tumble left on would be the whole view on the floor.
    //
    // Sources add to it every frame they want an offset (Add), before LateUpdate at 1010; nothing
    // added means nothing applied. Added on demand, like CameraShake, and idle at no cost.
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1010)]
    public sealed class ViewOffset : MonoBehaviour
    {
        Camera cam;

        // This frame's request, summed over the sources.
        Vector3 addPosition;
        Quaternion addParentRotation = Quaternion.identity;
        Quaternion addCameraRotation = Quaternion.identity;
        bool requested;

        // What was there before the offset, and what the offset wrote, so it is only taken off
        // when nobody else has written the camera since.
        bool applied;
        Vector3 basePosition, writtenPosition;
        Quaternion baseRotation, writtenRotation;

        public bool IsApplied => applied;

        public static ViewOffset For(Camera camera)
        {
            if (camera == null) return null;
            var v = camera.GetComponent<ViewOffset>();
            if (v == null) v = camera.gameObject.AddComponent<ViewOffset>();
            return v;
        }

        // position: metres, in the camera's parent space (the player). parentRotation turns the
        // view about the eye in that same space (a fall to the side); cameraRotation turns it in
        // the camera's own space, after everything else (a look up is Euler(-x, 0, 0)).
        public void Add(Vector3 position, Quaternion parentRotation, Quaternion cameraRotation)
        {
            addPosition += position;
            addParentRotation = parentRotation * addParentRotation;
            addCameraRotation = addCameraRotation * cameraRotation;
            requested = true;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Update()
        {
            // Normally a no-op: OnPostRender already took the offset off.
            Restore();
        }

        void LateUpdate()
        {
            Restore();
            if (!requested) return;

            Vector3 p = addPosition;
            Quaternion pr = addParentRotation, cr = addCameraRotation;
            addPosition = Vector3.zero;
            addParentRotation = addCameraRotation = Quaternion.identity;
            requested = false;

            // A camera that is not going to render gets nothing: OnPostRender would never take the
            // offset off, and the next physics step would aim the carry from the floor.
            if (cam == null || !cam.isActiveAndEnabled) return;

            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            writtenPosition = basePosition + p;
            writtenRotation = pr * baseRotation * cr;
            transform.localPosition = writtenPosition;
            transform.localRotation = writtenRotation;
            applied = true;
        }

        // Built-in pipeline: called on components next to the Camera, right after it rendered.
        void OnPostRender()
        {
            Restore();
        }

        void OnDisable()
        {
            Restore();
        }

        void Restore()
        {
            if (!applied) return;
            applied = false;
            if (transform.localPosition == writtenPosition) transform.localPosition = basePosition;
            if (transform.localRotation == writtenRotation) transform.localRotation = baseRotation;
        }
    }
}
