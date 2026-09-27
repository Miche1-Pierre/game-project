using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Movers
{
    // Makes the outside windows open, lets every hinged door answer from its frame, and locks
    // the house until the grandmother hands the keys over.
    //
    // Windows. A kit window module carries two real sashes as children, "Sash_L" and "Sash_R":
    // wooden leaves with their glass, exported with their pivot on the hinge line. By the time
    // this Start runs, HouseDestruction has split each sash's glass into GlassPane children (in
    // its Awake), so a pane still breaks on its own and the sash keeps swinging without it.
    // Each sash is hinged on its own side (Left on the module's -x edge, Right on +x) about its
    // pivot, opening outwards, and the module gets a HingedGroup, so one press opens the whole
    // window and one press closes it, whether you look at the glass or at the frame.
    //
    // A module without sashes (an old export, a window added by hand) falls back to what the
    // house did before sashes existed: its GlassPanes are gathered into two runtime leaves,
    // "Casement_L" and "Casement_R". Glass only, so the bars stay in the wall: it works, it does
    // not look right. The log says which window went which way.
    //
    // Doors, the garage door and the veranda door are set up by hand with HingedPanel. Each wall
    // that holds one gets a HingedGroup too, so an open door can be closed by looking at the
    // doorway rather than hunting for the leaf sticking out of it. The exterior ones also get a
    // DoorLock on the house key (ADR-009): locked through the intro, open once the keys are
    // handed over. F8 gives or takes the house keys, for testing.
    //
    // Placed once in the scene. Order 50 keeps it after every default-order Awake and Start.
    [DefaultExecutionOrder(50)]
    public class HouseInteractionSetup : MonoBehaviour
    {
        [Header("Where")]
        public Transform houseRoot;                           // found by name if left empty
        public string houseRootName = "GrandmaHouse_PierreKit";

        [Header("Windows")]
        public string windowPrefix = "EXT_WINDOW";            // outside windows only, see the plan names
        public string promptOpen = "Open the window";
        public string promptClose = "Close the window";

        [Header("Window sashes")]
        // Clash-free up to 95 degrees with the sash geometry (A3, measured in Blender).
        public float sashOpenAngle = 85f;
        // A sash tip at 240 deg/s moves at 1.8 to 2.0 m/s: under glass speed, nothing to brake.
        public float sashSpeed = 240f;
        public float sashSettleAngle = 15f;

        [Header("Fallback casements (modules without sashes)")]
        public float windowOpenAngle = 80f;
        public float windowSpeed = 160f;

        [Header("Door frames")]
        // Walls whose hand-placed hinged leaves also answer from the frame around them.
        public bool doorFrames = true;
        public string[] doorPrefixes = { "EXT_DOOR", "INT_DOOR", "GARAGE_DOOR", "VERANDA_DOOR" };

        [Header("Locks")]
        // Every hinged leaf in a wall with one of these names gets a DoorLock (unless it has one).
        public bool lockExteriorDoors = true;
        public string[] lockPrefixes = { "EXT_DOOR", "VERANDA_DOOR", "GARAGE_DOOR" };
        public string houseKeyId = DoorLock.HouseKey;

        public const string SashLeft = "Sash_L";
        public const string SashRight = "Sash_R";
        const string LeftLeaf = "Casement_L";
        const string RightLeaf = "Casement_R";
        const string FoundationGroup = "Foundation";
        // Panes whose centres are this close to the middle of the window go to the right leaf.
        const float MiddleTolerance = 0.01f;
        const string DebugOwner = "INTERACTION";

        readonly List<GlassPane> panes = new List<GlassPane>();
        readonly List<GlassPane> leftPanes = new List<GlassPane>();
        readonly List<GlassPane> rightPanes = new List<GlassPane>();

        // The instance whose F8 is registered. On a scene reload the new scene's setup can be
        // enabled before the old one is disabled; the old one must not take the key away then.
        static HouseInteractionSetup keyOwner;

        void OnEnable()
        {
            keyOwner = this;
            DebugCommands.Register(KeyCode.F8, false, "give or take the house keys", ToggleHouseKeys, DebugOwner);
        }

        void OnDisable()
        {
            if (keyOwner != this) return;
            keyOwner = null;
            DebugCommands.Unregister(DebugOwner);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { keyOwner = null; }

        void Start()
        {
            if (houseRoot == null)
            {
                var go = GameObject.Find(houseRootName);
                if (go != null) houseRoot = go.transform;
            }
            if (houseRoot == null)
            {
                Debug.LogWarning("[HouseInteractionSetup] no house root named " + houseRootName + ", no window will open.");
                return;
            }

            // Which way each window went, for the log: the setup is invisible otherwise.
            var sashNames = new StringBuilder();
            var casementNames = new StringBuilder();
            var closedNames = new StringBuilder();
            int modules = 0, batched = 0, sashWindows = 0, casementWindows = 0, leaves = 0;

            // HouseDestruction (Awake) moves each ground-floor wall's plinth under this group and
            // names it "<wall>_Plinth": foundation, not a window, whatever its name starts with.
            Transform foundation = houseRoot.Find(FoundationGroup);

            Transform[] all = houseRoot.GetComponentsInChildren<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform module = all[i];
                if (module == null || !module.name.StartsWith(windowPrefix, System.StringComparison.Ordinal)) continue;
                if (foundation != null && module.IsChildOf(foundation)) continue;
                modules++;
                if (module.TryGetComponent(out MeshRenderer mr) && mr.isPartOfStaticBatch) batched++;
                if (module.TryGetComponent(out Interactable _)) continue;   // done already, or set up by hand

                Transform sashL = FindSash(module, SashLeft);
                Transform sashR = FindSash(module, SashRight);
                int made;
                if (sashL != null || sashR != null)
                {
                    made = WireSashes(module, sashL, sashR);
                    if (made > 0) { sashWindows++; Append(sashNames, module.name); }
                }
                else
                {
                    made = MakeWindow(module);
                    if (made > 0) { casementWindows++; Append(casementNames, module.name); }
                }
                if (made == 0) Append(closedNames, module.name);
                leaves += made;
            }

            int frames = doorFrames ? WireDoorFrames() : 0;
            int locks = lockExteriorDoors ? WireLocks() : 0;

            Debug.Log("[HouseInteractionSetup] " + (sashWindows + casementWindows) + " of " + modules + " windows open (" +
                      leaves + " leaves), " + frames + " door frames, " + locks + " door locks on key '" + houseKeyId +
                      "', under " + houseRoot.name + ".\n  sashes (" + sashWindows + "): " + sashNames +
                      "\n  runtime casements, no sashes in the module (" + casementWindows + "): " + casementNames +
                      "\n  not opening (" + (modules - sashWindows - casementWindows) + "): " + closedNames);

            // A house full of windows that do not open always has one of two causes. Say which.
            if (modules > 0 && sashWindows + casementWindows == 0)
            {
                if (batched > 0)
                    Debug.LogWarning("[HouseInteractionSetup] " + batched + " of " + modules + " window modules are " +
                                     "statically batched, so HouseDestruction could not split their glass and no " +
                                     "window opens. Turn off Static Batching (Project Settings > Player > Other " +
                                     "Settings) or untick Static on the kit prefabs.");
                else
                    Debug.LogWarning("[HouseInteractionSetup] no sashes and no GlassPane under any of the " + modules +
                                     " window modules, so no window opens. Are the Sash_L / Sash_R children in the " +
                                     "window prefabs, is HouseDestruction in the scene with Split Glass on, and are " +
                                     "the window meshes Read/Write enabled?");
            }
        }

        static void Append(StringBuilder sb, string item)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(item);
        }

        // ---- windows with sashes ----

        // A sash right under the module, or one hinged by hand in the prefab: that one carries
        // its own HingedPanel, which seated itself in its Awake and now hangs the sash under
        // "<name>_Hinge". Direct children only either way, so a module's neighbours never count.
        static Transform FindSash(Transform module, string sashName)
        {
            Transform sash = module.Find(sashName);
            return sash != null ? sash : module.Find(sashName + "_Hinge/" + sashName);
        }

        // Hinges each sash about its own pivot and groups them. A sash that already carries a
        // HingedPanel (set up by hand in the prefab) keeps its settings and is only grouped.
        // Returns how many leaves swing: 0, 1 or 2.
        int WireSashes(Transform module, Transform left, Transform right)
        {
            if (HasOwnGlass(module))
                Debug.LogWarning("[HouseInteractionSetup] " + module.name + " has sashes and still carries glass of its " +
                                 "own: its module FBX predates the sashes (re-export the kit). That glass stays fixed.");

            HingedPanel l = MakeSash(module, left, HingedPanel.Hinge.Left);
            HingedPanel r = MakeSash(module, right, HingedPanel.Hinge.Right);
            return Group(module, l, r);
        }

        HingedPanel MakeSash(Transform module, Transform sash, HingedPanel.Hinge hinge)
        {
            if (sash == null) return null;
            if (!sash.TryGetComponent(out HingedPanel panel))
            {
                // AddComponent on an active object seats the hinge at once with the defaults;
                // Configure then moves it to the pivot. Two measurements of one small leaf, at
                // load, instead of switching the sash (and the panes and anything else on it)
                // off and on.
                panel = sash.gameObject.AddComponent<HingedPanel>();
                panel.hingeAtPivot = true;
                panel.isWindow = true;
                panel.speed = sashSpeed;
                panel.settleAngle = sashSettleAngle;
                // The module is the hint: its forward is the outside of the house, by kit convention.
                panel.Configure(hinge, sashOpenAngle, module, promptOpen, promptClose);
            }
            else panel.isWindow = true;
            return panel.Pivot != null ? panel : null;
        }

        // Glass split off the module body itself sits right under the module; a sash's panes
        // sit under the sash.
        static bool HasOwnGlass(Transform module)
        {
            for (int i = 0; i < module.childCount; i++)
                if (module.GetChild(i).TryGetComponent(out GlassPane _)) return true;
            return false;
        }

        int Group(Transform module, HingedPanel left, HingedPanel right)
        {
            if (left == null && right == null) return 0;
            var group = module.gameObject.AddComponent<HingedGroup>();
            group.promptOpen = promptOpen;
            group.promptClose = promptClose;
            if (left != null) group.panels.Add(left);
            if (right != null) group.panels.Add(right);
            group.Rebuild();
            return group.panels.Count;
        }

        // ---- fallback: runtime casements from the module's own panes ----

        // Gathers the module's whole panes into a left and a right leaf. Returns how many leaves
        // were made: 0 (no glass left to hinge), 1 or 2.
        int MakeWindow(Transform module)
        {
            panes.Clear();
            leftPanes.Clear();
            rightPanes.Clear();

            // Direct children only: HouseDestruction puts the panes right under their module.
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            for (int i = 0; i < module.childCount; i++)
            {
                Transform child = module.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (!child.TryGetComponent(out GlassPane pane) || pane.IsBroken) continue;
                if (child.TryGetComponent(out HingedPanel _)) continue;   // hinged by hand, leave it be
                float x = module.InverseTransformPoint(PaneCentre(pane)).x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                panes.Add(pane);
            }
            if (panes.Count == 0) return 0;

            // The middle of the glass, not of the wall module: a window need not sit in the
            // middle of its wall. One column of glass (or one pane) all goes to the right leaf.
            // Both kit windows are two columns wide (small 2 x 2 panes, big 2 x 3), so their
            // leaves are even; a window with a middle column would hang it on the right leaf.
            float middle = (minX + maxX) * 0.5f;
            for (int i = 0; i < panes.Count; i++)
            {
                float x = module.InverseTransformPoint(PaneCentre(panes[i])).x;
                if (x < middle - MiddleTolerance) leftPanes.Add(panes[i]);
                else rightPanes.Add(panes[i]);
            }

            HingedPanel left = MakeLeaf(module, LeftLeaf, HingedPanel.Hinge.Left, leftPanes);
            HingedPanel right = MakeLeaf(module, RightLeaf, HingedPanel.Hinge.Right, rightPanes);
            panes.Clear();
            leftPanes.Clear();
            rightPanes.Clear();
            return Group(module, left, right);
        }

        // One leaf: an empty object at the module's origin (so the panes keep their exact place),
        // the panes moved under it, then the hinge. The object is built switched off, so
        // HingedPanel's Awake waits until Configure has set the hinge edge, and seats it once.
        HingedPanel MakeLeaf(Transform module, string leafName, HingedPanel.Hinge hinge, List<GlassPane> side)
        {
            if (side.Count == 0) return null;

            var go = new GameObject(leafName);
            go.SetActive(false);
            go.layer = module.gameObject.layer;
            go.transform.SetParent(module, false);
            for (int i = 0; i < side.Count; i++) side[i].transform.SetParent(go.transform, true);

            var panel = go.AddComponent<HingedPanel>();
            panel.isWindow = true;
            panel.speed = windowSpeed;
            // Constant speed, as these leaves always swung: 80 degrees in 0.5 s. The door
            // settle would stretch it to 0.58 s at this speed.
            panel.settleAngle = 0f;
            // The module is the hint: its forward is the outside of the house, by kit convention.
            panel.Configure(hinge, windowOpenAngle, module, promptOpen, promptClose);
            go.SetActive(true);

            return panel.Pivot != null ? panel : null;
        }

        // ---- doors ----

        // Every hand-placed leaf whose hinge stands in a door wall gets that wall as a frame:
        // one HingedGroup per wall, holding all its leaves (a double door is one door).
        int WireDoorFrames()
        {
            int frames = 0;
            HingedPanel[] hinged = houseRoot.GetComponentsInChildren<HingedPanel>();
            for (int i = 0; i < hinged.Length; i++)
            {
                HingedPanel panel = hinged[i];
                if (panel == null || panel.Group != null || panel.Pivot == null) continue;

                // The wall the hinge stands in. A panel that IS the wall (a whole module hinged)
                // has a plain folder above its hinge, and a folder is no frame.
                Transform wall = panel.Pivot.parent;
                if (wall == null || !StartsWithAny(wall.name, doorPrefixes) || !wall.TryGetComponent(out Collider _)) continue;

                if (!wall.TryGetComponent(out HingedGroup group))
                {
                    if (wall.TryGetComponent(out Interactable _)) continue;   // something else answers there
                    group = wall.gameObject.AddComponent<HingedGroup>();
                    group.promptOpen = panel.promptOpen;
                    group.promptClose = panel.promptClose;
                    frames++;
                }
                group.Add(panel);
            }
            return frames;
        }

        // A DoorLock on the house key for every leaf of an exterior door that has none. Whether
        // it starts locked is the lock's own business (DoorLock.Start: the session, lockedAtStart).
        int WireLocks()
        {
            int locks = 0;
            HingedPanel[] hinged = houseRoot.GetComponentsInChildren<HingedPanel>(true);
            for (int i = 0; i < hinged.Length; i++)
            {
                HingedPanel panel = hinged[i];
                if (panel == null || panel.isWindow) continue;
                Transform wall = panel.Pivot != null ? panel.Pivot.parent : panel.transform.parent;
                bool exterior = (wall != null && StartsWithAny(wall.name, lockPrefixes)) || StartsWithAny(panel.name, lockPrefixes);
                if (!exterior) continue;
                if (!panel.TryGetComponent(out DoorLock _))
                    panel.gameObject.AddComponent<DoorLock>().keyId = houseKeyId;
                locks++;
            }
            return locks;
        }

        static bool StartsWithAny(string s, string[] prefixes)
        {
            if (prefixes == null) return false;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!string.IsNullOrEmpty(prefix) && s.StartsWith(prefix, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // ---- F8: the house keys ----

        // Locked anywhere: "give" the keys, as the grandmother would (every house lock opens) and
        // put the key itself in the hands of the player on the keyboard. Nothing locked: "take"
        // them back (the key goes home, the exterior doors lock again, and an open one is locked
        // once it is shut). Never raises KeysHandedOver: that is the grandmother's to say, and
        // it would start the contract. The details go to the console: SliceDebug toasts the
        // command's label right after running it.
        void ToggleHouseKeys()
        {
            CrewMember who = KeyboardPlayer();
            int actor = who != null ? who.index : Actors.World;
            KeyItem key = KeyItem.Find(houseKeyId);

            if (DoorLock.AnyLocked(houseKeyId))
            {
                int opened = DoorLock.UnlockAll(houseKeyId, actor);
                string where = key == null ? "no KeyItem '" + houseKeyId + "' in the scene"
                             : key.GiveTo(who) ? "the key to " + (who != null ? who.DisplayName : "nobody")
                             : "the key stays in the pocket it is in";
                Debug.Log("[HouseInteractionSetup] F8 house keys given: " + opened + " doors unlocked, " + where + ".");
            }
            else
            {
                int locked = DoorLock.LockAll(houseKeyId, actor);
                string where = key == null ? "no KeyItem '" + houseKeyId + "' in the scene"
                             : key.ReturnHome() ? "the key back where it started"
                             : "the key stays in the pocket it is in";
                Debug.Log("[HouseInteractionSetup] F8 house keys taken: " + locked + " doors locked, " + where + ".");
            }
        }

        // The crew member the keyboard drives (F1 moves it), else P1.
        static CrewMember KeyboardPlayer()
        {
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m != null && m.Input != null && (m.Input.Source is KeyboardMouseSource || m.Input.Source is LocalDevicesSource)) return m;
            }
            return CrewRoster.Get(0);
        }

        static Vector3 PaneCentre(GlassPane pane)
        {
            if (pane.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                return pane.transform.TransformPoint(mf.sharedMesh.bounds.center);
            if (pane.TryGetComponent(out Renderer r)) return r.bounds.center;
            return pane.transform.position;
        }
    }
}
