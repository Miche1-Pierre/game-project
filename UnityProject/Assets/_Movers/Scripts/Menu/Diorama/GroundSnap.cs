using UnityEngine;

namespace Movers
{
    // Marks an object of the title screen's landscape that stands on the ground: a tree, a
    // fence, the house, the camera rig. MenuLand puts it at the ground's height (plus `offset`)
    // once the land is built, so the scene can be laid out in X and Z only and the hills can be
    // retuned without anything floating or sinking. No per-frame cost.
    [DisallowMultipleComponent]
    public sealed class GroundSnap : MonoBehaviour
    {
        [Tooltip("Metres above the ground (negative: sunk in a little, for trees on a slope).")]
        public float offset;
        [Tooltip("Tilt with the slope (fences), instead of staying upright (trees, houses).")]
        public bool followSlope;
    }
}
