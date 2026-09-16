# C# ARCHITECTURE

_Conventions. Enterprise-grade but lean: readable, testable, no ceremony for its own sake._

## Style
- Standard C# / Unity conventions, nullable-aware where it helps.
- Small classes, single responsibility, composition over inheritance.
- Plain C# classes for logic (unit-testable), MonoBehaviours only for engine glue.
- Data in ScriptableObjects; behavior in components; wiring in the scene or a bootstrapper.

## Patterns we use
- **Events** for decoupling (`EVENT_SYSTEM.md`), not tangled references.
- **Lightweight DI or a service locator** for cross-cutting services (input, audio, net).
- **State machines** for player / NPC / game states.

## Patterns we avoid
- Deep inheritance trees, premature abstraction, giant "manager" god-objects, static mutable state.

## Rule
A system a teammate cannot confidently modify or debug is a liability (risk R7). Keep it small and explained.
