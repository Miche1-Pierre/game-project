using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Movers
{
    // The loading screen's mover: runs, trips over a box (or slips on a puddle), goes down,
    // gets up and runs on. The clips are played with a small Playables mixer on the crew body
    // (no controller asset needed), cross-faded, on a clock the loading screen hands in, so
    // the long frame when the next scene wakes up cannot skip the gag.
    //
    // Clips it can use, all optional (LoadingStageSettings):
    //   Crew_Run                       the loop (else the body's own controller at run speed)
    //   Crew_Fall                      the trip: dive, belly, back up into the run stride
    //   Crew_KnockedDown, Crew_GetUp   the slip: on the back, then up
    // Without the fall clips (CHARACTERS not installed yet) the whole body tumbles by code
    // around its feet instead: forward for the box, backward for the puddle.
    //
    // WorldSpeed tells the scenery how fast to scroll: the mover stops while he lies there.
    public sealed class RunnerAnimator
    {
        public enum Phase { Running, Falling, Slipping, GettingUp }

        const int Run = 0, Fall = 1, Down = 2, Up = 3, Clips = 4;

        readonly Animator animator;
        readonly Transform pivot;
        readonly LoadingStageSettings settings;
        readonly AnimationClip[] clips = new AnimationClip[Clips];
        readonly float[] weights = new float[Clips];
        readonly float[] targets = new float[Clips];
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[Clips];
        bool useGraph;
        float fade = 0.15f;

        float phaseTime;          // seconds in the current phase
        float phaseLength;        // how long the current clip lasts at its speed
        bool coded;               // the current trip is the code tumble
        float codedSign;          // -1 forward (box), +1 backward (puddle)
        Quaternion pivotRest;
        Vector3 pivotRestPos;

        public Phase Current { get; private set; } = Phase.Running;
        public bool IsRunning => Current == Phase.Running;
        public float WorldSpeed { get; private set; } = 1f;
        public bool HasFallClip => clips[Fall] != null;
        public bool HasSlipClips => clips[Down] != null && clips[Up] != null;
        public bool UsesClips => useGraph;
        public int Trips { get; private set; }
        // How far through the current phase, 0..1 (tests and the dust puffs read it).
        public float PhaseProgress => phaseLength > 0f ? Mathf.Clamp01(phaseTime / phaseLength) : 0f;
        // Seconds of animation played (the stage clock, long frames clamped).
        public float Seconds { get; private set; }
        // Degrees the left thigh has turned since the first moments: a running mover adds
        // hundreds a second, a frozen or T-posed one (clips that do not bind, a graph that
        // never advances) stays near 0. The Play-mode tests require it to grow.
        public float PoseMotion { get; private set; }
        public bool CanMeasurePose => probe != null;

        // Ignores the jump from the prefab's rest pose to the first animated pose.
        const float PoseSettle = 0.12f;
        readonly Transform probe;
        Quaternion probeLast;

        public RunnerAnimator(Animator animator, Transform pivot, LoadingStageSettings settings)
        {
            this.animator = animator;
            this.pivot = pivot;
            this.settings = settings;
            if (pivot != null)
            {
                pivotRest = pivot.localRotation;
                pivotRestPos = pivot.localPosition;
            }
            if (animator == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.isHuman) probe = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            if (probe != null) probeLast = probe.localRotation;

            clips[Run] = settings.run;
            clips[Fall] = settings.fall;
            clips[Down] = settings.knockedDown;
            clips[Up] = settings.getUp;

            if (clips[Run] != null && animator.avatar != null)
            {
                useGraph = true;
                animator.runtimeAnimatorController = null;
                graph = PlayableGraph.Create("MoversLoadingRunner");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph, Clips);
                for (int i = 0; i < Clips; i++)
                {
                    if (clips[i] == null) continue;
                    playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                    graph.Connect(playables[i], 0, mixer, i);
                }
                var output = AnimationPlayableOutput.Create(graph, "Runner", animator);
                output.SetSourcePlayable(mixer);
                weights[Run] = targets[Run] = 1f;
                ApplyWeights();
                graph.Play();
            }
            else
            {
                // No run clip: the crew's own controller runs at its run speed.
                animator.SetFloat("Speed", settings.runSpeed);
            }
        }

        // Starts a trip now: `slip` is the puddle (on the back), else the box (on the face).
        public void Trip(bool slip)
        {
            if (!IsRunning) return;
            Trips++;
            phaseTime = 0f;
            coded = !useGraph || (slip ? !HasSlipClips : !HasFallClip);
            codedSign = slip ? 1f : -1f;
            if (coded)
            {
                Current = slip ? Phase.Slipping : Phase.Falling;
                phaseLength = 1.6f;
                return;
            }
            if (slip)
            {
                Current = Phase.Slipping;
                Play(Down, Mathf.Max(0.1f, settings.knockedDownSpeed), 0.08f);
            }
            else
            {
                Current = Phase.Falling;
                Play(Fall, Mathf.Max(0.1f, settings.fallSpeed), 0.1f);
            }
        }

        void Play(int clip, float speed, float fadeTime)
        {
            for (int i = 0; i < Clips; i++) targets[i] = i == clip ? 1f : 0f;
            fade = fadeTime;
            if (playables[clip].IsValid())
            {
                playables[clip].SetTime(0);
                playables[clip].SetSpeed(speed);
            }
            phaseLength = clips[clip] != null ? clips[clip].length / speed : 1f;
        }

        public void Tick(float dt)
        {
            // A frame longer than this (the scene waking up) is taken as this: the gag slows
            // down for one frame rather than jumping to its end.
            dt = Mathf.Min(dt, 0.1f);
            phaseTime += dt;

            if (coded) TickCoded(dt);
            else TickClips(dt);

            if (useGraph)
            {
                for (int i = 0; i < Clips; i++)
                    weights[i] = Mathf.MoveTowards(weights[i], targets[i], dt / Mathf.Max(0.01f, fade));
                ApplyWeights();
                // The run loops by itself only if its clip loops; wrap it anyway.
                if (playables[Run].IsValid() && clips[Run].length > 0f && playables[Run].GetTime() > clips[Run].length * 4f)
                    playables[Run].SetTime(playables[Run].GetTime() % clips[Run].length);
                graph.Evaluate(dt);
            }

            Seconds += dt;
            if (probe != null)
            {
                // Without the graph the Animator poses the body after Update: this reads the
                // last frame's pose, which is just as good for "is it moving".
                Quaternion q = probe.localRotation;
                if (Seconds > PoseSettle) PoseMotion += Quaternion.Angle(probeLast, q);
                probeLast = q;
            }
        }

        void TickClips(float dt)
        {
            float u = PhaseProgress;
            switch (Current)
            {
                case Phase.Running:
                    WorldSpeed = Mathf.MoveTowards(WorldSpeed, 1f, dt * 3f);
                    break;
                case Phase.Falling:
                    // Momentum for a quarter of the fall, still on the ground, back up to a run
                    // at the end (Crew_Fall ends in the run stride).
                    WorldSpeed = u < 0.25f ? 1f - Ease(u / 0.25f) : u < 0.72f ? 0f : Ease((u - 0.72f) / 0.28f);
                    if (phaseTime >= phaseLength - 0.12f) BackToRun(0.14f);
                    break;
                case Phase.Slipping:
                    WorldSpeed = u < 0.35f ? 1f - Ease(u / 0.35f) : 0f;
                    if (phaseTime >= phaseLength - 0.06f)
                    {
                        Current = Phase.GettingUp;
                        phaseTime = 0f;
                        Play(Up, Mathf.Max(0.1f, settings.getUpSpeed), 0.1f);
                    }
                    break;
                case Phase.GettingUp:
                    WorldSpeed = u < 0.7f ? 0f : Ease((u - 0.7f) / 0.3f);
                    if (phaseTime >= phaseLength - 0.12f) BackToRun(0.16f);
                    break;
            }
        }

        void BackToRun(float fadeTime)
        {
            Current = Phase.Running;
            phaseTime = 0f;
            phaseLength = 0f;
            Play(Run, 1f, fadeTime);
        }

        // The code tumble: the whole body turns about its feet, down, a little bounce, a moment
        // lying there, back up.
        void TickCoded(float dt)
        {
            if (Current == Phase.Running)
            {
                WorldSpeed = Mathf.MoveTowards(WorldSpeed, 1f, dt * 3f);
                return;
            }
            float t = phaseTime;
            float angle, lift;
            if (t < 0.32f) { float k = t / 0.32f; angle = 86f * k * k; lift = 0f; WorldSpeed = 1f - k; }
            else if (t < 0.5f) { float k = (t - 0.32f) / 0.18f; angle = 86f - Mathf.Sin(k * Mathf.PI) * 9f; lift = 0.12f * k; WorldSpeed = 0f; }
            else if (t < 1.1f) { angle = 86f; lift = 0.12f; WorldSpeed = 0f; }
            else if (t < 1.55f) { float k = Ease((t - 1.1f) / 0.45f); angle = 86f * (1f - k); lift = 0.12f * (1f - k); WorldSpeed = k * 0.6f; }
            else
            {
                angle = 0f; lift = 0f;
                Current = Phase.Running;
                coded = false;
                phaseLength = 0f;
                phaseTime = 0f;
            }
            if (pivot != null)
            {
                pivot.localRotation = pivotRest * Quaternion.Euler(0f, 0f, angle * codedSign);
                pivot.localPosition = pivotRestPos + Vector3.up * lift;
            }
            if (useGraph && playables[Run].IsValid()) playables[Run].SetSpeed(Current == Phase.Running ? 1f : 0.35f);
        }

        void ApplyWeights()
        {
            float sum = 0f;
            for (int i = 0; i < Clips; i++) if (clips[i] != null) sum += weights[i];
            for (int i = 0; i < Clips; i++)
                if (clips[i] != null) mixer.SetInputWeight(i, sum > 0f ? weights[i] / sum : (i == Run ? 1f : 0f));
        }

        static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
