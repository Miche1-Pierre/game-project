# CHANGELOG

_Not just code. Categories: DESIGN, TECH, RESEARCH, DOCS, CONTENT, BALANCE, BUSINESS, MARKETING._

## 2026-09-16

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
