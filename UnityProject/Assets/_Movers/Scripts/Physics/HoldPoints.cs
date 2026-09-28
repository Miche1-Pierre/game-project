using UnityEngine;

namespace Movers
{
    // Optional: where the hands take hold of this carried thing, for a shape its box reads wrong.
    // Without one, CarryGrip works the holds out from the thing's box, and nothing needs one today.
    //
    // Each point is a child transform: its position is where the middle of the palm goes, and its
    // forward points straight out of the surface there, toward the hand. The right point alone
    // makes it a thing carried in one hand. Turned round in the hands, the right hand still takes
    // whichever point is on the right.
    [DisallowMultipleComponent]
    public sealed class HoldPoints : MonoBehaviour
    {
        public Transform left;
        public Transform right;
    }
}
