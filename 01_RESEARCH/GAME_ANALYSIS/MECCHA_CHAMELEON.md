# Meccha Chameleon

_Corpus analysis. Added Sept. 2026 on Pierre's suggestion, then completed with verified technical and production facts (`../RESEARCH_SOURCES.md`)._

## Identity
2026 (Windows June 10, Switch 2 Sept 9) - $5.99 - Lemorion_1224 + Haganeiro (2 devs, Japan) - 2 to 10 recommended, 24 max - **Unreal Engine 5** - 20 M+ sold in 2 months - 340 k peak CCU - 87% Very Positive. Made in ~2 months; the devs came from Fortnite UEFN.

## Hook
This game is different because: chameleons paint themselves to blend into the scene while hunters race a timer to spot them.

## Signature verb
**HIDE (by painting), or BLEND.**

## Fantasy
Be the thing hiding in plain sight, or the sharp eye that finds it.

## Core loop
Hunters vs chameleons, chameleons paint and pose to match the surroundings, hunters scan and tag before time runs out, swap roles, repeat.

## Central rule
Blend in well enough to survive the timer, or find all hidden players in time.

## Signature mechanic
Creative camouflage (painting/posing) as the hiding mechanic: skill and creativity decide who wins. The painting tools were modelled on Substance Painter, including the colour picker and the eyedropper, because the lead had used that software professionally.

## Social loop
Hiders bluff through placement and paint; seekers call out sightings; everyone laughs at near-misses. The comedy is the gap between what you think you painted and what the others see, which is dramatic irony in real time.

## Fun / tension / replayability
Fun: the reveal of a clever hide. Tension: the timer + being spotted. Replay: players and creativity are the variation, plus Steam Workshop maps made by players.

## Content: systems vs handmade
Systemic (players generate the content) + a few maps. The character is a plain white untextured biped, which is simultaneously the cheapest possible asset and a hard requirement of the mechanic.

## Differentiator
Prop-hunt reinvented with creative painting instead of fixed props.

## Technical profile (verified)
- **Engine: Unreal Engine 5**, confirmed by Epic's own Unreal Engine Japan account. The pair spent about 18 months shipping in UEFN (Unreal Editor for Fortnite) before moving to Steam, which fully explains the choice.
- **Networking: Epic Online Services, player-hosted listen server.** No dedicated servers, and no server depot ships on Steam. The store page states that the player cap depends on the host's connection.
- **Server cost: zero. Advertising cost: zero.**
- **The shipped executable is named `PenguinHotel.exe`.** They did not start a new project, they reused their previous game and renamed it at the store level. The multiplayer system comes straight from their earlier title LINK Penguins.
- The one real engineering problem they report: early on, a single player drawing slowed the whole session. Paint state is replicated, and optimising that replication was the actual work.
- **No AI disclosure on the Steam page**, meaning no generated content ships. The "vibe coded" reputation is unsubstantiated.

## Production reality
About 2 months of active work, 4 to 5 months counting the reused systems. It is their **seventh** Steam release since late 2024. The previous one, LINK Penguins, took 7 months and sold about 5,000 copies.

## Distribution
No paid marketing. A wide international closed beta (around 90% non-Japanese testers) with recording and streaming allowed, which seeded clips before launch. The game then reached #3 on Twitch with 10.4 M hours watched.

## What we take for our game
1. **Reusable capital beats a good concept.** The 2 months only exist because a working multiplayer layer already existed. Our equivalent asset does not exist yet, and that is our real constraint.
2. **The cheapest possible asset can be the mechanic.** Look for that overlap before art direction.
3. **Free networking is a commercial decision.** At 340 k CCU a per-seat netcode would have cost more per month than our entire budget.
4. **Let the testers make the clips.** The pre-launch beta was the marketing.
5. Another "2 people, 2 months, shipped to Steam" proof point, directly relevant to our team size and timeline.

## Caution
Clones appeared within weeks. One Steam copy uses the original's exact name on the Korean store. On Roblox, a dozen clones exist and the two largest together hold more concurrent players than the original does today. A concept readable in 10 seconds is also clonable in 10 days (see `../../00_PROJECT/RISKS.md`, R17).
