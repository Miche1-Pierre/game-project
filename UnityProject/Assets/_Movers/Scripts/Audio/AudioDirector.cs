using Movers.AudioSynth;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // The one place the game's sounds go through (the destruction's own pool included: it hands
    // its voices over with Adopt). It lives for the whole Play session, from the first scene to
    // the last, and creates itself: no scene has to contain it, so the menu, the house, the
    // tutorial and a scene started straight from the editor all sound the same.
    //
    // It owns:
    //   - the voices (VoicePool): budgets per channel, placement from the nearest player's head
    //     (Ears), walls (Occlusion), fades, and the volume sliders (GameAudio) every frame;
    //   - pause: timeScale 0 pauses the house (AudioListener.pause); the interface and the
    //     music keep playing (ignoreListenerPause);
    //   - the banks' uploads (SfxBank, VoiceBank), the music (MusicPlayer), the ambience
    //     (AmbienceAudio), the sounds of world events (WorldSoundEvents), and, when a scene
    //     loads, the sound components of its players, grandmother, truck, fires and movables
    //     (SceneAudioBinder);
    //   - Shift+F8, the audio overlay (AudioDebugOverlay). Not Shift+F7: the indicators
    //     (INDICATORS, CrewIndicators) held it in the same wave, and two owners of a key
    //     steal it from each other on every boot and reload. Both tracks moved (the
    //     indicators to Shift+F2); Shift+F8 is AUDIO's only key.
    //
    // Everything else calls the static API below with a SoundPreset and gets a handle back (-1
    // when nothing was played: the clip is still rendering, or every voice it could take is
    // more important).
    //
    // Runs late (900): after everything has moved this frame, before CameraShake (1000).
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        const string DebugOwner = "AUDIO";

        VoicePool pool;
        bool paused;
        Scene boundScene;
        bool bound;

        MusicPlayer music;
        AmbienceAudio ambience;
        WorldSoundEvents worldSounds;
        AudioDebugOverlay overlay;

        static bool quitting;

        // For the tests and the overlay.
        static readonly int[] playsPerKind = new int[(int)SfxKind.Count];
        static readonly float[] lastPlayPerKind = new float[(int)SfxKind.Count];
        static readonly int[] playsPerPreset = new int[(int)SoundPreset.Count];
        public static int PlaysOf(SfxKind kind) => playsPerKind[(int)kind];
        public static float LastPlayOf(SfxKind kind) => lastPlayPerKind[(int)kind];
        public static int PlaysOf(SoundPreset preset) => playsPerPreset[(int)preset];
        public static MusicPlayer Music => Instance != null ? Instance.music : null;
        public static AmbienceAudio Ambience => Instance != null ? Instance.ambience : null;
        public static WorldSoundEvents WorldSounds => Instance != null ? Instance.worldSounds : null;
        public static bool Paused => Instance != null && Instance.paused;

        // ---------------------------------------------------------------- start

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isPlaying) Ensure();
        }

        public static AudioDirector Ensure()
        {
            if (Instance != null) return Instance;
            if (quitting || !Application.isPlaying) return null;
            var go = new GameObject("AudioDirector");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioDirector>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            useGUILayout = false;
            pool = new VoicePool(transform);
            SfxBank.EnsureStarted();
            music = new MusicPlayer(transform);
            ambience = new AmbienceAudio();
            worldSounds = new WorldSoundEvents();
            overlay = new AudioDebugOverlay(this);
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        void OnEnable()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            worldSounds.Enable();
            DebugCommands.Register(KeyCode.F8, true, "audio overlay (voices, walls, surfaces)", overlay.Toggle, DebugOwner);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            worldSounds.Disable();
            DebugCommands.Unregister(DebugOwner);
        }

        // The first scene loaded before this existed; later ones come through sceneLoaded.
        void Start()
        {
            Bind(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (paused) AudioListener.pause = false;
            music?.Dispose();
            Application.quitting -= OnQuitting;
        }

        static void OnQuitting() { quitting = true; }

        // Additive scenes are not used; a single load is a new place with its own sounds.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Bind(scene);
        }

        void Bind(Scene scene)
        {
            if (!scene.IsValid() || (bound && scene == boundScene)) return;
            boundScene = scene;
            bound = true;
            // Whatever was playing belonged to the scene that is gone (a loop on her TV, the
            // engine): stop it. The music is the MusicPlayer's and carries on.
            pool.StopAllWorld();
            bool hasCrew = SceneAudioBinder.Bind(scene);
            music.OnScene(scene, hasCrew);
            ambience.OnScene(scene, hasCrew);
            worldSounds.OnScene();
        }

        // ---------------------------------------------------------------- public API

        // A one-shot at a point in the world.
        public static int PlayAt(SfxKind kind, Vector3 position, SoundPreset preset, float volume = 1f, float pitch = 1f)
        {
            var d = Ensure();
            if (d == null || !Finite(position)) return -1;
            return d.Begin(SfxBank.Pick(kind), preset, null, position, volume, pitch, false, (int)kind, null);
        }

        // A one-shot that moves with something (a voice at a mouth, a whoosh with a thrown
        // chair). offset is in the transform's local space.
        public static int PlayOn(SfxKind kind, Transform follow, Vector3 localOffset, SoundPreset preset, float volume = 1f, float pitch = 1f)
        {
            var d = Ensure();
            if (d == null || follow == null) return -1;
            return d.Begin(SfxBank.Pick(kind), preset, follow, localOffset, volume, pitch, false, (int)kind, follow);
        }

        public static int Play2D(SfxKind kind, SoundPreset preset, float volume = 1f, float pitch = 1f)
        {
            var d = Ensure();
            if (d == null) return -1;
            return d.Begin(SfxBank.Pick(kind), preset, null, Vector3.zero, volume, pitch, false, (int)kind, null);
        }

        // A clip of our own (a line of hers, an effort). follow may be null for a fixed point
        // (offset is then the world position) or for a 2D preset.
        public static int PlayClip(AudioClip clip, Transform follow, Vector3 offset, SoundPreset preset, float volume = 1f,
                                   float pitch = 1f, Transform ignoreForWalls = null)
        {
            var d = Ensure();
            if (d == null || clip == null) return -1;
            return d.Begin(clip, preset, follow, offset, volume, pitch, false, -1, ignoreForWalls != null ? ignoreForWalls : follow);
        }

        // A loop, until Stop. Fades in over fadeIn seconds. follow may be null (offset is then a
        // world position, or unused for a 2D preset).
        public static int StartLoop(SfxKind kind, Transform follow, Vector3 offset, SoundPreset preset, float volume = 1f,
                                    float pitch = 1f, float fadeIn = 0.2f)
        {
            var d = Ensure();
            if (d == null) return -1;
            int h = d.Begin(SfxBank.Pick(kind), preset, follow, offset, volume, pitch, true, (int)kind, follow);
            d.pool.FadeIn(h, fadeIn);
            return h;
        }

        public static void SetLoop(int handle, float volume, float pitch)
        {
            AudioVoice v = Live(handle);
            if (v == null) return;
            v.baseVolume = Mathf.Max(0f, volume);
            v.src.pitch = pitch;
        }

        public static void Stop(int handle, float fadeSeconds = 0.08f)
        {
            AudioVoice v = Live(handle);
            if (v != null) Instance.pool.Stop(v, fadeSeconds);
        }

        public static bool IsPlaying(int handle) => Live(handle) != null;

        public static bool TryGetSource(int handle, out AudioSource source)
        {
            AudioVoice v = Live(handle);
            source = v != null ? v.src : null;
            return source != null;
        }

        // How many voices of a preset are sounding now (not fading out).
        public static int ActiveCount(SoundPreset preset) => Instance != null ? Instance.pool.CountActive(preset) : 0;

        public static int ActiveVoices => Instance != null ? Instance.pool.CountBusy() : 0;

        // ImpactAudio sources currently handed over (Adopt) and sounding.
        public static int AdoptedVoices => Instance != null ? Instance.pool.CountAdopted() : 0;

        // The volume an adopted source plays at now, or -1 when it is not adopted (tests).
        public static float AdoptedVolume(AudioSource source)
        {
            var d = Instance;
            return d != null && source != null && d.pool.IsAdopted(source) ? source.volume : -1f;
        }

        // For a system with its own AudioSources (ImpactAudio): call right after Play. The
        // source is then heard from the nearest player, muffled by walls and scaled by the
        // channel's volume like everything else, until it stops. Its owner keeps playing it,
        // positioning it at the next Play, and setting its volume, which is read here as the
        // base.
        public static void Adopt(AudioSource source, Vector3 position, AudioChannel channel)
        {
            var d = Ensure();
            if (d == null || source == null || !Finite(position)) return;
            d.pool.Adopt(source, position, channel);
        }

        int Begin(AudioClip clip, SoundPreset preset, Transform follow, Vector3 offset, float volume, float pitch,
                  bool loop, int kind, Transform ignore)
        {
            if (quitting) return -1;
            int h = pool.Begin(clip, preset, follow, offset, volume, pitch, loop, kind, ignore);
            if (h < 0) return h;
            if (kind >= 0 && kind < playsPerKind.Length)
            {
                playsPerKind[kind]++;
                lastPlayPerKind[kind] = Time.unscaledTime;
            }
            playsPerPreset[(int)preset]++;
            return h;
        }

        static AudioVoice Live(int handle) => Instance != null ? Instance.pool.Live(handle) : null;

        // ---------------------------------------------------------------- every frame

        void Update()
        {
            // Turn finished renders into clips before anything asks for them this frame.
            SfxBank.Pump(SfxBank.AllLoaded ? 0 : 6);
            VoiceBank.Pump(6);
            music.Update();
            ambience.Update();
            worldSounds.Update();
        }

        void LateUpdate()
        {
            Ears.Refresh();
            UpdatePause();
            pool.Update(paused);
        }

        // The game paused (timeScale 0, the pause menu): the house goes quiet, the menu and
        // its music do not. Edge-triggered, so another system's own use of the pause is left
        // alone the rest of the time.
        void UpdatePause()
        {
            bool now = Time.timeScale < 0.0001f;
            if (now == paused) return;
            paused = now;
            AudioListener.pause = now;
        }

        // ---------------------------------------------------------------- the overlay's view

        internal VoicePool Pool => pool;

        void OnGUI()
        {
            overlay.Draw();
        }

        static bool Finite(Vector3 p)
        {
            float s = p.x + p.y + p.z;
            return !float.IsNaN(s) && !float.IsInfinity(s);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            quitting = false;
            System.Array.Clear(playsPerKind, 0, playsPerKind.Length);
            System.Array.Clear(lastPlayPerKind, 0, lastPlayPerKind.Length);
            System.Array.Clear(playsPerPreset, 0, playsPerPreset.Length);
        }
    }
}
