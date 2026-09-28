using UnityEngine;

namespace Movers
{
    // Puts a marker or a parked car on the road at Play (ADR-013): its position and heading go to
    // `distance` along `road`, `lateral` metres right of the centre line, on the ground. No road
    // set: only the height is snapped. A map is edited by moving a distance, not a transform, and
    // the land's own heights (CountryLand) are only known once it has built.
    //
    // Deterministic on both machines (the same scene, the same land), and done in Start before
    // the network id sweep (order 10000), so a car is registered where it will really stand.
    [DefaultExecutionOrder(9000)]
    [DisallowMultipleComponent]
    public sealed class RoadAnchor : MonoBehaviour
    {
        [Tooltip("The road to stand on. Empty: only the height is snapped, where the marker is.")]
        public RoadPath road;
        [Tooltip("Metres along the road.")]
        public float distance;
        [Tooltip("Metres right of the centre line (negative: left).")]
        public float lateral;
        [Tooltip("Face the road's direction of travel (plus yawOffset). Off: keep the placed heading.")]
        public bool alignYaw = true;
        [Tooltip("Degrees added to the road's heading: 180 faces back along it.")]
        public float yawOffset;
        [Tooltip("Rest the lowest enabled collider on the ground (a car). Off: the pivot goes on the ground.")]
        public bool restOnColliders = true;
        [Tooltip("Metres added above the ground.")]
        public float lift = 0.05f;

        static readonly RaycastHit[] hits = new RaycastHit[16];

        void Start() => Snap();

        public void Snap()
        {
            Vector3 p = transform.position;
            Quaternion rot = transform.rotation;
            if (road != null)
            {
                if (!road.Ready) road.BuildCenterline();
                if (road.Ready)
                {
                    road.Sample(distance, lateral, out Vector3 at, out Vector3 fwd);
                    p.x = at.x;
                    p.z = at.z;
                    fwd.y = 0f;
                    if (alignYaw && fwd.sqrMagnitude > 1e-6f)
                        rot = Quaternion.Euler(0f, Quaternion.LookRotation(fwd).eulerAngles.y + yawOffset, 0f);
                }
            }
            float pivotAboveBottom = restOnColliders ? PivotAboveBottom() : 0f;
            p.y = GroundHeight(p.x, p.z, transform, p.y - pivotAboveBottom) + pivotAboveBottom + lift;

            transform.SetPositionAndRotation(p, rot);
            if (TryGetComponent(out Rigidbody rb))
            {
                rb.position = p;
                rb.rotation = rot;
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            Physics.SyncTransforms();
        }

        // Metres from the pivot down to the bottom of the enabled colliders, measured upright.
        float PivotAboveBottom()
        {
            float bottom = float.MaxValue;
            var cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                bottom = Mathf.Min(bottom, c.bounds.min.y);
            }
            return bottom < float.MaxValue ? Mathf.Max(0f, transform.position.y - bottom) : 0f;
        }

        // The ground's height at a point. Out in the country: the land's own function (CountryLand,
        // roads included), which needs no collider in range. In the yard, or with no land: a ray
        // down on the colliders, ignoring `ignore` and its children. Nothing found: fallback.
        public static float GroundHeight(float x, float z, Transform ignore, float fallback)
        {
            var land = Land;
            if (land != null && !land.InYard(x, z)) return land.HeightAt(x, z);
            const float From = 200f;
            int n = Physics.RaycastNonAlloc(new Vector3(x, From, z), Vector3.down, hits, From * 2f,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || (ignore != null && c.transform.IsChildOf(ignore))) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;   // a car, a prop
                best = Mathf.Max(best, hits[i].point.y);
            }
            if (best > float.MinValue) return best;
            return land != null ? land.HeightAt(x, z) : fallback;
        }

        static CountryLand land;
        static int landFrame = -1;

        // Searched at most once per frame while there is none (a scene with no land).
        static CountryLand Land
        {
            get
            {
                if (land == null && Time.frameCount != landFrame)
                {
                    landFrame = Time.frameCount;
                    land = Object.FindAnyObjectByType<CountryLand>();
                }
                return land;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            land = null;
            landFrame = -1;
        }

        void OnDrawGizmosSelected()
        {
            if (road == null || !road.Ready) return;
            road.Sample(distance, lateral, out Vector3 at, out _);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, new Vector3(at.x, transform.position.y, at.z));
        }
    }
}
