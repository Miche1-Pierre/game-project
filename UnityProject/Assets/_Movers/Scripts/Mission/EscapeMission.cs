using UnityEngine;

namespace Movers
{
    // The police flee of a map (ADR-013, DEV2 section 8), on _Systems. Its presence is what turns
    // the grandmother's call from a failure into a flee (GameSession.HasEscape). Everything it
    // needs comes from scene markers (MissionRoute, PoliceSpawn, EscapeCheckpoint, the cars), so
    // a new map only places them.
    [DisallowMultipleComponent]
    public sealed class EscapeMission : MonoBehaviour
    {
        [Tooltip("Seconds from the call to the lead car's arrival on this map. Negative: GameLoopNumbers.policeCountdown.")]
        public float policeCountdown = -1f;
        [Tooltip("Seconds of escape from the arrival on this map. Negative: GameLoopNumbers.fleeTimeLimit.")]
        public float fleeTimeLimit = -1f;

        [Header("Markers (found in the scene when empty)")]
        public MissionRoute route;
        public PoliceSpawn spawn;
        public EscapeCheckpoint checkpoint;
        public TruckVehicle truck;

        public TruckVehicle Truck => truck;
        public MissionRoute Route => route;
        public EscapeCheckpoint Checkpoint => checkpoint != null ? checkpoint : EscapeCheckpoint.Active;
        // The car whose arrival starts the escape time.
        public Rigidbody LeadCarBody
        {
            get
            {
                var cars = spawn != null ? spawn.Cars : null;
                return cars != null && cars.Count > 0 && cars[0] != null ? cars[0].Body : null;
            }
        }

        public float Countdown(GameLoopNumbers n) => policeCountdown >= 0f ? policeCountdown : n.policeCountdown;
        public float FleeTime(GameLoopNumbers n) => fleeTimeLimit >= 0f ? fleeTimeLimit : n.fleeTimeLimit;

        void Awake()
        {
            var scene = gameObject.scene;
            if (route == null) route = SceneLookup.Find<MissionRoute>(scene);
            if (spawn == null) spawn = SceneLookup.Find<PoliceSpawn>(scene);
            if (checkpoint == null) checkpoint = SceneLookup.Find<EscapeCheckpoint>(scene);
            if (truck == null) truck = SceneLookup.Find<TruckVehicle>(scene);
        }
    }
}
