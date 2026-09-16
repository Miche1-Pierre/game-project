# ADR-003: Networking direction, Epic Online Services

## Status
**Proposed.** Not accepted. Requires agreement from both developers and a frozen player count. Do not implement.

## Context
`03_TECHNICAL/TECH_STACK.md` listed Steam Networking as the deferred default. September 2026 research on the current corpus produced cost evidence that was not available when that line was written.

Most of the corpus (R.E.P.O., Content Warning, PEAK) runs on Photon, which prices per concurrent user. The largest 2026 hit, Meccha Chameleon, runs on Epic Online Services with a player-hosted listen server and pays nothing, at 340 k peak concurrent players.

Our one-time budget is about 360 EUR (`08_BUSINESS/COST_MODEL.md`) with a ceiling near 750 EUR. A per-seat cost is the one expense that grows precisely when the project succeeds.

## Options
- **A. Epic Online Services.** Free at any scale. Official Unity plugin. Lobbies, P2P, sessions, matchmaking, voice. Adds an Epic dependency and an account layer.
- **B. Steam Networking (P2P / SDR relay).** Free, simplest, no third party beyond Valve. Steam only, which closes off other stores later.
- **C. Photon (PUN / Fusion).** What most of the corpus used. Fastest to integrate, well documented for exactly our genre. Free to 100 CCU, then priced per CCU with no ceiling.
- **D. Unity Netcode for GameObjects.** First party, free, what Lethal Company used. Requires our own transport and relay decision anyway.

## Decision
**Proposed:** option A, Epic Online Services through the official Unity plugin, with a player-hosted listen server, four players to start.

## Why
- It removes the only unbounded cost in the project.
- It does not require changing engine. We stay on Unity 6 per ADR-001.
- A listen server matches our scope: small sessions, no persistence, no authoritative simulation.
- It is proven at a scale far beyond anything we will reach.

## Consequences
Positive: zero recurring infrastructure cost, no billing risk on success, cross-store capable later.
Negative: session quality depends on the host's connection, no host migration unless built, an Epic account dependency, and less genre-specific documentation than Photon.

If speed of integration turns out to matter more than cost during the greybox, option C is the pragmatic fallback, accepting the bill.

## Revisit if
- The chosen concept needs authoritative physics or server-side simulation.
- The player count goes well above roughly 8.
- EOS integration costs more than two days of work during the prototype, in which case take the fastest option and revisit after the loop is proven.

## Blocked on
`00_PROJECT/OPEN_QUESTIONS.md` Q5 (player count) and Q9 (minimum architecture). Both are tagged `[JONATHAN]`.
