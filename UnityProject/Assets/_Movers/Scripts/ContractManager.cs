using UnityEngine;

namespace Movers
{
    // The economic loop: a required checklist, money, a timer, and delivery.
    public class ContractManager : MonoBehaviour
    {
        public TruckCargo truck;
        public MovableObject[] allObjects;
        public float timeLimit = 600f;

        [HideInInspector] public float timeLeft;
        [HideInInspector] public int money = 0;
        [HideInInspector] public bool complete = false;

        void Start() { timeLeft = timeLimit; }

        void Update()
        {
            if (complete) return;
            timeLeft -= Time.deltaTime;
            if (timeLeft < 0f) timeLeft = 0f;
            if (Input.GetKeyDown(KeyCode.E) && AllRequiredLoaded()) Deliver();
        }

        public int RequiredTotal()
        {
            int n = 0;
            foreach (var o in allObjects) if (o != null && o.requiredForContract) n++;
            return n;
        }

        public int RequiredLoaded()
        {
            int n = 0;
            foreach (var o in allObjects) if (o != null && o.requiredForContract && o.loaded) n++;
            return n;
        }

        public bool AllRequiredLoaded()
        {
            return RequiredTotal() > 0 && RequiredLoaded() >= RequiredTotal();
        }

        public void Deliver()
        {
            if (complete || !AllRequiredLoaded()) return;
            int payout = 0;
            foreach (var o in truck.inside)
            {
                if (o == null) continue;
                payout += o.broken ? o.contractValue / 2 : o.contractValue; // broken items pay less
            }
            money += payout;
            complete = true;
        }
    }
}
