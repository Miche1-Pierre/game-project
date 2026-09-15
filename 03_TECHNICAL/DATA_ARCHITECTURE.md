# DATA ARCHITECTURE

_Where data lives. Kept simple and local, no database._

## Runtime data
- Tuning and content: **ScriptableObject** config assets (items, rules, spawn tables). Designer-editable, no recompile.
- Session state: in-memory, owned by the relevant system.

## Persistence
- **No database, no backend.** Like most of the corpus (Meccha, PEAK, Lethal), the game is client-side over Steam P2P.
- If any persistence is needed (settings, unlocks), local files only (see `SAVE_SYSTEM.md`).
- Steam handles ownership, friends and lobbies; we do not run accounts.

## Rule
Introduce a backend or database only if a concept truly requires it, with an ADR and a cost line in `../08_BUSINESS/COST_MODEL.md`.
