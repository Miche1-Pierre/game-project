# OBJECT SYNC

_Syncing world objects, especially physics ones._

## Approach
- Host owns authoritative object state; clients interpolate.
- Do not sync every rigidbody; sync gameplay-relevant objects and let cosmetic physics run locally.
- Ownership transfer for carried / held objects (relevant if we do R.E.P.O.-style carrying).

## Rule
Physics sync is a top time-sink and perf risk (R2, R12). Keep synced object counts bounded.
