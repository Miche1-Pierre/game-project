using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Ugly-on-purpose OnGUI HUD. Zero package dependencies (no TMP, no Canvas).
    //
    // Per player, inside that player's viewport (SLICE_ARCHITECTURE section 12): the contract
    // top left, the toasts top centre, a one-line hint bottom left, and a "DELIVER HERE" marker
    // over the board once everything is loaded. Over both views: the intro card and the end
    // screen. The pockets, the prompt and the grandmother's patience are drawn by their own
    // systems in their own regions.
    public class GameHUD : MonoBehaviour
    {
        [Tooltip("Found through the session when empty.")]
        public ContractManager contract;

        readonly ContractPanel panel = new ContractPanel();
        readonly HudToasts toasts = new HudToasts();
        readonly SessionCards cards = new SessionCards();
        readonly List<CrewMember> views = new List<CrewMember>(4);

        const string DeliverMarker = "DELIVER HERE";
        const string IntroBanner = "Talk to the grandmother to get the keys";
        static readonly Color BannerColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color PoliceColor = new Color(1f, 0.35f, 0.3f);

        // The hint names what matters now, in the words of the device the player holds.
        const string KeyIntro = "Talk to the grandmother (E) to get the keys";
        const string KeyCarry = "Scroll: reach   R: turn it   RMB: throw   1-4: pocket it";
        const string KeyReady = "All loaded: E on the yellow board, right side of the truck at the back";
        const string KeyIdle = "LMB: grab   E: open, talk   1-4: pockets   F: wear, drink";
        const string PadIntro = "Talk to the grandmother (X) to get the keys";
        const string PadCarry = "LT + right stick: reach   LB: turn it   RT: throw   D-pad: pocket it";
        const string PadReady = "All loaded: X on the yellow board, right side of the truck at the back";
        const string PadIdle = "RB: grab   X: open, talk   D-pad: pockets   Y: wear, drink";

        string policeBanner = "";
        int policeSecond = -1;

        void Awake()
        {
            // Only GUI.Label is used: no GUILayout pass (and its allocations) every frame.
            useGUILayout = false;
        }

        void OnEnable() { toasts.Subscribe(); }
        void OnDisable() { toasts.Unsubscribe(); }

        void Update()
        {
            var session = GameSession.Current;
            if (contract == null && session != null) contract = session.contract;
            panel.Refresh(contract, session);
            UpdatePoliceBanner(session);
        }

        void OnGUI()
        {
            if (!HudMode.UseLegacy) return;   // the LumaFlow HUD (HudRoot) draws this now
            // Labels only: nothing to do for layout and input events.
            if (Event.current.type != EventType.Repaint) return;
            var session = GameSession.Current;
            bool intro = session != null && session.IntroCardShowing;
            bool end = session != null && Session.IsOver && session.Result != null;

            // Lower depth draws on top. The cards cover the interaction prompt and the smoke;
            // the in-game HUD sits with the prompt, above the smoke overlay (depth 5).
            GUI.depth = intro || end ? -10 : 0;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            if (end) { cards.DrawEnd(screen, session); return; }
            if (intro) { cards.DrawIntro(screen, session, contract); return; }

            CollectViews();
            if (views.Count == 0) DrawView(screen, null, session);   // no crew rig: one full view
            else
                for (int i = 0; i < views.Count; i++) DrawView(ViewportGUI.RectFor(views[i]), views[i], session);
        }

        void CollectViews()
        {
            views.Clear();
            var all = CrewRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                // A player whose camera is off (solo layout) has no view to draw in.
                if (m != null && m.View != null && m.View.isActiveAndEnabled) views.Add(m);
            }
        }

        void DrawView(Rect view, CrewMember member, GameSession session)
        {
            int size = ViewportGUI.FontSize(view, 16);
            if (contract != null) panel.Draw(view, size);

            string banner = "";
            Color bannerColor = BannerColor;
            if (session != null && session.PoliceCalled) { banner = policeBanner; bannerColor = PoliceColor; }
            else if (Session.State == SessionState.Intro && session != null && session.HasIntro) banner = IntroBanner;
            toasts.Draw(view, size, banner, bannerColor);

            DrawHint(view, member, size);
            DrawDeliverMarker(member, size);
        }

        void DrawHint(Rect view, CrewMember member, int size)
        {
            string hint = HintFor(member);
            if (hint == null) return;
            float lineH = size * 1.35f;
            Rect r = ViewportGUI.Region(view, HudRegion.BottomLeft, view.width * 0.55f, lineH * 2f);
            ViewportGUI.Label(r, hint, size, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerLeft);
        }

        string HintFor(CrewMember member)
        {
            // At the wheel the seat draws the driving controls (VehicleSeat, bottom centre).
            if (member != null && member.IsDriving) return null;
            bool pad = false;
            if (member != null && member.Input != null)
            {
                string label = member.Input.SourceLabel;
                if (label == "None") return null;       // nobody drives this player
                // "Gamepad 1", "Gamepad 2". A scripted player reads the keyboard words, as
                // PLAYER's own prompts do.
                pad = label.StartsWith("Gamepad", System.StringComparison.Ordinal);
            }
            if (Session.State == SessionState.Intro) return pad ? PadIntro : KeyIntro;
            if (member != null && member.Held != null) return pad ? PadCarry : KeyCarry;
            var dp = contract != null ? contract.DeliverPoint : null;
            if (dp != null && dp.CanInteract) return pad ? PadReady : KeyReady;
            return pad ? PadIdle : KeyIdle;
        }

        void DrawDeliverMarker(CrewMember member, int size)
        {
            var dp = contract != null ? contract.DeliverPoint : null;
            if (dp == null || !dp.CanInteract) return;
            Camera cam = member != null ? member.View : Camera.main;
            if (!ViewportGUI.WorldToGUI(cam, dp.transform.position + Vector3.up * 0.55f, out Vector2 at)) return;
            ViewportGUI.Label(new Rect(at.x - 100f, at.y - size, 200f, size * 1.6f), DeliverMarker, size, BannerColor, TextAnchor.MiddleCenter, true);
        }

        void UpdatePoliceBanner(GameSession session)
        {
            if (session == null || !session.PoliceCalled) return;
            int s = Mathf.CeilToInt(session.PoliceIn);
            if (s == policeSecond) return;
            policeSecond = s;
            policeBanner = "THE POLICE ARE COMING  " + s;
        }
    }
}
