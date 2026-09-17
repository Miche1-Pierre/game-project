# CLAUDE.md, project constitution

> Agent: read this file fully, then [`00_PROJECT/PROJECT_STATE.md`](00_PROJECT/PROJECT_STATE.md), before any action.

## 1. PROJECT IDENTITY
Indie coop/social game, PC/Steam, team of 2 (Miche1-Pierre + Spykernv). Concept: **steal under watch**, a moving crew empties a house while the owner is present (ADR-004, see `02_GAME_DESIGN/GAME_CONCEPT.md`). This repository is the decision repository; `UnityProject/` is only one part.

## 2. CURRENT PROJECT STATUS
Phase: greybox. The concept is locked and `02_GAME_DESIGN/GREYBOX_SPEC.md` exists, so the ADR-002 gate is passed and Unity may be created. Source of truth: `00_PROJECT/PROJECT_STATE.md`, read it and keep it updated.

## 3. DEVELOPMENT PHILOSOPHY
- Optimize for learning speed before development volume.
- Greybox before art. If it is not fun in grey boxes, art will not save it.
- Reusable systems before specialized features.
- The concept emerges from research, not the other way around.

## 4. GAME DESIGN PRINCIPLES
- Never add a feature just because it seems interesting. Check its purpose and its impact on the core loop.
- Every new mechanic must explain its contribution to the core loop.
- Funny failure over frustrating punishment.
- Aim for social friction that a spectator understands in under 10 seconds (benchmark thesis, `01_RESEARCH/`).
- Streamer and clip potential is a **property** of the design, not its **goal**. The game must be fun with nobody recording. Never optimize for producing clips at the expense of a good game.

## 5. TECHNICAL PRINCIPLES
- Simple and systemic over complex and specialized.
- No backend or infrastructure without an explicitly documented need.
- No premature optimization. Measure first.

## 6. UNITY RULES
- Unity 6 (6000.6.0f1), **Built-in RP, not URP**. See `03_TECHNICAL/UNITY_SETUP.md` and `03_TECHNICAL/ASSET_AUDIT.md`.
- Drive the editor through the Unity MCP (`03_TECHNICAL/MCP_WORKFLOW.md`).
- After any gameplay change: inspect the scene and read the console (zero errors).

## 7. C# RULES
- Standard C#/Unity conventions. Clear names, small classes, composition over inheritance.
- Define architecture boundaries in the docs before creating classes. Do not generate 150 classes upfront: code is born when the prototype needs it.

## 8. MCP WORKFLOW
Loop: prompt, code, Unity, test, observation, correction. Detail: `03_TECHNICAL/MCP_WORKFLOW.md`.

## 9. GIT WORKFLOW
- Short branches, atomic commits, clear messages.
- Commit and push only on explicit request.
- Never commit `UnityProject/Library`, `Temp`, `Logs` (see `.gitignore`).

## 10. TESTING RULES
- After a gameplay change: greybox playtest and Unity error check.
- Funny vs destructive bugs: see `11_TESTING/BUG_POLICY.md`.

## 11. PERFORMANCE RULES
Target 60 fps in greybox on our machines. Profile before optimizing.

## 12. MULTIPLAYER RULES
No final netcode before the gameplay and player count are frozen. Greybox in hot-seat or local. See `07_MULTIPLAYER/`.

## 13. ASSET RULES
No final asset until the relevant system is validated. Pipeline and statuses: `05_ART/ASSET_STATUS.md`.

## 14. AI USAGE RULES
AI 3D (generation, Blender) to accelerate already-decided content, never to decide the content.

## 15. SCOPE RULES
- Any feature that significantly grows scope: flag it, do not implement it quietly.
- Discarded ideas: write them in `04_PRODUCTION/REJECTED.md` so they are not reinvented.

## 16. DECISION RULES
- Before an important change, read the relevant design doc(s).
- Every structural decision becomes an ADR in `decisions/`.
- If a request implies an unresolved game-design decision, flag it instead of deciding alone.

## 17. CURRENT PRIORITIES
1. Build the greybox to `02_GAME_DESIGN/GREYBOX_SPEC.md`, nothing outside that scope. 2. Pass the go / no-go, which is spontaneous laughter. 3. Then write the reason-to-buy sentence (Q2).

## 18. DO NOT DO
- Do not build anything outside `02_GAME_DESIGN/GREYBOX_SPEC.md`. The out-of-scope list there is binding.
- Do not produce final assets.
- Do not turn a hypothesis into a requirement.
- Do not create infrastructure without a documented need.

## Writing style
Docs are in English. No em dashes: use a comma, parenthesis or colon. Concision.
