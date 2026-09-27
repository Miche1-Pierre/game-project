using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The driver's door. "[E] Drive" from outside. Once in, the crew member sits in the cab, his
    // CrewInput drives the truck (TruckVehicle), his camera goes behind the truck (ChaseCamera),
    // and his walking, grabbing, pointing, pockets and equip are switched off: hands on the wheel. "[E]
    // Get out" puts him back on his feet beside the cab, on free ground, facing where the camera
    // was looking.
    //
    // Getting out needs the truck almost stopped: jumping out of a moving truck is a stunt nobody
    // asked for, and the crew member would land inside the bumper.
    //
    // It sits on a small object at the door with its own thin collider, so the action ray finds
    // it there and nowhere else on the truck. The driver is parented to seatPoint, inside the cab.
    // While he drives his PlayerInteract is off, so this component reads the Interact button
    // itself and draws the prompt in his view.
    //
    // Anything else that moves him out of the seat (the debug "bring the other player here"
    // teleports him) is accepted: he gets his body back where he now stands, and the seat is free.
    [DisallowMultipleComponent]
    public sealed class VehicleSeat : Interactable
    {
        public TruckVehicle vehicle;            // found in the parents when empty
        public Transform seatPoint;             // the driver's root goes here; this transform when empty
        public float maxExitSpeed = 1f;         // m/s
        public float exitClearance = 0.3f;      // gap between the truck and the capsule of whoever gets out
        public ChaseCamera chase = new ChaseCamera();

        [Header("HUD")]
        public int fontSize = 18;               // at 1080 lines, scaled per viewport

        public CrewMember Driver => driver;
        public override string Prompt => occupied ? "Get out" : "Drive";
        public override bool CanInteract => !occupied && vehicle != null && vehicle.isActiveAndEnabled;

        // Where the driver's eyes are while he sits in the cab. CrewMember.EyePosition reads his
        // camera, and while he drives that camera is the chase view ten metres behind the truck.
        // A system that needs his real eyes (the grandmother's line of sight) finds the seat with
        // member.GetComponentInParent<VehicleSeat>() while member.IsDriving, and asks here.
        public Vector3 DriverEyePosition
        {
            get
            {
                if (driver == null) return seatPoint != null ? seatPoint.position : transform.position;
                Transform head = driverCam != null ? driverCam.parent : null;
                return head != null ? head.TransformPoint(savedCamPosition) : driver.transform.position + Vector3.up * 0.5f;
            }
        }

        CrewMember driver;
        bool occupied;                          // driver can turn Unity-null under us; this remembers there was one
        bool driverLeft;                        // CrewRoster said he left: deal with it in Update, not inside his OnDisable
        float enteredAt;

        // What was switched off or moved, to give it back exactly.
        CharacterController driverCapsule;
        Behaviour driverEquip;
        Transform savedParent;
        Transform driverCam;
        Vector3 savedCamPosition;
        Quaternion savedCamRotation;
        bool capsuleWasOn, controllerWasOn, grabWasOn, interactWasOn, pocketsWasOn, equipWasOn;
        readonly List<Collider> bodyColliders = new List<Collider>();    // scratch for the search
        readonly List<Collider> switchedOff = new List<Collider>();      // the ones this seat turned off

        Transform truckRoot;
        readonly RaycastHit[] rayHits = new RaycastHit[16];
        readonly Collider[] overlaps = new Collider[16];

        // HUD text, rebuilt only when what it says changes.
        enum Hint { None, GetOut, TooFast, Ramp, Notice }
        Hint shownHint;
        int shownKmh = -1, shownKg = -1;
        bool shownOver;
        ICrewInputSource shownSource;
        string hintLine = "", infoLine = "";
        string notice;
        float noticeUntil;

        void Awake()
        {
            useGUILayout = false;   // only GUI.Label: skip the layout pass and its allocations
            if (vehicle == null) vehicle = GetComponentInParent<TruckVehicle>();
            if (seatPoint == null) seatPoint = transform;
            truckRoot = vehicle != null ? vehicle.transform : transform.root;
        }

        void OnEnable() { CrewRoster.Left += OnCrewLeft; }

        void OnDisable()
        {
            CrewRoster.Left -= OnCrewLeft;
            // Most likely the scene is unloading, so nothing here moves or re-enables anything:
            // the flags are cleared and that is all.
            if (occupied) Forget();
        }

        void OnCrewLeft(CrewMember m)
        {
            if (occupied && m == driver) driverLeft = true;
        }

        public override void Interact(PlayerInteract by)
        {
            if (by == null) return;
            var member = by.GetComponent<CrewMember>();
            if (member == null) member = CrewRoster.Owner(by.transform);
            TryEnter(member);
        }

        public bool TryEnter(CrewMember m)
        {
            if (m == null || !CanInteract || m.IsDriving || seatPoint == null) return false;

            // Hands on the wheel: whatever he carried drops at the door.
            if (m.Grab != null && m.Grab.IsCarrying) m.Grab.Release(false);

            driver = m;
            occupied = true;
            driverLeft = false;
            enteredAt = Time.time;

            driverCapsule = m.GetComponent<CharacterController>();
            savedParent = m.transform.parent;
            driverCam = m.View != null ? m.View.transform : null;
            if (driverCam != null)
            {
                savedCamPosition = driverCam.localPosition;
                savedCamRotation = driverCam.localRotation;
            }

            // The capsule goes off before the parenting: a collider still on when the player
            // becomes a child of the truck would join the truck's rigid body. So does any other
            // collider on the body (a worn piece, a hitbox), and they all come back at the exit.
            capsuleWasOn = driverCapsule != null && driverCapsule.enabled;
            if (driverCapsule != null) driverCapsule.enabled = false;
            switchedOff.Clear();
            m.GetComponentsInChildren(false, bodyColliders);
            for (int i = 0; i < bodyColliders.Count; i++)
            {
                var c = bodyColliders[i];
                if (c == null || !c.enabled || c == driverCapsule) continue;
                c.enabled = false;
                switchedOff.Add(c);
            }
            bodyColliders.Clear();
            controllerWasOn = SwitchOff(m.Controller);
            grabWasOn = SwitchOff(m.Grab);
            interactWasOn = SwitchOff(m.Interact);
            // A pocket taken out at the wheel would appear in front of the chase camera, ten
            // metres behind the truck.
            pocketsWasOn = SwitchOff(m.Pockets);
            // The alt button with empty hands takes off the last thing worn and drops it at his
            // feet, which are inside the cab now: the item would come back to life inside the
            // truck's own colliders and be fired out at full depenetration speed, shoving the
            // truck. CrewMember does not expose it, so it is looked up here, once.
            driverEquip = m.GetComponent<PlayerEquip>();
            equipWasOn = SwitchOff(driverEquip);

            m.transform.SetParent(seatPoint, false);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.IsDriving = true;

            vehicle.SetDriver(m);
            chase.Begin(truckRoot);
            return true;
        }

        public bool TryExit()
        {
            if (!occupied || driver == null) return false;
            if (vehicle != null && vehicle.Body != null && vehicle.Body.linearVelocity.magnitude > maxExitSpeed) return false;
            if (!FindExitSpot(out Vector3 feet))
            {
                Notice("No room to get out here");
                return false;
            }
            Release(feet);
            return true;
        }

        void Update()
        {
            if (!occupied) return;
            if (driver == null)
            {
                Forget();
                return;
            }
            if (driverLeft)
            {
                // Still alive, only out of the roster: put him down somewhere sensible anyway.
                Release(FindExitSpot(out Vector3 feet) ? feet : DoorSideGround());
                return;
            }
            Transform body = driver.transform;
            if (body.parent != seatPoint || body.localPosition.sqrMagnitude > 0.01f)
            {
                // Someone else moved him (PlayerController.Teleport keeps his capsule off and his
                // parent as they were). Left like that, his body would ride along with the truck
                // wherever it was put, still "driving". Give it back to him where it now is.
                GiveBack(body.position, body.eulerAngles.y);
                return;
            }
            // The press that opened the door may still read as a press this frame.
            if (Time.time - enteredAt < 0.3f) return;
            var input = driver.Input;
            if (input != null && input.TryConsume(CrewButton.Interact)) TryExit();
        }

        void LateUpdate()
        {
            if (!occupied || driver == null || driverCam == null || vehicle == null || vehicle.Body == null) return;
            Vector2 look = driver.Input != null ? GameSettings.ApplyLook(driver.Input.LookDelta) : Vector2.zero;   // the player's sensitivity and invert Y
            chase.Tick(driverCam, truckRoot, vehicle.Body.linearVelocity, look, Time.deltaTime);
        }

        // Out through the door: standing on these feet, facing where the camera looked.
        void Release(Vector3 feet)
        {
            float yaw = driverCam != null ? chase.Yaw : driver.transform.eulerAngles.y;
            float lift = driverCapsule != null
                ? driverCapsule.height * 0.5f - driverCapsule.center.y + driverCapsule.skinWidth + 0.02f
                : 1f;
            GiveBack(feet + Vector3.up * lift, yaw);
        }

        // Back on his feet: out of the truck's hierarchy, capsule and controls on, head camera back.
        void GiveBack(Vector3 rootPosition, float yaw)
        {
            var m = driver;
            ClearSeat();

            // A parent destroyed meanwhile reads as null here, and the driver goes to the scene root.
            m.transform.SetParent(savedParent != null ? savedParent : null, true);
            // Upright whatever the truck was doing: he stands, the truck may be on its side.
            m.transform.SetPositionAndRotation(rootPosition, Quaternion.Euler(0f, yaw, 0f));
            if (driverCam != null)
            {
                driverCam.localPosition = savedCamPosition;
                driverCam.localRotation = savedCamRotation;
            }
            // The colliders last, once he stands where they should be.
            if (driverCapsule != null && capsuleWasOn) driverCapsule.enabled = true;
            for (int i = 0; i < switchedOff.Count; i++)
                if (switchedOff[i] != null) switchedOff[i].enabled = true;
            switchedOff.Clear();
            Restore(m.Controller, controllerWasOn);
            Restore(m.Grab, grabWasOn);
            Restore(m.Interact, interactWasOn);
            Restore(m.Pockets, pocketsWasOn);
            Restore(driverEquip, equipWasOn);
            driverEquip = null;
            m.IsDriving = false;
        }

        // The driver is gone (destroyed, or the scene is unloading): nothing to give back, and
        // nothing is switched back on, colliders included.
        void Forget()
        {
            if (driver != null) driver.IsDriving = false;
            switchedOff.Clear();
            driverEquip = null;
            ClearSeat();
        }

        void ClearSeat()
        {
            occupied = false;
            driverLeft = false;
            driver = null;
            if (vehicle != null) vehicle.SetDriver(null);
        }

        static bool SwitchOff(Behaviour b)
        {
            if (b == null) return false;
            bool was = b.enabled;
            b.enabled = false;
            return was;
        }

        static void Restore(Behaviour b, bool wasOn)
        {
            if (b != null && wasOn) b.enabled = true;
        }

        // ---- Where to get out --------------------------------------------------------------

        // The truck's own solid shape, in its space (TruckVehicle measures it once).
        Bounds TruckHull => vehicle != null ? vehicle.Hull : new Bounds(Vector3.zero, Vector3.zero);

        // Door side first, then further back along the box, the other side, the front, the back.
        // A truck on its side (a grenade) or boxed in gets a ring of spots all round it instead:
        // nobody stays locked in a cab.
        bool FindExitSpot(out Vector3 feet)
        {
            float r = driverCapsule != null ? driverCapsule.radius : 0.35f;
            float gap = r + exitClearance;
            Bounds truckLocalBounds = TruckHull;

            if (Vector3.Dot(truckRoot.up, Vector3.up) > 0.7f)
            {
                Vector3 door = truckRoot.InverseTransformPoint(transform.position);
                bool doorOnLeft = door.x < truckLocalBounds.center.x;
                float nearX = doorOnLeft ? truckLocalBounds.min.x - gap : truckLocalBounds.max.x + gap;
                float farX = doorOnLeft ? truckLocalBounds.max.x + gap : truckLocalBounds.min.x - gap;
                float midX = truckLocalBounds.center.x;

                for (int i = 0; i < 6; i++)
                {
                    Vector2 xz;
                    switch (i)
                    {
                        case 0: xz = new Vector2(nearX, door.z); break;
                        case 1: xz = new Vector2(nearX, door.z - 1.5f); break;
                        case 2: xz = new Vector2(farX, door.z); break;
                        case 3: xz = new Vector2(farX, door.z - 1.5f); break;
                        case 4: xz = new Vector2(midX, truckLocalBounds.max.z + gap); break;
                        default: xz = new Vector2(midX, truckLocalBounds.min.z - gap); break;
                    }
                    Vector3 probe = truckRoot.TransformPoint(new Vector3(xz.x, door.y, xz.y));
                    if (TryStandAt(probe, r, out feet)) return true;
                }
            }

            // Eight spots on a circle that clears the truck whichever way it lies, probed from the
            // height of its middle.
            Vector3 centre = truckRoot.TransformPoint(truckLocalBounds.center);
            float ring = truckLocalBounds.extents.magnitude + gap;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Vector3 probe = new Vector3(centre.x + Mathf.Sin(a) * ring, centre.y, centre.z + Mathf.Cos(a) * ring);
                if (TryStandAt(probe, r, out feet)) return true;
            }
            feet = default;
            return false;
        }

        // The ground under a probe point, if a standing capsule fits there. The probe starts at
        // door height, below any porch roof or tree the truck is parked under.
        bool TryStandAt(Vector3 probe, float radius, out Vector3 feet)
        {
            feet = default;
            int n = Physics.RaycastNonAlloc(probe + Vector3.up * 0.5f, Vector3.down, rayHits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            int best = -1;
            for (int i = 0; i < n; i++)
            {
                var c = rayHits[i].collider;
                if (c == null || c.transform.IsChildOf(truckRoot)) continue;   // the truck, and the driver in it
                if (rayHits[i].distance < nearest) { nearest = rayHits[i].distance; best = i; }
            }
            if (best < 0 || rayHits[best].normal.y < 0.6f) return false;
            Vector3 ground = rayHits[best].point;

            float height = driverCapsule != null ? driverCapsule.height : 1.8f;
            Vector3 bottom = ground + Vector3.up * (radius + 0.05f);
            Vector3 top = ground + Vector3.up * Mathf.Max(radius + 0.05f, height - radius + 0.05f);
            int k = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.95f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < k; i++)
            {
                var c = overlaps[i];
                if (c == null) continue;
                if (driver != null && c.transform.IsChildOf(driver.transform)) continue;
                return false;
            }
            feet = ground;
            return true;
        }

        // Last resort when he must come out and nowhere fits: the ground by the door.
        Vector3 DoorSideGround()
        {
            Bounds truckLocalBounds = TruckHull;
            Vector3 door = truckRoot.InverseTransformPoint(transform.position);
            float x = door.x < truckLocalBounds.center.x ? truckLocalBounds.min.x - 0.7f : truckLocalBounds.max.x + 0.7f;
            return truckRoot.TransformPoint(new Vector3(x, 0f, door.z));
        }

        // ---- The driver's HUD (bottom centre of his view; his PlayerInteract prompt is off) ----

        // The notice while it shows ("No room to get out here"), else null. Read by the
        // LumaFlow HUD (UICORE), which draws the driver's hints instead of OnGUI.
        public string ActiveNotice => Time.time < noticeUntil ? notice : null;

        void Notice(string text)
        {
            notice = text;
            noticeUntil = Time.time + 2f;
            shownHint = Hint.None;   // force a rebuild
        }

        void OnGUI()
        {
            if (!HudMode.UseLegacy) return;   // the LumaFlow HUD (HudRoot) draws this now
            if (!occupied || driver == null || vehicle == null) return;
            if (Event.current.type != EventType.Repaint) return;
            RefreshText();

            Rect view = ViewportGUI.RectFor(driver);
            int size = ViewportGUI.FontSize(view, fontSize);
            float lineH = size + 8f;
            Rect box = ViewportGUI.Region(view, HudRegion.BottomCenter, Mathf.Min(view.width - 24f, 560f), lineH * 2f);
            ViewportGUI.Label(new Rect(box.x, box.y, box.width, lineH), hintLine, size, Color.white, TextAnchor.MiddleCenter, true);
            ViewportGUI.Label(new Rect(box.x, box.y + lineH, box.width, lineH), infoLine, size,
                              shownOver ? new Color(1f, 0.45f, 0.35f) : new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleCenter);
        }

        void RefreshText()
        {
            Hint hint;
            if (Time.time < noticeUntil) hint = Hint.Notice;
            else if (!vehicle.CanDrive) hint = Hint.Ramp;
            else if (vehicle.Body != null && vehicle.Body.linearVelocity.magnitude > maxExitSpeed) hint = Hint.TooFast;
            else hint = Hint.GetOut;

            var source = driver.Input != null ? driver.Input.Source : null;
            if (hint != shownHint || (hint == Hint.GetOut && source != shownSource))
            {
                shownHint = hint;
                shownSource = source;
                switch (hint)
                {
                    case Hint.Notice: hintLine = notice; break;
                    case Hint.Ramp: hintLine = "Ramp going up..."; break;
                    case Hint.TooFast: hintLine = "Stop to get out"; break;
                    default: hintLine = "[" + InteractLabel(source) + "] Get out"; break;
                }
            }

            int kmh = Mathf.RoundToInt(vehicle.SpeedKmh);
            var cargo = vehicle.cargo;
            int kg = cargo != null ? Mathf.RoundToInt(cargo.LoadedKg) : -1;
            bool over = cargo != null && cargo.OverCapacity;
            if (kmh != shownKmh || kg != shownKg || over != shownOver)
            {
                shownKmh = kmh;
                shownKg = kg;
                shownOver = over;
                infoLine = cargo != null
                    ? kmh + " km/h     cargo " + kg + " / " + Mathf.RoundToInt(cargo.capacityKg) + " kg" + (over ? "  OVERLOADED" : "")
                    : kmh + " km/h";
            }
        }

        // The keyboard shows its key. A gamepad shows X, the Interact button of the proposed pad
        // layout (A5b section 3.4); PLAYER owns the real mapping.
        static string InteractLabel(ICrewInputSource source)
        {
            if (source is KeyboardMouseSource keys) return keys.interactKey.ToString();
            return "X";
        }
    }
}
