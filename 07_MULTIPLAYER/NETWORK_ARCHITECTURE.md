# NETWORK ARCHITECTURE

_The netcode choice stays deferred until the gameplay and the player count are frozen (`00_PROJECT/ROADMAP.md`). What changed in Sept. 2026 is that we now have cost evidence, so the default direction moved._

## What the corpus actually uses
| Game | Engine | Networking | Voice |
|---|---|---|---|
| Meccha Chameleon | Unreal Engine 5 | Epic Online Services, player-hosted listen server | not identified |
| Lethal Company | Unity | Netcode for GameObjects | Dissonance |
| R.E.P.O. | Unity | Photon PUN | Photon Voice |
| Content Warning | Unity | Photon PUN | Photon Voice |
| PEAK | Unity | Photon | not identified |
| Dear Passengers | Unity | unknown, never stated | unknown, never stated |

Semiwork chose Photon for R.E.P.O. on a recommendation from Landfall, with no prior multiplayer experience, and reported it was quick to set up. Their one hard problem was making physics feel smooth on clients, which Photon does not solve out of the box.

## The cost difference is the decision
| Solution | Cost |
|---|---|
| Epic Online Services | free, at any scale |
| Photon Fusion | free up to 100 CCU |
| Photon Fusion | $125/mo at 500 CCU, $250 at 1 000, $500 at 2 000 |
| Photon Premium | $0.50 per CCU, $1 000/mo minimum |
| Steam Networking (P2P / SDR) | free, Steam only |

Meccha Chameleon peaked at 340 k concurrent players on EOS and paid nothing. On the Photon premium grid that traffic is roughly $170 k per month. Our entire one-time budget is about 360 EUR (`08_BUSINESS/COST_MODEL.md`). A per-seat netcode is an uncapped liability we cannot absorb if the game works.

## Revised default direction
**Epic Online Services, via its official Unity plugin, with a player-hosted listen server.** EOS provides lobbies, peer-to-peer, sessions, matchmaking and voice, is engine-agnostic, and stays free regardless of player count. It does not require leaving Unity.

Steam Networking remains a valid fallback and is simpler if we accept being Steam-only. Photon is the fastest to integrate and is what most of the corpus used, but it is the only option with an unbounded bill.

## Consequences of a listen server
This is what Meccha Chameleon ships, and the trade-offs are visible in its player reports.
- Player cap depends on the host's upload bandwidth and CPU, so the cap is a recommendation, not a guarantee.
- Objects can desync or misalign on clients. Reviewers noticed.
- No host migration unless we build it. If the host leaves, the session dies.
- Physics authority must be decided explicitly. Both R.E.P.O. and Dear Passengers report physics in co-op as their single hardest problem.

## To settle after the concept (write an ADR)
- P2P vs authoritative server
- Target player count (`00_PROJECT/OPEN_QUESTIONS.md` Q5)
- Authority model and disconnect behaviour
- Host migration: build it or accept session death
- Voice: EOS voice, a third party such as Dissonance, or none

## Greybox
Unchanged. Testable in hot-seat or on two PCs, without final netcode. Do not integrate any networking SDK before the loop is proven.

_Status: deferred. Default direction revised to EOS, pending ADR-003._
