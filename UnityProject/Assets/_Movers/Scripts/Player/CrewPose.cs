using UnityEngine;

namespace Movers
{
    // Which pose a crew body holds: arms at rest, or the carry pose while its player carries
    // something. Sets the "Carrying" bool of AC_Crew from PlayerGrab.IsCarrying and leaves the
    // blend to the controller.
    //
    // Until 2026-09-25 the crew had the carry pose only, so a player with empty hands stood
    // with his arms out in front of him, and a garment on that body could not be judged.
    //
    // Lives on the crew prefabs, next to the Animator (MoversCrewPoseCLI puts it there). A crew
    // body with no PlayerGrab above it, standing in a scene on its own, simply stays at rest.
    [RequireComponent(typeof(Animator))]
    public class CrewPose : MonoBehaviour
    {
        public static readonly int Carrying = Animator.StringToHash("Carrying");

        Animator animator;
        PlayerGrab grab;

        void Awake()
        {
            animator = GetComponent<Animator>();
            grab = GetComponentInParent<PlayerGrab>();
        }

        void Update()
        {
            animator.SetBool(Carrying, grab != null && grab.IsCarrying);
        }
    }
}
