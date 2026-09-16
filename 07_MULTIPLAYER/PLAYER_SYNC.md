# PLAYER SYNC

_Keeping players consistent across clients. Concept / player-count dependent; approach set now._

## Approach
- Host-authoritative model over Steam P2P.
- Sync player transform + core state; interpolate on remote clients.
- Send input / intent, not just positions, where it reduces bandwidth and cheating.
- Only sync what gameplay needs (`../03_TECHNICAL/PERFORMANCE.md`).

_Detailed once player count and the concept are fixed (`../00_PROJECT/OPEN_QUESTIONS.md` Q5)._
