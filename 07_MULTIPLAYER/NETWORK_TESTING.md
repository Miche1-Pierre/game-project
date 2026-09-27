# NETWORK TESTING

_How we test the netcode. Overlaps `../11_TESTING/NETWORK_TESTS.md`._

## Steps
- Local two-instance, then two PCs on LAN, then Steam P2P.
- Simulate latency and packet loss.
- Test join, leave, rejoin, and host drop.
- Check sync of positions, objects, score, and any hidden state.

## Rule
Test multiplayer early, not at the end (risk R2).

**Update 2026-09-27 (ADR-012):** for the slice, two builds on one PC run an automated test against each other (`-netbot host` / `-netbot client`, over direct IP or Relay), and it prints PASS / FAIL lines to a net log: handshake, actions from the client's aim, knockback, transforms at rest, reload, disconnect both ways, bandwidth. Latency and loss simulation and the two-PC run are not done yet. See `NETCODE_SLICE.md` sections 14 and 17.3.
