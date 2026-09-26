using UnityEngine;

namespace Movers
{
    // Reads a button from the crew while their CrewInput is muted: skipping the intro card,
    // restarting from the end screen. Muting stops gameplay from seeing presses, but these two
    // cards still need one, from whichever player (keyboard, gamepad or script) presses it.
    //
    // It reads the press CrewInput already worked out this frame (CrewInput runs at -500,
    // GameSession at -400), rather than asking the input sources again: a second poll in the
    // same frame would find a stateful source already spent (ScriptedInputSource.Press lasts
    // one poll), and the press would never reach the card.
    public sealed class MutedInputReader
    {
        // No crew rig in the scene: the keyboard still works. Only this path keeps its own
        // edge state, so only this path needs a call every frame.
        static readonly KeyboardMouseSource Fallback = new KeyboardMouseSource();

        uint previous;
        bool primed;

        // Forget what the fallback keyboard held, so a key already down when a card opens is
        // not a press. (CrewInput's own presses are edges already.)
        public void Reset()
        {
            primed = false;
            previous = 0;
        }

        public bool Pressed(CrewButton a) => Pressed(a, a);

        public bool Pressed(CrewButton a, CrewButton b)
        {
            bool anyInput = false;
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var input = all[i] != null ? all[i].Input : null;
                if (input == null) continue;
                anyInput = true;
                if (DownThroughMute(input, a) || DownThroughMute(input, b)) return true;
            }
            return !anyInput && FallbackPressed(Bit(a) | Bit(b));
        }

        // CrewInput computes its presses every frame, muted or not (so nothing fires on
        // unmute), and only hides them behind Muted. Lifting the mute for the length of one
        // read shows them to this reader alone: no other code runs in between.
        static bool DownThroughMute(CrewInput input, CrewButton b)
        {
            bool was = input.Muted;
            input.Muted = false;
            bool down = input.Down(b);
            input.Muted = was;
            return down;
        }

        bool FallbackPressed(uint mask)
        {
            var f = new CrewInputFrame();
            Fallback.Poll(ref f, Time.unscaledDeltaTime);
            uint held = f.held & mask;
            bool pressed = primed && (held & ~previous) != 0;
            previous = held;
            primed = true;
            return pressed;
        }

        static uint Bit(CrewButton b) => 1u << (int)b;
    }
}
