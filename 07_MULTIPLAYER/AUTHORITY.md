# AUTHORITY

_Who decides what is true._

## Position
- **Host-authoritative** by default (simplest for a small co-op game over Steam P2P).
- The host resolves gameplay outcomes; clients predict and reconcile where needed.
- No dedicated server, no authoritative backend, unless a concept forces it (ADR + cost).

_Authority model confirmed with the concept and player count._

**Update 2026-09-27 (ADR-011):** decided for the slice. The host simulates everything. The client owns only its own body's movement and look, and streams its pose with its input. The client does no prediction beyond that. See `NETCODE_SLICE.md` sections 2 and 9.
