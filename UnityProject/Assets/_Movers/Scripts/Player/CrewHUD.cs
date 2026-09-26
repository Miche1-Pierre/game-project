using UnityEngine;

namespace Movers
{
    // The small per-player pieces of the HUD that are nobody else's: the crosshair in the
    // middle of this player's view, and how drunk this player is. Goes on the player root.
    // The prompt is PlayerInteract's and the pocket bar PlayerPockets', each in its own region
    // of the same view (SLICE_ARCHITECTURE, HUD regions).
    [DisallowMultipleComponent]
    public sealed class CrewHUD : MonoBehaviour
    {
        public bool showCrosshair = true;
        public float crosshairSize = 4f;
        public Color crosshairColor = new Color(1f, 1f, 1f, 0.85f);

        // An observation aid, not a player-facing gauge (it was GameHUD's, for one player): a
        // playtester watching over a shoulder needs to know whether the stagger is the beer or
        // the controls.
        public bool showDrunk = true;

        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.6f);

        Camera view;
        Drunkenness drunk;
        int shownPercent = -1;
        string drunkLabel;

        void Awake()
        {
            useGUILayout = false;
            drunk = GetComponent<Drunkenness>();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (view == null) view = CrewView.Of(this);
            if (!CrewView.TryGetRect(view, out Rect r)) return;
            GUI.depth = 0;

            if (showCrosshair)
            {
                float s = Mathf.Max(2f, crosshairSize * Mathf.Clamp(r.height / 1080f, 0.75f, 1.5f));
                var c = ViewportGUI.Region(r, HudRegion.Center, s, s);
                var old = GUI.color;
                GUI.color = Shadow;
                GUI.DrawTexture(new Rect(c.x - 1f, c.y - 1f, c.width + 2f, c.height + 2f), Texture2D.whiteTexture);
                GUI.color = crosshairColor;
                GUI.DrawTexture(c, Texture2D.whiteTexture);
                GUI.color = old;
            }

            if (showDrunk)
            {
                if (drunk == null) TryGetComponent(out drunk);   // the beer adds it on the first sip
                if (drunk != null && drunk.IsDrunk)
                {
                    int pct = Mathf.RoundToInt(drunk.Amount * 100f);
                    if (pct != shownPercent) { shownPercent = pct; drunkLabel = "Drunk " + pct + "%"; }
                    int size = ViewportGUI.FontSize(r, 16);
                    // Above the pocket bar, which owns the bottom-right corner itself.
                    var box = ViewportGUI.Region(r, HudRegion.BottomRight, 160f, size + 8f);
                    box.y -= size * 2.6f + 16f;
                    ViewportGUI.Label(box, drunkLabel, size, Color.white, TextAnchor.MiddleRight);
                }
            }
        }
    }
}
