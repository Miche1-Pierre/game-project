using UnityEngine;

namespace Movers
{
    // Her voice: a speech bubble over her head in every player's view, and a mumble of
    // procedural blips (Animal Crossing style) that carries in 3D. Short lines only; a
    // spectator reads them in a glance.
    //
    // The blips play on her own AudioSource and never through ImpactAudio, which would turn
    // each syllable into a LoudNoise that she would then hear herself.
    [DisallowMultipleComponent]
    public sealed class GrandmaSpeech : MonoBehaviour
    {
        public bool french = false;
        [Tooltip("Players further than this do not get her bubble, walls or not.")]
        public float bubbleRange = 16f;
        public float headHeight = 1.95f;
        public int fontSize = 18;

        [Header("How long a line stays up")]
        public float minSeconds = 1.6f;
        public float secondsPerChar = 0.055f;
        public float maxSeconds = 5f;

        [Header("Voice")]
        public AudioSource voice;
        [Range(0f, 1f)] public float volume = 0.55f;
        public float basePitch = 1f;
        // Raised by the brain as she gets angrier: the same voice, higher and sharper.
        [System.NonSerialized] public float pitchBoost;

        string text;
        float until = -1f;
        float nextBlip;
        int blipsLeft;
        AudioClip blip;
        readonly int[] lastVariant = new int[(int)Line.Count];
        GUIStyle style;
        readonly GUIContent content = new GUIContent();

        public bool IsSpeaking => Time.time < until;
        public string Text => IsSpeaking ? text : null;
        public float SpeakingUntil => until;

        void Awake()
        {
            useGUILayout = false;
            if (voice == null) voice = GetComponent<AudioSource>();
            if (voice == null) voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 2f;
            voice.maxDistance = 25f;
            voice.dopplerLevel = 0f;
            blip = MakeBlip();
        }

        void OnDestroy()
        {
            if (blip != null) Destroy(blip);
        }

        // Says one variant of the line (never the same one twice in a row) and returns how
        // long it stays up, in seconds.
        public float Say(Line line, string arg = null)
        {
            string[] variants = GrandmaLines.Variants(line, french);
            if (variants == null || variants.Length == 0) return 0f;
            int i = 0;
            if (variants.Length > 1)
            {
                i = Random.Range(0, variants.Length - 1);
                if (i >= lastVariant[(int)line]) i++;
            }
            lastVariant[(int)line] = i;
            if (Net.IsHost) GrandmaSync.SendSpeech(line, i, arg);
            string s = variants[i];
            if (s.Contains("{0}")) s = string.Format(s, string.IsNullOrEmpty(arg) ? (french ? "truc" : "thing") : arg);
            return SayText(s);
        }

        public float SayText(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            text = s;
            float seconds = Mathf.Clamp(minSeconds + s.Length * secondsPerChar, minSeconds, maxSeconds);
            until = Time.time + seconds;
            blipsLeft = Mathf.Clamp(s.Length / 3, 2, 24);
            nextBlip = Time.time;
            return seconds;
        }

        // Online client (GrandmaSync Speech): the variant the host picked, in this machine's
        // language. No Random. secondsLeft >= 0 (the snapshot) keeps the host's remaining time.
        public float SayVariant(Line line, int variant, string arg, float secondsLeft = -1f)
        {
            string[] variants = GrandmaLines.Variants(line, french);
            if (variants == null || variants.Length == 0) return 0f;
            int i = Mathf.Clamp(variant, 0, variants.Length - 1);
            lastVariant[(int)line] = i;
            string s = variants[i];
            if (s.Contains("{0}")) s = string.Format(s, string.IsNullOrEmpty(arg) ? (french ? "truc" : "thing") : arg);
            float seconds = SayText(s);
            if (secondsLeft < 0f || seconds <= 0f) return seconds;
            until = Time.time + secondsLeft;
            return secondsLeft;
        }

        public void Hush()
        {
            if (Net.IsHost) GrandmaSync.SendHush();
            until = -1f;
            blipsLeft = 0;
        }

        void Update()
        {
            if (blipsLeft <= 0 || Time.time < nextBlip) return;
            if (voice != null && voice.isActiveAndEnabled && blip != null)
            {
                voice.pitch = basePitch + pitchBoost + Random.Range(-0.15f, 0.25f);
                voice.PlayOneShot(blip, volume);
            }
            blipsLeft--;
            nextBlip = Time.time + Random.Range(0.08f, 0.13f);
        }

        // One short syllable: a warm tone with a buzzy edge, fast attack, quick decay.
        static AudioClip MakeBlip()
        {
            const int rate = 22050;
            int n = (int)(rate * 0.075f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float attack = Mathf.Min(1f, i / (rate * 0.006f));
                float env = attack * Mathf.Exp(-t * 28f);
                float w = 0.6f * Mathf.Sin(2f * Mathf.PI * 210f * t)
                        + 0.3f * Mathf.Sin(2f * Mathf.PI * 420f * t)
                        + 0.12f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 630f * t));
                data[i] = w * env * 0.5f;
            }
            var clip = AudioClip.Create("GrandmaBlip", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnGUI()
        {
            if (!HudMode.UseLegacy) return;   // the LumaFlow HUD (HudRoot) draws this now
            if (!IsSpeaking || Event.current.type != EventType.Repaint) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                style.normal.textColor = Color.white;
            }

            Vector3 head = transform.position + Vector3.up * headHeight;
            var crew = CrewRoster.All;
            bool any = false;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m == null || m.View == null || !m.View.isActiveAndEnabled) continue;
                any = true;
                DrawBubble(m.View, head);
            }
            if (!any && Camera.main != null) DrawBubble(Camera.main, head);
        }

        void DrawBubble(Camera cam, Vector3 head)
        {
            if ((cam.transform.position - head).sqrMagnitude > bubbleRange * bubbleRange) return;
            if (!ViewportGUI.WorldToGUI(cam, head, out Vector2 gui)) return;

            Rect view = ViewportGUI.RectFor(cam);
            int size = ViewportGUI.FontSize(view, fontSize);
            style.fontSize = size;
            float w = Mathf.Min(view.width * 0.42f, size * 17f);
            content.text = text;
            float h = style.CalcHeight(content, w - 12f) + 10f;
            float x = Mathf.Clamp(gui.x - w * 0.5f, view.x + 4f, view.xMax - w - 4f);
            float y = Mathf.Clamp(gui.y - h - 6f, view.y + 4f, view.yMax - h - 4f);
            var r = new Rect(x, y, w, h);
            ViewportGUI.Panel(r, 0.7f);
            GUI.Label(new Rect(r.x + 6f, r.y + 5f, r.width - 12f, r.height - 10f), content, style);
        }
    }
}
