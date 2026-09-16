# AI ARCHITECTURE

_NPC / enemy behavior. Concept-dependent, and deliberately minimal._

## Principles (set now)
- Prefer **players as the interesting agents**; keep NPC AI as simple as the concept allows (benchmark H3: make the players the detection system, not an expensive AI).
- If NPCs are needed: small state machines or behavior trees, readable and cheap, using Unity NavMesh for movement.
- No machine-learning agents. No heavyweight AI framework without a documented need.

## To define with the concept
Whether NPCs exist at all, their role (threat, witness, decor), and the minimum simulation that produces good stories (benchmark section 10).

_Status: awaiting the concept._
