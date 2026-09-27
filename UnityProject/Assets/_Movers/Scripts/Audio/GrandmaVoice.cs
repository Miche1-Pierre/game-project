using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // The grandmother's voice. Added next to GrandmaSpeech at runtime (SceneAudioBinder).
    //
    // GrandmaSpeech decides what she says and shows the bubble; this makes it heard. Every
    // time a new line goes up (SpeakingUntil changes), the written line is turned into babble,
    // one sung syllable per written one, in her old warm voice, coloured by her mood: sweet and
    // lilting when calm, clipped when annoyed, high and fast when angry, breathless on the
    // phone to the police (VoiceSynth, Babble). Rendered on a worker the first time, cached
    // after; it starts a frame or two after the bubble. Hush stops it.
    //
    // The old procedural blip is switched off by taking GrandmaSpeech's AudioSource away from
    // it (it only plays through that field): no change to NPC code.
    //
    // Between lines she makes small noises: a hum while busy and content, a sigh when she sits
    // down, a "hmm" when something catches her attention.
    [DisallowMultipleComponent]
    public sealed class GrandmaVoice : MonoBehaviour
    {
        // Her voice is the loudest thing in the house on purpose (it is how she is read), with
        // headroom: at 1.0 a line from 2 m reached full scale in the offline mix (previews/demo).
        [Range(0f, 1f)] public float lineVolume = 0.7f;
        [Range(0f, 1f)] public float humVolume = 0.4f;

        static readonly Vector3 MouthOffset = new Vector3(0f, 1.48f, 0f);

        GrandmaSpeech speech;
        GrandmaBrain brain;
        GrandmaMood mood;
        GrandmaActivities activities;
        Transform head;

        float seenUntil = float.NaN;
        string pendingKey;
        float pendingUntil;
        int line = -1, hum = -1;
        float nextHum;
        GrandmaState lastState;
        GrandmaActivities.Phase lastPhase;

        public int LinesRequested { get; private set; }
        public int LinesVoiced { get; private set; }
        public int Hums { get; private set; }
        public string LastLine { get; private set; }
        public VoiceMood LastMood { get; private set; }
        public int LineHandle => line;
        public bool IsVoicing => AudioDirector.IsPlaying(line);

        void Awake()
        {
            speech = GetComponent<GrandmaSpeech>();
            brain = GetComponent<GrandmaBrain>();
            mood = GetComponent<GrandmaMood>();
            activities = GetComponent<GrandmaActivities>();
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
            // Silence the blip: GrandmaSpeech plays it only through this field.
            if (speech != null) speech.voice = null;
            VoiceBank.EnsureGrandma();
            nextHum = Time.time + Random.Range(10f, 20f);
        }

        void Start()
        {
            Prewarm();
        }

        // Her first lines are rendered before she says them, so the greeting is not late.
        void Prewarm()
        {
            if (speech == null) return;
            Prewarm(Line.Greeting);
            Prewarm(Line.Intro1);
            Prewarm(Line.Intro2);
            Prewarm(Line.HouseRule);
            Prewarm(Line.HereAreTheKeys);
            Prewarm(Line.OffYouGo);
        }

        void Prewarm(Line l)
        {
            string[] variants = GrandmaLines.Variants(l, speech.french);
            if (variants == null) return;
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i].Contains("{0}")) continue;
                string key = VoiceBank.LineKey(variants[i], VoiceMood.Calm, speech.french);
                VoiceBank.RequestLine(key, variants[i], VoiceMood.Calm, speech.french);
            }
        }

        void Update()
        {
            if (speech == null) return;
            float until = speech.SpeakingUntil;
            if (until != seenUntil)
            {
                seenUntil = until;
                AudioDirector.Stop(line, 0.06f);
                line = -1;
                pendingKey = null;
                if (speech.IsSpeaking) BeginLine(speech.Text, until);
            }
            if (pendingKey != null) TryStartLine();
            SmallNoises();
        }

        void BeginLine(string text, float until)
        {
            if (string.IsNullOrEmpty(text)) return;
            AudioDirector.Stop(hum, 0.15f);
            hum = -1;
            VoiceMood m = MoodNow();
            string key = VoiceBank.LineKey(text, m, speech.french);
            VoiceBank.RequestLine(key, text, m, speech.french);
            pendingKey = key;
            pendingUntil = until;
            LastLine = text;
            LastMood = m;
            LinesRequested++;
            TryStartLine();
        }

        void TryStartLine()
        {
            // Too late to start: the bubble is almost gone.
            if (!speech.IsSpeaking || Time.time > pendingUntil - 0.3f)
            {
                pendingKey = null;
                return;
            }
            if (!VoiceBank.TryGetLine(pendingKey, out AudioClip clip)) return;
            pendingKey = null;
            Transform at = head != null ? head : transform;
            Vector3 offset = head != null ? Vector3.zero : MouthOffset;
            line = AudioDirector.PlayClip(clip, at, offset, SoundPreset.GrandmaVoice, lineVolume, 1f, transform);
            if (line >= 0) LinesVoiced++;
        }

        public VoiceMood MoodNow()
        {
            if (brain != null && (brain.State == GrandmaState.CallPolice || brain.PoliceCalled)) return VoiceMood.Police;
            MoodTier tier = mood != null ? mood.Tier : MoodTier.Sweet;
            switch (tier)
            {
                case MoodTier.Police: return VoiceMood.Police;
                case MoodTier.Angry:
                case MoodTier.Furious: return VoiceMood.Angry;
                case MoodTier.Annoyed: return VoiceMood.Annoyed;
                default:
                    bool upset = brain != null && (brain.State == GrandmaState.React || brain.State == GrandmaState.Confront);
                    return upset ? VoiceMood.Annoyed : VoiceMood.Calm;
            }
        }

        void SmallNoises()
        {
            float now = Time.time;
            GrandmaState state = brain != null ? brain.State : GrandmaState.Routine;
            GrandmaActivities.Phase phase = activities != null ? activities.CurrentPhase : GrandmaActivities.Phase.None;
            bool talking = speech.IsSpeaking || AudioDirector.IsPlaying(line);

            // Something caught her attention.
            if (state != lastState && state == GrandmaState.Observe && !talking && Random.value < 0.5f)
                Noise(EffortKind.Hmm, 0.7f);
            // She sits down with a sigh.
            if (phase != lastPhase && phase == GrandmaActivities.Phase.Entering && activities.Current != null && Seated(activities.Current.kind) &&
                !talking && Random.value < 0.6f)
                Noise(EffortKind.Sigh, 0.55f);
            lastState = state;
            lastPhase = phase;

            // Content and busy: a little tune under her breath.
            bool content = mood == null || mood.Tier <= MoodTier.Annoyed;
            if (phase == GrandmaActivities.Phase.Looping && state == GrandmaState.PerformActivity && content && !talking &&
                now >= nextHum && !AudioDirector.IsPlaying(hum))
            {
                nextHum = now + Random.Range(16f, 32f);
                AudioClip clip = VoiceBank.GrandmaHum();
                if (clip != null)
                {
                    hum = AudioDirector.PlayClip(clip, head != null ? head : transform, head != null ? Vector3.zero : MouthOffset,
                                                 SoundPreset.GrandmaVoice, humVolume, 1f, transform);
                    Hums++;
                }
            }
        }

        void Noise(EffortKind kind, float volume)
        {
            AudioClip clip = VoiceBank.GrandmaEffort(kind);
            if (clip == null) return;
            AudioDirector.PlayClip(clip, head != null ? head : transform, head != null ? Vector3.zero : MouthOffset,
                                   SoundPreset.GrandmaVoice, volume, 1f, transform);
        }

        static bool Seated(ActivityKind k) =>
            k == ActivityKind.SitRockingChair || k == ActivityKind.ReadBook || k == ActivityKind.WatchTV;

        void OnDisable()
        {
            AudioDirector.Stop(line, 0.05f);
            AudioDirector.Stop(hum, 0.05f);
            line = hum = -1;
        }
    }
}
