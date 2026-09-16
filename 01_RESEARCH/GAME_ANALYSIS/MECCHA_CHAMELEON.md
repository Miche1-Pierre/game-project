# MECCHA CHAMELEON

_Corpus analysis. Added Sept. 2026 (see `../RESEARCH_SOURCES.md`). The most important new data point in the corpus: it breaks three of our assumptions at once._

## Identity
2026 - $5.99 - lemorion_1224 + haganeiro (2 people) - 2 to 10 recommended, 24 max - **Unreal Engine 5** - 20 M+ sold in 2 months - 340 k peak CCU - 87% Very Positive.

## Hook
This game is different because: you paint your own white body to match the wall you are standing against.

## Signature verb
**BLEND.**

## Fantasy
Being a bad chameleon in a room full of friends who are looking for you.

## Core loop
Hiders sample a surface colour with an eyedropper, paint their body to match it, freeze in place, and hope. The seeker walks the room looking for the one shape that is slightly wrong. Round ends, roles rotate.

## Central rule
Your camouflage is something you make by hand, badly, under time pressure.

## Signature mechanic
Runtime painting on the player's own skinned mesh. The tools were modelled on Substance Painter, including the colour picker and the eyedropper, because the lead had used that software professionally.

## Social loop
The comedy is the gap between what you think you painted and what the others see. Spectators see the failure before the hider does, which is dramatic irony in real time (see `../WHITE_SPACES.md`, opportunity 4).

## Fun / tension / replayability
Fun: authored incompetence, your own hand is the joke. Tension: the seeker walking towards you while you are still painting. Replay: every map surface is a new problem, and Steam Workshop maps are made by players.

## Content: systems vs handmade
One system (paint on mesh) plus maps. The character art is a plain white untextured biped, which is simultaneously the cheapest possible asset and a hard requirement of the mechanic.

## Differentiator
Camouflage as a manual craft skill rather than a toggle or an ability.

## Technical profile (why it matters to us)
- **Engine: Unreal Engine 5**, confirmed by Epic's own Unreal Engine Japan account. The pair spent about 18 months shipping in UEFN (Unreal Editor for Fortnite) before moving to Steam, which fully explains the choice.
- **Networking: Epic Online Services, player-hosted listen server.** No dedicated servers, and no server depot ships on Steam. The store page itself states that the player cap depends on the host's connection.
- **Server cost: zero. Advertising cost: zero.**
- **The shipped executable is named `PenguinHotel.exe`.** They did not start a new project, they reused their previous game and renamed it at the store level. The multiplayer system comes straight from their earlier title LINK Penguins.
- The one real engineering problem they report: early on, a single player drawing slowed the whole session. Paint state is replicated, and optimising that replication was the actual work.

## Production reality
About 2 months of active work, 4 to 5 months counting the reused systems. It is their **seventh** Steam release since late 2024. The previous one, LINK Penguins, took 7 months and sold about 5,000 copies.

## Distribution
No paid marketing. A wide international closed beta (around 90% non-Japanese testers) with recording and streaming allowed, which seeded clips before launch. The game then reached #3 on Twitch with 10.4 M hours watched.

## What we take for our game
Four things, in order of importance.
1. **Reusable capital beats a good concept.** The 2 months only exist because a working multiplayer layer already existed. Our equivalent asset does not exist yet, and that is our real constraint, not the concept.
2. **The cheapest possible asset can be the mechanic.** A blank white character costs nothing and is the entire design. Look for that overlap before art direction.
3. **Free networking is a commercial decision.** See `../../07_MULTIPLAYER/NETWORK_ARCHITECTURE.md`. At 340 k CCU a per-seat netcode would have cost more per month than our entire budget.
4. **Let the testers make the clips.** The pre-launch beta was the marketing.

## Caution
Clones appeared within weeks. One Steam copy uses the original's exact name on the Korean store. On Roblox, a dozen clones exist and the two largest together hold more concurrent players than the original does today. A concept readable in 10 seconds is also clonable in 10 days (see `../../00_PROJECT/RISKS.md`, R17).
