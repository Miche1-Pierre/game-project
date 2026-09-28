using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A police car of the flee (ADR-013): scene-placed, parked at the PoliceSpawn and active at
    // load, so NetIds registers its body and the transform stream carries it online. The host
    // drives it (EscapeMission dispatches it); lights and siren are derived locally on both
    // machines.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PoliceCar : MonoBehaviour
    {
        static readonly List<PoliceCar> all = new List<PoliceCar>();

        Rigidbody body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        // The enabled cars of the game scene.
        public static IReadOnlyList<PoliceCar> All => all;

        // Lights and siren on (both machines).
        public bool IsDispatched => false;
        // Officer zone active (host truth; client: speed under 0.5 m/s).
        public bool IsStopped => false;

        public Rigidbody Body
        {
            get
            {
                if (body == null) body = GetComponent<Rigidbody>();
                return body;
            }
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
        }
    }
}
