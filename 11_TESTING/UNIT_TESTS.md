# UNIT TESTS

_Kept deliberately light. We unit-test logic, not the engine._

## Test
- Pure C# rules: scoring, win/lose conditions, quota/economy math, seeded RNG determinism, state machines.
- Tooling: Unity Test Framework (EditMode) for pure logic; PlayMode only when a system needs the runtime.

## Do not test
- Visuals, animation, feel, MonoBehaviour glue, anything better judged by a playtest.

## Rule
Write a unit test when a rule is subtle enough that a silent regression would not be obvious in a playtest. Otherwise rely on playtests.
