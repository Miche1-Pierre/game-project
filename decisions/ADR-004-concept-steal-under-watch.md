# ADR-004: Concept, steal under watch

## Status
**Accepted (2026-09-16).** Decided by both developers across two concept meetings.

## Context
`02_GAME_DESIGN/GAME_CONCEPT.md` held five unranked candidates from the research phase, and stated that a new idea raised at the brainstorm would be equally valid. The brainstorm produced one.

The project had no concept, no core loop and no greybox spec, and `ADR-002` blocks Unity creation until the spec exists. This decision unblocks the project.

## Options
- **A to E.** The five researched candidates: office under surveillance, competence comedy, deduction through work, hidden threat, the talkative sim.
- **F.** A new idea from the brainstorm: a moving crew that empties a house while the owner is present, and steals what it can.

## Decision
**Option F.** The signature verb is **steal under watch**, not carry.

Moving is the fiction that puts the players in the house with a legitimate reason to touch everything. The game is the theft under observation. The first meeting produced the setting, the second meeting made the verb explicit and chose it over transport.

Four players maximum, solo playable. First map, the grandmother's house, which is also the tutorial.

## Why
- **It answers the differentiation problem the transport framing did not.** In R.E.P.O. the antagonist is the object. Here the antagonist is the person who let you in. Nothing in the corpus occupies that space (`01_RESEARCH/DIFFERENTIATORS.md`).
- **The contract and the theft share the same verbs.** No mode switch, no steal button. That is a cheap system with a large output.
- **The setting needs no explanation.** Everyone understands a moving job and a house.
- **It is producible.** One handcrafted house, about 80 objects, one walking NPC. No procedural generation and no detection AI in the first build.

## Consequences
Positive: a clear verb, a differentiated position, a greybox that fits one map, and a tone that the team finds genuinely funny, which matters for a two-person project.

Negative and to be managed:
- **The reason to buy is not written.** Deferred by decision until the greybox is playable. `RISKS.md` R6 stays active until the sentence exists without naming another game.
- **There is no fallback.** The five candidates are no longer the natural plan B, and the team decided not to define one. The stated reason is that the project is also for fun (`OPEN_QUESTIONS.md` Q17).
- **Hypothesis H3 is retired, not invalidated.** It compared player-driven detection against NPC AI. The chosen concept has the players on the same side, so the comparison does not apply.
- The full vigilance system, which is the expensive part of the fantasy, is deferred to V2 and is not proven.

## Revisit if
- The greybox produces no spontaneous laughter. That is the stated go / no-go.
- The reason-to-buy sentence still cannot be written after the greybox without naming another game.
- Theft turns out to be unreadable in play, in which case the verb collapses back to transport and the concept loses its differentiator.

## Related
`02_GAME_DESIGN/GAME_CONCEPT.md`, `CORE_LOOP.md`, `GREYBOX_SPEC.md`, `ADR-002`, `ADR-003`.
