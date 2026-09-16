# SAVE SYSTEM

_First question: do we even need saves? Probably minimal._

## Position
- If the game is session-based (a match, then reset), we may need **no save at all** beyond settings.
- Settings and key bindings: a small local file (JSON or PlayerPrefs).
- Any progression or unlocks (only if the concept has them): a local, versioned file.
- **No cloud saves** initially; Steam Cloud can be added later at near-zero code cost if useful.

## Rule
Do not build a save system before a concept requires persistent state.

_Status: likely minimal, confirm with the concept._
