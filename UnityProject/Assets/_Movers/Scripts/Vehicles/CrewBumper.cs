using UnityEngine;

namespace Movers
{
    // Crew members in the truck's way are shoved out of it before the truck gets to them.
    //
    // A crew member is a CharacterController, and to a rigid body that is an immovable post.
    // Measured on a preview-scene rig: a 3.5 t box at 2.5 m/s lost 68 % of its speed in its first
    // physics step against a standing capsule and was at 0.01 m/s two steps later. Shoving the
    // player from the collision is too late, since the truck has already stopped by then. So this
    // looks ahead: every physics step, anyone the hull is about to reach gets a shove through
    // PlayerController.AddImpulse, at the speed the hull is coming at him plus a margin. He moves
    // in the next frame, before the truck arrives, and the truck never feels him (same rig with
    // the capsule moved ahead of the box: no speed lost at all). Hit from the front or the back,
    // he also goes a little sideways, out of the lane, instead of being pushed down the street.
    //
    // Left alone:
    // - someone standing on the truck (in the bed, on the roof): he is not in its way, and
    //   whether the crew rides in the back is an open question (TRUCK report);
    // - the driver;
    // - someone pinned against a wall: he cannot move, so the truck stops against him;
    // - the grandmother: she is a CharacterController too, but she is GRANDMA's. She still stops
    //   the truck like a post (TRUCK report, request to GRANDMA).
    //
    // Plain data plus one method, owned by TruckVehicle and called from its FixedUpdate.
    [System.Serializable]
    public sealed class CrewBumper
    {
        public float margin = 1f;               // m/s faster than the truck comes, so the gap opens
        // Seconds of the truck's travel to look ahead. The shove only moves the player in his next
        // Update, one or two physics steps later (more at a low frame rate).
        public float lookAhead = 0.1f;
        public float minClosingSpeed = 0.3f;    // m/s: slower is someone leaning on a parked truck
        public float sideKick = 0.5f;           // sideways speed per unit of closing speed, front and back only

        const int MaxPlayers = 4;               // Actors: players are 0..3
        [System.NonSerialized] int[] shovedFrame;   // made on first use: the serializer may skip initialisers

        // Online host, a body the client drives (NETCODE_SLICE 9.6): the shove travels to the
        // client and his new pose comes back a round trip later. Until then he is left alone,
        // and the hull ignores his stale capsule so the truck does not stop dead against it.
        const float RemoteWindowExtra = 0.2f;
        [System.NonSerialized] float[] remoteUntil;
        [System.NonSerialized] CharacterController[] remoteCapsule;
        [System.NonSerialized] Collider[] hullColliders;
        [System.NonSerialized] bool remoteWindowOpen;

        public void Tick(Rigidbody body, Bounds hull)
        {
            if (body == null || hull.size == Vector3.zero) return;
            if (remoteWindowOpen) CloseRemoteWindows();
            // Nothing on the truck moves fast enough to shove anyone: the usual case, parked.
            float reachSpeed = body.linearVelocity.magnitude + body.angularVelocity.magnitude * hull.extents.magnitude;
            if (reachSpeed < minClosingSpeed) return;
            if (shovedFrame == null) shovedFrame = new int[MaxPlayers];

            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                var m = crew[i];
                if (m == null || m.IsDriving || m.index < 0 || m.index >= MaxPlayers) continue;
                // Several physics steps can run before his next Update: one shove per frame, or
                // they would add up before the first one has moved him.
                if (shovedFrame[m.index] == Time.frameCount) continue;
                bool remote = !Net.Drives(m);
                if (remote && remoteUntil != null && Time.time < remoteUntil[m.index]) continue;
                var controller = m.Controller;
                if (controller == null || !controller.isActiveAndEnabled) continue;
                if (!m.TryGetComponent(out CharacterController capsule) || !capsule.enabled) continue;
                if (Shove(body, hull, controller, capsule, remote))
                {
                    shovedFrame[m.index] = Time.frameCount;
                    if (remote) OpenRemoteWindow(body, m.index, capsule);
                }
            }
        }

        void OpenRemoteWindow(Rigidbody body, int index, CharacterController capsule)
        {
            if (remoteUntil == null)
            {
                remoteUntil = new float[MaxPlayers];
                remoteCapsule = new CharacterController[MaxPlayers];
            }
            if (hullColliders == null) hullColliders = HullColliders(body);
            remoteUntil[index] = Time.time + Net.RoundTrip + RemoteWindowExtra;
            remoteCapsule[index] = capsule;
            for (int i = 0; i < hullColliders.Length; i++)
                if (Usable(hullColliders[i])) Physics.IgnoreCollision(hullColliders[i], capsule, true);
            remoteWindowOpen = true;
        }

        void CloseRemoteWindows()
        {
            bool open = false;
            for (int i = 0; i < MaxPlayers; i++)
            {
                var capsule = remoteCapsule[i];
                if (capsule == null) continue;
                if (Time.time < remoteUntil[i]) { open = true; continue; }
                remoteCapsule[i] = null;
                if (!Usable(capsule)) continue;   // switched off meanwhile: its pairs are gone anyway
                for (int k = 0; k < hullColliders.Length; k++)
                    if (Usable(hullColliders[k])) Physics.IgnoreCollision(hullColliders[k], capsule, false);
            }
            remoteWindowOpen = open;
        }

        static bool Usable(Collider c) => c != null && c.enabled && c.gameObject.activeInHierarchy;

        // The truck's own solid colliders: not triggers, wheels, the ramp (its own body) or a driver.
        static Collider[] HullColliders(Rigidbody body)
        {
            var all = body.GetComponentsInChildren<Collider>(true);
            var list = new System.Collections.Generic.List<Collider>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c.isTrigger || c is WheelCollider || c.attachedRigidbody != body) continue;
                if (CrewRoster.Owner(c.transform) != null) continue;
                list.Add(c);
            }
            return list.ToArray();
        }

        bool Shove(Rigidbody body, Bounds hull, PlayerController controller, CharacterController capsule, bool remote)
        {
            Vector3 centre = capsule.bounds.center;
            float radius = capsule.radius;
            float halfHeight = capsule.height * 0.5f;
            Quaternion rotation = body.rotation;
            Vector3 local = Quaternion.Inverse(rotation) * (centre - body.position);

            if (local.y + halfHeight < hull.min.y || local.y - halfHeight > hull.max.y) return false;   // under or over it
            float x = Mathf.Clamp(local.x, hull.min.x, hull.max.x);
            float z = Mathf.Clamp(local.z, hull.min.z, hull.max.z);
            if (x == local.x && z == local.z) return false;     // standing on the truck, or already inside it

            Vector3 near = body.position + rotation * new Vector3(x, Mathf.Clamp(local.y, hull.min.y, hull.max.y), z);
            Vector3 away = centre - near;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance < 1e-4f) return false;
            away /= distance;

            Vector3 hullVelocity = body.GetPointVelocity(near);
            float closing = Vector3.Dot(hullVelocity, away);
            if (closing < minClosingSpeed) return false;
            if (distance - radius > closing * lookAhead + 0.05f) return false;   // not yet

            // Top up to the speed he needs, whatever he is already doing: AddImpulse adds up.
            // A puppet's capsule never moves by itself: its velocity is the one its client reports.
            Vector3 current = remote ? controller.Velocity : capsule.velocity;
            Vector3 shove = Vector3.zero;
            float need = closing + margin - Vector3.Dot(current, away);
            if (need > 0f) shove += away * need;

            // Hit by the front or the back (the nearest point is on an end face): out of the lane.
            if (x == local.x && sideKick > 0f)
            {
                Vector3 side = rotation * (local.x >= hull.center.x ? Vector3.right : Vector3.left);
                side.y = 0f;
                side.Normalize();
                float sideNeed = closing * sideKick - Vector3.Dot(current, side);
                if (sideNeed > 0f) shove += side * sideNeed;
            }

            if (shove == Vector3.zero) return false;
            controller.AddImpulse(shove);
            return true;
        }
    }
}
