using System;
using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // What a crew member's body and hands sound like, from the world events he raises (and the
    // blasts that throw him, whoever set them off). Added to each player at runtime
    // (SceneAudioBinder); nothing in Player/ calls it.
    //
    //   picked up something heavy   "hnnngh" (his own pitch, so you hear who)
    //   threw it                    "hup!" and the whoosh of the thing
    //   blown off his feet          "oof" or "whoa"
    //   pocketed / took out         cloth rustle, a pat on the pocket
    //   smoking                     the draw as the cigarette reaches his lips, the ember
    //                               crackling while he holds it there, the long exhale with the
    //                               smoke, now and then a cough
    //   drinking                    the bottle on his lip, a gulp per swallow, "ahh" after,
    //                               and sometimes, a little later, a burp
    //   dragging furniture          wood scraping on the floor, as fast as it moves
    //
    // Funny failure over punishment (CLAUDE.md section 4): the burp and the cough are jokes,
    // not penalties. They make no world event, so the grandmother does not hear them.
    [DisallowMultipleComponent]
    public sealed class CrewSounds : MonoBehaviour
    {
        static readonly Vector3 Mouth = new Vector3(0f, -0.1f, 0.12f);
        static readonly Vector3 Hip = new Vector3(0.22f, 0.85f, 0f);

        CrewMember member;
        PlayerGrab grab;
        Action<WorldEvent> onEvent;
        float nextGrunt;

        bool smoking;
        bool breathIn;                 // drew since the last exhale
        int inhale = -1, crackle = -1;
        float inhaleAt = -1f, fallbackExhaleAt = -1f, lastInhale = -99f, lastExhale = -99f;
        float coughAt = -1f;

        bool drinking;
        float lastSwallow;
        int swallows;
        float burpAt = -1f;

        int scrape = -1;
        Transform scraping;
        Vector3 dragAt;           // online client: the dragged body's last position
        bool hasDragAt;

        public int Grunts { get; private set; }
        public int KnockDowns { get; private set; }          // blasts that threw him (tests)
        public int SmokeSounds { get; private set; }
        public int DrinkSounds { get; private set; }
        public int PocketSounds { get; private set; }

        int Index => member != null ? member.index : 0;

        Transform Head
        {
            get
            {
                Camera view = member != null ? member.View : null;
                return view != null ? view.transform : transform;
            }
        }

        void Awake()
        {
            member = GetComponent<CrewMember>();
            grab = GetComponent<PlayerGrab>();
            onEvent = OnWorldEvent;
            VoiceBank.EnsurePlayer(Index);
        }

        void OnEnable() { WorldEvents.Subscribe(onEvent); }

        void OnDisable()
        {
            WorldEvents.Unsubscribe(onEvent);
            AudioDirector.Stop(crackle, 0f);
            AudioDirector.Stop(scrape, 0f);
            crackle = scrape = -1;
            smoking = drinking = breathIn = false;
            inhaleAt = fallbackExhaleAt = -1f;
        }

        void OnWorldEvent(WorldEvent e)
        {
            // Blown off his feet: the instigator is whoever set the blast off (himself, the other
            // player, or World), the one thrown is the subject (Explosion.KnockPlayers). So
            // this one is matched on the subject, before the "my own actions" filter below.
            if (e.type == WorldEventType.PlayerKnockedDown)
            {
                if (member != null && ReferenceEquals(e.subject, member))
                {
                    Grunt(e.magnitude > 8f ? EffortKind.Whoa : EffortKind.Oof, 1f, true);
                    KnockDowns++;
                }
                return;
            }
            if (e.instigator != Index || !Actors.IsPlayer(e.instigator)) return;
            switch (e.type)
            {
                case WorldEventType.ObjectPickedUp:
                {
                    MovableObject mo = e.Item;
                    float mass = mo != null ? mo.Mass : 5f;
                    if (mass >= 25f) Grunt(EffortKind.Lift, Mathf.Clamp(0.55f + mass / 200f, 0.55f, 1f));
                    AudioDirector.PlayOn(SfxKind.GrabCloth, Head, new Vector3(0f, -0.5f, 0.4f), SoundPreset.Handling, 0.45f, UnityEngine.Random.Range(0.95f, 1.05f));
                    break;
                }
                case WorldEventType.ObjectThrown:
                {
                    if (e.magnitude > 3.5f) Grunt(EffortKind.Throw, 0.8f);
                    MovableObject mo = e.Item;
                    if (mo != null && e.magnitude > 2.5f)
                        AudioDirector.PlayOn(SfxKind.Whoosh, mo.transform, Vector3.zero, SoundPreset.Handling,
                                             Mathf.Clamp01(e.magnitude / 9f), Mathf.Clamp(0.85f + e.magnitude * 0.03f, 0.85f, 1.2f));
                    break;
                }
                case WorldEventType.ItemPocketed:
                    AudioDirector.PlayOn(SfxKind.Rustle, transform, Hip, SoundPreset.Handling, 0.55f, UnityEngine.Random.Range(0.95f, 1.05f));
                    AudioDirector.PlayOn(SfxKind.PocketPat, transform, Hip, SoundPreset.Handling, 0.5f, 1f);
                    PocketSounds++;
                    break;
                case WorldEventType.ItemUnpocketed:
                    AudioDirector.PlayOn(SfxKind.Rustle, transform, Hip, SoundPreset.Handling, 0.5f, UnityEngine.Random.Range(1f, 1.1f));
                    PocketSounds++;
                    break;
                case WorldEventType.PlayerSmoking:
                    SmokeOut();
                    break;
                case WorldEventType.PlayerDrinking:
                    Swallow();
                    break;
            }
        }

        // An effort in his own voice. At most one every 0.35 s: grabbing three chairs in a row
        // is one grunt, not three stacked. force skips that (a blast always gets its "whoa").
        public void Grunt(EffortKind kind, float volume, bool force = false)
        {
            if (!force && Time.time < nextGrunt) return;
            AudioClip clip = VoiceBank.Effort(Index, kind);
            if (clip == null) return;
            nextGrunt = Time.time + 0.35f;
            // 0.8: a grunt at the mouth is right next to the ears; at full level it drowned the
            // room in the offline mix.
            AudioDirector.PlayClip(clip, Head, Mouth, SoundPreset.CrewVoice, volume * 0.8f, UnityEngine.Random.Range(0.97f, 1.03f), transform);
            Grunts++;
        }

        // ---- smoking
        //
        // Two versions of the cigarette exist while the characters are reworked: one raises a
        // PlayerSmoking every half second from the press, the new one (SmokeTimeline) draws for
        // half a second once the filter reaches the lips and raises PlayerSmoking as the smoke
        // leaves the mouth, every 1.67 s drag. Both say IsSmoking while the button is held. So:
        // the draw starts a moment after IsSmoking rises and again a beat after each breath out,
        // the breath out is the PlayerSmoking that comes at least 0.45 s into a draw, and a
        // release mid-draw still breathes out (from the event, or a fallback if none comes).

        void UpdateSmoking(float now)
        {
            MovableObject held = grab != null ? grab.Held : null;
            CigaretteItem cig = null;
            if (held != null) held.TryGetComponent(out cig);
            bool on = cig != null && cig.IsSmoking;
            if (on && !smoking)
            {
                smoking = true;
                inhaleAt = now + 0.2f;
                fallbackExhaleAt = -1f;
                crackle = AudioDirector.StartLoop(SfxKind.SmokeCrackleLoop, cig.transform, Vector3.zero, SoundPreset.Handling, 0.45f, 1f, 0.15f);
            }
            else if (!on && smoking)
            {
                smoking = false;
                inhaleAt = -1f;
                AudioDirector.Stop(crackle, 0.25f);
                crackle = -1;
                if (breathIn) fallbackExhaleAt = now + 0.45f;
            }
            if (inhaleAt > 0f && now >= inhaleAt)
            {
                inhaleAt = -1f;
                if (smoking) BreatheIn(now);
            }
            if (fallbackExhaleAt > 0f && now >= fallbackExhaleAt)
            {
                fallbackExhaleAt = -1f;
                if (breathIn) BreatheOut(now);
            }
        }

        void SmokeOut()
        {
            float now = Time.time;
            if (!breathIn || now - lastInhale < 0.45f || now - lastExhale < 1f) return;
            BreatheOut(now);
            if (smoking) inhaleAt = now + 1.1f;
        }

        void BreatheIn(float now)
        {
            inhale = AudioDirector.PlayOn(SfxKind.SmokeInhale, Head, Mouth, SoundPreset.Handling, 0.6f, UnityEngine.Random.Range(0.95f, 1.05f));
            lastInhale = now;
            breathIn = true;
            SmokeSounds++;
        }

        void BreatheOut(float now)
        {
            AudioDirector.Stop(inhale, 0.1f);
            inhale = -1;
            AudioDirector.PlayOn(SfxKind.SmokeExhale, Head, Mouth, SoundPreset.Handling, 0.6f, UnityEngine.Random.Range(0.92f, 1.05f));
            lastExhale = now;
            breathIn = false;
            fallbackExhaleAt = -1f;
            SmokeSounds++;
            if (UnityEngine.Random.value < 0.12f) coughAt = now + UnityEngine.Random.Range(1.2f, 2f);
        }

        // ---- drinking: a PlayerDrinking event per swallow

        void Swallow()
        {
            if (!drinking)
            {
                drinking = true;
                swallows = 0;
                AudioDirector.PlayOn(SfxKind.BottleClink, Head, Mouth, SoundPreset.Handling, 0.35f, UnityEngine.Random.Range(1f, 1.1f));
            }
            swallows++;
            lastSwallow = Time.time;
            AudioDirector.PlayOn(SfxKind.Gulp, Head, Mouth + Vector3.down * 0.08f, SoundPreset.Handling, 0.65f, UnityEngine.Random.Range(0.93f, 1.07f));
            DrinkSounds++;
        }

        void EndDrink()
        {
            drinking = false;
            if (swallows >= 2 && UnityEngine.Random.value < 0.7f) Grunt(EffortKind.Ahh, 0.7f, true);
            if (swallows >= 4 && UnityEngine.Random.value < 0.4f) burpAt = Time.time + UnityEngine.Random.Range(1.2f, 2.5f);
            swallows = 0;
        }

        void Update()
        {
            float now = Time.time;
            UpdateSmoking(now);
            if (drinking && now - lastSwallow > 1f) EndDrink();
            if (coughAt > 0f && now >= coughAt)
            {
                coughAt = -1f;
                AudioDirector.PlayOn(SfxKind.Cough, Head, Mouth, SoundPreset.CrewVoice, 0.6f, member != null && member.index == 1 ? 1.1f : 0.95f);
            }
            if (burpAt > 0f && now >= burpAt)
            {
                burpAt = -1f;
                Grunt(EffortKind.Burp, 0.9f, true);
            }
            Drag();
        }

        // Heavy furniture pulled along the floor (PlayerGrab's drag): a scrape as loud and as
        // fast as the thing is moving.
        void Drag()
        {
            MovableObject held = grab != null && grab.IsDragging ? grab.Held : null;
            Transform t = held != null ? held.transform : null;
            if (t != scraping)
            {
                AudioDirector.Stop(scrape, 0.15f);
                scrape = -1;
                scraping = t;
                hasDragAt = false;
            }
            if (held == null) return;
            Rigidbody rb = held.rb;
            float speed = 0f;
            if (Net.IsClient && rb != null && rb.isKinematic) speed = ReplicaDragSpeed(t);
            else if (rb != null)
            {
                Vector3 v = rb.linearVelocity;
                speed = new Vector2(v.x, v.z).magnitude;
            }
            if (scrape < 0 && speed > 0.15f && SfxBank.Has(SfxKind.ScrapeLoop))
                scrape = AudioDirector.StartLoop(SfxKind.ScrapeLoop, t, Vector3.zero, SoundPreset.Impact, 0f, 1f, 0.05f);
            AudioDirector.SetLoop(scrape, Mathf.Clamp01(speed / 1.5f) * 0.65f, Mathf.Clamp(0.8f + speed * 0.15f, 0.8f, 1.15f));
        }

        // Online client: a replicated body is kinematic (the host moves it), so its speed is
        // how far the transform went since the last frame.
        float ReplicaDragSpeed(Transform t)
        {
            Vector3 p = t.position;
            float dt = Time.deltaTime;
            float speed = hasDragAt && dt > 0f ? new Vector2(p.x - dragAt.x, p.z - dragAt.z).magnitude / dt : 0f;
            dragAt = p;
            hasDragAt = true;
            return speed;
        }
    }
}
