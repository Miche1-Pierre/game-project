using UnityEngine;

namespace Movers
{
    // Retired. It read R, which is also "turn the held object", so rotating a box reset the
    // level; it read T; and its reset missed everything the destruction had broken. The reset
    // is now a scene reload on F5 (SessionDebug), and debug keys are F-keys only.
    //
    // The class stays, empty, only because Map01_Grandma.unity still has the component and a
    // missing script there would log a warning on load. Remove the component from that scene,
    // then delete this file.
    [AddComponentMenu("")]
    public class MoversDebugTools : MonoBehaviour
    {
    }
}
