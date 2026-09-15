# DISCONNECT

_Handling drops and rejoins gracefully._

## Approach
- A client dropping should not crash the session; the host continues.
- Clean up the dropped player's owned objects sensibly.
- Allow rejoin where the concept permits.
- If the host drops: for a small co-op game, ending the session cleanly is acceptable at first (host migration is optional, `HOST_MIGRATION.md`).

## Rule
Disconnect handling is part of a stable multiplayer build (M3). Test it (`../11_TESTING/MULTIPLAYER_TESTS.md`).
