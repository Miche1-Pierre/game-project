# RISKS

_Risks that could materially threaten the project's quality, timeline, feasibility or commercial potential. Monitor and update as evidence appears. Format: Probability / Impact / Mitigation / Trigger._

### R1. Timeline is too aggressive
**Probability:** High. **Impact:** High.
Two weeks from concept to a credible sortable build is extremely aggressive for a two-person team. The main danger is not unfinished features, it is producing a shallow game because too much time went into infrastructure, assets or secondary systems.
**Mitigation:** greybox first; define the minimum viable game before implementation; kill weak ideas quickly; avoid unnecessary infrastructure; use AI for acceleration, not uncontrolled code generation; prioritize player-facing value; keep explicit scope boundaries.
**Trigger:** if the core loop is not playable early in week one, immediately reassess scope.

### R2. Multiplayer becomes the main time sink
**Probability:** High. **Impact:** High.
Networking, synchronization, physics, disconnect handling and multiplayer bugs can consume a disproportionate amount of time.
**Mitigation:** fix player count early; validate gameplay before building unnecessary networking; keep the architecture as simple as possible; prefer established Steam/Unity solutions over custom infrastructure; test multiplayer early instead of last.
**Trigger:** if networking work starts delaying core gameplay iteration, reduce multiplayer complexity or scope immediately.

### R3. The core concept is not fun
**Probability:** Medium/High. **Impact:** Critical.
The project could reach a technically functional state without a compelling experience. This is the most important product risk.
**Mitigation:** build a greybox immediately; define explicit go/no-go criteria; playtest before final assets; keep alternative concepts available; treat prototypes as experiments, not commitments.
**Trigger:** if players do not voluntarily want another round after several attempts, reconsider the concept.

### R4. Scope creep
**Probability:** High. **Impact:** High.
A successful prototype creates pressure to add more mechanics, maps, systems, progression, content or polish than the team can produce.
**Mitigation:** explicit scope document; anti-scope rules; every new feature needs a player-value justification; maintain a rejected/deferred list; prefer depth from existing systems over new systems.
**Trigger:** any feature that does not directly improve the core experience must be challenged before implementation.

### R5. Asset production is slower than expected
**Probability:** High. **Impact:** Medium/High.
The AI 3D to Blender to Unity pipeline is an assumption, not a validated pipeline.
**Mitigation:** validate the pipeline early on representative assets; use modular and reusable assets; avoid unnecessary rigging; use existing animations where appropriate; use procedural animation when sufficient; keep art direction within the team's production capacity.
**Trigger:** if one asset needs disproportionate manual work, simplify the visual requirement rather than multiplying the workload.

### R6. The game is too similar to existing hits
**Probability:** Medium. **Impact:** High.
The research process could accidentally produce a game that combines popular mechanics without a distinctive identity.
**Mitigation:** separate mechanics from game identity; analyze why mechanics work rather than copying them; define the game's unique core verb; define a clear reason to buy; compare against the competitive landscape before production; identify explicitly what the game does differently.
**Trigger:** if the game can be described as "X + Lethal Company / R.E.P.O. / Among Us but with Y", stop and reassess.

### R7. AI-generated code becomes a liability
**Probability:** Medium. **Impact:** High.
AI can accelerate implementation while introducing hidden complexity, poor architecture, bugs, unnecessary abstractions, and code neither developer understands.
**Mitigation:** keep systems small; review generated code; test every gameplay system; avoid large opaque implementations; maintain clear architecture docs; ask Claude to explain non-trivial systems before expanding them.
**Trigger:** if a system cannot be confidently modified or debugged by the team, stop adding functionality until it is understood.

### R8. Team availability / motivation drops
**Probability:** Medium. **Impact:** High.
The plan assumes an unusually intense sprint. Personal constraints, fatigue or availability differences can affect the schedule.
**Mitigation:** confirm availability before committing to milestones; keep responsibilities explicit; maintain a realistic minimum schedule; avoid making the project depend on one person being available every day.
**Trigger:** if either developer cannot maintain the planned workload, re-evaluate scope and timeline rather than compensating with uncontrolled overtime.

### R9. The game is fun but not commercially compelling
**Probability:** Medium. **Impact:** Critical.
A game can be enjoyable yet fail commercially because players do not understand why to buy it, discover it, recommend it or choose it over competitors.
**Mitigation:** define a clear reason to buy; validate the concept against the Steam market; analyze comparable games; build the Steam page early enough to test positioning; test the pitch independently from the implementation; track wishlists, playtest reactions and creator interest.
**Trigger:** if players enjoy the game but cannot clearly explain what makes it special, revisit positioning and differentiation.

### R10. The game is marketable but not deep enough
**Probability:** Medium. **Impact:** High.
A strong trailer or funny clip may attract players, but the game may fail to retain them if the loop becomes repetitive quickly.
**Mitigation:** test replayability during greybox; measure whether players voluntarily restart; prefer systemic variation over scripted content; test multiple sessions, not only the first reaction.
**Trigger:** if players enjoy the first 10 minutes but lose interest after several rounds, investigate replayability before adding content.

### R11. Technical complexity exceeds player value
**Probability:** Medium. **Impact:** High.
The team may build sophisticated systems because they are technically interesting rather than because they improve the experience (complex backend, persistent accounts, advanced AI, custom networking, unnecessary procedural generation, complex progression).
**Mitigation:** every significant technical system must answer "what player-facing value does this create?" If unclear, defer it.

### R12. Performance / physics / synchronization problems
**Probability:** Medium. **Impact:** High.
A systemic multiplayer game can be hard to optimize if it relies on physics, many interactive objects or frequent network sync.
**Mitigation:** establish technical constraints early; test representative scenarios; avoid synchronizing unnecessary state; profile rather than guess; define maximum object/player counts; keep physics interactions bounded.
**Trigger:** if the worst-case scenario causes major frame-rate or network problems, reduce systemic complexity before adding optimization layers.

### R13. Content requirements grow unexpectedly
**Probability:** Medium. **Impact:** High.
A seemingly system-driven game may still need many maps, objects, animations, sounds or scenarios to stay interesting.
**Mitigation:** test replayability with minimal content; prefer reusable systems; identify the minimum content set required for variation; do not assume "more maps" solves replayability.
**Trigger:** if the game becomes boring despite a working core system, check whether the problem is the system itself before producing more content.

### R14. Release quality is below commercial standard
**Probability:** Medium. **Impact:** High.
The aggressive timeline can produce a technically playable game that feels unfinished (poor onboarding, bugs, weak UI, poor audio, inconsistent visuals, performance issues, multiplayer edge cases, missing Steam integration, lack of testing).
**Mitigation:** reserve explicit time for QA, external playtesting, bug fixing, UX polish, performance and Steam preparation. Do not spend the whole schedule on feature development.

### R15. Marketing starts too late
**Probability:** Medium. **Impact:** High.
A good game can fail to generate awareness if marketing only begins right before release.
**Mitigation:** define positioning early; prepare the Steam page before release; collect wishlists as soon as the product is presentable; capture footage during development; identify creator/streamer targets early; design the game so naturally interesting moments can be recorded and shared.
**Trigger:** if the game nears release without clear positioning, a store page or an acquisition plan, pause feature work and address distribution.

### R16. Commercial success creates scope pressure
**Probability:** Medium. **Impact:** Medium.
Positive feedback can push the team to add features rapidly, damaging the core product or delaying release.
**Mitigation:** maintain a clear MVP; separate launch scope from post-launch ideas; prioritize evidence over enthusiasm; do not expand the game merely because players request unrelated features.

### R17. The concept is cloned before we can capitalize
**Probability:** High. **Impact:** High.
Cloning is now near-instant. Meccha Chameleon was copied within weeks: one Steam clone uses the original's exact name on the Korean storefront, and on Roblox more than a dozen copies exist, the two largest together holding more concurrent players than the original does today. A concept readable in 10 seconds is reproducible in 10 days, and AI-assisted development has removed most of the cost of copying.
**Mitigation:** accept that the premise is not defensible and put the moat in what is slow to copy, which is feel, tuning, iteration speed and the relationship with players; ship the good version first rather than the early version; do not reveal the mechanic publicly long before we can sell it; register the name early.
**Trigger:** if a clone appears before our launch, do not redesign. Ship faster and better, and compete on execution.

### R18. Netcode becomes a recurring cost that scales with success
**Probability:** Medium. **Impact:** High.
Per-concurrent-user networking bills grow exactly when the game works. Photon Premium is priced at $0.50 per CCU with a $1 000 monthly minimum. At the scale Meccha Chameleon reached, 340 k peak CCU, that is roughly $170 k per month. Our whole one-time budget is about 360 EUR.
**Mitigation:** default to a networking solution that is free at any scale (`07_MULTIPLAYER/NETWORK_ARCHITECTURE.md`, ADR-003 proposed); if we take a paid SDK for integration speed during the greybox, treat it as a prototype-only decision and re-evaluate before launch; never ship with an uncapped per-seat bill.
**Trigger:** before any public build, confirm the worst-case monthly networking cost at 10x our expected peak. If it is not affordable, change the solution.
**Update 2026-09-16:** largely closed. ADR-003 accepted, free solutions only (Steam or Unity), Photon excluded. Residual risk is now technical, not financial: free stacks do not solve physics synchronisation, which is our core mechanic. Track that under R12.

### R19. Revenue is forecast from wishlists rather than sales
**Probability:** Medium. **Impact:** Medium.
Wishlists are not purchases. Median first-week conversion sits near 10 to 15%, and drops above $10. Dear Passengers holds over 2 M wishlists with no release date, no price and no demo, which is attention, not revenue.
**Mitigation:** always apply the conversion factor in any plan or projection (`10_MARKETING/WISHLIST_STRATEGY.md`); treat a high wishlist count with a low follower count as speculative interest.
**Trigger:** any planning document stating an expected revenue figure without the conversion factor gets corrected on the spot.

### R20. Announcing before the build can follow up
**Probability:** Medium. **Impact:** Medium.
A reveal that outruns production converts a spike into decay. Dear Passengers went from 0 to 2 M wishlists in 17 days, then published nothing for six weeks, missed the gameplay video it promised and did not ship the demo it announced for Gamescom.
**Mitigation:** do not publish a Steam page or trailer until a playable build exists and a follow-up cadence is possible; plan the reveal as the start of a sequence, not as an event.
**Trigger:** if we cannot commit to a second public artefact within four weeks of the reveal, delay the reveal.

### R21. The reason to buy is still unwritten
**Probability:** Certain today. **Impact:** Critical.
The concept is chosen, the scope is frozen and the build is about to start, but nobody can yet complete "you should buy this game because ______" without naming another game. The team deferred this deliberately until the greybox is playable, which is defensible, but it means we are building before we can sell.
**Mitigation:** write the sentence the week the greybox is playable, from what actually made people laugh rather than from the design document; if it still needs R.E.P.O. to be explained, treat that as a design finding, not a copywriting problem.
**Trigger:** if the sentence cannot be written after the greybox, reassess the concept before producing any asset.

### R22. The team agreement stays unsigned into a commercial release
**Probability:** Medium. **Impact:** Medium, rising with success.
Ownership, profit split, expenses and what happens if someone leaves were deliberately left unformalised on 2026-09-16, because the project is also for fun. That is fine while the game earns nothing. It becomes expensive exactly when it stops being fine.
**Mitigation:** treat the Steam page opening as the deadline. Before money can arrive, `08_BUSINESS/TEAM_AGREEMENT.md` gets agreed and dated.
**Trigger:** any of the following reopens it immediately: the greybox passes its go / no-go, a publisher makes contact, or a shared expense above the agreed threshold is incurred.

## Risk management rule
The project should not try to eliminate every risk. The objective is to identify the risks that can kill the project early and test them as cheaply as possible.

Highest-priority risks:
1. The core concept is not fun.
2. The game is not sufficiently differentiated.
3. Multiplayer complexity consumes the schedule.
4. The game is fun but not commercially compelling.
5. The timeline forces unacceptable compromises.
6. The game requires more content than the team can produce.
7. A recurring networking cost that scales with success (R18). Largely closed by ADR-003.
8. The reason to buy is still unwritten (R21).
