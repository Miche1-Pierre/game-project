using UnityEngine;

namespace Movers
{
    // Two-bone IK on a real skeleton (shoulder, elbow, wrist or hip, knee, ankle), applied after
    // the Animator has written the frame's pose. CrewAnimator folds the legs with it when the
    // body crouches as low as the eyes, and FirstPersonHands puts the body's hands where they
    // hold, carry or reach: one skeleton, so the shadow, the other player's view and your own
    // forearms all show the same arm.
    //
    // Bones are aimed from their positions, never from a local axis, so it works on any rig.
    // Weight 0 leaves the pose exactly as animated; in between, the elbow keeps the animated bend
    // plane and turns toward the pole as the weight rises, so a hand that starts reaching never
    // flips its elbow over.
    public static class LimbIK
    {
        // Moves `end` toward `target` (world space) by bending `upper` and `lower`, the joint
        // turned toward `pole` (a world direction). The end keeps the world rotation it was
        // animated with; the caller sets it afterwards if it wants another.
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, float weight)
        {
            if (weight <= 0f || upper == null || lower == null || end == null) return;
            weight = Mathf.Clamp01(weight);

            Vector3 a = upper.position, b = lower.position, c = end.position;
            float la = Vector3.Distance(a, b), lb = Vector3.Distance(b, c);
            if (la < 1e-4f || lb < 1e-4f) return;
            Quaternion endRotation = end.rotation;

            // The animated bend, as a direction off the shoulder-to-wrist line.
            Vector3 reach = c - a;
            Vector3 bent = (b - a) - reach.normalized * Vector3.Dot(b - a, reach.normalized);
            if (bent.sqrMagnitude < 1e-8f) bent = pole;
            Vector3 bendPole = weight >= 1f || pole.sqrMagnitude < 1e-8f
                ? pole : Vector3.Slerp(bent.normalized, pole.normalized, weight);

            Vector3 goal = Vector3.Lerp(c, target, weight);
            Vector3 joint = Joint(a, goal, la, lb, bendPole);

            upper.rotation = Quaternion.FromToRotation(b - a, joint - a) * upper.rotation;
            b = lower.position;
            c = end.position;
            lower.rotation = Quaternion.FromToRotation(c - b, goal - b) * lower.rotation;
            end.rotation = endRotation;
        }

        // Where the middle joint goes, from `root` toward `goal`, bent toward `pole`. The goal is
        // clamped to the limb's length: out of reach, the limb points at it, straight.
        public static Vector3 Joint(Vector3 root, Vector3 goal, float a, float b, Vector3 pole)
        {
            Vector3 d = goal - root;
            float len = d.magnitude;
            if (len < 1e-4f) return root + (pole.sqrMagnitude > 1e-8f ? pole.normalized : Vector3.down) * a;
            Vector3 dir = d / len;
            len = Mathf.Clamp(len, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
            float cos = Mathf.Clamp((a * a + len * len - b * b) / (2f * a * len), -1f, 1f);
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
            Vector3 bend = pole - dir * Vector3.Dot(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.down - dir * Vector3.Dot(Vector3.down, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.forward - dir * Vector3.Dot(Vector3.forward, dir);
            bend.Normalize();
            return root + dir * (a * cos) + bend * (a * sin);
        }
    }
}
