using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // The moving truck's sound, from TruckVehicle's public state only. Added to the truck at
    // runtime (SceneAudioBinder).
    //
    //   someone takes the wheel      the door, the starter, the diesel catching
    //   engine on                    an idle loop and a working loop, crossfaded by the
    //                                pedal; both pitched by revs (speed through virtual
    //                                gears, and pedal)
    //   rolling                      tyres and road, louder with speed up to audioTopSpeed
    //   braking hard                 a brake squeal; coming to rest after it, the air brake
    //   reversing                    the beeper at the back, lower and longer than the
    //                                grenade's beep so one is never taken for the other
    //   the driver gets out          the engine dies, the door
    //
    // Nobody at the wheel: silent. A parked delivery truck with its engine running all game
    // would be one more drone under everything.
    [DisallowMultipleComponent]
    public sealed class TruckAudio : MonoBehaviour
    {
        TruckVehicle truck;
        CrewMember driver;
        bool engineOn;
        float startedAt;
        float rpm, throttle;
        bool brakeArmed;
        int idle = -1, work = -1, road = -1, squeal = -1, beeper = -1;
        Vector3 engine = new Vector3(0f, 1f, 2.4f), cabDoor = new Vector3(1.1f, 1.2f, 2f), rear = new Vector3(0f, 1.4f, -3.5f);

        public bool EngineOn => engineOn;
        public float Revs => rpm;

        void Awake()
        {
            truck = GetComponent<TruckVehicle>();
        }

        void Start()
        {
            // Where the engine, the cab door and the back are, from the truck's own box.
            if (truck == null) return;
            Bounds h = truck.Hull;
            if (h.size.sqrMagnitude < 0.01f) return;
            Vector3 c = h.center, e = h.extents;
            float bottom = c.y - e.y;
            engine = new Vector3(c.x, bottom + 0.9f, c.z + e.z - 0.6f);
            cabDoor = new Vector3(c.x + e.x, bottom + 1.3f, c.z + e.z - 1.2f);
            rear = new Vector3(c.x, bottom + 1.5f, c.z - e.z);
        }

        void Update()
        {
            if (truck == null) return;
            CrewMember d = truck.Driver;
            if (d != driver)
            {
                DriverChanged(driver, d);
                driver = d;
            }
            if (!engineOn) return;

            float dt = Time.deltaTime;
            float v = truck.ForwardSpeed;
            float speed = Mathf.Abs(v);
            Vector2 move = d != null && d.Input != null ? d.Input.Move : Vector2.zero;
            bool handbrake = d != null && d.Input != null && d.Input.Held(CrewButton.Jump);
            float pedal = move.y;
            if (!Net.HasAuthority)
            {
                // Online client: the host's pedal and handbrake (the driver's input is not here).
                pedal = truck.Pedal;
                handbrake = truck.Handbrake;
            }

            // Pressing to go (either way) is throttle; pressing against the roll is braking.
            float go = pedal > 0.05f && v > -0.5f ? pedal : pedal < -0.05f && v < 0.5f ? -pedal : 0f;
            bool braking = speed > 1.5f && (handbrake || (pedal > 0.05f && v < -0.5f) || (pedal < -0.05f && v > 0.5f));
            throttle = Mathf.MoveTowards(throttle, go, dt * 3f);
            var tuning = truck.Tuning;
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(1f, tuning.audioTopSpeed));
            float rpmTarget = GearRevs(speed01, tuning.gearBands) * 0.75f + throttle * 0.3f;
            // Revs climb with the lag of a heavy engine and drop fast at a shift.
            rpm = Mathf.Lerp(rpm, rpmTarget, 1f - Mathf.Exp(-dt / (rpmTarget < rpm ? 0.08f : 0.25f)));

            // The loops come in as the starter's last cough fades (EngineStart is 1.8 s).
            float gate = Mathf.Clamp01((Time.time - startedAt - 0.9f) / 0.6f);
            float pitch = 0.85f + rpm * 0.9f;
            AudioDirector.SetLoop(idle, gate * Mathf.Lerp(0.6f, 0.25f, throttle), pitch);
            AudioDirector.SetLoop(work, gate * (throttle * 0.65f + rpm * 0.2f), pitch);
            AudioDirector.SetLoop(road, speed01 * 0.55f, 0.8f + speed01 * 0.6f);

            if (braking && squeal < 0) squeal = Loop(SfxKind.BrakeSqueakLoop, engine, 0f);
            if (!braking && squeal >= 0) { AudioDirector.Stop(squeal, 0.2f); squeal = -1; }
            AudioDirector.SetLoop(squeal, Mathf.Clamp01(speed / 8f) * 0.4f, 1f);

            // Brought to a stop after real braking: the air brake lets go.
            if (braking && speed > 3f) brakeArmed = true;
            if (brakeArmed && speed < 0.4f)
            {
                brakeArmed = false;
                AudioDirector.PlayOn(SfxKind.AirBrake, transform, engine + Vector3.down * 0.5f, SoundPreset.Engine, 0.55f, Random.Range(0.95f, 1.05f));
            }

            bool reversing = v < -0.3f;
            if (reversing && beeper < 0) beeper = Loop(SfxKind.ReverseBeepLoop, rear, 0.4f);
            if (!reversing && beeper >= 0) { AudioDirector.Stop(beeper, 0.05f); beeper = -1; }
        }

        void DriverChanged(CrewMember was, CrewMember now)
        {
            if (now != null && was == null)
            {
                AudioDirector.PlayOn(SfxKind.TruckDoor, transform, cabDoor, SoundPreset.Engine, 0.7f, Random.Range(0.97f, 1.03f));
                AudioDirector.PlayOn(SfxKind.EngineStart, transform, engine, SoundPreset.Engine, 0.8f, 1f);
                engineOn = true;
                startedAt = Time.time;
                rpm = 0f;
                throttle = 0f;
                idle = Loop(SfxKind.EngineIdleLoop, engine, 0f);
                work = Loop(SfxKind.EngineLoadLoop, engine, 0f);
                road = Loop(SfxKind.RoadRumbleLoop, new Vector3(engine.x, 0.4f, 0f), 0f);
            }
            else if (now == null && was != null)
            {
                AudioDirector.PlayOn(SfxKind.EngineStop, transform, engine, SoundPreset.Engine, 0.75f, 1f);
                AudioDirector.PlayOn(SfxKind.TruckDoor, transform, cabDoor, SoundPreset.Engine, 0.7f, Random.Range(0.97f, 1.03f));
                StopAll(0.3f);
                engineOn = false;
            }
        }

        // Virtual gears (DEV 2, 6.2): the physics has none, the sound does. Across the speed range
        // the revs climb through 'gears' bands and fall back at each shift: a saw-tooth, 0..1.
        static float GearRevs(float speed01, int gears)
        {
            gears = Mathf.Max(1, gears);
            float g = Mathf.Clamp01(speed01) * gears;
            int gear = Mathf.Min(gears - 1, (int)g);
            float inGear = Mathf.Clamp01(g - gear);
            return Mathf.Lerp(gear == 0 ? 0f : 0.4f, 1f, inGear);
        }

        int Loop(SfxKind kind, Vector3 local, float volume)
        {
            return AudioDirector.StartLoop(kind, transform, local, SoundPreset.Engine, volume, 1f, 0.1f);
        }

        void StopAll(float fade)
        {
            AudioDirector.Stop(idle, fade);
            AudioDirector.Stop(work, fade);
            AudioDirector.Stop(road, fade);
            AudioDirector.Stop(squeal, fade);
            AudioDirector.Stop(beeper, fade);
            idle = work = road = squeal = beeper = -1;
            brakeArmed = false;
        }

        void OnDisable()
        {
            StopAll(0f);
            engineOn = false;
            driver = null;
        }
    }
}
