# PERFORMANCE

_Target: 60 fps on our machines, worst realistic case. Test method: `../11_TESTING/PERFORMANCE_TESTS.md`._

## Guidelines
- Profile before optimizing (Unity Profiler), never guess.
- Bound rigidbody, particle and NPC counts; pool objects; sleep and cull aggressively.
- Batch draw calls (static and dynamic batching, GPU instancing, few materials, per `../05_ART/`).
- Avoid per-frame allocations; watch the GC.
- Do not sync unnecessary network state (`../07_MULTIPLAYER/`).

## Rule
Reduce systemic complexity before adding optimization layers (risk R12).
