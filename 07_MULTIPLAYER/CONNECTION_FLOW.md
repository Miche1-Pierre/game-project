# CONNECTION FLOW

_The path from menu to in-game._

## Flow
Main menu -> create or join (Steam lobby) -> lobby (ready-up) -> load game scene -> play -> return to lobby / menu.

## Notes
- Handle join-in-progress if the concept allows it (`DISCONNECT.md`).
- Keep the flow short; friends want to be in a match fast.

**Update 2026-09-27 (ADR-012):** for the slice the flow is menu, host (Relay join code) or join (code or IP), a lobby of two, load, snapshot, play, replay for both, leave. There is no Steam lobby yet and no join-in-progress. See `NETCODE_SLICE.md` section 3.
