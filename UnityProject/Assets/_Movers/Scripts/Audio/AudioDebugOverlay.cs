using System.Text;
using UnityEngine;

namespace Movers
{
    // Shift+F8: what is sounding right now. Every busy voice with its preset, volume, walls
    // and cutoff; the listener; the banks; the volumes; what each player is standing on; the
    // grandmother's voice. The answer to "why don't I hear X" should be on this screen.
    //
    // A narrow column on the left edge, kept off every view's crosshair (SLICE_ARCHITECTURE,
    // "HUD regions": debug overlays stay off Center). In side-by-side split, P1's crosshair is
    // a quarter of the way across the screen, so the column narrows to end before it, or, on
    // a screen too small for that, stops above it.
    //
    // The text is rebuilt four times a second, not every frame (a debug overlay, but still no
    // garbage per frame). The lines are short on purpose, to fit the column.
    internal sealed class AudioDebugOverlay
    {
        const float Margin = 8f;
        const float Top = 56f;
        const float Width = 300f;
        const float MinWidth = 180f;
        const float CentreKeepOut = 180f;   // the crosshair and the prompts right around it
        const int FontSize = 11;

        readonly AudioDirector director;
        readonly StringBuilder sb = new StringBuilder(2048);
        bool visible;
        string text = "";
        float nextBuild;
        GUIStyle style;

        public AudioDebugOverlay(AudioDirector director) { this.director = director; }

        public bool Visible => visible;

        public void Toggle()
        {
            visible = !visible;
            nextBuild = 0f;
            DebugCommands.Toast(visible ? "Audio overlay on" : "Audio overlay off");
        }

        public void Draw()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;
            if (Time.unscaledTime >= nextBuild)
            {
                nextBuild = Time.unscaledTime + 0.25f;
                Build();
            }
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = FontSize, richText = false, wordWrap = false, clipping = TextClipping.Clip };
                style.normal.textColor = new Color(1f, 0.95f, 0.85f);
            }
            Rect r = PanelRect();
            if (r.width < 1f || r.height < 1f) return;
            ViewportGUI.Panel(r, 0.6f);
            GUI.Label(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, r.height - 8f), text, style);
        }

        // The column, then cut back from every active view's centre it would cover. Views
        // come from the crew's cameras: with no crew (menu, loading) there is no crosshair and
        // the column keeps its full size.
        internal static Rect PanelRect()
        {
            var r = new Rect(Margin, Top, Mathf.Min(Width, Screen.width - 2f * Margin), Screen.height - Top - 6f * Margin);
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember c = crew[i];
                Camera cam = c != null ? c.View : null;
                if (cam == null || !cam.isActiveAndEnabled) continue;
                Rect keep = ViewportGUI.Region(ViewportGUI.RectFor(cam), HudRegion.Center, CentreKeepOut, CentreKeepOut);
                if (!r.Overlaps(keep)) continue;
                float w = keep.xMin - Margin - r.x;
                if (w >= MinWidth) r.width = w;
                else r.height = Mathf.Max(0f, keep.yMin - Margin - r.y);
            }
            return r;
        }

        void Build()
        {
            sb.Length = 0;
            AudioListener l = Ears.Listener;
            sb.Append("AUDIO  ears ").Append(l != null ? l.name : "NONE").Append(AudioDirector.Paused ? " (paused)" : "").Append('\n');
            sb.Append("sfx ").Append(SfxBank.LoadedCount).Append('/').Append(SfxBank.TotalCount)
              .Append("  lines ").Append(VoiceBank.CachedLines).Append('\n');
            MusicPlayer m = AudioDirector.Music;
            if (m != null) sb.Append("music ").Append(m.Wanted).Append(m.IsPlaying(m.Wanted) ? " playing" : " resting").Append("  duck ").Append(m.Duck.ToString("0.00")).Append('\n');
            sb.Append("vol  all ").Append(GameAudio.GetVolume(AudioChannel.Master).ToString("0.00"))
              .Append("  mus ").Append(GameAudio.GetVolume(AudioChannel.Music).ToString("0.00"))
              .Append("  sfx ").Append(GameAudio.GetVolume(AudioChannel.Sfx).ToString("0.00")).Append('\n');
            sb.Append("     voi ").Append(GameAudio.GetVolume(AudioChannel.Voice).ToString("0.00"))
              .Append("  amb ").Append(GameAudio.GetVolume(AudioChannel.Ambience).ToString("0.00"))
              .Append("  ui ").Append(GameAudio.GetVolume(AudioChannel.Ui).ToString("0.00")).Append('\n');
            AmbienceAudio a = AudioDirector.Ambience;
            if (a != null && a.Active) sb.Append("outside ").Append(a.Outdoor.ToString("0.00")).Append("  birds ").Append(a.BirdsPlayed).Append("  cars ").Append(a.CarsPlayed).Append('\n');

            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember c = crew[i];
                if (c == null || !c.TryGetComponent(out CrewFootsteps f)) continue;
                sb.Append(c.DisplayName).Append(" on ").Append(f.LastSurface).Append("  steps ").Append(f.StepCount);
                if (c.TryGetComponent(out CrewSounds s)) sb.Append("  grunts ").Append(s.Grunts);
                sb.Append('\n');
            }
            var voice = Object.FindAnyObjectByType<GrandmaVoice>();
            if (voice != null)
            {
                sb.Append("gran lines ").Append(voice.LinesVoiced).Append('/').Append(voice.LinesRequested)
                  .Append("  ").Append(voice.MoodNow()).Append("  hums ").Append(voice.Hums).Append('\n');
                if (voice.TryGetComponent(out GrandmaSteps gs)) sb.Append("     steps ").Append(gs.StepCount).Append(" on ").Append(gs.LastSurface).Append('\n');
            }

            sb.Append("\nsound        preset     vol  wl  cut    m\n");
            VoicePool pool = director.Pool;
            for (int i = 0; i < VoicePool.Size; i++) Line(pool.At(i));
            for (int i = 0; i < pool.AdoptedCount; i++) Line(pool.AdoptedAt(i));
            text = sb.ToString();
        }

        void Line(AudioVoice v)
        {
            if (v == null || !v.busy || v.src == null) return;
            Cell(v.Label, 12);
            Cell(v.preset.ToString(), 10);
            sb.Append(v.src.volume.ToString("0.00")).Append("  ")
              .Append(v.walls).Append("   ")
              .Append(v.lpf != null && v.lpf.enabled ? v.occlusionCutoff.ToString("00000") : "  off").Append("  ");
            if (v.settings.spatial) sb.Append((v.world - v.ear).magnitude.ToString("0.0"));
            else sb.Append("2D");
            sb.Append('\n');
        }

        void Cell(string s, int width)
        {
            if (s.Length > width) sb.Append(s, 0, width);
            else sb.Append(s).Append(' ', width - s.Length);
            sb.Append(' ');
        }
    }
}
