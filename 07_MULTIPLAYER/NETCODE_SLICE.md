# NETCODE FOR THE SLICE

_Online co-op for `Map01_PierreKit_House`: two PCs, two home networks, one join code. Decision: [ADR-012](../decisions/ADR-012-online-coop-relay.md). Written 2026-09-27 from the brief and ten subsystem surveys, then revised the same day after three reviews (appendix A, review log). This is the build contract for the parallel tracks: section 12 (frozen contracts) and section 13 (file ownership and workflow) are binding. Pierre's answers are in section 16. Built, merged and tested the same day: what changed from this spec, the test results and how to play are in section 17._

## 1. Goal and scope

**Goal.** Pierre and Jonathan play the slice together from two PCs on different home networks, with no port forwarding, no dedicated server and no backend beyond Unity Relay's free tier.

**In scope.**
- 2 players online: host = P1, client = P2. Each machine renders only its own player, full screen, with its own HUD, audio listener and pause menu.
- Host-authoritative simulation. The client runs only its own body (movement and look) and presentation.
- Connection by Relay join code, or by direct IP (LAN, localhost, VPN, the automated loopback test).
- Menu flow: host, join, lobby of two, start, play, end screen, replay for both, leave.

**Out of scope for the slice (accepted, see section 15).** Join or rejoin mid-run, host migration, more than 2 players online, split screen plus online, voice chat (use Discord), cheating protection, the F11 destruction reset online.

**Hard rule.** OFFLINE (solo and local split screen) behaves exactly as the baseline commit (section 13, step 0). No net object exists offline: no `_Net` GameObject, no NetSession, no NetworkManager, no extra WorldEvents listener. Every gate reads a static bool whose offline value keeps today's code path, and no gate consumes `UnityEngine.Random`.

**Stack (checked in `Library/PackageCache`).** NGO 2.13.3 is used for connection and messaging only. Scene management is off, there is no player prefab and there are no NetworkObjects. Unity Transport comes with NGO. Multiplayer Services 2.3.3 provides Relay, Authentication and Core.

**Cloud link.** The Unity project is linked to a Unity Cloud project (`ProjectSettings.asset` `cloudProjectId`, commit 86faae5). Relay still has to be enabled for that project in the dashboard. The code treats "not linked", "Relay disabled", "payment required" and "services unreachable" as a clean error on the menu, and direct IP never touches the services.

## 2. Roles and the Net facade

| | Offline | Host | Client |
|---|---|---|---|
| `Net.Role` | `Offline` (default, reset at SubsystemRegistration) | `Host` | `Client` |
| `Net.HasAuthority` | true | true | false |
| `Net.LocalMember` | -1 (all local) | 0 | 1 |
| `Net.Drives(P1)` | true | true | false (puppet) |
| `Net.Drives(P2)` | true | false while the client is connected, true after it left | true |
| Simulation | everything | everything, P2 fed by the client | own P2 body only |

**Gate vocabulary.** Only these words are used, and every gate uses the one whose offline value is today's behaviour.
- `Net.HasAuthority`: the simulation entry points (Update, FixedUpdate, collision damage, AI, timers, spawners). It is true offline.
- `Net.IsHost`: send hooks. It is false offline, so nothing is sent and nothing is allocated.
- `Net.IsClient`: client-only replica branches. It is false offline.
- `Net.IsOnline`: flow and UI choices (pause, leave, debug keys). It is false offline.
- `Net.Drives(member)` / `Net.IsLocal(member)`: per-body choices (puppet pose, chase camera, footsteps). Both are true offline.

**Role timing.** `NetSession` sets the role before it calls `SceneFlow.LoadGame(2)`. So every Awake in Map01 already sees it: CrewSpawner at -560 and HouseDestruction at -200.

**Where NGO types live.** Only `Net/NetSession.cs` references `Unity.Netcode`, `Unity.Networking.Transport` and `Unity.Services.*`. Gameplay files and sync files use the Movers-owned `NetWriter` / `NetReader`, which wrap a byte array. `NetOut` (in `Net/NetSync.cs`) hands finished batches to `NetSession.Send(bool reliable, byte[] buf, int len)` and receives through `NetOut.Dispatch(byte[] buf, int len)`, both `internal`, so `NetSync.cs` stays free of NGO types.
- **Why not leak `FastBufferWriter`:** it is a struct with manual lifetime (`using`, `TryBeginWrite`). A bug in one of 7 tracks would corrupt memory. Our own writer is about 150 lines of safe code.
- **Transport swap:** it keeps the transport replaceable by Steam at launch (ADR-004) without touching the tracks.
- **Cost:** one copy per message, which is negligible at our rates.

## 3. Connection

### 3.1 Two ways in
- **Relay (default).**
  - Host: `UnityServices.InitializeAsync()`, then `AuthenticationService.Instance.SignInAnonymouslyAsync()` if not `IsSignedIn`, then `RelayService.Instance.CreateAllocationAsync(1)`, then `GetJoinCodeAsync(allocation.AllocationId)`, then `utp.SetRelayServerData(allocation.ToRelayServerData(RelayProtocol.DTLS))`, then `NetworkManager.StartHost()`.
  - Client: same init and sign-in, then `JoinAllocationAsync(code)`, then `SetRelayServerData(joinAllocation.ToRelayServerData(RelayProtocol.DTLS))`, then `StartClient()`.
  - Namespaces: `Unity.Services.Core`, `Unity.Services.Authentication`, `Unity.Services.Relay` (RelayService, RelayServiceException, RelayExceptionReason), `Unity.Services.Relay.Models` (Allocation, JoinAllocation, AllocationUtils.ToRelayServerData) and `Unity.Services.Multiplayer` (RelayProtocol.DTLS).
- **Direct IP.** Host: `SetConnectionData("127.0.0.1", port, "0.0.0.0")`, then `StartHost()`. Client: `SetConnectionData(ip, port)`, then `StartClient()`. The default port is 7777. It serves the loopback test, LAN and VPN. It needs no Unity services and no account.
- **What the field accepts:** after trimming and upper-casing, 6 characters of A-Z or 0-9 is a Relay code. Anything containing `.` or `:` is `ip[:port]`.

### 3.2 NetworkManager setup (runtime, in NetSession, never in a scene)
- **Creation.** Only `HostRelay`, `HostDirect`, `Join`, and the command line (`-nethost` or `-netjoin`, parsed at BeforeSceneLoad, the root created at AfterSceneLoad, 17.2) create the `_Net` root GameObject (DontDestroyOnLoad) with NetSession, UnityTransport and NetworkManager. Static getters (`Status`, `Error`, `TakeMenuMessage`, ...) never create anything, so the title screen can poll them offline.
- **Components, on the `_Net` root** (NGO refuses a nested NetworkManager):
  ```csharp
  var utp = gameObject.AddComponent<UnityTransport>();
  var nm  = gameObject.AddComponent<NetworkManager>();
  nm.NetworkConfig = new NetworkConfig {
      NetworkTransport = utp, EnableSceneManagement = false, PlayerPrefab = null,
      ConnectionApproval = true, ForceSamePrefabs = false, TickRate = 30 };
  ```
  `NetworkConfig` is null after `AddComponent` in a player build (only the editor Reset hook fills it), and StartHost refuses a null transport. Both roles set identical values, because the config hash is compared on connect.
- **Approval.** Approve `request.ClientNetworkId == NetworkManager.ServerClientId` (the host itself; NGO runs the callback for it) with `CreatePlayerObject = false`. Approve the first remote id. Deny any other with `Approved = false, Reason = "mv:" + (byte)NetError.SessionFull`.
- **UnityTransport settings:** `ConnectTimeoutMS = 1000`, `MaxConnectAttempts = 15` (a 15 s connect timeout), `HeartbeatTimeoutMS = 500`, and `DisconnectTimeoutMS` left at the default 30000. A scene activation frame (HouseDestruction, the synchronous NavMesh bake) stalls the main thread, which pumps the transport; section 14 measures the longest frame.
- **Lifecycle.**
  - Register the named messages `mv.r` and `mv.u` right after `StartHost` or `StartClient` returns true, every session: `CustomMessagingManager` is created on start and dropped at shutdown.
  - Callbacks: `OnClientConnectedCallback` (ignored for `ServerClientId` on the host), `OnClientDisconnectCallback`, `OnTransportFailure`.
  - `Shutdown()` is deferred. Cancel, then Host or Join again, waits for `OnServerStopped` / `OnClientStopped` (or `!nm.ShutdownInProgress && !nm.IsListening`) before starting.
- `Application.runInBackground` is already 1, which the loopback test relies on.

### 3.3 Menu flow
1. Title screen: "Jouer en ligne", placed after "Jouer à deux".
2. Online page: "Héberger", "Rejoindre", "Retour".
3. Host page:
   - status line ("Création de la partie...", then "En attente du joueur 2...", then "Le joueur 2 est là !");
   - the code drawn as key caps, with "Copier le code" (`GUIUtility.systemCopyBuffer`);
   - "Lancer la partie", enabled only when `Net.PeerConnected`;
   - "Annuler".
   Direct host is a dev option shown in development builds only.
4. Join page:
   - a LumaFlow `TextField` (the `State<string>` and the `FocusNode` live in the model);
   - "Coller" (the only path for a pad);
   - "Se connecter" (Enter also submits), "Retour", and a status or error line.
   While the field is focused, MainMenu skips `DropStrayFocus()` and MenuNav, except Esc (which blurs, then goes back).
5. **Handshake.** Client connects, sends `Hello`. The host answers `Welcome(epoch)`, or disconnects the client with a reason (3.4). The client shows "Connecté, l'hôte va lancer".
6. **Start.** The host presses "Lancer". The host writes `LoadScene(epoch)`, sets `Net.Epoch`, and calls `SceneFlow.LoadGame(2)`. The client receives it, sets the same epoch, and calls `SceneFlow.LoadGame(2)`. A command that arrives while `SceneFlow.IsLoading` is queued in NetSession and replayed on `SceneFlow.LoadingFinished`, because `SceneFlow.Load` drops requests while loading. `Core/SceneFlow.cs` is not edited.
7. **In Map01.** The NetIds sweep runs on both machines (section 5). The client sends `Ready(epoch, idCount, idDigest)`. The host compares.
   - If they differ: the host disconnects the client with `VersionMismatch`, both machines write their id dump to the net log, and the client returns to the menu with the message.
   - If they match: `NetOut.InSnapshot` is set; the host writes `SnapshotBegin`, every sync's snapshot, the transform snapshot, then `SnapshotEnd`; `InSnapshot` is cleared and `Net.PeerReady` is set. Host hooks that fire during `SendSnapshot` (the same frame) are also sent, in call order, after the snapshot records. The client sets `Net.PeerReady` when it has applied `SnapshotEnd`.
8. **Holds until ready.**
   - Client: the loading screen stays up while `Net.IsClient && !Net.PeerReady`. After 20 s without `SnapshotEnd`, the client leaves to the menu with `net.failed`.
   - Host: the intro card stays up and cannot be skipped while `Net.IsHost && !Net.PeerReady`. After 30 s the host disconnects the client with `Timeout` and continues as after `OnPeerLeft` (3.5). A later `Ready` for that epoch is refused the same way.

### 3.4 Errors
Each error has a Loc key, shown on the menu status line.

| Situation | NetError | Loc key |
|---|---|---|
| `UnityServices.InitializeAsync` or sign-in throws, no network, or `RelayExceptionReason.RateLimited` | `ServicesUnavailable` | `net.services` |
| Project not linked or Relay disabled: `ServicesInitializationException`, or `RelayExceptionReason.InactiveProject` (15006), `Unauthorized` (15401), `Forbidden` (15403) | `NotLinked` | `net.notLinked` ("Relay n'est pas activé pour ce projet") |
| `RelayExceptionReason.PaymentRequired` (15402): decision 4's stop condition | `PaymentRequired` | `net.payment` ("Relay demande un paiement : jouez en IP directe") |
| `JoinCodeNotFound`, `InvalidRequest`, `InvalidArgument`, `EntityNotFound` | `BadCode` | `net.badCode` |
| Client: `OnClientDisconnectCallback(LocalClientId)` before any `OnClientConnectedCallback`, with an empty reason | `Timeout` | `net.failed` |
| Transport failure after connecting | `ConnectFailed` | `net.failed` |
| Different protocol or id digest | `VersionMismatch` | `net.version` ("Versions différentes : recompilez les deux") |
| A second client | `SessionFull` | `net.full` |
| Host gone in game (empty reason after connecting) | `HostLeft` | `net.hostLeft` |

- **Refusals travel in NGO's disconnect reason, never as a record.** A record written in the same frame would still be in NetOut's buffer when `DisconnectClient` flushes and closes. Host: `nm.DisconnectClient(id, "mv:" + (byte)error)`; approval sets `response.Reason` the same way. Client: in `OnClientDisconnectCallback(LocalClientId)` it parses `nm.DisconnectReason`: `mv:n` maps to that NetError, an empty reason maps as in the table.
- Every exception text goes to the net log (`NetSession.ErrorDetail`), never to the screen. After a failure the menu is fully usable: `MainMenuModel.Leaving` is never left set.

### 3.5 Disconnects
- **Client leaves or drops (host side).** Each sync's `OnPeerLeft` runs:
  - PlayerSync: P2's source becomes `NullInputSource`, P2 releases what it holds, and `Net.Drives(P2)` becomes true, so the host simulates P2 as an idle body with gravity and knockback;
  - TruckSync: if P2 is at the wheel, `VehicleSeat.ForceRelease` puts it out at the door so P1 can drive.
  Then a toast `net.partnerLeft`. The run continues.
- **Host leaves or drops (client side).** `NetSession` keeps `Role = Client` until the menu scene has loaded, so no gated code runs offline logic in Map01. It then calls `SceneFlow.LoadMenu()`, resets the role to Offline, and the title screen shows `net.hostLeft` once (`NetSession.TakeMenuMessage()`).
- **Leave from the pause menu or the end card** (`NetSession.LeaveToMenu()`). On the host it writes `ToMenu` to the client, calls `NetOut.FlushNow()`, then `Shutdown()`. On the client it writes `Leave`, flushes, and disconnects; the host continues alone.

### 3.6 Reload
- **Host funnel.** Every host reload goes through `NetSession.ReloadForBoth()`. It writes `Reload(epoch + 1)`, sets `Net.PeerReady = false` and the new epoch (which closes the current batch first, 4.1), then calls `SceneFlow.ReloadGame()`, which shows the loading screen online. Its callers:
  - `SceneReload.Reload` (E on the end card, F5): online it calls the funnel and returns;
  - CardsView "Rejouer" on the host;
  - a `ReplayRequest` from the client, honoured when `GameSession.Current.CanRestart`.
- **Idempotent.** `ReloadForBoth` and `RequestReplay` are no-ops while `SceneFlow.IsLoading`, or while a reload for the current epoch is pending (from the moment `Reload` is written until the new scene's sweep). The old scene keeps running during the 1.5 s load, so E, F5 and a request can all arrive in that window.
- **Client paths.** A client E on the end card travels by exactly one path: its raw input frame, which the host's `MutedInputReader` reads. The client's `GameSession` returns before its restart branch (11.7), and `SceneReload.Reload` on the client does nothing. Only the client's "Rejouer" button sends `ReplayRequest`.
- **No stale input.** The client sends no InputPose from the moment it receives `Reload` or `LoadScene` until its new scene's `Ready` is answered by the snapshot. The host ignores InputPose for P2 until `PeerReady` for the current epoch.
- **Stale world traffic.** See 4.1: control records are never epoch-filtered, world records are.

## 4. Message catalogue

### 4.1 Wire
- **Two NGO named messages only:**
  - `mv.r`, sent `NetworkDelivery.ReliableSequenced`;
  - `mv.u`, sent `NetworkDelivery.UnreliableSequenced`.
- **Batch payload:** `epoch u8 | hostTime f32 | records...`.
  - `hostTime` is the host's `Time.unscaledTime` since the session started, taken when the batch's FIRST record is written, not when it is flushed. The client's batches carry its own `clientTime` in that slot.
  - The epoch is read when a batch is opened. Any change of `Net.Epoch` closes the current batch first, so a batch never mixes epochs.
- **Record:** `sys u8 | op u8 | len u16 | payload[len]`. Length-prefixed, so an unknown record is skipped.
- **Epoch filter.** Control records (sys 0) are never epoch-filtered; they carry their epoch in the payload where it matters (`Welcome`, `LoadScene`, `Reload`, `Ready`). Every other record is dispatched only when the batch epoch equals the receiver's current epoch.
- **Size.**
  - A record's payload is at most `NetOut.MaxRecord` = 1000 B. `NetOut` logs an error and drops a larger record (never truncates silently); the loopback test fails on that error.
  - `NetOut` appends records to the current batch and starts a new named message when the next record would push it over 1100 B. It never splits a record.
  - NGO's non-fragmented limit is 1296 B (`NonFragmentedMessageMaxSize`), minus NGO's headers (about 32 B), so no message needs NGO fragmentation.
- **Robust records.** `Reliable` / `Unreliable` return a writer positioned in the shared batch. If the previous record was never closed with `End` (an exception in a hook), they roll it back first (truncate to its header, LogError). The flush does the same. One bad hook costs one record, not the batch.
- **Throttle.** The host sends at most 8 `mv.r` messages per frame. The rest waits in NetOut's queue, in order; only the snapshot and a collapse burst ever hit this.
- **Flush.** Once per frame, in LateUpdate at `NetOrder.Tick` (2900). That is before NGO's PostLateUpdate send, so a record leaves in the frame it was written. `NetOut.FlushNow()` flushes immediately (leave, shutdown).
- **Receive.** NGO processes incoming messages in its EarlyUpdate stage. Records are dispatched and applied there, before any `Update`.
- **NetStats** is a counter inside NetOut, not a separate file: bytes handed to `SendNamedMessage` plus 80 B per message (NGO, UTP, DTLS and UDP overhead), per direction, peak and steady; the longest frame between `LoadScene` / `Reload` and `PeerReady`; the RTT from echoed times. Written to the net log every 5 s.

### 4.2 Ordering guarantees we rely on
1. **One reliable pipeline.** UnityTransport maps Reliable, ReliableSequenced and ReliableFragmentedSequenced to one pipeline (`UnityTransport.SelectSendPipeline`), and we use only `mv.r`. So every reliable record of every sync arrives in the exact order the host wrote it. From that:
   - spawn before any reference to the id;
   - a state change before the WorldEvent the same code raises right after;
   - a blast's Breakable and GlassPane transitions (BlastSolver, steps 1-5) before its `ExplosionFx` (the cosmetic block).
   Records written in one frame can still land in different `mv.r` messages and so in different client frames: nothing may assume "same batch".
2. **Explosion before its knockdowns.** On the host, `Explosion.Run` raises `PlayerKnockedDown` (step 5) before `Explosion` (cosmetic block). `WorldEventRelay` holds each `PlayerKnockedDown` and writes it right after the next `Explosion` record, or at Tick if none comes. On the client `Explosion` is re-raised first, so KnockdownTumble and CrewAnimator find it in `GetRecent` within their 0.25 s window, even across two client frames.
3. **No ordering between `mv.u` and `mv.r`.** An unreliable sample for an id the client does not know yet is dropped. The body keeps moving, so the next sample comes, and every stop ends with a reliable `Settle`, which carries its own sample time (6).
4. **Session state before events.** `SessionStateChanged` is not forwarded. The client's `GameSession.ApplyReplica` writes `Session.*` and its own fields, then re-raises it. ToastFeed reads `BrokeIn` and `StartedHow`, and WorldSoundEvents reads `Session.Failure`, in that order.
5. **Input.** InputPose carries the held-bit changes the host has not acknowledged (9.2), so a lost packet loses no edge.

### 4.3 Records
Direction h>c is host to client, c>h is client to host. R is `mv.r`, U is `mv.u`. Sizes are payload bytes.

**Control (sys 0, CORE).** Never epoch-filtered.

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 Hello | c>h | R | protocol u16, build stamp string (`Application.isEditor ? "editor" : Application.buildGUID`) | on connect | ~40 |
| 2 Welcome | h>c | R | epoch u8 | reply | 1 |
| 3 (reserved) | | | refusals use the disconnect reason (3.4) | | |
| 4 LoadScene | h>c | R | epoch u8, scene string, players u8 | Lancer | ~25 |
| 5 Ready | c>h | R | epoch u8, idCount u16, idDigest u64 | after the sweep | 11 |
| 6 SnapshotBegin / 7 SnapshotEnd | h>c | R | epoch u8 | join, reload | 1 |
| 8 Reload | h>c | R | epoch u8 (the new one) | host reload funnel | 1 |
| 9 ToMenu | h>c | R | reason u8 | host leaves | 1 |
| 10 Leave | c>h | R | none | client leaves | 0 |
| 11 ReplayRequest | c>h | R | none | client "Rejouer" | 0 |
| 12 Ping / 13 Pong | both | U | time f32 | RTT for `Net.RoundTrip` and NetStats (added in the build) | 4 |

The build stamps are logged by the host and printed on a refusal line. A stamp mismatch alone is not fatal: the id digest decides.

**Events (sys 1, CORE):** op 1 Event, h>c, R. Payload: type u8, position 12, instigator s8, loudness unit8, magnitude f32, value i32, subject NetRef 5 (28 B). Sent as raised: a few per second, bursts of tens.

**Spawns (sys 2, CORE)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 Spawn | h>c | R | kind u8 (Cigarette, Beer, Grenade), id u32, pos 12, rot 4 | every host creation after the sweep | 21 |
| 2 Despawn | h>c | R | id u32 | the host finds a registered GameObject destroyed (polled at Tick, no hook) | 4 |

The snapshot carries Spawn for every live spawned object, and Despawn (a tombstone) for every scene id missing on the host.

**Items (sys 3, PLAYERS / ItemSync)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 Flags | h>c | R | id u32, holder s8 (member or -1), bits: dragging, inPocket, worn, loaded | host polls every MovableObject each frame, sends on change | 6 |
| 2 Pockets | h>c | R | member u8, 4 x id u32, outOfPocketSlot s8 | on change | 18 |
| 3 Worn | h>c | R | member u8, 6 x id u32 (EquipSlot order) | on change | 25 |
| 4 Cigarette | h>c | R | id, smoking bit, phase unit8 | edges, and 4 Hz while smoking | 6 |
| 5 Beer | h>c | R | id, drinking bit, fill unit8 | edges, and 5 Hz while drinking | 6 |
| 6 SmokePuff | h>c | R | cigarette id, smoker u8, cloud centre 12, dir half3 6, lifetime unit8, strength unit8, mouth 12, wisps u8 | each Puff, ~1.8/s per smoker | 38 |
| 7 BeerSplat | h>c | R | at 12, normal half3 6, size half 2 | Break (the despawn follows) | 20 |
| 8 PocketHint | h>c | R | member u8, code u8 (HandsFull, PocketEmpty), slot u8 | host `Hint()` for P2 | 3 |

**Structure (sys 4, DESTRUCTION / StructureSync).** All ops h>c, R.

| op | payload | when | size |
|---|---|---|---|
| 1 ModuleFractured | module id u32, healthLeft unit8 | `Fracture` right after `IsFractured = true` | 5 |
| 2 ChunkDetached | module id, chunk u8, flags u8 (fell, vanished), v half3, w half3 | both exits of `Detach` | 18 |
| 3 ChunkLook | module id, chunk u8, state u8 | `UpdateChunkLook`, only when `IsFractured` | 6 |
| 4 ModuleState | module id, state u8 | `Refresh` and `CollapseWhole` state change | 5 |
| 5 RoofFell | roof id u32 | `RoofSection.Fall`; the pose then goes on the transform stream | 4 |
| 6 ModuleSnapshot | id, left unit8, state, attached mask u16, looks 2 bits x 16, fixtures mask u8 | snapshot, fractured modules only | 13 |

Bursts: one `CollapseWhole` is 8 to 15 detaches, and at most 90 per frame, about 1.6 KB split over 2 messages.

**Props (sys 5, DESTRUCTION / PropsSync)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 Breakable | h>c | R | id u32, state u8. Destroyed adds point 12, impulse 12, inherit half3 6, instigator s8 | `MarkBroken`; for Destroyed, in `Shatter` after the children loop, before the own-renderer block | 5 or 36 |
| 2 Glass | h>c | R | id, state. Broken adds point 12, impulse 12, instigator | at the transition | 5 or 30 |
| 3 ExplosionFx | h>c | R | pos 12, radius half, power half, instigator s8 | the cosmetic block of `Explosion.Run` | 17 |
| 4 Sound | h>c | U | ImpactAudio kind u8, pos 12, volume unit8 | each host `ImpactAudio.Play` that passes the rate limit, except `Kind.Boom` | 14 |
| 5 Grenade | h>c | R | id, flags (armed, pinOut), fuseLeft u16 ms, armedBy s8 | arm, chain arm, fuse shortened | 8 |

`Boom` is never sent as a Sound: the ExplosionFx handler plays it.

**Doors (sys 6, INTERACTION / DoorSync)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 Panel | h>c | R | panel id u32, open bit | host polls the 57 panels each frame, sends on change | 5 |
| 2 Lock | h>c | R | lock id u32, locked bit | host polls the 5 locks | 5 |

Snapshot: every panel and lock, about 310 B, applied with `Snap()`.

**Players (sys 7, PLAYERS / PlayerSync)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 InputPose | c>h | U | see 9.2 | every client frame, capped at 60 Hz; 10 Hz while nothing changes | ~70 |
| 2 PuppetPose | h>c | U | member u8, pos 12, yaw angle 2, cam local rot 4, height half, bits (crouch, grounded, throwHeld), velocity half3 | 20 Hz, P1, never while P1 is seated | 28 |
| 3 Impulse | h>c | R | member u8, velocityChange 12 | host `AddImpulse` on a body it does not drive | 13 |
| 4 Drunk | h>c | R | member u8, amount unit8 | on change of at least 1/255, at most 5 Hz | 2 |
| 5 InputAck | h>c | U | last applied client frameSeq u16 | 20 Hz while connected | 2 |

**Truck (sys 8, GAMELOOP-TRUCK / TruckSync)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 State | h>c | U | forwardSpeed half, steer half, pedal s8, handbrake bit, velocity half3 | 20 Hz while the body is awake, 1 Hz while it sleeps | 12 |
| 2 SeatEnter | h>c | R | member u8, seatEpoch u8 | host `TryEnter` | 2 |
| 3 SeatExit | h>c | R | member u8, pos 12, yaw angle, seatEpoch | host `TryExit`, `Forget`, `ForceRelease` | 16 |
| 4 SeatNotice | h>c | R | member u8, code u8 | failed exit ("no room") | 2 |
| 5 Ramp | h>c | R | state u8 | on change | 1 |
| 6 Cargo | h>c | R | kg half, volume half, count u8 | on change | 5 |

**Grandma (sys 9, GRANDMA / GrandmaSync).** All ops h>c, R.

| op | payload | when | size |
|---|---|---|---|
| 1 Flags | state u8, bits: KeysGiven, PoliceCalled, AIEnabled, SessionOver, CollisionsOn, SeatedBody | on change | 2 |
| 2 Mood | patience u16 (x100), inLastWarning bit | with each GrandmaMoodChanged, and on warning edges | 3 |
| 3 Activity | spot id u32 (0 = none), phase u8 | on change | 5 |
| 4 Prop | GrandmaProp u8 | on change of `Shown` | 1 |
| 5 Anim | layer u8, state hash i32 | each real `CrossFadeInFixedTime` | 5 |
| 6 Speech | line u8, variant u8, arg string | each `Say` | 3-40 |
| 7 Hush | none | `Hush` | 0 |
| 8 Fire | fire id, lit bit, secondsLeft u16 | Ignite, Extinguish | 7 |
| 9 AnimSnapshot | layer u8, state hash i32, normalizedTime f32 | snapshot, per layer | 9 |
| 10 SpeechSnapshot | line u8, variant u8, arg string, secondsLeft f32 | snapshot, the current line | 7-44 |

The snapshot adds ops 9 and 10 (added in the build): per layer, the state hash and normalized time, and the current line with its seconds left.

**Session (sys 10, GAMELOOP-TRUCK / SessionSync)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 State | h>c | R | state, failure, timeLimit f32, timeLeft f32, flags (hasIntro, cardShowing, brokeIn), breakInBy s8, breakInWhat u8 (enum of 4), startedBy s8, startedHow u8 (enum of 6), policeRemaining f32 (-1 = none) | every transition, including the one-frame ContractStarted; StartContract, HideIntroCard, police | ~22 |
| 2 Clock | h>c | U | timeLeft f32 | 2 Hz | 4 |
| 3 SettlementLine | h>c | R | label string, detail string, amount i32 (the host's English strings, verbatim: SettlementText parses them) | once per line at the end, before SettlementEnd | <=406 |
| 4 LedgerPut | h>c | R | item id u32, thief s8, route u8, value i32, witnessed bit | an entry added or changed (host diffs on `Ledger.Version`, at most 4 Hz) | 11 |
| 5 SettlementEnd | h>c | R | completed bit, failure u8, total i32 | after the lines, before the end State | 6 |
| 6 LedgerDrop | h>c | R | item id u32 | an entry removed | 4 |
| 7 LedgerFinal | h>c | R | none | `FinalizeAll` | 0 |

`Settlement` detail strings are at most 5 names plus "and N more" (`Settlement.Names`), so a line fits one record. The ledger has one entry per loot item (up to about 120), so it is sent as deltas, never as one record. The snapshot sends one LedgerPut per entry.

**Audio (sys 11, UI-AUDIO / AudioSync):** op 1 PropImpact, h>c, U. Payload: SfxKind u8, point 12, volume unit8, pitch unit8 (15 B). Sent on each host `PropImpactSound` play, with a cooldown of 0.12 s per item.

**Transforms (sys 12, CORE / NetTransforms)**

| op | dir | del | payload | when | size |
|---|---|---|---|---|---|
| 1 Batch | h>c | U | n entries: id u32, flags u8 (teleport, anchored, asleep), then world: pos 12, rot 4; or anchored: member u8, camera-space pos half3 6, yaw-relative rot 4 | 20 Hz, moved bodies only, at most 45 entries per record | 21 or 16 per entry |
| 2 Settle | h>c | R | sampleTime f32, then one world entry | once when a body stops; the snapshot has every tracked body | 25 |

### 4.4 Budgets (planning, measured by NetStats)
Figures include the 80 B per message overhead that NetStats adds.
- **Down (host to client):**
  - steady state (the grandmother walking, 2 or 3 things carried, P1 walking): about 5 KB/s;
  - a grenade in a furnished room: 30 moving bodies at 20 Hz, about 15 KB/s, plus a reliable burst of 2 to 5 KB;
  - worst case (a floor collapsing, about 125 bodies moving): about 60 KB/s for a few seconds. Threshold: 80 KB/s. If it is exceeded, bodies farther than 15 m from the client's camera drop to 10 Hz.
- **Up (client to host):** about 9 KB/s while moving (InputPose, ~70 B at 60 Hz plus overhead), about 1.5 KB/s standing still.
- **Snapshot on join:** about 126 tracked bodies x 25 B = 3.2 KB; flags about 1 KB; doors 0.3 KB; breakables and glass (non-intact only) up to 2 KB; the ledger up to 1.3 KB; the rest under 1 KB. In total under 12 KB, about 12 messages over 2 frames.
- Relay's free tier limits are far above 2 players at these rates. Pierre still checks the dashboard (section 16).

## 5. Stable ids

### 5.1 Scheme
- **One id per GameObject, plus a kind byte on the wire.** A single GameObject carries several subjects (a movable is MovableObject + Breakable + Rigidbody; a door leaf is HingedPanel + Breakable + DoorLock), so a reference is `NetRef(id, kind)`.
- **Hash.** FNV-1a 64 over the post-setup hierarchy, built incrementally: `h(child) = FNV(h(parent), UTF-8 name, ordinal)`, seeded with the scene name. `ordinal` is the number of EARLIER siblings with the same name.
  - Not the raw sibling index: setup appends children (`GlassPane`, `_Hinge`, `Chunks`, `Foundation`) and moves leaves out of their walls, which shifts raw indices.
  - The ordinal is required. At Play about 215 GameObjects share a name with a sibling: 46 fences, porch and terrace posts, 128 `GlassPane`, 6 `Grenade`. About 190 of them hold state.
- **Wire id.** `(uint)(h ^ (h >> 32)) & 0x7FFFFFFF`. 0 means none, and the top bit is reserved for spawns.
- **What is registered.** Only GameObjects carrying a whitelisted type: MovableObject, Body (below), Breakable, GlassPane, DestructibleModule, RoofSection, HingedPanel, HingedGroup, DoorLock, HeldUsable, GrandmaBrain, ActivitySpot, FireplaceFire, TruckVehicle, VehicleSeat, TruckCargo, TruckRamp, DeliverPoint, GameSession, HouseDestruction. About 700 at Play by the survey; the build registers 541.
- **Body.** A Rigidbody that is non-kinematic at sweep time, has no MovableObject, and is not owned by HingedPanel, TruckRamp or RoofSection. In Map01 that is exactly `MovingTruck` and `HouseContents/Vehicles/Grandma_Car`. The 57 `_Hinge` pivots and the ramp are kinematic and carry registered components anyway (their panel or ramp has the id); a roof has no Rigidbody until it falls.
- **What the sweep skips:**
  - GameObjects with `hideFlags != None`, and the DontDestroyOnLoad scene;
  - the subtree of every CrewMember, except its `Pockets` child (the 4 starting pocket items);
  - everything without a whitelisted component, which includes the HUD, audio, debris, FX roots, `GrenadeFuses`, `MirrorCamera_*` and environment chunks.
- **Players.** Players are never path ids. A CrewMember is `NetRef(index, Crew)`, the same on both machines (0 = P1, 1 = P2).

### 5.2 When
- The sweep runs in `NetIdSweep.Start` (a MonoBehaviour in `Net/NetIds.cs`), `[DefaultExecutionOrder(10000)]`, in the first frame of the game scene. `NetSession` adds it at `sceneLoaded` on a runtime GameObject in the game scene. A new root is harmless because ordinals are per name.
- By then every structural setup has run, on both machines, unchanged:
  - CrewSpawner (-560, clones `Player_P2`);
  - HouseDestruction (-200: glass split, `Foundation`, modules, Breakables);
  - the HingedPanel Awakes (the `_Hinge` pivots);
  - the order-0 Starts (GrenadeCrate, StartingItemSpawner, PlayerPockets);
  - HouseInteractionSetup (50: sashes, groups, locks);
  - GrandmaMover (60).
- Ids are cached both ways (id to GameObject, GameObject to id) and never recomputed from a live path. Pockets, wearing, debris, seats, roof falls and resets all move objects afterwards.
- Right after the sweep, `NetIdSweep` runs the tracked-body kinematic pass on the client (6) and calls every sync's `OnSceneReady`, where syncs subscribe to what they need (section 12 rules).
- Offline the sweep does not exist.

### 5.3 Setup must not be gated
HouseDestruction.Awake, HingedPanel.Awake and Seat, HouseInteractionSetup.Start, GrenadeCrate.Start, StartingItemSpawner.Start, PlayerPockets.Awake and Start, CrewSpawner.Awake, CountryLand and CountryScatter all run on the client. The client needs the same objects and the same colliders. Only the later simulation (respawns, collapse queue, AI) is host-only.

### 5.4 Spawned ids
- **Host ids.** Anything created after the sweep gets `NetIds.NewSpawnId()` = `SpawnBit | counter`. The counter resets per epoch. Today that is the van respawns, the F9 grenade and thrown litter.
- **Announcing.** The host announces the object with `NetSpawns.Announce(go, kind)`. The client builds it with the same factory (`CigaretteItem.Create`, `BeerItem.Create`, `GrenadeItem.Create`), then registers it.
- **Despawns need no hook:** at every Tick the host checks its registered GameObjects for Unity-null and sends `Despawn`.
- **Wall chunks** are never ids of their own: they are `(module id, DestructibleChunk.Index)`, valid once the client has run `NetFracture` for that module.

### 5.5 Collision check, digest and dump
- **Collisions.** At sweep time the insert into `Dictionary<uint, GameObject>` detects a true 31-bit collision (probability about 1e-4 for 700 entries). On a clash: LogError with both paths, and a salted rehash of the later one. The clash is deterministic because both machines visit in the same order.
- **Digest.** FNV-64 over the entries in id order: `(id, NetKind of the first whitelisted component, sweep-time world position rounded to 1 cm)`, plus the count, sent in `Ready`. The position is hashed for static entries only: tracked bodies and the grandmother settle during the load frames by an amount that depends on frame timing (fix 5685ba3, 17.2), and the snapshot sets them anyway. The position term catches two same-named siblings swapped between machines (same ids, different objects). It also catches:
  - a scene edited between the host build and the client build;
  - a missing catalog asset, or different static batching or mesh readability, which change the pane and module sets.
- **Dump.** On a mismatch, and always with `-netdumpids <path>`, each machine writes its sorted `id kind x y z path` list. The mismatching lines go to the net log.
- **Body count check.** The sweep logs the tracked-body count (about 126: 112 scene movables, 12 start-time items, the truck, Grandma_Car). The loopback test prints it.

## 6. Transform stream

- **Who registers.**
  - The sweep tracks every MovableObject root and every Body (5.1: MovingTruck, Grandma_Car). Nothing else from the sweep is tracked or made kinematic.
  - GrandmaSync tracks the grandmother root in `OnSceneReady`.
  - StructureSync tracks a roof on `RoofFell`, on both machines.
  - `NetSpawns` tracks spawned objects.
- **Not streamed:** door and window pivots (open state plus a local swing), the ramp (state plus a local animation), wall chunks (local debris from `ChunkDetached`), players (their own pose channel, section 9).
- **Host send rule (20 Hz, at Tick).**
  - Skip a body that is inactive, or whose MovableObject is `inPocket` or `worn`.
  - Send when it moved more than 5 mm or turned more than 0.5 degrees since its last sent sample, or 1 s has passed while its rigidbody is awake.
  - When a body that was sending has not changed for 0.25 s, send one reliable `Settle` with its `sampleTime`.
  - The teleport flag is set after `NetTransforms.Snap(go)`, or when a jump between samples exceeds 3 m.
  - **Snap calls (host):** `KeyItem.Place`, `PlayerPockets.TakeOut` and `Stow`, `CrewEquip.Equip` and `Unequip`, `TruckVehicle.PlaceAt`. Each is one line, a no-op offline.
- **Held by the client's player (anchored).** When `mo.holder` belongs to a member that the host does not drive (P2), and `!holder.IsDragging`, the host sends an anchored entry:
  - `member`;
  - camera-space position `p2Cam.InverseTransformPoint(rb.position)`;
  - yaw-relative rotation `Inverse(p2Root.rotation) * rb.rotation`. The carry holds orientation in the body's yaw frame, never in pitch.
  A dragged object is always world-interpolated, never camera-relative.
- **Client apply, world (`NetOrder.WorldApply` = -530, Update).**
  - Per id: a buffer of `(hostTime, pos, rot)`, rendered at `estimatedHostNow - 0.1 s`, with lerp and slerp between the bracketing samples. Samples are inserted in hostTime order; a sample older than the newest one applied is dropped (for example a late U sample after a Settle). A `Settle` older than the newest buffered sample for its id is dropped.
  - At most 0.1 s of extrapolation past the newest sample, then hold. A teleport jumps.
  - **Skip and clear** the buffer of any id whose replicated MovableObject is `inPocket` or `worn`, or whose newest entry is anchored. On every mode change (pocket in or out, wear or unwear, anchored to world), the buffer is cleared and the first new sample is a teleport: the buffer is seeded with the current pose stamped at that sample's hostTime.
  - Writes `transform.SetPositionAndRotation`, then one `Physics.SyncTransforms()` after the pass. This runs before CrewInput (-500), every ray and HeldPose.
  - `estimatedHostNow = localTime - offset`, where `offset` is the minimum of `(receiveTime - hostTime)` over the last 2 s, smoothed.
- **Client apply, anchored (`NetOrder.AnchoredApply` = 5, Update).**
  - The camera-space offset and yaw-relative rotation are interpolated between the two anchored samples bracketing `estimatedHostNow - 0.05 s`, then applied to the CURRENT camera: `pos = localP2.View.TransformPoint(p)`, `rot = localP2.transform.rotation * r`. This adds no view lag and removes the sawtooth of a 20 Hz offset.
  - This runs after PlayerController (0) has moved the client's camera this frame, and before HeldPose (20) displaces the item's child renderers.
  - On leaving (throw, drop, drag), blend 0.15 s from the last anchored pose into the world stream.
- **Own-item collisions (client).** For an item the local member carries (anchored) or drags (Items Flags `dragging && holder == LocalMember`): `Physics.IgnoreCollision(local P2 CharacterController, each item collider, true)`, re-applied every frame while wanted (pairs are lost whenever a collider is toggled). Collision is restored only once the item's bounds no longer overlap the local capsule, or at most 0.5 s after the release.
- **Kinematic handling on the client.** At the sweep, and at each spawn, every TRACKED rigidbody gets, in this order:
  1. `collisionDetectionMode = ContinuousSpeculative`, to avoid the continuous-on-kinematic warning;
  2. `isKinematic = true`;
  3. `interpolation = None`.
  Pivots, the ramp and fallen roofs are never in this pass: they keep their own kinematic setup (the pivots their interpolation). Colliders stay ON: the client's P2 walks on crates, the ramp and the truck bed, and its rays hit items.
- **Keeping them kinematic.** Nothing on the client may set a tracked body back to dynamic. The gates in section 11 guarantee it: PlayerGrab, TruckCargo riding, the pocket and equip apply paths, the DebrisManager thaw (only its own pieces), and RoofSection (a kinematic body with no DebrisPiece).
- **Restoring on disconnect** is not needed: the client leaves to the menu and the scene unloads.

## 7. State stream

- **The per-system interface is `NetSync`** (section 12). Each track writes one or two sync classes under `Net/Sync/`. Gameplay files never write bytes: they call a static method of their track's sync (for example `StructureSync.ModuleFractured(this, left)`), and the sync writes the record. Polled systems (doors) need no call at all.
- **Dirty detection.** Two patterns, chosen per system.
  - Polling in `HostTick`, for small sets with cheap reads: doors (57 plus 5), MovableObject flags (124), pockets, worn, grandma prop and fire, ledger version, truck, drunk. It needs no hook in the gameplay file and cannot miss a change.
  - A hook at the transition, where the change carries data that polling would lose, or where the intermediate state matters: Breakable and Glass transitions (point and impulse), chunk detaches (host velocities), grandma cross-fades and speech (repeat triggers), session transitions (the one-frame ContractStarted), seat enter and exit, FX.
- **Apply rule on the client.** Replicated state is written into the SAME fields the presentation reads, through a replica method that has no side effects: no events, no sounds except the ones listed, no physics writes, no Random. Examples: `HingedPanel.Command`, not `SetOpen`; `DoorLock.locked`, not `Unlock()`; `NetFracture`, not `RemoveChunk`; `SetReplicaHeld`, not `Hold`. Components stay enabled: readers test `isActiveAndEnabled`.
- **Full snapshot on join and after each reload.**
  - The host calls `SendSnapshot()` on every sync in `NetSyncId` order: Spawns, Items, Structure, Props, Doors, Players, Truck, Grandma, Session, Audio, then Transforms (every tracked body as a Settle). `NetOut.InSnapshot` lets these records through before `PeerReady` (3.3).
  - Snapshot apply is silent: no debris, no sounds, no swing. Doors snap, already-broken objects are simply inactive, and fractured modules hide detached chunks.
- **Digest.** `NetSync.Digest` is optional, for debugging a desync by hand. It is not part of the handover or of the automated test.

## 8. Events and FX forwarding

- **Forwarded.** Every `WorldEventType` raised on the host is forwarded, except `SessionStateChanged`. The hook is one CORE line in `WorldEvents.Raise`, after the depth guard and before the listener loop: `if (Net.IsOnline) WorldEventRelay.OnRaised(e);`. So forward order equals raise order, including nested raises, and the relay never subscribes as a listener. The client re-raises through `WorldEvents.Raise` with `WorldEventRelay.Replaying = true`. `WorldEvent.time` is re-stamped locally by the constructor, which keeps the 0.25 s knockdown direction window valid. The relay holds `PlayerKnockedDown` until the next `Explosion` (4.2).
- **Subject mapping.** `NetIds.RefOf(subject)`:
  - CrewMember maps to `(index, Crew)`, for PlayerKnockedDown;
  - MovableObject, Breakable, GlassPane, DestructibleModule, RoofSection, HingedPanel, HingedGroup, DoorLock, HeldUsable (the cigarette or beer of PlayerSmoking and PlayerDrinking), GameSession and HouseDestruction map to `(goId, kind)`.
  An unresolved or destroyed subject gives `null` on the client, which every listener already tolerates. A deactivated subject still resolves: Breakable and Glass deactivate, they do not destroy.
- **Extra FX that WorldEvents do not carry:**
  - Props: `ExplosionFx`, `Sound` (every ImpactAudio sound except Boom: destruction, glass, door slam and rattle, lock latch, grenade pin and beep, roof, truck crash);
  - Items: `SmokePuff`, `BeerSplat`, `PocketHint`;
  - Audio: `PropImpact`;
  - Grandma: `Anim`, `Speech`, `Hush`;
  - Players: `Impulse`;
  - Truck: `SeatNotice`.
  The door rattle has no FX of its own: the replayed `DoorLockedRattle` makes DoorSync call `Jiggle` on the panel (or on each `CanSwing && IsLocked` panel of a group subject). DoorSync subscribes that listener in `OnSceneReady`.
- **Nothing plays twice.**
  1. The client's simulation never raises: every raising entry point is gated (section 11).
  2. Replica paths never call the raising methods (section 7).
  3. Safety nets: every `DestructionEvents` method returns early when `!Net.HasAuthority`, and `ImpactAudio.RaiseNoise` does too.
  4. On the client, `ImpactAudio.Play` ignores local calls, and only `ImpactAudio.PlayFromNet` plays. The replayed `Shatter`, `Crack`, `Jiggle`, the local door shut click and the grenade beep are therefore silent locally, and the host's sound arrives once. Boom plays once, from the ExplosionFx handler.
  5. On the client, `WorldEventRelay.OnRaised` logs a warning for every event raised while `!Replaying`, except `SessionStateChanged`. The test requires zero such warnings.
- **LoudNoise** is raised only on the host (the grandmother's hearing), and forwarded like any event (the F7 log).

## 9. Players

### 9.1 Input sources and views
| | Host | Client |
|---|---|---|
| P1 | LocalDevicesSource (keyboard, mouse and first pad), drives, camera shown (SoloFirst) | NullInputSource, puppet, camera disabled |
| P2 | RemoteInputSource, puppet posed by the client, camera disabled but its transform mirrors the client's | LocalDevicesSource (keyboard, mouse and first pad), drives, camera shown (SoloSecond), tagged MainCamera, holds the only AudioListener |

- **CrewSpawner online:** no `MakePad` (no second local player from a pad), role-specific sources, and `SplitScreen.SetLayout` (existing API) per role. F1, Shift+F1 and F2 stay registered; SliceDebug refuses them online (section 10).
- **The local source online is `LocalDevicesSource`** (Pierre's decision 16.5, replacing "the plain KeyboardMouseSource"). It wraps the keyboard and mouse and the first pad (looked for every 2 s): held bits OR-ed, look, scroll and roll summed, the stronger move wins. `Source is KeyboardMouseSource` checks go through `LocalDevicesSource.IsPad(src)` / `IsKeyboard(src)` or `HudPauseMenu.HasKeyboard()` (17.2). The sender reads the new `CrewInput.RawFrame`, not a wrapper.
- **RemoteInputSource never reports `Pause`.** So the client's Esc can never open or close the HOST's menu, including through MutedInputReader on the end card. Its `Label` is "Remote". `IsGamepad` stays false: each machine formats its own prompts with its own devices.

### 9.2 InputPose (client to host, unreliable)
- **Rate.** Every client frame, capped at 60 Hz. When nothing changed (same held bits, zero deltas, pose within 1 mm and 0.1 degree), 10 Hz.
- **`seq`** counts client FRAMES, not packets.
- **Payload:** `seq u16 | clientTime f32 | held u16 (now) | n u8 (<=16) | n x (frameSeq u16, dtMs u16 since the previous change, held u16) | move 2 x s8 | rotateLookTotal 2 x f32 | scrollTotal f32 | rollTotal f32 | flags u8 (paused, seated) | seatEpoch u8 | chaseYaw angle 2 | pose`.
- **Pose:** `pos 12 | yaw angle 2 | cam local rotation 4 | cc height half | bits (crouching, grounded, throwHeld) | velocity half3`.
- **Edges survive loss.** The change list holds every held-bit change after the host's last `InputAck`, at most the newest 16. The host dedupes by frameSeq.
- **Hold time survives loss.** `RemoteInputSource` replays changes on a host timeline that keeps their client spacing: it releases a queued change only once the host time since the previous applied change is at least its `dtMs`, with at most 0.5 s of backlog (older changes are then applied one per host poll). `Poll` consumes one change at most per call, so every Down and every Up gets its own host poll. A 0.3 s RMB hold stays a hold on the host (`PlayerGrab` tap vs hold at 0.18 s), even after a burst of loss.
- **Playout.** The host plays pose and changes together, on the client's clock: at host time `t` it applies the newest pose and changes with `clientTime <= t - offset - 0.035 s` (offset estimated like 6), interpolating the pose between the two bracketing packets and extrapolating with the sent velocity for at most 0.1 s when a packet is late. So the press edge and the camera it was aimed with arrive in the same host frame, and P2's camera does not step at the packet rate.
- **Deltas** are running totals since the epoch started. The host uses the difference from the last applied total, so a lost packet loses no motion, and nothing is applied twice.
  - The client adds its look delta to `rotateLookTotal` only in frames where its own ReplicaUpdate `lookLocked` is true, and the wheel to `rollTotal` when Rotate is held that frame, to `scrollTotal` otherwise. The host feeds `LookDelta` from `rotateLookTotal` alone, `RollDelta` from `rollTotal`, `Scroll` from `scrollTotal`. P2's own look is client-authoritative and never needs the raw look on the host.
- **When the client pauses** (`HudPauseMenu.OpenCount > 0` on the client, flag `paused`): the host mutes P2's CrewInput as offline, using HudPauseMenu's `mutedByUs` pattern (mute only if not already muted, unmute only what it muted). RemoteInputSource keeps reporting the last unpaused held bits, with no move and no deltas. On unpause it calls `CrewInput.ResyncHeld()` (the `ignoreNextHeld` path of `SetSource`), so no edge fires in either direction. Holding RMB with a live grenade while opening the menu throws nothing.
- **Session freeze.** The frame sent is the raw frame (unmuted), so the host's MutedInputReader sees the client's E and Space for the intro card and the restart. The session mute itself is mirrored on the client by `GameSession.ApplyReplica`, through `SetCrewFrozen`.
- **Sampling time.** Pose and input are sampled in LateUpdate at `NetOrder.PlayerDriver` (-520 is its Update order; its LateUpdate runs before CameraShake at 1000 and ViewOffset at 1010), so the render-only offsets are never sent. They are sent at Tick.
- **Not sent** from the receipt of `Reload` or `LoadScene` until the new snapshot (3.6).

### 9.3 P2 on the host
- `NetPlayerDriver.Update` at -520 applies the played-out P2 pose (9.2) through `PlayerController.ApplyNetPose(in NetPose)`:
  - `transform.SetPositionAndRotation` only. It NEVER changes `cc.enabled`: a puppet never calls `Move`, and toggling the capsule every frame would rebuild its shape, lose ignore pairs and re-fire collision enters on everything touching it;
  - `cam.localRotation` (full rotation, including drunk sway), the private `pitch`, cc height and center, cam local Y, `crouching`;
  - the stored velocity, grounded and throwHeld values, for the `Velocity`, `Grounded` and `NetThrowHeld` getters.
  After applying the poses of the frame, NetPlayerDriver calls `Physics.SyncTransforms()` once, so the capsule and every ray see the new pose.
- It skips any member with `IsDriving`, on BOTH machines, and a pose whose `seatEpoch` is older than `TruckSync.SeatEpoch`.
- This comes before CrewInput (-500), PlayerInteract (-100) and PlayerGrab (0). So the grab ray, interact ray, hold point, throw direction, grenade aim, pocket take-out point and cigarette mouth all come from the camera the client pressed with.
- `PlayerController.Update` returns at its top for a member the machine does not drive. The component stays ENABLED: Explosion, CrewAnimator and VehicleSeat test `isActiveAndEnabled`.
- The capsule stays enabled for the grandmother's sight and touch, explosions, CrewBumper and door-closing safety. The P2 camera component stays disabled; only its transform is used.

### 9.4 P1 on the client
The host sends PuppetPose at 20 Hz, and none while P1 is seated (the seat records place it). `NetPlayerDriver` applies it, interpolated 0.1 s like the transform stream, through the same `ApplyNetPose`. P1's PlayerGrab, PlayerInteract, pockets and equip do nothing on the client: they are gated, and the source is Null. `HeldPose.WatchGrenade` reads `pressing` from `controller.NetThrowHeld` for a member that is neither driven nor local here, so P1's grenade wind-up shows.

### 9.5 Grab and interact replicas (client's own P2)
- **PlayerGrab.**
  - Update keeps the `lookedAt` ray, for the Drag prompt. When `!Net.HasAuthority` it then runs `ReplicaUpdate`:
    - `lookLocked = held && !dragging && holdOrientation && input.Held(Rotate)`;
    - the carry speed and jump multipliers from the replicated held weight and dragging, through one static helper shared with the host path, so the client never walks at full speed with a fridge.
  - FixedUpdate returns before `DropIfGone` on the client.
  - `SetReplicaHeld(mo, dragging)` and `ClearReplica()` set `held`, `dragging`, `heldUsable` and `mo.holder` only. OnDisable on the client calls `ClearReplica`.
- **PlayerInteract.** It keeps `target = FindTarget()` for the prompt, then returns when `!Net.HasAuthority`. The client's P1 puppet skips it entirely, including the ray.

### 9.6 What the host does to P2's body
- **Knockback.** Inside `PlayerController.AddImpulse`: `if (Net.IsHost && !Net.Drives(member)) { PlayerSync.SendImpulse(member.index, v); return; }`. That covers Explosion and CrewBumper. The client calls `AddImpulse` on its own P2.
- **Truck shoves (CrewBumper, host).** For a member not driven here:
  1. it reads `controller.Velocity` (the replicated value) instead of `capsule.velocity`, which is always zero on the puppet;
  2. after a forwarded shove it skips that member for `Net.RoundTrip + 0.2 s`, so shoves do not stack up to 16 m/s while the client's new pose travels back;
  3. for that same window, `Physics.IgnoreCollision` between the truck hull colliders and the remote P2 capsule, then restored, so the truck does not stop dead against the stale puppet.
- **Knockdown.** The forwarded `Explosion`, then `PlayerKnockedDown` (subject = CrewMember), drive KnockdownTumble, CrewAnimator and CrewSounds on the client.
- **Seat.** Truck `SeatEnter` / `SeatExit`, applied on both machines by `VehicleSeat.ApplyEnter` / `ApplyExit`. That covers parenting to `seatPoint`, `IsDriving`, and switching the controller, grab, interact, pockets, equip and colliders off and on. The chase camera runs only where `Net.IsLocal(driver)`. On the client the seated P2 stops streaming its pose (`seated` flag) but keeps sending input and `chaseYaw`; the host's `Release` / `ForceRelease` uses that `chaseYaw` for a remote driver's exit facing.
- **Drunk.** The host's P2 Drunkenness is authoritative: `Add` from beer and concussion, decay, and `carrySlop` for the host's carry. The amount goes to the client (`Drunk`), where `Drunkenness.NetSetAmount` drives the sway and drift of the client-authoritative body. On the host the puppet P2 never applies `lookSway`, because its Update returns.
- **Blind (smoke).** SmokeVision reads local SmokeClouds, spawned by `SmokePuff` on each machine. Nothing else is needed.
- **Grandmother confiscation** (`Grab.Release` on P2 at the host) reaches the client as Items `Flags` (holder -1).

## 10. Session, flow, UI rules online
- **Pause.** `HudPauseMenu.SyncWorldPause` is a no-op online: no `Time.timeScale = 0` and no `AudioListener.pause`. The world keeps running on both machines (section 16, decision 1).
- **Intro card.** The host keeps it up until `Net.PeerReady`. After that, either player's E or Space skips it for both: the client's arrives through its raw input frame into the host's MutedInputReader.
- **End card.**
  - Either player's E after 3 s replays for both, through the reload funnel (3.6).
  - The client's "Rejouer" sends `ReplayRequest`, which the host honours when `CanRestart`.
  - "Menu" / Esc: on the host it ends the session for both; on the client it leaves alone.
- **Debug keys online.** One policy, in SliceDebug only, dispatched by key; `DebugCommands.Register` and every registration stay unchanged:
  - on the client, only F12, F7, F3, Shift+F3, F4 (read-only overlay), Shift+F2 and Shift+F8 are dispatched;
  - on the host online, F1, Shift+F1, F2 and F11 are refused, with the toast "Désactivé en ligne".
  F9, Shift+F9, F10, F6, F8 and Shift+F4 stay on the host; their effects replicate.

## 11. Per-subsystem integration

**Columns:** file (under `UnityProject/Assets/_Movers/Scripts/`), hook, gate, state (sync and op), events and FX. "none" in the first columns means the file is untouched and works from replicated state.

### 11.1 Player core (track PLAYERS, except where noted)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| Player/PlayerController.cs | Early return at the top of Update for a member not driven. `ApplyNetPose(in NetPose)` (9.3: transform only, never `cc.enabled`). `Velocity`, `Grounded` and `NetThrowHeld` return replicated values for puppets. `AddImpulse` forwards for a remote body. | `Net.Drives(member)` | Players InputPose (c>h), PuppetPose | Players Impulse |
| Player/PlayerGrab.cs | `ReplicaUpdate` after the `lookedAt` line. FixedUpdate gate before `DropIfGone`. Guards in `Hold` and `Release`. `SetReplicaHeld`, `ClearReplica`. OnDisable calls `ClearReplica` on the client. Static carry multipliers helper. | HasAuthority | Items Flags (holder, dragging) | ObjectPickedUp, Thrown, Released, FurnitureMoved forwarded |
| Player/HeldPose.cs | `WatchGrenade`: `pressing` from `controller.NetThrowHeld` for a member neither driven nor local | IsClient, IsLocal | PuppetPose throwHeld | none |
| Player/CursorLock.cs | No locking while `Net.Scripted` (the `-netbot` client) | none | none | none |
| Interaction/PlayerInteract.cs | Return after `FindTarget` when `!HasAuthority`. Skip the ray for a member that is neither driven nor local. | HasAuthority | none | host-raised |
| Player/CrewSpawner.cs | Role-specific `AssignDefaultSources`, RemoteInputSource for host P2, no pad hot-plug online, `SplitScreen.SetLayout` per role, MainCamera tag moved to the client's P2 | IsOnline, IsHost, IsClient | none | none |
| Core/CrewInput.cs (CORE) | `RawFrame` accessor, `ResyncHeld()` | none | InputPose source | none |
| Physics/MovableObject.cs (CORE) | `OnCollisionEnter` gate (legacy fragile greying) | HasAuthority | broken bit via Props | none |
| Net/RemoteInputSource.cs, Net/NetPlayerDriver.cs (new) | see 9.1 to 9.4 | | | |
| SplitScreen, KnockdownTumble, ViewOffset, CrewHUD, ButtonLabels, CrewView, ScriptedInputSource, GamepadSource | none | | read replicated | forwarded PlayerKnockedDown, Explosion |

### 11.2 Player presentation and items (track PLAYERS)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| Items/PlayerPockets.cs | Start runs on both (deterministic ids). Update returns after the `pocketRoot` line on the client. `NetApply(ids, outSlot)` records and replays playing particle systems, then SetActive, SetParent and `inPocket`. `NetHint(code, slot)` formats with the local input. Host `Hint` for P2 goes to ItemSync. `NetTransforms.Snap` in `Stow` and `TakeOut`. | HasAuthority | Items Pockets, PocketHint | ItemPocketed, ItemUnpocketed forwarded |
| Items/PlayerEquip.cs | Update top gate | HasAuthority | none | none |
| Items/CrewEquip.cs | `PlayerPockets.LeaveTruck(mo)` in Equip gated. The client applies worn state with the existing `Equip` / `Unequip`. `NetTransforms.Snap` in `Equip` and `Unequip`. | HasAuthority | Items Worn | none (there is no wear event) |
| Items/CigaretteItem.cs | Client branch in Update (local phase advance, glow). `Puff` split into the gameplay part and `PlayPuffFx(...)`, keeping today's order: scatter, Spawn, Emit, Raise. `NetApply(smoking, phase)`. Host Puff goes to ItemSync SmokePuff. | HasAuthority, IsHost | Items Cigarette, SmokePuff | PlayerSmoking forwarded |
| Items/BeerItem.cs | `OnCollisionEnter` gate. `Break` sends BeerSplat (the despawn is automatic). Static `SplatFx(at, normal, size)`. `NetApply(drinking, fill)`. | HasAuthority, IsHost | Items Beer, BeerSplat; Spawns Despawn | PlayerDrinking forwarded |
| Items/StartingItemSpawner.cs | Update gate. `Spawn()` after the sweep calls `NetSpawns.Announce`. | HasAuthority | Spawns Spawn | none |
| Effects/Drunkenness.cs | Decay gated. `NetSetAmount(a)`, which adds the component if missing. | HasAuthority | Players Drunk | none |
| CrewAnimator, FirstPersonBody, FirstPersonHands, FirstPersonArm, HandHeldProp, HeldUsable, EquipItem, ItemArt, SmokeTimeline, SmokeVision, SmokeCloud, SmokeTextures, MirrorSurface | none | | read replicated | none |

### 11.3 Interaction (track INTERACTION)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| Interaction/HingedPanel.cs | Defensive guard in `SetOpen(bool,int)` and `Rattle`. New internal `Snap()` for the snapshot. `Command` and `Jiggle` stay ungated: they are the client's apply path. FixedUpdate runs on both (a local swing from the replicated `isOpen`). | HasAuthority | Doors Panel (host polls `IsOpen`) | DoorOpened, DoorClosed, WindowOpened, WindowClosed forwarded |
| Interaction/HingedGroup.cs | Guards in `SetOpen(bool,int)` and `Rattle` | HasAuthority | derived | group events forwarded |
| Interaction/HingeCollisionRelay.cs | `OnCollisionEnter` gate | HasAuthority | none | none |
| Interaction/DoorLock.cs | `OnWorldEvent` gate. The client writes `locked` directly, never `Unlock()` / `Lock()`. | HasAuthority | Doors Lock (host polls `locked`) | DoorUnlocked, DoorLockedRattle forwarded; rattle: DoorSync calls `Jiggle` |
| Interaction/KeyItem.cs | Update gate. `NetTransforms.Snap` in `Place`. | HasAuthority | holder and pocket via Items | none |
| Interactable, HouseInteractionSetup (F8 central), NPC/DoorPassage | none | | | |

### 11.4 Destruction, structure (track DESTRUCTION)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| Destruction/HouseDestruction.cs | After `BuildGraph`, `if (Net.IsClient) StructureGraph.Uninstall(graph)`. FixedUpdate gate. `ResetDestruction` is host-only (F11 is off online). | IsClient, HasAuthority | none | StructureDamaged, StructureCollapsed, DoorBroken forwarded |
| Destruction/DestructibleModule.cs | `OnCollisionEnter` and `OnChunkCollision` gates. Host hooks: `Fracture` after `IsFractured = true`; both exits of `Detach` (existing velocities, no new Random); `UpdateChunkLook` only when `IsFractured`; `Refresh` and `CollapseWhole` state. New `NetFracture(left)`, `NetDetach(i, fell, vanished, v, w)` (bookkeeping without graph, Refresh or events; body or deactivate; dust; `PlayFromNet` crunch; local fixture drop), `NetChunkLook`, `NetState`, `NetApplySnapshot`. | HasAuthority, IsHost | Structure 1 to 4, 6 | none extra |
| Destruction/RoofSection.cs | Host `Fall` sends RoofFell and tracks the roof. `NetFall()`: kit colliders off, slab collider, kinematic Rigidbody, no DebrisPiece, no Register, dust. | IsHost | Structure RoofFell, then transforms | none |
| Destruction/DestructionEvents.cs | Early return in every method | HasAuthority | none | safety net |
| Destruction/DebrisManager.cs | Public `WakeFromBlast(pos, radius, power)`. No `Freeze` on the client, so replicated kinematic props never pass through frozen debris. | IsClient | none | none |
| StructureGraph, ChunkCollisionRelay, DestructibleChunk, ChunkSetData, Catalog, DestructionLayers, DestructionDebug (central keys) | none | | | |

### 11.5 Destruction, props and blast (track DESTRUCTION)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| Destruction/Breakable.cs | `OnCollisionEnter` and `ApplyDamage` gates. Host emits at `MarkBroken`, and for Destroyed in `Shatter` AFTER the child Breakable and GlassPane loop and BEFORE the own-renderer block (point, impulse, inherited velocity), so the children's records come first. `LetGo` and `MarkRemoved` gated. New `NetApply(state, in DamageEvent, Vector3 inherit, bool silent)` with a velocity override (the client's body is kinematic); it never recurses into children (their own records arrive first). | HasAuthority, IsHost | Props Breakable | ObjectDamaged, ObjectDestroyed, Contract*, StructureDamaged, DoorBroken forwarded |
| Destruction/GlassPane.cs | Same pattern. Replica entries for Crack and Shatter. | HasAuthority, IsHost | Props Glass | WindowBroken forwarded |
| Destruction/Explosion.cs | `Detonate` gate. The cosmetic block sends ExplosionFx (in its own try/catch). New `PlayCosmetic(pos, radius, power)`: SyncTransforms, PushBodies (only local debris is dynamic on the client), CameraShake, ExplosionFX, Boom through `PlayFromNet`, `DebrisManager.WakeFromBlast`. No `Detonated`, no WorldEvents. KnockPlayers is unchanged: `AddImpulse` forwards, and Drunkenness on P2 replicates. | HasAuthority, IsHost | Props ExplosionFx | Explosion, PlayerKnockedDown forwarded (4.2) |
| Destruction/ImpactAudio.cs | Host: after the rate-limit check, send Sound unless `kind == Kind.Boom`. Client: local `Play` returns, and `PlayFromNet(kind, pos, volume)` plays. `RaiseNoise` gated. | IsHost, IsClient, HasAuthority | Props Sound (U) | LoudNoise host-only, forwarded |
| Items/GrenadeItem.cs | `Tick`: `if (fuseLeft <= 0f && Net.HasAuthority) Explode();`. `OnNearbyDetonation` gate. `Create` announces when host and `NetIds.Ready`. Arm and pin changes are sent. New `NetApply(armed, pinOut, fuseLeft, armedBy)` hides the pin and calls `Arm` for the blink. | HasAuthority, IsHost | Props Grenade; Spawns | none |
| MeshShatter, ExplosionFX, BlastSolver, ImpactDamage, DebrisPiece, DestructionFX, DestructionMaterialTable, BreakMaterial, GrenadeCrate, Effects/CameraShake | none | | | |

### 11.6 Grandmother (track GRANDMA)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| NPC/GrandmaBrain.cs | FIRST line of Update: `if (!Net.HasAuthority) { anim.SetSpeed(mover.CurrentSpeed); return; }`. Gates in `OnTalk` and `OnWorldEvent`. `ApplyReplica(state, flags)`. | HasAuthority | Grandma Flags | KeysHandedOver, TheftWitnessed, GrandmaNoticed, GrandmaBumped, GrandmaCalledPolice forwarded |
| NPC/GrandmaMover.cs | Gates in Start (no bake, no agent on the client) and `OnWorldEvent`. Update client branch `TickReplica`: CurrentSpeed from the interpolated motion, never `body.Move`. Replica `CollisionsOn`. | HasAuthority | root on the transform stream; Flags CollisionsOn | none |
| NPC/GrandmaSenses.cs | `!Net.HasAuthority` added to the guards of Update, FixedUpdate and `OnWorldEvent` | HasAuthority | none | none |
| NPC/GrandmaMood.cs | Update gate. `ApplyReplica(patience, inLastWarning)`. | HasAuthority | Grandma Mood | GrandmaMoodChanged forwarded |
| NPC/GrandmaActivities.cs | `ApplyReplica(spot, phase)` | none | Grandma Activity | none |
| NPC/GrandmaAnimation.cs | Host hook after the early-outs of `CrossFade`. `ApplyCrossFade(hash, layer)`, and snapshot `Play(hash, layer, t)`. | IsHost | Grandma Anim | none |
| NPC/GrandmaSpeech.cs | Host hook after the variant pick (no extra Random). `SayVariant(line, variant, arg)` formats in the local language. Hook on `Hush`. | IsHost | Grandma Speech, Hush | none |
| NPC/FireplaceFire.cs | `Ignite(float secondsLeft)` overload | none | Grandma Fire (polled) | none |
| GrandmaProps (Show / HideAll called by the sync), GrandmaTalk, GrandmaHUD, GrandmaDebug, HandSockets, NavMeshBaker, ActivitySpot, Stimulus, GrandmaLines, GrandmaMoodTable, Audio/GrandmaVoice, GrandmaSteps, GrandmaActivityAudio | none | | read replicated | |

### 11.7 Game loop and truck (track GAMELOOP-TRUCK)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| Gameplay/GameSession.cs | Client branch right after the `releaseFrame` block (display clock only; it returns before the restart branch). `OnWorldEvent` gate. Gates in `Deliver`, `ForceComplete`, `Fail`, `DebugSetTimeLeft`, `DebugSkipIntroCard`. Host keeps the intro card until `Net.PeerReady`. `ApplyReplica(...)` writes `Session.*` and the fields, stamps local times, calls `SetCrewFrozen`, `MarkDelivered` and money, then `Announce()` per transition. Host sends on every transition. | HasAuthority, IsHost | Session State, Clock, SettlementLine, SettlementEnd | ContractDelivered, ItemStolen forwarded; SessionStateChanged re-raised locally |
| Gameplay/TheftLedger.cs | `ApplyPut`, `ApplyDrop`, `ApplyFinal`, each then `Changed()` | none | Session LedgerPut, LedgerDrop, LedgerFinal | none |
| Contracts/Settlement.cs | `static Settlement FromReplica(completed, failure, lines)` | none | Session SettlementLine, SettlementEnd | none |
| Gameplay/SceneReload.cs | Online: host calls `NetSession.ReloadForBoth()` and returns; client returns (its E travels as input) | IsOnline, IsHost | none | none |
| Contracts/DeliverPoint.cs | `Interact` gate | HasAuthority | none | none |
| Vehicles/TruckVehicle.cs | Gates in FixedUpdate and `OnCollisionEnter`. `ramp.Stow()` in `SetDriver` gated. `Velocity`, `Pedal`, `Handbrake` (stubbed by CORE), `ApplyReplica(...)`. `SpeedKmh` reads `Velocity`. `NetTransforms.Snap` in `PlaceAt`. | HasAuthority | Truck State (U) | none |
| Vehicles/VehicleSeat.cs | Update gate. The chase camera only when `Net.IsLocal(driver)`. Lines 218 and 419 read `vehicle.Velocity`. `Release` / `ForceRelease` use the remote driver's `chaseYaw`. `ApplyEnter`, `ApplyExit`, `ForceRelease` (the body part of TryEnter / GiveBack, shared). Host sends seat records and the notice. | HasAuthority, IsLocal | Truck Seat* | none |
| Vehicles/ChaseCamera.cs | The SphereCast treats as loose any body with `NetIds.IdOf(body.gameObject) != 0 \|\| !body.isKinematic`, so replicated kinematic items never pull the camera in | none (0 offline) | none | none |
| Vehicles/CrewBumper.cs | For a member not driven here: replicated velocity, per-member cooldown `Net.RoundTrip + 0.2 s`, truck hull vs capsule IgnoreCollision for that window (9.6) | Drives | none | Players Impulse via AddImpulse |
| Vehicles/TruckCargo.cs | FixedUpdate gate. `ApplyReplica(kg, volume, count)`. | HasAuthority | Truck Cargo; `loaded` via Items Flags | CargoLoaded, CargoUnloaded forwarded |
| Vehicles/TruckRamp.cs | `LandingClear` only with authority. `ApplyReplica(state)`. | HasAuthority | Truck Ramp | none |
| Vehicles/TruckWheelVisuals.cs | On the client, rpm is derived from `ForwardSpeed` | IsClient | none | none |
| Audio/TruckAudio.cs | Pedal and handbrake from `TruckVehicle.Pedal` / `Handbrake` when `!HasAuthority` | HasAuthority | none | none |
| ContractManager, ContractTracker, PocketableRule, GameLoopTuning, GameSettings, MutedInputReader, SceneLookup, SessionDebug, EventLogOverlay, MoversDebugTools, Core/SceneFlow | none | | | |

### 11.8 UI, menu and flow (track UI-AUDIO)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| UI/Hud/HudPauseMenu.cs | `SyncWorldPause` restores and returns online. The Menu row calls `NetSession.LeaveToMenu()` online. | IsOnline | none | none |
| UI/Hud/HudRoot.cs | End-card `LoadMenu` becomes `LeaveToMenu` online | IsOnline | none | none |
| UI/Hud/Views/CardsView.cs | Replay: host `ReloadForBoth`, client `RequestReplay`. Menu: `LeaveToMenu`. | IsOnline | none | none |
| UI/Hud/Views/PauseView.cs | "Quitter la partie" label online. The Layout row is hidden online. | IsOnline | none | none |
| UI/Hints/VerbHints.cs | `truck.Velocity` instead of `Body.linearVelocity` | none | none | none |
| Menu/MainMenu.cs, MainMenuModel.cs, MainMenuView.cs | `TitleRow.Online` and the Online, Hosting and Joining pages (3.3). Text-field guard. Poll `NetSession.Status` (never creates a session). `TakeMenuMessage` on the title. | none | none | none |
| Menu/Loading/LoadingScreen.cs | The Finishing step holds while `Net.IsClient && !Net.PeerReady` (NetSession enforces the 20 s limit) | IsClient | none | none |
| Menu/Loading/LoadingView.cs | Crew row "toi" / "J1 en ligne" online | IsOnline | none | none |
| Menu/NetText.cs (new) | `Loc.Register` of the net strings (the list in the ui survey, plus `net.notLinked`, `net.payment`, `net.version`, `net.full`) | | | |
| SharedHudModel, PlayerHudModel, ToastFeed, HudToasts, all views, indicators, GameHUD, SceneFlow, HudBootstrap | none | | read replicated | ToastFeed via forwarded events |

### 11.9 Audio and environment (track UI-AUDIO)
| File | Hook | Gate | State | Events / FX |
|---|---|---|---|---|
| Audio/CrewFootsteps.cs | For a body not driven here: velocity and grounded from `PlayerController.Velocity` / `Grounded` (stubbed by CORE) | Drives | none | none |
| Audio/CrewSounds.cs | Drag scrape speed from the held transform's delta when `rb.isKinematic && Net.IsClient` | IsClient | none | none |
| Audio/PropImpactSound.cs | `OnCollisionEnter` gate. Host sends PropImpact with the pitch actually used. | HasAuthority, IsHost | Audio PropImpact (U) | none |
| Audio/Occlusion.cs | On the client, also skip colliders whose attached rigidbody is registered (`NetIds.IdOf(go) != 0`) | IsClient | none | none |
| WorldSoundEvents, AudioDirector, VoicePool, Ears, AmbienceAudio, MusicPlayer, FireAudio, SceneAudioBinder, SfxBank, VoiceBank, Synth/*, Environment/* | none (Environment must never be gated: its colliders are needed on the client) | | | |

### 11.10 Scene ids and the event bus (track CORE)
- `Net/NetIds.cs`: the sweep (`NetIdSweep`), the registry, `NetRef`, `NetKind`, spawn ids, the digest and the dump (section 5).
- `Core/WorldEvents.cs`: the one relay line in `Raise` (section 8). Nothing else changes; offline the listener list and its count are unchanged.

## 12. Frozen contracts

**Namespace and folders.** New files use namespace `Movers` (no using directives in gameplay files). They live under `Scripts/Net/`, with syncs in `Scripts/Net/Sync/`. Only `NetSession.cs` references NGO or Services. No asmdef.

```csharp
// ===== Net/Net.cs (CORE) =====
public enum NetRole : byte { Offline = 0, Host = 1, Client = 2 }

public static class NetOrder
{
    public const int WorldApply    = -530; // client: interpolated world poses (Update)
    public const int PlayerDriver  = -520; // both: puppet pose apply (Update), local pose sample (LateUpdate)
    public const int AnchoredApply = 5;    // client: items held by the local player (Update)
    public const int Tick          = 2900; // both: sync ticks, transform stream, flush (LateUpdate)
}

public static class Net
{
    public const int HostMember = 0;
    public const int ClientMember = 1;

    public static NetRole Role { get; }          // Offline until NetSession sets it
    public static bool IsOnline { get; }         // Role != Offline
    public static bool IsHost { get; }
    public static bool IsClient { get; }
    public static bool HasAuthority { get; }     // Role != Client
    public static bool PeerConnected { get; }    // online and the other machine is connected
    public static bool PeerReady { get; }        // host: snapshot sent for this epoch; client: snapshot applied
    public static byte Epoch { get; }            // game-scene load counter of the session
    public static int LocalMember { get; }       // -1 offline, 0 host, 1 client
    public static float RoundTrip { get; }       // seconds, smoothed; 0 offline
    public static bool Scripted { get; }         // -netbot is running this machine (tests)

    public static bool IsLocal(CrewMember m);    // offline true; online m.index == LocalMember
    public static bool Drives(CrewMember m);     // offline true; host: P1, and P2 once the peer left; client: P2
    public static bool Drives(int memberIndex);

    internal static void SetRole(NetRole role);                  // NetSession only
    internal static void SetPeer(bool connected, bool ready);    // NetSession only
    internal static void SetEpoch(byte epoch);                   // NetSession only; closes the open batch first
    internal static void SetRoundTrip(float seconds);            // NetSession only
    internal static void SetScripted(bool on);                   // NetSession only
}

public struct NetPose                            // PlayerController.ApplyNetPose input
{
    public Vector3 position;
    public float yaw;                            // degrees
    public Quaternion camLocalRotation;          // pitch and drunk sway
    public float height;                         // CharacterController height
    public bool crouching, grounded, throwHeld;
    public Vector3 velocity;
}

// ===== Net/NetWire.cs (CORE) =====
public sealed class NetWriter
{
    public int Length { get; }
    public void WriteByte(byte v);
    public void WriteSByte(sbyte v);
    public void WriteBool(bool v);
    public void WriteUShort(ushort v);
    public void WriteShort(short v);
    public void WriteUInt(uint v);
    public void WriteInt(int v);
    public void WriteULong(ulong v);
    public void WriteFloat(float v);
    public void WriteHalf(float v);                  // IEEE 754 half, 2 bytes
    public void WriteUnit(float v01);                // 0..1 clamped, 1 byte
    public void WriteAngle(float degrees);           // 0..360, 2 bytes
    public void WriteVector3(Vector3 v);             // 12 bytes
    public void WriteVector3Half(Vector3 v);         // 6 bytes
    public void WriteRotation(Quaternion q);         // smallest three, 4 bytes
    public void WriteString(string s);               // u8 byte count + UTF-8, <= 200 bytes, null = 255
    public void WriteRef(in NetRef r);               // 5 bytes
    public void WriteRef(UnityEngine.Object o);      // NetIds.RefOf(o)
    public void WritePose(in NetPose p);             // 27 bytes (9.2)
}

public sealed class NetReader
{
    public int Remaining { get; }
    public byte ReadByte();
    public sbyte ReadSByte();
    public bool ReadBool();
    public ushort ReadUShort();
    public short ReadShort();
    public uint ReadUInt();
    public int ReadInt();
    public ulong ReadULong();
    public float ReadFloat();
    public float ReadHalf();
    public float ReadUnit();
    public float ReadAngle();
    public Vector3 ReadVector3();
    public Vector3 ReadVector3Half();
    public Quaternion ReadRotation();
    public string ReadString();
    public NetRef ReadRef();
    public NetPose ReadPose();
    public T ReadObject<T>() where T : UnityEngine.Object;   // ReadRef, then NetIds.Resolve<T>; null if gone
}

// ===== Net/NetIds.cs (CORE) =====
public enum NetKind : byte
{
    None = 0, Crew, Movable, Body, Breakable, Glass, Module, Roof, Panel, Group, Lock,
    Usable, Grandma, Spot, Fire, Truck, Seat, Cargo, Ramp, Deliver, Session, House
}

public readonly struct NetRef : System.IEquatable<NetRef>
{
    public readonly uint id;
    public readonly NetKind kind;
    public NetRef(uint id, NetKind kind);
    public bool IsNone { get; }
    public static readonly NetRef None;
    public bool Equals(NetRef other);
}

public static class NetIds
{
    public const uint SpawnBit = 0x80000000u;
    public static bool Ready { get; }                       // the sweep ran for the current scene
    public static int Count { get; }
    public static ulong Digest { get; }
    public static int TrackedBodyCount { get; }
    public static uint IdOf(GameObject go);                 // 0 if not registered (always 0 offline)
    public static GameObject Find(uint id);                 // null if unknown or destroyed
    public static NetRef RefOf(UnityEngine.Object o);       // CrewMember -> (index, Crew); registered component -> (goId, kind)
    public static UnityEngine.Object Resolve(in NetRef r);
    public static T Resolve<T>(in NetRef r) where T : UnityEngine.Object;
    public static uint NewSpawnId();                        // host only
    public static void RegisterSpawn(uint id, GameObject go);
    public static void Forget(uint id);
    public static void DumpTo(string path);                 // "id kind x y z path" lines, sorted
}
// NetIdSweep : MonoBehaviour, [DefaultExecutionOrder(10000)], internal, lives in NetIds.cs.

// ===== Net/NetSync.cs (CORE) =====
public enum NetSyncId : byte
{
    Control = 0, Events = 1, Spawns = 2, Items = 3, Structure = 4, Props = 5, Doors = 6,
    Players = 7, Truck = 8, Grandma = 9, Session = 10, Audio = 11, Transforms = 12, Count = 13
}   // order = snapshot order

public abstract class NetSync
{
    public abstract NetSyncId Id { get; }
    public virtual void OnSceneReady() { }              // both roles, online, right after the NetIds sweep; subscribe here
    public virtual void HostTick() { }                  // host, every frame at NetOrder.Tick while Net.PeerReady
    public virtual void ClientTick() { }                // client, every frame at NetOrder.Tick
    public virtual void SendSnapshot() { }              // host, once per Ready, writes records via NetOut.Reliable
    public abstract void Receive(byte op, NetReader r); // a record of this sync from the peer
    public virtual void Digest(ref ulong hash) { }      // optional, manual desync hunting
    public virtual void OnPeerLeft() { }                // host: the client is gone
    public virtual void OnSessionEnd() { }              // both: leave or reload; unsubscribe and clear caches here
    public static void Register(NetSync sync);          // stores into slot [Id], overwrites, never clears
    public static NetSync Get(NetSyncId id);
    public static void HashInto(ref ulong hash, ulong value);   // FNV step
}

public static class NetOut
{
    public const int MaxRecord = 1000;                              // payload bytes; larger: LogError and drop
    public static bool CanSend { get; }                             // online, peer connected, and (PeerReady || InSnapshot)
    public static NetWriter Reliable(NetSyncId sys, byte op);       // null when !CanSend (Control excepted); rolls back an unclosed record
    public static NetWriter Unreliable(NetSyncId sys, byte op);     // same rule
    public static void End(NetWriter w);                            // close the record; required after every non-null Reliable / Unreliable
    public static void FlushNow();                                  // leave, shutdown
    internal static bool InSnapshot { get; set; }                   // NetSession: SnapshotBegin .. SnapshotEnd
    internal static void Dispatch(byte[] buf, int len);             // NetSession: a received batch
}

// ===== Net/NetSpawns.cs (CORE) =====
public enum NetSpawnKind : byte { Cigarette = 0, Beer = 1, Grenade = 2 }

public static class NetSpawns
{
    public static void RegisterFactory(NetSpawnKind kind, System.Func<Vector3, Quaternion, GameObject> factory);
    public static void Announce(GameObject go, NetSpawnKind kind);  // host, after NetIds.Ready; no-op otherwise
}

// ===== Net/NetTransforms.cs (CORE; WorldApply and AnchoredApply components live here) =====
public static class NetTransforms
{
    public const float SendHz = 20f;
    public const float InterpolationDelay = 0.1f;
    public static void Track(uint id, Transform t);  // both roles, idempotent
    public static void Untrack(uint id);
    public static void Snap(uint id);                // host: the next sample teleports
    public static void Snap(GameObject go);          // host: same, by object; no-op offline or unregistered
}

// ===== Net/WorldEventRelay.cs (CORE) =====
public static class WorldEventRelay
{
    public static bool Replaying { get; }            // client: true while re-raising a host event
    internal static void OnRaised(in WorldEvent e);  // called by WorldEvents.Raise when Net.IsOnline
}

// ===== Net/NetSession.cs (CORE, the only NGO / Services file) =====
public enum NetStatus : byte { Idle, Connecting, WaitingForPeer, PeerJoined, Lobby, Loading, Playing, Failed }
public enum NetError : byte
{
    None, ServicesUnavailable, NotLinked, BadCode, ConnectFailed, Timeout, VersionMismatch, SessionFull, HostLeft,
    PaymentRequired
}

public sealed class NetSession : MonoBehaviour     // on the _Net root, DontDestroyOnLoad, [DefaultExecutionOrder(NetOrder.Tick)]
{
    public const ushort DefaultPort = 7777;
    public const ushort Protocol = 1;
    public static NetStatus Status { get; }        // getters never create a session
    public static NetError Error { get; }
    public static string ErrorDetail { get; }      // exception text, log only
    public static string JoinCode { get; }         // Relay code, or "ip:port" when hosting direct
    public static event System.Action StatusChanged;
    public static void HostRelay();                // creates _Net
    public static void HostDirect(ushort port = DefaultPort);   // creates _Net
    public static void Join(string codeOrAddress); // creates _Net; 6 chars A-Z0-9 = Relay code, else ip[:port]
    public static void StartGame();                // host, when Net.PeerConnected
    public static void Cancel();                   // menu: shut down, Idle
    public static void LeaveToMenu();              // in game
    public static void ReloadForBoth();            // host; idempotent (3.6)
    public static void RequestReplay();            // client; idempotent
    public static string TakeMenuMessage();        // Loc key for the title screen, once, or null
    internal static void Send(bool reliable, byte[] buf, int len);   // NetOut only
}
// Command line (both roles): -nethost [port|relay], -netjoin <code|ip[:port]>, -netautostart,
// -netlog <path>, -netbot <script>, -netdumpids <path>; client test only: -netbotpeerlog <path>.
// -nethost relay writes "NETJOINCODE <code>" to the net log and Debug.Log (16.7).

// ===== Changes to existing CORE files =====
// Core/CrewInput.cs:        public CrewInputFrame RawFrame => frame;   // this poll's frame, unmuted
//                           public void ResyncHeld();                  // ignoreNextHeld = true
// Core/WorldEvents.cs:      in Raise, after the depth guard, before the loop:
//                           if (Net.IsOnline) WorldEventRelay.OnRaised(e);
// Debug/SliceDebug.cs:      online dispatch policy (section 10); DebugCommands.Register unchanged
// Physics/MovableObject.cs: if (!Net.HasAuthority) return; at the top of OnCollisionEnter

// ===== Cross-track contracts. CORE commits compiling stubs first (13.2); the owner fills the bodies =====
// PLAYERS
public sealed class RemoteInputSource : ICrewInputSource   // Net/RemoteInputSource.cs
{
    public string Label { get; }                   // "Remote"
    public bool IsGamepad { get; set; }
    public void Poll(ref CrewInputFrame frame, float dt);   // never reports CrewButton.Pause; stub: empty frame
}
// PlayerController (existing Velocity keeps its signature; offline and driven bodies unchanged)
public bool Grounded { get; }                      // stub: cc != null && cc.isGrounded
public bool NetThrowHeld { get; }                  // stub: false
public void ApplyNetPose(in NetPose pose);         // stub: empty
// GAMELOOP-TRUCK: TruckVehicle
public Vector3 Velocity { get; }                   // stub: rb.linearVelocity
public float Pedal { get; }                        // stub: 0; -1..1
public bool Handbrake { get; }                     // stub: false
// GAMELOOP-TRUCK: TruckSync
public static byte SeatEpoch { get; }              // stub: 0; bumped by every SeatEnter / SeatExit
// DESTRUCTION: ImpactAudio
public static void PlayFromNet(Kind kind, Vector3 position, float volume);   // stub: empty
// Every Net/Sync/*.cs: an empty NetSync subclass with its Id and Receive, stubbed by CORE.
// UI-AUDIO: none new. HudPauseMenu.OpenCount (existing) is read by PlayerSync.
```

**Rules that go with the contracts.**
- **Sync ops.** Each track owns the op numbers inside its own `NetSyncId`, as listed in 4.3. Adding an op is the owner's call. Renumbering a published op is not allowed.
- **Registration.** `NetSync.Register` stores the sync in an array slot indexed by `Id` and overwrites it. It never clears, and NetSync has no SubsystemRegistration reset of its own, so the order of `RuntimeInitializeOnLoadMethod` calls does not matter.
- **No static subscriptions at registration.** No sync, and not WorldEventRelay, subscribes to WorldEvents or any static event at registration. They subscribe in `OnSceneReady` (online only) and unsubscribe in `OnSessionEnd`. WorldEventRelay needs no subscription at all (the line in `WorldEvents.Raise`). So the offline listener count is the baseline's.
- **Guaranteed order within a frame.** `HostTick` of every sync, then the transform stream, then Despawn detection, then the flush.
- **NetOut outside Tick.** A sync may call `NetOut` from any main-thread code, for example a hook inside `Explosion.Run`. Records keep call order.

## 13. Tracks, file ownership and workflow

### 13.1 Ownership
- **Order.** Step 0 (INTEGRATION), then CORE, then the CORE stubs commit, then PLAYERS, INTERACTION, DESTRUCTION, GRANDMA, GAMELOOP-TRUCK and UI-AUDIO in parallel, then INTEGRATION merges.
- **One owner per file,** as in `03_TECHNICAL/SLICE_ARCHITECTURE.md` section 3. A change needed in someone else's file is written as a request, not made. The only exception is the CORE stubs commit (13.2).
- **Paths.** The paths below are under `UnityProject/Assets/_Movers/Scripts/`, except where stated.

| Track | Edits | New files |
|---|---|---|
| CORE | Core/CrewInput.cs, Core/WorldEvents.cs, Debug/SliceDebug.cs, Physics/MovableObject.cs; stubs only (13.2): Player/PlayerController.cs, Vehicles/TruckVehicle.cs, Destruction/ImpactAudio.cs | Net/Net.cs, Net/NetWire.cs, Net/NetIds.cs, Net/NetSync.cs, Net/NetSpawns.cs, Net/NetTransforms.cs, Net/WorldEventRelay.cs, Net/NetSession.cs, `tools/net/compilecheck.sh`; stubs only: Net/RemoteInputSource.cs, Net/Sync/*.cs (9 files) |
| PLAYERS | Player/PlayerController.cs, Player/PlayerGrab.cs, Player/HeldPose.cs, Player/CursorLock.cs, Player/CrewSpawner.cs, Interaction/PlayerInteract.cs, Items/PlayerPockets.cs, Items/PlayerEquip.cs, Items/CrewEquip.cs, Items/CigaretteItem.cs, Items/BeerItem.cs, Items/StartingItemSpawner.cs, Effects/Drunkenness.cs, Net/RemoteInputSource.cs, Net/Sync/PlayerSync.cs, Net/Sync/ItemSync.cs | Net/NetPlayerDriver.cs |
| INTERACTION | Interaction/HingedPanel.cs, HingedGroup.cs, HingeCollisionRelay.cs, DoorLock.cs, KeyItem.cs, Net/Sync/DoorSync.cs | none |
| DESTRUCTION | Destruction/HouseDestruction.cs, DestructibleModule.cs, RoofSection.cs, DestructionEvents.cs, DebrisManager.cs, Breakable.cs, GlassPane.cs, Explosion.cs, ImpactAudio.cs, Items/GrenadeItem.cs, Net/Sync/StructureSync.cs, Net/Sync/PropsSync.cs | none |
| GRANDMA | NPC/GrandmaBrain.cs, GrandmaMover.cs, GrandmaSenses.cs, GrandmaMood.cs, GrandmaActivities.cs, GrandmaAnimation.cs, GrandmaSpeech.cs, FireplaceFire.cs, Net/Sync/GrandmaSync.cs | none |
| GAMELOOP-TRUCK | Gameplay/GameSession.cs, TheftLedger.cs, SceneReload.cs, Contracts/Settlement.cs, DeliverPoint.cs, Vehicles/TruckVehicle.cs, VehicleSeat.cs, TruckCargo.cs, TruckRamp.cs, TruckWheelVisuals.cs, ChaseCamera.cs, CrewBumper.cs, Audio/TruckAudio.cs, Net/Sync/SessionSync.cs, Net/Sync/TruckSync.cs | none |
| UI-AUDIO | UI/Hud/HudPauseMenu.cs, UI/Hud/HudRoot.cs, UI/Hud/Views/CardsView.cs, UI/Hud/Views/PauseView.cs, UI/Hints/VerbHints.cs, Menu/MainMenu.cs, MainMenuModel.cs, MainMenuView.cs, Menu/Loading/LoadingScreen.cs, LoadingView.cs, Audio/CrewFootsteps.cs, CrewSounds.cs, PropImpactSound.cs, Occlusion.cs, Net/Sync/AudioSync.cs | Menu/NetText.cs |
| INTEGRATION | Step 0: `UnityProject/Packages/manifest.json`, `UnityProject/Packages/packages-lock.json`, `UnityProject/ProjectSettings/ProjectSettings.asset`, `UnityProject/ProjectSettings/ProjectVersion.txt`. Then: `UnityProject/Assets/_Movers/Editor/BuildGreybox.cs` (new method only), 00_PROJECT/PROJECT_STATE.md, changelog/CHANGELOG.md, 03_TECHNICAL/SLICE_ARCHITECTURE.md (a pointer section) | Net/Test/NetTestBot.cs, `UnityProject/Assets/_Movers/Editor/MoversNetTestCLI.cs` |

The 9 sync stubs: PlayerSync, ItemSync, DoorSync, StructureSync, PropsSync, GrandmaSync, SessionSync, TruckSync, AudioSync. Control, Events, Spawns and Transforms are implemented by CORE inside its own files.

### 13.2 Workflow
- **Step 0, baseline (INTEGRATION, before anything else).** Done on main in 1bd50e5, without `ProjectVersion.txt` (16.6).
  - Remove `com.unity.inputsystem` from the manifest (it came in with c0bd6b0 as a Convai leftover; nothing depends on it, and its presence switches input handling back to Both). Keep `com.unity.ugui` and `com.unity.nuget.newtonsoft-json`, which Services need.
  - Restore `activeInputHandler: 0` (legacy Input Manager, as commit db706ea intended).
  - `ProjectVersion.txt` is not part of the baseline: by Pierre's decision it is never modified or committed (16.6).
  - With Pierre's go, commit this as the baseline. Run the existing CLIs and the offline Play of Map01 (split, solo P1, solo P2) on it, and record the results and the commit sha in PROJECT_STATE. Section 14 check 1 compares against that sha.
- **CORE, then the CORE stubs commit.** CORE writes the Net core, then commits compiling stubs of every cross-track member (section 12, "Cross-track contracts"), each with an offline-correct body. Only then do the parallel tracks start, from that commit. The owner fills the bodies.
- **Parallel tracks.** Each track works in its own git worktree, on its own local branch from the stubs commit. It never touches the Unity editor.
  - It proves its compile with the out-of-editor check `tools/net/compilecheck.sh` (CORE writes it from the existing index-compile pattern: Roslyn from the installed editor, against the UnityEngine DLLs, `Library/ScriptAssemblies` of the main checkout for LumaFlow, TargetIndicators, `Unity.Netcode.Runtime`, `Unity.Networking.Transport` and `Unity.Services.*`), for runtime, then runtime + editor. 0 errors.
  - It delivers its branch (local commits, never pushed), its compile log, and its gate list (every gate added, with file and line).
- **No track other than INTEGRATION calls any Unity MCP tool.** One editor serves everyone, Pierre uses it at the same time, a half-written file breaks the shared assembly, and every script reload drops the MCP bridge.
- **INTEGRATION** merges one track at a time into `feat/online-coop`. After each merge: the editor compile (0 errors), the offline Play of Map01 (split, solo P1, solo P2) with a clean console, and the relevant CLIs (MoversPlaytestCLI, MoversItemsPlaytestCLI, MoversStartingInventoryCLI, MoversSmokeCLI). A failure goes back to the owning track. Commits on `feat/online-coop` and any push happen on Pierre's request.
- **Editor preconditions (INTEGRATION, every time).** Before building or entering Play: check `EditorApplication.isPlaying`, every open scene's `isDirty`, and the Selection. If Pierre is playing or has unsaved changes, stop and ask; never save or discard for him. Exit Play only if the test entered it.

## 14. Test plan

**Build.** INTEGRATION adds `BuildGreybox.NetClient()` and leaves `Windows64()` unchanged. `NetClient()`:
- uses the enabled `EditorBuildSettings.scenes` (MainMenu, Map01_PierreKit_House), `BuildOptions.Development` (debug keys, the direct-host option, NGO's message-size validation), `StandaloneWindows64`, `Build/Movers.exe` (that is `C:/GameProject/UnityProject/Build/`, gitignored);
- refuses to start when `EditorApplication.isPlaying` or any open scene `isDirty`, logs `BUILD_RESULT`, and never calls `EditorApplication.Exit`;
- runs from the open editor through the MCP, outside Play mode.
A build is taken after every scene or script change: the id digest refuses a stale one. Build and host always come from the same saved state.

**Loopback, run 1: build versus build** (no editor state involved; `forceSingleInstance` is 0, so two instances run). All paths absolute under `C:/GameProject/UnityProject/`:
- Host: `Build/Movers.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile Logs/host_player.log -nethost 7777 -netautostart -netbot host -netlog Logs/net_host.log -netdumpids Logs/ids_host.txt`
- Client: `Build/Movers.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile Logs/client_player.log -netjoin 127.0.0.1 -netautostart -netbot client -netlog Logs/net_client.log -netdumpids Logs/ids_client.txt -netbotpeerlog Logs/net_host.log` (`-netbotpeerlog` lets the client check 6 against the host's `NETTEST POSE` lines).
- Second run (client killed): `-netbot host-stay` on the host and `-netbot client-quit` on the client, same other arguments.
- `Net.Scripted` stops CursorLock from capturing the mouse.

**Loopback, run 2: editor host versus build client.** Only after an editor dump (`MoversNetTestCLI.RunHost`, which also writes the id dump) diffs empty against the build's. If it is not empty, the differing paths are logged, and the setup code is fixed not to depend on editor-only state (mesh readability, static batching).

**NetTestBot** (`-netbot host` or `client`, or `MoversNetTestCLI.RunHost` in the editor) prints its own `NETTEST PASS|FAIL <check> <detail>` lines to the net log. Host script: at t = 40 s, `Explosion.Detonate(p2.position + offset, ...)` called directly; at the end, F6 found in `DebugCommands.All` by key and run. No reflection, HashSet or Regex in any MCP RunCommand snippet. Client script, driving P2 through ScriptedInputSource, in stages with timeouts:
1. walk to the front door, press E;
2. grab a chair, carry it 5 m, throw it into a window pane;
3. stand still (the host's grenade);
4. on the end card, press E (replay for both).
Then the host kills the connection on its side for the disconnect check (run 1), and on a second run the client is killed.

**Checks.**
1. **Offline unchanged.** Same results as the baseline commit (13.2) for the existing CLIs; the offline Play of Map01 (split, solo P1, solo P2) has 0 console errors; no `_Net` object, NetSession or NetworkManager exists offline, and the WorldEvents listener count equals the baseline's.
2. **Handshake.** Equal id count (about 700), digest and tracked-body count (about 126) on both machines. `PeerReady` within 10 s of LoadScene. The client's loading screen stayed up until `SnapshotEnd`. The longest frame between LoadScene and PeerReady is under 5 s on both (else: bake the NavMesh asynchronously or after the handshake).
3. **Actions resolved on the host from the client's aim** (client stages 1 and 2, checked on the client replica): door open (Doors Panel), chair held by P2 (Items Flags holder 1), pane broken (Props Glass).
4. **Knockback** (stage 3): the client's P2 moved at least 1 m within 0.5 s of Impulse, and one PlayerKnockedDown was replayed with subject P2 and an Explosion before it.
5. **No double raise.** The client logged 0 local-raise warnings, and 0 dropped oversize records on either side.
6. **Transforms at rest.** 20 random tracked bodies within 1 cm and 1 degree between host and client, 2 s after the last motion (both sides print them; the host prints its list, the client checks against it).
7. **Reload** (stage 4): the client reloads once, epoch + 1, a new snapshot, the handshake passes again.
8. **Disconnect.** Client killed: host toast, P2 idle and falling under gravity, the run continues. Host killed: the client is back on the title screen with `net.hostLeft`.
9. **Health.** 0 console errors in both player logs. NetStats: down under 80 KB/s peak and 10 KB/s steady, up under 10 KB/s. Frame rate is informational in loopback (two instances share one GPU) and binding only in the two-PC run.

**Manual, after the loopback passes.**
- Relay from one PC: HostRelay in one build, then join from another instance using the code.
- Wrong code gives `net.badCode`. The network cable pulled gives `net.services` or `net.failed`.
- Finally Pierre and Jonathan, from two homes: a full run, then one replay. 60 fps on the host there.

## 15. Known limits accepted for the greybox
- **Latency on the client's own actions.** Grab, throw, pocket, smoke, drink, open and drive act about one round trip plus 35 ms of playout later, typically 80 to 170 ms over Relay, and so do their gesture sounds. Movement and look are instant.
- **Truck driving from the client** adds about 150 to 250 ms of steering latency. To be judged in the playtest.
- **Debris, smoke clouds, ash and splats differ** between the two screens. Heavy rubble crushes only on the host.
- **Door brake divergence:** on the client the leaf can pass through a shoved chair for a few frames.
- **Prompts can be one round trip stale:** a door the grandmother just opened can still read "Ouvrir".
- **The creak voice of a door may differ per machine** (instance-id hash).
- **Held-item collision.** The client's own held or dragged item ignores the client's own capsule. Offline it is a dynamic body that gets pushed.
- **Pause and the session mute share `CrewInput.Muted` on the host's P2.** If the intro card or end card releases the crew while the client's menu is open, P2 is unmuted early; the client's paused flag still zeroes its move.
- **No join mid-run, no reconnect, no host migration.** The snapshot machinery covers join at load only. Beer splats and earlier debris are not in the snapshot.
- **Online is 2 players, one per PC, with no split screen.** Each local player uses the keyboard, the mouse and the first pad at once (16.5).
- **F11 (destruction reset) is disabled online.** F1, Shift+F1 and F2 are refused on the host online.
- **The ids depend on identical builds and scene.** Pierre edits Map01 often, so both players must run the same build. The digest refuses a mismatch instead of desyncing.
- **No cheating protection,** and no bandwidth optimisation beyond quantisation, change-only sends and the far-body fallback.

## 16. Pierre's decisions (2026-09-27)
The questions this section asked were answered by Pierre the same day. His answers are binding and override the sections above where they differ.
1. **Pause online does not stop the world.** Kept: the pausing player stays in the house, visible and in reach of the grandmother.
2. **Who controls the run.** The defaults are kept:
   - either player skips the intro card (once both are loaded);
   - either player's E replays for both;
   - the client's "Menu" leaves alone, and the host continues with P2 idle;
   - the host's "Menu" ends it for both.
3. **A dropped player's body stays in the house** as an idle body. Kept.
4. **Relay only while it is free.** Relay is enabled for the linked project (cloud project `daa2bc4d-a341-48f8-895f-b2ca6608f325`), and the first Relay run asked for no payment. If Relay ever answers `PaymentRequired`: stop and report, and play by direct IP (`net.payment`). Nothing paid is ever attempted.
5. **Gamepad online: yes.** On each machine the one local player listens to the keyboard and mouse AND the first gamepad at once, through a small composite source (`LocalDevicesSource`, 9.1). This replaces "the client plays keyboard and mouse only". The join page keeps "Coller" for the pad.
6. **Step 0 is done on main** (1bd50e5): `com.unity.inputsystem` removed, `activeInputHandler: 0`. `ProjectVersion.txt` is never modified or committed: it would force Jonathan onto Pierre's editor build. This work adds and removes no package.
7. **Relay from the command line too.** `-nethost relay` hosts by Relay and writes the line `NETJOINCODE <code>` to the net log and Debug.Log. `-netjoin <6-char code>` joins by Relay.
8. **Speed, no test batteries:** build what the spec says, compile-check, one offline smoke, the loopback run, fix, done.

Convai is not an open question here: it was rejected the same day (commit db706ea, `04_PRODUCTION/REJECTED.md`, "nothing paid").

## 17. As built and tested (2026-09-27)

### 17.1 Where it is
- **Branch `feat/online-coop`, not pushed.** CORE (f469d4e to fee4e41, the stubs commit), then the merges of PLAYERS, GAMELOOP-TRUCK, DESTRUCTION, INTERACTION, GRANDMA, UI-AUDIO and the test kit (be58072 to 2d46da3), then the integration fixes (4d19d76 to ea23fd8).
- **Offline.** Every gate reads a static bool whose offline value keeps today's path, and no offline path gains a `Random` call. The only offline-visible change is the title row "Jouer en ligne" (3.3).

### 17.2 Changes from the spec
Each one was made by the owning track or by INTEGRATION, and is recorded here so the spec stays the reference.
- **Gamepad online (16.5).** `LocalDevicesSource`, at the bottom of `Player/CrewSpawner.cs`, is the local source of P1 on the host and of P2 on the client (9.1). `IsPad` / `IsKeyboard` replace the `is KeyboardMouseSource` checks in CursorLock, PlayerPockets, InputGlyphs, HudKeys, ButtonLabels, PlayerHudModel, SharedHudModel, DoorLock, VehicleSeat and the debug lookups (fix 4d19d76); HudPauseMenu uses `HasKeyboard()`. Offline values are unchanged.
- **CORE.**
  - The command line is parsed at BeforeSceneLoad and `_Net` is created at AfterSceneLoad, not at SubsystemRegistration: the reset order is undefined there, and NGO needs a loaded scene.
  - `_Net` is destroyed once its transport has shut down (Cancel, a menu failure, back to the menu), so a later offline game has no `_Net`.
  - Control ops 12 Ping and 13 Pong (4.3) measure `Net.RoundTrip`.
  - Extra public API: `NetOut.Now`, `PeerTime` and `PeerNow` (the estimated peer clock, used by the P2 playout); `NetLog` (`-netlog`); `NetSession.Notice` (a Loc key toast, `net.partnerLeft`, shown by HudRoot), `BotScript`, `LocKey` and `SetTestOptions`.
  - The client's 20 s timer starts when it sends Ready, the host's 30 s timer when it writes LoadScene or Reload.
  - The direct host's JoinCode shows the machine's first non-loopback IPv4 and the port.
  - `NetSpawns` falls back on the items' own `Create` when no factory is registered.
- **Ids (fix 5685ba3).** The digest no longer hashes the positions of tracked bodies and of the grandmother (5.5): the starting beer falls and she snaps to the NavMesh by an amount that depends on frame timing, so two copies of one build refused each other with `VersionMismatch`.
- **PLAYERS.**
  - The host plays P2 out at `NetOut.PeerNow - 35 ms`; the client poses P1 at `PeerNow - 0.1 s`.
  - If an InputPose's held-now differs from the last queued change (more than 16 changes lost), the host queues a synthetic change.
  - Drunk is sent for P2 only. P1's sway reaches the client inside the PuppetPose camera rotation.
  - Items Flags: `inPocket` and `worn` are applied only by the Pockets and Worn records (one writer per field). The host rescans MovableObjects every 1 s for fresh spawns.
  - SmokePuff: lifetime in 0.1 s steps and strength in 0.01 steps, one byte each.
  - While seated, the client sends `TruckSync.LocalChaseYaw` and the host feeds `TruckSync.SetRemoteChaseYaw` (fix 2ebee08).
  - The client's replica hold lets go of an object the host broke (fix 308886c).
- **INTERACTION.** `HingedPanel.Snap(bool open)`. The snapshot reuses ops 1 and 2: snapped before `PeerReady`, commanded after. `DoorLock.Lock` and `Unlock` gain defensive guards. DoorSync logs its panel and lock counts.
- **DESTRUCTION.**
  - `NetDetach` plays no crunch, and the client's fixture drop is silent: the host's Sound record already carries the sound (section 8).
  - A third `Detach` exit (null transform) also sends ChunkDetached, as vanished.
  - `GlassPane.NetApply` always makes 4 shards: copying the host's 3 to 5 would need a Random call.
  - The Structure snapshot also sends ModuleState for damaged walls that are not fractured.
  - Snapshot records are applied silently. GrenadeItem's `OnUseBegin` and `OnUseEnd` gain defensive gates.
- **GRANDMA.** Flags bit `SeatedBody` keeps the client's stand-in capsule on while she sits. The snapshot uses ops 9 and 10 (4.3). Mood is sent right before each GrandmaMoodChanged. `GrandmaMover.Teleport` calls `NetTransforms.Snap`.
- **GAMELOOP-TRUCK.**
  - Truck State also goes out at 1 Hz while the truck sleeps, so the final zero-speed sample arrives.
  - `TruckSync.SeatEpoch` is bumped only when a seat record is actually written. Otherwise a reload left the host's counter ahead, and the host dropped every client pose.
  - LedgerPut is also resent when an entry's Damaged value flips. The client sums the settlement total from the lines.
  - `breakInWhat` and `startedHow` are indexes into GameSession's word tables, which must follow `StartContract`'s strings.
  - The intro hold is `Net.IsHost && Net.PeerConnected && !Net.PeerReady`, so it cannot stick once the client has left.
- **UI-AUDIO.**
  - Online page rows: "Héberger", "Rejoindre", "Retour", then "Héberger en IP directe" (development builds only).
  - The join field takes focus when the keyboard was used last; a pad starts on "Coller".
  - `MainMenuModel.Leaving` is set only when the status reaches Loading, so a failure never leaves it set.
  - AudioSync: volume as unit8 of volume / 1.5, pitch as unit8 over 0.5 to 1.5. The pause menu hides its Layout row online.
- **Test kit.**
  - `-netbotpeerlog <host net log>` lets the client run check 6 against the host's `NETTEST POSE` lines.
  - The bot unlocks the house doors on the host at t = 1 s (test only, logged), puts the blast 1.5 m in front of P2, and teleports its own P2 when its straight-line walk is stuck (logged as INFO).
  - Check 8's side comes from the script: `host` / `client` (the host quits), `host-stay` / `client-quit` (the client quits).
  - Check 9 (bandwidth) is PASS / FAIL through NetStats peak and steady getters.
- **Outside the spec (fix a61fc32).** `MirrorSurface` falls back on `Sprites/Default` in a player build, which has no `Unlit/Texture` (37,000 exceptions per run before). The editor path is unchanged.

### 17.3 Test results
- **Compile.** `tools/net/compilecheck.sh` at 0 errors (runtime, then runtime + editor) on every track commit; the editor compile after the merges at 0 errors.
- **Offline smoke,** after the merges, split screen, Map01 for about 27 s: 2 players, `Net.Role` Offline, no `_Net`, no NetworkManager, 0 console errors, 0 warnings in Play. Solo P1, solo P2 and the existing CLIs were not run (16.8). It was not rerun after the last three fixes, which touch no offline path in the editor.
- **Loopback run 1, build against build, direct IP.** The first run failed on the digest and on MirrorSurface exceptions (fixes 5685ba3 and a61fc32). After the fixes: host 12 PASS / 0 FAIL, client 21 / 0.
  - Handshake: 541 ids, 126 tracked bodies, one digest; PeerReady 2.4 to 2.5 s after LoadScene; longest frame 42 ms on the host, under 1 s on the client.
  - Door, chair and pane resolved on the host from the client's aim; knockback 1.8 to 2.9 m in 0.5 s; 20 bodies at rest, worst 0.33 cm; reload to epoch 2 with a new snapshot; host killed: the client is back on the title with `net.hostLeft`.
  - Bandwidth: down peak 13 to 17 KB/s, steady 2 to 3 KB/s, up 0.7 to 1.4 KB/s. 0 exceptions in both player logs.
- **Client killed:** host 16 / 0 (the `net.partnerLeft` toast, P2 idle and grounded, the run continues); client 19 / 1, the pane flake in 17.5.
- **Relay, two builds on one PC:** the code was logged 2.7 s after start; host 12 / 0, client 21 / 0; RTT 70 to 90 ms; no payment asked.
- **Run 2, editor host against build client:** same digest and counts; host 12 / 0, client 20 / 1 (the same flake).

### 17.4 How to play
- **Same build on both PCs,** from the same commit and saved scene: the digest refuses a mismatch (`net.version`).
- **Build.** In the editor, outside Play and with no unsaved scene, run `BuildGreybox.NetClient()` (MCP RunCommand; there is no menu item) or File > Build Profiles (Windows, Development Build, `UnityProject/Build/Movers.exe`). Wait for `BUILD_RESULT=Succeeded`. Zip `UnityProject/Build` without `Movers_BackUpThisFolder_ButDontShipItWithYourGame` for Jonathan, or he builds the same commit.
- **Host (Pierre):** "Jouer en ligne", "Héberger", "Copier le code", send the code; at "Le joueur 2 est là !", "Lancer la partie". The editor can host too (same digest as the build, checked) if the build was made from the same saved scene.
- **Client (Jonathan):** "Jouer en ligne", "Rejoindre", Ctrl+V or "Coller", then "Se connecter" or Enter. The game loads when the host starts it.
- **If Relay fails:** "Héberger en IP directe" (development builds, UDP 7777 open on the host), over LAN or a free VPN such as Tailscale. The client types `<ip>` or `<ip>:7777`.
- **Command line,** from `UnityProject/`: host `Build/Movers.exe -nethost relay -netautostart -netlog Logs/net_host.log` (the code is on the `NETJOINCODE` line), client `Build/Movers.exe -netjoin <code> -netautostart`. Direct IP: `-nethost 7777` and `-netjoin 127.0.0.1`. Add `-netbot host` / `-netbot client` for the automated test (section 14).

### 17.5 Open
- **Not run:** a wrong code (`net.badCode`), a pulled cable, typing in the join page's TextField under the legacy Input Manager, a real pad through `LocalDevicesSource`, the truck driven by the client, and the two-PC run with Jonathan (60 fps on the host).
- **Bot flakes, not netcode faults** (the host logs show the client's aim applied exactly):
  - 3-pane-broken passed 2 runs of 5: the bot stops 2.6 m from the pane, so the carried chair has no run-up. Likely fix: `NetTestTargets.PaneStandOff` to about 3.4 m.
  - The straight-line walk gets stuck at the front door, the doorway and the chair on every run, then teleports.
- **Leftovers:** `PlayerSync.TryGetChaseYaw` is unused. DoorSync, TruckSync and NetTestBot add obsolete-API warnings (`FindObjectsSortMode`), like the existing code.

## Appendix A. Review log (2026-09-27)
Three reviews (desync, offline and code, API and feasibility) were checked against the sources. Accepted issues are fixed in the sections cited. Rejected or narrowed items, one line each:
- **Epoch filter drops LoadScene / Reload:** accepted (4.1, 3.6).
- **Hinge pivots and the ramp counted as Body:** accepted, Body defined (5.1, 6); the body count check replaces the reviewer's "about 127" with the counted 126 (112 + 12 + truck + car).
- **Stale poses on worn, pocketed and released items:** accepted (6); Snap also added to `Stow` and `Equip`.
- **CrewBumper stacking:** accepted with a cooldown of `RoundTrip + 0.2 s` instead of an impulse sequence and ack: same effect, no extra fields.
- **InputPose redundancy and hold time** (two reviewers): accepted, merged into one design: change list after the host's ack (Players InputAck), dtMs replay, 60 Hz (9.2).
- **Reload not idempotent:** accepted (3.6).
- **PuppetPose toggles the capsule:** accepted (9.3, 9.4).
- **Carried item stutter:** accepted (9.2 playout, 6 anchored interpolation).
- **Chase camera and exit yaw:** accepted (11.7).
- **Pause phantom Up:** accepted (9.2).
- **Rotate and wheel deltas:** accepted (9.2).
- **Knockdown direction:** accepted, fixed in the relay by writing Explosion first (4.2) instead of changing KnockdownTumble or the event payload.
- **Breakable record before the children:** accepted (11.5).
- **Own dragged body blocks the capsule:** accepted (6).
- **P1 grenade wind-up:** accepted (9.4, `throwHeld`).
- **Ready timeouts:** accepted (3.3).
- **Swapped ordinals in the digest:** accepted as hardening (5.5); no case exists in Map01 today.
- **Parallel tracks cannot compile or verify:** accepted (13.2).
- **Snapshot dropped before PeerReady:** accepted (`NetOut.InSnapshot`).
- **Cross-track stubs:** accepted (13.2, section 12).
- **Unowned new files:** accepted (NetPlayerDriver for PLAYERS; NetIdSweep and the apply components inside CORE files).
- **Static registration order and subscriptions:** accepted (section 12 rules, the `WorldEvents.Raise` line).
- **Offline baseline drift:** accepted (step 0). The editor version needs no settling: 6000.6.0f1 is the installed editor and CLAUDE.md's.
- **BuildGreybox and the MCP:** accepted (14). Narrowed: `Windows64()` is left unchanged (it is not used by this work), not given the new scene list.
- **Boom twice:** accepted (11.5).
- **F1 / F2 owned twice:** accepted, SliceDebug only; SplitScreen.cs leaves the PLAYERS edits.
- **NetSession created on first use:** accepted (3.2, section 12).
- **SceneFlow.ReloadGame listed as a caller:** accepted, removed; SceneFlow is not edited.
- **NetOut Begin / End fragility:** accepted (4.1).
- **Test framework too big:** accepted: no Python, no per-sync digest in the handover or the test, NetStats inside NetOut, the bot prints PASS / FAIL itself. Smoke, talk and truck stages are left to the manual run.
- **Client build not producible:** accepted (14).
- **NetworkManager config:** accepted (3.2).
- **Refuse lost at disconnect:** accepted, refusals ride the disconnect reason (3.4); op 3 is reserved.
- **Ledger and Settlement over the record cap:** accepted for the ledger (deltas). Narrowed for the settlement: one record per line with the host's strings, not item-id lists, because `Names` caps a detail at 5 names, a line fits in 406 B, and SettlementText already parses the English strings.
- **Editor host and build client may differ in ids:** accepted (run 1 is build versus build, `-netdumpids`).
- **Disconnect timeout too short:** accepted (default 30 s, longest-frame check). The claim that Relay drops peers silent for about 10 s is not verified in the package sources; the longest-frame check covers it either way.
- **RelayProtocol namespace:** accepted (3.1).
- **Relay error map and timeout detection:** accepted (3.4, `NetError.PaymentRequired`).
- **NGO lifecycle details:** accepted (3.2).
- **Hello build check never fires:** accepted. Narrowed: the stamp is `Application.buildGUID` (built into Unity, "editor" in the editor) instead of a Resources file written by BuildGreybox, which would add an asset to track.
- **Bandwidth budget counts payload only:** accepted (4.1 NetStats, 4.4); the far-body 10 Hz fallback is kept for when the measurement asks for it.
- **hostTime of throttled records:** accepted (4.1, Settle `sampleTime`).
- **Loopback gaps:** accepted (14, `-netbot`, `-netdumpids`, `Net.Scripted`).
