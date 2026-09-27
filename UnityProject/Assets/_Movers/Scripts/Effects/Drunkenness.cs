using UnityEngine;

namespace Movers
{
    // How drunk this player is, and what that does to them.
    //
    // Kept apart from the bottle on purpose. The beer is a source, this is the state, and the
    // two have different lifetimes: you can put the bottle down, you cannot put this down. It
    // also means the next thing that makes the crew useless (a bang on the head, a cat to the
    // face) feeds the same meter instead of growing a second one.
    //
    // Nothing here is a new system. It writes the fields PlayerController and PlayerGrab
    // already expose for exactly this, the way PlayerGrab writes speedMultiplier: the drunk
    // player is the sober player with worse numbers.
    //
    // It takes your aim and your heading, never your speed and never your control. You can
    // always cross the room, you just cannot do it in a straight line, and that difference is
    // the whole of CLAUDE.md rule 4.
    public class Drunkenness : MonoBehaviour
    {
        [Header("Clock")]
        // A full bottle to sober, in seconds. A whole beer is about a room and a half of
        // regret, which is long enough to be a decision and short enough not to end the run.
        public float soberSeconds = 25f;

        [Header("What it does")]
        public float maxLookSway = 5.5f;    // degrees the view wanders off where you put it
        public float maxRoll = 8f;          // degrees the horizon tips
        public float maxDrift = 24f;        // degrees between where you point and where you go
        public float maxCarrySlop = 1f;     // how much looser the grip gets, 0 disables it
        public float swaySpeed = 0.6f;

        [Range(0f, 1f)] [SerializeField] float amount;

        public float Amount => amount;
        public bool IsDrunk => amount > 0.01f;

        PlayerController controller;
        PlayerGrab grab;
        float seed;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            grab = GetComponent<PlayerGrab>();
            // Four players should not lurch in unison, which is what a shared clock would do.
            seed = Random.Range(0f, 100f);
        }

        public void Add(float a)
        {
            amount = Mathf.Clamp01(amount + a);
        }

        // Online client: the host's amount for this body (Players Drunk). The host decays it,
        // this only sways the body the client drives. Adds the component on first need, as a
        // bottle does on the host.
        public static void NetSetAmount(GameObject player, float a)
        {
            if (player == null) return;
            var d = player.GetComponent<Drunkenness>();
            if (d == null)
            {
                if (a <= 0f) return;   // sober and never drank: nothing to install
                d = player.AddComponent<Drunkenness>();
            }
            d.amount = Mathf.Clamp01(a);
        }

        void Update()
        {
            if (soberSeconds > 0.01f && Net.HasAuthority)   // online, only the host sobers anyone up
                amount = Mathf.MoveTowards(amount, 0f, Time.deltaTime / soberSeconds);

            // Eased, so a mouthful is a wobble and the whole bottle is a problem. Linear would
            // make half a beer feel like half a joke, which is no joke at all.
            float k = amount * (0.35f + 0.65f * amount);
            float t = Time.time * swaySpeed;

            if (controller != null)
            {
                // Layered sines at ratios that do not divide, so the wander never settles into
                // a rhythm the player can read and correct for.
                float pitch = (Mathf.Sin(t * 0.9f + seed) + 0.5f * Mathf.Sin(t * 2.3f + seed)) * 0.67f;
                float yaw = (Mathf.Sin(t * 0.7f + seed * 2f) + 0.5f * Mathf.Sin(t * 1.7f + seed)) * 0.67f;
                float roll = Mathf.Sin(t * 0.5f + seed * 3f);

                controller.lookSway = new Vector3(pitch * maxLookSway * k,
                                                  yaw * maxLookSway * k,
                                                  roll * maxRoll * k);
                controller.moveDrift = Mathf.Sin(t * 0.45f + seed * 4f) * maxDrift * k;
            }

            // The grip goes with it: what you carry lags further behind and swings more. It is
            // the same weight you always had, held by someone who should not be holding it.
            if (grab != null) grab.carrySlop = k * maxCarrySlop;
        }

        // Never leave a player crooked because the component was switched off mid-stagger.
        void OnDisable()
        {
            if (controller != null)
            {
                controller.lookSway = Vector3.zero;
                controller.moveDrift = 0f;
            }
            if (grab != null) grab.carrySlop = 0f;
        }
    }
}
