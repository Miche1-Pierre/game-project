# PROJECT STATE

_Living dashboard. The agent reads it at start and updates it at end of session. Last update: 2026-09-16._

**PHASE:** Concept selection

**CONCEPT:** TBD (5 candidates, see `02_GAME_DESIGN/GAME_CONCEPT.md`)
**CORE LOOP:** TBD

**CURRENT HYPOTHESES:**
- H1: social friction readable in under 10 s is the real engine, to exploit as explicit intent.
- H2: the clip as an output of the loop is a white space (only Content Warning exploits it).
- H3: making the players the detection system (not an NPC AI) is lighter and more emergent.
- H4 (new): reusable capital, not concept quality, is what makes a 2-month build possible. See `01_RESEARCH/SUCCESS_PATTERNS.md` pattern K.
- H5 (new): the reveal artefact is the entire marketing campaign, and third parties distribute it. See `01_RESEARCH/VIRALITY_PATTERNS.md`.

**VALIDATED:** nothing yet, no build exists
**INVALIDATED:** one corpus assumption. "Unity everywhere" is no longer strictly true: the largest 2026 hit is Unreal 5. Our Unity choice stands (ADR-001), but it is a preference, not a benchmark finding.
**RUNNING EXPERIMENT:** none
**CURRENT BUILD:** none (Unity not created yet, by decision)

**NEXT DECISION:** choose the concept to prototype (write an ADR)

**BLOCKERS:** none technically. 8 questions tagged `[JONATHAN]` block the concept, the player count, the team agreement and now the netcode direction.

**SCOPE:** research done, decision structure in place; guided doc pass done; Sept. 16 benchmark pass done (2 new corpus fiches, virality patterns, pricing, wishlist strategy, netcode costing)
**TECH:** Unity 6.6.0f1 + URP + MCP ready to wire, project not created. Networking default direction moved from Steam Networking to Epic Online Services, free at any scale (ADR-003, **proposed, not accepted**)
**BUSINESS:** team agreement and costs drafted (Pierre's positions, pending Jonathan); price band benchmarked at $6 to $9 (`08_BUSINESS/PRICING.md`), not decided
**STEAM:** strategy set, nothing created. Reveal deliberately not scheduled (see R20)

**NEXT 3 ACTIONS:**
1. Concept brainstorm with Spykernv (start from the mechanics learned, not "let's make the office game"). Add Q22 to the agenda: what reusable base do we build regardless of the concept.
2. Lock 1 (or 2) concept(s), then fill GAME_CONCEPT / CORE_LOOP / GAME_RULES / MECHANICS / SYSTEMS.
3. Write GREYBOX_SPEC, then only then create Unity.

## What the September 16 benchmark pass changed
Studied Meccha Chameleon (20 M+ copies, 2 people, 2 months) and Dear Passengers (2 M+ wishlists, unreleased), both reputed to be "vibe coded". Neither is. Full fiches in `01_RESEARCH/GAME_ANALYSIS/`.

- **The "2 people in 2 months" story is real but incomplete.** It is the authors' 7th Steam game, their previous one sold about 5 000 copies, and the shipped executable is still named after the game they reused. Speed came from owned capital, not from AI or from luck.
- **Neither game is AI-built.** Dear Passengers is made by a 70-person studio that wrote its own scripting language for Unity. Steam's January 2026 rules exempt AI code assistants from disclosure entirely, so "vibe coded" is not even a visible category on the store.
- **Netcode is a commercial decision, not a technical one.** Per-CCU pricing is an uncapped bill that arrives exactly when the game works. This produced ADR-003 and risk R18.
- **Distribution is a design problem.** The wishlist explosion we studied came from one trailer the studio did not distribute, amplified by third parties, with 435 followers to its name. This produced `01_RESEARCH/VIRALITY_PATTERNS.md` and `10_MARKETING/WISHLIST_STRATEGY.md`.
- **Four new risks:** R17 instant cloning, R18 netcode cost, R19 wishlist-based forecasting, R20 revealing before the build can follow up.
- **Four new open questions:** Q22 reusable capital, Q23 AI content disclosure, Q24 paid netcode during the prototype, Q25 reveal timing.

**Tension to resolve, not resolved here.** ADR-002 forbids Unity before the greybox spec exists. The team we studied did the opposite, building the whole thing with minimum systems and graphics first, then adding. Both approaches are defensible. ADR-002 already carries a revisit clause on analysis paralysis. Flagging it rather than deciding alone, per CLAUDE.md rule 16.
