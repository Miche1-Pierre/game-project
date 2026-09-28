using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Movers
{
    // How the grandmother gets about (A8_navigation.md, measured recipe).
    //
    // - Path: a NavMesh built at Play from the physics colliders (NavMeshBaker), searched with
    //   A* by the NavMeshAgent. The agent only plans and steers (updatePosition off).
    // - Body: a CharacterController, like the players. Her feet follow the real floor, the
    //   stair ramps and the step slabs, and she bumps into players instead of walking through
    //   them. Moved in Update like PlayerController moves the crew: a controller Move is a
    //   kinematic sweep, not a simulated body, and moving it in FixedUpdate would stutter.
    // - Doors are not in the NavMesh. When her path crosses a shut door she stops short, opens
    //   it with HingedPanel.SetOpen (she has the keys), waits for it to swing, walks through,
    //   and closes it behind her if it was shut.
    // - The house changes: walls fall, furniture is carried off. After structural events she
    //   re-collects the colliders and updates the NavMesh with the full bounds, then re-plans.
    // - Furniture is not in the NavMesh by default (A8's rule: every rigidbody collider is left
    //   out). She pushes light things aside and detours round heavy ones when she walks into
    //   them. furnitureAsObstacles bakes furniture in instead: an open question (REPORT.md).
    // - Stuck (she wants to walk and does not move): a player in the way gets an "excuse me"
    //   and a moment to step aside; otherwise she marks the spot as blocked for a few seconds
    //   (a carving obstacle) and re-plans around it. After a few tries she gives up the trip.
    // - Seated, her CharacterController is off (the pose is inside the chair). A stand-in
    //   capsule keeps her solid and talkable meanwhile.
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class GrandmaMover : MonoBehaviour
    {
        public enum MoveStatus { Idle, Moving, Arrived, Failed }

        [Header("Body (her capsule, A8)")]
        public float radius = 0.28f;
        public float height = 1.58f;
        public float stepOffset = 0.30f;
        public float slopeLimit = 45f;
        public float gravity = -18f;

        [Header("Speeds, m/s (her walk clip is native at 0.5, A6)")]
        public float walkSpeed = 0.6f;
        public float hurrySpeed = 1.0f;
        public float acceleration = 4f;
        public float turnSpeed = 240f;

        [Header("NavMesh, built at Play")]
        public Vector3 navBoundsCenter = new Vector3(10f, 4f, -5f);
        public Vector3 navBoundsSize = new Vector3(44f, 16f, 50f);
        public int tileSize = 64;
        [Tooltip("Off (default, A8's rule): every rigidbody collider is left out of the NavMesh; she pushes light things aside and detours round heavy ones when she walks into them. On (A8 variant B): furniture at rest goes in as Not Walkable and she plans round it, so a sofa dropped in a doorway blocks her. A design question for Pierre (REPORT.md).")]
        public bool furnitureAsObstacles = false;
        [Tooltip("furnitureAsObstacles only: loose bodies at least this heavy are obstacles she walks around.")]
        public float obstacleMinKg = 5f;
        public Transform[] ignoreInNavMesh = new Transform[0];
        [Tooltip("Seconds after a wall, a door or a structural piece changes. Several in a row: the first one counts.")]
        public float structureRebuildDelay = 0.5f;
        [Tooltip("Seconds after a blast (or, with furnitureAsObstacles, a piece of furniture moving) for things to come to rest. Each new event restarts the wait, up to settleMaxWait.")]
        public float settleRebuildDelay = 4f;
        public float settleMaxWait = 12f;
        [Tooltip("furnitureAsObstacles only: seconds between two checks that the baked furniture is still where it was.")]
        public float furnitureScanInterval = 2f;

        [Header("Doors")]
        public float doorStopDistance = 0.9f;
        public float doorLookAhead = 3f;
        public float doorWaitMax = 2.5f;
        public bool closeDoorsBehind = true;

        [Header("Getting stuck")]
        public float stuckSeconds = 1f;
        public float blockedByPlayerWait = 1.5f;
        public float detourSeconds = 6f;
        public int maxStuckRetries = 3;

        [Header("Pushing light things aside")]
        public float pushMaxKg = 15f;
        public float pushForce = 40f;

        [Header("Seated stand-in (her body while the CharacterController is off)")]
        public float seatedRadius = 0.25f;
        public float seatedHeight = 1.3f;

        [Header("Facing")]
        [Tooltip("Her model faces -Z (A4 2.1). Measured from her shoulders at Awake when autoDetectFacing is on.")]
        public bool modelFacesBackward = true;
        public bool autoDetectFacing = true;

        const float SampleRadius = 1.0f;
        // Seconds after opening a door before she closes it without having gone through, once
        // her way no longer leads through it (she was called off, or re-planned elsewhere).
        const float DoorGiveUpSeconds = 4f;
        // How far past the shut leaf she must be before it swings shut behind her: about a
        // leaf's width, so the closing leaf never sweeps her.
        const float DoorCloseClearance = 0.9f;

        CharacterController body;
        CapsuleCollider seatedBody;
        readonly List<Collider> seatColliders = new List<Collider>();
        NavMeshAgent agent;
        NavMeshBaker baker;
        readonly DoorPassage doors = new DoorPassage();
        NavMeshPath path;
        readonly Vector3[] corners = new Vector3[48];
        int cornerCount;
        Action<WorldEvent> onWorldEvent;

        MoveStatus status = MoveStatus.Idle;
        Vector3 destination;
        float moveSpeed;
        float arriveRadius = 0.3f;
        bool pendingMove;

        Vector3 horizontal;
        float verticalSpeed;
        bool hasFace;
        Vector3 facePoint;

        HingedPanel waitingDoor;
        float doorWaitUntil;
        float nextDoorCheck;
        HingedPanel closeBehind;
        int closeBehindSide;
        float closeBehindSince;
        Vector3 lastDoorPosition;
        float lastDoorTime = -99f;

        float stuckTime;
        int stuckRetries;
        float pauseUntil;
        float repathAt = -1f;
        // Two kinds of NavMesh update. Structure: as soon as possible, the first request wins.
        // Settle: after things have come to rest, so every new blast pushes it back (capped).
        float structureRebuildAt = -1f;
        float settleRebuildAt = -1f;
        float settleSince = -1f;
        float nextFurnitureScan;

        readonly GameObject[] detours = new GameObject[2];
        readonly float[] detourUntil = new float[2];

        bool sliding;
        Vector3 slideFrom, slideTo;
        Quaternion rotFrom, rotTo;
        float slideT, slideDuration;
        bool collideAfterSlide;

        Rigidbody pushBody;
        Vector3 pushDirection;

        // Online client: where the stream put her last frame, for her walking speed.
        Vector3 replicaLast;
        bool replicaHasLast;

        public NavMeshBaker Baker => baker;
        public bool Ready => agent != null;
        public MoveStatus Status => status;
        public Vector3 Destination => destination;
        public bool PathIsPartial { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float LastPathLength { get; private set; }
        public int RepathCount { get; private set; }
        // Re-plans caused by a NavMesh update (the house changed), and the length of the last
        // one: what the new NavMesh offered her, whatever re-plans came after.
        public int RebuildRepathCount { get; private set; }
        public float LastRebuildPathLength { get; private set; }
        public int DoorOpenCount { get; private set; }
        // Every time she stopped in front of a door: to open it, or for one still swinging.
        public int DoorWaitCount { get; private set; }
        public bool IsWaitingForDoor => waitingDoor != null;
        public bool IsSliding => sliding;
        public bool CollisionsOn => body != null && body.enabled;
        public bool SeatedBodyOn => seatedBody != null && seatedBody.enabled;
        public Collider SeatedBody => seatedBody;
        public int DoorCount => doors.Count;
        public int CornerCount => cornerCount;
        public Vector3 Corner(int i) => corners[Mathf.Clamp(i, 0, corners.Length - 1)];

        // A player stands in her way: the brain says something.
        public event Action<CrewMember> BlockedBy;

        // Which way she looks. Her model may face -Z, so never read transform.forward directly.
        public Vector3 Forward
        {
            get
            {
                Vector3 f = modelFacesBackward ? -transform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        public bool ReachedDestination
        {
            get
            {
                if (status != MoveStatus.Arrived) return false;
                Vector3 d = destination - transform.position;
                d.y = 0f;
                return d.magnitude <= arriveRadius + 0.5f;
            }
        }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            body.radius = radius;
            body.height = height;
            body.skinWidth = 0.03f;
            body.center = new Vector3(0f, height * 0.5f + body.skinWidth, 0f);   // feet on the floor
            body.stepOffset = stepOffset;
            body.slopeLimit = slopeLimit;
            body.minMoveDistance = 0f;

            // The placeholder's static capsule would fight her own controller.
            foreach (var capsule in GetComponents<CapsuleCollider>())
            {
                if (!capsule.enabled) continue;
                capsule.enabled = false;
                Debug.LogWarning("[GrandmaMover] a CapsuleCollider on " + name + " was switched off: remove it (INTEGRATION.md step 1).");
            }

            path = new NavMeshPath();
            DetectFacing();

            baker = new NavMeshBaker();
            onWorldEvent = OnWorldEvent;
        }

        // Her front, from her shoulders in the scene pose: the right arm is on her right.
        void DetectFacing()
        {
            if (!autoDetectFacing) return;
            var anim = GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) return;
            Transform l = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform r = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (l == null || r == null) return;
            Vector3 right = r.position - l.position;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-4f) return;
            Vector3 face = Vector3.Cross(right, Vector3.up);
            modelFacesBackward = Vector3.Dot(face, transform.forward) < 0f;
        }

        void OnEnable() { WorldEvents.Subscribe(onWorldEvent); }
        void OnDisable() { WorldEvents.Unsubscribe(onWorldEvent); }

        void Start()
        {
            // Online client: no NavMesh bake and no agent. The host walks her; her root comes
            // in on the transform stream.
            if (!Net.HasAuthority) return;
            baker.bounds = new Bounds(navBoundsCenter, navBoundsSize);
            NavMeshBuildSettings s = NavMesh.GetSettingsByID(0);
            s.agentRadius = radius;
            s.agentHeight = height;
            s.agentClimb = stepOffset;
            s.agentSlope = 40f;
            s.overrideTileSize = true;
            s.tileSize = tileSize;
            baker.settings = s;
            baker.furnitureAsObstacles = furnitureAsObstacles;
            baker.obstacleMinKg = obstacleMinKg;
            baker.self = transform;
            baker.ignoreRoots = ignoreInNavMesh;
            int mask = ~0;
            int debris = LayerMask.NameToLayer("Debris");
            if (debris >= 0) mask &= ~(1 << debris);
            baker.layerMask = mask;
            baker.Built += OnBuilt;

            // HingedPanels seat their hinges in Awake; this runs after HouseInteractionSetup (50).
            doors.Collect();
            if (!baker.BuildNow())
                Debug.LogWarning("[GrandmaMover] the NavMesh build failed (" + baker.SourceCount + " sources): she stays put.");
        }

        void OnDestroy()
        {
            if (baker != null)
            {
                baker.Built -= OnBuilt;
                baker.Dispose();
            }
            for (int i = 0; i < detours.Length; i++)
                if (detours[i] != null) Destroy(detours[i]);
        }

        void OnBuilt()
        {
            if (agent == null) { CreateAgent(); return; }
            if (status == MoveStatus.Moving && Repath(true))
            {
                RebuildRepathCount++;
                LastRebuildPathLength = LastPathLength;
            }
        }

        void CreateAgent()
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[GrandmaMover] no NavMesh within 2 m of " + name + " at " + transform.position + ": she stays put.");
                return;
            }
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.agentTypeID = baker.settings.agentTypeID;
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.speed = walkSpeed;
            agent.acceleration = 8f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;
            agent.autoRepath = false;   // re-planned here, after each NavMesh update
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.Warp(hit.position);
            Debug.Log("[GrandmaMover] NavMesh built in " + baker.FirstBuildMs.ToString("0") + " ms (collect " + baker.CollectMs.ToString("0") +
                      " ms, " + baker.SourceCount + " sources (" + baker.BoxedCount + " unreadable meshes as boxes), " +
                      baker.ObstacleCount + " furniture obstacles, " + doors.Count + " doors).");
            if (pendingMove)
            {
                pendingMove = false;
                if (!ComputePath()) status = MoveStatus.Failed;
            }
        }

        // ---------------------------------------------------------------- orders

        // Walk (or hurry) to a point. Returns false when there is no way at all; a partial way
        // is accepted and she stops at its end (PathIsPartial).
        public bool MoveTo(Vector3 target, float speed, float arriveWithin = 0.3f)
        {
            destination = target;
            moveSpeed = Mathf.Max(0.1f, speed);
            arriveRadius = Mathf.Max(0.1f, arriveWithin);
            status = MoveStatus.Moving;
            stuckRetries = 0;
            stuckTime = 0f;
            waitingDoor = null;
            pauseUntil = 0f;
            hasFace = false;
            if (sliding || !body.enabled) return true;   // leaves once the slide is over
            if (agent == null) { pendingMove = true; return true; }
            if (!ComputePath()) { status = MoveStatus.Failed; return false; }
            return true;
        }

        public void Stop()
        {
            status = MoveStatus.Idle;
            pendingMove = false;
            waitingDoor = null;
            cornerCount = 0;
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        }

        public void Face(Vector3 point) { hasFace = true; facePoint = point; }
        public void ClearFace() { hasFace = false; }

        public bool IsFacing(Vector3 point, float toleranceDegrees = 15f)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.sqrMagnitude < 1e-4f || Vector3.Angle(Forward, d) <= toleranceDegrees;
        }

        public Quaternion RotationFacing(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return transform.rotation;
            return Quaternion.LookRotation(modelFacesBackward ? -direction : direction, Vector3.up);
        }

        // Moves her with collisions off, for sitting down and standing up (the seat is inside
        // the chair). collideWhenDone puts her body back on at the end. Without it she stays
        // seated in `seat`, and the stand-in capsule takes over from her body.
        public void SlideTo(Vector3 position, Vector3 facing, float seconds, bool collideWhenDone, GameObject seat = null)
        {
            Stop();
            sliding = true;
            body.enabled = false;
            if (!collideWhenDone) SetSeatedBody(seat);
            slideFrom = transform.position;
            slideTo = position;
            rotFrom = transform.rotation;
            rotTo = RotationFacing(facing);
            slideT = 0f;
            slideDuration = Mathf.Max(0.01f, seconds);
            collideAfterSlide = collideWhenDone;
            horizontal = Vector3.zero;
            verticalSpeed = 0f;
            CurrentSpeed = 0f;
        }

        public void EnableCollisions()
        {
            sliding = false;
            if (body.enabled) return;
            BodyOn();
        }

        // Host: something (the truck, CrewBumper) is about to hit her. She staggers aside and
        // perceives being run over or bumped, blamed on 'by'.
        public void Knock(Vector3 velocityChange, int by) { }

        // Puts her somewhere at once (debug, tests, the intro placement).
        public void Teleport(Vector3 position, Vector3 facing)
        {
            Stop();
            sliding = false;
            body.enabled = false;
            transform.SetPositionAndRotation(position, RotationFacing(facing));
            NetTransforms.Snap(gameObject);   // host: the next sample jumps; no-op offline
            horizontal = Vector3.zero;
            verticalSpeed = 0f;
            BodyOn();
        }

        // Online client (GrandmaSync Flags): her body as the host has it. Off while she slides
        // or sits; seated, the stand-in capsule keeps her solid and talkable.
        public void SetReplicaCollisions(bool on, bool seated)
        {
            if (on) { if (!body.enabled) BodyOn(); return; }
            body.enabled = false;
            if (seated) SetSeatedBody(null);
            else if (seatedBody != null) seatedBody.enabled = false;
        }

        void BodyOn()
        {
            body.enabled = true;
            if (seatedBody != null) seatedBody.enabled = false;   // also forgets its ignore pairs
            Resync();
        }

        // While she sits, her CharacterController (her only collider) is off, and players would
        // walk through her and their interaction ray would miss her ("Talk" gone). A plain
        // capsule stands in: the crew bumps into it, the ray finds GrandmaTalk through it.
        void SetSeatedBody(GameObject seat)
        {
            if (seatedBody == null)
            {
                var go = new GameObject("SeatedBody") { layer = gameObject.layer };
                go.transform.SetParent(transform, false);
                seatedBody = go.AddComponent<CapsuleCollider>();
                seatedBody.direction = 1;
                seatedBody.radius = seatedRadius;
                seatedBody.height = Mathf.Max(seatedHeight, seatedRadius * 2f);
                seatedBody.center = new Vector3(0f, seatedBody.height * 0.5f + 0.05f, 0f);
            }
            seatedBody.enabled = true;

            // The seat is inside the capsule: without this the capsule would shove the chair
            // away. Set after enabling, every time: Unity only takes ignore pairs between
            // enabled colliders on active objects, and drops them when one is switched off.
            // Nothing to undo when she stands up; a leftover pair would be harmless anyway.
            if (seat == null || !seat.activeInHierarchy) return;
            seatColliders.Clear();
            seat.GetComponentsInChildren(seatColliders);
            for (int i = 0; i < seatColliders.Count; i++)
            {
                Collider c = seatColliders[i];
                if (c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(seatedBody, c, true);
            }
            seatColliders.Clear();
        }

        public bool RecentlyOperatedDoorNear(Vector3 point, float radiusMetres, float seconds)
        {
            return Time.time - lastDoorTime <= seconds && (point - lastDoorPosition).sqrMagnitude <= radiusMetres * radiusMetres;
        }

        // Length of what is left of the current path, from where she stands.
        public float RemainingPathLength()
        {
            if (status != MoveStatus.Moving || cornerCount < 2) return 0f;
            int next = NextCorner();
            float len = Vector3.Distance(transform.position, corners[next]);
            for (int i = next + 1; i < cornerCount; i++) len += Vector3.Distance(corners[i - 1], corners[i]);
            return len;
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (!Net.HasAuthority) { TickReplica(dt); return; }
            baker.Tick();
            TickRebuilds();
            TickDetours();

            if (sliding) { TickSlide(dt); return; }
            if (agent == null || !body.enabled || dt <= 0f) { CurrentSpeed = 0f; return; }

            if (!agent.isOnNavMesh)
            {
                Resync();
                if (!agent.isOnNavMesh) { CurrentSpeed = 0f; return; }
            }

            Vector3 wanted = status == MoveStatus.Moving ? Steer() : Vector3.zero;
            horizontal = Vector3.MoveTowards(horizontal, wanted, acceleration * dt);

            Vector3 look = horizontal.sqrMagnitude > 0.0025f ? horizontal
                         : waitingDoor != null ? doors.CenterOf(waitingDoor) - transform.position
                         : hasFace ? facePoint - transform.position : Vector3.zero;
            look.y = 0f;
            if (look.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, RotationFacing(look), turnSpeed * dt);

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            else verticalSpeed += gravity * dt;
            body.Move((horizontal + Vector3.up * verticalSpeed) * dt);

            Vector3 v = body.velocity;
            v.y = 0f;
            CurrentSpeed = v.magnitude;

            agent.nextPosition = transform.position;
            Vector3 drift = agent.nextPosition - transform.position;
            drift.y = 0f;
            if (drift.sqrMagnitude > 1f) Resync();   // pushed off the mesh (a blast, a teleport)

            TickCloseBehind();
            if (status == MoveStatus.Moving) CheckProgress(dt, wanted);
        }

        // Online client: her speed from the streamed motion (the walk blend reads it). Never a
        // body Move: the stream owns her position. Zero while her body is off, as on the host.
        void TickReplica(float dt)
        {
            Vector3 p = transform.position;
            Vector3 d = p - replicaLast;
            d.y = 0f;
            bool jump = !replicaHasLast || d.sqrMagnitude > 1f;   // a teleport, not a walk
            replicaLast = p;
            replicaHasLast = true;
            if (jump || dt <= 0f || !body.enabled) { CurrentSpeed = 0f; return; }
            CurrentSpeed = d.magnitude / dt;
        }

        void FixedUpdate()
        {
            if (pushBody == null) return;
            pushBody.AddForce(pushDirection * pushForce, ForceMode.Force);
            pushBody = null;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // A chair left in the corridor gets nudged aside, like a real person would.
            Rigidbody rb = hit.rigidbody;
            if (rb == null || rb.isKinematic || rb.mass > pushMaxKg || hit.moveDirection.y < -0.5f) return;
            Vector3 d = hit.moveDirection;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            pushBody = rb;
            pushDirection = d.normalized;
        }

        Vector3 Steer()
        {
            if (Time.time < pauseUntil) return Vector3.zero;
            if (repathAt > 0f && Time.time >= repathAt)
            {
                repathAt = -1f;
                Repath(true);
                if (status != MoveStatus.Moving) return Vector3.zero;
            }

            if (waitingDoor != null)
            {
                bool through = !waitingDoor.CanSwing || (waitingDoor.IsOpen && !waitingDoor.IsMoving);
                if (!through && Time.time < doorWaitUntil) return Vector3.zero;
                waitingDoor = null;
            }

            if (Time.time >= nextDoorCheck)
            {
                nextDoorCheck = Time.time + 0.15f;
                HingedPanel door = doors.FindAhead(transform.position, corners, NextCorner(), cornerCount, doorLookAhead, out float along);
                if (door != null && along <= doorStopDistance + 0.1f)
                {
                    OpenDoor(door);
                    return Vector3.zero;
                }
            }

            Vector3 desired = agent.desiredVelocity;
            desired.y = 0f;
            if (desired.sqrMagnitude > moveSpeed * moveSpeed) desired = desired.normalized * moveSpeed;
            return desired;
        }

        void OpenDoor(HingedPanel door)
        {
            waitingDoor = door;
            doorWaitUntil = Time.time + doorWaitMax;
            DoorWaitCount++;
            if (door.IsOpen) return;   // swinging open already: just wait for it
            door.SetOpen(true);
            DoorOpenCount++;
            NoteDoor(door);
            if (closeDoorsBehind)
            {
                closeBehind = door;
                closeBehindSide = doors.SideOf(door, transform.position);
                closeBehindSince = Time.time;
            }
        }

        void NoteDoor(HingedPanel door)
        {
            lastDoorPosition = doors.CenterOf(door);
            lastDoorTime = Time.time;
        }

        // She shuts the door she opened once she is through it and clear of its swing. Distance
        // alone is not enough: she stops up to 1.3 m short of a shut leaf to open it, which is
        // already "clear", and the door would shut in her face the moment it finished opening.
        void TickCloseBehind()
        {
            HingedPanel door = closeBehind;
            if (door == null || waitingDoor != null) return;
            if (!door.CanSwing || !door.IsOpen) { closeBehind = null; return; }
            Vector3 pos = transform.position;
            // Clear of the leaf's whole swing too: a garage door hinged at the top sweeps 2.6 m
            // into the garage, and closed on her there it shoves her back to the door.
            if (door.IsMoving || doors.IsInside(door, pos, DoorCloseClearance) || doors.InSweep(door, pos, radius + 0.05f)) return;
            if (doors.SideOf(door, pos) == closeBehindSide)
            {
                // Still on the side she opened it from. Hers to close only once she has given
                // that way up: a new plan that no longer goes through it, or a stop that lasts.
                if (status == MoveStatus.Moving && doors.IsOnPath(door, pos, corners, NextCorner(), cornerCount, doorLookAhead * 2f)) return;
                if (Time.time - closeBehindSince < DoorGiveUpSeconds) return;
            }
            // Never on a player standing in the doorway.
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != null && doors.IsInside(door, crew[i].Position, 0.2f)) return;
            door.SetOpen(false);
            NoteDoor(door);
            closeBehind = null;
        }

        void CheckProgress(float dt, Vector3 wanted)
        {
            // Arrival is measured here, against the end of the planned path: the agent's own
            // remainingDistance is not always known on the frame a path is set.
            if (cornerCount > 0)
            {
                Vector3 end = corners[cornerCount - 1] - transform.position;
                float dy = end.y;
                end.y = 0f;
                if (end.sqrMagnitude <= arriveRadius * arriveRadius && Mathf.Abs(dy) < 1f)
                {
                    status = MoveStatus.Arrived;
                    if (agent.isOnNavMesh) agent.ResetPath();
                    return;
                }
            }
            if (!agent.pathPending && !agent.hasPath && cornerCount > 0)
            {
                // The agent dropped its path (a NavMesh swap under it): plan again.
                Repath(true);
                return;
            }

            bool trying = waitingDoor == null && Time.time >= pauseUntil && wanted.sqrMagnitude > 0.04f;
            if (trying && CurrentSpeed < 0.05f) stuckTime += dt;
            else stuckTime = Mathf.Max(0f, stuckTime - dt);
            if (stuckTime > stuckSeconds) OnStuck();
        }

        void OnStuck()
        {
            stuckTime = 0f;
            stuckRetries++;
            if (stuckRetries > maxStuckRetries)
            {
                Stop();
                status = MoveStatus.Failed;
                return;
            }

            Vector3 ahead = horizontal.sqrMagnitude > 1e-4f ? horizontal.normalized : Forward;
            Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
            Vector3 top = transform.position + Vector3.up * (height - radius);
            if (Physics.CapsuleCast(bottom, top, radius * 0.9f, ahead, out RaycastHit hit, 0.7f, ~0, QueryTriggerInteraction.Ignore))
            {
                CrewMember member = CrewRoster.Owner(hit.collider.transform);
                if (member != null && stuckRetries == 1)
                {
                    try { BlockedBy?.Invoke(member); } catch (Exception e) { Debug.LogException(e); }
                    pauseUntil = Time.time + blockedByPlayerWait;
                    return;
                }
            }
            Detour(transform.position + ahead * 0.65f);
            repathAt = Time.time + 0.2f;   // carving applies on the next navigation update
        }

        // Marks a spot as blocked for a few seconds so the next plan goes round it.
        void Detour(Vector3 at)
        {
            int slot = detourUntil[0] <= detourUntil[1] ? 0 : 1;
            GameObject go = detours[slot];
            if (go == null)
            {
                go = new GameObject("GrandmaDetour");
                var obstacle = go.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(0.7f, 1.6f, 0.7f);
                obstacle.center = new Vector3(0f, 0.8f, 0f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                detours[slot] = go;
            }
            go.transform.position = at;
            go.SetActive(true);
            detourUntil[slot] = Time.time + detourSeconds;
        }

        void TickDetours()
        {
            for (int i = 0; i < detours.Length; i++)
                if (detours[i] != null && detours[i].activeSelf && Time.time >= detourUntil[i]) detours[i].SetActive(false);
        }

        void TickSlide(float dt)
        {
            CurrentSpeed = 0f;
            slideT += dt / slideDuration;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slideT));
            transform.SetPositionAndRotation(Vector3.Lerp(slideFrom, slideTo, t), Quaternion.Slerp(rotFrom, rotTo, t));
            if (slideT < 1f) return;
            sliding = false;
            if (collideAfterSlide)
            {
                BodyOn();
                if (status == MoveStatus.Moving && agent != null && !ComputePath()) status = MoveStatus.Failed;
            }
        }

        // ---------------------------------------------------------------- planning

        bool ComputePath()
        {
            cornerCount = 0;
            PathIsPartial = false;
            if (!agent.isOnNavMesh) Resync();
            if (!agent.isOnNavMesh) return false;
            if (!SampleNear(destination, out Vector3 to)) return false;
            Vector3 from = transform.position;
            if (NavMesh.SamplePosition(from, out NavMeshHit here, SampleRadius, NavMesh.AllAreas)) from = here.position;
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid) return false;
            agent.speed = moveSpeed;
            if (!agent.SetPath(path))
            {
                // In the frame a NavMesh update lands (OnBuilt), the agent is still bound to the
                // replaced tiles: isOnNavMesh says true and the path above is complete, but
                // SetPath refuses (measured, E2 tests 09 and 10). A warp binds it to the new ones.
                agent.Warp(from);
                if (!agent.SetPath(path)) return false;
            }
            cornerCount = path.GetCornersNonAlloc(corners);
            PathIsPartial = path.status == NavMeshPathStatus.PathPartial;
            LastPathLength = 0f;
            for (int i = 1; i < cornerCount; i++) LastPathLength += Vector3.Distance(corners[i - 1], corners[i]);
            return true;
        }

        // The NavMesh point for a target. A noise often comes from a shelf or a table top, more
        // than a metre above the floor she can stand on: then the floor under it. The search
        // stays small and looks down only, so it never snaps onto a roof (A8 1.3).
        static bool SampleNear(Vector3 target, out Vector3 onMesh)
        {
            onMesh = target;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas)
                || NavMesh.SamplePosition(target + Vector3.down * 1.4f, out hit, SampleRadius + 0.3f, NavMesh.AllAreas))
            {
                onMesh = hit.position;
                return true;
            }
            return false;
        }

        // True when a new path was planned.
        bool Repath(bool count)
        {
            if (agent == null || status != MoveStatus.Moving) return false;
            if (sliding || !body.enabled) return false;
            if (!ComputePath()) { Stop(); status = MoveStatus.Failed; return false; }
            if (count) RepathCount++;
            return true;
        }

        void Resync()
        {
            if (agent == null) return;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) agent.Warp(hit.position);
        }

        int NextCorner()
        {
            if (cornerCount < 2) return Mathf.Max(0, cornerCount - 1);
            Vector3 target = agent != null && agent.hasPath ? agent.steeringTarget : corners[1];
            for (int i = 1; i < cornerCount; i++)
                if ((corners[i] - target).sqrMagnitude < 0.01f) return i;
            return 1;
        }

        // ---------------------------------------------------------------- the house changes

        void OnWorldEvent(WorldEvent e)
        {
            if (!Net.HasAuthority) return;   // no NavMesh on the client
            switch (e.type)
            {
                case WorldEventType.StructureDamaged:
                case WorldEventType.DoorBroken:
                    ScheduleRebuild(structureRebuildDelay);
                    break;
                case WorldEventType.StructureCollapsed:
                    ScheduleRebuild(structureRebuildDelay);
                    // What fell is loose now (a roof section): where it lands only matters when
                    // furniture is baked in.
                    if (furnitureAsObstacles) ScheduleSettle();
                    break;
                case WorldEventType.FurnitureMoved:
                case WorldEventType.CargoLoaded:
                case WorldEventType.CargoUnloaded:
                case WorldEventType.ObjectDestroyed:
                    // Loose things are only in the NavMesh with furniture baked in, and then
                    // only the heavy ones: a cup changing place changes nothing.
                    MovableObject item = e.Item;
                    if (furnitureAsObstacles && (item == null || item.Mass >= obstacleMinKg)) ScheduleSettle();
                    break;
                case WorldEventType.Explosion:
                    // A safety net: what the blast broke raises its own structure events.
                    ScheduleSettle();
                    break;
            }
        }

        // A structural change: as soon as possible. Several pieces fall in one blast: one
        // rebuild for all of them, at the first deadline.
        public void ScheduleRebuild(float delay)
        {
            float at = Time.time + Mathf.Max(0f, delay);
            if (structureRebuildAt < 0f || at < structureRebuildAt) structureRebuildAt = at;
        }

        // Things are moving: once they are at rest. Every new event pushes the deadline back,
        // so the rebuild sees where the blown furniture landed, not where it was flying; the
        // cap keeps a player shuffling chairs about from postponing it for ever. A structural
        // rebuild in between does not cancel this one.
        public void ScheduleSettle()
        {
            float now = Time.time;
            if (settleRebuildAt < 0f) settleSince = now;
            settleRebuildAt = Mathf.Min(now + Mathf.Max(0f, settleRebuildDelay), settleSince + Mathf.Max(settleRebuildDelay, settleMaxWait));
        }

        void TickRebuilds()
        {
            float now = Time.time;
            bool due = false;
            if (structureRebuildAt >= 0f && now >= structureRebuildAt) { structureRebuildAt = -1f; due = true; }
            if (settleRebuildAt >= 0f && now >= settleRebuildAt) { settleRebuildAt = -1f; due = true; }
            if (due)
            {
                baker.furnitureAsObstacles = furnitureAsObstacles;   // follows the inspector in Play
                baker.Request();
            }

            // Furniture baked in can move without an event: her own pushes, a blast, debris, a
            // drag. Its old place would stay a hole in her NavMesh. A cheap look every few
            // seconds at the bodies the last build saw.
            if (!furnitureAsObstacles || now < nextFurnitureScan) return;
            nextFurnitureScan = now + Mathf.Max(0.5f, furnitureScanInterval);
            if (settleRebuildAt < 0f && baker.HasNavMesh && !baker.Busy && baker.FurnitureChanged(0.3f)) ScheduleSettle();
        }
    }
}
