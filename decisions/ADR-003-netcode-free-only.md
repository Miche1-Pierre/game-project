# ADR-003: Networking, free solutions only

## Status
**Accepted (2026-09-16).** Supersedes the proposed Epic Online Services draft of 2026-09-15, which was written before the team decided.

## Context
September 2026 benchmark research produced cost evidence. Most of the corpus (R.E.P.O., Content Warning, PEAK) runs on Photon, which prices per concurrent user with no ceiling. Meccha Chameleon runs on Epic Online Services with a player-hosted listen server and pays nothing at 340 k peak concurrent players.

Our one-time budget is about 360 EUR with a ceiling near 750 EUR (`08_BUSINESS/COST_MODEL.md`). A per-seat cost is the single expense that grows precisely when the project succeeds.

Player count was frozen at four, which made the question decidable.

## Options
- **A. Steam Networking (P2P / Steam Datagram Relay).** Free, simplest, Steam only.
- **B. Unity Netcode for GameObjects.** First party, free, what Lethal Company used.
- **C. Epic Online Services.** Free at any scale, engine agnostic, adds an Epic account dependency.
- **D. Photon.** Fastest to integrate, best documented for this exact genre, priced per concurrent user.

## Decision
**Free solutions only. Steam or Unity, that is options A and B.** Photon is excluded.

The team's rule as stated: everything that can be done for free is done for free. Host is a player, no backend, no dedicated server, four players maximum.

Epic Online Services is kept as a documented alternative should a non-Steam store matter later. It is not the default any more.

## Why
- It removes the only unbounded cost in the project.
- Four players with no persistence does not need an authoritative server.
- Unity and Steam are already in the stack. No new vendor, no new account layer.
- The corpus proves the pattern works at scale far beyond anything we will reach.

## Consequences
Positive: zero recurring infrastructure cost, no billing risk on success, no third party beyond ones we already use.

Negative and accepted:
- Session quality depends on the host's connection.
- No host migration unless we build it. If the host leaves, the session ends.
- Steam only, for as long as we stay on option A.
- **Physics synchronisation is not solved by any of these.** Both benchmarked teams named physics in co-op as their hardest problem. Since object physics is our core mechanic, this is our largest technical risk, not an integration detail (`00_PROJECT/RISKS.md` R12 and R18).

## Validation order
Per the meeting: each developer local first, then two clients on one machine using the school VMs, then Steam networking last. Steam networking is explicitly **not** part of the greybox.

## Revisit if
- The concept ever needs authoritative physics or server-side simulation.
- Player count goes meaningfully above four.
- We decide to ship outside Steam, which is when option C comes back.

## Related
`07_MULTIPLAYER/NETWORK_ARCHITECTURE.md`, `03_TECHNICAL/TECH_STACK.md`, `02_GAME_DESIGN/GREYBOX_SPEC.md`.
