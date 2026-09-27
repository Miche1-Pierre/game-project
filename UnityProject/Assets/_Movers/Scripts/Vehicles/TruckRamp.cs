using UnityEngine;

namespace Movers
{
    // The loading ramp. Down while the truck is parked, so the crew can walk crates up into the
    // bed; slid in under the bed as soon as someone takes the wheel (TruckVehicle will not move
    // until it is in), and back down once the truck stands empty and still.
    //
    // It is its own kinematic body, not a piece of the truck's. Down, its far end rests on the
    // road: as part of the truck that contact would fight the suspension and lift the rear
    // wheels. Its contacts with the truck itself are ignored for the same reason (it overlaps
    // the bed edge by a few centimetres). While it moves its collider is off: a sliding plank
    // shoves nobody, and whoever stood on it simply drops to the road. With nothing to collide,
    // the motion is an animation, so it runs in Update and stays smooth at any frame rate.
    //
    // Poses are worked out from the authored one, in the truck's space: swing up level about the
    // top edge (the hinge at the bed), drop under the bed floor, slide in.
    //
    // It only comes down onto clear ground. Something loose where it would land (a crate, the
    // grandmother's car) would be kicked away the moment the collider came back on, so it waits,
    // stowed or hanging with its collider off, until that is moved.
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class TruckRamp : MonoBehaviour
    {
        public float duration = 1.2f;
        public float stowDrop = 0.14f;      // how far under the hinge it slides in: below the bed floor
        public float clearCheckInterval = 0.5f;

        enum State { Deployed, Stowing, Stowed, Deploying }
        State state = State.Deployed;
        float t;                            // 0 = down, 1 = stowed

        Vector3 hinge, hingeLocal, droppedPos, stowedPos, downPos, boxCentre, boxSize;
        Quaternion downRot, flatRot;
        Collider[] own;
        Collider[] truckColliders;
        Rigidbody truckBody;
        readonly Collider[] blockers = new Collider[16];
        float nextClearCheck;

        public bool IsDeployed => state == State.Deployed;
        public bool IsStowed => state == State.Stowed;
        public bool IsMoving => state == State.Stowing || state == State.Deploying;
        public byte NetState => (byte)state;     // TruckSync Ramp
        bool replicaDeployed;                    // client: the host's ramp is down, finish coming down

        const float SwingEnd = 0.4f;        // first swing up level, then drop, then slide in
        const float DropEnd = 0.55f;

        void Awake()
        {
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            own = GetComponentsInChildren<Collider>(true);
            ComputePoses();
        }

        void Start()
        {
            truckBody = transform.parent != null ? transform.parent.GetComponentInParent<Rigidbody>() : null;
            truckColliders = truckBody != null ? truckBody.GetComponentsInChildren<Collider>(true) : new Collider[0];
            IgnoreTruck();
        }

        public void Stow()
        {
            if (state == State.Stowed || state == State.Stowing) return;
            state = State.Stowing;
            SetColliders(false);
        }

        public void Deploy()
        {
            if (state == State.Deployed || state == State.Deploying) return;
            if (!LandingClear()) return;   // TruckVehicle asks again every step while it stands parked
            state = State.Deploying;
        }

        void Update()
        {
            if (state == State.Stowing)
            {
                t = Mathf.MoveTowards(t, 1f, Time.deltaTime / Mathf.Max(0.05f, duration));
                Apply();
                if (t >= 1f) state = State.Stowed;
            }
            else if (state == State.Deploying)
            {
                t = Mathf.MoveTowards(t, 0f, Time.deltaTime / Mathf.Max(0.05f, duration));
                Apply();
                // Only the host looks at the ground; the client lands when the host did.
                if (t <= 0f && (Net.HasAuthority ? LandingClear() : replicaDeployed))
                {
                    state = State.Deployed;
                    SetColliders(true);
                }
            }
        }

        // Online client: the host's state (TruckSync Ramp). Moves animate as on the host; an end
        // state snaps, unless the matching move is already under way here. snap: the join
        // snapshot, silent.
        public void ApplyReplica(byte netState, bool snap)
        {
            if (Net.HasAuthority) return;
            var s = (State)netState;
            replicaDeployed = false;
            switch (s)
            {
                case State.Stowing:
                    if (state == State.Stowed) return;
                    state = State.Stowing;
                    SetColliders(false);
                    break;
                case State.Deploying:
                    if (state == State.Deployed) return;
                    state = State.Deploying;
                    SetColliders(false);
                    break;
                case State.Stowed:
                    if (state == State.Stowing && !snap) return;   // lands on its own at t = 1
                    t = 1f;
                    Apply();
                    state = State.Stowed;
                    SetColliders(false);
                    break;
                case State.Deployed:
                    if (state == State.Deploying && !snap) { replicaDeployed = true; return; }
                    t = 0f;
                    Apply();
                    state = State.Deployed;
                    SetColliders(true);
                    break;
            }
        }

        void ComputePoses()
        {
            downRot = transform.localRotation;
            downPos = transform.localPosition;

            var box = GetComponent<BoxCollider>();
            boxSize = box != null ? Vector3.Scale(box.size, transform.localScale) : transform.localScale;
            boxCentre = box != null ? Vector3.Scale(box.center, transform.localScale) : Vector3.zero;
            Vector3 centre = boxCentre;
            float half = boxSize.z * 0.5f;

            // The higher end is the one on the bed: that is the hinge.
            Vector3 endPlus = downPos + downRot * (centre + Vector3.forward * half);
            Vector3 endMinus = downPos + downRot * (centre - Vector3.forward * half);
            bool plusHigh = endPlus.y >= endMinus.y;
            hinge = plusHigh ? endPlus : endMinus;
            hingeLocal = centre + (plusHigh ? Vector3.forward : Vector3.back) * half;

            Vector3 outward = (plusHigh ? endMinus : endPlus) - hinge;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.back;

            // Level, same heading, no roll. Its +z keeps pointing at the end it pointed at.
            flatRot = Quaternion.LookRotation(plusHigh ? -outward : outward, Vector3.up);
            Vector3 flatPos = hinge - flatRot * hingeLocal;
            droppedPos = flatPos + Vector3.down * stowDrop;
            stowedPos = droppedPos - outward * (half * 2f + 0.05f);
        }

        void Apply()
        {
            if (t < SwingEnd)
            {
                // A true swing about the hinge, so the top edge stays on the bed.
                Quaternion r = Quaternion.Slerp(downRot, flatRot, Smooth(t / SwingEnd));
                transform.localRotation = r;
                transform.localPosition = hinge - r * hingeLocal;
            }
            else if (t < DropEnd)
            {
                transform.localRotation = flatRot;
                transform.localPosition = Vector3.Lerp(hinge - flatRot * hingeLocal, droppedPos, Smooth((t - SwingEnd) / (DropEnd - SwingEnd)));
            }
            else
            {
                transform.localRotation = flatRot;
                transform.localPosition = Vector3.Lerp(droppedPos, stowedPos, Smooth((t - DropEnd) / (1f - DropEnd)));
            }
        }

        // Is the ground where the ramp lands free of loose bodies? Static ground never counts (the
        // foot of the ramp rests on the road), nor does the truck itself, nor a walking capsule:
        // a crew member standing there is lifted onto the ramp by his own controller. Checked at
        // most every clearCheckInterval.
        bool LandingClear()
        {
            if (Time.time < nextClearCheck) return false;
            nextClearCheck = Time.time + clearCheckInterval;
            Transform parent = transform.parent;
            if (parent == null) return true;

            Vector3 centre = parent.TransformPoint(downPos + downRot * boxCentre);
            Quaternion rotation = parent.rotation * downRot;
            Vector3 half = Vector3.Max(boxSize * 0.5f - Vector3.one * 0.02f, Vector3.one * 0.01f);
            int n = Physics.OverlapBoxNonAlloc(centre, half, blockers, rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = blockers[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                var body = c.attachedRigidbody;
                if (body == null || body.isKinematic || body == truckBody) continue;
                return false;
            }
            return true;
        }

        static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        void SetColliders(bool on)
        {
            for (int i = 0; i < own.Length; i++)
                if (own[i] != null) own[i].enabled = on;
            if (on) IgnoreTruck();
        }

        // Re-applied every time the colliders come back on: switching a collider off and on
        // gives it a new physics shape, which forgets what it was told to ignore.
        void IgnoreTruck()
        {
            if (truckColliders == null) return;
            for (int i = 0; i < own.Length; i++)
            {
                var a = own[i];
                if (a == null || !a.enabled || !a.gameObject.activeInHierarchy) continue;
                for (int j = 0; j < truckColliders.Length; j++)
                {
                    var b = truckColliders[j];
                    if (b == null || b == a || b.isTrigger || b is WheelCollider) continue;
                    if (!b.enabled || !b.gameObject.activeInHierarchy || b.transform.IsChildOf(transform)) continue;
                    Physics.IgnoreCollision(a, b, true);
                }
            }
        }
    }
}
