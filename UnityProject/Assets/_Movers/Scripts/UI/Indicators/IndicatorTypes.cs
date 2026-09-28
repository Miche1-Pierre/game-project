using UnityEngine;

namespace Movers
{
    // What a marker points at. The house keys are not a kind of their own: during the intro they
    // are in the grandmother's hands, so her marker carries them (a gold key badge) instead of a
    // second token stacked on the same spot. Exit and Police belong to the police flee: the exit
    // checkpoint once she called them, and each dispatched police car.
    public enum IndicatorKind { Partner, Grandma, Deliver, Exit, Police }

    // How a target shows inside one player's view this frame (the compass tape is separate).
    public enum MarkerMode
    {
        Hidden,
        Ring,       // in view: a ring over it, with the distance
        Edge,       // out of view: a token at the edge of the view and an arrow towards it
    }

    // When the grandmother is shown. Knowing where she is changes the stealing (steal under
    // watch, ADR-009): a design knob for Pierre, not a settled rule (NOTES.md, open question 1).
    public enum GrandmaReveal
    {
        Always,     // wherever she is
        WhenAlert,  // while she looks, investigates, reacts or confronts, and during the intro
        IntroOnly,  // only while the crew still has to get the keys from her
        Never,
    }

    // The picture in the middle of a token. Number is the other player's number, in Fredoka.
    public enum IndicatorIcon
    {
        None, Number, GrandmaCalm, GrandmaAnnoyed, GrandmaAngry, GrandmaFurious, Truck, Box, Key,
        Flag, PoliceCar,
    }

    // One target as one view shows it this frame: what the tests and the debug read, never what
    // the game logic decides with. Positions are in OnGUI pixels (origin top left, y down), the
    // space of ViewportGUI and Camera.pixelRect turned over.
    public struct IndicatorSnapshot
    {
        public IndicatorKind kind;
        public int partnerIndex;          // Partner: the other player's CrewMember.index; -1 otherwise
        public float distance;            // metres from the viewer's eyes

        public bool onTape;
        public float tapeDegrees;         // from the package's compass pose: + right, - left, 0 straight ahead
        public bool tapeClamped;          // behind the player: parked at the end of the tape
        public Vector2 tapePosition;      // the token's centre on the tape

        public MarkerMode mode;
        public Vector2 position;          // the ring or edge token's centre
        public float arrowAngle;          // Edge: screen degrees the arrow points to, 0 right, 90 down
        public bool aroundHud;            // Edge: slid round a HUD corner panel, or its target is under one
        public bool keysBadge;
        public Color color;

        // Where the UI Toolkit elements really are after the last layout (worldBound), for the
        // tests to check the drawing and not only the maths. NaN before the first layout.
        public Vector2 drawnPosition;
        public Vector2 drawnTapePosition;
    }
}
