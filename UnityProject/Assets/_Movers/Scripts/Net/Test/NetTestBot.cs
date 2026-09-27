using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    // The loopback test's player (NETCODE_SLICE 14). "-netbot <script>" on the command line, or
    // the editor's MoversNetTestCLI.RunHost, starts it. It plays a fixed script on its side of
    // the session and prints "NETTEST PASS|FAIL <check> <detail>" lines to the net log (check
    // names start with the spec's check number), then "NETTEST DONE pass=<n> fail=<m>", and
    // quits the player a few seconds later.
    //
    // Scripts. "host" and "client": at the end the host quits, and the client checks it is back
    // on the title with net.hostLeft (check 8, host killed). "host-stay" and "client-quit": the
    // client quits, and the host checks the toast and P2's idle body (check 8, client killed).
    // "-netbotpeerlog <path>" on the client names the host's net log (loopback: one disk), so the
    // client can compare the host's resting poses with its own (check 6).
    //
    // Test code only: nothing creates it unless a bot script was asked for, so never offline.
    [DefaultExecutionOrder(-600)]   // before CrewInput (-500) polls the scripted source
    public sealed class NetTestBot : MonoBehaviour
    {
        // The script's clock, seconds since the first PeerReady of the session.
        const float HostUnlockAt = 1f;
        const float IntroDeadline = 20f;
        const float StagesDeadline = 37f;    // client: stages 1 and 2 give up here
        const float StandStillAt = 38f;
        const float BlastAt = 40f;           // host: the grenade-sized blast in front of P2
        const float PoseListAt = 47f;        // host: prints the resting poses
        const float PoseCompareAt = 50f;     // client: compares them
        const float EndCardAt = 52f;         // host: F6, force Completed
        const float StageTimeout = 80f;      // client: the end card must be up by then

        const float FirstReadyTimeout = 180f;   // the other machine may be started by hand
        const float HardDeadline = 420f;
        const float HandshakeLimit = 10f;
        const float LongestFrameLimit = 5f;
        const float BlastRadius = 6.5f;
        const float BlastAhead = 1.5f;       // in front: P2 faces the pane it broke, and flies back into the room
        const float QuitGrace = 5f;

        static NetTestBot instance;

        // For the editor CLI, which exits Play when the bot is done.
        public static bool Finished { get; private set; }
        public static int PassCount { get; private set; }
        public static int FailCount { get; private set; }

        string script;
        bool isHost, weQuit, finished, expectDisconnect;
        string peerLogPath;
        float launchedAt, t0 = -1f;
        readonly NetTestPilot pilot = new NetTestPilot();

        // The handshake watcher: one load window per LoadScene / Reload.
        NetStatus lastStatus;
        bool lastReady, droppedEarly;
        float loadAt = -1f;
        int readyCount;

        int errors, localRaises, errorsLogged;
        bool partnerLeftToast, reportedFight;

        float Now => Time.unscaledTime;
        float T => t0 < 0f ? 0f : Now - t0;

        // =====================================================================
        // Start and stop
        // =====================================================================

        public static void Launch(string script)
        {
            if (instance != null || string.IsNullOrEmpty(script)) return;
            var go = new GameObject("_NetTestBot");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NetTestBot>();
            instance.Configure(script);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            if (!string.IsNullOrEmpty(NetSession.BotScript)) Launch(NetSession.BotScript);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            Finished = false;
            PassCount = FailCount = 0;
        }

        void Configure(string name)
        {
            script = name.Trim().ToLowerInvariant();
            launchedAt = Now;
            peerLogPath = ArgAfter("-netbotpeerlog");
            Info("bot '" + script + "' started" + (peerLogPath != null ? ", peer log " + peerLogPath : ""));
            switch (script)
            {
                case "host": isHost = true; weQuit = true; break;
                case "host-stay": isHost = true; weQuit = false; break;
                case "client": isHost = false; weQuit = false; break;
                case "client-quit": isHost = false; weQuit = true; break;
                default:
                    Check(false, "bot", "unknown script '" + name + "' (host, client, host-stay, client-quit)");
                    Finish();
                    return;
            }
            StartCoroutine(isHost ? HostScript() : ClientScript());
        }

        static string ArgAfter(string flag)
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == flag && !args[i + 1].StartsWith("-")) return args[i + 1];
            }
            catch (Exception) { }
            return null;
        }

        void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            NetSession.Notice += OnNotice;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            NetSession.Notice -= OnNotice;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            expectDisconnect = true;
            StopAllCoroutines();
            pilot.Stop();
            NetLog.Write("NETTEST DONE pass=" + PassCount + " fail=" + FailCount);
            Finished = true;
            StartCoroutine(QuitAfterGrace());
        }

        // The other side may still be reading the last records: a short grace, none when it is
        // already gone. In the editor the CLI exits Play instead.
        IEnumerator QuitAfterGrace()
        {
            float until = Now + (Net.PeerConnected ? QuitGrace : 1f);
            while (Now < until) yield return null;
            if (!Application.isEditor) Application.Quit();
        }

        // =====================================================================
        // Report
        // =====================================================================

        void Check(bool ok, string check, string detail)
        {
            if (ok) PassCount++;
            else FailCount++;
            NetLog.Write("NETTEST " + (ok ? "PASS " : "FAIL ") + check + " " + detail);
        }

        static void Info(string line) { NetLog.Write("NETTEST INFO " + line); }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning && message != null && message.StartsWith("WorldEventRelay:")) localRaises++;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            if (errorsLogged >= 5 || message == null) return;
            errorsLogged++;
            int cut = message.IndexOf('\n');
            Info("console " + type + ": " + (cut > 0 ? message.Substring(0, cut) : message));
        }

        void OnNotice(string key)
        {
            if (key == "net.partnerLeft") partnerLeftToast = true;
        }

        // =====================================================================
        // Watcher: handshakes, lost peers, deadlines
        // =====================================================================

        void Update()
        {
            if (finished) return;
            WatchHandshake();
            if (!isHost && NetIds.Ready && !pilot.Bind(Net.ClientMember) && !reportedFight)
            {
                reportedFight = true;
                Check(false, "bot-input", "something keeps replacing P2's input source, the bot stopped taking it back");
            }

            if (Now - launchedAt > HardDeadline)
            {
                Check(false, "deadline", "the script did not end within " + HardDeadline + " s");
                Finish();
                return;
            }
            if (readyCount == 0)
            {
                if (NetSession.Status == NetStatus.Failed)
                {
                    Check(false, "2-handshake", "the connection failed: " + NetSession.Error + " (" + NetSession.ErrorDetail + ")");
                    Finish();
                }
                else if (Now - launchedAt > FirstReadyTimeout)
                {
                    Check(false, "2-handshake", "no PeerReady within " + FirstReadyTimeout + " s, status " + NetSession.Status);
                    Finish();
                }
                return;
            }
            if (expectDisconnect) return;
            if (isHost && !Net.PeerConnected)
            {
                Check(false, "peer-lost", "the client left in the middle of the script, t=" + F(T));
                Finish();
            }
            else if (!isHost && (!Net.IsClient || NetSession.Status == NetStatus.Failed))
            {
                Check(false, "host-lost", "lost the host in the middle of the script, t=" + F(T) + ", " + NetSession.Error);
                Finish();
            }
        }

        // Check 2 at every PeerReady: in time, the longest frame, and on the client the loading
        // screen held until the snapshot was applied.
        void WatchHandshake()
        {
            var status = NetSession.Status;
            if (status != lastStatus)
            {
                if (status == NetStatus.Loading)
                {
                    loadAt = Now;
                    droppedEarly = false;
                }
                lastStatus = status;
            }
            bool ready = Net.PeerReady;
            if (!ready && Net.IsClient && loadAt >= 0f && NetIds.Ready && LoadingScreen.Active == null) droppedEarly = true;
            if (ready && !lastReady) OnPeerReady();
            lastReady = ready;
        }

        void OnPeerReady()
        {
            readyCount++;
            if (t0 < 0f) t0 = Now;
            float took = loadAt >= 0f ? Now - loadAt : -1f;
            string label = readyCount == 1 ? "2-handshake" : "7-handshake-again";
            Check(took >= 0f && took <= HandshakeLimit, label,
                  "PeerReady " + F(took) + " s after LoadScene, epoch " + Net.Epoch + ", ids " + NetIds.Count +
                  ", digest " + NetIds.Digest.ToString("X16") + ", tracked bodies " + NetIds.TrackedBodyCount);
            float longest = NetStats.LastLongestFrame;
            Check(longest < LongestFrameLimit, "2-longest-frame", F(longest * 1000f) + " ms between LoadScene and PeerReady");
            if (!isHost)
                Check(!droppedEarly && LoadingScreen.Active != null, "2-loading-held",
                      droppedEarly ? "the loading screen went before the snapshot was applied" : "the loading screen was up until SnapshotEnd");
            loadAt = -1f;
        }

        // =====================================================================
        // Host script
        // =====================================================================

        IEnumerator HostScript()
        {
            while (readyCount == 0) yield return null;
            var tg = NetTestTargets.Pick();
            Info("targets " + tg.Describe());

            while (T < HostUnlockAt) yield return null;
            int unlocked = DoorLock.UnlockAll(DoorLock.HouseKey, Actors.World);
            Info("host unlocked " + unlocked + " house doors, so the client's E opens the front door");

            // Section 10: either player's E skips the card. The host presses nothing.
            while (T < IntroDeadline && IntroShowing()) yield return null;
            if (IntroShowing())
            {
                Check(false, "10-intro-skip", "the card was still up at " + IntroDeadline + " s: the client's E never reached the host; skipped by the test hook");
                GameSession.Current.SendMessage("DebugSkipIntroCard", SendMessageOptions.DontRequireReceiver);
            }
            else Check(true, "10-intro-skip", "the card went at t=" + F(T) + " s with no key pressed on the host");

            while (T < BlastAt - 1f) yield return null;
            Info("host view: " + tg.HostView());

            while (T < BlastAt) yield return null;
            yield return StartCoroutine(HostBlast());

            while (T < PoseListAt) yield return null;
            NetTestPoses.PrintResting();

            while (T < EndCardAt) yield return null;
            if (RunDebugKey(KeyCode.F6, false)) Info("F6 (force Completed) run at t=" + F(T));
            else Check(false, "7-reload", "F6 is not registered in DebugCommands");

            byte e0 = Net.Epoch;
            int r0 = readyCount;
            float until = Now + 30f;
            while (Net.Epoch == e0 && Now < until) yield return null;
            if (Net.Epoch == e0) Check(false, "7-reload", "no reload within 30 s of the end card: the client's E did not reach the host");
            else
            {
                until = Now + 30f;
                while (readyCount == r0 && Now < until) yield return null;
                Check(readyCount > r0 && Net.Epoch == (byte)(e0 + 1), "7-reload",
                      "epoch " + e0 + " to " + Net.Epoch + ", " + (readyCount > r0 ? "the client is ready again" : "no new Ready in 30 s"));
            }
            expectDisconnect = true;

            float settle = Now + 3f;
            while (Now < settle) yield return null;
            FinalChecks();

            if (weQuit)
            {
                Info("the host quits now: the client checks it lands on the title with net.hostLeft (check 8)");
                Finish();
                yield break;
            }
            yield return StartCoroutine(HostWatchesClientLeave());
            Finish();
        }

        IEnumerator HostBlast()
        {
            var p2 = CrewRoster.Get(Net.ClientMember);
            if (p2 == null)
            {
                Check(false, "4-knockback-host", "no P2 on the host");
                yield break;
            }
            Vector3 before = p2.Position;
            Vector3 ahead = p2.transform.forward;
            ahead.y = 0f;
            ahead = ahead.sqrMagnitude > 1e-4f ? ahead.normalized : Vector3.forward;
            Vector3 at = before + ahead * BlastAhead + Vector3.up * 0.5f;
            Info("detonating at " + V(at) + ", " + BlastAhead + " m in front of P2 at " + V(before));
            Explosion.Detonate(at, BlastRadius, 1f, Actors.World);

            float until = Now + 1.5f, moved = 0f;
            while (Now < until && p2 != null)
            {
                moved = Mathf.Max(moved, (p2.Position - before).magnitude);
                yield return null;
            }
            Check(moved >= 1f, "4-knockback-host", "P2's puppet on the host moved " + F(moved) + " m within 1.5 s of the blast");
        }

        IEnumerator HostWatchesClientLeave()
        {
            Info("waiting for the client to quit (check 8, client killed)");
            float until = Now + 90f;
            while (Net.PeerConnected && Now < until) yield return null;
            if (Net.PeerConnected)
            {
                Check(false, "8-client-killed", "the client never left");
                yield break;
            }
            var p2 = CrewRoster.Get(Net.ClientMember);
            float y0 = p2 != null ? p2.Position.y : 0f;
            float settle = Now + 3f;
            while (Now < settle) yield return null;

            Check(partnerLeftToast, "8-host-toast", partnerLeftToast ? "net.partnerLeft shown" : "no net.partnerLeft notice");
            Check(p2 != null && Net.Drives(p2), "8-p2-idle", p2 == null ? "P2's body is gone" : "the host now simulates P2's body");
            if (p2 != null)
            {
                bool grounded = p2.Controller != null && p2.Controller.Grounded;
                float drop = y0 - p2.Position.y;
                Check(grounded || drop > 0.05f, "8-p2-gravity", "grounded " + grounded + ", dropped " + F(drop) + " m in 3 s");
            }
            bool running = Net.IsHost && GameSession.Current != null && SceneManager.GetActiveScene().name == SceneFlow.GameScene;
            Check(running, "8-run-continues", running ? "still hosting Map01" : "the host left the game scene");
        }

        // =====================================================================
        // Client script
        // =====================================================================

        IEnumerator ClientScript()
        {
            while (readyCount == 0) yield return null;
            var tg = NetTestTargets.Pick();
            Info("targets " + tg.Describe());

            // Stage 0: E through the raw input frame skips the host's intro card (section 10).
            float until = Now + 25f, nextPress = Now + 1.2f;
            while (Now < until && (IntroShowing() || P2Muted()))
            {
                if (Now >= nextPress && pilot.Member != null)
                {
                    pilot.source.Press(CrewButton.Interact);
                    nextPress = Now + 0.8f;
                }
                yield return null;
            }
            Check(!IntroShowing() && !P2Muted(), "10-intro-skip-client",
                  "the crew is free at t=" + F(T) + " s" + (P2Muted() ? " (P2 still muted)" : ""));

            yield return StartCoroutine(StageDoor(tg));
            yield return StartCoroutine(StageChair(tg));
            yield return StartCoroutine(StageStandStill());

            while (T < PoseCompareAt) yield return null;
            NetTestPoses.Compare(peerLogPath, Check);

            yield return StartCoroutine(StageEndCard());
            expectDisconnect = true;

            float settle = Now + 3f;
            while (Now < settle) yield return null;
            FinalChecks();

            if (weQuit)
            {
                Info("the client quits now: the host checks P2 stays as an idle body (check 8)");
                Finish();
                yield break;
            }
            Info("waiting for the host to go (check 8, host killed)");
            until = Now + 90f;
            while (Now < until && !(Net.Role == NetRole.Offline && SceneManager.GetActiveScene().name == SceneFlow.MenuScene))
                yield return null;
            bool home = Net.Role == NetRole.Offline && SceneManager.GetActiveScene().name == SceneFlow.MenuScene;
            Check(home && NetSession.Error == NetError.HostLeft, "8-host-killed",
                  (home ? "back on the title" : "not back on the title") + ", error " + NetSession.Error +
                  " (" + NetSession.LocKey(NetSession.Error) + ")");
            Finish();
        }

        // Stage 1: walk to the front door, E, and the host's Doors Panel opens it here.
        IEnumerator StageDoor(NetTestTargets tg)
        {
            var door = tg.door;
            if (door == null)
            {
                Check(false, "3-door-open", "no front door found");
                yield break;
            }
            var me = pilot.Member;
            if (me != null) tg.OrientDoor(me.Position);
            Vector3 outside = tg.doorCenter + tg.doorOutward * 1.4f;
            outside.y = tg.doorFloorY;
            yield return StartCoroutine(Walk(outside, "the front door"));

            int presses = 0;
            while (!door.IsOpen && presses < 3 && T < StagesDeadline)
            {
                presses++;
                yield return StartCoroutine(AimAndPress(() => tg.doorCenter, CrewButton.Interact));
                float until = Now + 2.5f;
                while (!door.IsOpen && Now < until) yield return null;
            }
            Check(door.IsOpen, "3-door-open", NetTestTargets.PathOf(door.transform) + " after " + presses + " press(es)" +
                  (door.IsLocked ? ", still locked here" : ""));
        }

        // Stage 2: grab the chair, carry it, throw it into a window pane.
        IEnumerator StageChair(NetTestTargets tg)
        {
            var chair = tg.chair;
            if (chair == null || T > StagesDeadline)
            {
                Check(false, "3-chair-held", chair == null ? "no chair found" : "out of time before the chair");
                Check(false, "3-pane-broken", "nothing was thrown");
                yield break;
            }
            var me = pilot.Member;
            // The chair behind the door: in through the doorway first.
            if (tg.door != null && me != null
                && Vector3.Dot(tg.chairStart - tg.doorCenter, tg.doorOutward) < 0f
                && Vector3.Dot(me.Position - tg.doorCenter, tg.doorOutward) > 0f)
            {
                Vector3 inside = tg.doorCenter - tg.doorOutward * 1.2f;
                inside.y = tg.doorFloorY;
                yield return StartCoroutine(Walk(inside, "inside the front door"));
            }

            Vector3 approach = NetTestTargets.Approach(chair.transform.position, pilot.Member != null ? pilot.Member.Position : tg.doorCenter, 1.4f);
            approach.y = NetTestTargets.BoundsOf(chair.gameObject).min.y;
            yield return StartCoroutine(Walk(approach, "the chair"));

            int presses = 0;
            while (!HeldByMe(chair) && presses < 3 && T < StagesDeadline)
            {
                presses++;
                yield return StartCoroutine(AimAndPress(() => NetTestTargets.BoundsOf(chair.gameObject).center, CrewButton.Grab));
                float until = Now + 2f;
                while (!HeldByMe(chair) && Now < until) yield return null;
            }
            bool held = HeldByMe(chair);
            Check(held, "3-chair-held", NetTestTargets.PathOf(chair.transform) + " after " + presses + " press(es), holder " +
                  (chair.holder != null ? "P" + (chair.holder.Actor + 1) : "none"));
            if (!held)
            {
                Check(false, "3-pane-broken", "not thrown: the chair was never held");
                yield break;
            }

            var pane = tg.pane;
            if (pane == null)
            {
                Check(false, "3-pane-broken", "no window pane found near the chair");
                yield break;
            }
            if (T > StagesDeadline)
            {
                Check(false, "3-pane-broken", "out of time before the throw");
                yield break;
            }
            Vector3 pickup = pilot.Member != null ? pilot.Member.Position : tg.chairStart;
            yield return StartCoroutine(Walk(tg.paneStand, "the window pane"));
            if (pilot.Member != null) Info("carried the chair " + F(Flat(pilot.Member.Position - pickup).magnitude) + " m");

            yield return StartCoroutine(AimAndPress(() => pane.WorldBounds.center, CrewButton.Throw));
            float wait = Now + 3f;
            while (!pane.IsBroken && Now < wait) yield return null;
            Check(pane.IsBroken, "3-pane-broken", NetTestTargets.PathOf(pane.transform) + (pane.IsBroken ? " broken" : " intact 3 s after the throw"));
        }

        // Stage 3: stand still for the host's blast. The Impulse moves the client's own P2, and
        // the replayed Explosion comes before the replayed PlayerKnockedDown (4.2).
        IEnumerator StageStandStill()
        {
            pilot.Stop();
            while (T < StandStillAt) yield return null;

            float explosionAt = -1f;
            bool knockdown = false, knockdownAfterExplosion = false;
            Action<WorldEvent> listener = e =>
            {
                if (e.type == WorldEventType.Explosion && explosionAt < 0f) explosionAt = Time.unscaledTime;
                if (e.type == WorldEventType.PlayerKnockedDown && e.subject is CrewMember m && m.index == Net.ClientMember)
                {
                    knockdown = true;
                    knockdownAfterExplosion = explosionAt >= 0f;
                }
            };
            WorldEvents.Subscribe(listener);
            Vector3 baseline = pilot.Member != null ? pilot.Member.Position : Vector3.zero;
            float moved = 0f;
            try
            {
                while (T < BlastAt + 7f)
                {
                    var me = pilot.Member;
                    if (me != null)
                    {
                        if (explosionAt < 0f) baseline = me.Position;
                        else if (Now <= explosionAt + 0.5f) moved = Mathf.Max(moved, (me.Position - baseline).magnitude);
                    }
                    if (explosionAt >= 0f && knockdown && Now > explosionAt + 0.6f) break;
                    yield return null;
                }
            }
            finally { WorldEvents.Unsubscribe(listener); }

            Check(explosionAt >= 0f && moved >= 1f, "4-knockback",
                  explosionAt < 0f ? "no Explosion replayed by t=" + F(T) : "P2 moved " + F(moved) + " m within 0.5 s of the replayed Explosion");
            Check(knockdownAfterExplosion, "4-knockdown",
                  !knockdown ? "no PlayerKnockedDown replayed for P2" : knockdownAfterExplosion ? "PlayerKnockedDown for P2 after its Explosion" : "PlayerKnockedDown came before the Explosion");
        }

        // Stage 4: on the end card, E replays for both (3.6): one reload, epoch + 1.
        IEnumerator StageEndCard()
        {
            while (!Session.IsOver && T < StageTimeout) yield return null;
            if (!Session.IsOver)
            {
                Check(false, "7-reload", "the end card never showed here (the host's F6 at " + EndCardAt + " s)");
                yield break;
            }
            float delay = GameSession.Current != null ? GameSession.Current.Numbers.restartDelay : 3f;
            float until = Now + delay + 0.5f;
            while (Now < until) yield return null;

            byte e0 = Net.Epoch;
            int r0 = readyCount;
            for (int i = 0; i < 6 && Net.Epoch == e0; i++)
            {
                pilot.source.Press(CrewButton.Interact);
                until = Now + 1.2f;
                while (Net.Epoch == e0 && Now < until) yield return null;
            }
            if (Net.Epoch == e0)
            {
                Check(false, "7-reload", "E on the end card did not reload the game");
                yield break;
            }
            until = Now + 30f;
            while (readyCount == r0 && Now < until) yield return null;
            Check(readyCount > r0 && Net.Epoch == (byte)(e0 + 1), "7-reload",
                  "epoch " + e0 + " to " + Net.Epoch + ", " + (readyCount > r0 ? "new snapshot applied" : "no new snapshot in 30 s"));
        }

        // =====================================================================
        // Shared steps
        // =====================================================================

        // Walks straight at the point. Stuck or too slow: teleports there (logged), so one
        // fence does not fail every later stage.
        IEnumerator Walk(Vector3 point, string what)
        {
            float start = Now, best = float.MaxValue, bestAt = Now;
            while (true)
            {
                if (pilot.Member == null) yield break;
                float d = pilot.Steer(point);
                if (d < NetTestPilot.ArriveRadius) break;
                if (d < best - 0.2f) { best = d; bestAt = Now; }
                bool stuck = Now - bestAt > 2.5f;
                if (stuck || Now - start > 10f || T > StagesDeadline)
                {
                    pilot.Stop();
                    pilot.TeleportTo(point);
                    Info("walk to " + what + ": " + (stuck ? "stuck" : "too slow") + " " + F(d) + " m away, teleported to " + V(point));
                    yield return null;
                    break;
                }
                yield return null;
            }
            pilot.Stop();
        }

        // Turns until the crosshair holds on the point, then one press. The look then stays
        // still for a few frames, so the host's playout aims with the same camera.
        IEnumerator AimAndPress(Func<Vector3> point, CrewButton button)
        {
            float until = Now + 3f, steadySince = -1f;
            while (Now < until && pilot.Member != null)
            {
                float err = pilot.Aim(point(), out _);
                if (err < 2f)
                {
                    if (steadySince < 0f) steadySince = Now;
                    if (Now - steadySince >= 0.25f) break;
                }
                else steadySince = -1f;
                yield return null;
            }
            pilot.Stop();
            pilot.source.Press(button);
            float hold = Now + 0.2f;
            while (Now < hold) yield return null;
        }

        void FinalChecks()
        {
            Check(NetStats.DroppedOversize == 0, "5-oversize", NetStats.DroppedOversize + " oversize records dropped here");
            if (!isHost) Check(localRaises == 0, "5-no-double-raise", localRaises + " WorldEvents raised locally on the client");
            Check(errors == 0, "9-console-errors", errors + " console errors here");
            Info("9-bandwidth: read the NETSTATS lines (down < 80 KB/s peak and < 10 KB/s steady, up < 10 KB/s)");
        }

        bool IntroShowing()
        {
            var s = GameSession.Current;
            return s != null && s.IntroCardShowing;
        }

        bool P2Muted()
        {
            var me = pilot.Member;
            return me == null || me.Input == null || me.Input.Muted;
        }

        static bool HeldByMe(MovableObject mo)
        {
            return mo != null && mo.holder != null && mo.holder.Actor == Net.ClientMember;
        }

        static bool RunDebugKey(KeyCode key, bool shift)
        {
            var all = DebugCommands.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].key != key || all[i].shift != shift || all[i].run == null) continue;
                all[i].run();
                return true;
            }
            return false;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        internal static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        internal static string V(Vector3 v) => "(" + F(v.x) + " " + F(v.y) + " " + F(v.z) + ")";
    }

    // Drives the client's own P2 through a ScriptedInputSource, the same CrewInput a player goes
    // through: the look is closed-loop (turn by the error, every frame), the walk is straight.
    sealed class NetTestPilot
    {
        public const float ArriveRadius = 0.35f;
        const float Gain = 0.35f;
        const float MaxStepDegrees = 25f;
        const float WalkTurnLimit = 35f;
        const int MaxRebinds = 5;

        public readonly ScriptedInputSource source = new ScriptedInputSource();
        CrewMember member;
        int rebinds;

        public CrewMember Member => member;

        // Takes P2's input, again after a reload (a new body) or if something replaced it.
        // Returns false when the source is not ours.
        public bool Bind(int index)
        {
            var m = CrewRoster.Get(index);
            if (m == null || m.Input == null) return true;
            if (m == member && ReferenceEquals(m.Input.Source, source)) return true;
            if (m == member && rebinds > MaxRebinds) return false;
            if (m == member) rebinds++;
            member = m;
            source.ReleaseAll();
            m.Input.SetSource(source);
            return true;
        }

        public void Stop()
        {
            source.move = Vector2.zero;
            source.look = Vector2.zero;
        }

        // Sets this frame's look toward the point; returns the larger angle left, degrees.
        public float Aim(Vector3 point, out float yawError)
        {
            yawError = 0f;
            if (member == null) return 180f;
            Transform cam = member.View != null ? member.View.transform : member.transform;
            Vector3 to = point - cam.position;
            float flat = new Vector2(to.x, to.z).magnitude;
            yawError = Mathf.DeltaAngle(member.transform.eulerAngles.y, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            float wantPitch = Mathf.Atan2(-to.y, Mathf.Max(0.01f, flat)) * Mathf.Rad2Deg;   // positive looks down
            float pitchError = Mathf.DeltaAngle(0f, cam.localEulerAngles.x) - wantPitch;

            // PlayerController turns by look * GameSettings.ApplyLook * mouseSensitivity degrees.
            Vector2 unit = GameSettings.ApplyLook(Vector2.one);
            float sens = member.Controller != null ? member.Controller.mouseSensitivity : 2f;
            float kx = Mathf.Abs(unit.x * sens) > 1e-4f ? unit.x * sens : 1f;
            float ky = Mathf.Abs(unit.y * sens) > 1e-4f ? unit.y * sens : 1f;
            source.look = new Vector2(Mathf.Clamp(yawError * Gain, -MaxStepDegrees, MaxStepDegrees) / kx,
                                      Mathf.Clamp(pitchError * Gain, -MaxStepDegrees, MaxStepDegrees) / ky);
            return Mathf.Max(Mathf.Abs(yawError), Mathf.Abs(pitchError));
        }

        // Faces the point at eye level and walks while roughly facing it; returns the flat distance.
        public float Steer(Vector3 point)
        {
            if (member == null) return 0f;
            Vector3 flat = point - member.Position;
            flat.y = 0f;
            float dist = flat.magnitude;
            Aim(new Vector3(point.x, member.EyePosition.y, point.z), out float yawError);
            source.move = dist > ArriveRadius && Mathf.Abs(yawError) < WalkTurnLimit ? Vector2.up : Vector2.zero;
            return dist;
        }

        // Puts the feet on whatever floor is under the point.
        public void TeleportTo(Vector3 point)
        {
            var pc = member != null ? member.Controller : null;
            if (pc == null) return;
            float floor = point.y;
            if (Physics.Raycast(point + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                floor = hit.point.y;
            pc.Teleport(new Vector3(point.x, floor - pc.FeetHeight + 0.05f, point.z), member.transform.eulerAngles.y);
        }
    }

    // What the script plays with, looked up the same way on both machines from the scene as it
    // loaded: the front door (the house-key door nearest the truck), a chair near it, and a
    // window pane a few metres from the chair. Both sides log what they picked.
    sealed class NetTestTargets
    {
        const float PaneStandOff = 2.6f;   // the carry holds the chair ~2.2 m out: it ends up at the glass

        public HingedPanel door;
        public Vector3 doorCenter, doorOutward = Vector3.forward;
        public float doorFloorY;
        public MovableObject chair;
        public Vector3 chairStart;
        public GlassPane pane;
        public Vector3 paneStand;

        public static NetTestTargets Pick()
        {
            var tg = new NetTestTargets();
            var truck = UnityEngine.Object.FindFirstObjectByType<TruckVehicle>();
            var p2 = CrewRoster.Get(Net.ClientMember);
            Vector3 reference = truck != null ? truck.transform.position : p2 != null ? p2.Position : Vector3.zero;

            tg.door = FrontDoor(reference);
            if (tg.door != null)
            {
                Bounds b = tg.door.TryGetClosedBounds(null, out Bounds shut) ? shut : BoundsOf(tg.door.gameObject);
                tg.doorCenter = b.center;
                tg.doorFloorY = b.min.y;
                tg.OrientDoor(reference);
            }
            tg.chair = Chair(tg.door != null ? tg.doorCenter : reference);
            if (tg.chair != null)
            {
                tg.chairStart = tg.chair.transform.position;
                float floor = BoundsOf(tg.chair.gameObject).min.y;
                tg.pane = Pane(tg.chairStart, floor);
                if (tg.pane != null)
                {
                    tg.paneStand = StandPoint(tg.pane, tg.chairStart, PaneStandOff);
                    tg.paneStand.y = floor;
                }
            }
            return tg;
        }

        // The door's thin horizontal axis, pointing to the side the point is on.
        public void OrientDoor(Vector3 side)
        {
            if (door == null) return;
            doorOutward = ThinAxisToward(door.TryGetClosedBounds(null, out Bounds b) ? b : BoundsOf(door.gameObject), side);
        }

        public string Describe()
        {
            return "door " + (door != null ? PathOf(door.transform) + " at " + NetTestBot.V(doorCenter) : "none") +
                   ", chair " + (chair != null ? PathOf(chair.transform) + " id " + NetIds.IdOf(chair.gameObject) + " at " + NetTestBot.V(chairStart) : "none") +
                   ", pane " + (pane != null ? PathOf(pane.transform) + " at " + NetTestBot.V(pane.WorldBounds.center) : "none");
        }

        public string HostView()
        {
            return "door open " + (door != null && door.IsOpen) +
                   ", chair holder " + (chair != null && chair.holder != null ? "P" + (chair.holder.Actor + 1) : "none") +
                   ", pane broken " + (pane != null && pane.IsBroken);
        }

        static HingedPanel FrontDoor(Vector3 reference)
        {
            HingedPanel best = null;
            float bestScore = float.MaxValue;
            var locks = DoorLock.All;
            for (int i = 0; i < locks.Count; i++)
            {
                var l = locks[i];
                if (l == null || l.keyId != DoorLock.HouseKey) continue;
                var p = l.Panel;
                if (p == null || p.isWindow || !p.isActiveAndEnabled) continue;
                Vector3 d = p.transform.position - reference;
                d.y = 0f;
                float score = d.sqrMagnitude + (UnderExteriorDoor(p.transform) ? 0f : 1e6f);
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        static bool UnderExteriorDoor(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith("EXT_DOOR", StringComparison.Ordinal)) return true;
            return false;
        }

        static MovableObject Chair(Vector3 near)
        {
            MovableObject best = null;
            float bestScore = float.MaxValue;
            foreach (var mo in UnityEngine.Object.FindObjectsByType<MovableObject>(FindObjectsSortMode.None))
            {
                if (mo == null || !mo.isActiveAndEnabled || mo.inPocket || mo.worn || mo.destroyed || mo.pocketable) continue;
                if (!mo.canCarry || !mo.canThrow || mo.weight < 1f || mo.weight > 40f) continue;
                if (mo.GetComponent<Rigidbody>() == null || NetIds.IdOf(mo.gameObject) == 0) continue;
                float score = (mo.transform.position - near).magnitude + (IsChair(mo) ? 0f : 1000f);
                if (score < bestScore || (score == bestScore && best != null && NetIds.IdOf(mo.gameObject) < NetIds.IdOf(best.gameObject)))
                {
                    bestScore = score;
                    best = mo;
                }
            }
            return best;
        }

        static bool IsChair(MovableObject mo)
        {
            return Has(mo.name, "chair") || Has(mo.displayName, "chair") || Has(mo.name, "chaise") || Has(mo.displayName, "chaise")
                || Has(mo.name, "stool") || Has(mo.name, "tabouret");
        }

        static bool Has(string s, string part) => s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        // A pane at chest height, 3 to 14 m from the chair, about 6 m away by preference, with
        // a clear line from the chair to where the thrower stands.
        static GlassPane Pane(Vector3 from, float floor)
        {
            GlassPane best = null;
            float bestScore = float.MaxValue;
            foreach (var pane in UnityEngine.Object.FindObjectsByType<GlassPane>(FindObjectsSortMode.None))
            {
                if (pane == null || pane.IsGone) continue;
                Bounds b = pane.WorldBounds;
                if (b.size.sqrMagnitude < 0.04f) continue;
                float height = b.center.y - floor;
                if (height < 0.5f || height > 2.1f) continue;
                Vector3 d = b.center - from;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < 3f || dist > 14f) continue;
                float score = Mathf.Abs(dist - 6f);
                Vector3 stand = StandPoint(pane, from, PaneStandOff);
                if (!ClearLine(from + Vector3.up * 0.8f, new Vector3(stand.x, floor + 1f, stand.z))) score += 100f;
                if (score < bestScore || (score == bestScore && best != null && NetIds.IdOf(pane.gameObject) < NetIds.IdOf(best.gameObject)))
                {
                    bestScore = score;
                    best = pane;
                }
            }
            return best;
        }

        public static Vector3 StandPoint(GlassPane pane, Vector3 side, float distance)
        {
            Bounds b = pane.WorldBounds;
            return b.center + ThinAxisToward(b, side) * distance;
        }

        static Vector3 ThinAxisToward(Bounds b, Vector3 side)
        {
            Vector3 axis = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            return Vector3.Dot(side - b.center, axis) >= 0f ? axis : -axis;
        }

        static bool ClearLine(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            foreach (var hit in Physics.RaycastAll(a, d / len, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<MovableObject>() != null) continue;
                if (hit.collider.GetComponentInParent<CrewMember>() != null) continue;
                return false;
            }
            return true;
        }

        // A point `distance` from the target, on the side of `from`.
        public static Vector3 Approach(Vector3 target, Vector3 from, float distance)
        {
            Vector3 d = from - target;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
            return target + d.normalized * distance;
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; }
                else b.Encapsulate(c.bounds);
            }
            return b;
        }

        public static string PathOf(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }

    // Check 6: 20 tracked bodies, the same seeded pick on both machines. The host prints those
    // at rest; the client compares them with its own, when it can read the host's log.
    static class NetTestPoses
    {
        const int Count = 20;
        const int Seed = 20260927;
        const string Tag = "NETTEST POSE ";
        const float MaxMetres = 0.01f;
        const float MaxDegrees = 1f;

        static List<uint> Candidates()
        {
            var ids = new List<uint>();
            foreach (uint id in NetIds.TrackedBodies)
                if ((id & NetIds.SpawnBit) == 0) ids.Add(id);
            ids.Sort();
            var rng = new System.Random(Seed);
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }
            return ids;
        }

        public static void PrintResting()
        {
            int printed = 0;
            foreach (uint id in Candidates())
            {
                if (printed >= Count) break;
                var go = NetIds.Find(id);
                if (go == null || !go.activeInHierarchy || !AtRest(go)) continue;
                Transform t = go.transform;
                NetLog.Write(Tag + id + " " + F(t.position.x) + " " + F(t.position.y) + " " + F(t.position.z) + " " +
                             F(t.rotation.x) + " " + F(t.rotation.y) + " " + F(t.rotation.z) + " " + F(t.rotation.w));
                printed++;
            }
            NetLog.Write("NETTEST INFO 6-transforms: the host printed " + printed + " resting bodies");
        }

        public static void Compare(string hostLog, Action<bool, string, string> check)
        {
            if (string.IsNullOrEmpty(hostLog))
            {
                NetLog.Write("NETTEST INFO 6-transforms: no -netbotpeerlog, compare the host's POSE lines by hand");
                return;
            }
            string[] lines;
            try
            {
                using (var fs = new FileStream(hostLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                    lines = reader.ReadToEnd().Split('\n');
            }
            catch (Exception e)
            {
                check(false, "6-transforms", "cannot read the host log " + hostLog + ": " + e.Message);
                return;
            }

            int compared = 0, bad = 0;
            float worstMetres = 0f, worstDegrees = 0f;
            uint worstId = 0;
            foreach (string line in lines)
            {
                int at = line.IndexOf(Tag, StringComparison.Ordinal);
                if (at < 0 || !TryParse(line.Substring(at + Tag.Length), out uint id, out Vector3 pos, out Quaternion rot)) continue;
                compared++;
                var go = NetIds.Find(id);
                if (go == null) { bad++; worstId = id; continue; }
                float metres = Vector3.Distance(go.transform.position, pos);
                float degrees = Quaternion.Angle(go.transform.rotation, rot);
                if (metres > MaxMetres || degrees > MaxDegrees) bad++;
                if (metres > worstMetres || degrees > worstDegrees) worstId = id;
                worstMetres = Mathf.Max(worstMetres, metres);
                worstDegrees = Mathf.Max(worstDegrees, degrees);
            }
            check(compared > 0 && bad == 0, "6-transforms",
                  compared + " bodies compared, " + bad + " off, worst " + F(worstMetres * 100f) + " cm " + F(worstDegrees) + " deg (id " + worstId + ")");
        }

        static bool AtRest(GameObject go)
        {
            var mo = go.GetComponent<MovableObject>();
            if (mo != null && (mo.holder != null || mo.inPocket || mo.worn)) return false;
            var rb = go.GetComponent<Rigidbody>();
            return rb == null || rb.isKinematic || rb.IsSleeping()
                || (rb.linearVelocity.sqrMagnitude < 1e-4f && rb.angularVelocity.sqrMagnitude < 1e-3f);
        }

        static bool TryParse(string text, out uint id, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;
            id = 0;
            string[] p = text.Trim().Split(' ');
            if (p.Length < 8 || !uint.TryParse(p[0], out id)) return false;
            var f = new float[7];
            for (int i = 0; i < 7; i++)
                if (!float.TryParse(p[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i])) return false;
            pos = new Vector3(f[0], f[1], f[2]);
            rot = new Quaternion(f[3], f[4], f[5], f[6]);
            return true;
        }

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
