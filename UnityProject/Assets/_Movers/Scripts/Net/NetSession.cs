using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Movers
{
    public enum NetStatus : byte { Idle, Connecting, WaitingForPeer, PeerJoined, Lobby, Loading, Playing, Failed }
    public enum NetError : byte
    {
        None, ServicesUnavailable, NotLinked, BadCode, ConnectFailed, Timeout, VersionMismatch, SessionFull, HostLeft,
        PaymentRequired
    }

    // The connection, and the only file that references NGO or Unity Services (NETCODE_SLICE 3,
    // ADR-012). NGO carries two named messages, "mv.r" and "mv.u"; everything inside them is
    // Movers records (NetOut). The _Net root is created only by HostRelay, HostDirect, Join or
    // the command line; the static getters never create it, so the title screen polls offline.
    [DefaultExecutionOrder(NetOrder.Tick)]
    public sealed class NetSession : MonoBehaviour
    {
        public const ushort DefaultPort = 7777;
        public const ushort Protocol = 1;

        const string ReliableName = "mv.r";
        const string UnreliableName = "mv.u";
        const ulong NoPeer = ulong.MaxValue;
        const float HostReadyTimeout = 30f;
        const float ClientSnapshotTimeout = 20f;

        // Control ops (sys 0, 4.3). 12 and 13 measure the round trip (NetStats, Net.RoundTrip).
        const byte OpHello = 1, OpWelcome = 2, OpLoadScene = 4, OpReady = 5, OpSnapshotBegin = 6, OpSnapshotEnd = 7,
                   OpReload = 8, OpToMenu = 9, OpLeave = 10, OpReplayRequest = 11, OpPing = 12, OpPong = 13;

        static NetSession instance;
        static NetStatus status;
        static NetError error;
        static string errorDetail, joinCode, menuMessage;

        public static NetStatus Status => status;
        public static NetError Error => error;
        public static string ErrorDetail => errorDetail;
        public static string JoinCode => joinCode;
        public static event Action StatusChanged;
        // A Loc key for a toast on the local HUD ("net.partnerLeft"). UI subscribes; offline silent.
        public static event Action<string> Notice;
        // The -netbot script ("host", "client"), or null; NetTestBot reads it.
        public static string BotScript => botScript;

        // ---- command line (both roles) ----
        // -nethost [port|relay], -netjoin <code|ip[:port]>, -netautostart, -netlog <path>,
        // -netbot <script>, -netdumpids <path>.
        static string cmdHost, cmdJoin, botScript, dumpIdsPath;
        static bool autoStart;

        // ---- per-root state ----
        UnityTransport utp;
        NetworkManager nm;
        Action<NetSession> pendingStart;
        bool disposing;
        int attempt;
        byte[] recvBuf = new byte[2048];

        // ---- per-session state ----
        ulong peerId = NoPeer;
        bool connectedOnce, gameStarted, leaving, resetRoleOnMenu, reloadPending, hostSweepDone, peerGone;
        int refusedEpoch = -1, replayEpoch = -1;
        float readyDeadline, snapshotDeadline, nextPing;
        bool hasPendingReady;
        byte pendingReadyEpoch;
        ushort pendingReadyCount;
        ulong pendingReadyDigest;
        enum PendingLoad : byte { None, Game, Reload, Menu }
        PendingLoad pendingLoad;
        int pendingPlayers = 2;

        // =====================================================================
        // Menu-facing API
        // =====================================================================

        public static void HostRelay() { Begin(s => s.StartHostRelay()); }

        public static void HostDirect(ushort port = DefaultPort) { Begin(s => s.StartHostDirect(port)); }

        public static void Join(string codeOrAddress)
        {
            string text = (codeOrAddress ?? "").Trim();
            string upper = text.ToUpperInvariant();
            if (IsRelayCode(upper)) { Begin(s => s.StartJoinRelay(upper)); return; }
            if (text.IndexOf('.') >= 0 || text.IndexOf(':') >= 0)
            {
                string ip = text;
                ushort port = DefaultPort;
                int colon = text.LastIndexOf(':');
                if (colon > 0)
                {
                    ip = text.Substring(0, colon);
                    if (!ushort.TryParse(text.Substring(colon + 1), out port)) { SetFailed(NetError.BadCode, "bad port in '" + text + "'"); return; }
                }
                Begin(s => s.StartJoinDirect(ip, port));
                return;
            }
            SetFailed(NetError.BadCode, "not a code or an address: '" + text + "'");
        }

        public static void StartGame()
        {
            var s = instance;
            if (s == null || !Net.IsHost || !Net.PeerConnected || s.gameStarted) return;
            s.gameStarted = true;
            byte e = (byte)(Net.Epoch + 1);
            var w = NetOut.Reliable(NetSyncId.Control, OpLoadScene);
            if (w != null)
            {
                w.WriteByte(e);
                w.WriteString(SceneFlow.GameScene);
                w.WriteByte(2);
                NetOut.End(w);
            }
            Net.SetPeer(true, false);
            Net.SetEpoch(e);
            s.hostSweepDone = false;
            s.readyDeadline = Time.unscaledTime + HostReadyTimeout;
            NetStats.BeginLoadWindow();
            NetLog.Write("LoadScene epoch " + e);
            SetStatus(NetStatus.Loading);
            s.QueueLoad(PendingLoad.Game, 2);
        }

        public static void Cancel()
        {
            var s = instance;
            if (s == null) { SetStatus(NetStatus.Idle); return; }
            s.pendingStart = null;
            s.EndScene();
            s.StopTransport();
            Net.SetRole(NetRole.Offline);
            error = NetError.None;
            SetStatus(NetStatus.Idle);
        }

        public static void LeaveToMenu()
        {
            var s = instance;
            if (s == null || !Net.IsOnline) { SceneFlow.LoadMenu(); return; }
            if (s.leaving) return;
            s.leaving = true;
            var w = NetOut.Reliable(NetSyncId.Control, Net.IsHost ? OpToMenu : OpLeave);
            if (w != null)
            {
                if (Net.IsHost) w.WriteByte((byte)NetError.HostLeft);
                NetOut.End(w);
            }
            NetOut.FlushNow();
            NetLog.Write("leave to menu");
            s.EndScene();
            s.StopTransport();
            s.resetRoleOnMenu = true;
            SetStatus(NetStatus.Idle);
            s.QueueLoad(PendingLoad.Menu, 0);
        }

        // Every host reload goes through here (3.6): E on the end card, F5, "Rejouer", a
        // ReplayRequest. A no-op while loading or while a reload for this epoch is pending.
        public static void ReloadForBoth()
        {
            var s = instance;
            if (s == null || !Net.IsHost) return;
            if (SceneFlow.IsLoading || s.reloadPending || s.leaving) return;
            s.reloadPending = true;
            byte e = (byte)(Net.Epoch + 1);
            var w = NetOut.Reliable(NetSyncId.Control, OpReload);
            if (w != null) { w.WriteByte(e); NetOut.End(w); }
            s.EndScene();
            bool peer = Net.PeerConnected;
            Net.SetPeer(peer, false);
            Net.SetEpoch(e);
            s.hostSweepDone = false;
            s.hasPendingReady = false;
            s.readyDeadline = peer ? Time.unscaledTime + HostReadyTimeout : 0f;
            NetStats.BeginLoadWindow();
            NetLog.Write("Reload epoch " + e);
            SetStatus(NetStatus.Loading);
            s.QueueLoad(PendingLoad.Reload, 0);
        }

        public static void RequestReplay()
        {
            var s = instance;
            if (s == null || !Net.IsClient || SceneFlow.IsLoading || s.replayEpoch == Net.Epoch) return;
            s.replayEpoch = Net.Epoch;
            var w = NetOut.Reliable(NetSyncId.Control, OpReplayRequest);
            if (w != null) NetOut.End(w);
        }

        public static string TakeMenuMessage()
        {
            string m = menuMessage;
            menuMessage = null;
            return m;
        }

        // The Loc key the menu shows for an error (3.4).
        public static string LocKey(NetError e)
        {
            switch (e)
            {
                case NetError.ServicesUnavailable: return "net.services";
                case NetError.NotLinked: return "net.notLinked";
                case NetError.PaymentRequired: return "net.payment";
                case NetError.BadCode: return "net.badCode";
                case NetError.Timeout:
                case NetError.ConnectFailed: return "net.failed";
                case NetError.VersionMismatch: return "net.version";
                case NetError.SessionFull: return "net.full";
                case NetError.HostLeft: return "net.hostLeft";
                default: return null;
            }
        }

        // For the editor test CLI, which has no command line of its own (14).
        public static void SetTestOptions(string logPath, string bot, string dumpIds, bool autostart)
        {
            if (logPath != null) NetLog.SetPath(logPath);
            botScript = bot;
            Net.SetScripted(!string.IsNullOrEmpty(bot));
            dumpIdsPath = dumpIds;
            autoStart = autostart;
        }

        // =====================================================================
        // NetOut and NetIdSweep entry points
        // =====================================================================

        internal static void Send(bool reliable, byte[] buf, int len)
        {
            var s = instance;
            if (s == null || s.nm == null || !s.nm.IsListening || len <= 0) return;
            ulong target = NetworkManager.ServerClientId;
            if (Net.IsHost)
            {
                if (s.peerId == NoPeer) return;
                target = s.peerId;
            }
            var w = new FastBufferWriter(len, Allocator.Temp);
            try
            {
                w.WriteBytesSafe(buf, len);
                s.nm.CustomMessagingManager?.SendNamedMessage(reliable ? ReliableName : UnreliableName, target, w,
                    reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced);
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { w.Dispose(); }
        }

        internal static void OnSweepDone()
        {
            var s = instance;
            if (s == null || !Net.IsOnline) return;
            s.reloadPending = false;
            if (!string.IsNullOrEmpty(dumpIdsPath)) NetIds.DumpTo(dumpIdsPath);
            if (Net.IsHost)
            {
                s.hostSweepDone = true;
                if (s.hasPendingReady)
                {
                    s.hasPendingReady = false;
                    s.ProcessReady(s.pendingReadyEpoch, s.pendingReadyCount, s.pendingReadyDigest);
                }
            }
            else
            {
                var w = NetOut.Reliable(NetSyncId.Control, OpReady);
                if (w != null)
                {
                    w.WriteByte(Net.Epoch);
                    w.WriteUShort((ushort)Mathf.Min(NetIds.Count, ushort.MaxValue));
                    w.WriteULong(NetIds.Digest);
                    NetOut.End(w);
                }
                s.snapshotDeadline = Time.unscaledTime + ClientSnapshotTimeout;
            }
        }

        // =====================================================================
        // Creation, start and stop
        // =====================================================================

        static bool IsRelayCode(string s)
        {
            if (s.Length != 6) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            }
            return true;
        }

        static void Begin(Action<NetSession> start)
        {
            if (instance == null) CreateRoot();
            error = NetError.None;
            errorDetail = null;
            joinCode = null;
            SetStatus(NetStatus.Connecting);
            instance.disposing = false;
            instance.pendingStart = start;   // run in Update once any previous session has shut down
        }

        static void CreateRoot()
        {
            var go = new GameObject("_Net");
            go.SetActive(false);   // configure before NGO's Awake and OnEnable
            DontDestroyOnLoad(go);
            var utp = go.AddComponent<UnityTransport>();
            utp.ConnectTimeoutMS = 1000;
            utp.MaxConnectAttempts = 15;
            utp.HeartbeatTimeoutMS = 500;
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp, EnableSceneManagement = false, PlayerPrefab = null,
                ConnectionApproval = true, ForceSamePrefabs = false, TickRate = 30,
            };
            instance = go.AddComponent<NetSession>();
            instance.utp = utp;
            instance.nm = nm;
            go.SetActive(true);
            nm.ConnectionApprovalCallback = instance.Approve;
            nm.OnClientConnectedCallback += instance.OnClientConnected;
            nm.OnClientDisconnectCallback += instance.OnClientDisconnected;
            nm.OnTransportFailure += instance.OnTransportFailure;
        }

        void ResetSessionState()
        {
            attempt++;
            peerId = NoPeer;
            connectedOnce = gameStarted = leaving = resetRoleOnMenu = reloadPending = hostSweepDone = peerGone = false;
            hasPendingReady = false;
            refusedEpoch = replayEpoch = -1;
            readyDeadline = snapshotDeadline = 0f;
            nextPing = 0f;
            pendingLoad = PendingLoad.None;
        }

        bool TransportIdle => nm == null || (!nm.ShutdownInProgress && !nm.IsListening);

        void StopTransport()
        {
            if (nm != null && (nm.IsListening || nm.ShutdownInProgress))
            {
                if (!nm.ShutdownInProgress) nm.Shutdown();
            }
            disposing = true;
        }

        void Started(NetRole role)
        {
            Net.SetRole(role);
            Net.SetPeer(false, false);
            Net.SetEpoch(0);
            NetOut.ResetQueues();
            NetOut.StartClock();
            var cmm = nm.CustomMessagingManager;
            cmm.RegisterNamedMessageHandler(ReliableName, OnMessage);
            cmm.RegisterNamedMessageHandler(UnreliableName, OnMessage);
            NetLog.Write((role == NetRole.Host ? "host" : "client") + " started, protocol " + Protocol + ", build " + BuildStamp);
        }

        static string BuildStamp => Application.isEditor ? "editor" : Application.buildGUID;

        void StartHostDirect(ushort port)
        {
            ResetSessionState();
            utp.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            if (!nm.StartHost()) { Fail(NetError.ConnectFailed, "StartHost returned false (port " + port + " in use?)"); return; }
            joinCode = LocalAddress() + ":" + port;
            Started(NetRole.Host);
            SetStatus(NetStatus.WaitingForPeer);
        }

        void StartJoinDirect(string ip, ushort port)
        {
            ResetSessionState();
            utp.SetConnectionData(ip, port);
            if (!nm.StartClient()) { Fail(NetError.ConnectFailed, "StartClient returned false for " + ip + ":" + port); return; }
            Started(NetRole.Client);
            SetStatus(NetStatus.Connecting);
        }

        async void StartHostRelay()
        {
            ResetSessionState();
            int token = attempt;
            try
            {
                await SignIn();
                if (token != attempt || this == null) return;
                Allocation alloc = await RelayService.Instance.CreateAllocationAsync(1);
                if (token != attempt || this == null) return;
                string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                if (token != attempt || this == null) return;
                utp.SetRelayServerData(alloc.ToRelayServerData(RelayProtocol.DTLS));
                if (!nm.StartHost()) { Fail(NetError.ConnectFailed, "StartHost returned false (Relay)"); return; }
                joinCode = code;
                Started(NetRole.Host);
                NetLog.Write("NETJOINCODE " + code);
                Debug.Log("NETJOINCODE " + code);
                SetStatus(NetStatus.WaitingForPeer);
            }
            catch (Exception e)
            {
                if (token == attempt && this != null) Fail(MapError(e), e.ToString());
            }
        }

        async void StartJoinRelay(string code)
        {
            ResetSessionState();
            int token = attempt;
            try
            {
                await SignIn();
                if (token != attempt || this == null) return;
                JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(code);
                if (token != attempt || this == null) return;
                utp.SetRelayServerData(join.ToRelayServerData(RelayProtocol.DTLS));
                if (!nm.StartClient()) { Fail(NetError.ConnectFailed, "StartClient returned false (Relay)"); return; }
                Started(NetRole.Client);
                SetStatus(NetStatus.Connecting);
            }
            catch (Exception e)
            {
                if (token == attempt && this != null) Fail(MapError(e), e.ToString());
            }
        }

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static NetError MapError(Exception e)
        {
            if (e is AggregateException ae && ae.InnerException != null) e = ae.InnerException;
            if (e is ServicesInitializationException) return NetError.NotLinked;
            if (e is RelayServiceException re)
            {
                switch (re.Reason)
                {
                    case RelayExceptionReason.PaymentRequired: return NetError.PaymentRequired;
                    case RelayExceptionReason.InactiveProject:
                    case RelayExceptionReason.Unauthorized:
                    case RelayExceptionReason.Forbidden: return NetError.NotLinked;
                    case RelayExceptionReason.JoinCodeNotFound:
                    case RelayExceptionReason.InvalidRequest:
                    case RelayExceptionReason.InvalidArgument:
                    case RelayExceptionReason.EntityNotFound: return NetError.BadCode;
                    default: return NetError.ServicesUnavailable;
                }
            }
            return NetError.ServicesUnavailable;
        }

        static string LocalAddress()
        {
            try
            {
                foreach (var a in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a))
                        return a.ToString();
            }
            catch (Exception) { }
            return "127.0.0.1";
        }

        // A failure in the menu (3.4): named on the status line, detail in the net log only.
        void Fail(NetError e, string detail)
        {
            EndScene();
            StopTransport();
            Net.SetRole(NetRole.Offline);
            SetFailed(e, detail);
        }

        static void SetFailed(NetError e, string detail)
        {
            error = e;
            errorDetail = detail;
            NetLog.Write("error " + e + ": " + detail);
            SetStatus(NetStatus.Failed);
        }

        static void SetStatus(NetStatus s)
        {
            if (status == s) return;
            status = s;
            try { StatusChanged?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        static void RaiseNotice(string key)
        {
            NetLog.Write("notice " + key);
            try { Notice?.Invoke(key); } catch (Exception e) { Debug.LogException(e); }
        }

        // Leave or reload: every sync unsubscribes and drops its caches; ids are recomputed by
        // the next sweep.
        void EndScene()
        {
            NetSync.ForEach(s => s.OnSessionEnd());
            NetIds.Clear();
            NetOut.InSnapshot = false;
        }

        // =====================================================================
        // NGO callbacks
        // =====================================================================

        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            res.CreatePlayerObject = false;
            res.Pending = false;
            if (req.ClientNetworkId == NetworkManager.ServerClientId) { res.Approved = true; return; }
            if (peerId == NoPeer && !gameStarted && !leaving)
            {
                peerId = req.ClientNetworkId;
                res.Approved = true;
                return;
            }
            res.Approved = false;
            res.Reason = "mv:" + (byte)NetError.SessionFull;
            NetLog.Write("refused client " + req.ClientNetworkId + ": session full");
        }

        void OnClientConnected(ulong id)
        {
            if (Net.IsHost)
            {
                if (id == NetworkManager.ServerClientId) return;
                NetLog.Write("client " + id + " connected, waiting for Hello");
                return;
            }
            if (!Net.IsClient || id != nm.LocalClientId) return;
            connectedOnce = true;
            var w = NetOut.Reliable(NetSyncId.Control, OpHello);
            if (w != null)
            {
                w.WriteUShort(Protocol);
                w.WriteString(BuildStamp);
                NetOut.End(w);
            }
            NetLog.Write("connected, Hello sent");
        }

        void OnClientDisconnected(ulong id)
        {
            if (Net.IsHost)
            {
                if (id == NetworkManager.ServerClientId || id != peerId) return;
                PeerLeft("client disconnected");
                return;
            }
            if (!Net.IsClient || leaving) return;
            if (id != nm.LocalClientId && id != NetworkManager.ServerClientId) return;
            NetError e = ParseReason(nm.DisconnectReason);
            if (e == NetError.None) e = connectedOnce ? NetError.HostLeft : NetError.Timeout;
            ClientLost(e, "disconnected: '" + nm.DisconnectReason + "'");
        }

        void OnTransportFailure()
        {
            NetLog.Write("transport failure");
            if (Net.IsClient && !leaving) { ClientLost(connectedOnce ? NetError.ConnectFailed : NetError.Timeout, "transport failure"); return; }
            if (Net.IsHost)
            {
                if (peerId != NoPeer) PeerLeft("transport failure");
                if (!gameStarted) Fail(NetError.ConnectFailed, "transport failure");
            }
        }

        static NetError ParseReason(string reason)
        {
            if (string.IsNullOrEmpty(reason) || !reason.StartsWith("mv:")) return NetError.None;
            return byte.TryParse(reason.Substring(3), out byte b) ? (NetError)b : NetError.None;
        }

        // Host side: the client left or dropped (3.5). In game the run continues with P2 idle.
        void PeerLeft(string why)
        {
            bool wasConnected = Net.PeerConnected;
            NetLog.Write("peer left: " + why);
            if (gameStarted)
            {
                if (peerGone) return;   // refused, then its disconnect callback
                peerGone = true;
                Net.SetPeer(false, false);   // Drives(P2) becomes true on the host
                NetSync.ForEach(s => s.OnPeerLeft());
                readyDeadline = 0f;
                if (wasConnected) RaiseNotice("net.partnerLeft");
                return;
            }
            peerId = NoPeer;
            Net.SetPeer(false, false);
            if (Net.IsHost && !leaving) SetStatus(NetStatus.WaitingForPeer);
        }

        void Refuse(NetError e)
        {
            if (peerId == NoPeer) return;
            NetLog.Write("refusing the client: " + e);
            ulong id = peerId;
            PeerLeft("refused " + e);
            try { nm.DisconnectClient(id, "mv:" + (byte)e); } catch (Exception ex) { Debug.LogException(ex); }
        }

        // Client side: the host is gone, refused us, or never answered (3.4, 3.5).
        void ClientLost(NetError e, string detail)
        {
            if (leaving) return;
            leaving = true;
            NetLog.Write("lost the host: " + e + " (" + detail + ")");
            if (e == NetError.VersionMismatch) foreach (var line in NetIds.DumpLines()) NetLog.Write("id " + line);
            if (gameStarted)
            {
                // Role stays Client until the menu has loaded: no gated code runs offline logic in Map01.
                error = e;
                errorDetail = detail;
                menuMessage = LocKey(e);
                EndScene();
                StopTransport();
                resetRoleOnMenu = true;
                SetStatus(NetStatus.Failed);
                QueueLoad(PendingLoad.Menu, 0);
                return;
            }
            Fail(e, detail);
        }

        void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (Net.IsHost && sender != peerId) return;
            int len = reader.Length - reader.Position;
            if (len <= 0) return;
            if (recvBuf.Length < len) recvBuf = new byte[Mathf.Max(len, recvBuf.Length * 2)];
            reader.ReadBytesSafe(ref recvBuf, len);
            NetOut.Dispatch(recvBuf, len);
        }

        // =====================================================================
        // Control records
        // =====================================================================

        void OnControl(byte op, NetReader r)
        {
            switch (op)
            {
                case OpHello:
                {
                    if (!Net.IsHost) return;
                    ushort protocol = r.ReadUShort();
                    string stamp = r.ReadString();
                    NetLog.Write("Hello: protocol " + protocol + ", client build " + stamp + ", host build " + BuildStamp);
                    if (protocol != Protocol) { Refuse(NetError.VersionMismatch); return; }
                    Net.SetPeer(true, false);
                    var w = NetOut.Reliable(NetSyncId.Control, OpWelcome);
                    if (w != null) { w.WriteByte(Net.Epoch); NetOut.End(w); }
                    SetStatus(NetStatus.PeerJoined);
                    return;
                }
                case OpWelcome:
                {
                    if (!Net.IsClient) return;
                    Net.SetEpoch(r.ReadByte());
                    Net.SetPeer(true, false);
                    NetLog.Write("Welcome, epoch " + Net.Epoch);
                    SetStatus(NetStatus.Lobby);
                    return;
                }
                case OpLoadScene:
                {
                    if (!Net.IsClient) return;
                    byte e = r.ReadByte();
                    string scene = r.ReadString();
                    int players = r.ReadByte();
                    NetLog.Write("LoadScene epoch " + e + " " + scene);
                    gameStarted = true;
                    ClientLoad(e, PendingLoad.Game, players);
                    return;
                }
                case OpReady:
                {
                    if (!Net.IsHost) return;
                    byte e = r.ReadByte();
                    ushort count = r.ReadUShort();
                    ulong digest = r.ReadULong();
                    if (!hostSweepDone && e == Net.Epoch)
                    {
                        hasPendingReady = true;
                        pendingReadyEpoch = e; pendingReadyCount = count; pendingReadyDigest = digest;
                        return;
                    }
                    ProcessReady(e, count, digest);
                    return;
                }
                case OpSnapshotBegin:
                    if (Net.IsClient) NetLog.Write("SnapshotBegin epoch " + r.ReadByte());
                    return;
                case OpSnapshotEnd:
                {
                    if (!Net.IsClient) return;
                    byte e = r.ReadByte();
                    if (e != Net.Epoch) return;
                    Net.SetPeer(true, true);
                    snapshotDeadline = 0f;
                    NetStats.EndLoadWindow();
                    NetLog.Write("PeerReady (snapshot applied), epoch " + e);
                    SetStatus(NetStatus.Playing);
                    return;
                }
                case OpReload:
                {
                    if (!Net.IsClient) return;
                    byte e = r.ReadByte();
                    NetLog.Write("Reload epoch " + e);
                    EndScene();
                    ClientLoad(e, PendingLoad.Reload, 0);
                    return;
                }
                case OpToMenu:
                    if (!Net.IsClient) return;
                    ClientLost(NetError.HostLeft, "host went to the menu");
                    return;
                case OpLeave:
                    if (Net.IsHost) NetLog.Write("the client is leaving");
                    return;
                case OpReplayRequest:
                {
                    if (!Net.IsHost) return;
                    var gs = GameSession.Current;
                    if (gs != null && gs.CanRestart) ReloadForBoth();
                    return;
                }
                case OpPing:
                {
                    float t = r.ReadFloat();
                    var w = NetOut.Unreliable(NetSyncId.Control, OpPong);
                    if (w != null) { w.WriteFloat(t); NetOut.End(w); }
                    return;
                }
                case OpPong:
                {
                    float sample = NetOut.Now - r.ReadFloat();
                    if (sample < 0f || sample > 5f) return;
                    float rtt = Net.RoundTrip;
                    Net.SetRoundTrip(rtt <= 0f ? sample : Mathf.Lerp(rtt, sample, 0.2f));
                    return;
                }
            }
        }

        void ClientLoad(byte epoch, PendingLoad kind, int players)
        {
            Net.SetPeer(true, false);   // no InputPose until the new snapshot (3.6)
            Net.SetEpoch(epoch);
            snapshotDeadline = 0f;
            NetStats.BeginLoadWindow();
            SetStatus(NetStatus.Loading);
            QueueLoad(kind, players);
        }

        void ProcessReady(byte e, ushort count, ulong digest)
        {
            if (e != Net.Epoch || refusedEpoch == e)
            {
                NetLog.Write("Ready for epoch " + e + " refused (current " + Net.Epoch + ")");
                if (refusedEpoch == e) Refuse(NetError.Timeout);
                return;
            }
            if (Net.PeerReady) return;
            if (count != NetIds.Count || digest != NetIds.Digest)
            {
                NetLog.Write("VERSION MISMATCH: host ids " + NetIds.Count + " " + NetIds.Digest.ToString("X16") +
                             ", client ids " + count + " " + digest.ToString("X16"));
                foreach (var line in NetIds.DumpLines()) NetLog.Write("id " + line);
                Refuse(NetError.VersionMismatch);
                return;
            }

            NetOut.InSnapshot = true;
            try
            {
                var w = NetOut.Reliable(NetSyncId.Control, OpSnapshotBegin);
                if (w != null) { w.WriteByte(e); NetOut.End(w); }
                for (int i = (int)NetSyncId.Spawns; i < (int)NetSyncId.Count; i++)
                {
                    var sync = NetSync.Get((NetSyncId)i);
                    if (sync == null) continue;
                    try { sync.SendSnapshot(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                w = NetOut.Reliable(NetSyncId.Control, OpSnapshotEnd);
                if (w != null) { w.WriteByte(e); NetOut.End(w); }
            }
            finally { NetOut.InSnapshot = false; }

            Net.SetPeer(true, true);
            readyDeadline = 0f;
            NetStats.EndLoadWindow();
            NetLog.Write("PeerReady (snapshot sent), epoch " + e + ", ids " + count + ", tracked bodies " + NetIds.TrackedBodyCount);
            SetStatus(NetStatus.Playing);
        }

        // =====================================================================
        // Frame loop
        // =====================================================================

        void QueueLoad(PendingLoad kind, int players)
        {
            pendingLoad = kind;
            pendingPlayers = players;
            RunPendingLoad();
        }

        // SceneFlow drops a request made while it is loading: hold it and replay it after.
        void RunPendingLoad()
        {
            if (pendingLoad == PendingLoad.None || SceneFlow.IsLoading) return;
            var kind = pendingLoad;
            pendingLoad = PendingLoad.None;
            switch (kind)
            {
                case PendingLoad.Game: SceneFlow.LoadGame(Mathf.Max(2, pendingPlayers)); break;
                case PendingLoad.Reload: SceneFlow.ReloadGame(); break;
                case PendingLoad.Menu: SceneFlow.LoadMenu(); break;
            }
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneFlow.LoadingFinished += OnLoadingFinished;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneFlow.LoadingFinished -= OnLoadingFinished;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void OnLoadingFinished(string scene) { RunPendingLoad(); }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneFlow.GameScene && Net.IsOnline && !leaving)
            {
                var go = new GameObject("_NetScene");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<NetIdSweep>();
                NetTransforms.AddClientComponents(go);
            }
            else if (scene.name == SceneFlow.MenuScene && resetRoleOnMenu)
            {
                resetRoleOnMenu = false;
                Net.SetRole(NetRole.Offline);
                SetStatus(NetStatus.Idle);   // the reason, if any, waits in TakeMenuMessage
                disposing = true;
            }
        }

        void Update()
        {
            if (pendingStart != null)
            {
                if (TransportIdle)
                {
                    var start = pendingStart;
                    pendingStart = null;
                    try { start(this); }
                    catch (Exception e) { Fail(NetError.ConnectFailed, e.ToString()); }
                }
                return;
            }
            if (disposing && !resetRoleOnMenu)
            {
                if (TransportIdle) { disposing = false; instance = null; Destroy(gameObject); }
                return;
            }

            RunPendingLoad();
            float now = Time.unscaledTime;
            if (Net.IsHost)
            {
                if (autoStart && status == NetStatus.PeerJoined && !gameStarted) StartGame();
                if (readyDeadline > 0f && now > readyDeadline && !Net.PeerReady)
                {
                    readyDeadline = 0f;
                    refusedEpoch = Net.Epoch;
                    NetLog.Write("the client was not ready in " + HostReadyTimeout + " s");
                    Refuse(NetError.Timeout);
                }
            }
            else if (Net.IsClient)
            {
                if (snapshotDeadline > 0f && now > snapshotDeadline && !Net.PeerReady)
                {
                    snapshotDeadline = 0f;
                    ClientLost(NetError.Timeout, "no snapshot in " + ClientSnapshotTimeout + " s");
                }
            }
        }

        void LateUpdate()
        {
            if (!Net.IsOnline || leaving) return;
            if (Net.PeerConnected && Time.unscaledTime >= nextPing)
            {
                nextPing = Time.unscaledTime + 1f;
                var w = NetOut.Unreliable(NetSyncId.Control, OpPing);
                if (w != null) { w.WriteFloat(NetOut.Now); NetOut.End(w); }
            }
            NetSync.RunTick();
        }

        sealed class ControlSync : NetSync
        {
            public override NetSyncId Id => NetSyncId.Control;
            public override void Receive(byte op, NetReader r) { if (instance != null) instance.OnControl(op, r); }
        }

        // =====================================================================
        // Statics and the command line
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            status = NetStatus.Idle;
            error = NetError.None;
            errorDetail = joinCode = menuMessage = null;
            StatusChanged = null;
            Notice = null;
            cmdHost = cmdJoin = botScript = dumpIdsPath = null;
            autoStart = false;
            NetSync.Register(new ControlSync());
        }

        // After the SubsystemRegistration resets (NetLog, Net), before the first scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ReadCommandLine()
        {
            string[] args;
            try { args = Environment.GetCommandLineArgs(); }
            catch (Exception) { return; }
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
                switch (a)
                {
                    case "-nethost": cmdHost = next ?? DefaultPort.ToString(); break;
                    case "-netjoin": cmdJoin = next; break;
                    case "-netautostart": autoStart = true; break;
                    case "-netlog": if (next != null) NetLog.SetPath(next); break;
                    case "-netbot": botScript = next ?? "client"; Net.SetScripted(true); break;
                    case "-netdumpids": dumpIdsPath = next; break;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartFromCommandLine()
        {
            if (cmdHost != null)
            {
                if (string.Equals(cmdHost, "relay", StringComparison.OrdinalIgnoreCase)) HostRelay();
                else HostDirect(ushort.TryParse(cmdHost, out ushort port) ? port : DefaultPort);
            }
            else if (!string.IsNullOrEmpty(cmdJoin)) Join(cmdJoin);
        }
    }
}
