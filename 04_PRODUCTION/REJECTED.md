# REJECTED

_What we have explicitly decided NOT to do. Prevents reinventing an already-eliminated idea (human or AI)._

| Rejected idea | Reason | Date | Revisit if |
|---|---|---|---|
| (example) Rich NPC detection AI | Too expensive and unreadable for two, replaceable by player-driven detection | 2026-09-15 | AI budget and readability solved |
| Breakable floors and stairs | Breaking the ground you stand on is a different game: the house has to stay walkable for the carry, and a hole to the cellar ends every route through it. Everything else in the house breaks (ADR-008). Amended 2026-09-26 (ADR-009): roofs still never break, but they now fall as whole sections when nothing holds them up | 2026-09-25 | Destruction turns out to be the core verb |
| Asset workflow: "native MCP operations before Python" | The Blender MCP only changes a scene through Python anyway. The versioned generator script is the source and runs headless; the MCP is for looking at a scene live (ADR-011) | 2026-09-27 | The MCP gains operations a script cannot express |
| Asset workflow: a dedicated `AssetValidation.unity` scene | The real context already exists: `Map01_PierreKit_House`, with its mirror and the 1.80 m body. `MoversWearCLI` shoots there in Play mode and never saves | 2026-09-27 | The map can no longer host the wear test |
| Asset workflow: GLB as an option, URP | Every importer setting and every tool is FBX, and the project is Built-in RP in Gamma colour space (`03_TECHNICAL/UNITY_SETUP.md`) | 2026-09-27 | The render pipeline changes, in its own ADR |
| Asset workflow: automated import, prefab and collider for environment pieces | Pierre removed the prefab factories on 2026-09-20 (commit 7e70bd7) and assembles Map 1 by hand. A carried object's collider is a design decision. A material remap by name reuses `MoversMaterialFixCLI` | 2026-09-27 | Assembling by hand becomes the bottleneck, and Pierre asks |
| Asset workflow: reference tiers as folders | Moving files breaks Unity GUIDs and Pierre's paths. A register of globs in `05_ART/style/profile.json` says the same without moving anything | 2026-09-27 | Files have to move for another reason |
| Asset workflow: a YAML spec per asset | Blender ships without PyYAML. The generator's docstring is the spec, announced before building | 2026-09-27 | Specs need to be read by something other than the generator |
| Asset workflow: six skills | Three cover what the pilot actually ran: `make-asset`, `asset-to-unity`, `art-style` | 2026-09-27 | Another step keeps being run by hand |
| Asset workflow: multi-agent judging by default | A ranked vote exhausted the session limit during the pilot, and the human verdict is the gate anyway | 2026-09-27 | A cheap judge agrees with the team's eye on real cases |
| Asset workflow: automatic tier promotion | The agent's own review passed the robe twice where the team's eyes did not. A tier changes only with a named approver and a date | 2026-09-27 | Not planned |
| Asset workflow: fine-tuning a model on the style | The proposal itself concludes against it, and the style is not final (ART_DIRECTION section 16) | 2026-09-27 | The final direction is decided and hundreds of assets remain |

_Status: living. Every discarded idea lands here with its reason._