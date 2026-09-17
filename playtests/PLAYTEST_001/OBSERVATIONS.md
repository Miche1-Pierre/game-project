# Observations

_Facts, not opinions._

## What the tester did
Played the greybox solo, picked up and carried objects.

## What they reported
"Le portage des objets est correct, très légèrement trop flottant."
Carry judged correct in principle, weight not readable in the hold.

## What the code showed
`PlayerGrab.FixedUpdate` assigned `linearVelocity` outright every physics step,
which cancels gravity and inertia. A held object hung at eye level with no mass,
whatever it weighed. The float was not a tuning value being slightly off, it was
the control model having no weight term at all.

## Two defects found while investigating, neither reported by the tester
- Every swapped object carried **two colliders**. The pack prefabs ship their own
  MeshColliders and the visual swap nested them without stripping. A concave
  MeshCollider on a dynamic Rigidbody is invalid: Unity logged an error and the
  collision response was wrong. This may have contributed to the reported feel.
- The player spawned **facing a blank wall** 1.5 m away with every object behind
  them. The first frame of the build showed nothing.

## After the fixes
"C'est parfait." Carry validated.
