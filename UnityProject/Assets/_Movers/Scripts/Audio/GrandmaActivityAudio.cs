using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // What her activities sound like: the rocking chair creaking, pages turning, the teacup,
    // the soup pot bubbling, the watering can, the match for the fire, her television. Added
    // next to GrandmaActivities at runtime (SceneAudioBinder); it only watches which activity
    // she is in (Current, CurrentPhase).
    //
    // It tells the crew where she is and that she is settled, which is exactly what a thief
    // wants to know: the TV murmuring from the living room means the kitchen is safe.
    [DisallowMultipleComponent]
    public sealed class GrandmaActivityAudio : MonoBehaviour
    {
        GrandmaActivities activities;
        ActivitySpot spot;
        GrandmaActivities.Phase phase;
        int loop = -1;
        float nextOneShot, pourUntil, pourPauseUntil;

        public ActivityKind? Sounding { get; private set; }

        void Awake()
        {
            activities = GetComponent<GrandmaActivities>();
        }

        void Update()
        {
            if (activities == null) return;
            ActivitySpot cur = activities.Current;
            GrandmaActivities.Phase ph = activities.CurrentPhase;
            if (cur != spot || ph != phase) Change(cur, ph);
            if (spot == null || phase != GrandmaActivities.Phase.Looping) return;

            float now = Time.time;
            switch (spot.kind)
            {
                case ActivityKind.ReadBook:
                    if (now >= nextOneShot)
                    {
                        nextOneShot = now + Random.Range(5f, 11f);
                        AudioDirector.PlayOn(SfxKind.PageTurn, spot.transform, new Vector3(0f, 0.9f, 0.3f), SoundPreset.Activity, 0.6f, Random.Range(0.95f, 1.05f));
                    }
                    break;
                case ActivityKind.DrinkTea:
                    if (now >= nextOneShot)
                    {
                        nextOneShot = now + Random.Range(4f, 8f);
                        bool sip = Random.value < 0.65f;
                        AudioDirector.PlayOn(sip ? SfxKind.SipTea : SfxKind.TeaClink, spot.transform, new Vector3(0f, 1.2f, 0.25f),
                                             SoundPreset.Activity, sip ? 0.55f : 0.5f, Random.Range(0.96f, 1.04f));
                    }
                    break;
                case ActivityKind.WaterPlants:
                    // The can is tipped, then righted, then tipped again.
                    if (loop >= 0 && now >= pourUntil)
                    {
                        AudioDirector.Stop(loop, 0.3f);
                        loop = -1;
                        pourPauseUntil = now + Random.Range(1.5f, 3f);
                    }
                    else if (loop < 0 && now >= pourPauseUntil)
                    {
                        loop = AudioDirector.StartLoop(SfxKind.WaterPourLoop, spot.transform, new Vector3(0f, 0.5f, 0.5f), SoundPreset.Activity, 0.45f, 1f, 0.2f);
                        pourUntil = now + Random.Range(2f, 3.5f);
                    }
                    break;
            }
        }

        void Change(ActivitySpot cur, GrandmaActivities.Phase ph)
        {
            AudioDirector.Stop(loop, 0.5f);
            loop = -1;
            Sounding = null;
            spot = cur;
            phase = ph;
            if (spot == null || phase != GrandmaActivities.Phase.Looping) return;

            Sounding = spot.kind;
            float now = Time.time;
            nextOneShot = now + Random.Range(1f, 2.5f);
            Transform t = spot.transform;
            switch (spot.kind)
            {
                case ActivityKind.SitRockingChair:
                    loop = AudioDirector.StartLoop(SfxKind.RockCreakLoop, t, new Vector3(0f, 0.4f, 0f), SoundPreset.Activity, 0.6f, Random.Range(0.95f, 1.05f), 0.8f);
                    break;
                case ActivityKind.Cook:
                    loop = AudioDirector.StartLoop(SfxKind.PotBubbleLoop, t, new Vector3(0f, 0.95f, 0.55f), SoundPreset.Activity, 0.5f, 1f, 1.5f);
                    break;
                case ActivityKind.WatchTV:
                {
                    Transform tv = FindTelevision(spot);
                    loop = tv != null
                        ? AudioDirector.StartLoop(SfxKind.TvMurmurLoop, tv, new Vector3(0f, 0.4f, 0f), SoundPreset.Activity, 0.55f, 1f, 1f)
                        : AudioDirector.StartLoop(SfxKind.TvMurmurLoop, t, new Vector3(0f, 1f, 2.5f), SoundPreset.Activity, 0.55f, 1f, 1f);
                    break;
                }
                case ActivityKind.LightFire:
                    AudioDirector.PlayOn(SfxKind.MatchStrike, t, new Vector3(0f, 0.5f, 0.5f), SoundPreset.Activity, 0.6f, 1f);
                    break;
                case ActivityKind.DrinkTea:
                    AudioDirector.PlayOn(SfxKind.TeaClink, t, new Vector3(0f, 0.9f, 0.3f), SoundPreset.Activity, 0.5f, 1f);
                    break;
                case ActivityKind.WaterPlants:
                    pourPauseUntil = now + 0.6f;
                    break;
            }
        }

        static Transform FindTelevision(ActivitySpot s)
        {
            var req = s.requires;
            if (req == null) return null;
            for (int i = 0; i < req.Length; i++)
            {
                GameObject g = req[i];
                if (g == null) continue;
                string n = g.name;
                if (n.IndexOf("tele", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("tv", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return g.transform;
            }
            return null;
        }

        void OnDisable()
        {
            AudioDirector.Stop(loop, 0f);
            loop = -1;
            spot = null;
        }
    }
}
