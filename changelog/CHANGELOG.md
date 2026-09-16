# CHANGELOG

_Not just code. Categories: DESIGN, TECH, RESEARCH, DOCS, CONTENT, BALANCE, BUSINESS, MARKETING._

## 2026-09-16

### Benchmark pass: "vibe coded" hits (branch `strat-jo`)
Studied two 2026 games reputed to be AI-built, Meccha Chameleon and Dear Passengers. Neither is. Both are made by experienced teams, and Steam's January 2026 rules put AI code assistants outside disclosure entirely.

- **RESEARCH:** two new `GAME_ANALYSIS` fiches. Meccha Chameleon (Unreal 5 + Epic Online Services, 2 people, 2 months, 20 M+ copies, 340 k peak CCU, zero ad spend, zero server cost, shipped executable still named `PenguinHotel.exe`). Dear Passengers (Unity, FLEXUS 70+ staff, unreleased, 2 M+ wishlists, netcode and player count never stated).
- **RESEARCH:** filled `VIRALITY_PATTERNS` (9 distribution patterns, the reveal artefact as the whole campaign, the anti-trailer, the beta as seeding, wishlist softness, instant cloning).
- **RESEARCH:** `SUCCESS_PATTERNS` gained K (reusable capital beats a lucky concept) and L (the cheapest asset is sometimes the mechanic). `DIFFERENTIATORS` gained BLEND. `MARKET_MAP` gained two rows and lost the absolute "Unity everywhere" claim.
- **TECH:** networking default direction moved from Steam Networking to Epic Online Services via its official Unity plugin, free at any scale. Rationale is cost: per-CCU pricing at the scale of the games we study would exceed our entire budget many times over.
- **DECISION:** ADR-003 written as **Proposed, not accepted**. Blocked on Q5 (player count) and Q9 (minimum architecture), both Jonathan-tagged.
- **TECH:** `TECH_STACK` gained a note on Valve's January 2026 AI disclosure rules. Code assistants are out of scope; shipped generated assets are not.
- **BUSINESS:** filled `PRICING` with the corpus price table and the wishlist conversion data. Working band $6 to $9, not a decision.
- **MARKETING:** filled `WISHLIST_STRATEGY` with the reveal sequencing and the forecasting rule.
- **DOCS:** `RISKS` gained R17 to R20 (instant cloning, netcode cost scaling with success, wishlist-based revenue forecasting, revealing before the build can follow up). `OPEN_QUESTIONS` gained Q22 to Q25. `RESEARCH_SOURCES` gained 21 sources.
- **DOCS:** `PROJECT_STATE` updated, including the unresolved tension between ADR-002 (spec first) and the build-first method used by the team we studied. Flagged, not decided.

## 2026-09-15

### Repository setup
- **TECH:** created the decision repository `C:\GameProject` (full structure, `CLAUDE.md`, `PROJECT_STATE.md`), pushed to private GitHub `Miche1-Pierre/game-project`.
- **RESEARCH:** benchmark "Atlas Coop Viral" transferred into `01_RESEARCH/` (14 game fiches, mechanics matrix, patterns, design space, white spaces, sources).
- **TECH:** Unity environment verified (CLI beta.8, editor 6.6.0f1, Personal license, URP template). Unity project NOT created, by decision (ADR-002).
- **DECISION:** ADR-001 (Unity 6), ADR-002 (spec first).

### Guided documentation pass (with Pierre)
- **DOCS:** filled `OBJECTIVES` (3 priorities, ~2-week sprint, ~140 h budget, milestones), `VISION` (experience-first, streamer is a property not the goal), `OPEN_QUESTIONS` (21 questions, Jonathan-tagged), `RISKS` (16 risks with Probability/Impact/Mitigation/Trigger, incl. "fun but not commercially compelling").
- **BUSINESS:** filled `TEAM_AGREEMENT` (Pierre's proposed 50/50 positions, pending team discussion, plus mandatory "developer leaving" and "project accounts" discussions) with `IP_OWNERSHIP`/`PROFIT_SHARING`/`LEGAL_ENTITY`/`TAXES` as pointers; `COST_MODEL` (comfortable envelope ~350 to 500 EUR one-time, ceiling ~750; Claude Max x5 at 108 EUR/month recurring).
- **ART:** filled `ART_DIRECTION` and `STYLE_GUIDE` (silhouette-first, few materials, minimal rigging, cheap animation; low-poly default, not locked; Jusant / Bruno Simon as quality references).
- **DESIGN:** neutralized `GAME_CONCEPT` so the 5 candidates stay unranked until the brainstorm.

### English standardization + game analyses
- **DOCS:** whole repository standardized to English. CLAUDE.md gained the streamer guardrail (clip potential is a property, not the goal); ROADMAP gained the M0-M4 timeline; CONSTRAINTS gained the time budget.
- **RESEARCH:** the 14 `GAME_ANALYSIS` fiches completed (hook, signature verb, fantasy, core loop, central rule, signature mechanic, social loop, fun/tension/replayability, content model, differentiator, and the takeaway for our game).
