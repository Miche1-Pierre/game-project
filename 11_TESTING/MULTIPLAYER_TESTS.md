# MULTIPLAYER TESTS

_Test the social layer early (risk R2). Progression from cheap to real._

## Steps
1. Hot-seat or two instances on one machine (fastest).
2. Two PCs on LAN.
3. Steam P2P once the loop is proven.

## What to check
- State stays in sync across clients (positions, objects, score).
- Join and leave mid-session do not break the game.
- The host has clear authority (`../07_MULTIPLAYER/AUTHORITY.md`).
- Latency does not ruin the core interaction.

## Rule
Do not build networking depth before the gameplay is proven fun in local / hot-seat.
