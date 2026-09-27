using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The truck bed. Anything whose centre of mass rests inside this box counts as loaded. No
    // load button, no inventory: the object physically has to be in the truck (ADR-009).
    //
    // It no longer trusts trigger enter and exit. A pocketed or shattered object is switched off
    // and Unity sends no exit for that, and one exit out of three colliders was enough to unload
    // a three-collider sofa. So it looks, ten times a second: which objects touch the box, and
    // where is each one's centre of mass. The box is still a trigger collider, for its size and
    // its gizmo, and it rides with the truck.
    //
    // Hysteresis, so a crate sliding about at the open end does not flicker in and out of the
    // contract: an object loads once its centre has been in the box for settleSeconds with nobody
    // holding it, and unloads only when its centre is exitMargin outside the box. A vase thrown
    // through the box, or a crate carried in and out again, never counts.
    //
    // CargoLoaded and CargoUnloaded go out on WorldEvents with the object's value and whoever
    // handled it last. Cargo that falls out of a moving truck is the driver's doing. Every
    // CargoLoaded gets exactly one CargoUnloaded later, even when another system takes the object
    // out of `inside` itself (PlayerPockets does when an item is pocketed in the bed) or the
    // object is shattered: listeners keep their own lists and must never be left with a ghost.
    [DisallowMultipleComponent]
    public class TruckCargo : MonoBehaviour
    {
        // What is loaded right now, for other systems to read. The check rebuilds it from its own
        // record ten times a second, so a system that removes an entry (PlayerPockets) is simply
        // agreed with once the object has really left the box.
        public readonly HashSet<MovableObject> inside = new HashSet<MovableObject>();

        [Header("Capacity")]
        // Only reported (LoadedKg, OverCapacity). What overloading does is a design question; the
        // truck already feels the weight through the suspension and the engine.
        public float capacityKg = 2500f;

        [Header("Counting")]
        public float settleSeconds = 0.3f;
        public float exitMargin = 0.3f;
        public float attributionSeconds = 5f;   // "who loaded it": the last handler within this window
        public float checkInterval = 0.1f;

        [Header("Riding in a moving truck")]
        public float movingSpeed = 1.5f;        // m/s: above this the truck counts as moving
        // Light things are thin and fast enough to slip through an 8 cm wall in one physics step
        // when the truck stops dead; they get continuous collision while the truck moves.
        public float lightCargoKg = 40f;
        // An object that ends up inside a wall anyway is eased out, not fired across the street.
        public float ridingDepenetrationSpeed = 2f;

        public float LoadedKg { get; private set; }
        public float LoadedVolume { get; private set; }
        public int LoadedCount => Net.HasAuthority ? loaded.Count : replicaCount;
        public bool OverCapacity => LoadedKg > capacityKg;
        public bool Contains(MovableObject m) => m != null && loaded.Contains(m);

        sealed class Track
        {
            public int stamp;
            public float insideSince = -1f;
            public bool riding;
            public CollisionDetectionMode savedMode;
            public float savedDepenetration;
        }

        // What CargoLoaded was raised for and CargoUnloaded not yet: the truth the events and the
        // totals are built on, whoever edits `inside`.
        readonly HashSet<MovableObject> loaded = new HashSet<MovableObject>();
        readonly Dictionary<MovableObject, Track> tracks = new Dictionary<MovableObject, Track>();
        readonly List<MovableObject> scratch = new List<MovableObject>();
        readonly Collider[] hits = new Collider[128];
        BoxCollider box;
        Rigidbody truck;                        // the body the box rides on, or null (a static truck)
        TruckVehicle vehicle;
        Vector3 boxCentreInTruck;
        Quaternion boxRotationInTruck;
        int stamp;
        float nextCheck;
        bool warnedFull;
        int replicaCount;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            truck = GetComponentInParent<Rigidbody>();
            vehicle = GetComponentInParent<TruckVehicle>();
            if (box == null) Debug.LogWarning("[TruckCargo] " + name + " has no BoxCollider: nothing will ever count as loaded.");
            if (truck != null && box != null)
            {
                Transform t = truck.transform;
                boxCentreInTruck = t.InverseTransformPoint(transform.TransformPoint(box.center));
                boxRotationInTruck = Quaternion.Inverse(t.rotation) * transform.rotation;
            }
        }

        void FixedUpdate()
        {
            if (!Net.HasAuthority) return;   // the client's totals come from TruckSync, `loaded` from Items
            if (box == null || Time.time < nextCheck) return;
            nextCheck = Time.time + checkInterval;
            Check();
        }

        // The box in world space, from the physics pose of the truck rather than its interpolated
        // transform, so it agrees with the centres of mass it is compared with.
        void Zone(out Vector3 centre, out Quaternion rotation, out Vector3 half)
        {
            Vector3 s = transform.lossyScale;
            half = new Vector3(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y), Mathf.Abs(box.size.z * s.z)) * 0.5f;
            if (truck != null)
            {
                rotation = truck.rotation * boxRotationInTruck;
                centre = truck.position + truck.rotation * Vector3.Scale(boxCentreInTruck, truck.transform.lossyScale);
            }
            else
            {
                rotation = transform.rotation;
                centre = transform.TransformPoint(box.center);
            }
        }

        void Check()
        {
            Zone(out Vector3 centre, out Quaternion rotation, out Vector3 half);
            Quaternion toBox = Quaternion.Inverse(rotation);
            float now = Time.time;
            bool moving = truck != null && truck.linearVelocity.sqrMagnitude > movingSpeed * movingSpeed;
            stamp++;

            Vector3 reach = half + Vector3.one * exitMargin;
            int n = Physics.OverlapBoxNonAlloc(centre, reach, hits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (n == hits.Length && !warnedFull)
            {
                warnedFull = true;
                Debug.LogWarning("[TruckCargo] more than " + hits.Length + " colliders in the bed; some cargo may be missed.");
            }

            for (int i = 0; i < n; i++)
            {
                var body = hits[i].attachedRigidbody;
                if (body == null || body == truck) continue;
                if (!body.TryGetComponent(out MovableObject m)) continue;
                if (!tracks.TryGetValue(m, out Track track))
                {
                    track = new Track();
                    tracks.Add(m, track);
                }
                if (track.stamp == stamp) continue;   // one object, several colliders
                track.stamp = stamp;

                Vector3 p = toBox * (body.worldCenterOfMass - centre);
                Judge(m, track, p, half, now, moving);
            }

            // Loaded objects the box no longer touches: moved far in one step, teleported, pocketed,
            // worn, shattered or destroyed (inactive colliders are not in the overlap).
            scratch.Clear();
            foreach (var m in loaded)
                if (m == null || !tracks.TryGetValue(m, out Track t) || t.stamp != stamp) scratch.Add(m);
            for (int i = 0; i < scratch.Count; i++) Unload(scratch[i], centre);

            // Forget what is neither near the box nor loaded, so the table does not grow all game.
            scratch.Clear();
            foreach (var pair in tracks)
                if (pair.Value.stamp != stamp && !loaded.Contains(pair.Key)) scratch.Add(pair.Key);
            for (int i = 0; i < scratch.Count; i++)
            {
                if (tracks.TryGetValue(scratch[i], out Track t)) StopRiding(scratch[i], t);
                tracks.Remove(scratch[i]);
            }

            SyncPublicList();
            Recount();
        }

        void Judge(MovableObject m, Track track, Vector3 p, Vector3 half, float now, bool moving)
        {
            bool eligible = m.canBeLoaded && !m.destroyed && !m.inPocket && !m.worn && m.isActiveAndEnabled;

            if (!loaded.Contains(m))
            {
                // In hands is not in the truck: it loads when it is put down.
                if (eligible && m.holder == null && InBox(p, half, 0f))
                {
                    if (track.insideSince < 0f) track.insideSince = now;
                    else if (now - track.insideSince >= settleSeconds) Load(m);
                }
                else track.insideSince = -1f;
            }
            else if (!eligible || !InBox(p, half, exitMargin))
            {
                Unload(m, m.transform.position);
                track.insideSince = -1f;
            }

            // Riding physics only for what lies loose in a moving truck: a crate in someone's hands
            // is steered by those hands.
            if (moving && m.holder == null && loaded.Contains(m)) StartRiding(m, track);
            else StopRiding(m, track);
        }

        static bool InBox(Vector3 p, Vector3 half, float margin)
        {
            return Mathf.Abs(p.x) <= half.x + margin
                && Mathf.Abs(p.y) <= half.y + margin
                && Mathf.Abs(p.z) <= half.z + margin;
        }

        void Load(MovableObject m)
        {
            if (!loaded.Add(m)) return;
            inside.Add(m);
            m.loaded = true;
            WorldEvents.Raise(WorldEventType.CargoLoaded, m.transform.position,
                              m.RecentHandler(attributionSeconds), 0f, 0f, m.contractValue, m);
        }

        // where: a destroyed object has no transform left to ask.
        void Unload(MovableObject m, Vector3 where)
        {
            inside.Remove(m);
            if (!loaded.Remove(m)) return;

            // A destroyed object compares equal to null but its C# fields are still readable, and
            // the event still names it, so a listener can drop it from its own list.
            bool alive = m != null;
            if (alive)
            {
                m.loaded = false;
                if (tracks.TryGetValue(m, out Track t)) StopRiding(m, t);
                where = m.transform.position;
            }

            int who = m.RecentHandler(attributionSeconds);
            if (who == Actors.World && vehicle != null && vehicle.Driver != null && truck != null
                && truck.linearVelocity.sqrMagnitude > movingSpeed * movingSpeed)
                who = vehicle.DriverActor;
            WorldEvents.Raise(WorldEventType.CargoUnloaded, where, who, 0f, 0f, m.contractValue, m);
        }

        // `inside` mirrors `loaded`: entries others removed while the object is still in the box
        // come back, entries nobody loaded go.
        void SyncPublicList()
        {
            foreach (var m in loaded) inside.Add(m);
            if (inside.Count == loaded.Count) return;
            scratch.Clear();
            foreach (var m in inside)
                if (!loaded.Contains(m)) scratch.Add(m);
            for (int i = 0; i < scratch.Count; i++) inside.Remove(scratch[i]);
        }

        void Recount()
        {
            float kg = 0f, volume = 0f;
            foreach (var m in loaded)
            {
                if (m == null) continue;
                kg += m.Mass;
                volume += m.Volume;
            }
            LoadedKg = kg;
            LoadedVolume = volume;
        }

        // Online client: the host's totals (TruckSync Cargo).
        public void ApplyReplica(float kg, float volume, int count)
        {
            if (Net.HasAuthority) return;
            LoadedKg = kg;
            LoadedVolume = volume;
            replicaCount = count;
        }

        void StartRiding(MovableObject m, Track t)
        {
            if (t.riding || m.rb == null || m.rb.isKinematic) return;
            t.riding = true;
            t.savedMode = m.rb.collisionDetectionMode;
            t.savedDepenetration = m.rb.maxDepenetrationVelocity;
            if (m.Mass <= lightCargoKg) m.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m.rb.maxDepenetrationVelocity = ridingDepenetrationSpeed;
        }

        void StopRiding(MovableObject m, Track t)
        {
            if (!t.riding) return;
            t.riding = false;
            if (m == null || m.rb == null) return;
            // A body turned kinematic meanwhile (picked up, worn) cannot take ContinuousDynamic back.
            if (!m.rb.isKinematic || t.savedMode != CollisionDetectionMode.ContinuousDynamic)
                m.rb.collisionDetectionMode = t.savedMode;
            m.rb.maxDepenetrationVelocity = t.savedDepenetration;
        }

        void OnDisable()
        {
            foreach (var pair in tracks) StopRiding(pair.Key, pair.Value);
        }

        void OnDrawGizmos()
        {
            var bc = GetComponent<BoxCollider>();
            if (bc == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f);
            Gizmos.DrawCube(bc.center, bc.size);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(bc.center, bc.size);
        }
    }
}
