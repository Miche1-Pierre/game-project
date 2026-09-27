using UnityEngine;

namespace Movers
{
    // A crew member of the title screen, walking the road's verge with a cardboard box in his
    // arms: the crew's own body and controller (AC_Crew_Slice), Speed for the walk cycle and the
    // Carry layer up, exactly what the game drives. He walks a stretch of the road and starts it
    // again from the far end, where a clump of trees hides the jump.
    //
    // The body is a child with its own yaw (the crew model faces -Z, so it is turned 180); this
    // object faces the way he walks.
    [DisallowMultipleComponent]
    public sealed class RoadsideWalker : MonoBehaviour
    {
        public RoadPath road;
        public Animator body;
        [Tooltip("Where on the road his stretch starts and ends, metres along it. He walks from start to end.")]
        public float startDistance = 60f;
        public float endDistance = 10f;
        [Tooltip("Metres from the centre line (negative: the left verge).")]
        public float lateral = -3.9f;
        public float walkSpeed = 1.25f;
        [Tooltip("Where he is on his stretch when the scene starts, 0 to 1: the crew walks spread out.")]
        [Range(0f, 1f)] public float phase;
        [Tooltip("Sways the walk a little, so three walkers never march in step.")]
        public float wander = 0.25f;
        [Tooltip("The verge sits a few centimetres under the tarmac.")]
        public float groundOffset = -0.05f;

        [Header("The box in his arms")]
        public bool carryBox = true;
        public Material template;
        [Tooltip("Bottom centre of the box, in this object's space (forward is the way he walks).")]
        public Vector3 boxPosition = new Vector3(0f, 0.92f, 0.36f);
        public Vector3 boxSize = new Vector3(0.56f, 0.42f, 0.44f);

        static readonly int SpeedId = Animator.StringToHash("Speed");
        int carryLayer = -1;
        float s;
        float wanderPhase;

        public float Distance => s;

        void Start()
        {
            s = Mathf.Lerp(startDistance, endDistance, phase);
            wanderPhase = phase * 17f;
            if (carryBox) CardboardBox.Create(transform, boxPosition, boxSize, template, gameObject.layer);
            if (body != null)
            {
                body.applyRootMotion = false;
                carryLayer = body.GetLayerIndex("Carry");
                body.SetFloat(SpeedId, walkSpeed);
                if (carryLayer >= 0) body.SetLayerWeight(carryLayer, 1f);
                // Each walker's step starts at a different point of the cycle.
                body.Update(0.4f + phase * 3f);
            }
            Place(0f);
        }

        void Update()
        {
            if (road == null || !road.Ready) return;
            float dt = Time.deltaTime;
            float dir = Mathf.Sign(endDistance - startDistance);
            s += dir * walkSpeed * dt;
            if ((dir > 0f && s >= endDistance) || (dir < 0f && s <= endDistance)) s = startDistance;
            Place(dt);
            if (body != null)
            {
                body.SetFloat(SpeedId, walkSpeed);
                if (carryLayer >= 0) body.SetLayerWeight(carryLayer, 1f);
            }
        }

        void Place(float dt)
        {
            if (road == null || !road.Ready) return;
            wanderPhase += dt * 0.35f;
            float side = lateral + Mathf.Sin(wanderPhase) * wander;
            float dir = Mathf.Sign(endDistance - startDistance);
            road.Sample(s, side, out Vector3 p, out Vector3 f);
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) return;
            f = f.normalized * dir;
            transform.SetPositionAndRotation(p + Vector3.up * groundOffset, Quaternion.LookRotation(f, Vector3.up));
        }
    }
}
