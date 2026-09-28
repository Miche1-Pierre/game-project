using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Where the police cars of a flee start (ADR-013): a scene marker, placed per map, with the
    // cars parked at it. They are scene objects, active at load, so the network id sweep
    // registers their bodies (a car spawned at runtime would not be on the transform stream).
    // EscapeMission sends them one after the other, `stagger` seconds apart.
    [DisallowMultipleComponent]
    public sealed class PoliceSpawn : MonoBehaviour
    {
        [Tooltip("The cars parked here, the lead car first. Empty: every PoliceCar of the scene, nearest first.")]
        public PoliceCar[] cars = new PoliceCar[0];
        [Tooltip("Seconds between two departures. Negative: GameLoopNumbers.policeStagger.")]
        public float stagger = -1f;

        readonly List<PoliceCar> resolved = new List<PoliceCar>(4);

        // The cars in departure order. Built once, at the first ask (after every car's OnEnable).
        public IReadOnlyList<PoliceCar> Cars
        {
            get
            {
                if (resolved.Count > 0) return resolved;
                if (cars != null)
                    for (int i = 0; i < cars.Length; i++)
                        if (cars[i] != null && !resolved.Contains(cars[i])) resolved.Add(cars[i]);
                if (resolved.Count == 0)
                {
                    var all = PoliceCar.All;
                    for (int i = 0; i < all.Count; i++)
                        if (all[i] != null && all[i].gameObject.scene == gameObject.scene) resolved.Add(all[i]);
                    Vector3 at = transform.position;
                    resolved.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
                }
                return resolved;
            }
        }

        public float Stagger(GameLoopNumbers n) => stagger >= 0f ? stagger : n.policeStagger;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.3f, 1f);
            Gizmos.DrawWireSphere(transform.position, 1.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 4f);
        }
    }
}
