# TEST STRATEGY

_How we verify the game. Lean, greybox-first, observation over opinion._

## Principles
- The main test is the **playtest**, not the unit test. See `../playtests/`.
- Observation over opinion: what players DO beats what they SAY.
- Funny vs destructive bugs are handled differently (`BUG_POLICY.md`).
- Test multiplayer early, not at the end (risk R2).

## Layers
1. **Manual playtest** (every gameplay change): is it fun, is it readable, do they replay?
2. **Unit tests** (`UNIT_TESTS.md`): pure logic only (rules, scoring, RNG).
3. **Multiplayer tests** (`MULTIPLAYER_TESTS.md`): sync, join/leave.
4. **Performance** (`PERFORMANCE_TESTS.md`): worst-case scene at 60 fps.
5. **Pre-build smoke** (`REGRESSION.md`): a short checklist before each build.

## Rule
No new gameplay system is "done" without a playtest observation logged and a Unity console with zero errors (`../04_PRODUCTION/DEFINITION_OF_DONE.md`).
