# REJECTED

_What we have explicitly decided NOT to do. Prevents reinventing an already-eliminated idea (human or AI)._

| Rejected idea | Reason | Date | Revisit if |
|---|---|---|---|
| (example) Rich NPC detection AI | Too expensive and unreadable for two, replaceable by player-driven detection | 2026-09-15 | AI budget and readability solved |
| Convai for the grandmother (AI dialogue, voice, actions) | It needs an API key and a Convai account. The free plan is tiny (one active user, one conversation at a time), and at release every player conversation is billed to us: the per-seat cost ADR-004 excludes. Pierre: nothing paid. The SDK was imported, patched for 6000.6, then removed the same day (commits 768a6f2 and its removal) | 2026-09-27 | A free, local (offline) dialogue model becomes good enough, and talking to her is a verb the playtests ask for |
| Breakable floors and stairs | Breaking the ground you stand on is a different game: the house has to stay walkable for the carry, and a hole to the cellar ends every route through it. Everything else in the house breaks (ADR-008). Amended 2026-09-26 (ADR-009): roofs still never break, but they now fall as whole sections when nothing holds them up | 2026-09-25 | Destruction turns out to be the core verb |

_Status: living. Every discarded idea lands here with its reason._