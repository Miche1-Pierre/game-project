using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Tiny playtest helper (greybox): R resets all movables + the contract, T respawns the player.
    // Self-wires from the scene, no inspector setup needed. Not shipped.
    public class MoversDebugTools : MonoBehaviour
    {
        public ContractManager contract;
        public GameObject player;
        public Transform playerSpawn;

        struct Pose { public Vector3 pos; public Quaternion rot; }
        readonly Dictionary<MovableObject, Pose> initial = new Dictionary<MovableObject, Pose>();

        void Start()
        {
            if (contract == null) contract = Object.FindAnyObjectByType<ContractManager>();
            if (player == null)
            {
                var pc = Object.FindAnyObjectByType<PlayerController>();
                if (pc != null) player = pc.gameObject;
            }
            if (playerSpawn == null)
            {
                var s = GameObject.Find("PlayerSpawn");
                if (s != null) playerSpawn = s.transform;
            }
            if (contract != null && contract.allObjects != null)
                foreach (var m in contract.allObjects)
                    if (m != null) initial[m] = new Pose { pos = m.transform.position, rot = m.transform.rotation };
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) ResetAll();
            if (Input.GetKeyDown(KeyCode.T)) Respawn();
        }

        void ResetAll()
        {
            foreach (var kv in initial)
            {
                var m = kv.Key;
                if (m == null) continue;
                if (m.rb != null) { m.rb.linearVelocity = Vector3.zero; m.rb.angularVelocity = Vector3.zero; }
                m.transform.SetPositionAndRotation(kv.Value.pos, kv.Value.rot);
                m.loaded = false;
                m.broken = false;
            }
            if (contract != null)
            {
                contract.money = 0;
                contract.complete = false;
                contract.timeLeft = contract.timeLimit;
                if (contract.truck != null) contract.truck.inside.Clear();
            }
        }

        void Respawn()
        {
            if (player == null || playerSpawn == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(playerSpawn.position, playerSpawn.rotation);
            if (cc != null) cc.enabled = true;
        }

        void OnGUI()
        {
            GUI.Label(new Rect(Screen.width - 230, 12, 220, 20), "[R] reset objects   [T] respawn");
        }
    }
}
