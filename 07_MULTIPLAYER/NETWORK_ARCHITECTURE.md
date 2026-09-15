# NETWORK ARCHITECTURE

_Deferred decision: we do not choose the netcode before freezing the gameplay and the player count (see `00_PROJECT/ROADMAP.md`)._

## Default direction
Steam Networking (P2P relayed via Steam Datagram Relay), host/client, four players to start. Avoids building server infrastructure at the outset.

## To settle after the concept (write an ADR)
- P2P vs authoritative server
- Target player count
- Authority (host) and disconnects
- Voice (proximity) built-in or third-party

## Greybox
Testable in hot-seat or two PCs, without final netcode.

_Status: deferred, direction noted._