using UnityEngine;

namespace Movers
{
    // Where one player's HUD pieces may draw. The rect comes from ViewportGUI; this only adds
    // the rule every per-player widget needs: a view that is not on screen (the other player in
    // a solo layout, F2) gets nothing drawn for it, or its prompt would land on top of the
    // player who is.
    public static class CrewView
    {
        public static bool TryGetRect(Camera cam, out Rect rect)
        {
            rect = default;
            if (cam == null || !cam.isActiveAndEnabled) return false;
            rect = ViewportGUI.RectFor(cam);
            return rect.width > 1f && rect.height > 1f;
        }

        // The camera a player looks through: the controller's, as CrewMember resolves it.
        public static Camera Of(Component player)
        {
            if (player == null) return null;
            var m = player.GetComponent<CrewMember>();
            if (m != null && m.View != null) return m.View;
            var pc = player.GetComponent<PlayerController>();
            return pc != null && pc.cam != null ? pc.cam.GetComponent<Camera>() : null;
        }
    }
}
