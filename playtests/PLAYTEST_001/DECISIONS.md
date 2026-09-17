# Decisions

| Item | Decision |
|---|---|
| Carry control model | KEEP. Judged correct, not rewritten. |
| Weight in the hold | MODIFY. Hold point drops 6 mm per kg, capped at 70 cm. |
| Follow strength | MODIFY. 14 to 10, so the object lags behind the aim. |
| Angular damping while carried | MODIFY. 6 to 2.5, it was freezing all sway. |
| Colliders on swapped visuals | FIX. Stripped, one collider per object now. |
| Spawn position | FIX. Outside, between truck and house, facing the opening. |

The four carry values are inspector fields, so the feel can be retuned without
touching code. `followStrength` is the first dial if it ever reads too heavy.
