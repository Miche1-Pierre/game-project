using UnityEngine;

namespace Movers
{
    // Ugly-on-purpose OnGUI HUD. Zero package dependencies (no TMP, no Canvas).
    public class GameHUD : MonoBehaviour
    {
        public ContractManager contract;

        Drunkenness drunk;   // found once, on the first frame someone is drinking

        void OnGUI()
        {
            if (contract == null) return;

            // Explicit, because SmokeVision draws a full-screen overlay and OnGUI sorts by
            // depth with lower on top. The smoke takes the player's view, never the checklist.
            GUI.depth = 0;

            var label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };

            GUILayout.BeginArea(new Rect(12, 12, 340, 460), GUI.skin.box);
            GUILayout.Label("<b>MOVING CONTRACT</b>", title);
            foreach (var o in contract.allObjects)
            {
                if (o == null || !o.requiredForContract) continue;
                string mark = o.loaded ? "[x]" : "[ ]";
                string br = o.broken ? "  (broken)" : "";
                GUILayout.Label($"{mark} {o.displayName}   ${o.contractValue}{br}", label);
            }
            GUILayout.Space(6);
            GUILayout.Label($"Loaded: {contract.RequiredLoaded()} / {contract.RequiredTotal()}", label);
            GUILayout.Label($"Money: ${contract.money}", label);
            GUILayout.Label($"Time: {Mathf.CeilToInt(contract.timeLeft)}s", label);
            // An observation aid, not a player-facing gauge. A playtester watching over a
            // shoulder needs to know whether the stagger is the beer or the controls.
            if (drunk == null) drunk = Object.FindFirstObjectByType<Drunkenness>();
            if (drunk != null && drunk.IsDrunk)
                GUILayout.Label($"Drunk: {Mathf.RoundToInt(drunk.Amount * 100)}%", label);
            GUILayout.Space(6);
            if (contract.complete)
                GUILayout.Label("<b>CONTRACT COMPLETE</b>", title);
            else if (contract.AllRequiredLoaded())
                GUILayout.Label("All loaded. Press E to DELIVER.", label);
            else
                GUILayout.Label("Load every required item into the truck.", label);
            GUILayout.EndArea();

            // Three lines now, so the strip is taller than the two-line one it grew out of.
            // At 50 px the third line printed on top of the second.
            GUILayout.BeginArea(new Rect(12, Screen.height - 74, Screen.width - 24, 70));
            GUILayout.Label("WASD move  |  Shift sprint  |  Ctrl crouch  |  Space jump  |  LMB grab/drop  |  RMB throw  |  E deliver  |  Esc cursor", label);
            GUILayout.Label("Carrying:  scroll to push out / pull in  |  hold R to turn it (mouse turns, scroll rolls)", label);
            GUILayout.Label("Hands free:  hold RMB to smoke (the cloud blinds anyone in it, you included, 7s)  |  hold F to drink the beer (one bottle, then it is gone)", label);
            GUILayout.EndArea();
        }
    }
}
