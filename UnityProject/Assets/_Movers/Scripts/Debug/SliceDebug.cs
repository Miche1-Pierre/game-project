using UnityEngine;

namespace Movers
{
    // The one reader of debug keys. Systems register their commands in DebugCommands; this
    // component fires them, shows the list (F12) and the last toast. Put one on _Systems.
    // Active in the editor and in development builds only.
    [DefaultExecutionOrder(-600)]
    public sealed class SliceDebug : MonoBehaviour
    {
        public KeyCode helpKey = KeyCode.F12;
        bool showHelp;

        void Update()
        {
            if (!Debug.isDebugBuild) return;
            if (Input.GetKeyDown(helpKey)) showHelp = !showHelp;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var list = DebugCommands.All;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c.shift != shift || !Input.GetKeyDown(c.key)) continue;
                try
                {
                    c.run();
                    DebugCommands.Toast((c.shift ? "Shift+" : "") + c.key + "  " + c.label);
                }
                catch (System.Exception e) { Debug.LogException(e); }
                break;   // one command per key press; the list may have changed
            }
        }

        void OnGUI()
        {
            if (!Debug.isDebugBuild) return;
            int size = ViewportGUI.FontSize(new Rect(0, 0, Screen.width, Screen.height), 16);

            if (DebugCommands.lastToast != null && Time.unscaledTime - DebugCommands.lastToastTime < 2f)
            {
                var r = new Rect(0, Screen.height * 0.5f - 80, Screen.width, 30);
                ViewportGUI.Label(r, DebugCommands.lastToast, size, Color.yellow, TextAnchor.MiddleCenter, true);
            }

            if (!showHelp) return;
            var all = DebugCommands.All;
            float lineH = size + 6;
            var box = new Rect(20, 80, 520, 30 + lineH * (all.Count + 1));
            ViewportGUI.Panel(box, 0.7f);
            ViewportGUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, lineH), "Debug keys (F12 hides)", size, Color.white, TextAnchor.UpperLeft, true);
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                string k = (c.shift ? "Shift+" : "") + c.key;
                ViewportGUI.Label(new Rect(box.x + 10, box.y + 8 + lineH * (i + 1), box.width - 20, lineH),
                    k + "   " + c.label + "   (" + c.owner + ")", size, Color.white);
            }
        }
    }
}
