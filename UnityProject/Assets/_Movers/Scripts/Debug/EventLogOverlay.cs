using System.Globalization;
using System.Text;
using UnityEngine;

namespace Movers
{
    // F7: the last world events, newest first, with who did it and where. What a playtester
    // looks at when the grandmother reacts to something nobody saw, or a theft does not show
    // up in the ledger. On _Systems.
    [DisallowMultipleComponent]
    public sealed class EventLogOverlay : MonoBehaviour
    {
        const string Owner = "GAMELOOP log";
        const int Shown = 24;
        const float RebuildInterval = 0.25f;
        // A strip down the left edge, a fifth of the screen wide: in split screen P1's
        // crosshair sits at a quarter of the screen width, and debug overlays stay off the
        // Center region (SLICE_ARCHITECTURE section 12). Lines are cut at the edge, not
        // wrapped, so the strip never grows past its height.
        const float WidthShare = 0.2f;

        static EventLogOverlay active;
        static bool visible;   // kept across a reload: an open log stays open

        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.75f);

        readonly StringBuilder sb = new StringBuilder(2048);
        GUIStyle style;        // made in the first OnGUI: GUI.skin is only readable there
        string text = "";
        float nextBuild;

        void Awake() { useGUILayout = false; }

        void OnEnable()
        {
            active = this;
            DebugCommands.Register(KeyCode.F7, false, "event log", Toggle, Owner);
        }

        void OnDisable()
        {
            if (active != this) return;
            active = null;
            DebugCommands.Unregister(Owner);
        }

        static void Toggle() { visible = !visible; }

        void Update()
        {
            if (!visible || Time.unscaledTime < nextBuild) return;
            nextBuild = Time.unscaledTime + RebuildInterval;
            sb.Length = 0;
            sb.Append("<b>World events</b> (F7)  ").Append(Session.State);
            int n = Mathf.Min(Shown, WorldEvents.RecentCount);
            for (int i = 0; i < n; i++) AppendLine(WorldEvents.GetRecent(i));
            text = sb.ToString();
        }

        // "12.3 CargoLoaded P1 Chair_A $120 (21,2,-19)": the what and the who first, since the
        // end of a long line is what gets cut.
        void AppendLine(in WorldEvent e)
        {
            sb.Append('\n').Append(e.time.ToString("0.0", CultureInfo.InvariantCulture))
              .Append(' ').Append(e.type).Append(' ').Append(Actors.Name(e.instigator));
            if (e.subject != null) sb.Append(' ').Append(e.subject.name);
            if (e.value != 0) sb.Append(" $").Append(e.value);
            if (e.loudness > 0f) sb.Append(" loud ").Append(e.loudness.ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append(" (").Append(Mathf.RoundToInt(e.position.x)).Append(',')
              .Append(Mathf.RoundToInt(e.position.y)).Append(',')
              .Append(Mathf.RoundToInt(e.position.z)).Append(')');
        }

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = true, clipping = TextClipping.Clip };
            GUI.depth = -5;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            style.fontSize = ViewportGUI.FontSize(screen, 13);
            float lineH = style.fontSize * 1.3f;
            var box = new Rect(12f, Screen.height * 0.42f, Screen.width * WidthShare, lineH * (Shown + 2));
            ViewportGUI.Panel(box, 0.7f);

            var r = new Rect(box.x + 6f, box.y + 6f, box.width - 12f, box.height - 12f);
            var old = GUI.color;
            GUI.color = Shadow;
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);
            GUI.color = Color.white;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active = null;
            visible = false;
        }
    }
}
