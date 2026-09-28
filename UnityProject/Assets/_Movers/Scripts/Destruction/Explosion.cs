using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // What a blast does to the world, in one call: Explosion.Detonate(where, how far, how hard,
    // and who is to blame).
    //
    // The grenade is one caller; anything else that ever goes bang (a gas bottle, a chain
    // reaction) gets the same blast, and nothing here knows or cares what exploded.
    //
    // The order is the design:
    //  1. find everything breakable in reach and measure what stands between it and the blast,
    //     BEFORE anything breaks, so the rubble of the first wall cannot shield the second;
    //  2. damage it nearest first, so a wall that gives way lets the blast through to what is
    //     behind it, and a wall that holds still protects the next room (1 and 2: BlastSolver);
    //  3. ask the structure what lost its support: it starts to fall over the next frames;
    //  4. only then push, so the debris the damage just spawned flies with everything else;
    //  5. shove and rattle the players, show it, play it, say it (WorldEvents), and tell the
    //     grenades last, so a chain reaction starts from a room that has already been blown up.
    //
    // Numbers are greybox: tuned so a grenade in the cellar is a spectacle, not a simulation.
    // Funny failure over frustrating punishment (CLAUDE.md 4): a blast throws you and leaves
    // you woozy, it never takes control away and there is nothing to die of.
    public static class Explosion
    {
        // Raised after the damage, the forces and the knockback have all been applied. Grenades
        // listen to it for chain reactions. position, radius, power. Who set it off is in
        // CurrentInstigator while the listeners run.
        public static event System.Action<Vector3, float, float> Detonated;

        // Who the blast being run is blamed on (see Actors); World between blasts. A grenade lit
        // by this blast reads it, so a chain reaction belongs to whoever started it.
        public static int CurrentInstigator { get; private set; } = Actors.World;

        // Cost of the last blast in ms (collect, damage, structure, push, knock, picture, sound,
        // events: everything but the grenades' own reaction), and of every blast in the frame it
        // happened in (a chain can run several in one frame). On the online client: the cost of
        // its PlayCosmetic.
        public static float LastBlastMs { get; private set; }
        public static float LastFrameBlastMs { get; private set; }
        public static int LastBlastFrame { get; private set; } = -1;
        public static int BlastCount { get; private set; }
        // Worst frame of the last blast's own frame and the 5 after it, minus the median of the 30
        // frames before it (ms): the deferred detaches, rubble and client bursts a blast leaves
        // behind, and on the client the burst of detach records that arrives with it.
        // On both machines (a client blast is its PlayCosmetic). See FrameClock.
        public static float MaxFrameMsAfterBlast => FrameClock.WorstAfterBlastMs;

        // Every number of the push, the knock and the shake is in the table's Blast block
        // (DestructionMaterialTable): pushImpulse (N.s at the centre, linear falloff, like
        // AddExplosionForce), pushUplift (the push centre is lowered, so things lift as they
        // fly), maxLaunchSpeed (faster than this a light object tunnels through a wall), maxSpin
        // (a tumble you can follow: a sofa spinning at the project's 50 rad/s in a 2.5 m room is
        // a blur that clips through the walls), playerReach, knockHorizontal, knockUp,
        // knockedDownSpeed, concussion (a bang on the head is the drunk wobble), shakeReach.
        //
        // A chain reaction longer than this in one call is a bug (something re-arming itself),
        // not a cellar full of grenades.
        const int MaxChain = 64;
        const int MaxOverlap = 8192;

        struct Blast
        {
            public Vector3 position;
            public float radius;
            public float power;
            public int instigator;
        }

        struct Victim
        {
            public CrewMember member;
            public Vector3 center;
            public float falloff;
            public BlastSolver.Occluder cover;
        }

        // Who launched what: a vase this blast throws into a window a second later breaks it in
        // the grenade owner's name (ImpactDamage asks LaunchedBy). Kept here rather than written
        // into MovableObject's handler fields, which the player code owns and other systems read
        // as "who carried it".
        struct Launch
        {
            public int instigator;
            public float time;
        }

        // The push pass sees everything the damage just spawned as well: a few walls' worth of
        // fresh debris on top of the furniture, so it has room to grow.
        static Collider[] pushOverlap = new Collider[1024];
        static readonly HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();
        static readonly Dictionary<Rigidbody, Launch> launched = new Dictionary<Rigidbody, Launch>(256);
        static readonly List<Rigidbody> expired = new List<Rigidbody>(64);
        static readonly List<Victim> victims = new List<Victim>(4);
        static readonly Queue<Blast> pending = new Queue<Blast>();
        static readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

        static bool running;
        static bool quitting;
        static bool warnedPushFull;
        static bool warnedChain;

        // Statics survive a play-mode exit when the domain reload is disabled. Listeners left over
        // from the last run belong to grenades that no longer exist.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Detonated = null;
            pending.Clear();
            launched.Clear();
            running = false;
            warnedPushFull = false;
            warnedChain = false;
            quitting = false;
            CurrentInstigator = Actors.World;
            LastBlastMs = 0f;
            LastFrameBlastMs = 0f;
            LastBlastFrame = -1;
            BlastCount = 0;
            FrameClock.Install();
            // Removed first: with the domain reload off this runs every play session, and the
            // handler must not pile up.
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        // Leaving play mode destroys every object, and a grenade that goes off from its
        // OnDestroy then would build a fireball and voices in a scene that is being torn down
        // ("Some objects were not cleaned up"). Nothing blows up after this.
        static void OnQuitting() { quitting = true; }

        public static void Detonate(Vector3 position, float radius = 6.5f, float power = 1f, int instigator = Actors.World)
        {
            // Online, blasts are the host's; the client sees them through PlayCosmetic.
            if (!Net.HasAuthority) return;
            if (!Application.isPlaying || quitting) return;
            if (!IsFinite(position) || !(radius > 0.05f) || !(power > 0f)
                || float.IsInfinity(radius) || float.IsInfinity(power)) return;

            pending.Enqueue(new Blast { position = position, radius = radius, power = power, instigator = instigator });

            // Called again from inside a blast: a grenade the blast just broke, or a listener of
            // Detonated. Queue it and let the running call get to it once the current blast is
            // finished, rather than re-entering halfway through and trampling the shared lists.
            // This also turns a cellar full of grenades into a loop instead of a deep recursion.
            if (running) return;

            running = true;
            int count = 0;
            try
            {
                while (pending.Count > 0)
                {
                    var b = pending.Dequeue();
                    if (++count > MaxChain)
                    {
                        if (!warnedChain)
                        {
                            warnedChain = true;
                            Debug.LogWarning("Explosion: more than " + MaxChain + " blasts in one chain, the rest are dropped. Is something detonating itself again?");
                        }
                        break;
                    }
                    Run(b);
                }
            }
            finally
            {
                running = false;
                pending.Clear();
                victims.Clear();
                pushed.Clear();
                CurrentInstigator = Actors.World;
            }
        }

        static void Run(Blast blast)
        {
            Vector3 c = blast.position;
            float radius = blast.radius, power = blast.power;
            CurrentInstigator = blast.instigator;

            // Every step below is guarded on its own, and the grenades are told whatever
            // happened before. A bug in one step (or a missing shader in the picture) is logged
            // and costs that step only: it must never swallow the Detonated that the next
            // grenade of a chain reaction is waiting for.
            watch.Restart();
            try
            {
                // 1. what is in reach, and what stands in front of it, before anything breaks
                BlastSolver.Collect(c, radius);
                Vector3 eye = BlastSolver.ShieldOrigin(c);
                BlastSolver.MeasureCover(eye);
                MeasurePlayers(c, eye, radius);

                // 2. break things, nearest first, with room kept in the debris budget for the
                // wall chunks it is about to break
                BlastSolver.ExpectChunks(radius, power);
                BlastSolver.Apply(c, radius, power, blast.instigator);

                // 3. what lost its support starts to fall, over the next frames
                StructureGraph.Current?.ResolvePending();

                // 4. push everything loose, the fresh debris included. The debris was placed by
                // setting transforms, which the physics scene has not seen yet.
                Physics.SyncTransforms();
                PushBodies(c, radius, power, blast.instigator);

                // 5. people
                KnockPlayers(c, radius, blast.instigator);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            // Picture and sound: cosmetic, so each one fails alone. The online client gets the
            // same picture from this record, after the transitions of the blast (4.2).
            if (Net.IsHost)
            {
                try { PropsSync.ExplosionFx(c, radius, power, blast.instigator); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            try { Shake(c, radius, power); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { ExplosionFX.Spawn(c, radius, power); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { ImpactAudio.Play(ImpactAudio.Kind.Boom, c, 1f, blast.instigator); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { WorldEvents.Raise(WorldEventType.Explosion, c, blast.instigator, 1f, radius); }
            catch (System.Exception e) { Debug.LogException(e); }
            watch.Stop();
            RecordCost((float)watch.Elapsed.TotalMilliseconds);

            // 6. the grenades, last, so a chain reaction starts from a room already blown up
            Announce(c, radius, power);
        }

        // The online client's side of a host blast (Props ExplosionFx): the push on the local
        // debris (the only dynamic bodies there), the shake, the fireball, the boom, and a wake
        // for frozen debris. Nothing breaks, nobody is knocked (the host does both and they
        // replicate), and no Detonated and no WorldEvents: the host's are forwarded.
        public static void PlayCosmetic(Vector3 position, float radius, float power)
        {
            if (!Application.isPlaying || quitting) return;
            if (!IsFinite(position) || !(radius > 0.05f) || !(power > 0f)
                || float.IsInfinity(radius) || float.IsInfinity(power)) return;
            watch.Restart();
            try
            {
                Physics.SyncTransforms();
                PushBodies(position, radius, power, Actors.World);
            }
            catch (System.Exception e) { Debug.LogException(e); }
            finally { pushed.Clear(); }
            try { Shake(position, radius, power); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { ExplosionFX.Spawn(position, radius, power); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { ImpactAudio.PlayFromNet(ImpactAudio.Kind.Boom, position, 1f); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { DebrisManager.WakeFromBlast(position, radius, power); }
            catch (System.Exception e) { Debug.LogException(e); }
            watch.Stop();
            RecordCost((float)watch.Elapsed.TotalMilliseconds);
        }

        // Stronger than a grenade shakes harder, up to the table's cap, and further.
        static void Shake(Vector3 c, float radius, float power)
        {
            var t = DestructionMaterialTable.Current;
            CameraShake.Shake(c, Mathf.Clamp(power, 0f, t.shakeMaxStrength), radius * t.shakeReach);
        }

        static void RecordCost(float ms)
        {
            int frame = Time.frameCount;
            LastFrameBlastMs = LastBlastFrame == frame ? LastFrameBlastMs + ms : ms;
            LastBlastFrame = frame;
            LastBlastMs = ms;
            BlastCount++;
            FrameClock.MarkBlast();
        }

        // ---- players ----

        static void MeasurePlayers(Vector3 c, Vector3 eye, float radius)
        {
            victims.Clear();
            float reach = radius * DestructionMaterialTable.Current.playerReach;
            var crew = CrewRoster.All;
            for (int i = 0; i < crew.Count; i++)
            {
                var m = crew[i];
                if (m == null || !m.isActiveAndEnabled || m.Controller == null || !m.Controller.isActiveAndEnabled) continue;

                var cc = m.GetComponent<CharacterController>();
                bool body = cc != null && cc.enabled;
                Vector3 center = body ? cc.bounds.center : m.transform.position;
                Vector3 nearest = body ? cc.bounds.ClosestPoint(c) : center;
                float d = Vector3.Distance(nearest, c);
                if (d > reach) continue;

                victims.Add(new Victim
                {
                    member = m,
                    center = center,
                    falloff = BlastSolver.Falloff(d, reach),
                    cover = BlastSolver.FindOccluder(eye, center, null, null, m.transform),
                });
            }
        }

        static void KnockPlayers(Vector3 c, float radius, int instigator)
        {
            var t = DestructionMaterialTable.Current;
            for (int i = 0; i < victims.Count; i++)
            {
                var v = victims[i];
                if (v.member == null || v.member.Controller == null || !v.member.Controller.isActiveAndEnabled) continue;

                float f = v.falloff * BlastSolver.CoverOf(v.cover);
                if (f <= 0.001f) continue;

                Vector3 away = v.center - c;
                away.y = 0f;
                // Standing right on it: thrown backwards, which is at least the funny direction.
                if (away.sqrMagnitude < 0.0001f) away = -v.member.transform.forward;
                away.y = 0f;
                away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.zero;

                Vector3 shove = away * (t.knockHorizontal * f) + Vector3.up * (t.knockUp * f);
                v.member.Controller.AddImpulse(shove);

                var drunk = v.member.GetComponent<Drunkenness>();
                if (drunk != null) drunk.Add(t.concussion * f);

                float speed = shove.magnitude;
                if (speed >= t.knockedDownSpeed)
                    WorldEvents.Raise(WorldEventType.PlayerKnockedDown, v.center, instigator, 0f, speed, 0, v.member);
            }
        }

        // ---- push ----

        static void PushBodies(Vector3 c, float radius, float power, int instigator)
        {
            pushed.Clear();
            ForgetOldLaunches();
            float now = Time.time;
            float grace = DestructionMaterialTable.Current.structureLaunchGrace;
            int n = Physics.OverlapSphereNonAlloc(c, radius, pushOverlap, ~0, QueryTriggerInteraction.Ignore);
            while (n >= pushOverlap.Length && pushOverlap.Length < MaxOverlap)
            {
                pushOverlap = new Collider[pushOverlap.Length * 2];
                n = Physics.OverlapSphereNonAlloc(c, radius, pushOverlap, ~0, QueryTriggerInteraction.Ignore);
            }
            if (n >= pushOverlap.Length && !warnedPushFull)
            {
                warnedPushFull = true;
                Debug.LogWarning("Explosion: " + pushOverlap.Length + " colliders to push in one blast, some were left where they were.");
            }
            for (int i = 0; i < n; i++)
            {
                var col = pushOverlap[i];
                pushOverlap[i] = null;
                if (col == null) continue;
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || !pushed.Add(rb)) continue;
                // A shattered object is switched off (destruction contract), so this skips it.
                if (!rb.gameObject.activeInHierarchy) continue;
                // One broken this very blast whose switch-off is still pending: its pieces fly,
                // it does not.
                if (rb.TryGetComponent(out Breakable br) && br.IsDestroyed) continue;
                // A wall chunk this blast (or one a moment ago) just launched already flies at its
                // eject speed: pushed again it would leave twice as fast. Only for a short grace,
                // on both machines (the client's chunks land a frame or so after the host's), so
                // a later blast still throws the old rubble about.
                rb.TryGetComponent(out DebrisPiece piece);
                if (piece != null && piece.structureChunk && now - piece.launchedAt < grace) continue;
                if (Push(rb, col, c, radius, power, piece != null)) Blame(rb, instigator);
            }
        }

        // What this body breaks in the next seconds is the blast's doing. Debris it flings keeps
        // that name for good: its crush (DebrisPiece.instigator) is on the one who threw it last.
        static void Blame(Rigidbody rb, int instigator)
        {
            if (instigator == Actors.World) return;
            launched[rb] = new Launch { instigator = instigator, time = Time.time };
            if (rb.TryGetComponent(out DebrisPiece piece)) piece.instigator = instigator;
        }

        // Who launched this body within the last 'withinSeconds', or World.
        internal static int LaunchedBy(Rigidbody rb, float withinSeconds)
        {
            if (rb == null || !launched.TryGetValue(rb, out Launch l)) return Actors.World;
            return Time.time - l.time <= withinSeconds ? l.instigator : Actors.World;
        }

        // Once per blast, before the push: the table only ever holds the last few seconds.
        static void ForgetOldLaunches()
        {
            if (launched.Count == 0) return;
            float now = Time.time;
            expired.Clear();
            foreach (var kv in launched)
                if (kv.Key == null || now - kv.Value.time > ImpactDamage.BlameSeconds) expired.Add(kv.Key);
            for (int i = 0; i < expired.Count; i++) launched.Remove(expired[i]);
            expired.Clear();
        }

        // The spec'd push is rb.AddExplosionForce(power * 900, c, radius, 0.8, Impulse) then a
        // 28 m/s clamp. AddExplosionForce only queues a force for the next physics step, so a
        // clamp written straight after it reads the old velocity and clamps nothing. The same
        // push is therefore computed here (linear falloff from the nearest point, centre lowered
        // by the upwards modifier) and written as a velocity, where the clamp actually holds.
        // The launch cap also falls with distance (lightDebrisLaunchFalloff): without it every
        // light shard in the room flew at the cap, the far ones as fast as the near ones.
        // Debris gets no cover ray: a blast pushes hundreds of shards, and one ray each was most
        // of its cost for a difference nobody sees. False when the body was out of reach.
        static bool Push(Rigidbody rb, Collider col, Vector3 c, float radius, float power, bool debris)
        {
            var t = DestructionMaterialTable.Current;
            Vector3 com = rb.worldCenterOfMass;
            Vector3 cp = BlastSolver.ClosestPoint(col, c);
            float falloff = 1f - Mathf.Clamp01(Vector3.Distance(cp, c) / radius);
            if (falloff <= 0f) return false;
            if (!debris && BlastSolver.IsBuildingInTheWay(c, com, rb)) falloff *= t.solidCover;

            Vector3 dir = com - (c - Vector3.up * t.pushUplift);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            float speed = Mathf.Min(power * t.pushImpulse * falloff / Mathf.Max(0.05f, rb.mass),
                                    t.maxLaunchSpeed * Mathf.Pow(falloff, t.lightDebrisLaunchFalloff));
            Vector3 dv = dir * speed;

            rb.WakeUp();
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity + dv, t.maxLaunchSpeed);

            // Tumble: the push lands on the near face, not the centre of mass, so things spin
            // away from the blast instead of sliding off it like pucks.
            Vector3 axis = Vector3.Cross(cp - com, dir);
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.Cross(dir, Random.onUnitSphere);
            if (axis.sqrMagnitude < 1e-6f) return true;
            float spin = Mathf.Min(dv.magnitude * 0.6f, t.maxSpin);
            rb.angularVelocity += axis.normalized * spin;
            return true;
        }

        static void Announce(Vector3 c, float radius, float power)
        {
            var handler = Detonated;
            if (handler == null) return;
            // One listener at a time, so a grenade that throws does not stop the next one from
            // hearing the blast.
            foreach (System.Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((System.Action<Vector3, float, float>)d)(c, radius, power);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                     || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        // ---- what a blast costs the frames after it (MaxFrameMsAfterBlast) ----

        // A blast's cost does not end with its call: deferred chunk detaches, rubble, the client's
        // bursts of detach records and the physics of hundreds of new bodies land over the next
        // frames. So every frame's length is kept (the last 30), and a blast takes their median
        // as the baseline and watches its own frame and the 5 after it: the worst of them minus
        // the baseline is what the blast cost (DEV 2 section 11, budget 16 ms).
        //
        // A frame's length is known at the start of the next one (unscaled delta time), so the
        // clock runs at the very start of the player loop, as its own step: no GameObject, the
        // same on the host, the client and offline, and it costs one float a frame.
        static class FrameClock
        {
            const int History = 30;
            const int FramesAfter = 5;

            struct Marker { }   // names the step in the player loop (and in the Profiler)

            static readonly float[] history = new float[History];
            static readonly float[] sorted = new float[History];
            static int count;
            static int next;
            static int blastFrame = -1;
            static float baseline;
            static float worst;

            public static float WorstAfterBlastMs { get; private set; }

            // Once per play session. The step may already be in the loop (domain reload off, or a
            // loop kept across a reload): it is found by its marker's name, pointed at this Tick,
            // and never added twice.
            public static void Install()
            {
                count = 0;
                next = 0;
                blastFrame = -1;
                baseline = 0f;
                worst = 0f;
                WorstAfterBlastMs = 0f;

                var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
                var phases = loop.subSystemList;
                if (phases == null) return;
                for (int i = 0; i < phases.Length; i++)
                {
                    if (phases[i].type != typeof(UnityEngine.PlayerLoop.Initialization)) continue;
                    var steps = phases[i].subSystemList ?? new UnityEngine.LowLevel.PlayerLoopSystem[0];
                    for (int k = 0; k < steps.Length; k++)
                    {
                        if (steps[k].type == null || steps[k].type.FullName != typeof(Marker).FullName) continue;
                        steps[k].type = typeof(Marker);
                        steps[k].updateDelegate = Tick;
                        phases[i].subSystemList = steps;
                        loop.subSystemList = phases;
                        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
                        return;
                    }
                    var grown = new UnityEngine.LowLevel.PlayerLoopSystem[steps.Length + 1];
                    grown[0] = new UnityEngine.LowLevel.PlayerLoopSystem { type = typeof(Marker), updateDelegate = Tick };
                    System.Array.Copy(steps, 0, grown, 1, steps.Length);
                    phases[i].subSystemList = grown;
                    loop.subSystemList = phases;
                    UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
                    return;
                }
            }

            // A blast (or its picture on the client) ran this frame. A blast inside the window of
            // the one before keeps that one's baseline: the frames it would take it from are
            // already the aftermath.
            public static void MarkBlast()
            {
                int frame = Time.frameCount;
                if (frame == blastFrame) return;
                bool open = blastFrame >= 0 && frame - blastFrame <= FramesAfter + 1;
                if (!open) baseline = Median();
                blastFrame = frame;
                worst = 0f;
                WorstAfterBlastMs = 0f;
            }

            static void Tick()
            {
                if (!Application.isPlaying) return;
                float ms = Time.unscaledDeltaTime * 1000f;
                if (!(ms >= 0f) || float.IsInfinity(ms)) return;
                int ended = Time.frameCount - 1;   // the frame this length belongs to

                if (blastFrame >= 0 && ended >= blastFrame)
                {
                    if (ended <= blastFrame + FramesAfter)
                    {
                        worst = Mathf.Max(worst, ms);
                        WorstAfterBlastMs = Mathf.Max(0f, worst - baseline);
                    }
                    else blastFrame = -1;   // window over, the reading stays until the next blast
                }

                history[next] = ms;
                next = (next + 1) % History;
                if (count < History) count++;
            }

            static float Median()
            {
                if (count == 0) return 0f;
                System.Array.Copy(history, sorted, count);
                System.Array.Sort(sorted, 0, count);
                return count % 2 == 1 ? sorted[count / 2] : 0.5f * (sorted[count / 2 - 1] + sorted[count / 2]);
            }
        }
    }
}
