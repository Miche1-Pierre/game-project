using System;

namespace Movers.AudioSynth
{
    // Every one-shot and loop the game synthesises, by name. Add new kinds at the end: the
    // Unity side keys its mixing table on these values.
    public enum SfxKind
    {
        // Footsteps: work boots (crew) and slippers (the grandmother), per surface.
        StepWood, StepStone, StepGrass, StepCarpet, StepMetal,
        SlipperWood, SlipperStone, SlipperGrass, SlipperCarpet,
        LandWood, LandStone, LandGrass, LandCarpet, LandMetal,
        FloorCreak,

        // Handling.
        ImpactSoft, ImpactWood, ImpactHeavy, ImpactMetal, ImpactGlass, ImpactCeramic, ImpactFabric,
        ScrapeLoop, Whoosh, GrabCloth, Rustle, PocketPat, KeyJingle,
        BottleClink, Gulp, BottleSmash, SmokeInhale, SmokeCrackleLoop, SmokeExhale, Cough,

        // Doors and windows.
        LatchClick, DoorCreak, SashSlide, LockClack,

        // The truck.
        EngineIdleLoop, EngineLoadLoop, EngineStart, EngineStop, TruckDoor, AirBrake,
        BrakeSqueakLoop, ReverseBeepLoop, RoadRumbleLoop,

        // Interface.
        UiHover, UiClick, UiConfirm, UiBack, UiOpen, UiClose, UiToast, UiDenied, UiTick, UiCoins, UiSlider,

        // Stingers.
        StingTheftSeen, JingleWin, JingleFail, JingleDelivered, PhoneDial, SirenLoop,

        // Ambience.
        WindLoop, BirdCall, CarPass, RoomToneLoop,

        // What the grandmother is busy with.
        RockCreakLoop, PageTurn, TeaClink, SipTea, PotBubbleLoop, WaterPourLoop, MatchStrike,
        FireCrackleLoop, TvMurmurLoop,

        Count
    }

    // A finished sound: samples (interleaved when channels is 2), its rate, whether it loops.
    public struct Rendered
    {
        public float[] samples;
        public int rate;
        public bool loop;
        public int channels;   // 0 or 1: mono

        public int Channels => channels < 1 ? 1 : channels;
        public int Frames => samples == null ? 0 : samples.Length / Channels;
        public float Seconds => rate > 0 ? Frames / (float)rate : 0f;
    }

    public static class Sfx
    {
        // 32 kHz keeps everything up to 16 kHz, above what any of these recipes puts out.
        // The long loops are all below 12 kHz (wind, engines, bubbles), so 24 kHz: a quarter
        // less memory for nothing anyone could hear.
        public const int OneShotRate = 32000;
        public const int LoopRate = 24000;

        public static int RateOf(SfxKind k) => IsLoop(k) || k == SfxKind.CarPass ? LoopRate : OneShotRate;

        // How many variants each kind has (played at random, never the same twice in a row).
        public static int Variants(SfxKind k)
        {
            switch (k)
            {
                case SfxKind.StepWood: case SfxKind.StepStone: case SfxKind.StepGrass:
                case SfxKind.StepCarpet: case SfxKind.StepMetal:
                case SfxKind.SlipperWood: case SfxKind.SlipperStone: case SfxKind.SlipperGrass:
                case SfxKind.SlipperCarpet:
                    return 6;
                case SfxKind.FloorCreak: case SfxKind.DoorCreak: case SfxKind.ImpactWood:
                case SfxKind.ImpactSoft: case SfxKind.Gulp: case SfxKind.BirdCall:
                    return k == SfxKind.BirdCall ? 6 : 4;
                case SfxKind.LandWood: case SfxKind.LandStone: case SfxKind.LandGrass:
                case SfxKind.LandCarpet: case SfxKind.LandMetal: case SfxKind.ImpactHeavy:
                case SfxKind.ImpactMetal: case SfxKind.ImpactGlass: case SfxKind.ImpactCeramic:
                case SfxKind.ImpactFabric: case SfxKind.Whoosh: case SfxKind.Rustle:
                case SfxKind.KeyJingle: case SfxKind.PageTurn: case SfxKind.TeaClink:
                    return 3;
                case SfxKind.CarPass:
                    return 2;
                default:
                    return 1;
            }
        }

        public static bool IsLoop(SfxKind k)
        {
            switch (k)
            {
                case SfxKind.ScrapeLoop: case SfxKind.SmokeCrackleLoop: case SfxKind.EngineIdleLoop:
                case SfxKind.EngineLoadLoop: case SfxKind.BrakeSqueakLoop: case SfxKind.ReverseBeepLoop:
                case SfxKind.RoadRumbleLoop: case SfxKind.SirenLoop: case SfxKind.WindLoop:
                case SfxKind.RoomToneLoop: case SfxKind.RockCreakLoop: case SfxKind.PotBubbleLoop:
                case SfxKind.WaterPourLoop: case SfxKind.FireCrackleLoop: case SfxKind.TvMurmurLoop:
                    return true;
                default:
                    return false;
            }
        }

        public static Rendered Render(SfxKind k, int variant)
        {
            var r = new Rng((int)k, variant);
            bool loop = IsLoop(k);
            int rate = RateOf(k);
            float[] d;
            switch (k)
            {
                case SfxKind.StepWood: d = Step(Surface.Wood, false, r, rate); break;
                case SfxKind.StepStone: d = Step(Surface.Stone, false, r, rate); break;
                case SfxKind.StepGrass: d = Step(Surface.Grass, false, r, rate); break;
                case SfxKind.StepCarpet: d = Step(Surface.Carpet, false, r, rate); break;
                case SfxKind.StepMetal: d = Step(Surface.Metal, false, r, rate); break;
                case SfxKind.SlipperWood: d = Slipper(Surface.Wood, r, rate); break;
                case SfxKind.SlipperStone: d = Slipper(Surface.Stone, r, rate); break;
                case SfxKind.SlipperGrass: d = Slipper(Surface.Grass, r, rate); break;
                case SfxKind.SlipperCarpet: d = Slipper(Surface.Carpet, r, rate); break;
                case SfxKind.LandWood: d = Step(Surface.Wood, true, r, rate); break;
                case SfxKind.LandStone: d = Step(Surface.Stone, true, r, rate); break;
                case SfxKind.LandGrass: d = Step(Surface.Grass, true, r, rate); break;
                case SfxKind.LandCarpet: d = Step(Surface.Carpet, true, r, rate); break;
                case SfxKind.LandMetal: d = Step(Surface.Metal, true, r, rate); break;
                case SfxKind.FloorCreak: d = Creak(r, rate, r.Range(0.28f, 0.42f), r.Range(55f, 90f), r.Range(0.5f, 0.8f), 0.55f); break;

                case SfxKind.ImpactSoft: d = ImpactSoft(r, rate); break;
                case SfxKind.ImpactWood: d = ImpactWood(r, rate); break;
                case SfxKind.ImpactHeavy: d = ImpactHeavy(r, rate); break;
                case SfxKind.ImpactMetal: d = ImpactMetal(r, rate); break;
                case SfxKind.ImpactGlass: d = Clink(r, rate, r.Range(1500f, 1900f), 0.8f); break;
                case SfxKind.ImpactCeramic: d = ImpactCeramic(r, rate); break;
                case SfxKind.ImpactFabric: d = ImpactFabric(r, rate); break;
                case SfxKind.ScrapeLoop: d = ScrapeLoop(r, rate); break;
                case SfxKind.Whoosh: d = Whoosh(r, rate); break;
                case SfxKind.GrabCloth: d = Rustle(r, rate, 0.16f, 4, 0.5f, true); break;
                case SfxKind.Rustle: d = Rustle(r, rate, 0.42f, 9, 0.7f, false); break;
                case SfxKind.PocketPat: d = PocketPat(r, rate); break;
                case SfxKind.KeyJingle: d = KeyJingle(r, rate); break;
                case SfxKind.BottleClink: d = Clink(r, rate, 1250f, 0.7f); break;
                case SfxKind.Gulp: d = Gulp(r, rate); break;
                case SfxKind.BottleSmash: d = BottleSmash(r, rate); break;
                case SfxKind.SmokeInhale: d = SmokeInhale(r, rate); break;
                case SfxKind.SmokeCrackleLoop: d = SmokeCrackleLoop(r, rate); break;
                case SfxKind.SmokeExhale: d = SmokeExhale(r, rate); break;
                case SfxKind.Cough: d = Cough(r, rate); break;

                case SfxKind.LatchClick: d = LatchClick(r, rate); break;
                case SfxKind.DoorCreak: d = DoorCreak(r, rate, variant); break;
                case SfxKind.SashSlide: d = SashSlide(r, rate); break;
                case SfxKind.LockClack: d = LockClack(r, rate); break;

                case SfxKind.EngineIdleLoop: d = EngineLoop(r, rate, false); break;
                case SfxKind.EngineLoadLoop: d = EngineLoop(r, rate, true); break;
                case SfxKind.EngineStart: d = EngineStart(r, rate); break;
                case SfxKind.EngineStop: d = EngineStop(r, rate); break;
                case SfxKind.TruckDoor: d = TruckDoor(r, rate); break;
                case SfxKind.AirBrake: d = AirBrake(r, rate); break;
                case SfxKind.BrakeSqueakLoop: d = BrakeSqueakLoop(r, rate); break;
                case SfxKind.ReverseBeepLoop: d = ReverseBeepLoop(rate); break;
                case SfxKind.RoadRumbleLoop: d = RoadRumbleLoop(r, rate); break;

                case SfxKind.UiHover: d = UiHover(r, rate); break;
                case SfxKind.UiClick: d = UiClick(r, rate); break;
                case SfxKind.UiConfirm: d = UiNotes(rate, new[] { 77f, 84f }, 0.075f, 0.55f); break;
                case SfxKind.UiBack: d = UiNotes(rate, new[] { 84f, 77f }, 0.075f, 0.42f); break;
                case SfxKind.UiOpen: d = UiSlide(r, rate, true); break;
                case SfxKind.UiClose: d = UiSlide(r, rate, false); break;
                case SfxKind.UiToast: d = UiToast(rate); break;
                case SfxKind.UiDenied: d = UiDenied(r, rate); break;
                case SfxKind.UiTick: d = UiTick(r, rate); break;
                case SfxKind.UiCoins: d = Coins(r, rate, 5); break;
                case SfxKind.UiSlider: d = UiSliderTick(r, rate); break;

                case SfxKind.StingTheftSeen: d = StingTheft(r, rate); break;
                case SfxKind.JingleWin: d = JingleWin(r, rate); break;
                case SfxKind.JingleFail: d = SadTrombone(rate); break;
                case SfxKind.JingleDelivered: d = KaChing(r, rate); break;
                case SfxKind.PhoneDial: d = PhoneDial(rate); break;
                case SfxKind.SirenLoop: d = SirenLoop(rate); break;

                case SfxKind.WindLoop: d = WindLoop(r, rate); break;
                case SfxKind.BirdCall: d = Bird(r, rate, variant); break;
                case SfxKind.CarPass: d = CarPass(r, rate); break;
                case SfxKind.RoomToneLoop: d = RoomToneLoop(r, rate); break;

                case SfxKind.RockCreakLoop: d = RockCreakLoop(r, rate); break;
                case SfxKind.PageTurn: d = PageTurn(r, rate); break;
                case SfxKind.TeaClink: d = TeaClink(r, rate); break;
                case SfxKind.SipTea: d = SipTea(r, rate); break;
                case SfxKind.PotBubbleLoop: d = PotBubbleLoop(r, rate); break;
                case SfxKind.WaterPourLoop: d = WaterPourLoop(r, rate); break;
                case SfxKind.MatchStrike: d = MatchStrike(r, rate); break;
                case SfxKind.FireCrackleLoop: d = FireCrackleLoop(r, rate); break;
                case SfxKind.TvMurmurLoop: d = TvMurmurLoop(r, rate); break;
                default: d = new float[1]; break;
            }
            return new Rendered { samples = d, rate = rate, loop = loop };
        }

        public enum Surface { Wood, Stone, Grass, Carpet, Metal }

        // ================================================================ footsteps

        // A step is a heel and a toe, 60 to 90 ms apart. A landing is both feet at once and
        // twice the weight.
        static float[] Step(Surface s, bool landing, Rng r, int rate)
        {
            var d = Buf.Seconds(landing ? 0.5f : 0.3f, rate);
            float toe = r.Range(0.055f, 0.09f);
            float w = landing ? 1.4f : 1f;
            switch (s)
            {
                case Surface.Wood:
                    // A floorboard: a hollow knock (110 to 160 Hz), the board's own ring around
                    // 500 Hz and the heel's "tock". The low knock is what headphones feel; the
                    // ring and the tock are what a laptop speaker can actually play, so they
                    // carry most of the energy.
                    Foot(d, rate, r, 0f, w, (at, g) =>
                    {
                        float body = r.Range(105f, 150f);
                        Gen.Modes(d, rate, at, new[] { body, body * 2.35f, body * 4.1f }, new[] { 0.045f, 0.03f, 0.015f }, new[] { 0.35f * g, 0.3f * g, 0.2f * g });
                        float board = r.Range(430f, 580f);
                        Gen.Modes(d, rate, at, new[] { board, board * 1.63f, board * 2.9f }, new[] { 0.03f, 0.018f, 0.01f }, new[] { 0.55f * g, 0.35f * g, 0.2f * g });
                        Gen.NoiseBurst(d, rate, r, at, r.Range(900f, 1300f), 1.0f, 0.0008f, 0.016f, 3.2f * g);
                        Gen.NoiseBurst(d, rate, r, at, r.Range(2600f, 3400f), 0.9f, 0.0004f, 0.005f, 1.6f * g);
                    }, toe, landing);
                    break;
                case Surface.Stone:
                    // A sharp click and a little grit: sand under a boot on a flagstone.
                    Foot(d, rate, r, 0f, w, (at, g) =>
                    {
                        Gen.NoiseBurst(d, rate, r, at, r.Range(2800f, 3600f), 0.8f, 0.0004f, 0.007f, 1.6f * g);
                        Gen.NoiseBurst(d, rate, r, at, 1100f, 1f, 0.001f, 0.018f, 1.4f * g);
                        Gen.Thump(d, rate, at, 120f, 80f, 0.02f, 0.02f, 0.2f * g);
                        Grit(d, rate, r, at + 0.004f, 0.06f, 7, 0.25f * g);
                    }, toe, landing);
                    break;
                case Surface.Grass:
                    // A swish with a few crackles of stems, and almost no low end.
                    Foot(d, rate, r, 0f, w, (at, g) =>
                    {
                        Gen.NoiseBurst(d, rate, r, at, r.Range(2000f, 3000f), 0.6f, 0.012f, 0.05f, 0.9f * g);
                        Gen.NoiseBurst(d, rate, r, at, 700f, 0.7f, 0.006f, 0.03f, 0.35f * g);
                        Grit(d, rate, r, at, 0.1f, 18, 0.18f * g);
                        Gen.Thump(d, rate, at, 90f, 60f, 0.02f, 0.03f, 0.25f * g);
                    }, toe, landing);
                    break;
                case Surface.Carpet:
                    // Muffled: a soft thump and the sole brushing the pile.
                    Foot(d, rate, r, 0f, w, (at, g) =>
                    {
                        Gen.Thump(d, rate, at, 110f, 70f, 0.02f, 0.04f, 0.35f * g);
                        Gen.NoiseBurst(d, rate, r, at, 420f, 0.7f, 0.003f, 0.035f, 1.6f * g);
                        Gen.NoiseBurst(d, rate, r, at + 0.008f, r.Range(1300f, 1900f), 0.8f, 0.006f, 0.03f, 1.1f * g);
                    }, toe, landing);
                    break;
                default:
                    // The truck bed and its ramp: a hollow sheet of steel.
                    Foot(d, rate, r, 0f, w, (at, g) =>
                    {
                        float f = r.Range(160f, 200f);
                        Gen.Modes(d, rate, at, new[] { f, f * 2.29f, f * 3.83f, f * 6.2f, f * 9.4f, f * 13.1f },
                                  new[] { 0.1f, 0.09f, 0.07f, 0.05f, 0.035f, 0.025f },
                                  new[] { 0.5f * g, 0.45f * g, 0.4f * g, 0.35f * g, 0.25f * g, 0.15f * g });
                        Gen.NoiseBurst(d, rate, r, at, 2500f, 0.8f, 0.0004f, 0.005f, 1.6f * g);
                    }, toe, landing);
                    break;
            }
            return Buf.Finish(d, landing ? 0.9f : 0.7f, rate);
        }

        static void Foot(float[] d, int rate, Rng r, float at, float weight, Action<float, float> strike, float toe, bool landing)
        {
            strike(at, weight * r.Range(0.85f, 1f));
            if (landing)
            {
                // Both feet, then the knees: one strike, a second 30 to 50 ms later, and a
                // low thud of the whole body.
                strike(at + r.Range(0.03f, 0.05f), weight * 0.8f);
                Gen.Thump(d, rate, at, 90f, 50f, 0.04f, 0.09f, 0.5f);
            }
            else
            {
                strike(at + toe, weight * r.Range(0.35f, 0.55f));
            }
        }

        // Sparse tiny clicks: grit, stems, crackles.
        static void Grit(float[] d, int rate, Rng r, float at, float span, int count, float gain)
        {
            for (int i = 0; i < count; i++)
            {
                float t = at + span * r.Value * r.Value;
                Gen.NoiseBurst(d, rate, r, t, r.Range(3500f, 7000f), 1.5f, 0.0002f, r.Range(0.0008f, 0.002f), gain * r.Range(0.4f, 1f));
            }
        }

        // Her slippers: a soft pat, then the sole sliding on ("shh-thp"). The slide is what
        // carries through a wall, so it is the loud part, on purpose: you should hear her come.
        static float[] Slipper(Surface s, Rng r, int rate)
        {
            var d = Buf.Seconds(0.36f, rate);
            float scuffHz = s == Surface.Stone ? 2600f : s == Surface.Grass ? 2300f : s == Surface.Carpet ? 950f : 1600f;
            float slideAt = r.Range(0.0f, 0.02f);
            Gen.NoiseBurst(d, rate, r, slideAt, scuffHz * r.Range(0.9f, 1.1f), 0.7f, r.Range(0.03f, 0.05f), 0.055f, 0.9f);
            Gen.NoiseBurst(d, rate, r, slideAt, scuffHz * 0.45f, 0.8f, 0.03f, 0.05f, 0.35f);
            float pat = slideAt + r.Range(0.05f, 0.08f);
            Gen.Thump(d, rate, pat, 120f, 80f, 0.015f, 0.025f, 0.45f);
            if (s == Surface.Wood)
                Gen.Modes(d, rate, pat, new[] { 135f, 320f }, new[] { 0.04f, 0.02f }, new[] { 0.3f, 0.12f });
            if (s == Surface.Grass) Grit(d, rate, r, slideAt, 0.1f, 10, 0.12f);
            if (s == Surface.Stone) Grit(d, rate, r, slideAt, 0.08f, 6, 0.15f);
            // The second, lazier half of the shuffle.
            Gen.NoiseBurst(d, rate, r, pat + r.Range(0.07f, 0.11f), scuffHz * 0.85f, 0.7f, 0.02f, 0.04f, 0.35f);
            return Buf.Finish(d, 0.75f, rate);
        }

        // Stick-slip friction: a train of tiny impulses at a slowly changing rate, rung
        // through two or three wooden resonances. Floorboards, hinges, the rocking chair.
        static float[] Creak(Rng r, int rate, float seconds, float pulseHz, float sweep, float peak,
                             float res1 = 650f, float res2 = 1400f, float res3 = 2300f)
        {
            var d = Buf.Seconds(seconds + 0.08f, rate);
            var b1 = new Svf(); b1.Set(res1 * r.Range(0.9f, 1.1f), 9f, rate);
            var b2 = new Svf(); b2.Set(res2 * r.Range(0.9f, 1.1f), 7f, rate);
            var b3 = new Svf(); b3.Set(res3 * r.Range(0.9f, 1.1f), 5f, rate);
            float next = 0f;
            float shape = r.Range(0f, 1f);
            int n = (int)(seconds * rate);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float x = Math.Min(1f, t / seconds);
                float imp = 0f;
                if (i < n && t >= next)
                {
                    // The rate rises then falls (or the reverse), like a hinge under a hand.
                    float curve = MathF.Sin(MathF.PI * (x * 0.8f + shape * 0.2f));
                    float hz = pulseHz * (1f + sweep * (curve - 0.5f));
                    next = t + 1f / Math.Max(10f, hz) * r.Range(0.85f, 1.15f);
                    imp = r.Range(0.6f, 1f);
                }
                float env = i < n ? Env.Bump(t, seconds) : 0f;
                float y = b1.Band(imp) * 1f + b2.Band(imp) * 0.7f + b3.Band(imp) * 0.35f;
                d[i] = y * (0.3f + 0.7f * env);
            }
            return Buf.Finish(d, peak, rate, 0.02f);
        }

        // ================================================================ handling

        static float[] ImpactSoft(Rng r, int rate)
        {
            var d = Buf.Seconds(0.25f, rate);
            Gen.NoiseBurst(d, rate, r, 0f, r.Range(900f, 1400f), 0.9f, 0.0006f, 0.014f, 2.2f);
            Gen.Thump(d, rate, 0f, r.Range(150f, 190f), 110f, 0.01f, 0.03f, 0.3f);
            Gen.NoiseBurst(d, rate, r, 0.002f, 450f, 0.8f, 0.001f, 0.02f, 1.2f);
            return Buf.Finish(d, 0.6f, rate);
        }

        static float[] ImpactWood(Rng r, int rate)
        {
            var d = Buf.Seconds(0.45f, rate);
            float f = r.Range(85f, 120f);
            Gen.Modes(d, rate, 0f, new[] { f, f * 2.2f, f * 3.6f, f * 5.3f }, new[] { 0.08f, 0.06f, 0.045f, 0.03f }, new[] { 0.5f, 0.45f, 0.4f, 0.3f });
            // The panel's own clack, a few hundred hertz up: what says "wood" and not "thud".
            float p = r.Range(380f, 520f);
            Gen.Modes(d, rate, 0f, new[] { p, p * 1.7f, p * 2.6f }, new[] { 0.04f, 0.025f, 0.015f }, new[] { 0.5f, 0.35f, 0.2f });
            Gen.NoiseBurst(d, rate, r, 0f, 800f, 0.8f, 0.0008f, 0.025f, 2.6f);
            Gen.NoiseBurst(d, rate, r, 0f, 2500f, 1f, 0.0004f, 0.006f, 1.4f);
            // A corner lands a moment after the first edge.
            if (r.Chance(0.6f)) Gen.NoiseBurst(d, rate, r, r.Range(0.03f, 0.07f), 900f, 0.9f, 0.0008f, 0.015f, 1.2f);
            return Buf.Finish(d, 0.8f, rate);
        }

        static float[] ImpactHeavy(Rng r, int rate)
        {
            var d = Buf.Seconds(0.7f, rate);
            Gen.Thump(d, rate, 0f, 75f, 42f, 0.05f, 0.14f, 0.8f);
            Gen.Modes(d, rate, 0f, new[] { 62f, 128f, 215f, 330f }, new[] { 0.14f, 0.09f, 0.06f, 0.04f }, new[] { 0.4f, 0.4f, 0.35f, 0.3f });
            // The frame of the thing clacks as it lands, and everything inside it rattles.
            Gen.Modes(d, rate, 0f, new[] { 310f, 540f, 870f }, new[] { 0.05f, 0.035f, 0.02f }, new[] { 0.45f, 0.35f, 0.2f });
            Gen.NoiseBurst(d, rate, r, 0f, 420f, 0.7f, 0.002f, 0.06f, 1.8f);
            Gen.NoiseBurst(d, rate, r, 0f, 1300f, 0.9f, 0.001f, 0.02f, 1.6f);
            Gen.NoiseBurst(d, rate, r, r.Range(0.04f, 0.06f), 1200f, 1f, 0.001f, 0.02f, 1.2f);
            Gen.NoiseBurst(d, rate, r, r.Range(0.09f, 0.13f), 1600f, 1f, 0.001f, 0.015f, 0.8f);
            return Buf.Finish(d, 0.95f, rate);
        }

        static float[] ImpactMetal(Rng r, int rate)
        {
            var d = Buf.Seconds(1f, rate);
            float f = r.Range(480f, 620f);
            Gen.Modes(d, rate, 0f, new[] { f, f * 2.57f, f * 4.21f, f * 6.67f, f * 9.6f },
                      new[] { 0.35f, 0.25f, 0.18f, 0.12f, 0.08f }, new[] { 0.7f, 0.5f, 0.35f, 0.25f, 0.15f });
            Gen.NoiseBurst(d, rate, r, 0f, 3000f, 0.8f, 0.0003f, 0.004f, 0.9f);
            return Buf.Finish(d, 0.7f, rate);
        }

        static float[] Clink(Rng r, int rate, float f, float peak)
        {
            var d = Buf.Seconds(0.7f, rate);
            Gen.Modes(d, rate, 0f, new[] { f, f * 2.36f, f * 3.52f, f * 4.9f }, new[] { 0.28f, 0.16f, 0.1f, 0.06f }, new[] { 0.8f, 0.55f, 0.35f, 0.2f });
            Gen.NoiseBurst(d, rate, r, 0f, 5000f, 1f, 0.0002f, 0.002f, 0.4f);
            return Buf.Finish(d, peak, rate);
        }

        static float[] ImpactCeramic(Rng r, int rate)
        {
            var d = Buf.Seconds(0.35f, rate);
            float f = r.Range(850f, 1050f);
            Gen.Modes(d, rate, 0f, new[] { f, f * 2.5f, f * 4.1f }, new[] { 0.08f, 0.05f, 0.03f }, new[] { 0.8f, 0.5f, 0.3f });
            Gen.NoiseBurst(d, rate, r, 0f, 2200f, 1f, 0.0004f, 0.006f, 0.8f);
            return Buf.Finish(d, 0.65f, rate);
        }

        static float[] ImpactFabric(Rng r, int rate)
        {
            var d = Buf.Seconds(0.3f, rate);
            Gen.NoiseBurst(d, rate, r, 0f, 420f, 0.5f, 0.005f, 0.05f, 1.4f);
            Gen.NoiseBurst(d, rate, r, 0.004f, 1500f, 0.7f, 0.004f, 0.03f, 0.9f);   // the cushion's whump
            Gen.Thump(d, rate, 0f, 100f, 70f, 0.02f, 0.04f, 0.35f);
            return Buf.Finish(d, 0.55f, rate);
        }

        // Furniture dragged across a floor: a low judder of wood on wood, with the odd squeak.
        static float[] ScrapeLoop(Rng r, int rate)
        {
            float loopSec = 2f;
            var longer = Buf.Seconds(loopSec + 0.2f, rate);
            var bp = Biquad.BandPass(420f, 0.9f, rate);
            var bp2 = Biquad.BandPass(1100f, 1.2f, rate);
            var squeak = new Svf(); squeak.Set(1350f, 14f, rate);
            float judder = 0f, next = 0f, level = 0.7f;
            for (int i = 0; i < longer.Length; i++)
            {
                float t = i / (float)rate;
                if (t >= next)
                {
                    next = t + r.Range(0.03f, 0.08f);
                    level = r.Range(0.35f, 1f);
                }
                judder += (level - judder) * 0.004f;
                float n = r.Bipolar;
                float squeakImp = r.Chance(0.0009f) ? 1f : 0f;
                longer[i] = bp.Process(n) * judder * 1.2f + bp2.Process(n) * judder * 0.3f + squeak.Band(squeakImp) * 0.6f;
            }
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.2f * rate)), 0.6f, rate, 0f);
        }

        static float[] Whoosh(Rng r, int rate)
        {
            float len = r.Range(0.3f, 0.42f);
            var d = Buf.Seconds(len + 0.05f, rate);
            var f = new Svf();
            float lo = r.Range(350f, 500f), hi = r.Range(1500f, 2200f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float x = Math.Min(1f, t / len);
                if ((i & 31) == 0) f.Set(lo + (hi - lo) * MathF.Sin(MathF.PI * x), 1.3f, rate);
                d[i] = f.Band(r.Bipolar) * Env.Bump(t, len);
            }
            return Buf.Finish(d, 0.55f, rate);
        }

        static float[] Rustle(Rng r, int rate, float seconds, int grains, float peak, bool thump)
        {
            var d = Buf.Seconds(seconds + 0.05f, rate);
            // A soft cloth slide under the grains.
            Gen.NoiseBurst(d, rate, r, 0f, 1200f, 0.6f, seconds * 0.3f, seconds * 0.25f, 0.35f);
            for (int g = 0; g < grains; g++)
                Gen.NoiseBurst(d, rate, r, r.Value * seconds * 0.85f, r.Range(1800f, 4800f), 0.8f,
                               r.Range(0.002f, 0.006f), r.Range(0.008f, 0.02f), r.Range(0.3f, 0.8f));
            if (thump) Gen.Thump(d, rate, 0.01f, 140f, 100f, 0.01f, 0.025f, 0.4f);
            return Buf.Finish(d, peak, rate);
        }

        static float[] PocketPat(Rng r, int rate)
        {
            var d = Buf.Seconds(0.2f, rate);
            Gen.Thump(d, rate, 0f, 130f, 95f, 0.01f, 0.03f, 0.4f);
            Gen.NoiseBurst(d, rate, r, 0f, 700f, 0.8f, 0.001f, 0.02f, 1.4f);
            Gen.NoiseBurst(d, rate, r, 0.01f, 2600f, 0.8f, 0.004f, 0.03f, 0.9f);   // the pocket's fabric
            return Buf.Finish(d, 0.5f, rate);
        }

        static float[] KeyJingle(Rng r, int rate)
        {
            var d = Buf.Seconds(0.6f, rate);
            int hits = r.Range(7, 12);
            for (int h = 0; h < hits; h++)
            {
                float at = 0.35f * r.Value * r.Value;
                float f = r.Range(2600f, 4700f);
                float g = r.Range(0.3f, 1f);
                Gen.Modes(d, rate, at, new[] { f, f * 1.51f, f * 2.33f, f * 3.1f },
                          new[] { r.Range(0.03f, 0.07f), 0.03f, 0.02f, 0.015f }, new[] { 0.6f * g, 0.4f * g, 0.25f * g, 0.15f * g });
            }
            return Buf.Finish(d, 0.5f, rate);
        }

        static float[] Gulp(Rng r, int rate)
        {
            var d = Buf.Seconds(0.3f, rate);
            // The throat: a small wet click.
            Gen.NoiseBurst(d, rate, r, 0f, 900f, 1.5f, 0.0008f, 0.006f, 0.6f);
            // The liquid: a blob whose pitch jumps up as it goes down.
            float phase = 0f;
            float f0 = r.Range(170f, 220f), f1 = r.Range(380f, 480f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate - 0.012f;
                if (t < 0f) continue;
                float f = f0 + (f1 - f0) * Env.Smooth(t / 0.06f);
                phase += 2f * MathF.PI * f / rate;
                d[i] += MathF.Sin(phase) * Env.AD(t, 0.006f, 0.045f) * 0.9f;
            }
            Gen.Thump(d, rate, 0.02f, 110f, 85f, 0.02f, 0.04f, 0.4f);
            return Buf.Finish(d, 0.55f, rate);
        }

        static float[] BottleSmash(Rng r, int rate)
        {
            var d = Buf.Seconds(0.7f, rate);
            Gen.NoiseBurst(d, rate, r, 0f, 4200f, 0.7f, 0.0005f, 0.03f, 0.9f);
            for (int p = 0; p < 10; p++)
            {
                float at = 0.25f * r.Value * r.Value;
                float f = r.Range(2400f, 6500f);
                Gen.Modes(d, rate, at, new[] { f, f * 1.7f }, new[] { r.Range(0.02f, 0.06f), 0.02f }, new[] { r.Range(0.12f, 0.3f), 0.1f });
            }
            // The beer: a short wet splash under the glass.
            Gen.NoiseBurst(d, rate, r, 0.01f, 800f, 0.6f, 0.01f, 0.12f, 0.6f);
            return Buf.Finish(d, 0.75f, rate);
        }

        static float[] SmokeInhale(Rng r, int rate)
        {
            float len = 0.95f;
            var d = Buf.Seconds(len + 0.1f, rate);
            var air = Biquad.BandPass(1500f, 0.8f, rate);
            var mouth = Biquad.BandPass(650f, 1.2f, rate);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float env = Env.ASR(t, len, 0.35f, 0.12f);
                float n = r.Bipolar;
                d[i] = (air.Process(n) * 0.6f + mouth.Process(n) * 0.4f) * env * 0.8f;
            }
            // The paper and the ember: crackles, more of them as the draw gets deeper.
            for (int c = 0; c < 28; c++)
            {
                float at = len * MathF.Sqrt(r.Value);
                Gen.NoiseBurst(d, rate, r, at, r.Range(3000f, 6500f), 1.5f, 0.0002f, r.Range(0.001f, 0.003f), r.Range(0.2f, 0.6f));
            }
            // The lips let go.
            Gen.NoiseBurst(d, rate, r, len - 0.02f, 1800f, 1.5f, 0.0005f, 0.005f, 0.3f);
            return Buf.Finish(d, 0.5f, rate);
        }

        static float[] SmokeCrackleLoop(Rng r, int rate)
        {
            float loopSec = 2f;
            var longer = Buf.Seconds(loopSec + 0.1f, rate);
            var air = Biquad.BandPass(1800f, 0.7f, rate);
            for (int i = 0; i < longer.Length; i++) longer[i] = air.Process(r.Bipolar) * 0.06f;
            for (int c = 0; c < 26; c++)
                Gen.NoiseBurst(longer, rate, r, r.Value * loopSec, r.Range(3000f, 7000f), 1.5f, 0.0002f, r.Range(0.001f, 0.003f), r.Range(0.2f, 0.7f));
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.1f * rate)), 0.4f, rate, 0f);
        }

        // "Haaaa": whispered through the shape of an open vowel, long and relaxed.
        static float[] SmokeExhale(Rng r, int rate)
        {
            float len = r.Range(1.2f, 1.5f);
            var d = Buf.Seconds(len + 0.1f, rate);
            var f1 = Biquad.BandPass(700f, 3f, rate);
            var f2 = Biquad.BandPass(1150f, 4f, rate);
            var air = Biquad.BandPass(2500f, 0.8f, rate);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float env = Env.AD(t, 0.07f, len * 0.35f) * Env.ASR(t, len, 0f, 0.3f);
                float n = r.Bipolar;
                d[i] = (f1.Process(n) * 1.2f + f2.Process(n) * 0.7f + air.Process(n) * 0.35f) * env;
            }
            Gen.Thump(d, rate, 0f, 160f, 120f, 0.01f, 0.02f, 0.25f);   // the lips part
            return Buf.Finish(d, 0.5f, rate);
        }

        static float[] Cough(Rng r, int rate)
        {
            var d = Buf.Seconds(0.8f, rate);
            float at = 0f;
            for (int c = 0; c < 3; c++)
            {
                float g = 1f - c * 0.25f;
                Gen.NoiseBurst(d, rate, r, at, 600f, 2f, 0.004f, 0.05f, 1.2f * g);
                Gen.NoiseBurst(d, rate, r, at, 1500f, 3f, 0.004f, 0.04f, 0.6f * g);
                Gen.NoiseBurst(d, rate, r, at, 2600f, 3f, 0.003f, 0.03f, 0.3f * g);
                Gen.Thump(d, rate, at, 170f, 120f, 0.02f, 0.03f, 0.2f * g);
                at += r.Range(0.18f, 0.25f);
            }
            return Buf.Finish(d, 0.6f, rate);
        }

        // ================================================================ doors

        static float[] LatchClick(Rng r, int rate)
        {
            var d = Buf.Seconds(0.2f, rate);
            Gen.Modes(d, rate, 0f, new[] { 2900f, 4700f }, new[] { 0.015f, 0.01f }, new[] { 0.6f, 0.35f });
            Gen.NoiseBurst(d, rate, r, 0f, 2400f, 1f, 0.0003f, 0.003f, 0.8f);
            Gen.Thump(d, rate, 0f, 380f, 300f, 0.005f, 0.012f, 0.3f);
            Gen.Modes(d, rate, 0.055f, new[] { 3300f, 5200f }, new[] { 0.012f, 0.008f }, new[] { 0.35f, 0.2f });
            return Buf.Finish(d, 0.55f, rate);
        }

        static float[] DoorCreak(Rng r, int rate, int variant)
        {
            // Four hinges with four characters: a low groan, a mid whine, a two-part creak and
            // a short squeak.
            switch (variant % 4)
            {
                case 0: return Creak(r, rate, 0.75f, 40f, 0.7f, 0.6f, 420f, 950f, 1800f);
                case 1: return Creak(r, rate, 0.55f, 95f, 0.9f, 0.55f, 800f, 1650f, 2900f);
                case 2:
                {
                    var a = Creak(r, rate, 0.32f, 60f, 0.5f, 0.55f, 600f, 1250f, 2200f);
                    var b = Creak(r, rate, 0.4f, 75f, 0.6f, 0.5f, 650f, 1400f, 2400f);
                    var d = Buf.Seconds(0.85f, rate);
                    Buf.Mix(d, a, 0, 1f);
                    Buf.Mix(d, b, (int)(0.38f * rate), 0.9f);
                    return Buf.Finish(d, 0.6f, rate);
                }
                default: return Creak(r, rate, 0.28f, 140f, 0.6f, 0.5f, 1100f, 2300f, 3600f);
            }
        }

        static float[] SashSlide(Rng r, int rate)
        {
            float len = 0.42f;
            var d = Buf.Seconds(len + 0.1f, rate);
            var bp = Biquad.BandPass(900f, 1f, rate);
            float next = 0f, level = 1f, jud = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                if (t >= next) { next = t + r.Range(0.015f, 0.035f); level = r.Range(0.3f, 1f); }
                jud += (level - jud) * 0.01f;
                d[i] = bp.Process(r.Bipolar) * jud * Env.Bump(t, len);
            }
            Gen.Modes(d, rate, len - 0.02f, new[] { 180f, 420f }, new[] { 0.04f, 0.02f }, new[] { 0.6f, 0.3f });
            return Buf.Finish(d, 0.5f, rate);
        }

        static float[] LockClack(Rng r, int rate)
        {
            var d = Buf.Seconds(0.25f, rate);
            Gen.Modes(d, rate, 0f, new[] { 850f, 2100f, 3900f }, new[] { 0.05f, 0.03f, 0.02f }, new[] { 0.8f, 0.5f, 0.3f });
            Gen.NoiseBurst(d, rate, r, 0f, 1500f, 1f, 0.0004f, 0.006f, 0.8f);
            Gen.Modes(d, rate, 0.07f, new[] { 1200f, 3100f }, new[] { 0.03f, 0.015f }, new[] { 0.5f, 0.3f });
            return Buf.Finish(d, 0.6f, rate);
        }

        // ================================================================ the truck

        // A four-cylinder diesel at idle fires 25 times a second: each firing is a low
        // resonant puff of exhaust with a tick of injector clatter. One exact second of it
        // loops; the pitch of the AudioSource makes the revs.
        static float[] EngineLoop(Rng r, int rate, bool load)
        {
            float loopSec = 1f;
            var longer = Buf.Seconds(loopSec + 0.25f, rate);
            float fire = 25f;
            var ex1 = new Svf(); ex1.Set(load ? 110f : 85f, 3f, rate);
            var ex2 = new Svf(); ex2.Set(load ? 240f : 175f, 4f, rate);
            var roar = new OnePole(); roar.Set(load ? 700f : 350f, rate);
            var clatter = Biquad.BandPass(3200f, 1.2f, rate);
            float next = 0f;
            float clat = 0f;
            float lastAmp = 1f;
            for (int i = 0; i < longer.Length; i++)
            {
                float t = i / (float)rate;
                float imp = 0f;
                if (t >= next)
                {
                    next += 1f / fire;
                    // Cylinders are not identical: one of four is a touch weaker.
                    int cyl = (int)(t * fire) % 4;
                    lastAmp = (cyl == 2 ? 0.8f : 1f) * r.Range(0.9f, 1.1f);
                    imp = lastAmp;
                    clat = 1f;
                }
                clat *= 0.992f;
                float n = r.Bipolar;
                float y = ex1.Band(imp * 6f) * 1.0f + ex2.Band(imp * 4f) * 0.5f;
                y += roar.Low(n) * (load ? 0.9f : 0.35f) * (0.6f + 0.4f * lastAmp);
                y += clatter.Process(n) * clat * (load ? 0.35f : 0.22f);
                longer[i] = y;
            }
            var d = Buf.Loop(longer, (int)(loopSec * rate), (int)(0.2f * rate));
            if (load)
            {
                // A faint turbo whistle on the loaded loop.
                for (int i = 0; i < d.Length; i++) d[i] += MathF.Sin(2f * MathF.PI * 1800f * i / rate) * 0.012f;
            }
            return Buf.Finish(d, 0.8f, rate, 0f);
        }

        static float[] EngineStart(Rng r, int rate)
        {
            var d = Buf.Seconds(1.8f, rate);
            // The starter: a whirring motor, stuttering as the pistons fight it.
            float ph = 0f;
            var lp = new OnePole(); lp.Set(2600f, rate);
            for (int i = 0; i < (int)(0.95f * rate); i++)
            {
                float t = i / (float)rate;
                ph += 2f * MathF.PI * (160f + 20f * MathF.Sin(t * 3f)) / rate;
                float saw = (ph / (2f * MathF.PI)) % 1f * 2f - 1f;
                float crank = 0.55f + 0.45f * MathF.Sin(2f * MathF.PI * 10f * t);
                d[i] += lp.Low(saw) * crank * Env.ASR(t, 0.95f, 0.05f, 0.1f) * 0.7f;
            }
            // Then it catches: firings speed up from a cough to a steady idle.
            float at = 0.8f;
            float rateHz = 8f;
            while (at < 1.75f)
            {
                float g = Math.Min(1f, (at - 0.75f) * 2.5f);
                Gen.Thump(d, rate, at, 110f, 80f, 0.01f, 0.03f, 0.5f * g);
                Gen.NoiseBurst(d, rate, r, at, 300f, 0.8f, 0.002f, 0.02f, 0.8f * g);
                Gen.NoiseBurst(d, rate, r, at, 800f, 1f, 0.001f, 0.012f, 0.9f * g);   // the chug
                rateHz = Math.Min(27f, rateHz * 1.12f);
                at += 1f / rateHz;
            }
            Buf.FadeOut(d, 0.25f, rate);
            return Buf.Finish(d, 0.75f, rate);
        }

        static float[] EngineStop(Rng r, int rate)
        {
            var d = Buf.Seconds(1.2f, rate);
            float at = 0f, rateHz = 25f, g = 1f;
            while (rateHz > 5f && at < 1f)
            {
                Gen.Thump(d, rate, at, 100f, 75f, 0.01f, 0.03f, 0.45f * g);
                Gen.NoiseBurst(d, rate, r, at, 280f, 0.8f, 0.002f, 0.02f, 0.7f * g);
                Gen.NoiseBurst(d, rate, r, at, 750f, 1f, 0.001f, 0.012f, 0.8f * g);
                at += 1f / rateHz;
                rateHz *= 0.9f;
                g *= 0.94f;
            }
            Gen.Thump(d, rate, at, 70f, 40f, 0.03f, 0.08f, 0.8f);   // it shudders to a stop
            return Buf.Finish(d, 0.7f, rate);
        }

        static float[] TruckDoor(Rng r, int rate)
        {
            var d = Buf.Seconds(0.6f, rate);
            Gen.Thump(d, rate, 0f, 90f, 60f, 0.02f, 0.08f, 1f);
            Gen.Modes(d, rate, 0f, new[] { 140f, 330f, 610f, 1180f }, new[] { 0.15f, 0.1f, 0.07f, 0.05f }, new[] { 0.6f, 0.45f, 0.3f, 0.2f });
            Gen.NoiseBurst(d, rate, r, 0f, 1400f, 0.8f, 0.0005f, 0.012f, 0.8f);
            Gen.Modes(d, rate, 0.012f, new[] { 3000f, 4600f }, new[] { 0.015f, 0.01f }, new[] { 0.6f, 0.4f });
            Gen.NoiseBurst(d, rate, r, 0.005f, 900f, 0.9f, 0.001f, 0.03f, 1.2f);   // the panel slams
            return Buf.Finish(d, 0.85f, rate);
        }

        static float[] AirBrake(Rng r, int rate)
        {
            var d = Buf.Seconds(0.9f, rate);
            var hi = Biquad.BandPass(3800f, 0.6f, rate);
            var mid = Biquad.BandPass(1500f, 0.8f, rate);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float env = t < 0.005f ? t / 0.005f : t < 0.25f ? 1f : MathF.Exp(-(t - 0.25f) / 0.14f);
                float n = r.Bipolar;
                d[i] = (hi.Process(n) + mid.Process(n) * 0.5f) * env;
            }
            return Buf.Finish(d, 0.6f, rate);
        }

        static float[] BrakeSqueakLoop(Rng r, int rate)
        {
            float loopSec = 1.5f;
            var longer = Buf.Seconds(loopSec + 0.2f, rate);
            float ph = 0f, ph2 = 0f;
            var bp = Biquad.BandPass(2400f, 4f, rate);
            for (int i = 0; i < longer.Length; i++)
            {
                float t = i / (float)rate;
                float wob = 2400f + 45f * MathF.Sin(2f * MathF.PI * 6f * t) + 20f * MathF.Sin(2f * MathF.PI * 1.3f * t);
                ph += 2f * MathF.PI * wob / rate;
                ph2 += 2f * MathF.PI * wob * 2.76f / rate;
                float amp = 0.7f + 0.3f * MathF.Sin(2f * MathF.PI * 0.8f * t);
                longer[i] = (MathF.Sin(ph) * 0.5f + MathF.Sin(ph2) * 0.08f + bp.Process(r.Bipolar) * 0.25f) * amp;
            }
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.2f * rate)), 0.35f, rate, 0f);
        }

        // The reversing beeper of every delivery truck. Lower (1 kHz) and longer (0.45 s)
        // than the grenade's 1.9 kHz, 70 ms beep, on purpose: one must never be mistaken for
        // the other.
        static float[] ReverseBeepLoop(int rate)
        {
            var d = Buf.Seconds(1f, rate);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float env = Env.ASR(t, 0.45f, 0.01f, 0.02f);
                float x = 2f * MathF.PI * 1040f * t;
                d[i] = (MathF.Sin(x) + MathF.Sin(3f * x) / 5f) * env;
            }
            return Buf.Finish(d, 0.5f, rate, 0f);
        }

        static float[] RoadRumbleLoop(Rng r, int rate)
        {
            float loopSec = 2f;
            var longer = Buf.Seconds(loopSec + 0.2f, rate);
            var pink = new Pink();
            var lp = new OnePole(); lp.Set(220f, rate);
            var tyre = Biquad.BandPass(1300f, 0.5f, rate);
            for (int i = 0; i < longer.Length; i++)
            {
                float p = pink.Next(r);
                longer[i] = lp.Low(p) * 2.2f + tyre.Process(r.Bipolar) * 0.12f;
            }
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.2f * rate)), 0.6f, rate, 0f);
        }

        // ================================================================ interface

        // Wood and cardboard, like the rest of the game: taps on a crate, not glassy beeps.
        static float[] UiHover(Rng r, int rate)
        {
            var d = Buf.Seconds(0.08f, rate);
            Gen.Modes(d, rate, 0f, new[] { 1900f, 4100f }, new[] { 0.018f, 0.009f }, new[] { 0.6f, 0.2f });
            Gen.NoiseBurst(d, rate, r, 0f, 3000f, 1f, 0.0003f, 0.002f, 0.3f);
            return Buf.Finish(d, 0.3f, rate);
        }

        static float[] UiClick(Rng r, int rate)
        {
            var d = Buf.Seconds(0.2f, rate);
            Gen.Modes(d, rate, 0f, new[] { 620f, 1450f, 2600f }, new[] { 0.05f, 0.03f, 0.015f }, new[] { 0.7f, 0.4f, 0.2f });
            Gen.Thump(d, rate, 0f, 190f, 150f, 0.01f, 0.025f, 0.5f);
            Gen.NoiseBurst(d, rate, r, 0f, 1200f, 0.9f, 0.0004f, 0.008f, 0.6f);
            return Buf.Finish(d, 0.55f, rate);
        }

        // A few marimba notes (MIDI numbers), a small step apart.
        static float[] UiNotes(int rate, float[] midi, float step, float peak)
        {
            var d = Buf.Seconds(step * midi.Length + 0.45f, rate);
            for (int i = 0; i < midi.Length; i++)
                Gen.Mallet(d, rate, i * step, Buf.Hz(midi[i]), 0.22f, i == midi.Length - 1 ? 1f : 0.8f);
            return Buf.Finish(d, peak, rate);
        }

        static float[] UiSlide(Rng r, int rate, bool open)
        {
            float len = 0.24f;
            var d = Buf.Seconds(len + 0.12f, rate);
            var f = new Svf();
            for (int i = 0; i < (int)(len * rate); i++)
            {
                float t = i / (float)rate;
                float x = t / len;
                if ((i & 31) == 0) f.Set(open ? 500f + 1300f * x : 1800f - 1300f * x, 1.4f, rate);
                d[i] = f.Band(r.Bipolar) * Env.Bump(t, len) * 0.8f;
            }
            // The flap of a box: at the end when opening, at the start when closing.
            Gen.Thump(d, rate, open ? len - 0.02f : 0f, 170f, 130f, 0.01f, 0.03f, 0.6f);
            Gen.NoiseBurst(d, rate, r, open ? len - 0.02f : 0f, 900f, 0.9f, 0.0005f, 0.01f, 0.4f);
            return Buf.Finish(d, 0.45f, rate);
        }

        static float[] UiToast(int rate)
        {
            var d = Buf.Seconds(0.6f, rate);
            Gen.Mallet(d, rate, 0f, Buf.Hz(81f), 0.3f, 1f, 5.4f, 0.25f);   // A5, a kalimba tine
            Gen.Modes(d, rate, 0f, new[] { Buf.Hz(81f) * 2.76f }, new[] { 0.12f }, new[] { 0.12f });
            return Buf.Finish(d, 0.4f, rate);
        }

        static float[] UiDenied(Rng r, int rate)
        {
            var d = Buf.Seconds(0.3f, rate);
            Gen.Modes(d, rate, 0f, new[] { 290f, 700f }, new[] { 0.04f, 0.02f }, new[] { 0.8f, 0.3f });
            Gen.Modes(d, rate, 0.09f, new[] { 250f, 610f }, new[] { 0.05f, 0.02f }, new[] { 0.7f, 0.25f });
            Gen.NoiseBurst(d, rate, r, 0f, 900f, 1f, 0.0005f, 0.006f, 0.4f);
            return Buf.Finish(d, 0.5f, rate);
        }

        static float[] UiTick(Rng r, int rate)
        {
            var d = Buf.Seconds(0.07f, rate);
            Gen.Modes(d, rate, 0f, new[] { 2900f, 5200f }, new[] { 0.01f, 0.006f }, new[] { 0.6f, 0.3f });
            Gen.NoiseBurst(d, rate, r, 0f, 4000f, 1f, 0.0002f, 0.0015f, 0.4f);
            return Buf.Finish(d, 0.4f, rate);
        }

        static float[] UiSliderTick(Rng r, int rate)
        {
            var d = Buf.Seconds(0.05f, rate);
            Gen.Modes(d, rate, 0f, new[] { 2400f }, new[] { 0.008f }, new[] { 1f });
            return Buf.Finish(d, 0.3f, rate);
        }

        static float[] Coins(Rng r, int rate, int count)
        {
            var d = Buf.Seconds(0.25f + count * 0.07f, rate);
            for (int c = 0; c < count; c++)
            {
                float f = r.Range(3600f, 4400f) * (1f + c * 0.04f);
                Gen.Modes(d, rate, c * r.Range(0.05f, 0.08f), new[] { f, f * 1.42f, f * 2.07f },
                          new[] { 0.09f, 0.06f, 0.04f }, new[] { 0.6f, 0.4f, 0.25f });
            }
            return Buf.Finish(d, 0.45f, rate);
        }

        // ================================================================ stingers

        // "Uh-oh": two pizzicato notes falling a minor third, and a knock.
        static float[] StingTheft(Rng r, int rate)
        {
            var d = Buf.Seconds(1f, rate);
            Gen.Pluck(d, rate, r, 0f, Buf.Hz(62f), 0.5f, 0.9f, 0.55f, 0.995f);
            Gen.Pluck(d, rate, r, 0.18f, Buf.Hz(59f), 0.7f, 1f, 0.5f, 0.995f);
            Gen.Modes(d, rate, 0.18f, new[] { 180f, 420f }, new[] { 0.06f, 0.03f }, new[] { 0.4f, 0.2f });
            return Buf.Finish(d, 0.6f, rate);
        }

        static float[] JingleWin(Rng r, int rate)
        {
            var d = Buf.Seconds(2.4f, rate);
            float[] up = { 77f, 81f, 84f, 89f };   // F5 A5 C6 F6
            for (int i = 0; i < up.Length; i++) Gen.Mallet(d, rate, i * 0.11f, Buf.Hz(up[i]), 0.5f, 0.8f, 5.4f, 0.3f);
            // The chord rings, over a plucked low F.
            float at = up.Length * 0.11f + 0.05f;
            foreach (float m in new[] { 65f, 69f, 72f, 77f }) Gen.Mallet(d, rate, at, Buf.Hz(m), 0.8f, 0.45f, 4f, 0.2f);
            Gen.Pluck(d, rate, r, at, Buf.Hz(41f), 1.5f, 0.8f, 0.35f, 0.998f);
            var verb = new TinyVerb(rate, 0.7f);
            for (int i = 0; i < d.Length; i++) d[i] += verb.Process(d[i]) * 0.9f;
            return Buf.Finish(d, 0.7f, rate, 0.3f);
        }

        // "Wah wah wah waaah": the funny failure (CLAUDE.md section 4), not a punishment.
        static float[] SadTrombone(int rate)
        {
            float[] notes = { 58f, 57f, 56f, 55f };   // Bb3 A3 Ab3 G3
            float[] lens = { 0.36f, 0.36f, 0.36f, 1.25f };
            float total = 0.2f;
            foreach (float l in lens) total += l;
            var d = Buf.Seconds(total, rate);
            var f = new Svf();
            float phase = 0f;
            float at = 0f;
            int pos = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                int len = (int)(lens[n] * rate);
                float hz = Buf.Hz(notes[n]);
                for (int i = 0; i < len && pos < d.Length; i++, pos++)
                {
                    float t = i / (float)rate;
                    float vib = n == notes.Length - 1 ? 1f + 0.018f * MathF.Sin(2f * MathF.PI * 5.5f * Math.Max(0f, t - 0.25f)) * Math.Min(1f, t) : 1f;
                    float bend = 1f - 0.03f * Env.Smooth(t / lens[n]);   // each note sags
                    phase += hz * vib * bend / rate;
                    phase -= MathF.Floor(phase);
                    float saw = phase * 2f - 1f;
                    // The plunger mute: the filter opens then closes on every note ("wah").
                    float open = Env.Bump(t, lens[n]);
                    if ((i & 15) == 0) f.Set(300f + 1500f * open, 2.2f, rate);
                    float env = Env.ASR(t, lens[n], 0.03f, n == notes.Length - 1 ? 0.5f : 0.05f);
                    d[pos] = f.Low(saw) * env;
                }
                at += lens[n];
            }
            return Buf.Finish(d, 0.7f, rate, 0.1f);
        }

        static float[] KaChing(Rng r, int rate)
        {
            var d = Buf.Seconds(1.3f, rate);
            // The drawer ratchet...
            for (int c = 0; c < 3; c++)
                Gen.Modes(d, rate, c * 0.045f, new[] { 1800f + c * 120f, 3700f }, new[] { 0.015f, 0.01f }, new[] { 0.5f, 0.3f });
            // ...and the bell.
            Gen.Modes(d, rate, 0.16f, new[] { 2093f, 5230f, 7900f, 4186f }, new[] { 0.6f, 0.3f, 0.15f, 0.4f }, new[] { 0.7f, 0.3f, 0.15f, 0.25f });
            var coins = Coins(r, rate, 4);
            Buf.Mix(d, coins, (int)(0.3f * rate), 0.6f);
            return Buf.Finish(d, 0.7f, rate, 0.1f);
        }

        // She dials the police: "1", "7" (the French police number), then the ringing tone.
        static float[] PhoneDial(int rate)
        {
            var d = Buf.Seconds(2.6f, rate);
            Tone(d, rate, 0f, 0.14f, 697f, 1209f, 0.5f);    // 1
            Tone(d, rate, 0.26f, 0.14f, 852f, 1209f, 0.5f); // 7
            Tone(d, rate, 0.8f, 1.2f, 440f, 440f, 0.35f);   // ring back
            var lp = Biquad.BandPass(1000f, 0.5f, rate);    // through a phone's little speaker
            for (int i = 0; i < d.Length; i++) d[i] = lp.Process(d[i]);
            return Buf.Finish(d, 0.45f, rate);
        }

        static void Tone(float[] d, int rate, float at, float len, float a, float b, float gain)
        {
            int start = (int)(at * rate);
            int n = Math.Min(d.Length - start, (int)(len * rate));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                d[start + i] += (MathF.Sin(2f * MathF.PI * a * t) + MathF.Sin(2f * MathF.PI * b * t)) * 0.5f * gain * Env.ASR(t, len, 0.005f, 0.01f);
            }
        }

        // A two-tone siren, far off, after she has called.
        static float[] SirenLoop(int rate)
        {
            float half = 0.7f;
            var d = Buf.Seconds(half * 2f, rate);
            var lp = new OnePole(); lp.Set(1800f, rate);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float hz = t < half ? 435f : 580f;
                ph += hz / rate;
                ph -= MathF.Floor(ph);
                float sq = MathF.Sin(2f * MathF.PI * ph) + MathF.Sin(6f * MathF.PI * ph) / 3f + MathF.Sin(10f * MathF.PI * ph) / 5f;
                float edge = Math.Min(1f, Math.Min(t % half, half - t % half) / 0.02f);
                d[i] = lp.Low(sq) * (0.4f + 0.6f * edge);
            }
            return Buf.Finish(d, 0.5f, rate, 0f);
        }

        // ================================================================ ambience

        // Wind in the garden: pink noise through a slowly breathing low-pass, with gusts that
        // bring a leafy hiss. Every modulation completes whole cycles in the loop's 12 seconds,
        // so the loop point is invisible.
        static float[] WindLoop(Rng r, int rate)
        {
            float loopSec = 12f;
            var longer = Buf.Seconds(loopSec + 0.5f, rate);
            var pink = new Pink();
            var f = new Svf();
            var leaves = Biquad.HighPass(3500f, 0.7f, rate);
            for (int i = 0; i < longer.Length; i++)
            {
                float t = i / (float)rate;
                float w1 = MathF.Sin(2f * MathF.PI * t / 12f);
                float w2 = MathF.Sin(2f * MathF.PI * t * 3f / 12f + 1.3f);
                float gust = 0.5f + 0.3f * w1 + 0.2f * w2;
                if ((i & 63) == 0) f.Set(420f + 1100f * gust, 0.8f, rate);
                float p = pink.Next(r);
                longer[i] = f.Low(p) * (0.5f + 0.5f * gust) * 2f + leaves.Process(r.Bipolar) * 0.14f * gust * gust;
            }
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.5f * rate)), 0.5f, rate, 0f);
        }

        static float[] Bird(Rng r, int rate, int variant)
        {
            var d = Buf.Seconds(1.6f, rate);
            switch (variant % 6)
            {
                case 0:   // "tweet tweet tweet"
                {
                    int n = r.Range(2, 5);
                    for (int i = 0; i < n; i++) Chirp(d, rate, i * 0.14f, r.Range(3000f, 3400f), r.Range(4500f, 5200f), 0.06f, 1f);
                    break;
                }
                case 1:   // a trill
                {
                    float c = r.Range(3800f, 4600f);
                    float ph = 0f;
                    for (int i = 0; i < (int)(0.45f * rate); i++)
                    {
                        float t = i / (float)rate;
                        float hz = c + 500f * MathF.Sin(2f * MathF.PI * 28f * t);
                        ph += 2f * MathF.PI * hz / rate;
                        d[i] += MathF.Sin(ph) * Env.ASR(t, 0.45f, 0.03f, 0.1f) * (0.6f + 0.4f * MathF.Sin(2f * MathF.PI * 28f * t));
                    }
                    break;
                }
                case 2:   // a falling whistle and a flick up
                    Chirp(d, rate, 0f, 3200f, 2200f, 0.28f, 1f);
                    Chirp(d, rate, 0.34f, 2600f, 3600f, 0.08f, 0.7f);
                    break;
                case 3:   // "chip chip chip chip"
                    for (int i = 0; i < 5; i++) Chirp(d, rate, i * 0.075f, 5200f, 4700f, 0.022f, 0.8f);
                    break;
                case 4:   // a wood pigeon, soft and low: "hoo-HOO-hoo"
                {
                    float[] at = { 0f, 0.42f, 0.95f };
                    float[] len = { 0.3f, 0.45f, 0.3f };
                    float[] hz = { 480f, 540f, 470f };
                    var lp = new OnePole(); lp.Set(1500f, rate);
                    for (int k = 0; k < 3; k++)
                    {
                        int s = (int)(at[k] * rate);
                        for (int i = 0; i < (int)(len[k] * rate) && s + i < d.Length; i++)
                        {
                            float t = i / (float)rate;
                            float y = MathF.Sin(2f * MathF.PI * hz[k] * t * (1f - 0.04f * t)) + r.Bipolar * 0.05f;
                            d[s + i] += lp.Low(y) * Env.ASR(t, len[k], 0.06f, 0.12f) * (k == 1 ? 1f : 0.7f);
                        }
                    }
                    break;
                }
                default:  // a robin's warble: a handful of quick gliding notes
                {
                    float at = 0f;
                    int n = r.Range(5, 9);
                    for (int i = 0; i < n; i++)
                    {
                        float a = r.Range(2600f, 5200f), b = a * r.Range(0.8f, 1.25f);
                        float len = r.Range(0.04f, 0.11f);
                        Chirp(d, rate, at, a, b, len, r.Range(0.5f, 1f));
                        at += len + r.Range(0.01f, 0.06f);
                    }
                    break;
                }
            }
            return Buf.Finish(d, 0.5f, rate, 0.05f);
        }

        static void Chirp(float[] d, int rate, float at, float fromHz, float toHz, float len, float gain)
        {
            int s = (int)(at * rate);
            int n = (int)(len * rate);
            float ph = 0f;
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float x = i / (float)n;
                float hz = fromHz + (toHz - fromHz) * x;
                ph += 2f * MathF.PI * hz / rate;
                d[s + i] += MathF.Sin(ph) * Env.Bump(x, 1f) * gain;
            }
        }

        // A car on the road beyond the hedge: it swells, passes, and fades, a little lower in
        // pitch on the way out.
        static float[] CarPass(Rng r, int rate)
        {
            float len = 6f;
            var d = Buf.Seconds(len, rate);
            var tyre = new Svf();
            var eng = new OnePole(); eng.Set(380f, rate);
            float ph = 0f;
            float mid = len * r.Range(0.45f, 0.55f);
            float baseHz = r.Range(70f, 95f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float x = (t - mid) / 1.3f;
                float near = MathF.Exp(-x * x);
                float doppler = 1f + 0.05f * -MathF.Tanh(x * 1.5f);
                ph += baseHz * doppler / rate;
                ph -= MathF.Floor(ph);
                if ((i & 63) == 0) tyre.Set(500f + 900f * near, 0.7f, rate);
                float y = tyre.Band(r.Bipolar) * 0.8f + eng.Low(ph * 2f - 1f) * 0.9f;
                d[i] = y * near;
            }
            return Buf.Finish(d, 0.5f, rate, 0.2f);
        }

        static float[] RoomToneLoop(Rng r, int rate)
        {
            float loopSec = 6f;
            var longer = Buf.Seconds(loopSec + 0.3f, rate);
            var pink = new Pink();
            var lp = new OnePole(); lp.Set(180f, rate);
            for (int i = 0; i < longer.Length; i++) longer[i] = lp.Low(pink.Next(r));
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.3f * rate)), 0.3f, rate, 0f);
        }

        // ================================================================ her activities

        static float[] RockCreakLoop(Rng r, int rate)
        {
            float loopSec = 2.8f;
            var longer = Buf.Seconds(loopSec + 0.2f, rate);
            var a = Creak(r, rate, 0.34f, 55f, 0.5f, 0.6f, 520f, 1150f, 2100f);
            var b = Creak(r, rate, 0.3f, 70f, 0.5f, 0.45f, 560f, 1250f, 2200f);
            Buf.Mix(longer, a, (int)(0.15f * rate), 1f);
            Buf.Mix(longer, b, (int)(1.55f * rate), 0.8f);
            // The runners on the floor at each end of the swing.
            Gen.Modes(longer, rate, 0.1f, new[] { 120f, 260f }, new[] { 0.05f, 0.03f }, new[] { 0.25f, 0.1f });
            Gen.Modes(longer, rate, 1.5f, new[] { 125f, 270f }, new[] { 0.05f, 0.03f }, new[] { 0.2f, 0.08f });
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.1f * rate)), 0.5f, rate, 0f);
        }

        static float[] PageTurn(Rng r, int rate)
        {
            float len = 0.38f;
            var d = Buf.Seconds(len + 0.1f, rate);
            var f = new Svf();
            for (int i = 0; i < (int)(len * rate); i++)
            {
                float t = i / (float)rate;
                if ((i & 31) == 0) f.Set(2200f + 2800f * (t / len), 1.1f, rate);
                d[i] = f.Band(r.Bipolar) * Env.Bump(t, len) * (0.6f + 0.4f * r.Value);
            }
            Gen.NoiseBurst(d, rate, r, len - 0.03f, 800f, 0.8f, 0.001f, 0.015f, 0.8f);   // the page lands
            return Buf.Finish(d, 0.45f, rate);
        }

        static float[] TeaClink(Rng r, int rate)
        {
            var d = Buf.Seconds(0.5f, rate);
            float f = r.Range(2400f, 2800f);
            Gen.Modes(d, rate, 0f, new[] { f, f * 2.05f, f * 3.1f }, new[] { 0.12f, 0.06f, 0.04f }, new[] { 0.7f, 0.35f, 0.2f });
            Gen.Modes(d, rate, r.Range(0.08f, 0.11f), new[] { f * 1.03f, f * 2.1f }, new[] { 0.08f, 0.04f }, new[] { 0.35f, 0.15f });
            Gen.NoiseBurst(d, rate, r, 0f, 4000f, 1f, 0.0002f, 0.002f, 0.3f);
            return Buf.Finish(d, 0.45f, rate);
        }

        static float[] SipTea(Rng r, int rate)
        {
            var d = Buf.Seconds(0.55f, rate);
            var bp = Biquad.BandPass(2300f, 2f, rate);
            for (int i = 0; i < (int)(0.4f * rate); i++)
            {
                float t = i / (float)rate;
                float warble = 0.6f + 0.4f * MathF.Sin(2f * MathF.PI * 16f * t);
                d[i] = bp.Process(r.Bipolar) * Env.ASR(t, 0.4f, 0.06f, 0.1f) * warble;
            }
            return Buf.Finish(d, 0.35f, rate);
        }

        static float[] PotBubbleLoop(Rng r, int rate)
        {
            float loopSec = 3f;
            var longer = Buf.Seconds(loopSec + 0.2f, rate);
            var lp = new OnePole(); lp.Set(700f, rate);
            for (int i = 0; i < longer.Length; i++) longer[i] = lp.Low(r.Bipolar) * 0.12f;
            for (int b = 0; b < 70; b++) Bubble(longer, rate, r, r.Value * loopSec, r.Range(180f, 520f), r.Range(0.2f, 0.7f));
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.2f * rate)), 0.45f, rate, 0f);
        }

        static void Bubble(float[] d, int rate, Rng r, float at, float hz, float gain)
        {
            int s = (int)(at * rate);
            int n = (int)(0.05f * rate);
            float ph = 0f;
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float t = i / (float)rate;
                ph += 2f * MathF.PI * hz * (1f + 6f * t) / rate;   // a bubble rises in pitch as it bursts
                d[s + i] += MathF.Sin(ph) * Env.AD(t, 0.002f, 0.012f) * gain;
            }
        }

        static float[] WaterPourLoop(Rng r, int rate)
        {
            float loopSec = 2f;
            var longer = Buf.Seconds(loopSec + 0.2f, rate);
            var bp = Biquad.BandPass(1400f, 0.7f, rate);
            for (int i = 0; i < longer.Length; i++)
            {
                float t = i / (float)rate;
                longer[i] = bp.Process(r.Bipolar) * (0.35f + 0.1f * MathF.Sin(2f * MathF.PI * 3f * t));
            }
            for (int b = 0; b < 160; b++) Bubble(longer, rate, r, r.Value * loopSec, r.Range(600f, 1800f), r.Range(0.1f, 0.4f));
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.2f * rate)), 0.45f, rate, 0f);
        }

        static float[] MatchStrike(Rng r, int rate)
        {
            var d = Buf.Seconds(1f, rate);
            var bp = Biquad.BandPass(3200f, 1f, rate);
            for (int i = 0; i < (int)(0.12f * rate); i++)
            {
                float t = i / (float)rate;
                d[i] += bp.Process(r.Bipolar) * Env.Bump(t, 0.12f) * r.Range(0.4f, 1f);
            }
            var flare = Biquad.BandPass(1100f, 0.7f, rate);
            for (int i = (int)(0.1f * rate); i < d.Length; i++)
            {
                float t = i / (float)rate - 0.1f;
                d[i] += flare.Process(r.Bipolar) * Env.AD(t, 0.05f, 0.2f) * 0.8f;
            }
            Grit(d, rate, r, 0.1f, 0.4f, 12, 0.25f);
            return Buf.Finish(d, 0.5f, rate);
        }

        static float[] FireCrackleLoop(Rng r, int rate)
        {
            float loopSec = 4f;
            var longer = Buf.Seconds(loopSec + 0.3f, rate);
            var pink = new Pink();
            var lp = new OnePole(); lp.Set(420f, rate);
            for (int i = 0; i < longer.Length; i++)
            {
                float t = i / (float)rate;
                float breathe = 0.7f + 0.3f * MathF.Sin(2f * MathF.PI * t / 4f * 3f);
                longer[i] = lp.Low(pink.Next(r)) * 0.45f * breathe;
            }
            for (int c = 0; c < 70; c++)
            {
                float at = r.Value * loopSec;
                bool big = r.Chance(0.15f);
                Gen.NoiseBurst(longer, rate, r, at, r.Range(1500f, 6000f), 1.2f, 0.0002f, big ? 0.006f : r.Range(0.001f, 0.003f), big ? 2.5f : r.Range(0.5f, 1.5f));
            }
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.3f * rate)), 0.5f, rate, 0f);
        }

        // Her television: a presenter's babble and a little jingle, squeezed through a set's
        // small speaker.
        static float[] TvMurmurLoop(Rng r, int rate)
        {
            float loopSec = 9f;
            var longer = Buf.Seconds(loopSec + 0.4f, rate);
            var v = VoiceProfile.Presenter();
            v.rate = rate;
            string[] lines =
            {
                "Good evening, and welcome back to the show.",
                "Tonight: the garden, the weather, and a very big marrow!",
                "Stay with us after the break.",
            };
            float at = 0.1f;
            for (int i = 0; i < lines.Length && at < loopSec; i++)
            {
                var segs = Babble.FromText(lines[i], VoiceMood.Calm, false, (uint)(900 + i), 3.2f);
                var voice = VoiceSynth.Render(segs, v, (uint)(77 + i));
                Buf.Mix(longer, voice, (int)(at * rate), 0.8f);
                at += voice.Length / (float)rate + 0.35f;
            }
            // A jingle bed under the talk.
            float beat = 0.36f;
            float[] tune = { 72f, 76f, 79f, 76f, 74f, 77f, 81f, 77f };
            for (int b = 0; b * beat < loopSec; b++)
                Gen.Mallet(longer, rate, b * beat, Buf.Hz(tune[b % tune.Length]), 0.2f, 0.12f);
            var hp = Biquad.HighPass(350f, 0.7f, rate);
            var lp = Biquad.LowPass(3200f, 0.7f, rate);
            for (int i = 0; i < longer.Length; i++) longer[i] = lp.Process(hp.Process(longer[i]));
            return Buf.Finish(Buf.Loop(longer, (int)(loopSec * rate), (int)(0.4f * rate)), 0.5f, rate, 0f);
        }
    }
}
