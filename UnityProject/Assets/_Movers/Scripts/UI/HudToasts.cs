using System;
using UnityEngine;

namespace Movers
{
    // Short lines at the top centre of every view (HUD region TopCenter) when something happens
    // that both players should know about: a theft, a broken piece of the contract, the
    // grandmother noticing, the keys. The same lines in both views, so a spectator reads the
    // story from either half of the screen. Texts are built when the event arrives (a few per
    // minute), never per frame.
    public sealed class HudToasts
    {
        struct Toast
        {
            public string text;
            public Color color;
            public float at;
        }

        const int Max = 4;
        const float Life = 4f;
        const float Fade = 0.6f;
        const float NoticedEvery = 3f;   // she notices a lot; one line every few seconds is enough
        const float Margin = 12f;        // ViewportGUI.Region's default, which the panel uses
        const float MinWidth = 120f;

        static readonly Color White = Color.white;
        static readonly Color Warning = new Color(1f, 0.66f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        static readonly Color Good = new Color(0.6f, 0.9f, 0.6f);

        readonly Toast[] toasts = new Toast[Max];
        readonly Action<WorldEvent> handler;
        readonly GUIContent content = new GUIContent();
        GUIStyle measure;   // made in the first OnGUI: GUI.skin is only readable there
        int head, count;
        float lastNoticed = -99f;

        public HudToasts() { handler = OnEvent; }

        public void Subscribe() => WorldEvents.Subscribe(handler);
        public void Unsubscribe() => WorldEvents.Unsubscribe(handler);

        public void Push(string text, Color color)
        {
            toasts[head] = new Toast { text = text, color = color, at = Time.unscaledTime };
            head = (head + 1) % Max;
            if (count < Max) count++;
        }

        void OnEvent(WorldEvent e)
        {
            MovableObject item = e.Item;
            switch (e.type)
            {
                case WorldEventType.KeysHandedOver:
                    Push(Actors.Name(e.instigator) + " has the keys. The clock is running.", Good);
                    break;
                case WorldEventType.SessionStateChanged:
                    var s = GameSession.Current;
                    if ((SessionState)(int)e.magnitude != SessionState.ContractStarted || s == null) break;
                    if (s.BrokeIn)
                        Push("Break-in! " + Actors.Name(s.BreakInBy) + " broke " + s.BreakInWhat + ". The clock is running.", Warning);
                    else if (s.StartedHow.Length > 0)   // in without the keys, nothing broken
                        Push(Actors.Name(s.StartedBy) + " " + s.StartedHow + " before the keys. The clock is running.", Warning);
                    break;
                case WorldEventType.ItemPocketed:
                    if (TheftLedger.IsLoot(item))
                        Push(Actors.Name(e.instigator) + " pocketed the " + item.displayName + " (" + ContractPanel.Money(item.contractValue) + ")", ColorOf(e.instigator));
                    break;
                case WorldEventType.CargoLoaded:
                    if (TheftLedger.IsLoot(item))
                        Push("The " + item.displayName + " is in the truck. Not on the list (" + ContractPanel.Money(item.contractValue) + ")", ColorOf(e.instigator));
                    break;
                case WorldEventType.TheftWitnessed:
                    Push("Grandma saw " + Actors.Name(e.instigator) + " take " + (item != null ? "the " + item.displayName : "something") + "!", Bad);
                    break;
                case WorldEventType.ContractObjectDamaged:
                    Push("The " + NameOf(e) + " is damaged: half pay.", Warning);
                    break;
                case WorldEventType.ContractObjectDestroyed:
                    Push("The " + NameOf(e) + " is destroyed: billed " + ContractPanel.Money(e.value) + ".", Bad);
                    break;
                case WorldEventType.GrandmaNoticed:
                    if (Time.unscaledTime - lastNoticed < NoticedEvery) break;
                    lastNoticed = Time.unscaledTime;
                    Push("Grandma noticed something.", Warning);
                    break;
                case WorldEventType.GrandmaCalledPolice:
                    Push("Grandma is calling the police!", Bad);
                    break;
            }
        }

        // banner: a line that stays while it is true (the intro instruction, the police
        // countdown), drawn above the toasts. Empty for none.
        public void Draw(Rect view, int size, string banner, Color bannerColor)
        {
            float lineH = size * 1.35f;
            float w = Mathf.Min(view.width * 0.6f, 720f * size / 16f);
            // Centred, and as clear of the right edge as the contract panel keeps the left one:
            // in a half-width split view the panel and the patience bar (TopRight) would
            // otherwise sit under both ends of the lines. Narrow views wrap instead.
            float side = ContractPanel.Width(view, size) + 2f * Margin;
            w = Mathf.Max(MinWidth, Mathf.Min(w, view.width - 2f * side));
            Rect area = ViewportGUI.Region(view, HudRegion.TopCenter, w, lineH * (Max + 1), Margin);
            float y = area.y;
            if (!string.IsNullOrEmpty(banner))
            {
                float h = Height(banner, size + 2, w, lineH);
                ViewportGUI.Label(new Rect(area.x, y, w, h), banner, size + 2, bannerColor, TextAnchor.UpperCenter, true);
                y += h + lineH * 0.2f;
            }
            float now = Time.unscaledTime;
            for (int i = 0; i < count; i++)
            {
                // Newest first.
                var t = toasts[(head - 1 - i + Max) % Max];
                float age = now - t.at;
                if (age > Life) continue;
                var c = t.color;
                c.a = Mathf.Clamp01((Life - age) / Fade);
                float h = Height(t.text, size, w, lineH);
                ViewportGUI.Label(new Rect(area.x, y, w, h), t.text, size, c, TextAnchor.UpperCenter, true);
                y += h;
            }
        }

        // The height a bold, wrapped line takes at this width, so a toast that wraps in a
        // narrow view pushes the next one down instead of printing over it. Same settings as
        // ViewportGUI.Label's style; the style and content are reused, nothing is allocated.
        float Height(string text, int fontSize, float width, float minHeight)
        {
            if (measure == null) measure = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            measure.fontSize = fontSize;
            measure.fontStyle = FontStyle.Bold;
            content.text = text;
            return Mathf.Max(minHeight, measure.CalcHeight(content, width));
        }

        static string NameOf(in WorldEvent e)
        {
            var m = e.Item;
            if (m != null) return m.displayName;
            return e.subject != null ? e.subject.name : "piece";
        }

        static Color ColorOf(int actor)
        {
            var m = Actors.IsPlayer(actor) ? CrewRoster.Get(actor) : null;
            return m != null ? Color.Lerp(m.color, Color.white, 0.35f) : White;
        }
    }
}
