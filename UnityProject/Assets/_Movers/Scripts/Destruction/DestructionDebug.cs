using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Movers
{
    // Destruction's debug keys (SLICE_ARCHITECTURE section 11), registered through
    // DebugCommands, so SliceDebug is the only reader of F-keys:
    //   F3        overlay: what is under the crosshair (module, material, health, state,
    //             support), destruction counters, and the support lines around it
    //   Shift+F3  collider view: the boxes of every collider near the camera, coloured by body
    //   F9        a grenade in the driven player's hands
    //   Shift+F9  an explosion at the crosshair
    //   F10       the selected damage at the crosshair (a share of the target's health)
    //   Shift+F10 cycles that share: 10, 25, 50, 100 %
    //   F11       the destruction reset
    // "The driven player" is the one the keyboard drives (F1 swaps it). Added by
    // HouseDestruction in the editor and development builds; costs nothing while the overlays
    // are off.
    [DisallowMultipleComponent]
    public sealed class DestructionDebug : MonoBehaviour
    {
        const string Owner = "DESTRUCTION";
        const float Reach = 12f;
        const float ExplosionReach = 40f;
        // ms: what a blast may cost its frame and the 5 after it (DEV 2 section 11).
        const float FrameBudgetMs = 16f;
        static readonly float[] DamageSteps = { 0.10f, 0.25f, 0.50f, 1f };

        // ImpactDamage counts the launched-debris strikes it lets through each frame (DEV 2 3.14).
        // Read by name, once, so this overlay does not depend on the counter being there: "n/a"
        // until it is.
        const string StrikesCounter = "DebrisStrikesLastFrame";
        static System.Func<int> strikesReader;
        static bool strikesLooked;

        public static bool OverlayOn { get; private set; }
        public static bool CollidersOn { get; private set; }
        public static float DamageShare => DamageSteps[damageStep];
        static int damageStep = 1;

        static Material lineMaterial;

        // What the crosshair is on, refreshed a few times a second while the overlay is on.
        Collider aimCollider;
        Vector3 aimPoint;
        int aimNode = -1;
        string panelText = "";
        float nextText;
        readonly StringBuilder sb = new StringBuilder(512);
        readonly List<int> down = new List<int>(8), up = new List<int>(8), side = new List<int>(8);
        static Collider[] nearby = new Collider[512];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OverlayOn = false;
            CollidersOn = false;
            damageStep = 1;
        }

        void OnEnable()
        {
            DebugCommands.Register(KeyCode.F3, false, "Destruction overlay", ToggleOverlay, Owner);
            DebugCommands.Register(KeyCode.F3, true, "Collider and rigidbody view", ToggleColliders, Owner);
            DebugCommands.Register(KeyCode.F9, false, "Grenade in your hands", GrenadeInHands, Owner);
            DebugCommands.Register(KeyCode.F9, true, "Explosion at the crosshair", ExplosionAtCrosshair, Owner);
            DebugCommands.Register(KeyCode.F10, false, "Damage at the crosshair", DamageAtCrosshair, Owner);
            DebugCommands.Register(KeyCode.F10, true, "Cycle damage 10/25/50/100 %", CycleDamage, Owner);
            DebugCommands.Register(KeyCode.F11, false, "Reset destruction", ResetDestruction, Owner);
            Camera.onPostRender -= DrawLines;
            Camera.onPostRender += DrawLines;
        }

        void OnDisable()
        {
            DebugCommands.Unregister(Owner);
            Camera.onPostRender -= DrawLines;
        }

        // ---- commands ----

        static void ToggleOverlay() { OverlayOn = !OverlayOn; }
        static void ToggleColliders() { CollidersOn = !CollidersOn; }

        static void CycleDamage()
        {
            damageStep = (damageStep + 1) % DamageSteps.Length;
            DebugCommands.Toast("F10 damage: " + Mathf.RoundToInt(DamageShare * 100f) + " % of the target's health");
        }

        static void GrenadeInHands()
        {
            var m = DrivenMember();
            if (m == null || m.Grab == null) { DebugCommands.Toast("No crew member to hold it"); return; }
            if (m.Grab.IsCarrying) m.Grab.Release(false);
            var g = GrenadeItem.Create(m.EyePosition + m.LookDirection * 0.8f);
            var mo = g != null ? g.GetComponent<MovableObject>() : null;
            if (mo == null || !m.Grab.Hold(mo)) DebugCommands.Toast("Grenade dropped in front of " + m.DisplayName);
        }

        static void ExplosionAtCrosshair()
        {
            var m = DrivenMember();
            if (m == null || m.View == null) return;
            Ray ray = new Ray(m.View.transform.position, m.View.transform.forward);
            Vector3 at = Physics.Raycast(ray, out RaycastHit hit, ExplosionReach, DestructionLayers.QueryMask, QueryTriggerInteraction.Ignore)
                ? hit.point + hit.normal * 0.2f
                : ray.GetPoint(5f);
            var table = DestructionMaterialTable.Current;
            Explosion.Detonate(at, table.grenadeRadius, table.grenadePower, m.index);
        }

        static void DamageAtCrosshair()
        {
            var m = DrivenMember();
            if (m == null || m.View == null) return;
            Ray ray = new Ray(m.View.transform.position, m.View.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, Reach, DestructionLayers.QueryMask, QueryTriggerInteraction.Ignore))
            {
                DebugCommands.Toast("Nothing breakable under the crosshair");
                return;
            }
            float share = DamageShare;
            DamageResult r;
            string what;
            if (DestructibleModule.TryGetChunk(hit.collider, out DestructibleChunk chunk))
            {
                r = chunk.Owner.ApplyToChunk(chunk.Index, Hit(hit, ray, share * chunk.MaxHealth, m.index));
                what = chunk.Owner.name + " chunk " + chunk.Index;
            }
            else
            {
                var d = hit.collider.GetComponentInParent<IDamageable>();
                if (d == null) { DebugCommands.Toast(hit.collider.name + " does not break"); return; }
                float max = d is DestructibleModule dm && !dm.IsFractured && dm.Spec != null ? dm.Spec.ChunkHealth : d.MaxHealth;
                r = d.ApplyDamage(Hit(hit, ray, share * max, m.index));
                what = ((Component)d).name;
            }
            StructureGraph.Current?.ResolvePending();
            DebugCommands.Toast(what + ": " + r.applied.ToString("0") + " damage, " + r.before + " > " + r.after + (r.removed ? ", removed" : ""));
        }

        static DamageEvent Hit(RaycastHit hit, Ray ray, float damage, int instigator)
        {
            return new DamageEvent(hit.point, ray.direction, damage, damage * 0.05f, 0f, DamageType.Tool, instigator);
        }

        static void ResetDestruction()
        {
            var house = HouseDestruction.Instance;
            if (house == null) { DebugCommands.Toast("No HouseDestruction in this scene"); return; }
            house.ResetDestruction();
        }

        // The crew member the keyboard drives, else the first one.
        static CrewMember DrivenMember()
        {
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != null && crew[i].Input != null && (crew[i].Input.Source is KeyboardMouseSource || crew[i].Input.Source is LocalDevicesSource)) return crew[i];
            return crew.Count > 0 ? crew[0] : null;
        }

        // ---- the overlay ----

        void Update()
        {
            if (!OverlayOn) return;
            var m = DrivenMember();
            aimCollider = null;
            aimNode = -1;
            if (m != null && m.View != null)
            {
                Ray ray = new Ray(m.View.transform.position, m.View.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, Reach, DestructionLayers.QueryMask, QueryTriggerInteraction.Ignore))
                {
                    aimCollider = hit.collider;
                    aimPoint = hit.point;
                    aimNode = NodeOf(aimCollider);
                }
            }
            if (Time.unscaledTime >= nextText)
            {
                nextText = Time.unscaledTime + 0.1f;
                panelText = BuildText();
            }
        }

        static int NodeOf(Collider c)
        {
            if (c == null) return -1;
            if (DestructibleModule.TryGetChunk(c, out DestructibleChunk chunk)) return chunk.Node;
            var module = c.GetComponentInParent<DestructibleModule>();
            if (module != null && !module.IsFractured) return module.GraphNode;
            var br = c.GetComponentInParent<Breakable>();
            if (br != null && br.GraphNode >= 0) return br.GraphNode;
            var roof = c.GetComponentInParent<RoofSection>();
            if (roof != null) return roof.GraphNode;
            return -1;
        }

        string BuildText()
        {
            sb.Length = 0;
            if (aimCollider == null) sb.Append("(nothing under the crosshair)\n");
            else DescribeTarget();

            var debris = DebrisManager.Existing;
            var graph = StructureGraph.Current;
            var table = DestructionMaterialTable.Current;
            sb.Append("\ndebris ").Append(debris != null ? debris.Count : 0).Append('/').Append(debris != null ? debris.maxPieces : 0)
              .Append(", spawned this frame ").Append(debris != null ? debris.SpawnedThisFrame : 0)
              .Append(" (last ").Append(debris != null ? debris.SpawnedLastFrame : 0).Append(')')
              .Append("\ndebris strikes last frame ").Append(DebrisStrikes()).Append('/').Append(table.debrisStrikesPerFrame)
              .Append("\nlast blast ").Append(Explosion.LastBlastMs.ToString("0.0")).Append(" ms (its frame ")
              .Append(Explosion.LastFrameBlastMs.ToString("0.0")).Append(" ms), after it ")
              .Append(Explosion.MaxFrameMsAfterBlast.ToString("0.0")).Append(" ms of ").Append(FrameBudgetMs.ToString("0"))
              .Append("\n  ").Append(BlastSolver.LastTargetCount).Append(" targets, ")
              .Append(BlastSolver.LastExpectedChunks).Append(" chunks announced, last wall swap ")
              .Append(DestructibleModule.LastFractureMs.ToString("0.0")).Append(" ms")
              .Append("\nhit feedback ").Append(ImpactFeedback.HitsLastBusyFrame).Append(" in a frame, last ")
              .Append((ImpactFeedback.LastEnergy / 1000f).ToString("0.0")).Append(" kJ");
            if (graph != null)
                sb.Append("\ngraph ").Append(graph.LiveCount).Append('/').Append(graph.NodeCount).Append(" nodes, ")
                  .Append(graph.QueuedCount).Append(" falling, ").Append(graph.SelfSupportedCount).Append(" held as built");
            sb.Append("\nbroken-up walls ").Append(DestructibleModule.FracturedCount)
              .Append("   F10 = ").Append(Mathf.RoundToInt(DamageShare * 100f)).Append(" % health");
            return sb.ToString();
        }

        void DescribeTarget()
        {
            IDamageable d;
            string state;
            if (DestructibleModule.TryGetChunk(aimCollider, out DestructibleChunk chunk))
            {
                var m = chunk.Owner;
                sb.Append("<b>").Append(m.name).Append("</b>  chunk ").Append(chunk.Index).Append('\n')
                  .Append("module ").Append(m.ModuleName).Append(" (wall, broken up)\n")
                  .Append("material ").Append(m.Material).Append("   chunk HP ").Append(chunk.Health.ToString("0")).Append('/')
                  .Append(chunk.MaxHealth.ToString("0")).Append(' ').Append(StateName(chunk.State, false, false)).Append('\n')
                  .Append("wall HP ").Append(m.Health.ToString("0")).Append('/').Append(m.MaxHealth.ToString("0")).Append(' ')
                  .Append(StateName(m.State, false, false)).Append(", ").Append(m.AttachedCount).Append('/').Append(m.Chunks.Count)
                  .Append(" chunks left\n");
                AppendSupport();
                AppendLastHit(m.LastHit, m.LastHitTime);
                return;
            }
            d = aimCollider.GetComponentInParent<IDamageable>();
            var roof = aimCollider.GetComponentInParent<RoofSection>();
            if (d == null && roof == null)
            {
                sb.Append("<b>").Append(aimCollider.name).Append("</b>  (does not break)\n");
                AppendSupport();
                return;
            }
            if (d == null)
            {
                sb.Append("<b>").Append(roof.name).Append("</b>  roof section, ").Append(roof.Mass.ToString("0")).Append(" kg")
                  .Append(roof.HasFallen ? ", fallen" : "").Append('\n');
                AppendSupport();
                return;
            }
            var comp = (Component)d;
            bool glass = d is GlassPane;
            bool foundation = d is Breakable b0 && b0.stateCap < DestructionState.Destroyed;
            state = StateName(d.State, glass, foundation);
            string module = "-", kind = glass ? "glass" : foundation ? "foundation" : "prop";
            if (d is DestructibleModule dm) { module = dm.ModuleName; kind = "wall"; }
            else if (d is Breakable br && br.structural)
            {
                var mf = br.GetComponent<MeshFilter>();
                module = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-";
                kind = foundation ? "foundation" : "element";
            }
            sb.Append("<b>").Append(comp.name).Append("</b>\n")
              .Append("module ").Append(module).Append(" (").Append(kind).Append(")\n")
              .Append("material ").Append(d.Material).Append("   HP ").Append(d.Health.ToString("0")).Append('/')
              .Append(d.MaxHealth.ToString("0")).Append("   ").Append(state).Append('\n');
            AppendSupport();
            if (d is DestructibleModule m2) AppendLastHit(m2.LastHit, m2.LastHitTime);
            else if (d is Breakable b2) AppendLastHit(b2.LastHit, b2.LastHitTime);
            else if (d is GlassPane p2) AppendLastHit(p2.LastHit, p2.LastHitTime);
        }

        void AppendSupport()
        {
            var graph = StructureGraph.Current;
            if (graph == null || !graph.IsValid(aimNode)) { sb.Append("support: not in the structure graph\n"); return; }
            if (graph.IsAnchor(aimNode))
            {
                sb.Append("support: anchor").Append(graph.IsSelfSupported(aimNode) ? " (held as built)" : "").Append('\n');
                return;
            }
            if (graph.Explain(aimNode, out int steps, out int hops))
                sb.Append("support: yes, ").Append(steps).Append(" steps to an anchor, ").Append(hops).Append(" sideways\n");
            else sb.Append(graph.IsFalling(aimNode) ? "support: none, falling\n" : "support: NONE\n");
        }

        void AppendLastHit(in DamageEvent e, float time)
        {
            if (time < 0f) { sb.Append("last hit: none\n"); return; }
            sb.Append("last hit: ").Append(e.type).Append(' ').Append(e.damage.ToString("0")).Append(" by ")
              .Append(Actors.Name(e.instigator)).Append(", ").Append((Time.time - time).ToString("0.0")).Append(" s ago\n");
        }

        static string DebrisStrikes()
        {
            if (!strikesLooked)
            {
                strikesLooked = true;
                const System.Reflection.BindingFlags Static =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                var p = typeof(ImpactDamage).GetProperty(StrikesCounter, Static);
                if (p != null && p.PropertyType == typeof(int)) strikesReader = () => (int)p.GetValue(null);
                var f = p == null ? typeof(ImpactDamage).GetField(StrikesCounter, Static) : null;
                if (f != null && f.FieldType == typeof(int)) strikesReader = () => (int)f.GetValue(null);
            }
            return strikesReader != null ? strikesReader().ToString() : "n/a";
        }

        static string StateName(DestructionState s, bool glass, bool foundation)
        {
            if (glass) return s == DestructionState.Destroyed ? "Broken" : s == DestructionState.Damaged ? "Cracked" : "Intact";
            if (s == DestructionState.Destroyed) return foundation ? "Damaged" : "Collapsed";
            return s.ToString();
        }

        void OnGUI()
        {
            if (!OverlayOn) return;
            var m = DrivenMember();
            Rect view = ViewportGUI.RectFor(m);
            int size = ViewportGUI.FontSize(view, 14);
            float w = Mathf.Min(460f, view.width * 0.45f);
            float h = (size + 5) * 16;
            // Left edge, below the contract panel, clear of the crosshair.
            var box = new Rect(view.x + 12f, view.y + view.height * 0.32f, w, h);
            ViewportGUI.Panel(box, 0.6f);
            ViewportGUI.Label(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f), panelText, size, Color.white);
        }

        // ---- lines ----

        static Material LineMaterial
        {
            get
            {
                if (lineMaterial == null)
                {
                    Shader sh = Shader.Find("Hidden/Internal-Colored");
                    if (sh == null) return null;
                    lineMaterial = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                    lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    lineMaterial.SetInt("_ZWrite", 0);
                    lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
                return lineMaterial;
            }
        }

        // Built-in pipeline: every camera calls this after it rendered. Only the crew's cameras
        // draw (not the scene view), and only while an overlay is on.
        void DrawLines(Camera cam)
        {
            if (!OverlayOn && !CollidersOn) return;
            if (cam == null || CrewRoster.Owner(cam.transform) == null) return;
            var mat = LineMaterial;
            if (mat == null) return;
            GL.PushMatrix();
            mat.SetPass(0);
            GL.Begin(GL.LINES);
            if (OverlayOn) DrawSupport();
            if (CollidersOn) DrawColliders(cam.transform.position);
            GL.End();
            GL.PopMatrix();
        }

        void DrawSupport()
        {
            var graph = StructureGraph.Current;
            if (graph == null || !graph.IsValid(aimNode)) return;
            Bounds b = graph.BoundsOf(aimNode);
            Color own = graph.IsAnchor(aimNode) ? Color.green : graph.IsSupported(aimNode) ? Color.white : Color.red;
            Box(b, own);
            graph.GetNeighbours(aimNode, down, up, side);
            Vector3 c = b.center;
            for (int i = 0; i < down.Count; i++)
            {
                int n = down[i];
                Color col = graph.IsAnchor(n) ? Color.green : graph.IsSupported(n) ? new Color(0.4f, 1f, 0.4f) : Color.red;
                Line(c, graph.BoundsOf(n).center, col);
                Box(graph.BoundsOf(n), col * 0.8f);
            }
            for (int i = 0; i < side.Count; i++)
                Line(c, graph.BoundsOf(side[i]).center, graph.IsSupported(side[i]) ? Color.yellow : Color.red);
            for (int i = 0; i < up.Count; i++)
                Line(c, graph.BoundsOf(up[i]).center, new Color(0.4f, 0.8f, 1f));
        }

        static void DrawColliders(Vector3 eye)
        {
            int n = Physics.OverlapSphereNonAlloc(eye, Reach, nearby, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = nearby[i];
                nearby[i] = null;
                if (col == null) continue;
                var rb = col.attachedRigidbody;
                Color c;
                if (rb == null) c = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                else if (rb.TryGetComponent(out DebrisPiece _)) c = rb.isKinematic ? new Color(0.6f, 0.35f, 0.1f) : new Color(1f, 0.55f, 0.1f);
                else if (rb.isKinematic) c = Color.yellow;
                else if (rb.IsSleeping()) c = new Color(0.3f, 0.5f, 1f);
                else c = Color.green;
                if (col.isTrigger) c.a = 0.25f;
                Box(col.bounds, c);
            }
        }

        static void Line(Vector3 a, Vector3 b, Color c)
        {
            GL.Color(c);
            GL.Vertex(a);
            GL.Vertex(b);
        }

        static void Box(Bounds b, Color c)
        {
            Vector3 mn = b.min, mx = b.max;
            Vector3 p000 = mn, p100 = new Vector3(mx.x, mn.y, mn.z), p010 = new Vector3(mn.x, mx.y, mn.z), p110 = new Vector3(mx.x, mx.y, mn.z);
            Vector3 p001 = new Vector3(mn.x, mn.y, mx.z), p101 = new Vector3(mx.x, mn.y, mx.z), p011 = new Vector3(mn.x, mx.y, mx.z), p111 = mx;
            Line(p000, p100, c); Line(p100, p110, c); Line(p110, p010, c); Line(p010, p000, c);
            Line(p001, p101, c); Line(p101, p111, c); Line(p111, p011, c); Line(p011, p001, c);
            Line(p000, p001, c); Line(p100, p101, c); Line(p110, p111, c); Line(p010, p011, c);
        }
    }
}
