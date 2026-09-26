using UnityEngine;

namespace Movers
{
    // One crew member (a player), on the player root next to PlayerController. The one place
    // other systems go to find "the players": the explosion knocks them, the grandmother watches
    // them, the HUD draws in their viewport. Nothing outside Player/ looks up PlayerController
    // or Camera.main directly any more.
    [DefaultExecutionOrder(-550)]
    [DisallowMultipleComponent]
    public sealed class CrewMember : MonoBehaviour
    {
        public int index = 0;                          // 0 = P1, 1 = P2
        public Color color = new Color(0.85f, 0.2f, 0.2f);

        public CrewInput Input { get; private set; }
        public PlayerController Controller { get; private set; }
        public PlayerGrab Grab { get; private set; }
        public PlayerInteract Interact { get; private set; }
        public PlayerPockets Pockets { get; private set; }
        public Camera View { get; private set; }

        // Set by the truck while this player sits at the wheel.
        public bool IsDriving { get; set; }

        public string DisplayName => "P" + (index + 1);
        public MovableObject Held => Grab != null ? Grab.Held : null;
        public Vector3 Position => transform.position;
        public Vector3 EyePosition => View != null ? View.transform.position : transform.position + Vector3.up * 1.6f;
        public Vector3 LookDirection => View != null ? View.transform.forward : transform.forward;

        void Awake()
        {
            Resolve();
        }

        // Public so a spawner can call it after adding components at runtime.
        public void Resolve()
        {
            Input = GetComponent<CrewInput>();
            Controller = GetComponent<PlayerController>();
            Grab = GetComponent<PlayerGrab>();
            Interact = GetComponent<PlayerInteract>();
            Pockets = GetComponent<PlayerPockets>();
            View = null;
            if (Controller != null && Controller.cam != null) View = Controller.cam.GetComponent<Camera>();
            if (View == null) View = GetComponentInChildren<Camera>(true);
        }

        void OnEnable() { CrewRoster.Register(this); }
        void OnDisable() { CrewRoster.Unregister(this); }
    }
}
