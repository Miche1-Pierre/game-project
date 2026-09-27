using UnityEngine;

namespace Movers
{
    // The crew on this machine: which players exist and who drives which (ADR-009, two local
    // players). Goes on _Systems.
    //
    // At Play it turns the scene's one player into P1 and clones it into P2, with the blue
    // body, beside it. There is no player prefab: the scene player is the reference, so every
    // value tuned on it (the carry numbers above all) is the value both players get.
    //
    // It owns the input sources. P1 takes the keyboard and mouse, P2 the first gamepad when one
    // is plugged in, otherwise nothing and stands still. F1 moves the keyboard to the other
    // player for solo testing, Shift+F1 brings the other player next to the one you drive.
    //
    // Runs before every player component (-560, CrewMember is -550) so the clone is made from
    // the scene data, before anything has woken up and built runtime children on P1.
    [DefaultExecutionOrder(-560)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CursorLock))]
    public sealed class CrewSpawner : MonoBehaviour
    {
        public static CrewSpawner Active { get; private set; }

        [Header("Players")]
        // Left empty: the player in the scene.
        public PlayerController firstPlayer;
        public bool spawnSecondPlayer = true;
        // The second player's body (PF_Crew_02_Blue). Left empty, P2 keeps a copy of P1's body.
        public GameObject secondBody;
        public Color firstColor = new Color(0.85f, 0.2f, 0.2f);
        public Color secondColor = new Color(0.2f, 0.45f, 0.95f);
        // How far apart the two start, and where Shift+F1 puts the other one.
        public float spacing = 1.2f;

        [Header("Input")]
        public bool gamepadForSecond = true;
        // Pads are plugged in and out while the game runs; checked this often, not every frame,
        // because the check allocates.
        public float padCheckSeconds = 2f;

        const string Owner = "PLAYER";

        readonly KeyboardMouseSource keyboard = new KeyboardMouseSource();
        static readonly NullInputSource none = new NullInputSource();
        GamepadSource pad;
        float nextPadCheck;
        readonly Collider[] overlap = new Collider[8];

        // What F1 did, shown on the next Update. SliceDebug toasts the key's own label right
        // after running the command, which would bury "Keyboard drives P2" in the same frame.
        // SliceDebug runs first (-600), so the next Update is still this frame.
        string resultToast;

        public KeyboardMouseSource Keyboard => keyboard;

        // A scene without a spawner (an older map, the tutorial before its bootstrap ran)
        // gets a one-player crew: registered, on the keyboard, no clone.
        public static CrewSpawner CreateDefault()
        {
            var go = new GameObject("_Crew");
            go.SetActive(false);   // so Awake sees the settings below, not the defaults
            var s = go.AddComponent<CrewSpawner>();
            s.spawnSecondPlayer = false;
            go.SetActive(true);
            return s;
        }

        void Awake()
        {
            // A spawner left over from the scene being unloaded (a reload) does not count; a
            // second one in this scene does.
            if (Active != null && Active != this && Active.gameObject.scene == gameObject.scene)
            {
                Debug.LogWarning("[CrewSpawner] a second spawner on " + name + " is ignored; " + Active.name + " owns the crew.");
                enabled = false;
                return;
            }
            Active = this;

            if (firstPlayer == null) firstPlayer = FindFirstPlayer();
            if (firstPlayer == null)
            {
                Debug.LogWarning("[CrewSpawner] no PlayerController in the scene, no crew.");
                return;
            }

            var p1 = firstPlayer.gameObject;
            // The title screen's "Jouer seul" asks for one player (SceneFlow.RequestedPlayers);
            // a scene played straight from the editor reads 2 and keeps its own setting.
            if (SceneFlow.RequestedPlayers < 2) spawnSecondPlayer = false;
            if (!spawnSecondPlayer)
            {
                Configure(CrewSetup.Ensure(p1), 0, firstColor);
                return;
            }

            // Switched off while it is copied, so the copy is born switched off too: nothing on it
            // wakes up until it has its own number, camera settings and body.
            bool wasActive = p1.activeSelf;
            p1.SetActive(false);
            Configure(CrewSetup.Ensure(p1), 0, firstColor);

            var p2 = Instantiate(p1, SpotBeside(p1.transform, p1.GetComponent<CharacterController>()), p1.transform.rotation, p1.transform.parent);
            p2.name = p1.name + "_P2";
            MakeSecond(p2);

            p1.SetActive(wasActive);
            p2.SetActive(true);
        }

        void Configure(CrewMember m, int index, Color color)
        {
            m.index = index;
            m.color = color;
            CrewSetup.PutOnCrewLayer(m.gameObject);
        }

        void MakeSecond(GameObject p2)
        {
            Configure(CrewSetup.Ensure(p2), 1, secondColor);

            // One pair of ears for the whole machine (SplitScreen moves them), and one
            // MainCamera: the first player's.
            var view = p2.GetComponentInChildren<Camera>(true);
            if (view != null)
            {
                view.gameObject.tag = "Untagged";
                var ears = view.GetComponent<AudioListener>();
                if (ears != null) DestroyImmediate(ears);
            }

            SwapBody(p2);
        }

        // P1's body is a red crew prefab with a CrewEquip added in the scene. The copy gets the
        // blue one, with its own CrewEquip, at the same place. Destroyed at once rather than at
        // the end of the frame: when the copy wakes up, its components look for their body and
        // must not find the red one.
        void SwapBody(GameObject p2)
        {
            if (secondBody == null) return;
            var old = p2.GetComponentInChildren<Animator>(true);
            if (old == null) return;

            Transform t = old.transform;
            var body = Instantiate(secondBody, t.parent);
            body.name = t.name;
            body.transform.SetLocalPositionAndRotation(t.localPosition, t.localRotation);
            body.transform.localScale = t.localScale;
            if (old.GetComponent<CrewEquip>() != null && body.GetComponent<CrewEquip>() == null)
                body.AddComponent<CrewEquip>();
            DestroyImmediate(t.gameObject);
        }

        static PlayerController FindFirstPlayer()
        {
            var all = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
            PlayerController first = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].TryGetComponent(out CrewMember m) && m.index == 0) return all[i];
                if (first == null) first = all[i];
            }
            return first;
        }

        void Start()
        {
            if (gamepadForSecond) pad = MakePad(GamepadSource.FirstConnected());
            nextPadCheck = Time.unscaledTime + padCheckSeconds;
            AssignDefaultSources();
        }

        // P1 on the keyboard, P2 on the pad (or nothing), anyone else on nothing. What the game
        // starts with; also how a test hands the players back after driving them by script.
        public void AssignDefaultSources()
        {
            var roster = CrewRoster.All;
            for (int i = 0; i < roster.Count; i++)
            {
                var m = roster[i];
                if (m == null || m.Input == null) continue;
                if (i == 0) m.Input.SetSource(keyboard);
                else if (i == 1) m.Input.SetSource(PadOrNone());
                else m.Input.SetSource(none);
            }
        }

        void OnEnable()
        {
            DebugCommands.Register(KeyCode.F1, false, "keyboard to the other player", SwapKeyboard, Owner);
            DebugCommands.Register(KeyCode.F1, true, "bring the other player here", BringOther, Owner);
        }

        void OnDisable()
        {
            // Only while the keys are still ours: during a reload the new scene's spawner may
            // already have taken them, and unregistering by owner would remove its commands.
            if (CrewSetup.OwnsDebugKey(KeyCode.F1, this)) DebugCommands.Unregister(Owner);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            if (resultToast != null)
            {
                DebugCommands.Toast(resultToast);
                resultToast = null;
            }

            if (!gamepadForSecond || Time.unscaledTime < nextPadCheck) return;
            nextPadCheck = Time.unscaledTime + padCheckSeconds;

            int now = GamepadSource.FirstConnected();
            int had = pad != null ? pad.joystick : 0;
            if (now == had) return;

            // Plugged in or pulled out: whoever is not on the keyboard gets the change.
            var old = pad;
            pad = MakePad(now);
            var roster = CrewRoster.All;
            for (int i = 0; i < roster.Count; i++)
            {
                var input = roster[i] != null ? roster[i].Input : null;
                if (input == null || ReferenceEquals(input.Source, keyboard)) continue;
                if (ReferenceEquals(input.Source, old) || input.Source is NullInputSource)
                {
                    input.SetSource(PadOrNone());
                    break;
                }
            }
            DebugCommands.Toast(pad != null ? pad.Label + " connected" : "Gamepad disconnected");
        }

        static GamepadSource MakePad(int joystick) => joystick > 0 ? new GamepadSource(joystick) : null;

        ICrewInputSource PadOrNone() => pad != null ? pad : (ICrewInputSource)none;

        // Index in the roster of the player on the keyboard, or -1.
        int KeyboardIndex()
        {
            var roster = CrewRoster.All;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].Input != null && ReferenceEquals(roster[i].Input.Source, keyboard))
                    return i;
            return -1;
        }

        // F1. The keyboard goes to the next player; the pad, if there is one, to the player the
        // keyboard just left, so a lone tester with a pad in the other hand still has both.
        // SetSource clears every held button on both sides, so a swap in the middle of a hold
        // never fires a release (which would throw an armed grenade).
        public void SwapKeyboard()
        {
            var roster = CrewRoster.All;
            if (roster.Count < 2) { resultToast = "F1: only one player"; return; }
            int from = Mathf.Max(0, KeyboardIndex());
            int to = (from + 1) % roster.Count;
            var a = roster[from];
            var b = roster[to];
            if (a == null || b == null || a.Input == null || b.Input == null) return;

            b.Input.SetSource(keyboard);
            a.Input.SetSource(PadOrNone());
            resultToast = "Keyboard drives " + b.DisplayName;
        }

        // Shift+F1. The player nobody is watching gets lost behind a wardrobe; this puts them
        // beside the one you drive, empty handed.
        public void BringOther()
        {
            var roster = CrewRoster.All;
            if (roster.Count < 2) return;
            int from = Mathf.Max(0, KeyboardIndex());
            var here = roster[from];
            var other = roster[(from + 1) % roster.Count];
            if (here == null || other == null || other.Controller == null) return;

            if (other.Grab != null) other.Grab.Release(false);
            var cc = other.GetComponent<CharacterController>();
            other.Controller.Teleport(SpotBeside(here.transform, cc), here.transform.eulerAngles.y);
            resultToast = other.DisplayName + " brought next to " + here.DisplayName;
        }

        // A free spot beside a player, right, left, behind or ahead, for a capsule of this size.
        // Falls back to the right: the physics will push the two apart.
        Vector3 SpotBeside(Transform t, CharacterController cc)
        {
            Vector3 right = t.right, fwd = t.forward;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = i == 0 ? right : i == 1 ? -right : i == 2 ? -fwd : fwd;
                Vector3 p = t.position + dir * spacing;
                if (IsFree(p, cc)) return p;
            }
            return t.position + right * spacing;
        }

        bool IsFree(Vector3 p, CharacterController cc)
        {
            if (cc == null) return true;
            Vector3 c = p + cc.center;
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            // Lifted a little off the floor it is standing on, which is not in the way.
            Vector3 bottom = c - Vector3.up * Mathf.Max(0f, half - 0.12f);
            Vector3 top = c + Vector3.up * half;
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, cc.radius, overlap,
                                                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (overlap[i] != null && !(overlap[i] is CharacterController)) return false;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = null;
        }
    }
}
