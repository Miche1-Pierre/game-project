# PHYSICS

_Unity PhysX. Bounded and readable, since physics may be a core comedy source._

## Approach
- Unity 3D physics (PhysX), fixed timestep tuned for stability.
- Keep interactive rigidbody counts bounded (risk R12); pool and sleep aggressively.
- For multiplayer, prefer host-authoritative physics with client interpolation; avoid syncing every rigidbody (`../07_MULTIPLAYER/OBJECT_SYNC.md`).
- Layer-based collision matrix to keep interactions cheap and intentional.

## Rule
Physics is a situation multiplier, not a simulation showcase (benchmark Pattern D). Tune for funny and readable, not realistic.

_Detailed once the concept says how central physics is._
