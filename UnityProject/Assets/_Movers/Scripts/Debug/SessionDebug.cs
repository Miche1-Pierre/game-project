using UnityEngine;

namespace Movers
{
    // The game loop's debug keys (SLICE_ARCHITECTURE section 11), dispatched by SliceDebug:
    //   F5        reset: reload the scene, the only reset that catches everything
    //   F6        force Completed: settle now with whatever is loaded
    //   Shift+F6  force Failed
    // On _Systems next to GameSession.
    [DisallowMultipleComponent]
    public sealed class SessionDebug : MonoBehaviour
    {
        const string Owner = "GAMELOOP";

        // On a reload the new scene may register before the old one unregisters: only the
        // instance that registered last may take the keys away.
        static SessionDebug active;

        void OnEnable()
        {
            active = this;
            DebugCommands.Register(KeyCode.F5, false, "reset the scene (reload)", SceneReload.Reload, Owner);
            DebugCommands.Register(KeyCode.F6, false, "force Completed", ForceComplete, Owner);
            DebugCommands.Register(KeyCode.F6, true, "force Failed", ForceFail, Owner);
        }

        void OnDisable()
        {
            if (active != this) return;
            active = null;
            DebugCommands.Unregister(Owner);
        }

        static void ForceComplete()
        {
            var s = GameSession.Current;
            if (s != null) s.ForceComplete();
        }

        static void ForceFail()
        {
            var s = GameSession.Current;
            if (s != null) s.Fail(FailReason.Other);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active = null;
        }
    }
}
