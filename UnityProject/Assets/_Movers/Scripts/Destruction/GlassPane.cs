using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // One pane of glass, split off a kit module at runtime by HouseDestruction. It lives on a
    // child GameObject named "GlassPane" of the module, local transform identity, with its own
    // MeshFilter, MeshRenderer and MeshCollider.
    //
    // It is its own object, and not just health on the wall, because a window should go long
    // before the wall does: a thrown mug breaks it, a carried chair knocked into it breaks it,
    // and once it is gone the opening is open. Breaking deactivates the pane, which removes its
    // collider, so you can climb or throw things through the hole.
    //
    // Glass cracks before it breaks (ADR-009): below half health it turns milky, and a hit that
    // finds it whole only cracks it unless it is at least twice the pane's health (a blast next
    // to it, a chair swung hard). The second knock finishes it. When it goes it drops 3 to 5
    // real shards and a glitter of particles, instead of the 8 to 16 rigidbodies it used to.
    [DisallowMultipleComponent]
    public class GlassPane : MonoBehaviour, IDamageable
    {
        [Tooltip("Hits at glass speeds (above 2.2 m/s) eat into this.")]
        public float maxHealth = 12f;

        public bool IsBroken { get; private set; }
        public bool IsCracked { get; private set; }
        public float Health { get { Init(); return health; } }
        public float MaxHealth => maxHealth;
        public event System.Action<GlassPane> Broken;

        public BreakMaterial Material => BreakMaterial.Glass;
        public DestructionState State => IsBroken ? DestructionState.Destroyed
                                       : IsCracked ? DestructionState.Damaged : DestructionState.Intact;
        public bool IsGone => IsBroken || !gameObject.activeInHierarchy;
        public Bounds WorldBounds => rend != null ? rend.bounds : new Bounds(transform.position, Vector3.zero);

        // The last hit that landed, for the debug overlay.
        public DamageEvent LastHit { get; private set; }
        public float LastHitTime { get; private set; } = -1f;

        const float HitCooldown = 0.1f;
        const float ArmDelay = 1f;             // nothing settling at load time breaks a window
        const float KgPerSquareMetre = 10f;    // ordinary window glass, about 4 mm
        const float CrackedAt = 0.5f;          // share of health left when it cracks
        const float BreaksAtOnce = 2f;         // x maxHealth: a hit this hard skips the crack
        static readonly Color CrackedColor = new Color(0.9f, 0.94f, 0.97f, 0.78f);

        MeshRenderer rend;
        bool initialized;
        float health;
        float mass;
        float nextHitTime;
        float armedAt;

        static readonly List<Renderer> single = new List<Renderer>(1);
        static MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            rend = GetComponent<MeshRenderer>();
            armedAt = Time.time + ArmDelay;
        }

        // Resolved on first use, because the pane is created with AddComponent and its
        // settings (and its renderer's bounds) are only final after that call returns.
        void Init()
        {
            if (initialized) return;
            initialized = true;
            if (rend == null) rend = GetComponent<MeshRenderer>();
            health = maxHealth;
            mass = PaneMass();
        }

        float PaneMass()
        {
            if (rend == null) return 2f;
            Vector3 s = rend.bounds.size;
            float face = Mathf.Max(s.x * s.y, Mathf.Max(s.y * s.z, s.x * s.z));
            return Mathf.Clamp(face * KgPerSquareMetre, 0.3f, 30f);
        }

        void OnCollisionEnter(Collision c)
        {
            // Unity sends collision messages to disabled components too: disabled means off.
            if (!enabled || IsBroken) return;
            if (c.rigidbody == null) return;
            float now = Time.time;
            if (now < armedAt || now < nextHitTime) return;

            Init();
            if (!ImpactDamage.TryMeasure(c, BreakMaterial.Glass, mass, ImpactDamage.Glass, transform.position, null,
                                         out DamageEvent e))
                return;

            nextHitTime = now + HitCooldown;
            ApplyDamage(e);
        }

        // Kept for older callers: a plain impact, nobody to blame.
        public void ApplyDamage(float amount, Vector3 point, Vector3 impulse)
        {
            ApplyDamage(new DamageEvent(point, impulse, amount, impulse.magnitude, 0f, DamageType.Impact));
        }

        public DamageResult ApplyDamage(in DamageEvent e)
        {
            var before = State;
            if (IsBroken || !enabled || !(e.damage > 0f)) return DamageResult.None(before);
            Init();
            float applied = e.damage * (e.type == DamageType.Blast
                ? DestructionMaterialTable.Current.paneBlastFactor
                : DestructionMaterialTable.Factor(e.type, BreakMaterial.Glass));
            if (!(applied > 0f)) return DamageResult.None(before);
            LastHit = e;
            LastHitTime = Time.time;

            // A whole pane only cracks, unless the hit is hard enough to go straight through.
            if (!IsCracked && applied < maxHealth * BreaksAtOnce)
                health = Mathf.Max(health - applied, Mathf.Min(health, maxHealth * CrackedAt) * 0.5f);
            else
                health -= applied;

            if (health <= 0f) Shatter(e);
            else if (!IsCracked && health <= maxHealth * CrackedAt) Crack(e);
            return new DamageResult { applied = applied, before = before, after = State, removed = IsBroken };
        }

        void Crack(in DamageEvent e)
        {
            IsCracked = true;
            if (rend != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                var shared = rend.sharedMaterial;
                if (shared != null && shared.HasProperty(ColorId))
                {
                    Color c = shared.GetColor(ColorId);
                    rend.GetPropertyBlock(block);
                    block.SetColor(ColorId, new Color(Mathf.Lerp(c.r, CrackedColor.r, 0.7f), Mathf.Lerp(c.g, CrackedColor.g, 0.7f),
                                                      Mathf.Lerp(c.b, CrackedColor.b, 0.7f), Mathf.Max(c.a, CrackedColor.a)));
                    rend.SetPropertyBlock(block);
                }
            }
            ImpactAudio.Play(ImpactAudio.Kind.Glass, e.position, 0.3f, e.instigator);
        }

        // Kept for older callers.
        public void Shatter(Vector3 point, Vector3 impulse)
        {
            Shatter(new DamageEvent(point, impulse, 0f, impulse.magnitude, 0f, DamageType.Impact));
        }

        public void Shatter(in DamageEvent e)
        {
            if (IsBroken) return;
            Init();
            IsBroken = true;
            health = 0f;
            Vector3 at = transform.position;

            if (rend != null && rend.enabled && gameObject.activeInHierarchy)
            {
                Bounds bounds = rend.bounds;
                at = bounds.center;
                // The veranda roof is glass: debris frozen on it must fall once it is gone.
                DebrisManager.WakeInBounds(bounds);
                single.Clear();
                single.Add(rend);
                var table = DestructionMaterialTable.Current;
                MeshShatter.Shatter(single, Random.Range(3, 6), mass, Vector3.zero, e.position, e.ImpulseVector,
                                    table.glassShardLifetime, e.instigator);
                single.Clear();
                DestructionFX.Glass(bounds, e.ImpulseVector / Mathf.Max(1f, mass));
                ImpactAudio.Play(ImpactAudio.Kind.Glass, at, 0.9f, e.instigator);
            }

            gameObject.SetActive(false);
            DestructionEvents.Window(this, at, e.instigator);
            Broken?.Invoke(this);
        }

        // Undoes a break, for the debug reset (Breakable.Revive and ReviveAll call it): full
        // health, clear glass, and the pane with its collider back in the opening. The shards
        // already thrown are not collected; they age out on their own.
        public void Revive()
        {
            bool wasBroken = IsBroken;
            IsBroken = false;
            IsCracked = false;
            initialized = false;
            Init();
            nextHitTime = 0f;
            armedAt = Time.time + ArmDelay;
            if (rend != null) rend.SetPropertyBlock(null);
            if (wasBroken) gameObject.SetActive(true);
        }
    }
}
