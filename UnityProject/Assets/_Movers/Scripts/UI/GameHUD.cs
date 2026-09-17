using UnityEngine;

namespace Movers
{
    // Ugly-on-purpose OnGUI HUD. Zero package dependencies (no TMP, no Canvas).
    public class GameHUD : MonoBehaviour
    {
        public ContractManager contract;

        void OnGUI()
        {
            if (contract == null) return;

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
            GUILayout.Space(6);
            if (contract.complete)
                GUILayout.Label("<b>CONTRACT COMPLETE</b>", title);
            else if (contract.AllRequiredLoaded())
                GUILayout.Label("All loaded. Press E to DELIVER.", label);
            else
                GUILayout.Label("Load every required item into the truck.", label);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(12, Screen.height - 34, Screen.width - 24, 30));
            GUILayout.Label("WASD move  |  Mouse look  |  LMB grab/drop  |  RMB throw  |  E deliver  |  Esc cursor", label);
            GUILayout.EndArea();
        }
    }
}
