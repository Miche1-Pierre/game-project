using System;
using LumaFlow;
using UnityEngine;

namespace Movers
{
    // What one pocket bar shows. Compared every frame, published only when it changes.
    public struct PocketView : IEquatable<PocketView>
    {
        public MovableObject s0, s1, s2, s3;
        public int armed;       // bit per pocket holding a ticking grenade
        public int active;      // the pocket what is in the hands came out of, or -1
        public bool pad;

        public MovableObject this[int i] => i == 0 ? s0 : i == 1 ? s1 : i == 2 ? s2 : s3;

        public bool Equals(PocketView o) =>
            ReferenceEquals(s0, o.s0) && ReferenceEquals(s1, o.s1) && ReferenceEquals(s2, o.s2) && ReferenceEquals(s3, o.s3) &&
            armed == o.armed && active == o.active && pad == o.pad;
        public override bool Equals(object obj) => obj is PocketView o && Equals(o);
        public override int GetHashCode() => armed ^ (active << 4) ^ (pad ? 1 << 8 : 0);
    }

    // One player's HUD state: the hints for what they look at and hold, their pockets, their
    // drink, their truck, their pause menu. HudRoot feeds it once per frame; the player's view
    // rebuilds only the part whose state changed.
    public sealed class PlayerHudModel
    {
        public readonly CrewMember member;
        public readonly VerbHints verbs;
        public readonly HudPauseMenu pause;

        public readonly State<HintSet> Hints = new State<HintSet>(default);
        public readonly State<int> Device = new State<int>(0);          // 0 keyboard, 1 pad, 2 nobody
        public readonly State<bool> Crosshair = new State<bool>(true);

        public readonly State<PocketView> Pockets = new State<PocketView>(new PocketView { active = -1 });
        public readonly State<string> PocketHint = new State<string>(null);

        public readonly State<bool> Drunk = new State<bool>(false);
        public readonly UiFill DrunkFill = new UiFill();

        public readonly State<bool> Driving = new State<bool>(false);
        public readonly State<string> Speed = new State<string>("0");
        public readonly State<string> Cargo = new State<string>("");
        public readonly State<bool> Overloaded = new State<bool>(false);
        public readonly UiFill CargoFill = new UiFill();

        public readonly UiFill PatienceFill = new UiFill();
        public readonly UiAnchor SpeechAnchor = new UiAnchor();
        public readonly UiAnchor DeliverAnchor = new UiAnchor();

        public readonly State<bool> ControlsOpen = new State<bool>(false);

        public int Index => member != null ? member.index : 0;
        public ICrewInputSource Source => member != null && member.Input != null ? member.Input.Source : null;

        string shownHint;
        int shownKmh = -1, shownKg = -1, shownCap = -1;
        Drunkenness drunk;
        VehicleSeat seat;

        public PlayerHudModel(CrewMember member)
        {
            this.member = member;
            verbs = new VerbHints(member);
            pause = new HudPauseMenu(member);
        }

        public void Update(SharedHudModel shared, bool visible)
        {
            if (member == null) return;
            var src = Source;
            Device.Value = LocalDevicesSource.IsPad(src) ? 1 : src == null || src is NullInputSource ? 2 : 0;

            // Pause with the controls sheet open closes the sheet first, rather than opening a
            // menu over it.
            if (ControlsOpen.Value && !pause.IsOpen && member.Input != null && member.Input.TryConsume(CrewButton.Pause))
            {
                ControlsOpen.Value = false;
                UiAudio.Close();
            }
            pause.Tick(visible);
            if (!pause.IsOpen && visible && Device.Value != 2 && HudKeys.ControlsPressed(member.Input))
            {
                ControlsOpen.Value = !ControlsOpen.Value;
                if (ControlsOpen.Value) UiAudio.Open(); else UiAudio.Close();
            }

            if (!visible) return;
            bool frozen = member.Input != null && member.Input.Muted;
            Hints.Value = frozen || pause.IsOpen ? default : verbs.Compute();
            Crosshair.Value = !member.IsDriving && !pause.IsOpen;

            UpdatePockets(src);
            UpdateDrunk();
            UpdateTruck();
            PatienceFill.Set(shared.Patience01);
        }

        void UpdatePockets(ICrewInputSource src)
        {
            var p = member.Pockets;
            var view = new PocketView { active = -1, pad = LocalDevicesSource.IsPad(src) };
            if (p != null && p.isActiveAndEnabled)
            {
                view.s0 = p.GetItem(0);
                view.s1 = p.GetItem(1);
                view.s2 = p.GetItem(2);
                view.s3 = p.GetItem(3);
                for (int i = 0; i < PlayerPockets.SlotCount; i++)
                {
                    var item = view[i];
                    if (item != null && item.TryGetComponent(out GrenadeItem g) && g.IsArmed) view.armed |= 1 << i;
                }
                view.active = p.OutOfPocketSlot;
                string hint = p.ActiveHint;
                if (!ReferenceEquals(hint, shownHint))
                {
                    shownHint = hint;
                    PocketHint.Value = PocketWords(hint);
                }
            }
            Pockets.Value = view;
        }

        // PlayerPockets' own feedback ("Hands full", "Pocket 2 is empty") in the game's language.
        static string PocketWords(string english)
        {
            if (string.IsNullOrEmpty(english)) return null;
            if (english == "Hands full") return Loc.T("pocket.handsFull");
            if (english.StartsWith("Pocket ", StringComparison.Ordinal) && english.EndsWith(" is empty", StringComparison.Ordinal))
                return Loc.F("pocket.empty", english.Substring(7, english.Length - 7 - 9));
            return english;
        }

        void UpdateDrunk()
        {
            if (drunk == null) member.TryGetComponent(out drunk);   // the beer adds it on the first sip
            bool on = drunk != null && drunk.IsDrunk;
            Drunk.Value = on;
            if (on) DrunkFill.Set(drunk.Amount);
        }

        // The truck's plate is for whoever is at the wheel; the passenger (also IsDriving, which
        // means seated) rides without it.
        void UpdateTruck()
        {
            bool driving = TruckVehicle.IsAtWheel(member);
            if (!driving) seat = null;
            else if (seat == null) seat = member.GetComponentInParent<VehicleSeat>();
            TruckVehicle truck = seat != null ? seat.vehicle : null;
            Driving.Value = driving && truck != null;
            if (truck == null) return;

            int kmh = Mathf.RoundToInt(truck.SpeedKmh);
            if (kmh != shownKmh)
            {
                shownKmh = kmh;
                Speed.Value = Loc.Int(kmh);
            }
            var cargo = truck.cargo;
            if (cargo == null) return;
            int kg = Mathf.RoundToInt(cargo.LoadedKg), cap = Mathf.RoundToInt(cargo.capacityKg);
            if (kg != shownKg || cap != shownCap)
            {
                shownKg = kg;
                shownCap = cap;
                Cargo.Value = Loc.F("drive.kg", kg, cap);
            }
            Overloaded.Value = cargo.OverCapacity;
            CargoFill.Set(cap > 0 ? (float)kg / cap : 0f);
        }

        // After a language change.
        public void Relocalize()
        {
            shownHint = null;
            shownKmh = shownKg = shownCap = -1;
            Hints.Value = default;   // recomputed next frame with the new words
        }

        public void Dispose()
        {
            pause.Close(true);
        }
    }
}
