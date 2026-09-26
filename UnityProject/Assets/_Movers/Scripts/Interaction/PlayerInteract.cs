using UnityEngine;

namespace Movers
{
    // The action button (E, X on a pad). Look at a door, a window or the garage door and press it.
    //
    // One ray from the eyes, about an arm and a step long, and the first thing it meets decides.
    // No outline, no highlight, just the verb under the crosshair: "[E] Open". A wall in the way
    // hides the door behind it. The thing in your hands does not, because what you carry is not
    // what you are looking at, and a mover opens doors with a box in his arms all day long.
    //
    // It takes the press with CrewInput.TryConsume, first, for this player only (execution order
    // -100): anything else that listens to the action button (the delivery at the truck is an
    // Interactable too) sees the press as spent, so opening the front door next to the truck
    // never settles the contract by accident. The other player's press is theirs.
    //
    // The prompt is drawn in this player's own view (bottom centre), so two players side by
    // side each read their own. It is the verb for this player (PromptFor): at a locked door
    // the one with the key and the one without read different things, and E does what was
    // read. With empty hands, something too heavy to lift says "[LMB] Drag".
    [DefaultExecutionOrder(-100)]
    public class PlayerInteract : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        public Transform cam;
        public PlayerGrab grab;

        [Header("Ray")]
        public float range = 2.6f;
        public LayerMask mask = Physics.DefaultRaycastLayers;

        [Header("Prompt")]
        public int fontSize = 18;   // at 1080 lines of view; scaled with the view's height

        // A window frame and the glass split off it can come back from the ray at almost the same
        // distance, in no particular order. Within this band the one that can be used wins, so
        // the prompt does not flicker between the glass and the frame around it.
        const float SamePlane = 0.02f;

        readonly RaycastHit[] hits = new RaycastHit[16];
        Interactable target;
        CrewInput input;
        CrewMember member;
        Camera view;

        // The label is rebuilt only when the prompt string or the device changes, never per frame.
        string shownPrompt;
        string shownKey;
        string label;

        // What the crosshair is on right now, or null. Other systems (a HUD hint, a tutorial)
        // can read it rather than cast their own ray.
        public Interactable Target => target;

        // Who is pressing, for an Interactable that cares (a lock and its key, the truck seat).
        public CrewMember Member => member != null ? member : (member = GetComponent<CrewMember>());
        public PlayerGrab Grab => grab;

        void Awake()
        {
            // Only GUI.Label is used. Without this, Unity runs a GUILayout pass (and allocates
            // for it) every frame, prompt or no prompt.
            useGUILayout = false;

            input = CrewSetup.InputOf(gameObject);
            if (grab == null) grab = GetComponent<PlayerGrab>();
            if (cam == null)
            {
                var pc = GetComponent<PlayerController>();
                if (pc != null) cam = pc.cam;
            }
            // No Camera.main fallback: with two players it would be the other player's eyes.
            if (cam == null)
                Debug.LogWarning("[PlayerInteract] no camera on " + name + ", the action button does nothing.");
        }

        void Update()
        {
            target = FindTarget();
            if (target == null) return;
            // Taken first: whatever Interact does, the press is spent for this player.
            if (!input.TryConsume(CrewButton.Interact)) return;
            target.Interact(this);
        }

        Interactable FindTarget()
        {
            if (cam == null) return null;

            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, hits, range, mask,
                                            QueryTriggerInteraction.Ignore);
            if (n <= 0) return null;
            SortByDistance(n);

            Transform held = grab != null && grab.Held != null ? grab.Held.transform : null;
            float first = -1f;

            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                Transform t = c.transform;
                if (t.IsChildOf(transform)) continue;                 // our own capsule, what we wear
                if (held != null && t.IsChildOf(held)) continue;      // the box in our arms

                if (first < 0f) first = hits[i].distance;
                else if (hits[i].distance > first + SamePlane) break; // something closer is in the way

                // Covers: a window frame is a whole wall module, and only the part of it near
                // the window counts as looking at the window.
                var it = c.GetComponentInParent<Interactable>();
                if (it != null && it.isActiveAndEnabled && it.CanInteract && it.Covers(hits[i].point)) return it;
            }
            return null;
        }

        // RaycastNonAlloc returns hits in no order. Insertion sort: a handful of hits, no garbage.
        void SortByDistance(int n)
        {
            for (int i = 1; i < n; i++)
            {
                RaycastHit h = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > h.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = h;
            }
        }

        // What the prompt says right now, and on which button, or null. OnGUI draws exactly
        // this; a tutorial hint or a test reads it here instead of parsing the screen.
        public string CurrentPrompt(out CrewButton button)
        {
            button = CrewButton.Interact;
            // For this player, never the anonymous Prompt: at a door the verb depends on who
            // holds its key, and Interact(this) acts for this player. Reading the verb for
            // somebody else would show "Locked" and then unlock, or "Open" and then lock.
            if (target != null) return target.PromptFor(this);
            if (grab != null && grab.DragTarget != null)
            {
                button = CrewButton.Grab;
                return "Drag";
            }
            return null;
        }

        void OnGUI()
        {
            // Same depth as the HUD: above SmokeVision's overlay (depth 5). In the smoke you
            // cannot see the door, you can still read that you are looking at one.
            GUI.depth = 0;
            if (Event.current.type != EventType.Repaint) return;

            string prompt = CurrentPrompt(out CrewButton button);
            if (string.IsNullOrEmpty(prompt)) return;

            if (view == null) view = CrewView.Of(this);
            if (!CrewView.TryGetRect(view, out Rect r)) return;

            string key = ButtonLabels.For(input, button);
            if (!ReferenceEquals(prompt, shownPrompt) || !ReferenceEquals(key, shownKey))
            {
                shownPrompt = prompt;
                shownKey = key;
                label = "[" + key + "] " + prompt;
            }

            // A shadowed label, because it sits on whatever the camera sees: a white wall, a
            // bright garden, the smoke.
            int size = ViewportGUI.FontSize(r, fontSize);
            var box = ViewportGUI.Region(r, HudRegion.BottomCenter, r.width, size + 12f);
            ViewportGUI.Label(box, label, size, Color.white, TextAnchor.MiddleCenter, true);
        }

        void OnDisable() { target = null; }
    }
}
