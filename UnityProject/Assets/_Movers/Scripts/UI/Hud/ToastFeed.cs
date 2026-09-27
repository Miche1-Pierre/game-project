using System;
using System.Collections.Generic;
using LumaFlow;
using UnityEngine;

namespace Movers
{
    // Short lines both players should read when something happens: a theft, a broken piece of
    // the contract, the grandmother noticing, the keys. The same lines in every view, so a
    // spectator reads the story from either half of the screen. Built when the event arrives
    // (a few per minute), never per frame; the views rebuild when Version moves.
    public sealed class ToastFeed
    {
        public sealed class Entry
        {
            public WidgetKey key;
            public string text;
            public string icon;
            public Color color;
            public float at;
            public readonly State<float> opacity = new State<float>(1f);
        }

        public const int Max = 4;
        public const float Life = 5f;
        public const float FadeSeconds = 0.4f;
        const float NoticedEvery = 3f;   // she notices a lot; one line every few seconds is enough

        readonly List<Entry> live = new List<Entry>(Max + 1);
        readonly Action<WorldEvent> handler;
        int nextId;
        float lastNoticed = -99f;

        public readonly State<int> Version = new State<int>(0);
        public IReadOnlyList<Entry> Live => live;

        public ToastFeed() { handler = OnEvent; }

        public void Subscribe() => WorldEvents.Subscribe(handler);
        public void Unsubscribe() => WorldEvents.Unsubscribe(handler);

        public void Push(string text, string icon, Color color)
        {
            var e = new Entry
            {
                key = new WidgetKey("toast" + (nextId++)),
                text = text,
                icon = icon,
                color = color,
                at = Time.unscaledTime,
            };
            live.Insert(0, e);   // newest first
            if (live.Count > Max) live.RemoveAt(live.Count - 1);
            UiAudio.Toast();
            Version.Value++;
        }

        // Fades the old ones out, then drops them. No allocation when nothing expires.
        public void Tick()
        {
            float now = Time.unscaledTime;
            bool removed = false;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                float age = now - live[i].at;
                if (age > Life) { live.RemoveAt(i); removed = true; }
                else if (age > Life - FadeSeconds) live[i].opacity.Value = 0f;
            }
            if (removed) Version.Value++;
        }

        public void Clear()
        {
            if (live.Count == 0) return;
            live.Clear();
            Version.Value++;
        }

        void OnEvent(WorldEvent e)
        {
            var t = UiTheme.Current;
            MovableObject item = e.Item;
            switch (e.type)
            {
                case WorldEventType.KeysHandedOver:
                    // The contract board slides in at the same moment (ContractBoardView).
                    Push(Loc.T("toast.keysList"), UiSprites.IconKey, t.good);
                    break;
                case WorldEventType.SessionStateChanged:
                {
                    var s = GameSession.Current;
                    if ((SessionState)(int)e.magnitude != SessionState.ContractStarted || s == null) break;
                    if (s.BrokeIn)
                        Push(Loc.F("toast.breakIn", Who(s.BreakInBy), What(s.BreakInWhat)), UiSprites.IconWarning, t.warn);
                    else if (s.StartedHow.Length > 0)   // in without the keys, nothing broken
                        Push(Loc.F("toast.startedHow", Who(s.StartedBy), How(s.StartedHow)), UiSprites.IconWarning, t.warn);
                    break;
                }
                case WorldEventType.ItemPocketed:
                    if (TheftLedger.IsLoot(item))
                        Push(Loc.F("toast.pocketed", Who(e.instigator), Loc.Item(item.displayName), Loc.Money(item.contractValue)),
                             UiSprites.IconPocket, PlayerColor(e.instigator));
                    break;
                case WorldEventType.CargoLoaded:
                    if (TheftLedger.IsLoot(item))
                        Push(Loc.F("toast.loot", Loc.Item(item.displayName), Loc.Money(item.contractValue)),
                             UiSprites.IconTruck, PlayerColor(e.instigator));
                    break;
                case WorldEventType.TheftWitnessed:
                    Push(Loc.F("toast.witnessed", Who(e.instigator), item != null ? Loc.Item(item.displayName) : Loc.T("toast.something")),
                         UiSprites.IconGrandmaAngry, t.bad);
                    break;
                case WorldEventType.ContractObjectDamaged:
                    Push(Loc.F("toast.contractDamaged", NameOf(e)), UiSprites.IconWarning, t.warn);
                    break;
                case WorldEventType.ContractObjectDestroyed:
                    Push(Loc.F("toast.contractDestroyed", NameOf(e), Loc.Money(e.value)), UiSprites.IconCross, t.bad);
                    break;
                case WorldEventType.GrandmaNoticed:
                    if (Time.unscaledTime - lastNoticed < NoticedEvery) break;
                    lastNoticed = Time.unscaledTime;
                    Push(Loc.T("toast.noticed"), UiSprites.IconGrandmaAnnoyed, t.warn);
                    break;
                case WorldEventType.GrandmaCalledPolice:
                    Push(Loc.T("toast.police"), UiSprites.IconGrandmaFurious, t.bad);
                    break;
            }
        }

        // "J2" in French, "P2" in English.
        public static string Who(int actor)
        {
            if (Actors.IsPlayer(actor)) return Loc.F("player.label", actor + 1);
            return actor == Actors.Grandma ? Loc.T("grandma.name") : Loc.T("toast.something");
        }

        static string What(string english)
        {
            switch (english)
            {
                case "a window": return Loc.T("what.window");
                case "a door": return Loc.T("what.door");
                case "a wall": return Loc.T("what.wall");
            }
            return english;
        }

        static string How(string english)
        {
            switch (english)
            {
                case "opened a window": return Loc.T("how.openedWindow");
                case "broke a window": return Loc.T("how.brokeWindow");
                case "broke a door": return Loc.T("how.brokeDoor");
                case "broke through a wall": return Loc.T("how.brokeWall");
                case "brought part of the house down": return Loc.T("how.brought");
            }
            return english;
        }

        static string NameOf(in WorldEvent e)
        {
            var m = e.Item;
            if (m != null) return Loc.Item(m.displayName);
            return e.subject != null ? e.subject.name : Loc.T("toast.piece");
        }

        static Color PlayerColor(int actor)
        {
            var m = Actors.IsPlayer(actor) ? CrewRoster.Get(actor) : null;
            return m != null ? Color.Lerp(m.color, Color.white, 0.2f) : UiTheme.Current.accent;
        }
    }
}
