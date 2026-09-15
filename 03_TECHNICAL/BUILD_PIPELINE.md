# BUILD PIPELINE

_Getting from editor to a runnable Windows build._

## Approach
- Target: Windows standalone (IL2CPP for the shipping build; Mono is fine in dev).
- One-command builds via the Unity CLI (`unity build`) so builds are reproducible and CI-friendly.
- Versioned builds; keep a changelog entry (`../changelog/`).
- Steam upload via Steamworks tools later (`../09_STEAM/STEAM_SUBMISSION.md`).

## Rule
The build pipeline must work end to end before the sortable-build milestone (M3). A game that cannot be built and shared is not sortable.
