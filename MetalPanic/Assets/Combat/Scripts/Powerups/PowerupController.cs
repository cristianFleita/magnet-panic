using System.Collections.Generic;
using MagnetPanic.Combat.Scoring;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Owns the single-powerup-active state. Receives mission-driven grants
    /// through <see cref="IPowerupBroker"/>, rolls a powerup against the
    /// supplied weights, cancels any in-flight effect, then runs the new one
    /// for its duration.
    ///
    /// Effect instances are <c>new</c>'d once at <see cref="Awake"/> and reused
    /// every activation — keeps allocations off the gameplay path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerupController : MonoBehaviour, IPowerupBroker
    {
        [Header("References (auto-resolved)")]
        [SerializeField] ArkhamPlayerMotor motor;
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] ScoringRuntime scoring;

        [Header("Slow Time")]
        [SerializeField, Range(0.1f, 1f)] float slowTimeScale = 0.4f;
        [SerializeField, Min(1f)] float slowTimeDuration = 8f;

        [Header("Overload Pulse")]
        [SerializeField, Min(0.1f)] float pulseInterval = 1.0f;
        [SerializeField, Min(1f)] float pulseRadius = 6f;
        [SerializeField, Min(0)] int pulseDamage = 1;
        [SerializeField, Min(0.1f)] float pulseKnockbackDistance = 1.6f;
        [SerializeField, Min(1f)] float pulseDuration = 10f;
        [Tooltip("Optional one-shot VFX spawned at the player each pulse beat.")]
        [SerializeField] GameObject pulseVfxPrefab;

        [Header("Magnetic Mine")]
        [Tooltip("Prefab spawned at the player's position. Must carry a SphereCollider (its radius doubles as both the trigger range and the AoE damage radius) and a MagneticMineBehaviour component.")]
        [SerializeField] GameObject mineEntityPrefab;
        [SerializeField, Min(0)] int mineDamage = 4;
        [SerializeField, Min(1)] int mineMagnetizeMarks = 2;
        [SerializeField, Min(0.1f)] float mineArmTime = 0.5f;
        [SerializeField, Min(1f)] float mineTimeout = 30f;
        [Tooltip("One-shot VFX prefab spawned at the mine's position the moment it detonates.")]
        [SerializeField] GameObject mineVfxPrefab;

        [Header("Events")]
        public UnityEvent<PowerupId> OnPowerupActivated = new UnityEvent<PowerupId>();
        public UnityEvent<PowerupId> OnPowerupDeactivated = new UnityEvent<PowerupId>();
        public UnityEvent<PowerupId, float> OnPowerupTimerChanged = new UnityEvent<PowerupId, float>(); // id, remaining

        readonly Dictionary<PowerupId, IPowerupEffect> effects = new Dictionary<PowerupId, IPowerupEffect>();
        readonly PowerupContext context = new PowerupContext();

        IPowerupEffect active;
        float remaining;

        public PowerupId ActiveId => active != null ? active.Id : PowerupId.None;
        public float Remaining => Mathf.Max(0f, remaining);
        public float SlowTimeScale => slowTimeScale;
        public float SlowTimeDuration => slowTimeDuration;
        public float PulseInterval => pulseInterval;
        public float PulseRadius => pulseRadius;
        public int PulseDamage => pulseDamage;
        public float PulseKnockbackDistance => pulseKnockbackDistance;
        public float PulseDuration => pulseDuration;
        public GameObject PulseVfxPrefab => pulseVfxPrefab;
        public GameObject MineEntityPrefab => mineEntityPrefab;
        public int MineDamage => mineDamage;
        public int MineMagnetizeMarks => mineMagnetizeMarks;
        public float MineArmTime => mineArmTime;
        public float MineTimeout => mineTimeout;
        public GameObject MineVfxPrefab => mineVfxPrefab;

        void Awake()
        {
            ResolveReferences();

            effects[PowerupId.SlowTime] = new SlowTimeEffect();
            effects[PowerupId.OverloadPulse] = new OverloadPulseEffect();
            effects[PowerupId.MagneticMine] = new MagneticMineEffect();
        }

        void OnDisable()
        {
            CancelActive(runEnded: true);
        }

        void OnDestroy()
        {
            CancelActive(runEnded: true);
        }

        void Update()
        {
            if (active == null)
                return;

            // Use unscaled delta so SlowTime's own timeScale=0.4 doesn't make
            // its timer drift; gameplay timers stay in real seconds.
            float dt = Time.unscaledDeltaTime;
            active.Tick(dt);

            if (remaining > 0f)
            {
                remaining -= dt;
                OnPowerupTimerChanged.Invoke(active.Id, Mathf.Max(0f, remaining));
                if (remaining <= 0f)
                    Deactivate();
            }
        }

        // ---------- IPowerupBroker ----------

        public void GrantPowerup(PowerupWeights weights)
        {
            PowerupId picked = weights.Total > 0f ? weights.Roll() : PowerupWeights.Uniform.Roll();
            Activate(picked);
        }

        public void GrantRandomPowerup()
        {
            Activate(PowerupWeights.Uniform.Roll());
        }

        public void GrantSpecificPowerup(PowerupId id)
        {
            Activate(id);
        }

        // ---------- Public API ----------

        public void Activate(PowerupId id)
        {
            if (id == PowerupId.None)
                return;
            if (!effects.TryGetValue(id, out IPowerupEffect effect) || effect == null)
            {
                Debug.LogWarning($"[PowerupController] No effect registered for {id}.", this);
                return;
            }

            CancelActive(runEnded: false);

            context.Controller = this;
            context.Motor = motor;
            context.EnemyManager = enemyManager;
            context.Player = motor != null ? motor.transform : transform;
            context.ActivationPosition = context.Player != null ? context.Player.position : transform.position;

            active = effect;
            remaining = effect.Duration;
            effect.Activate(context);

            OnPowerupActivated.Invoke(active.Id);
            OnPowerupTimerChanged.Invoke(active.Id, remaining);
        }

        public void CancelActive()
        {
            CancelActive(runEnded: false);
        }

        // ---------- Internal ----------

        void Deactivate()
        {
            if (active == null)
                return;
            PowerupId finishedId = active.Id;
            active.Deactivate(context, runEnded: false);
            active = null;
            remaining = 0f;
            OnPowerupDeactivated.Invoke(finishedId);
            OnPowerupTimerChanged.Invoke(PowerupId.None, 0f);
        }

        void CancelActive(bool runEnded)
        {
            if (active == null)
                return;
            PowerupId finishedId = active.Id;
            active.Deactivate(context, runEnded);
            active = null;
            remaining = 0f;
            if (!runEnded)
            {
                OnPowerupDeactivated.Invoke(finishedId);
                OnPowerupTimerChanged.Invoke(PowerupId.None, 0f);
            }
        }

        void ResolveReferences()
        {
            if (motor == null)
                motor = GetComponentInParent<ArkhamPlayerMotor>();
            if (motor == null)
                motor = FindFirstObjectByType<ArkhamPlayerMotor>();
            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();
        }
    }
}
