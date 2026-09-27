using Movers.AudioSynth;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // The world around the house, for any scene with a crew in it.
    //
    // - Wind: a 2D bed, full in the garden, a faint draught indoors.
    // - Room tone: the hum of a quiet house, only indoors.
    // - Birds: real 3D sources placed in the open around a player, never inside the house. So
    //   indoors they come through the walls, muffled, from the side the garden is on, with no
    //   special case: the walls do it (AudioDirector occlusion).
    // - A car now and then on the street beyond the gate.
    //
    // Indoors is judged per player (is there a roof over this head?) and averaged: two
    // players share the speakers, one in the kitchen and one in the garden hear a bit of both.
    public sealed class AmbienceAudio
    {
        bool active;
        int wind = -1, room = -1;
        float outdoor = 1f, outdoorTarget = 1f;
        float nextBird, nextCar, nextCheck;
        bool hasStreet;
        Bounds street;

        public bool Active => active;
        public float Outdoor => outdoor;
        public int BirdsPlayed { get; private set; }
        public int CarsPlayed { get; private set; }

        public void OnScene(Scene scene, bool hasCrew)
        {
            active = hasCrew;
            wind = room = -1;
            if (!active) return;
            float now = Time.unscaledTime;
            nextBird = now + Random.Range(1.5f, 3f);
            nextCar = now + Random.Range(12f, 30f);
            hasStreet = FindStreet(out street);
        }

        public void Update()
        {
            if (!active) return;
            float now = Time.unscaledTime;
            if (now >= nextCheck)
            {
                nextCheck = now + 0.25f;
                outdoorTarget = MeasureOutdoor();
            }
            outdoor = Mathf.MoveTowards(outdoor, outdoorTarget, Time.unscaledDeltaTime / 1.5f);

            if (SfxBank.Has(SfxKind.WindLoop) && !AudioDirector.IsPlaying(wind))
                wind = AudioDirector.StartLoop(SfxKind.WindLoop, null, Vector3.zero, SoundPreset.AmbienceBed, 0.3f, 1f, 4f);
            if (SfxBank.Has(SfxKind.RoomToneLoop) && !AudioDirector.IsPlaying(room))
                room = AudioDirector.StartLoop(SfxKind.RoomToneLoop, null, Vector3.zero, SoundPreset.AmbienceBed, 0.2f, 1f, 4f);
            AudioDirector.SetLoop(wind, Mathf.Lerp(0.08f, 0.45f, outdoor), 1f);
            AudioDirector.SetLoop(room, Mathf.Lerp(0.35f, 0f, outdoor), 1f);

            if (now >= nextBird)
            {
                // Fewer birds when everyone is indoors: they are still there, just less often heard.
                nextBird = now + Random.Range(2.5f, 7f) * (outdoor < 0.3f ? 1.5f : 1f);
                if (SfxBank.Has(SfxKind.BirdCall) && BirdSpot(out Vector3 at))
                {
                    AudioDirector.PlayAt(SfxKind.BirdCall, at, SoundPreset.Ambience3D, Random.Range(0.45f, 0.8f), Random.Range(0.94f, 1.08f));
                    BirdsPlayed++;
                }
            }

            if (hasStreet && now >= nextCar)
            {
                nextCar = now + Random.Range(25f, 60f);
                if (SfxBank.Has(SfxKind.CarPass))
                {
                    AudioDirector.PlayAt(SfxKind.CarPass, StreetPoint(), SoundPreset.Ambience3D, 0.7f, Random.Range(0.9f, 1.1f));
                    CarsPlayed++;
                }
            }
        }

        static float MeasureOutdoor()
        {
            var crew = CrewRoster.All;
            int n = 0;
            float sum = 0f;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m == null || m.View == null || !m.View.isActiveAndEnabled) continue;
                n++;
                if (!Roofed(m.View.transform.position)) sum += 1f;
            }
            return n > 0 ? sum / n : 1f;
        }

        static bool Roofed(Vector3 p)
        {
            return Physics.Raycast(p + Vector3.up * 0.3f, Vector3.up, 25f, Occlusion.Mask, QueryTriggerInteraction.Ignore);
        }

        // Somewhere in the open 8 to 22 m from a player, up in the trees or on the roof line.
        static bool BirdSpot(out Vector3 at)
        {
            at = default;
            var crew = CrewRoster.All;
            if (crew.Count == 0) return false;
            CrewMember m = crew[Random.Range(0, crew.Count)];
            if (m == null) return false;
            Vector3 from = m.Position;
            for (int tries = 0; tries < 4; tries++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.5f) continue;
                float dist = Random.Range(8f, 22f);
                Vector3 p = from + new Vector3(dir.x, 0f, dir.y) * dist + Vector3.up * Random.Range(3f, 8f);
                if (!Roofed(p)) { at = p; return true; }
            }
            return false;
        }

        Vector3 StreetPoint()
        {
            Vector3 c = street.center, e = street.extents;
            bool alongX = e.x >= e.z;
            float t = Random.Range(-0.6f, 0.6f);
            return new Vector3(c.x + (alongX ? e.x * t : 0f), c.y + 0.6f, c.z + (alongX ? 0f : e.z * t));
        }

        // The street of Map01 is a root object "Street" (with "Road" under it). Another scene
        // without one simply has no traffic.
        static bool FindStreet(out Bounds b)
        {
            b = default;
            GameObject go = GameObject.Find("Street");
            if (go == null) go = GameObject.Find("Road");
            if (go == null) return false;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
