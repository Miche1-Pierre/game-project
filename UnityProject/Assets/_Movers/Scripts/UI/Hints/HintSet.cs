using System;

namespace Movers
{
    // Every verb the key hints can name, derived from the code (PlayerGrab, PlayerInteract and
    // its Interactables, HeldUsable and its items, PlayerEquip, PlayerPockets, KeyItem,
    // VehicleSeat, TruckVehicle). The words are in Loc under "verb.*".
    public enum Verb : byte
    {
        None,
        Grab, Drop, LetGo, Drag, Throw, ThrowAway, Rotate, Roll, Reach,
        Pocket, Smoke, Drink, EmptyBottle, Wear, TakeOff, PullPin, ThrowGrenade, ReleaseThrow,
        Open, Close, Lock, Unlock, Locked, Talk, Drive, GetOut, Deliver,
        StopToGetOut, RampMoving, Handbrake, Steer, Look,
        Move, Jump, Sprint, Crouch, Pause, Controls,
        Raw,        // an Interactable's own words the table does not know: shown as they are
    }

    // How the button is worked, drawn as a small word next to the key.
    public enum Gesture : byte { Press, Hold, Tap, Release }

    // One "[key] verb" row. A value type with value equality: the whole set is computed every
    // frame and compared with the last one, and only a difference rebuilds the widgets.
    public readonly struct HintLine : IEquatable<HintLine>
    {
        public readonly Verb verb;
        public readonly Glyph glyph;
        public readonly Glyph glyph2;     // a second key worked with the first (LT + stick)
        public readonly Gesture gesture;
        public readonly short a, b;       // numbers in the words ("Livrer 7/12")
        public readonly string raw;       // Verb.Raw and door prompts: the words, already translated
        public readonly bool warn;        // a verb that is a refusal ("Locked", "Stop to get out")

        public HintLine(Verb verb, Glyph glyph, Gesture gesture = Gesture.Press, int a = 0, int b = 0,
                        string raw = null, bool warn = false, Glyph glyph2 = default)
        {
            this.verb = verb;
            this.glyph = glyph;
            this.glyph2 = glyph2;
            this.gesture = gesture;
            this.a = (short)a;
            this.b = (short)b;
            this.raw = raw;
            this.warn = warn;
        }

        public bool IsEmpty => verb == Verb.None;

        public bool Equals(HintLine o) =>
            verb == o.verb && gesture == o.gesture && a == o.a && b == o.b && warn == o.warn &&
            ReferenceEquals(raw, o.raw) && glyph.Equals(o.glyph) && glyph2.Equals(o.glyph2);

        public override bool Equals(object obj) => obj is HintLine o && Equals(o);
        public override int GetHashCode() => ((int)verb * 397) ^ glyph.GetHashCode() ^ (a << 16) ^ b;
    }

    // What the small card above the hints says about the object looked at or held.
    public readonly struct TargetCard : IEquatable<TargetCard>
    {
        public readonly MovableObject item;
        public readonly bool held;       // in the hands (true) or under the crosshair (false)
        public readonly int heldBy;      // the other player carrying it, or -1

        public TargetCard(MovableObject item, bool held, int heldBy)
        {
            this.item = item;
            this.held = held;
            this.heldBy = heldBy;
        }

        public bool IsEmpty => ReferenceEquals(item, null);

        public bool Equals(TargetCard o) => ReferenceEquals(item, o.item) && held == o.held && heldBy == o.heldBy;
        public override bool Equals(object obj) => obj is TargetCard o && Equals(o);
        public override int GetHashCode() => (ReferenceEquals(item, null) ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)) ^ (held ? 1 : 0) ^ (heldBy << 2);
    }

    // Everything one player's hint panel shows this frame: at most four rows (more would be a
    // manual, not a hint) and the card. Fixed fields, no array, so it is copied and compared
    // without garbage.
    public struct HintSet : IEquatable<HintSet>
    {
        public const int Max = 4;

        HintLine l0, l1, l2, l3;
        public int Count { get; private set; }
        public TargetCard card;

        public void Add(in HintLine line)
        {
            if (line.IsEmpty || Count >= Max) return;
            switch (Count)
            {
                case 0: l0 = line; break;
                case 1: l1 = line; break;
                case 2: l2 = line; break;
                default: l3 = line; break;
            }
            Count++;
        }

        public HintLine this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return l0;
                    case 1: return l1;
                    case 2: return l2;
                    case 3: return l3;
                    default: return default;
                }
            }
        }

        public bool Contains(Verb v)
        {
            for (int i = 0; i < Count; i++) if (this[i].verb == v) return true;
            return false;
        }

        public bool IsEmpty => Count == 0 && card.IsEmpty;

        public bool Equals(HintSet o)
        {
            if (Count != o.Count || !card.Equals(o.card)) return false;
            for (int i = 0; i < Count; i++) if (!this[i].Equals(o[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is HintSet o && Equals(o);
        public override int GetHashCode() => (Count * 397) ^ card.GetHashCode() ^ l0.GetHashCode();

        // The words of a row, in the current language.
        public static string Words(in HintLine line)
        {
            switch (line.verb)
            {
                case Verb.Raw: return line.raw ?? "";
                case Verb.Grab: return Loc.T("verb.grab");
                case Verb.Drop: return Loc.T("verb.drop");
                case Verb.LetGo: return Loc.T("verb.letGo");
                case Verb.Drag: return Loc.T("verb.drag");
                case Verb.Throw: return Loc.T("verb.throw");
                case Verb.ThrowAway: return Loc.T("verb.throwAway");
                case Verb.Rotate: return Loc.T("verb.rotate");
                case Verb.Roll: return Loc.T("verb.roll");
                case Verb.Reach: return Loc.T("verb.reach");
                case Verb.Pocket: return Loc.T("verb.pocket");
                case Verb.Smoke: return Loc.T("verb.smoke");
                case Verb.Drink: return Loc.T("verb.drink");
                case Verb.EmptyBottle: return Loc.T("verb.empty");
                case Verb.Wear: return Loc.T("verb.wear");
                case Verb.TakeOff: return Loc.T("verb.takeOff");
                case Verb.PullPin: return Loc.T("verb.pullPin");
                case Verb.ThrowGrenade: return Loc.T("verb.throwGrenade");
                case Verb.ReleaseThrow: return Loc.T("verb.releaseThrow");
                case Verb.Open: return line.raw ?? Loc.T("verb.open");
                case Verb.Close: return line.raw ?? Loc.T("verb.close");
                case Verb.Lock: return Loc.T("verb.lock");
                case Verb.Unlock: return Loc.T("verb.unlock");
                case Verb.Locked: return Loc.T("verb.locked");
                case Verb.Talk: return Loc.T("verb.talk");
                case Verb.Drive: return Loc.T("verb.drive");
                case Verb.GetOut: return Loc.T("verb.getOut");
                case Verb.Deliver: return Loc.F("verb.deliver", line.a, line.b);
                case Verb.StopToGetOut: return Loc.T("verb.stopToGetOut");
                case Verb.RampMoving: return Loc.T("verb.rampMoving");
                case Verb.Handbrake: return Loc.T("verb.handbrake");
                case Verb.Steer: return Loc.T("verb.steer");
                case Verb.Look: return Loc.T("verb.look");
                case Verb.Move: return Loc.T("verb.move");
                case Verb.Jump: return Loc.T("verb.jump");
                case Verb.Sprint: return Loc.T("verb.sprint");
                case Verb.Crouch: return Loc.T("verb.crouch");
                case Verb.Pause: return Loc.T("verb.pause");
                case Verb.Controls: return Loc.T("verb.controls");
            }
            return "";
        }

        public static string GestureWord(Gesture g)
        {
            switch (g)
            {
                case Gesture.Hold: return Loc.T("gesture.hold");
                case Gesture.Tap: return Loc.T("gesture.tap");
                case Gesture.Release: return Loc.T("gesture.release");
            }
            return null;
        }
    }
}
