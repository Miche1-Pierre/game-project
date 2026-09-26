using UnityEngine;

namespace Movers
{
    // Where a player's HUD pieces go. Every per-player widget draws inside its own player's
    // viewport, in the region its owning system was given (03_TECHNICAL/SLICE_ARCHITECTURE.md,
    // "HUD regions"), so two views side by side never draw on top of each other.
    public enum HudRegion
    {
        TopLeft,        // contract panel (GameHUD)
        TopCenter,      // toasts: "P2 pocketed the watch", "Grandma saw that!"
        TopRight,       // the grandmother's patience (GrandmaHUD)
        Center,         // crosshair
        BottomLeft,     // controls hint
        BottomCenter,   // interaction prompt
        BottomRight,    // pockets
    }

    public static class ViewportGUI
    {
        // OnGUI space (origin top-left, y down) for a camera's viewport. Camera.pixelRect has y up.
        public static Rect RectFor(Camera cam)
        {
            if (cam == null) return new Rect(0, 0, Screen.width, Screen.height);
            Rect r = cam.pixelRect;
            return new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
        }

        public static Rect RectFor(CrewMember m) => RectFor(m != null ? m.View : null);

        // A region of a viewport, as a box of the given size anchored in that region.
        public static Rect Region(Rect view, HudRegion region, float width, float height, float margin = 12f)
        {
            float x, y;
            switch (region)
            {
                case HudRegion.TopLeft: x = view.x + margin; y = view.y + margin; break;
                case HudRegion.TopCenter: x = view.x + (view.width - width) * 0.5f; y = view.y + margin; break;
                case HudRegion.TopRight: x = view.xMax - width - margin; y = view.y + margin; break;
                case HudRegion.Center: x = view.x + (view.width - width) * 0.5f; y = view.y + (view.height - height) * 0.5f; break;
                case HudRegion.BottomLeft: x = view.x + margin; y = view.yMax - height - margin; break;
                case HudRegion.BottomCenter: x = view.x + (view.width - width) * 0.5f; y = view.yMax - height - margin * 5f; break;
                default: x = view.xMax - width - margin; y = view.yMax - height - margin; break;
            }
            return new Rect(x, y, width, height);
        }

        // A world point in OnGUI coordinates for this camera. False when behind the camera or
        // outside its viewport.
        public static bool WorldToGUI(Camera cam, Vector3 world, out Vector2 gui)
        {
            gui = default;
            if (cam == null) return false;
            Vector3 s = cam.WorldToScreenPoint(world);
            if (s.z <= 0f) return false;
            if (!cam.pixelRect.Contains(new Vector2(s.x, s.y))) return false;
            gui = new Vector2(s.x, Screen.height - s.y);
            return true;
        }

        // Font size that stays readable when a view is half the screen wide.
        public static int FontSize(Rect view, int atFullHeight1080)
        {
            return Mathf.Max(10, Mathf.RoundToInt(atFullHeight1080 * Mathf.Clamp(view.height / 1080f, 0.6f, 1.6f)));
        }

        static GUIStyle label;
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.75f);

        // A label with a drop shadow, no allocation per call beyond the string itself.
        public static void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            if (label == null) label = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            label.fontSize = size;
            label.alignment = anchor;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            var old = GUI.color;
            GUI.color = Shadow;
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, label);
            GUI.color = color;
            GUI.Label(r, text, label);
            GUI.color = old;
        }

        // A filled bar, 0..1, with a dark back.
        public static void Bar(Rect r, float fill01, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(fill01), r.height - 2), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // A translucent panel behind text.
        public static void Panel(Rect r, float alpha = 0.45f)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
