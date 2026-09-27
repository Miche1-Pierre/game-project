using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // Gives the scene's things their sound components when it loads, so no scene or prefab
    // has to be edited to be heard:
    //
    //   every crew member      CrewFootsteps, CrewSounds (also later joiners, CrewRoster.Joined)
    //   the grandmother        GrandmaVoice, GrandmaSteps, GrandmaActivityAudio
    //   the truck              TruckAudio
    //   each fireplace         FireAudio
    //   every movable          PropImpactSound (and any picked up later, ObjectPickedUp)
    //
    // Run by the AudioDirector on each scene load, after the scene's Awake and OnEnable (so the
    // second player already exists) and before its Start. The components die with the scene.
    public static class SceneAudioBinder
    {
        static Action<CrewMember> onJoined;
        static Action<WorldEvent> onEvent;
        static bool hooked;

        // When the current scene was bound: impacts wait a moment after it (things settling).
        public static float BoundAt { get; private set; } = -99f;

        // Returns whether the scene has a crew (a playable scene, as opposed to the menu).
        public static bool Bind(Scene scene)
        {
            BoundAt = Time.time;
            Surfaces.Forget();
            Hook();

            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++) Attach(crew[i]);

            var speeches = UnityEngine.Object.FindObjectsByType<GrandmaSpeech>(FindObjectsInactive.Exclude);
            for (int i = 0; i < speeches.Length; i++)
            {
                GameObject g = speeches[i].gameObject;
                Ensure<GrandmaVoice>(g);
                if (g.GetComponent<GrandmaMover>() != null) Ensure<GrandmaSteps>(g);
                if (g.GetComponent<GrandmaActivities>() != null) Ensure<GrandmaActivityAudio>(g);
            }

            var trucks = UnityEngine.Object.FindObjectsByType<TruckVehicle>(FindObjectsInactive.Exclude);
            for (int i = 0; i < trucks.Length; i++) Ensure<TruckAudio>(trucks[i].gameObject);

            var fires = UnityEngine.Object.FindObjectsByType<FireplaceFire>(FindObjectsInactive.Exclude);
            for (int i = 0; i < fires.Length; i++) Ensure<FireAudio>(fires[i].gameObject);

            var movables = UnityEngine.Object.FindObjectsByType<MovableObject>(FindObjectsInactive.Exclude);
            for (int i = 0; i < movables.Length; i++) Ensure<PropImpactSound>(movables[i].gameObject);

            return CrewRoster.Count > 0;
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            onJoined = Attach;
            onEvent = OnEvent;
            CrewRoster.Joined += onJoined;
            WorldEvents.Subscribe(onEvent);
        }

        static void Attach(CrewMember m)
        {
            if (m == null) return;
            Ensure<CrewFootsteps>(m.gameObject);
            Ensure<CrewSounds>(m.gameObject);
        }

        // Things that were not in the scene at load (a cigarette from the spawner, a bottle
        // taken out of a pocket) get their impact sound the first time someone handles them.
        static void OnEvent(WorldEvent e)
        {
            if (e.type != WorldEventType.ObjectPickedUp && e.type != WorldEventType.ItemUnpocketed) return;
            MovableObject mo = e.Item;
            if (mo != null) Ensure<PropImpactSound>(mo.gameObject);
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            if (!go.TryGetComponent(out T c)) c = go.AddComponent<T>();
            return c;
        }

        // CrewRoster and WorldEvents drop every listener when statics reset; hook again then.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hooked = false;
            onJoined = null;
            onEvent = null;
            BoundAt = -99f;
        }
    }
}
