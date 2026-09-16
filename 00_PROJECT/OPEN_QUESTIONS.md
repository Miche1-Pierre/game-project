# OPEN QUESTIONS

_Unresolved questions that can materially affect project decisions. Resolve them when sufficient evidence becomes available. Do not resolve important questions through assumptions when they can be tested. `[JONATHAN]` marks a question that needs Jonathan's decision or commitment._

## Game Direction

### Q1. Which concept do we prototype first? `[JONATHAN]`
Which candidate concept has the strongest combination of: immediate fun, differentiation, replayability, social interaction, emergent situations, commercial potential, technical feasibility, and production feasibility within our constraints? Do we prototype one concept first, or run two very small competing prototypes before committing?
_Status: Open._

### Q2. What is the actual reason to buy each candidate?
For every candidate, can we complete this in one clear sentence: "You should buy this game because ______." If the answer depends mainly on the setting, the graphics, or "it's funny with friends", the concept is probably not differentiated enough.
_Status: Open._

### Q3. What is the core player action / verb?
What is the fundamental action players repeatedly perform that makes this game different? Examples from other games are references, not answers.
_Status: Open._

### Q4. What creates the emergent stories?
Once a candidate is selected, what combination of systems produces the unexpected situations we want? Ideally a small number of interacting systems rather than many scripted events.
_Status: Open. Resolve during: Game Design._

### Q5. What player count should we target? `[JONATHAN]`
Options: 2, 2-4, 2-6, more than 6. Player count affects design, social dynamics, level design, network architecture, performance, testing, hosting, marketing and replayability. Default to the smallest count that produces the intended experience.
_Status: Open._

## Development / Technical

### Q6. Is the 1-week greybox / 2-week sortable target realistic?
Treat it as a hypothesis, not a commitment. The real timeline depends on the selected concept, multiplayer complexity, number of systems, asset and UI requirements, and technical unknowns.
_Status: Open._

### Q7. How should multiplayer be validated during the greybox?
Possible progression: local/single-machine where possible, then two-PC/LAN, then Steam networking, then external playtest. Avoid spending significant time on networking before proving the gameplay.
_Status: Open._

### Q8. Do we need Steam networking from the first multiplayer prototype?
Only if the mechanics themselves depend on real networked multiplayer. Otherwise check whether local simulation / LAN can validate the core experience first.
_Status: Open._

### Q9. What is the minimum networking architecture required?
Before implementing networking, determine: maximum players, authority model, required synchronized objects, required physics sync, host/client responsibilities, persistence, voice, and disconnect/reconnect requirements. Do not build infrastructure the final game does not require.
_Status: Open._

### Q10. How much of the asset pipeline can actually be automated?
Validate the full pipeline: AI generation, Blender if required, optimization, materials/textures, rig/animation if required, Unity, final build. The objective is sufficient perceived quality at minimum production cost, not maximum quality.
_Status: Open._

### Q11. What can be procedural instead of authored?
For each content requirement, determine whether it can be procedural, system-generated, reusable, modular, AI-assisted, or hand-authored. Prefer systems and reusable assets where they produce equivalent player value.
_Status: Open._

## Team

### Q12. What is Jonathan's realistic availability during the initial sprint? `[JONATHAN]`
Confirm approximate hours/day, which days are realistically available, which responsibilities Jonathan wants to own, and whether the two-week near-full-time assumption holds. The ~140 combined human hours estimate is not confirmed until both developers agree.
_Status: Open._

### Q13. How do we divide ownership? `[JONATHAN]`
Ownership of: game design, programming, Unity implementation, art/asset pipeline, audio, marketing, Steam, playtesting, community/creator outreach, release management. Both developers should understand the whole project, but ownership should be explicit.
_Status: Open._

### Q14. What is our decision-making process? `[JONATHAN]`
When we disagree on a design or technical decision: who decides, do we prototype both options, and what evidence is sufficient to settle it? Prefer evidence from prototypes and playtests over personal preference.
_Status: Open._

### Q15. What level of commercial commitment does Jonathan want? `[JONATHAN]`
The objective is now ambitious ("we want to try for a hit"). Beyond sales numbers, agree on the level of effort accepted after the prototype: Steam page, marketing, creator outreach, support, bug fixing, launch. Both developers must be aligned on how far they are willing to push post-prototype.
_Status: Open._

## Validation

### Q16. What exactly constitutes a successful greybox?
Define go/no-go criteria before building it. Possible criteria: players understand the objective and interactions quickly, naturally create unexpected situations, want to replay without being asked, laugh/react naturally, discover strategies without instruction, and the game stays interesting after several rounds and without final graphics.
_Status: Open._

### Q17. What happens if the first prototype fails?
Possible strategies: modify the core loop, remove problematic systems, change the objective, switch to a second candidate, or abandon the concept. A failed prototype is information, not sunk cost.
_Status: Open._

### Q18. How much external testing do we need before committing?
Determine when to test with developers only, friends, players unfamiliar with the project, and Steam Playtest users.
_Status: Open._

## Commercial

### Q19. What is the actual target audience?
Do not define the audience only as "people who like co-op games". Determine primary player profile, typical group size, games they already play, why they would discover this game, why they would buy it, and what makes them recommend it.
_Status: Open._

### Q20. What is the intended price range?
Price should eventually reflect comparable games, expected playtime, replayability, production quality, differentiation and target audience. Do not optimize price before the product is understood.
_Status: Open._

### Q21. What evidence would make us change direction commercially?
Define thresholds for wishlist growth, playtest retention, player reactions, creator interest, store-page conversion and sales. The project should have explicit signals that can trigger a strategic change.
_Status: Open._

## Added September 2026 (benchmark pass on Meccha Chameleon and Dear Passengers)

### Q22. What reusable capital do we build first? `[JONATHAN]`
The fastest hits were not fast because the concept was good. They were fast because a working system already existed: Meccha Chameleon reused the multiplayer layer of the authors' previous game, to the point that the shipped executable is still named `PenguinHotel.exe`. We have no such asset. Do we accept that our first project pays the cost of building it, or do we deliberately build a reusable co-op base (lobby, join, sync, interaction) that survives a concept change?
_Status: Open. This may matter more than the concept choice in Q1._

### Q23. What, if anything, ships as AI-generated content?
Valve's January 2026 rules put AI code assistants out of scope, so using Claude Code needs no disclosure. Any generated 3D asset, texture, audio or store image that ships does, and the declaration is visible on our store page. Given that our art plan assumes AI 3D generation (`00_PROJECT/CONSTRAINTS.md`), do we accept a disclosure on the page, and does it affect perception in this genre? Note that both games we studied ship with no disclosure at all.
_Status: Open. Resolve before the store page, not before the greybox._

### Q24. Do we pay for netcode speed during the prototype? `[JONATHAN]`
Free at any scale (Epic Online Services, Steam Networking) versus fastest to integrate but priced per concurrent user (Photon, which most of the corpus used). The cost only bites if we succeed, and then it bites hard. See ADR-003, proposed, and `07_MULTIPLAYER/NETWORK_ARCHITECTURE.md`.
_Status: Open. Depends on Q5 (player count)._

### Q25. When do we reveal publicly?
The reveal is the campaign, and it is a one-shot asset. Revealing early buys wishlists we may not convert and starts a clock we may not meet. Revealing late forfeits the wishlist accumulation window. What has to be true before we publish a Steam page?
_Status: Open. Proposed condition in `10_MARKETING/WISHLIST_STRATEGY.md`: a playable build exists and a second public artefact is possible within four weeks._

## Current Priority
The highest-priority unresolved questions are:
1. Which concept deserves the first prototype?
2. What is its unique reason to buy?
3. What is the core player verb?
4. What player count creates the intended experience?
5. What does the minimum viable greybox need to prove?
6. Is the two-week production target technically realistic?
7. What is the fallback if the first concept fails?
8. What reusable capital do we build first (Q22)?

Everything else can wait until these are sufficiently resolved.
