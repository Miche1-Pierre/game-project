using UnityEngine;

namespace Movers
{
    // The moving contract: the list of the house's objects, which of them the client wants
    // moved, the truck they go in and the time allowed. What happens to them is tracked by
    // ContractTracker; the run itself (timer, delivery, settlement) belongs to GameSession.
    //
    // Destruction changes the list in one way: an object on it that was smashed to pieces can
    // no longer be loaded, so it stops blocking delivery, and the client bills the crew its
    // full value instead (Lost). Money can go negative: a crew that levels the house can owe
    // more than it earned, and that is the joke, not a bug.
    public class ContractManager : MonoBehaviour
    {
        public TruckCargo truck;
        [Tooltip("Every movable object of the house: the list is the ones marked requiredForContract, the rest is hers to lose.")]
        public MovableObject[] allObjects;
        [Tooltip("Seconds to finish the job once it starts (the keys, or a break-in). 0 = no limit.")]
        public float timeLimit = 600f;

        // The session owns the clock now; kept readable here for older code.
        public float timeLeft => Session.TimeLeft;
        // Written by GameSession at the end: the settlement total, and whether it was delivered.
        [HideInInspector] public int money = 0;
        [HideInInspector] public bool complete = false;

        // Built on first use, not in Awake: Tutorial_01's bootstrap adds this component first
        // and fills allObjects after.
        ContractTracker tracker;
        public ContractTracker Tracker => tracker ?? (tracker = new ContractTracker(allObjects));

        // Where this contract is delivered (look at it, press the action key: PlayerInteract).
        // Null until Start, or when there is no truck.
        public DeliverPoint DeliverPoint { get; private set; }

        void Awake()
        {
            // A scene with a contract always has a run to go with it. Tutorial_01 predates the
            // session and has no grandmother, so its run starts in progress (GameSession).
            // Its GameSession wakes inside AddComponent and finds this contract by itself.
            // Its clock keeps the old behaviour, stopping at 0 while play goes on: ADR-009 says
            // the tutorial keeps working, and a carry playtest must not be cut off at 10 min.
            // Whether it should fail instead is an open question for Pierre.
            if (SceneLookup.Find<GameSession>(gameObject.scene) == null)
            {
                var session = gameObject.AddComponent<GameSession>();
                session.contract = this;
                session.failOnTimeUp = false;
            }
        }

        void Start()
        {
            // Delivery is a place at the truck. Map01 has the board placed in the scene; a scene
            // built before it existed (Tutorial_01) gets one made now, when its truck is known.
            DeliverPoint = SceneLookup.Find<DeliverPoint>(gameObject.scene);
            if (DeliverPoint == null && truck != null) DeliverPoint = DeliverPoint.CreateAt(truck, this, null);
            if (DeliverPoint != null && DeliverPoint.contract == null) DeliverPoint.contract = this;

            var s = GameSession.Current;
            var n = s != null ? s.Numbers : GameLoopNumbers.Defaults;
            PocketableRule.Apply(allObjects, n.pocketMaxKg, n.pocketMaxSide);
        }

        // Objects on the list that still exist: a destroyed one is no longer on the checklist.
        public int RequiredTotal() => Tracker.Remaining;
        public int RequiredLoaded() => Tracker.RemainingLoaded;

        // The client bills the crew for every object on the list they smashed.
        public int Lost => Tracker.DestroyedValue;

        // There was a list and every surviving object on it is in the truck.
        public bool AllRequiredLoaded() => Tracker.AllRemainingLoaded;

        // Kept for older callers. Delivering is the DeliverPoint's job now; this asks the
        // session to do exactly what the board does.
        public void Deliver()
        {
            var s = GameSession.Current;
            if (s == null) return;
            s.Deliver(Actors.World, truck != null ? truck.transform.position : transform.position);
        }
    }
}
