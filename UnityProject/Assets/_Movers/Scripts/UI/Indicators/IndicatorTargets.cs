using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // Something worth pointing at, and how it looks this frame. The look is the same for every
    // viewer (IndicatorTargets.Refresh); what each view does with it is CrewIndicatorView's.
    public sealed class IndicatorTarget
    {
        public IndicatorKind kind;
        public Transform root;              // what the package's compass tracks (only its heading matters)
        public CrewMember member;           // Partner
        public GrandmaBrain grandma;        // Grandma
        public DeliverPoint board;          // Deliver
        public EscapeCheckpoint exit;       // Exit
        public PoliceCar car;               // Police

        // This frame:
        public bool alive;
        public Vector3 anchor;              // where a ring hangs: over the head, over the board
        public float level;                 // the height compared with the viewer's eyes for "another floor"
        public Color color;
        public IndicatorIcon icon;
        public int number = -1;             // Partner: CrewMember.index
        public bool keys;                   // the grandmother still has the house keys
        public bool ready;                  // Deliver: everything left on the list is in the truck
        public float pulse;                 // 0 still, 1 beating (she is alert, delivery is ready)
        public bool onTape = true;
        public bool markers = true;         // rings and edge arrows allowed (the reveal rules)
        public float noRingWithin;          // no ring this close: her speech bubble is over her head
        public bool anyDistance;            // a ring however far (the exit): no ringMaxDistance
        public float markersWithin;         // > 0: no ring or edge arrow farther than this (the tape still shows it)
    }

    // The targets of the running scene: every crew member (each view skips its own), the
    // grandmother, the delivery board on the truck, and for the police flee the exit and the
    // police cars (scene markers, found through their own registries). Kept in drawing order,
    // back to front: the board, the exit, the cars, her, the crew, so a player's token is never
    // hidden under hers.
    public sealed class IndicatorTargets
    {
        const float HeadAboveFeet = 2.05f;      // a crew ring hangs over the cap
        const float OverGrandmaHead = 0.5f;
        const float OverBoard = 0.9f;
        const float OverPoliceCar = 2.2f;

        readonly List<IndicatorTarget> list = new List<IndicatorTarget>(8);
        GrandmaBrain grandma;
        ContractManager contract;

        public IReadOnlyList<IndicatorTarget> All => list;

        // Bumped whenever the list changes, so each view re-syncs its tokens then and only then.
        public int Version { get; private set; }

        // Once, at Start: the scene's grandmother and contract (SceneLookup allocates: never per frame).
        public void Find(Scene scene)
        {
            grandma = SceneLookup.Find<GrandmaBrain>(scene);
            var session = GameSession.Current;
            contract = session != null && session.contract != null && session.contract.gameObject.scene == scene
                ? session.contract
                : SceneLookup.Find<ContractManager>(scene);
            Version++;
        }

        // Per frame. Rebuilds the list when who is there changed (a player joined, the board was
        // built by ContractManager.Start), then works out each target's look.
        public void Refresh(IndicatorSettings s)
        {
            if (Changed()) Rebuild();
            for (int i = 0; i < list.Count; i++)
            {
                IndicatorTarget t = list[i];
                switch (t.kind)
                {
                    case IndicatorKind.Partner: RefreshPartner(t); break;
                    case IndicatorKind.Grandma: RefreshGrandma(t, s); break;
                    case IndicatorKind.Exit: RefreshExit(t, s); break;
                    case IndicatorKind.Police: RefreshPolice(t, s); break;
                    default: RefreshDeliver(t, s); break;
                }
            }
        }

        DeliverPoint Board => contract != null ? contract.DeliverPoint : null;

        bool Changed()
        {
            EscapeCheckpoint exit = EscapeCheckpoint.Active;
            var cars = PoliceCar.All;
            int expected = (Board != null ? 1 : 0) + (exit != null ? 1 : 0) + (grandma != null ? 1 : 0);
            for (int i = 0; i < cars.Count; i++) if (cars[i] != null) expected++;
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++) if (crew[i] != null) expected++;
            if (expected != list.Count) return true;
            int n = 0;
            if (Board != null && (list[n].kind != IndicatorKind.Deliver || list[n++].board != Board)) return true;
            if (exit != null && (list[n].kind != IndicatorKind.Exit || list[n++].exit != exit)) return true;
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i] == null) continue;
                if (list[n].kind != IndicatorKind.Police || list[n++].car != cars[i]) return true;
            }
            if (grandma != null && (list[n].kind != IndicatorKind.Grandma || list[n++].grandma != grandma)) return true;
            for (int i = 0; i < crew.Count; i++)
            {
                if (crew[i] == null) continue;
                if (list[n].kind != IndicatorKind.Partner || list[n++].member != crew[i]) return true;
            }
            return false;
        }

        void Rebuild()
        {
            list.Clear();
            DeliverPoint board = Board;
            if (board != null) list.Add(new IndicatorTarget { kind = IndicatorKind.Deliver, board = board, root = board.transform });
            EscapeCheckpoint exit = EscapeCheckpoint.Active;
            if (exit != null) list.Add(new IndicatorTarget { kind = IndicatorKind.Exit, exit = exit, root = exit.transform, anyDistance = true });
            var cars = PoliceCar.All;
            for (int i = 0; i < cars.Count; i++)
                if (cars[i] != null)
                    list.Add(new IndicatorTarget { kind = IndicatorKind.Police, car = cars[i], root = cars[i].transform });
            if (grandma != null) list.Add(new IndicatorTarget { kind = IndicatorKind.Grandma, grandma = grandma, root = grandma.transform });
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != null)
                    list.Add(new IndicatorTarget { kind = IndicatorKind.Partner, member = crew[i], root = crew[i].transform, number = crew[i].index });
            Version++;
        }

        static void RefreshPartner(IndicatorTarget t)
        {
            CrewMember m = t.member;
            t.alive = m != null && m.isActiveAndEnabled;
            if (!t.alive) return;
            t.anchor = m.Position + Vector3.up * HeadAboveFeet;
            t.level = m.EyePosition.y;
            t.color = IndicatorUi.Opaque(m.color);
            t.icon = IndicatorIcon.Number;
            t.number = m.index;
            t.pulse = 0f;
            t.onTape = true;
            t.markers = true;
        }

        static void RefreshGrandma(IndicatorTarget t, IndicatorSettings s)
        {
            GrandmaBrain g = t.grandma;
            t.alive = g != null && g.isActiveAndEnabled;
            if (!t.alive) return;
            GrandmaState state = g.State;
            bool alert = state == GrandmaState.Observe || state == GrandmaState.Investigate || state == GrandmaState.React
                      || state == GrandmaState.Confront || state == GrandmaState.CallPolice;
            // The house keys are in her hands until she has handed them over (ADR-009, the intro).
            t.keys = Session.State == SessionState.Intro && !g.KeysGiven;
            t.onTape = Reveal(s.grandmaOnTape, t.keys, alert);
            t.markers = Reveal(s.grandmaMarkers, t.keys, alert);
            MoodTier tier = g.mood != null ? g.mood.Tier : MoodTier.Sweet;
            t.color = s.MoodColor(tier);
            t.icon = tier == MoodTier.Sweet ? IndicatorIcon.GrandmaCalm
                   : tier == MoodTier.Annoyed ? IndicatorIcon.GrandmaAnnoyed
                   : tier == MoodTier.Angry ? IndicatorIcon.GrandmaAngry
                   : IndicatorIcon.GrandmaFurious;
            t.pulse = alert ? 1f : t.keys ? 0.5f : 0f;
            Vector3 head = g.senses != null ? g.senses.HeadPosition : g.transform.position + Vector3.up * 1.45f;
            t.anchor = head + Vector3.up * OverGrandmaHead;
            t.level = head.y;
            // While she talks, her bubble (GrandmaSpeech, within its range) is the marker.
            t.noRingWithin = g.speech != null && g.speech.IsSpeaking ? g.speech.bubbleRange : 0f;
        }

        static void RefreshDeliver(IndicatorTarget t, IndicatorSettings s)
        {
            DeliverPoint b = t.board;
            t.alive = b != null && b.isActiveAndEnabled;
            if (!t.alive) return;
            t.ready = b.CanInteract;
            t.color = t.ready ? s.deliver : s.truck;
            t.icon = t.ready ? IndicatorIcon.Box : IndicatorIcon.Truck;
            t.pulse = t.ready ? 1f : 0f;
            t.anchor = b.transform.position + Vector3.up * OverBoard;
            t.level = b.transform.position.y;
            t.onTape = true;
            t.markers = true;
        }

        // The way out, from the police call to the end of the run: for everyone, the driver and
        // the passenger included, at any distance (a ring in view, an arrow at the edge).
        static void RefreshExit(IndicatorTarget t, IndicatorSettings s)
        {
            EscapeCheckpoint e = t.exit;
            t.alive = s.showExit && e != null && e.isActiveAndEnabled && Session.Phase != MissionPhase.Job && !Session.IsOver;
            if (!t.alive) return;
            t.color = s.exit;
            t.icon = IndicatorIcon.Flag;
            // Beating once the police are here: now is the time to go.
            t.pulse = Session.Phase == MissionPhase.PoliceHere ? 1f : 0.4f;
            t.anchor = e.MarkerPosition;
            t.level = e.Center.y;
            t.onTape = true;
            t.markers = true;
        }

        // A dispatched police car (lights on, on both machines). Near ones get rings and arrows,
        // far ones only a token on the tape; a stopped car (its officers are out) beats.
        static void RefreshPolice(IndicatorTarget t, IndicatorSettings s)
        {
            PoliceCar c = t.car;
            t.alive = s.showPolice && c != null && c.isActiveAndEnabled && c.IsDispatched && !Session.IsOver;
            if (!t.alive) return;
            Vector3 at = c.transform.position;
            t.color = s.police;
            t.icon = IndicatorIcon.PoliceCar;
            t.pulse = c.IsStopped ? 1f : 0f;
            t.anchor = at + Vector3.up * OverPoliceCar;
            t.level = at.y;
            t.onTape = true;
            t.markers = true;
            t.markersWithin = s.policeMarkerMaxDistance;
        }

        static bool Reveal(GrandmaReveal r, bool intro, bool alert)
        {
            switch (r)
            {
                case GrandmaReveal.Always: return true;
                case GrandmaReveal.WhenAlert: return alert || intro;
                case GrandmaReveal.IntroOnly: return intro;
                default: return false;
            }
        }
    }
}
