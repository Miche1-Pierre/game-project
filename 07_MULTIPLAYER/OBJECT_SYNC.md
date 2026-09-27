# OBJECT SYNC

_Syncing world objects, especially physics ones._

## Approach
- Host owns authoritative object state; clients interpolate.
- Do not sync every rigidbody; sync gameplay-relevant objects and let cosmetic physics run locally.
- Ownership transfer for carried / held objects (relevant if we do R.E.P.O.-style carrying).

## Rule
Physics sync is a top time-sink and perf risk (R2, R12). Keep synced object counts bounded.

**Update 2026-09-27 (ADR-011):** decided for the slice. There is no ownership transfer: the host owns every rigidbody, and the client draws kinematic copies from a 20 Hz transform stream. An item carried by the client's player is drawn relative to the client's own camera. Debris is simulated locally on each machine from replicated events. Ids come from the hierarchy after setup. See `NETCODE_SLICE.md` sections 5 and 6.
