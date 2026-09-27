# DISCONNECT

_Handling drops and rejoins gracefully._

## Approach
- A client dropping should not crash the session; the host continues.
- Clean up the dropped player's owned objects sensibly.
- Allow rejoin where the concept permits.
- If the host drops: for a small co-op game, ending the session cleanly is acceptable at first (host migration is optional, `HOST_MIGRATION.md`).

## Rule
Disconnect handling is part of a stable multiplayer build (M3). Test it (`../11_TESTING/MULTIPLAYER_TESTS.md`).

**Update 2026-09-27 (ADR-011):** decided for the slice. A client that drops or leaves: the host continues, P2 releases what it held (and the truck seat, if driving) and stays as an idle body under gravity. The host that drops or leaves: the run ends and the client returns to the title with a message. No rejoin and no host migration. Tested in the loopback run. See `NETCODE_SLICE.md` sections 3.5 and 16.
