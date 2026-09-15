# PERFORMANCE (networking)

_Keeping the netcode cheap. General perf: `../03_TECHNICAL/PERFORMANCE.md`._

## Guidelines
- Sync the minimum state at the lowest acceptable rate; interpolate.
- Avoid per-frame full-state sends; use deltas and events.
- Bound synced object counts (`OBJECT_SYNC.md`).
- Test under simulated latency and loss (`NETWORK_TESTING.md`).
