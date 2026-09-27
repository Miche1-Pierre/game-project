using UnityEngine;

namespace Movers
{
    // Where the sound is heard from, in split screen.
    //
    // Unity has one AudioListener (SplitScreen keeps exactly one, on P1's camera in split and
    // on the watched camera in solo). With nothing else, P2's footsteps, P2's grunts and the
    // vase P2 just knocked over are all heard from P1's head: quiet, panned wrong, or silent
    // when P1 is in the garden. That is most of what P2 hears of his own game.
    //
    // So every sound the AudioDirector plays is heard by the nearest player instead: its
    // position is taken into that player's camera space and put back into the world around the
    // real listener at the same offset. The listener then hears it at the distance and on the
    // side that player would. Both players share one pair of speakers, so this is the only fair
    // answer: the sound belongs to whoever is closest to it.
    public static class Ears
    {
        static AudioListener listener;
        static float nextSearch;
        static float missingSince = -1f;

        public static AudioListener Listener => listener;
        public static Transform ListenerTransform => listener != null ? listener.transform : null;

        // Called once a frame by the AudioDirector. Finding the listener allocates, so it is
        // only done when the one we had is gone or switched off (a layout change moves it).
        // force: look again now (a test right after a layout change).
        public static void Refresh(bool force = false)
        {
            if (!force && listener != null && listener.isActiveAndEnabled) { missingSince = -1f; return; }
            if (!force && Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 0.25f;
            listener = null;
            var all = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
                if (all[i].isActiveAndEnabled) { listener = all[i]; break; }
            if (listener != null) { missingSince = -1f; return; }

            // No ears at all is silence with no error anywhere. A scene built without one (a
            // menu, a test scene) gets one on its main camera after a moment: the moment lets
            // SplitScreen place its own first.
            if (missingSince < 0f) { missingSince = Time.unscaledTime; return; }
            if (Time.unscaledTime - missingSince < 0.5f) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            listener = cam.gameObject.AddComponent<AudioListener>();
            Debug.LogWarning("[Audio] no AudioListener in the scene, added one to " + cam.name + ".");
        }

        // The camera of the player nearest to a point, or null when there is no crew (the
        // menu). Only cameras that are drawing count: in solo layout the hidden one is not
        // anyone's ears.
        public static Transform NearestView(Vector3 world)
        {
            var crew = CrewRoster.All;
            Transform best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m == null) continue;
                Camera cam = m.View;
                if (cam == null || !cam.isActiveAndEnabled) continue;
                float d = (cam.transform.position - world).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = cam.transform; }
            }
            return best;
        }

        // Where to put an AudioSource so the listener hears `world` as the nearest player would.
        // earPosition is that player's head, for distance and occlusion.
        public static Vector3 Map(Vector3 world, out Vector3 earPosition)
        {
            Transform ears = ListenerTransform;
            Transform view = NearestView(world);
            if (ears == null)
            {
                earPosition = view != null ? view.position : world;
                return world;
            }
            if (view == null || view == ears)
            {
                earPosition = ears.position;
                return world;
            }
            earPosition = view.position;
            return ears.TransformPoint(view.InverseTransformPoint(world));
        }

        // The head closest to a point: the ears the sound is judged from.
        public static Vector3 NearestEar(Vector3 world)
        {
            Transform view = NearestView(world);
            if (view != null) return view.position;
            Transform ears = ListenerTransform;
            return ears != null ? ears.position : world;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            listener = null;
            nextSearch = 0f;
            missingSince = -1f;
        }
    }
}
