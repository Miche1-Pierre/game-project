using UnityEngine;

namespace Movers
{
    // A player driven by code: the editor playtests and the Play-mode tests press buttons
    // through the same CrewInput a human goes through, instead of reaching into private
    // methods. Everything set here stays set until changed, except Press and LookOnce, which
    // last exactly one poll.
    //
    // CrewInput ignores what is held on the first poll after SetSource (so a key swap never
    // fires a phantom press): give it one frame before the first Press.
    public sealed class ScriptedInputSource : ICrewInputSource
    {
        public Vector2 move;
        public Vector2 look;        // mouse-axis units per frame
        public float scroll;
        public float roll;

        uint held;
        uint pulse;
        Vector2 lookPulse;

        public string Label => "Script";

        public void Hold(CrewButton b, bool down)
        {
            if (down) held |= Bit(b);
            else held &= ~Bit(b);
        }

        // Down on the next poll, up on the one after: one click.
        public void Press(CrewButton b) { pulse |= Bit(b); }

        // One frame of look, like a single mouse movement.
        public void LookOnce(Vector2 delta) { lookPulse += delta; }

        public void ReleaseAll()
        {
            held = 0;
            pulse = 0;
            move = look = lookPulse = Vector2.zero;
            scroll = roll = 0f;
        }

        public void Poll(ref CrewInputFrame f, float dt)
        {
            f.move = Vector2.ClampMagnitude(move, 1f);
            f.lookDelta = look + lookPulse;
            f.scroll = scroll;
            f.rollDelta = roll;
            f.held = held | pulse;
            pulse = 0;
            lookPulse = Vector2.zero;
        }

        static uint Bit(CrewButton b) => 1u << (int)b;
    }
}
