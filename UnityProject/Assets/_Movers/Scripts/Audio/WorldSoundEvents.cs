using System;
using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // Sounds for facts on the WorldEvents bus that no one component owns. The AudioDirector
    // keeps one; it listens for the whole session.
    //
    //   DoorOpened        the latch and the hinge: each door creaks in its own voice (one of
    //                     four, chosen by the door), so the crew learns which door that was
    //   DoorClosed        the hinge again, higher, and the latch (the slam itself is
    //                     HingedPanel's, through ImpactAudio)
    //   Window...         the sash sliding
    //   DoorUnlocked      the key turning in the lock
    //   KeysHandedOver    the bunch of keys changing hands
    //   TheftWitnessed    "uh-oh": two pizzicato notes, for everyone, at once
    //   GrandmaCalledPolice  she dials; a few seconds later, far off, a siren that grows
    //   ContractDelivered the till
    //   SessionState      the end of the run: a jingle for a delivery, a sad trombone for a
    //                     failure (funny failure, CLAUDE.md section 4), and the music steps out
    public sealed class WorldSoundEvents
    {
        readonly Action<WorldEvent> onEvent;
        bool enabled;
        UnityEngine.Object lastDoor;
        float lastDoorTime;
        int siren = -1;
        float sirenAt = -1f, sirenStart;
        float failJingleAt = -1f;

        public int DoorSounds { get; private set; }
        public int Stingers { get; private set; }

        public WorldSoundEvents()
        {
            onEvent = OnEvent;
        }

        public void Enable()
        {
            if (enabled) return;
            enabled = true;
            WorldEvents.Subscribe(onEvent);
        }

        public void Disable()
        {
            if (!enabled) return;
            enabled = false;
            WorldEvents.Unsubscribe(onEvent);
        }

        // A new scene: whatever was pending belonged to the old one. (The subscription stays:
        // the bus lives for the whole session.)
        public void OnScene()
        {
            siren = -1;
            sirenAt = -1f;
            failJingleAt = -1f;
            lastDoor = null;
        }

        public void Update()
        {
            float now = Time.unscaledTime;
            if (sirenAt > 0f && now >= sirenAt)
            {
                sirenAt = -1f;
                siren = AudioDirector.StartLoop(SfxKind.SirenLoop, null, Vector3.zero, SoundPreset.AmbienceBed, 0.05f, 1f, 1f);
            }
            // Far off and coming closer: the siren grows over twenty seconds.
            if (siren >= 0 && AudioDirector.IsPlaying(siren))
            {
                float t = Mathf.Clamp01((now - sirenStart) / 20f);
                AudioDirector.SetLoop(siren, Mathf.Lerp(0.05f, 0.45f, t), Mathf.Lerp(0.97f, 1.02f, t));
            }
            if (failJingleAt > 0f && now >= failJingleAt)
            {
                failJingleAt = -1f;
                AudioDirector.Play2D(SfxKind.JingleFail, SoundPreset.Stinger, 0.8f);
                Stingers++;
            }
        }

        void OnEvent(WorldEvent e)
        {
            switch (e.type)
            {
                case WorldEventType.DoorOpened:
                {
                    // One sound per door per moment: a double door opening is one creak.
                    float now = Time.time;
                    if (e.subject != null && e.subject == lastDoor && now - lastDoorTime < 0.3f) break;
                    lastDoor = e.subject;
                    lastDoorTime = now;
                    AudioDirector.PlayAt(SfxKind.LatchClick, e.position, SoundPreset.Door, 0.55f, UnityEngine.Random.Range(0.95f, 1.05f));
                    int voice = e.subject != null ? (e.subject.GetHashCode() & 0x7fffffff) % 4 : UnityEngine.Random.Range(0, 4);
                    AudioClip creak = SfxBank.Get(SfxKind.DoorCreak, voice);
                    if (creak != null)
                        AudioDirector.PlayClip(creak, null, e.position + Vector3.up * 0.3f, SoundPreset.Door, 0.6f, UnityEngine.Random.Range(0.96f, 1.04f));
                    DoorSounds++;
                    break;
                }
                case WorldEventType.DoorClosed:
                {
                    AudioDirector.PlayAt(SfxKind.LatchClick, e.position, SoundPreset.Door, 0.5f, UnityEngine.Random.Range(0.9f, 1f));
                    // The same hinge, swinging the other way: a little higher, a little shorter.
                    int voice = e.subject != null ? (e.subject.GetHashCode() & 0x7fffffff) % 4 : UnityEngine.Random.Range(0, 4);
                    AudioClip creak = SfxBank.Get(SfxKind.DoorCreak, voice);
                    if (creak != null)
                        AudioDirector.PlayClip(creak, null, e.position + Vector3.up * 0.3f, SoundPreset.Door, 0.4f, UnityEngine.Random.Range(1.08f, 1.15f));
                    DoorSounds++;
                    break;
                }
                case WorldEventType.WindowOpened:
                case WorldEventType.WindowClosed:
                    AudioDirector.PlayAt(SfxKind.SashSlide, e.position, SoundPreset.Door, 0.6f,
                                         e.type == WorldEventType.WindowOpened ? UnityEngine.Random.Range(1f, 1.08f) : UnityEngine.Random.Range(0.9f, 0.98f));
                    DoorSounds++;
                    break;
                case WorldEventType.DoorUnlocked:
                    AudioDirector.PlayAt(SfxKind.LockClack, e.position, SoundPreset.Door, 0.6f, 1f);
                    DoorSounds++;
                    break;
                case WorldEventType.KeysHandedOver:
                    AudioDirector.PlayAt(SfxKind.KeyJingle, e.position + Vector3.up * 1.1f, SoundPreset.Handling, 0.8f, 1f);
                    break;
                case WorldEventType.TheftWitnessed:
                    AudioDirector.Play2D(SfxKind.StingTheftSeen, SoundPreset.Stinger, 0.7f);
                    Stingers++;
                    break;
                case WorldEventType.GrandmaCalledPolice:
                    AudioDirector.PlayAt(SfxKind.PhoneDial, e.position + Vector3.up * 1.4f, SoundPreset.Door, 0.8f, 1f);
                    AudioDirector.Music?.Silence(1.5f);
                    sirenAt = Time.unscaledTime + 4f;
                    sirenStart = sirenAt;
                    break;
                case WorldEventType.ContractDelivered:
                    AudioDirector.Play2D(SfxKind.JingleDelivered, SoundPreset.Stinger, 0.75f);
                    Stingers++;
                    break;
                case WorldEventType.SessionStateChanged:
                    OnSession((SessionState)Mathf.RoundToInt(e.magnitude));
                    break;
            }
        }

        void OnSession(SessionState state)
        {
            if (state == SessionState.Completed)
            {
                AudioDirector.Music?.Silence(1f);
                AudioDirector.Play2D(SfxKind.JingleWin, SoundPreset.Stinger, 0.8f);
                Stingers++;
            }
            else if (state == SessionState.Failed)
            {
                AudioDirector.Music?.Silence(1f);
                // After the police siren has had a moment, or at once when time simply ran out.
                failJingleAt = Time.unscaledTime + (Session.Failure == FailReason.PoliceCalled ? 2.5f : 0.3f);
            }
        }
    }
}
