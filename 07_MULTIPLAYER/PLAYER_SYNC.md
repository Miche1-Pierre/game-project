# PLAYER SYNC

_Keeping players consistent across clients. Concept / player-count dependent; approach set now._

## Approach
- Host-authoritative model over Steam P2P.
- Sync player transform + core state; interpolate on remote clients.
- Send input / intent, not just positions, where it reduces bandwidth and cheating.
- Only sync what gameplay needs (`../03_TECHNICAL/PERFORMANCE.md`).

_Detailed once player count and the concept are fixed (`../00_PROJECT/OPEN_QUESTIONS.md` Q5)._

**Update 2026-09-27 (ADR-012):** decided for the slice (2 players online). The client sends its input frames, with loss-proof button edges, together with its pose. The host poses its copy of P2 from that pose before any ray. P1 is an interpolated puppet on the client. See `NETCODE_SLICE.md` section 9.
