# CHANGELOG

_Not just code. Categories: DESIGN, TECH, RESEARCH, DOCS, CONTENT, BALANCE, BUSINESS, MARKETING._

## 2026-09-17

### Merge of `strat-jo` into the main line
Branch `strat-jo` (benchmark research and the concept-meeting decisions) merged with `main` (deep doc fill, concept lock, and the first working Unity greybox). Both sides preserved.

- **MERGE:** `UnityProject/` taken from `main` untouched. Tutorial_01 runs: grab, carry, truck loading, contract, HUD.
- **MERGE:** ADR numbering reconciled. `main` had taken ADR-003 for the concept, so the netcode ADR was renumbered to **ADR-004**. The duplicate concept ADR written on `strat-jo` was removed in favour of `main`'s ADR-003.
- **MERGE:** the two Meccha Chameleon fiches combined. `main`'s design analysis kept, engine corrected from unknown to Unreal Engine 5, and the verified technical and production facts added.
- **MERGE:** `VIRALITY_PATTERNS` now has both halves, the design properties from `main` and the distribution mechanics from `strat-jo`.
- **MERGE:** `MARKET_MAP` carries Megabonk, Meccha Chameleon and Dear Passengers.
- **DESIGN, FLAGGED NOT DECIDED:** `GAME_CONCEPT` gained an **open divergence** section. ADR-003 says the verb is MOVE / CARRY and that theft must not become the core. The later concept meeting, with both developers, recorded the verb as STEAL UNDER WATCH. The two readings produce different games. Proposed tie-breaker: play Tutorial_01 and see whether carrying alone already generates stories.
- **DESIGN:** `GREYBOX_SPEC` keeps `main`'s Tutorial_01 as the current build, with the larger meeting scope recorded below it and explicitly held until the verb question is settled.
- **DESIGN:** `CORE_LOOP` filled against the current concept, with the alternative reading documented.

### Concept meeting decisions (branch `strat-jo`)
Two concept meetings on 16 and 17 September resolved 22 of 29 open questions, declined 5, and left 2 open. The project moved from "no concept" to "ready to build".

- **DECISION:** the concept meeting recorded the signature verb as **steal under watch**, not carry. On merge this became the open divergence above rather than an accepted ADR, because `main` had already locked MOVE / CARRY in ADR-003.
- **DECISION:** ADR-003 rewritten and **accepted**, renamed to `ADR-004-netcode-free-only`. Free solutions only, Steam or Unity Netcode, host is a player, no backend, 4 players. Photon excluded. The earlier Epic Online Services proposal is demoted to a documented alternative.
- **DESIGN:** `GAME_CONCEPT` rewritten, the five candidates moved to an archive section. `CORE_LOOP` filled, including the two-ledger idea: the contract and the theft use the same verbs.
- **DESIGN:** a fuller greybox scope was specified. Scope: grandmother's house, 10 rooms, about 80 objects, cat and fish as living objects, one NPC with a single interaction, A* patrol only, static truck, no shop, no procedural generation, no detection AI.
- **DESIGN:** delegated decision taken, garage over barn, with reasons recorded in the spec.
- **PRODUCTION:** `MILESTONES` filled with the team's own plan. Greybox, V1, one month of communication in parallel with map production, beta. Earlier agent-produced duration estimates were rejected as unreliable and removed rather than argued.
- **MARKETING:** `TARGET_AUDIENCE` filled. Casual evening-with-friends audience, groups of four, discovery through French-speaking streamers first, purchase driven by trend effect, non-evergreen by design.
- **MARKETING:** `WISHLIST_STRATEGY` sequencing replaced by the team's real plan, triggered by the tutorial working end to end in V1.
- **BUSINESS:** `PRICING` updated. Range $4.99 to $14.99, volume over margin, settled by the final look and the wishlist curve. Caution recorded that the upper end and the volume strategy pull in opposite directions.
- **TESTING:** `PLAYTESTS` gained the external protocol, 16 players in 4 teams of 4, with the two stranger teams as the real signal.
- **ART:** `ASSET_STATUS` gained the pipeline decision. Free packs as the base, completed by AI 3D generation. Steam disclosure accepted as a consequence.
- **DOCS:** `RISKS` gained R21 (the reason to buy is unwritten) and R22 (unsigned team agreement), and R18 marked largely closed by ADR-004.
- **DOCS:** H3 retired from `PROJECT_STATE`, H6 added. `OPEN_QUESTIONS` and `QUESTIONS_RESTANTES` rewritten as a decision journal plus a 2-item open list.

**Still open:** the reason to buy (Q2, deferred to just after the greybox) and the commercial change-of-direction thresholds (Q21). Five team questions declined deliberately.

## 2026-09-16

### Concept locked and first Unity greybox (branch `main`)
- **DECISION:** ADR-003, concept accepted. **The Movers**, a 1-4 player co-op physical moving game. Verb MOVE / CARRY.
- **DESIGN:** `GAME_CONCEPT` and `GREYBOX_SPEC` written for Tutorial_01.
- **TECH:** Unity project created (6000.6.0f1). `GreyboxBootstrap`, `PlayerController`, `PlayerGrab`, `MovableObject`, `TruckCargo`, `GameHUD`. Grab, carry, truck loading, contract and delivery work. Primitives only, single player, no networking.
- **TECH:** greybox rebuilt as visible editor objects rather than runtime-only, plus the Tutorial_01 scene and greybox materials.


### Deep fill pass across folders (01, 03 to 13)
- **RESEARCH:** added `DEVELOPMENT_CONTEXT.md` (team size, dev time, background per game) and two games (Meccha Chameleon, Megabonk); filled the virality / social / replayability pattern files.
- **TECH:** architecture, C# conventions, data (no database), save (minimal), input, audio system, physics, event system, object interaction, performance, build, debugging, third-party. OOP + modular + config-driven + no backend by default. Gameplay/detection/AI get principles pending the concept.
- **ART:** environment, characters, props, materials, textures, lighting, VFX, animation, specs (low-poly, free-first, lighting-driven, minimal rigging).
- **AUDIO:** direction, sound design, music, voice, ambience, asset list (free / open-source SFX, minimal music).
- **PRODUCTION:** milestones, tasks, backlog, priorities, content/asset/build pipelines, readiness checklist.
- **MULTIPLAYER:** host-authoritative Steam P2P approach across sync, authority, lobby, connection, disconnect, host migration (deferred), voice, cheating (minimal), performance, testing.
- **BUSINESS:** model, pricing, budget, revenue, third-party costs (premium one-time, 50/50, EU selling worldwide).
- **STEAM:** setup, store page, capsules, screenshots, trailer, playtest, wishlists, checklist.
- **MARKETING:** competitors (from the benchmark), content / creator / social / trailer / clip / wishlist / launch strategy. Positioning, USP and audience kept light pending the concept.
- **RELEASE:** plan, build, QA, submission, launch, creator outreach, support, incident response, post-launch.
- **DESIGN:** `DESIGN_PRINCIPLES` derived from VISION. Only `02_GAME_DESIGN` detail remains, pending the concept.

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
- **RESEARCH:** benchmark "Atlas Coop Viral" transferred into `01_RESEARCH/` (game fiches, mechanics matrix, patterns, design space, white spaces, sources).
- **TECH:** Unity environment verified (CLI beta.8, editor 6.6.0f1, Personal license, URP template). Unity project NOT created, by decision (ADR-002).
- **DECISION:** ADR-001 (Unity 6), ADR-002 (spec first).

### Guided documentation pass (with Pierre)
- **DOCS:** filled `OBJECTIVES`, `VISION`, `OPEN_QUESTIONS` (21, Jonathan-tagged), `RISKS` (16, P/I/M/Trigger).
- **BUSINESS:** `TEAM_AGREEMENT` (50/50 proposed, pending) + `IP`/`PROFIT`/`LEGAL`/`TAXES` pointers; `COST_MODEL`.
- **ART:** `ART_DIRECTION` and `STYLE_GUIDE` (silhouette-first, low-poly not locked).
- **DESIGN:** neutralized `GAME_CONCEPT` (candidates unranked).

### English standardization + game analyses
- **DOCS:** whole repository standardized to English (streamer guardrail added to CLAUDE.md, timeline to ROADMAP, time budget to CONSTRAINTS).
- **RESEARCH:** the game analysis fiches completed.
- **TESTING / ANALYTICS:** filled (lean, grounded in OBJECTIVES/RISKS).
