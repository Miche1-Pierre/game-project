# DEVELOPMENT CONTEXT

_How these games were actually made: team size, dev time, background, tools. The most useful angle for us, because it tells us what a tiny team can realistically ship and how fast. Cross-checked, Sept. 2026 (`RESEARCH_SOURCES.md`)._

## Table
| Game | Team | Dev time | Background / environment | Engine |
|---|---|---|---|---|
| Content Warning | 5 (Landfall x Zorbus) | ~6 weeks, a 1-month jam in Seoul | established small studio | Unity |
| Buckshot Roulette | 1 (Mike Klubnika, Estonia) | ~2 months | itch.io first, then Steam | unknown |
| Meccha Chameleon | 2 (Lemorion_1224, Haganeiro, JP) | ~2 months | ex-Fortnite UEFN creators | unknown |
| PEAK | Aggro Crab x Landfall (small) | ~1 month, game-jam "side hustle" | two established indie studios | Unity |
| Megabonk | 1 (vedinad) | weeks-scale | solo, first breakout | unknown |
| Lethal Company | 1 (Zeekerss) | months (EA) | 21, ex-Roblox horror dev | Unity |
| R.E.P.O. | Semiwork (small, Sweden) | unclear | small studio | Unity |
| Among Us | 3 (InnerSloth) | months, then a 2-year slow burn | tiny studio | Unity |
| Chained Together | Anegar Games (small) | short | debut studio | unknown |
| Schedule I | 1 ("Tyler" / TVGS) | long (deep sim) | solo | Unity |
| Balatro | 1 (LocalThunk) | ~2.5 years | IT day-job background | LÖVE / Lua |
| Overcooked 2 | Ghost Town / Team17 | ~1 year | small studio + publisher | Unity |
| Phasmophobia | 1 at launch (Daniel Knight), now 30+ | years (still EA) | solo, then grew | Unity |
| Gang Beasts | Boneloaf (small) | years (EA) | small studio | Unity |
| GTFO | 10 Chambers (small) | years | ex-Payday veterans | Unity |
| Big Walk | House House (small) | years | Untitled Goose Game devs | unknown |

## Patterns that matter for us
- **Tiny teams win.** Most hits are 1 to 5 people. Two of us is normal for this space.
- **Speed is possible.** Content Warning (~6 weeks, 5 people), Buckshot (~2 months, 1), Meccha (~2 months, 2), PEAK (~1 month jam). Our 2-week / 2-person + AI target is aggressive but in the same family, especially since AI compresses the coding.
- **Deep-systems games take years** (Balatro 2.5y, Schedule I, GTFO). That lane is closed to a 2-week sprint. We want the "fast, small, systemic" lane, not the "deep sim" lane.
- **Reused knowledge helps.** Meccha's devs came from Fortnite UEFN; Landfall, Aggro Crab and 10 Chambers were experienced. Our edge is software-engineering experience + AI.
- **No heavy backend.** These games run on Steam P2P with no custom server or database (Meccha, PEAK, R.E.P.O., Lethal). Near-zero cost, and our default (`../03_TECHNICAL/DATA_ARCHITECTURE.md`).

## What this tells us
A 2-person team can ship a viral co-op game in weeks IF the concept is small and systemic, runs on Steam P2P with no backend, and leans on free or generated assets. That is exactly the lane our OBJECTIVES and CONSTRAINTS already target.
