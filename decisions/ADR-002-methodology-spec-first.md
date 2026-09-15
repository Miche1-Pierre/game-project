# ADR-002: Method, spec first, greybox next

## Status
Accepted (2026-09-15)

## Context
Temptation to create the Unity project immediately. The team wants to freeze the method and the concept first.

## Decision
1. Specify everything (decision repository + greybox spec) BEFORE creating Unity.
2. Greybox in grey boxes, then playtest, then go/no-go, then only the visual production.
3. Optimize for learning speed before development volume.

## Why
- If it is bad in greybox, art will not save it.
- A team of two cannot afford content before the fun is proven.
- The concept must emerge from research, not from a hypothesis frozen in advance.

## Consequences
- No Unity code until `02_GAME_DESIGN/GREYBOX_SPEC.md` exists.
- Every structural decision becomes an ADR; every tested hypothesis becomes an `experiments/` entry.

## Revisit if
The spec phase becomes a brake that prevents learning (analysis paralysis): switch to prototyping faster.
