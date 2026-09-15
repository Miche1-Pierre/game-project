# PERFORMANCE TESTS

_Target: 60 fps on our machines in the worst realistic case._

## Method
- Define the worst-case scene (max players + max interactive objects + active VFX).
- Profile with the Unity Profiler; do not guess.
- Cap object and player counts explicitly if needed.
- Keep physics interactions bounded (risk R12).

## Rule
Profile before optimizing. Reduce systemic complexity before adding optimization layers.
