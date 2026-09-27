using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Several hinged panels worked as one thing: the two sashes of a window, a door and the
    // frame around it.
    //
    // Put it on the object whose collider surrounds the panels (a wall module) and list the
    // panels. Looking at any of them, or at that frame close to them, shows one prompt, and E
    // opens them all or closes them all. Two reasons. A window is one thing to open, not two
    // leaves that each want their own press. And an open leaf sticks out of the wall at an
    // angle, which is hard to aim at; the frame it left behind is where the eye goes to close it.
    //
    // Locks work through it too: a door wall answers "Locked" for the locked leaf in it, one E
    // by a key holder unlocks every leaf the key fits, and the key's own button (TurnKey)
    // locks or unlocks them all. One press is one event on the bus (subject: the group), not
    // one per leaf.
    //
    // HouseInteractionSetup adds one to every window module and to every door wall that holds a
    // hinged leaf. It can also be placed by hand.
    public class HingedGroup : Interactable
    {
        public List<HingedPanel> panels = new List<HingedPanel>();
        public string promptOpen = "Open";
        public string promptClose = "Close";

        [Header("Aim")]
        // Only the part of this object's collider near the panels counts: a window module is a
        // whole stretch of wall. Metres added around the panels' shut box, in this object's
        // local space (a kit wall: x along it, y up, z through it). Read when the group is built.
        public bool onlyNearPanels = true;
        public Vector3 aimMargin = new Vector3(0.3f, 0.3f, 1f);

        Bounds area;
        bool hasArea;

        // Every Awake has run by now, so hand-placed panels are seated and their shut pose known.
        void Start() { Rebuild(); }

        // For groups filled from code. Rebuilds at once: no need to wait for Start.
        public void Add(HingedPanel panel)
        {
            if (panel == null) return;
            if (!panels.Contains(panel)) panels.Add(panel);
            Rebuild();
        }

        // Claims the panels and measures where they sit when shut. Safe to call any number of
        // times, and correct while panels are open, since their shut pose is what is measured.
        public void Rebuild()
        {
            hasArea = false;
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p == null) continue;
                p.Group = this;
                if (!p.TryGetClosedBounds(transform, out Bounds b)) continue;
                if (!hasArea) { area = b; hasArea = true; }
                else area.Encapsulate(b);
            }
            if (hasArea) area.Expand(aimMargin * 2f);
        }

        // Only panels that can still move count. A leaf whose glass is all gone may be stuck
        // open, and it must not keep the prompt on "Close" for ever.
        public bool AnyOpen
        {
            get
            {
                for (int i = 0; i < panels.Count; i++)
                {
                    HingedPanel p = panels[i];
                    if (p != null && p.IsOpen && p.CanSwing) return true;
                }
                return false;
            }
        }

        // A window when any of its leaves is one: it raises the window events.
        public bool IsWindow
        {
            get
            {
                for (int i = 0; i < panels.Count; i++)
                    if (panels[i] != null && panels[i].isWindow) return true;
                return false;
            }
        }

        public bool IsLocked
        {
            get
            {
                for (int i = 0; i < panels.Count; i++)
                {
                    HingedPanel p = panels[i];
                    if (p != null && p.CanSwing && p.IsLocked) return true;
                }
                return false;
            }
        }

        public override string Prompt => PromptFor(Viewer());

        public override string PromptFor(PlayerInteract by)
        {
            CrewMember who = CrewOf(by);
            DoorVerb verb = VerbFor(who);
            // The key holder also reads what the key's own button does here: "Open   [F] Lock".
            if (verb == DoorVerb.Open && KeyFits(who)) return DoorLock.WithLockHint(promptOpen, who);
            return DoorLock.Label(verb, promptOpen, promptClose);
        }

        // Looking at one of its leaves is looking at the group: the leaf hands it its prompt.
        protected override bool IsAimedAt(Interactable target)
        {
            return target == this || (target is HingedPanel p && p.Group == this);
        }

        public override bool CanInteract
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                for (int i = 0; i < panels.Count; i++)
                {
                    HingedPanel p = panels[i];
                    if (p != null && p.CanSwing) return true;
                }
                return false;
            }
        }

        public override bool Covers(Vector3 hitPoint)
        {
            if (!onlyNearPanels || !hasArea) return true;
            return area.Contains(transform.InverseTransformPoint(hitPoint));
        }

        // What E does on the whole group, for this player. Anything open: close everything. All
        // shut and something locked: unlock what the key fits, or rattle. Otherwise open, key
        // or no key (locking is TurnKey).
        public DoorVerb VerbFor(CrewMember who)
        {
            if (AnyOpen) return DoorVerb.Close;
            bool anyLocked = false, canUnlock = false;
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p == null || !p.CanSwing) continue;
                DoorLock l = p.Lock;
                if (l == null || !l.IsLocked) continue;
                anyLocked = true;
                if (l.OpensFor(who)) canUnlock = true;
            }
            if (anyLocked) return canUnlock ? DoorVerb.Unlock : DoorVerb.Locked;
            return DoorVerb.Open;
        }

        public override void Interact(PlayerInteract by)
        {
            CrewMember who = CrewOf(by);
            int actor = ActorOf(who);
            switch (VerbFor(who))
            {
                case DoorVerb.Close: SetOpen(false, actor); break;
                case DoorVerb.Open: SetOpen(true, actor); break;
                case DoorVerb.Unlock: UnlockFor(who, actor); break;
                default: Rattle(actor); break;
            }
        }

        // The key in this player's hands, turned in the whole door (KeyItem, on the Alt button):
        // all shut, every leaf the key fits unlocks if any of them is locked, else they all lock.
        // false: something is open, or the key fits nothing here.
        public bool TurnKey(CrewMember who)
        {
            if (AnyOpen || !KeyFits(who)) return false;
            bool anyLocked = false;
            for (int i = 0; i < panels.Count; i++)
            {
                DoorLock l = FittingLock(panels[i], who);
                if (l != null && l.IsLocked) anyLocked = true;
            }
            int actor = ActorOf(who);
            for (int i = 0; i < panels.Count; i++)
            {
                DoorLock l = FittingLock(panels[i], who);
                if (l == null) continue;
                if (anyLocked) l.Unlock(actor);
                else l.Lock(actor);
            }
            return true;
        }

        // A key in this player's hands fits a lock on one of the leaves that still swing.
        bool KeyFits(CrewMember who)
        {
            if (who == null) return false;
            for (int i = 0; i < panels.Count; i++)
                if (FittingLock(panels[i], who) != null) return true;
            return false;
        }

        static DoorLock FittingLock(HingedPanel p, CrewMember who)
        {
            if (p == null || !p.CanSwing) return null;
            DoorLock l = p.Lock;
            return l != null && l.LockableBy(who) ? l : null;
        }

        void UnlockFor(CrewMember who, int actor)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p == null || !p.CanSwing) continue;
                DoorLock l = p.Lock;
                if (l != null && l.IsLocked && l.OpensFor(who)) l.Unlock(actor);
            }
        }

        void Rattle(int actor)
        {
            if (!Net.HasAuthority) return;   // online, the client jiggles from the replayed event (DoorSync)
            bool rattled = false;
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p != null && p.CanSwing && p.IsLocked) rattled |= p.Jiggle(actor);
            }
            if (rattled) DoorLock.AnnounceRattle(EventPoint(), actor, this);
        }

        // Lock-agnostic, like HingedPanel.SetOpen: the bare form is the grandmother's.
        public void SetOpen(bool open) { SetOpen(open, Actors.Grandma); }

        public void SetOpen(bool open, int instigator)
        {
            if (!Net.HasAuthority) return;   // online, only the host works doors (DoorSync replicates the leaves)
            bool changed = false;
            for (int i = 0; i < panels.Count; i++)
            {
                HingedPanel p = panels[i];
                if (p != null && p.CanSwing) changed |= p.Command(open, instigator);
            }
            if (changed) HingedPanel.Announce(open, IsWindow, EventPoint(), instigator, this);
        }

        Vector3 EventPoint()
        {
            return hasArea ? transform.TransformPoint(area.center) : transform.position;
        }
    }
}
