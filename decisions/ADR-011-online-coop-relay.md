# ADR-011: Online co-op for the slice, over Unity Relay

## Status
**Accepted (2026-09-27), Pierre's decision. Revised the same day after three reviews of the spec (NETCODE_SLICE appendix A).**
- **Pierre's answers, the same day (NETCODE_SLICE section 16):** the run-control defaults are kept, the gamepad works online (keyboard, mouse and first pad on each PC), step 0 is done in 1bd50e5, and `ProjectVersion.txt` is never committed.
- **Tested 2026-09-27 (NETCODE_SLICE 17.3):** two builds on one PC pass the automated loopback test over direct IP and over Relay (host 12 of 12, client 21 of 21; one known bot flake on the pane throw), at 2 to 3 KB/s steady and 70 to 90 ms RTT over Relay, and Relay asked for no payment. The offline smoke is clean. Not yet played from two homes.
- It is an exception to CLAUDE.md section 12 ("no final netcode before the gameplay and player count are frozen; greybox in hot-seat or local").
- It is also an exception to `07_MULTIPLAYER/NETWORK_ARCHITECTURE.md`, section Greybox ("do not integrate any networking SDK before the loop is proven").
- It amends `02_GAME_DESIGN/GREYBOX_SPEC.md`: networking leaves the out-of-scope list for `Map01_PierreKit_House` only, and its multiplayer validation order gains "two PCs over Relay" before Steam.
- ADR-004 is unchanged: free solutions only, host is a player, no backend, Steam at launch.

To be read by Spykernv.

## Context
- **The go / no-go needs two people** (ADR-009, PROJECT_STATE): spontaneous laughter cannot be tested solo. Pierre wants to play the slice with Jonathan, who is not in the same place. Local split screen (ADR-009) cannot reach him.
- **Pierre's plan:** Unity 6, Netcode for GameObjects, Unity Transport, Multiplayer Services Relay; a host and a join code; no port forwarding.
- **Constraint:** "rien de payant" (Pierre, 2026-09-27).
- **Already done:** the packages are installed (NGO 2.13.3, Multiplayer Services 2.3.3, commit c0bd6b0), and the project is linked to Unity Cloud (commit 86faae5).
- **The slice is one machine's simulation today:**
  - two local players;
  - about 125 physics items;
  - structural destruction;
  - an autonomous NPC;
  - a drivable truck;
  - an event bus.
  Physics synchronisation is the known hardest problem (RISKS R12).

## Options
- **A. Steam Networking now.** Free, and the launch target. It needs Steamworks integration and an App ID, and ADR-004 places it last and outside the greybox.
- **B. NGO with Unity Relay, host-authoritative, custom messages.** Free tier. No port forwarding. The packages are installed. The transport can be swapped later.
- **C. NGO with NetworkObjects and NetworkTransforms on every body.** The idiomatic NGO path, but it means prefabs and scene objects rebuilt around NGO components, ownership rules for about 700 objects, and NGO types in every gameplay file.
- **D. Screen streaming (one PC, remote play).** Not Pierre's plan, and the remote player feels the stream latency on every action, movement included.
- **E. Direct IP only (port forwarding or a VPN).** Free, but asks each player to configure a router or install a VPN.

## Decision
**Option B, with E kept as a built-in fallback and test path.**
1. **Host-authoritative, host is a player, 2 players online** (host = P1, client = P2). Each PC renders only its own player.
2. **The client simulates only its own body** (movement and look), and streams its pose with its input. Everything else runs on the host from the forwarded buttons: grab, throw, interact, pockets, smoke, drink, grenades, drive.
3. **NGO is used for connection and two named messages only.** No NetworkObjects, no scene management, no player prefab. Game traffic is Movers-owned records in a Movers-owned byte format. Only one file (`Net/NetSession.cs`) references NGO or Unity Services.
4. **World replication.**
   - Stable ids from the hierarchy after setup.
   - A 20 Hz transform stream to kinematic replicas.
   - A reliable ordered state stream with a full snapshot on join.
   - Forwarded WorldEvents and FX.
   - Debris and particles simulated locally on each machine.
5. **Connection:**
   - by Relay join code (anonymous sign-in, Relay free tier, DTLS);
   - or by direct IP, for LAN, VPN and the automated loopback test.
   - Relay failures are named on the menu, including "payment required", which is this ADR's stop condition. Direct IP never touches Unity services.
6. **Offline play is unchanged:** it behaves as a recorded baseline commit (NETCODE_SLICE section 13.2, step 0), no net object and no extra event listener exists offline, and every gate is a no-op there.
7. **How it is built.** A CORE track writes the Net core and compiling stubs of every cross-track member first. Six tracks then work in parallel in separate git worktrees, proving their compile out of the editor. Only the INTEGRATION track drives the Unity editor, one merge at a time, because one editor is shared with Pierre.

The build contract is `07_MULTIPLAYER/NETCODE_SLICE.md`.

## Why
- **It is the cheapest way to get the only test that matters:** two people laughing, from two homes.
- **Relay removes the router and firewall problem** with no server of our own. Below its free tier it costs nothing, and 2 players at a few KB/s are far below it.
- **Host authority fits the physics.** One simulation owns every rigidbody, and the client draws kinematic copies. There is no ownership transfer and no two-physics-world desync, which is the problem R.E.P.O. and Dear Passengers named.
- **Custom messages over NetworkObjects** keep the slice's 49k lines intact. The gameplay code gains gates and small replica methods, not a rewrite. The transport stays swappable: at launch the same records can ride Steam networking (ADR-004), with only `NetSession` rewritten.

## Consequences
- **Scope grows openly.**
  - About 85 existing files gain gates or replica methods.
  - About 25 new files (including the stubs), in 8 tracks (NETCODE_SLICE section 13).
  - This is the risk R2 names: networking eating the schedule. The limit is the slice itself: no new mechanic is built for online.
- **Latency on the client's own actions.**
  - Grab, throw, pocket, open and drive happen about one round trip plus a 35 ms playout buffer late, typically 80 to 170 ms over Relay. The buffer keeps button timing (tap versus hold) and aim intact through packet loss.
  - Driving from the client adds about 150 to 250 ms.
  - Movement and look stay instant.
- **The two screens are not identical.** Debris, smoke clouds and splats differ, and heavy rubble crushes only on the host. The replicated gameplay state (who holds what, what is broken, doors, the grandmother, the run) is identical.
- **No join mid-run, no reconnect, no host migration.** If the host leaves, the run ends for the client (ADR-004 already accepted this).
- **Both players must run the same build.** Pierre edits Map01 often. An id digest in the handshake refuses a mismatch instead of desyncing.
- **Offline must keep passing its existing tests,** against the baseline commit. INTEGRATION proves it after each track merge; tracks prove their compile only.
- **A baseline cleanup comes first:** the Input System package that arrived with the Convai import is removed and legacy input restored (step 0, done in 1bd50e5).
- **The automated test runs two builds against each other first** (loopback, direct IP), because the editor and a player build can disagree on setup details that change the ids.
- **The Relay dashboard** must be enabled by Pierre for the linked project (it is, 2026-09-27). The code shows a clear error when it is not, and direct IP still works.

## Revisit if
- **The Unity dashboard asks for a payment method** to use Relay (the menu shows `net.payment`), or the free tier changes. Then play by direct IP over a free VPN, and move to Steam networking sooner.
- **The first online session shows the client's latency spoiling the carry or the truck.** Then predict the client's own grab and carry locally, or give the driver's machine the truck.
- **Networking work starts delaying gameplay iteration** (R2 trigger): freeze online at what works and go back to local split screen for design work.
- **We move to Steam at launch.** Then replace `NetSession`'s transport, keep the records, and write the launch ADR that ADR-004 asks for.
- **Player count online must go above 2.** The record format allows it, but player addressing, views and input routing assume one remote player.

## Related
- `07_MULTIPLAYER/NETCODE_SLICE.md` (the spec)
- ADR-004 (free netcode), ADR-009 (the slice, two local players)
- `03_TECHNICAL/SLICE_ARCHITECTURE.md`
- `00_PROJECT/RISKS.md` R2, R12, R18
