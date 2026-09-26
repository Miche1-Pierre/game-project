using UnityEngine;

namespace Movers
{
    // The grandmother's mind, on screen. F4: her state, patience, activity, path, vision cone,
    // hearing radius and the last noise. Shift+F4: her AI on and off (off: she stands still,
    // hears nothing and her patience is frozen). Keys go through DebugCommands (SliceDebug).
    [DisallowMultipleComponent]
    public sealed class GrandmaDebug : MonoBehaviour
    {
        const string Owner = "GRANDMA";

        public GrandmaBrain brain;
        public GrandmaMover mover;
        public GrandmaSenses senses;
        public GrandmaMood mood;
        public GrandmaActivities activities;
        public bool overlay;

        string text = "";
        float nextText;
        Material lineMaterial;

        void Awake()
        {
            useGUILayout = false;
            if (brain == null) brain = GetComponent<GrandmaBrain>();
            if (mover == null) mover = GetComponent<GrandmaMover>();
            if (senses == null) senses = GetComponent<GrandmaSenses>();
            if (mood == null) mood = GetComponent<GrandmaMood>();
            if (activities == null) activities = GetComponent<GrandmaActivities>();
        }

        void OnEnable()
        {
            DebugCommands.Register(KeyCode.F4, false, "Grandma overlay", ToggleOverlay, Owner);
            DebugCommands.Register(KeyCode.F4, true, "Grandma AI on/off", ToggleAI, Owner);
        }

        void OnDisable() { DebugCommands.Unregister(Owner); }

        void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        void ToggleOverlay() { overlay = !overlay; }

        void ToggleAI()
        {
            if (brain != null) brain.SetAIEnabled(!brain.AIEnabled);
        }

        void Update()
        {
            if (!overlay || Time.unscaledTime < nextText) return;
            nextText = Time.unscaledTime + 0.2f;
            text = Describe();
        }

        string Describe()
        {
            if (brain == null) return "no GrandmaBrain";
            var sb = new System.Text.StringBuilder(512);
            sb.Append("Grandma  ").Append(brain.State).Append(' ').Append(brain.StateTime.ToString("0.0")).Append('s');
            if (!brain.AIEnabled) sb.Append("  [AI OFF]");
            sb.Append('\n');
            if (mood != null)
            {
                sb.Append("Patience ").Append(mood.Patience.ToString("0")).Append("  ").Append(GrandmaMood.Word(mood.Tier));
                int worst = mood.WorstOffender();
                if (worst != Actors.World) sb.Append("  worst ").Append(Actors.Name(worst)).Append(' ').Append(mood.BlameOf(worst).ToString("0"));
                sb.Append('\n');
            }
            if (activities != null)
            {
                ActivitySpot spot = activities.Current != null ? activities.Current : brain.RoutineTarget;
                sb.Append("Activity ").Append(spot != null ? spot.Label + " (" + spot.kind + ")" : "-")
                  .Append(' ').Append(activities.CurrentPhase)
                  .Append("  done ").Append(activities.DoneCount).Append(", kinds ").Append(activities.DistinctKindsDone).Append('\n');
            }
            if (mover != null)
            {
                sb.Append("Move ").Append(mover.Status).Append(' ').Append(mover.CurrentSpeed.ToString("0.00")).Append(" m/s  left ")
                  .Append(mover.RemainingPathLength().ToString("0.0")).Append(" m");
                if (mover.PathIsPartial) sb.Append(" PARTIAL");
                if (mover.IsWaitingForDoor) sb.Append(" (door)");
                if (mover.SeatedBodyOn) sb.Append(" (seated)");
                sb.Append("  repaths ").Append(mover.RepathCount).Append(" (").Append(mover.RebuildRepathCount).Append(" after updates)")
                  .Append("  doors opened ").Append(mover.DoorOpenCount).Append(", stops ").Append(mover.DoorWaitCount).Append('\n');
                NavMeshBaker b = mover.Baker;
                if (b != null)
                    sb.Append("NavMesh ").Append(b.BuildCount).Append(" builds, first ").Append(b.FirstBuildMs.ToString("0"))
                      .Append(" ms, last ").Append(b.LastBuildMs.ToString("0")).Append(" ms, ").Append(b.SourceCount).Append(" sources, ")
                      .Append(b.ObstacleCount).Append(" furniture, ").Append(mover.DoorCount).Append(" doors\n");
            }
            if (brain.LastStimulusTime > 0f)
            {
                Stimulus s = brain.LastStimulus;
                sb.Append("Last ").Append(s.kind).Append(" by ").Append(Actors.Name(s.instigator)).Append(s.seen ? " (seen) " : " (heard) ")
                  .Append((Time.time - brain.LastStimulusTime).ToString("0")).Append("s ago\n");
            }
            if (senses != null)
            {
                if (senses.LastNoiseTime > 0f)
                    sb.Append("Noise ").Append(senses.LastNoiseHeard ? "heard" : "missed").Append(", reach ").Append(senses.LastNoiseRadius.ToString("0.0"))
                      .Append(" m, ").Append((Time.time - senses.LastNoiseTime).ToString("0")).Append("s ago\n");
                sb.Append("Sees");
                for (int p = 0; p < 4; p++) if (CrewRoster.Get(p) != null) sb.Append(' ').Append(Actors.Name(p)).Append(senses.Sees(p) ? ":yes" : ":no");
                sb.Append("  witnessed ").Append(senses.WitnessedCount).Append('\n');
            }
            GrandmaStats st = brain.Stats;
            sb.Append("obs ").Append(st.observations).Append("  inv ").Append(st.investigations).Append("  react ").Append(st.reactions)
              .Append("  confront ").Append(st.confrontations).Append("  thefts ").Append(st.theftsWitnessed).Append("  bumps ").Append(st.bumps);
            return sb.ToString();
        }

        void OnGUI()
        {
            if (!overlay || Event.current.type != EventType.Repaint) return;
            var crew = CrewRoster.All;
            bool any = false;
            for (int i = 0; i < crew.Count; i++)
            {
                CrewMember m = crew[i];
                if (m == null || m.View == null || !m.View.isActiveAndEnabled) continue;
                any = true;
                Draw(ViewportGUI.RectFor(m.View));
            }
            if (!any) Draw(new Rect(0f, 0f, Screen.width, Screen.height));
        }

        void Draw(Rect view)
        {
            int size = ViewportGUI.FontSize(view, 13);
            float w = Mathf.Min(460f, view.width * 0.6f);
            float h = (size + 5f) * 9f + 12f;
            Rect r = ViewportGUI.Region(view, HudRegion.TopRight, w, h);
            r.y += size + 40f;   // under the patience bar
            ViewportGUI.Panel(r, 0.6f);
            ViewportGUI.Label(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 12f), text, size, new Color(1f, 0.85f, 0.95f));
        }

        // World-space lines, drawn in every camera: her path, her cone, the last noise.
        void OnRenderObject()
        {
            if (!overlay || mover == null || senses == null) return;
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                lineMaterial.SetInt("_ZWrite", 0);
                lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            Vector3 lift = Vector3.up * 0.1f;
            if (mover.Status == GrandmaMover.MoveStatus.Moving && mover.CornerCount > 1)
            {
                GL.Color(new Color(1f, 0.9f, 0.2f, 0.9f));
                Vector3 a = transform.position + lift;
                for (int i = 1; i < mover.CornerCount; i++)
                {
                    Vector3 b = mover.Corner(i) + lift;
                    GL.Vertex(a); GL.Vertex(b);
                    a = b;
                }
            }

            bool seeing = false;
            for (int p = 0; p < 4; p++) seeing |= senses.Sees(p);
            GL.Color(seeing ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(0.3f, 1f, 0.4f, 0.7f));
            Vector3 head = senses.HeadPosition;
            Vector3 fwd = mover.Forward;
            float half = senses.visionAngle * 0.5f;
            Vector3 prev = head + Quaternion.Euler(0f, -half, 0f) * fwd * senses.visionRange;
            GL.Vertex(head); GL.Vertex(prev);
            for (int i = 1; i <= 12; i++)
            {
                Vector3 next = head + Quaternion.Euler(0f, -half + senses.visionAngle * i / 12f, 0f) * fwd * senses.visionRange;
                GL.Vertex(prev); GL.Vertex(next);
                prev = next;
            }
            GL.Vertex(head); GL.Vertex(prev);

            if (Time.time - senses.LastNoiseTime < 5f)
            {
                GL.Color(senses.LastNoiseHeard ? new Color(0.2f, 0.9f, 1f, 0.9f) : new Color(0.6f, 0.6f, 0.6f, 0.7f));
                Vector3 c = senses.LastNoisePosition;
                float r = senses.LastNoiseRadius;
                Vector3 last = c + new Vector3(r, 0f, 0f);
                for (int i = 1; i <= 32; i++)
                {
                    float t = i / 32f * Mathf.PI * 2f;
                    Vector3 p = c + new Vector3(Mathf.Cos(t) * r, 0f, Mathf.Sin(t) * r);
                    GL.Vertex(last); GL.Vertex(p);
                    last = p;
                }
                GL.Vertex(c + Vector3.left * 0.3f); GL.Vertex(c + Vector3.right * 0.3f);
                GL.Vertex(c + Vector3.back * 0.3f); GL.Vertex(c + Vector3.forward * 0.3f);
                GL.Vertex(c); GL.Vertex(c + Vector3.up * 1f);
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
