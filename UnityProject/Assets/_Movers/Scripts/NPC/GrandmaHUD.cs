using UnityEngine;

namespace Movers
{
    // Her patience, top right of every player's view (HudRegion.TopRight): a bar and a word.
    // The one number the crew must keep an eye on, readable in split screen.
    [DisallowMultipleComponent]
    public sealed class GrandmaHUD : MonoBehaviour
    {
        public GrandmaMood mood;
        public int fontSize = 16;
        public float width = 230f;

        static readonly string[] Labels =
        {
            "Grandma: Sweet", "Grandma: Annoyed", "Grandma: Angry", "Grandma: Furious", "Grandma: Calling the police"
        };

        static readonly Color[] Colours =
        {
            new Color(0.35f, 0.85f, 0.4f), new Color(0.95f, 0.85f, 0.25f), new Color(1f, 0.55f, 0.15f),
            new Color(0.95f, 0.2f, 0.15f), new Color(0.6f, 0.1f, 0.1f)
        };

        void Awake()
        {
            useGUILayout = false;
            if (mood == null) mood = GetComponent<GrandmaMood>();
        }

        void OnGUI()
        {
            if (!HudMode.UseLegacy) return;   // the LumaFlow HUD (HudRoot) draws this now
            if (mood == null || Event.current.type != EventType.Repaint) return;
            var crew = CrewRoster.All;
            bool any = false;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m == null || m.View == null || !m.View.isActiveAndEnabled) continue;
                any = true;
                Draw(ViewportGUI.RectFor(m.View));
            }
            if (!any) Draw(new Rect(0f, 0f, Screen.width, Screen.height));
        }

        void Draw(Rect view)
        {
            int size = ViewportGUI.FontSize(view, fontSize);
            float w = Mathf.Min(width * size / fontSize, view.width * 0.45f);
            float h = size + 22f;
            Rect r = ViewportGUI.Region(view, HudRegion.TopRight, w, h);
            int tier = (int)mood.Tier;
            ViewportGUI.Panel(r, 0.4f);
            ViewportGUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 12f, size + 6f), Labels[tier], size, Color.white, TextAnchor.UpperLeft, true);
            ViewportGUI.Bar(new Rect(r.x + 6f, r.yMax - 12f, r.width - 12f, 8f), mood.Patience / 100f, Colours[tier]);
        }
    }
}
