using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The one place that talks to her Animator (AC_Grandma_Slice, SLICE_ARCHITECTURE section
    // 13): states are entered with CrossFadeInFixedTime by name, the walk blend follows
    // SetFloat("Speed").
    //
    // Every state is looked up first. A clip that has not been authored yet, or the old
    // one-state AC_Grandma, degrades to her idle ("Locomotion" on the base layer, nothing on the
    // upper body) instead of an Animator error, so the brain can be played before the clips exist.
    [DisallowMultipleComponent]
    public sealed class GrandmaAnimation : MonoBehaviour
    {
        // State names of AC_Grandma_Slice that the code asks for (the activity clips are named
        // by ActivitySpot). One list, so a renamed state is one edit.
        public const string Locomotion = "Locomotion";
        public const string GiveKeys = "Give_Keys";
        public const string Talk = "Talk";
        public const string ShakeFist = "Angry_ShakeFist";
        public const string HandsOnHips = "Angry_HandsOnHips";
        public const string Point = "Angry_Point";
        public const int BaseLayer = 0;
        public const int UpperLayer = 1;

        // Which anger fits: pointing at a culprit, hands on hips at rudeness, a fist at breakage.
        public static string AngerFor(StimulusKind kind)
        {
            switch (kind)
            {
                case StimulusKind.TheftWitnessed:
                case StimulusKind.CarryingSeen:
                case StimulusKind.Smoking:
                case StimulusKind.Drinking: return Point;
                case StimulusKind.Bumped:
                case StimulusKind.SeatTaken:
                case StimulusKind.BehindSchedule: return HandsOnHips;
                default: return ShakeFist;
            }
        }

        public Animator animator;
        public float crossFade = 0.25f;
        public string upperEmptyState = "Empty";
        [Tooltip("Seconds over which Speed follows her real speed.")]
        public float speedDamping = 0.1f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");

        RuntimeAnimatorController resolvedFor;
        bool hasSpeed;
        // (layer, short name hash) -> the hash the Animator answers to, or 0 when it has no such state.
        readonly Dictionary<long, int> resolved = new Dictionary<long, int>();

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        bool Usable => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;

        void Resolve()
        {
            if (resolvedFor == animator.runtimeAnimatorController) return;
            resolvedFor = animator.runtimeAnimatorController;
            resolved.Clear();
            hasSpeed = false;
            foreach (var p in animator.parameters)
                if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
        }

        // The hash to cross-fade to, or 0 when this layer has no such state. HasState is asked
        // with the short name first, then with the full path, and the answer is kept.
        int Lookup(string state, int layer)
        {
            if (string.IsNullOrEmpty(state) || layer >= animator.layerCount) return 0;
            int shortHash = Animator.StringToHash(state);
            long key = ((long)layer << 32) | (uint)shortHash;
            if (resolved.TryGetValue(key, out int hash)) return hash;

            hash = 0;
            if (animator.HasState(layer, shortHash)) hash = shortHash;
            else
            {
                int full = Animator.StringToHash(animator.GetLayerName(layer) + "." + state);
                if (animator.HasState(layer, full)) hash = full;
            }
            resolved[key] = hash;
            return hash;
        }

        public bool Has(string state, int layer = BaseLayer)
        {
            if (!Usable) return false;
            Resolve();
            return Lookup(state, layer) != 0;
        }

        // Base layer. Returns false when the state is missing (she then shows her idle).
        public bool Play(string state)
        {
            if (!Usable) return false;
            Resolve();
            int hash = Lookup(state, BaseLayer);
            bool found = hash != 0;
            if (!found) hash = Lookup(Locomotion, BaseLayer);
            if (hash != 0) CrossFade(hash, BaseLayer);
            return found;
        }

        // Upper-body layer (talk, anger), which plays over whatever her legs do.
        public bool PlayUpper(string state)
        {
            if (!Usable) return false;
            Resolve();
            int hash = Lookup(state, UpperLayer);
            if (hash == 0) return false;
            CrossFade(hash, UpperLayer);
            return true;
        }

        public void StopUpper()
        {
            if (!Usable) return;
            Resolve();
            int hash = Lookup(upperEmptyState, UpperLayer);
            if (hash != 0) CrossFade(hash, UpperLayer);
        }

        void CrossFade(int hash, int layer)
        {
            // Already there, or on the way: a second cross-fade would restart it.
            if (animator.IsInTransition(layer))
            {
                var next = animator.GetNextAnimatorStateInfo(layer);
                if (next.shortNameHash == hash || next.fullPathHash == hash) return;
            }
            else
            {
                var cur = animator.GetCurrentAnimatorStateInfo(layer);
                if (cur.shortNameHash == hash || cur.fullPathHash == hash) return;
            }
            if (Net.IsHost) GrandmaSync.SendAnim(layer, hash);
            animator.CrossFadeInFixedTime(hash, crossFade, layer);
        }

        // Online client (GrandmaSync Anim): the host's cross-fade, with the same early-outs.
        public void ApplyCrossFade(int hash, int layer)
        {
            if (!Usable || hash == 0 || layer < 0 || layer >= animator.layerCount) return;
            CrossFade(hash, layer);
        }

        // Online client, snapshot: straight into a state at a point of its clip.
        public void Play(int hash, int layer, float normalizedTime)
        {
            if (!Usable || hash == 0 || layer < 0 || layer >= animator.layerCount || !animator.HasState(layer, hash)) return;
            animator.Play(hash, layer, normalizedTime);
        }

        public int LayerCount => Usable ? animator.layerCount : 0;

        // Host, snapshot: the state a layer is in (or fading to) and how far through it.
        public bool CurrentState(int layer, out int hash, out float normalizedTime)
        {
            hash = 0;
            normalizedTime = 0f;
            if (!Usable || layer < 0 || layer >= animator.layerCount) return false;
            var info = animator.IsInTransition(layer) ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
            hash = info.fullPathHash;
            normalizedTime = info.loop ? Mathf.Repeat(info.normalizedTime, 1f) : Mathf.Clamp01(info.normalizedTime);
            return hash != 0;
        }

        public void SetSpeed(float metresPerSecond)
        {
            if (!Usable) return;
            Resolve();
            if (hasSpeed) animator.SetFloat(SpeedHash, metresPerSecond, speedDamping, Time.deltaTime);
        }
    }
}
