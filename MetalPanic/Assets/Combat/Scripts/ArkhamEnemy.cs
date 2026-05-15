using System.Collections;
using System.Collections.Generic;
using MagnetPanic.Combat.Scoring;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArkhamEnemy : MonoBehaviour, IMarkable, IPoolable
    {
        static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");
        static readonly int StrafeHash = Animator.StringToHash("Strafe");
        static readonly int StrafeDirectionHash = Animator.StringToHash("StrafeDirection");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DeathHash = Animator.StringToHash("Death");
        static readonly int AirPunchHash = Animator.StringToHash("AirPunch");

        enum MoveMode
        {
            None,
            StrafeLeft,
            StrafeRight,
            Approach,
            Retreat
        }

        [Header("Definition")]
        [SerializeField] EnemyDefinition definition;

        [Header("References")]
        [SerializeField] ArkhamEnemyManager manager;
        [SerializeField] ArkhamCombatController playerCombat;
        [SerializeField] Animator animator;
        [SerializeField] CharacterController characterController;
        [SerializeField] CombatHealth combatHealth;
        [SerializeField] WorldSpaceHealthBar healthBar;
        [SerializeField] GameObject chargeTelegraph;
        [Tooltip("Spawned over this enemy's head when the player lands a counter against it. The player carries the pre-counter 'magnetic sense' cue instead.")]
        [SerializeField] GameObject counterStunVfxPrefab;
        [SerializeField] float counterStunVfxHeight = 2.15f;
        [SerializeField] float counterStunVfxLifetime = 1.05f;

        [Header("Stats")]
        [SerializeField] int maxHealth = 3;
        [SerializeField] int magneticMarksToMagnetize = 2;
        [SerializeField] float stunDuration = 0.45f;
        [SerializeField, Tooltip("Stun duration applied when this enemy gets countered by the player. Spider-Man style: a beat of helplessness.")]
        float counterStunDuration = 1f;
        [SerializeField] float knockbackDistance = 0.55f;
        [SerializeField] float knockbackDuration = 0.16f;
        [Header("Counter Profile")]
        [SerializeField, Tooltip("When false, the player's counter cannot interrupt this enemy's telegraph. Heavy archetypes use this to force dodges.")]
        bool canBeCountered = true;
        [SerializeField, Tooltip("Threat-token cost the Attack Director spends to send this enemy to attack. Heavy/elite enemies cost 2.")]
        [Range(1, 3)] int attackTokenCost = 1;
        [SerializeField, Tooltip("Seconds after spawn before this enemy is eligible to be chosen by the Attack Director (Spider-Man new-enemy grace).")]
        float spawnAttackGracePeriod = 1.2f;
        [SerializeField] bool destroyOnDeath = true;
        [SerializeField] float deathDespawnDelay = 0.8f;
        [SerializeField] bool autoCreateHealthBar = true;
        [SerializeField] float healthBarHeight = 2.35f;

        [Header("Magnetism")]
        [SerializeField] MagneticMarkState markState = MagneticMarkState.Normal;
        [SerializeField] float markDecayTime = 6f;
        [SerializeField] float magneticMass = 3f;
        [SerializeField] float magneticPullSnapSharpness = 18f;
        [SerializeField] float magnetizedRepelDuration = 0.42f;
        [SerializeField, Tooltip("Extra hit radius added to the projectile center when sweeping for enemy-vs-enemy collisions while flying as a magnet repel projectile. Combined with the target's CharacterController radius.")]
        float magnetizedProjectileRadius = 0.8f;
        [SerializeField] float markedProjectileDamageMultiplier = 1.2f;
        [SerializeField] float wallSlamBounceDistance = 0.18f;
        [SerializeField] float counterPulseDistance = 1.1f;
        [SerializeField] bool alwaysPullableByMagnet;
        [SerializeField] GameObject magnetizedIndicator;
        [SerializeField] bool autoCreateMagnetizedIndicator = true;
        [SerializeField] float magnetizedIndicatorHeight = 2.15f;
        [SerializeField] Color magnetizedIndicatorColor = new Color(1f, 0.82f, 0.16f, 0.85f);

        [Header("Debug")]
        [SerializeField, Tooltip("Print Console messages and draw debug lines for magnet-repel projectile collisions against other enemies.")]
        bool debugMagnetProjectileCollisions = false;

        [Header("Movement")]
        [SerializeField] float strafeSpeed = 1.25f;
        [SerializeField] float approachSpeed = 5f;
        [SerializeField] float retreatSpeed = 2.25f;
        [SerializeField] float retreatDistance = 4.25f;
        [SerializeField] bool disableStrafe;

        [Header("Pathfinding")]
        [Tooltip("Route Approach movement around static arena obstacles using NavMesh.CalculatePath. Falls back to straight-line steering if no NavMesh is baked or no path is found.")]
        [SerializeField] bool useNavMeshPathing = true;
        [Tooltip("Seconds between path recomputations. Lower = more reactive, higher = cheaper. ~6 Hz is plenty for a 1v1 chase.")]
        [SerializeField, Min(0.05f)] float pathRecomputeInterval = 0.18f;
        [Tooltip("How far the player must move from the last sample to force an early recompute.")]
        [SerializeField, Min(0.1f)] float pathTargetMoveThreshold = 1.25f;
        [Tooltip("Distance to the current corner that counts as arrival; the follower then advances to the next corner.")]
        [SerializeField, Min(0.1f)] float pathArrivalThreshold = 0.55f;
        [SerializeField] bool drawPathGizmo;

        [Header("Attack")]
        [SerializeField] float prepareAttackTime = 0.35f;
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float attackHitDelay = 0.2f;
        [SerializeField] float attackRecovery = 0.55f;
        [SerializeField, Tooltip("Tolerance factor applied to attackRange when validating the impact frame. Player outside this radius takes no damage.")]
        float hitRangeTolerance = 1.25f;
        [SerializeField] bool useLinearCharge;
        [SerializeField] float chargeSpeed = 11f;
        [SerializeField] float chargeDuration = 0.55f;
        [SerializeField] float chargeHitRadius = 1.1f;
        [SerializeField, Tooltip("Damage applied when the linear charge connects with the player.")]
        int chargeDamage = 2;
        [SerializeField, Tooltip("When true the charge impact triggers the player's Hit_Knockback state.")]
        bool chargeCausesKnockdown = true;
        [SerializeField, Tooltip("Ideal distance for linear-charge enemies to stay from the player while idle.")]
        float chargeIdealDistance = 5f;
        [SerializeField, Tooltip("Reposition tolerance around chargeIdealDistance: bot retreats below distance-tolerance, approaches above distance+tolerance.")]
        float chargeIdealTolerance = 1.2f;

        [Header("Scoring")]
        [Tooltip("If true, kills against this enemy award the boss XP bonus from ScoringConfig.")]
        [SerializeField] bool isBoss;

        [Header("Events")]
        public UnityEvent<ArkhamEnemy> OnDamaged = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnDeath = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnMagnetized = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnCountered = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnAnchored = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnAnchorReleased = new UnityEvent<ArkhamEnemy>();

        KillMethod lastDamageMethod = KillMethod.Unknown;
        KillMethod pendingDamageMethod = KillMethod.Unknown;
        public KillMethod LastDamageMethod => lastDamageMethod;
        public bool IsBoss => isBoss;

        /// <summary>
        /// Lets external damage sources tag the next call into one of this
        /// enemy's damage entry points with a specific kill method (e.g. an
        /// overload or an enemy-as-projectile collision). The tag is consumed
        /// by the next damage call and reset afterwards.
        /// </summary>
        public void TagNextDamageMethod(KillMethod method)
        {
            pendingDamageMethod = method;
        }

        KillMethod ConsumePendingMethod(KillMethod fallback)
        {
            if (pendingDamageMethod != KillMethod.Unknown)
            {
                KillMethod method = pendingDamageMethod;
                pendingDamageMethod = KillMethod.Unknown;
                return method;
            }
            return fallback;
        }

        int magneticMarks;
        bool _isPreparingAttack;
        public bool isPreparingAttack 
        { 
            get => _isPreparingAttack; 
            set => _isPreparingAttack = value; 
        }

        bool isAttacking;
        bool isRetreating;
        bool isLockedTarget;
        bool isStunned;
        bool isDead;
        bool isMagnetized;
        bool isMagneticallyControlled;
        bool isAnchorHeld;
        bool attackHitApplied;
        float lastMarkTime = -999f;
        float spawnTime = -999f;
        bool forcedKeepDistance;
        float forcedKeepDistanceTarget = 6.5f;
        MoveMode moveMode;
        Coroutine behaviorCoroutine;
        Coroutine movementCoroutine;
        ArenaSystem arenaSystem;
        bool isMagnetRepelProjectile;
        bool isExecutingLinearCharge;
        Vector3 lastArenaWallHitNormal;
        readonly List<Collider> ignoredEnemyColliders = new List<Collider>();

        // Behavior add-ons (detected at runtime)
        SpitterDroneBehavior spitterDrone;
        GrapplerBehavior grappler;

        readonly EnemyNavPath navPath = new EnemyNavPath();

        /// <summary>
        /// Multiplier applied to the enemy's own locomotion (approach + linear
        /// charge). Player-driven motion (magnetic pull / repel / knockback)
        /// is intentionally not scaled — those are external forces, not the
        /// enemy's volition. Used by <see cref="Powerups.SlowTimeEffect"/> to
        /// "slow the world" without touching Time.timeScale.
        /// </summary>
        public float ExternalSpeedMultiplier { get; set; } = 1f;

        public bool IsAlive => !isDead && isActiveAndEnabled && combatHealth != null && combatHealth.IsAlive;
        public bool IsStunned => isStunned;
        public bool IsAttackable => IsAlive && !isLockedTarget;
        /// <summary>
        /// True while this enemy is mid-attack — used by the Attack Director to know
        /// when an attack slot is still occupied. Includes uncounterable enemies.
        /// </summary>
        public bool IsCounterable => IsAlive && (isPreparingAttack || isAttacking);
        /// <summary>
        /// True only when the player's counter can legitimately punish this enemy's
        /// telegraph. Heavy archetypes return false even while attacking.
        /// </summary>
        public bool IsCounterTarget => IsCounterable && canBeCountered;
        public bool CanBeCountered => canBeCountered;

        /// <summary>
        /// True when this enemy's attack is geometrically close enough to land
        /// THIS beat (melee in range, runner mid-dash, spitter firing). Used by
        /// the player's CounterSenseIndicator so the cue only blinks when the
        /// player actually needs to react — not for every distant windup.
        /// </summary>
        public bool IsImminentThreat
        {
            get
            {
                if (!IsAlive)
                    return false;

                // Shooter actively firing — the projectile is leaving the barrel.
                if (spitterDrone != null && spitterDrone.IsFiring)
                    return true;

                // Mid-attack (already past windup): the hit is one frame away.
                if (isAttacking)
                    return true;

                // Windup: only flag as imminent when the enemy is geometrically
                // close enough to actually connect. Use generous tolerance so
                // the cue flips on a beat before the strike, not after.
                if (isPreparingAttack && playerCombat != null)
                {
                    float dist = DistanceToPlayer();
                    float threshold;
                    if (useLinearCharge)
                        threshold = chargeIdealDistance + chargeIdealTolerance + 1.5f;
                    else
                        threshold = attackRange * hitRangeTolerance + 0.5f;

                    return dist <= threshold;
                }

                return false;
            }
        }

        public bool IsImminentCounterThreat => IsImminentThreat && canBeCountered;

        public bool IsShooter => spitterDrone != null;
        public bool IsShooterFiring => spitterDrone != null && spitterDrone.IsFiring;

        /// <summary>
        /// Called by <see cref="ArkhamEnemyManager"/> to mark this enemy as a
        /// reserve — too many bots are already in the player's close-combat
        /// ring, so this one should orbit at <paramref name="targetDistance"/>
        /// instead of crowding in. Mid-attack or pre-attack enemies are not
        /// forced (the manager skips them).
        /// </summary>
        public void SetForcedKeepDistance(bool keep, float targetDistance)
        {
            forcedKeepDistance = keep;
            if (keep)
                forcedKeepDistanceTarget = Mathf.Max(retreatDistance, targetDistance);
        }

        public bool IsForcedReserve => forcedKeepDistance;
        public int AttackTokenCost => Mathf.Max(1, attackTokenCost);
        public bool IsAttacking => isAttacking;
        public CombatHealth Health => combatHealth;
        public int CurrentHealth => combatHealth != null ? combatHealth.CurrentHealth : 0;
        public int MaxHealth => combatHealth != null ? combatHealth.MaxHealth : maxHealth;
        public bool IsMagnetized => isMagnetized;
        public bool IsAlwaysPullableByMagnet => alwaysPullableByMagnet;
        public int MagneticMarks => magneticMarks;
        public MagneticMarkState MarkState => markState;
        public float MagneticMass => magneticMass;
        public bool CanBePulledByMagnet => IsAlive && IsMagneticPullTarget && !isLockedTarget;
        public bool IsMagneticPullTarget => alwaysPullableByMagnet || markState == MagneticMarkState.Magnetized;
        public float BodyRadius => characterController != null ? characterController.radius : 0.45f;

        public bool CanDirectorSelect =>
            IsAlive &&
            !isLockedTarget &&
            !isStunned &&
            !isMagneticallyControlled &&
            !isPreparingAttack &&
            !isAttacking &&
            !isRetreating &&
            Time.time >= spawnTime + spawnAttackGracePeriod;

        void Awake()
        {
            EnsureEvents();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (combatHealth == null)
                combatHealth = GetComponent<CombatHealth>();

            if (combatHealth == null)
                combatHealth = gameObject.AddComponent<CombatHealth>();

            if (arenaSystem == null)
                arenaSystem = FindFirstObjectByType<ArenaSystem>();

            spitterDrone = GetComponent<SpitterDroneBehavior>();
            grappler = GetComponent<GrapplerBehavior>();

            if (definition != null)
                ApplyDefinitionFields(definition);

            combatHealth.Configure(maxHealth, true);
            EnsureHealthBar();
            EnsureMagnetizedIndicator();
            UpdateMagnetizedIndicator();
            ReleaseAnchor();
            HideChargeTelegraph();
        }

        public void Apply(EnemyDefinition def)
        {
            if (def == null)
                return;

            definition = def;
            ApplyDefinitionFields(def);

            if (combatHealth != null)
                combatHealth.Configure(maxHealth, true);

            UpdateMagnetizedIndicator();
        }

        void ApplyDefinitionFields(EnemyDefinition def)
        {
            maxHealth = def.maxHealth;
            alwaysPullableByMagnet = def.alwaysPullableByMagnet;
            magneticMarksToMagnetize = Mathf.Max(1, def.magneticMarksToMagnetize);
            magneticMass = Mathf.Max(0.5f, def.magneticMass);
            approachSpeed = def.approachSpeed;
            strafeSpeed = def.strafeSpeed;
            retreatSpeed = def.retreatSpeed;
            retreatDistance = def.retreatDistance;
            disableStrafe = def.disableStrafe;
            prepareAttackTime = def.prepareAttackTime;
            attackRange = def.attackRange;
            attackHitDelay = def.attackHitDelay;
            attackRecovery = def.attackRecovery;
            knockbackDistance = def.knockbackDistance;
            knockbackDuration = def.knockbackDuration;
            useLinearCharge = def.useLinearCharge;
            chargeSpeed = def.chargeSpeed;
            chargeDuration = def.chargeDuration;
            chargeHitRadius = def.chargeHitRadius;
            chargeDamage = Mathf.Max(1, def.chargeDamage);
            chargeCausesKnockdown = def.chargeCausesKnockdown;
            chargeIdealDistance = Mathf.Max(0f, def.chargeIdealDistance);
            chargeIdealTolerance = Mathf.Max(0.1f, def.chargeIdealTolerance);
            canBeCountered = def.canBeCountered;
            counterStunDuration = Mathf.Max(0.1f, def.counterStunDuration);
            attackTokenCost = Mathf.Clamp(def.attackTokenCost, 1, 3);
            spawnAttackGracePeriod = Mathf.Max(0f, def.spawnAttackGracePeriod);
        }

        public void ConfigureMagneticProfile(bool alwaysPullable, float mass)
        {
            alwaysPullableByMagnet = alwaysPullable;
            magneticMass = Mathf.Max(0.5f, mass);
            UpdateMagnetizedIndicator();
        }

        void OnEnable()
        {
            ResetRuntimeState();

            if (manager == null)
                manager = GetComponentInParent<ArkhamEnemyManager>();

            if (playerCombat == null)
                playerCombat = FindFirstObjectByType<ArkhamCombatController>();

            manager?.Register(this);
            HideCounterCue();
            StartIdleMovement();
        }

        void OnDisable()
        {
            manager?.Unregister(this);
        }

        public void OnSpawn()
        {
            if (!enabled)
            {
                enabled = true;
                return;
            }

            ResetRuntimeState();
            manager?.Register(this);
            StartIdleMovement();
        }

        public void OnDespawn()
        {
            StopBehaviorCoroutine();
            HideCounterCue();
            ReleaseAnchor();
            SetEnemyCollisionsIgnored(false);
            isDead = true;
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isLockedTarget = false;
            isStunned = false;
            isMagnetized = false;
            isMagneticallyControlled = false;
            isAnchorHeld = false;
            isMagnetRepelProjectile = false;
            isExecutingLinearCharge = false;
            attackHitApplied = false;
            moveMode = MoveMode.None;
            magneticMarks = 0;
            markState = MagneticMarkState.Normal;
            UpdateMagnetizedIndicator();

            if (characterController != null)
                characterController.enabled = false;

            enabled = false;
        }

        void Update()
        {
            DecayMagneticMark();
            FacePlayer();
            TickNavPath();
            Move();
        }

        void TickNavPath()
        {
            if (!useNavMeshPathing || playerCombat == null || !IsAlive || isMagneticallyControlled || isStunned || isLockedTarget)
                return;

            navPath.recomputeInterval = pathRecomputeInterval;
            navPath.targetMovedThreshold = pathTargetMoveThreshold;
            navPath.arrivalThreshold = pathArrivalThreshold;
            navPath.Tick(transform.position, playerCombat.transform.position, Time.deltaTime);

            if (drawPathGizmo)
                navPath.DrawDebug(Color.yellow);
        }

        public void Configure(
            ArkhamEnemyManager enemyManager,
            ArkhamCombatController player,
            Animator targetAnimator,
            GameObject indicator)
        {
            EnsureEvents();

            manager = enemyManager;
            playerCombat = player;
            if (targetAnimator != null)
                animator = targetAnimator;
            // 'indicator' is intentionally ignored — the counter cue moved to the player
            // (CounterSenseIndicator on ArkhamCombatController). The parameter is kept
            // for back-compat with existing call sites.
            _ = indicator;
            if (combatHealth == null)
                combatHealth = GetComponent<CombatHealth>();
            EnsureHealthBar();
            EnsureMagnetizedIndicator();
            UpdateMagnetizedIndicator();
            ReleaseAnchor();
        }

        void EnsureEvents()
        {
            OnDamaged ??= new UnityEvent<ArkhamEnemy>();
            OnDeath ??= new UnityEvent<ArkhamEnemy>();
            OnMagnetized ??= new UnityEvent<ArkhamEnemy>();
            OnCountered ??= new UnityEvent<ArkhamEnemy>();
        }

        public void SetManager(ArkhamEnemyManager enemyManager)
        {
            manager = enemyManager;
        }

        public void LockAsTarget(bool locked)
        {
            isLockedTarget = locked;
            if (locked)
                StopMoving();
        }

        public void BeginAttack()
        {
            if (!CanDirectorSelect)
                return;

            // Grappler: attempt grab before normal attack
            if (grappler != null && grappler.TryGrapple())
                return;

            // Spitter drone: fire projectile instead of melee
            if (spitterDrone != null)
            {
                StopBehaviorCoroutine();
                isExecutingLinearCharge = false;
                SetEnemyCollisionsIgnored(false);
                isPreparingAttack = true;
                ShowCounterCue();
                behaviorCoroutine = StartCoroutine(RangedAttackRoutine());
                return;
            }

            StopBehaviorCoroutine();
            isExecutingLinearCharge = false;
            SetEnemyCollisionsIgnored(false);

            bool wantsCharge = useLinearCharge && DistanceToPlayer() > attackRange;
            behaviorCoroutine = StartCoroutine(wantsCharge ? LinearChargeRoutine() : AttackRoutine());
        }

        public void BeginRetreat()
        {
            if (!IsAlive || isLockedTarget)
                return;

            StopBehaviorCoroutine();
            behaviorCoroutine = StartCoroutine(RetreatRoutine());
        }

        public void CounteredBy(ArkhamCombatController attacker)
        {
            if (!IsAlive)
                return;

            // Heavy archetypes shouldn't be counterable — guard at the entry point
            // so external callers can't bypass the rule via direct CounteredBy().
            if (!canBeCountered)
                return;

            playerCombat = attacker;
            OnCountered.Invoke(this);
            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isExecutingLinearCharge = false;
            SetEnemyCollisionsIgnored(false);
            isRetreating = false;
            isStunned = true;
            isMagneticallyControlled = false;
            SetMarkState(MagneticMarkState.Magnetized);
            StopMoving();
            SpawnCounterStunVfx();

            Vector3 direction = transform.position - attacker.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            behaviorCoroutine = StartCoroutine(MagneticPushRoutine(direction.normalized, counterPulseDistance, 0.12f, counterStunDuration));
        }

        void SpawnCounterStunVfx()
        {
            if (counterStunVfxPrefab == null)
                return;

            Vector3 worldPos = transform.position + Vector3.up * counterStunVfxHeight;
            GameObject instance = Instantiate(counterStunVfxPrefab, worldPos, Quaternion.identity, transform);
            instance.name = counterStunVfxPrefab.name + " (CounterStun)";
            float life = counterStunVfxLifetime > 0f ? counterStunVfxLifetime : counterStunDuration;
            Destroy(instance, life);
        }

        /// <summary>
        /// Forces the enemy into stun state for a specified duration.
        /// Used by GrapplerBehavior when the player escapes the grapple.
        /// </summary>
        public void ForceStun(float duration)
        {
            if (!IsAlive)
                return;

            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isExecutingLinearCharge = false;
            SetEnemyCollisionsIgnored(false);
            isRetreating = false;
            isStunned = true;
            StopMoving();

            if (animator != null)
                animator.SetTrigger(HitHash);

            behaviorCoroutine = StartCoroutine(StunRoutine(duration));
        }

        public void TakeStrike(ArkhamCombatController attacker, int damage, bool wasCounter)
        {
            if (!IsAlive)
                return;

            playerCombat = attacker;
            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isExecutingLinearCharge = false;
            SetEnemyCollisionsIgnored(false);
            isRetreating = false;
            isStunned = true;
            isLockedTarget = false;

            // If this enemy was grappling the player, release them
            if (grappler != null && grappler.IsGrappling)
                grappler.InterruptGrapple();
            StopMoving();

            ApplyMark(1);
            lastDamageMethod = ConsumePendingMethod(KillMethod.Strike);
            combatHealth.ApplyDamage(Mathf.Max(1, damage));
            OnDamaged.Invoke(this);

            if (!combatHealth.IsAlive)
            {
                Die();
                return;
            }

            if (animator != null)
                animator.SetTrigger(HitHash);

            Vector3 direction = (transform.position - attacker.transform.position).normalized;
            direction.y = 0f;
            behaviorCoroutine = StartCoroutine(HitReactionRoutine(direction, knockbackDistance));
        }

        public void HitEvent()
        {
            TryApplyAttackHit();
        }

        bool TryApplyAttackHit()
        {
            if (playerCombat == null || attackHitApplied || !isAttacking)
                return false;

            bool isCharge = isExecutingLinearCharge && !isMagnetRepelProjectile;

            float allowedRange = isMagnetRepelProjectile
                ? float.PositiveInfinity
                : isCharge
                    ? Mathf.Max(chargeHitRadius, attackRange * 0.5f) * hitRangeTolerance
                    : attackRange * hitRangeTolerance;

            if (DistanceToPlayer() > allowedRange)
                return false;

            attackHitApplied = true;

            if (isCharge)
                playerCombat.ReceiveDamage(this, Mathf.Max(1, chargeDamage), chargeCausesKnockdown);
            else
                playerCombat.ReceiveDamage(this);

            return true;
        }

        void SetEnemyCollisionsIgnored(bool ignore)
        {
            if (characterController == null)
                return;

            if (ignore)
            {
                ignoredEnemyColliders.Clear();
                if (manager == null)
                    return;

                IReadOnlyList<ArkhamEnemy> others = manager.Enemies;
                for (int i = 0; i < others.Count; i++)
                {
                    ArkhamEnemy other = others[i];
                    if (other == null || other == this)
                        continue;

                    CharacterController otherCC = other.GetComponent<CharacterController>();
                    if (otherCC == null || !otherCC.enabled)
                        continue;

                    Physics.IgnoreCollision(characterController, otherCC, true);
                    ignoredEnemyColliders.Add(otherCC);
                }
            }
            else
            {
                for (int i = 0; i < ignoredEnemyColliders.Count; i++)
                {
                    Collider col = ignoredEnemyColliders[i];
                    if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                        continue;

                    Physics.IgnoreCollision(characterController, col, false);
                }

                ignoredEnemyColliders.Clear();
            }
        }

        public void ApplyMark(int stacks)
        {
            if (!IsAlive)
                return;

            int stackCount = Mathf.Max(1, stacks);
            magneticMarks = Mathf.Clamp(magneticMarks + stackCount, 0, magneticMarksToMagnetize);
            lastMarkTime = Time.time;

            if (magneticMarks >= magneticMarksToMagnetize)
                SetMarkState(MagneticMarkState.Magnetized);
            else
                SetMarkState(MagneticMarkState.Marked);
        }

        public void SetMarkState(MagneticMarkState state)
        {
            MagneticMarkState previous = markState;
            markState = state;
            lastMarkTime = Time.time;

            switch (markState)
            {
                case MagneticMarkState.Normal:
                    magneticMarks = 0;
                    break;
                case MagneticMarkState.Marked:
                    magneticMarks = Mathf.Max(1, Mathf.Min(magneticMarks, magneticMarksToMagnetize - 1));
                    break;
                case MagneticMarkState.Magnetized:
                    magneticMarks = magneticMarksToMagnetize;
                    break;
                case MagneticMarkState.Stunned:
                    break;
            }

            isMagnetized = markState == MagneticMarkState.Magnetized;

            if (isMagnetized && previous != MagneticMarkState.Magnetized)
                OnMagnetized.Invoke(this);

            UpdateMagnetizedIndicator();
        }

        public float GetTimeSinceLastMark()
        {
            return lastMarkTime < 0f ? float.PositiveInfinity : Time.time - lastMarkTime;
        }

        public void BeginMagneticPull()
        {
            if (!CanBePulledByMagnet)
                return;

            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isStunned = true;
            isMagneticallyControlled = true;
            StopMoving();
        }

        public void MagnetPullTowards(Vector3 point, float pullSpeed, float deltaTime)
        {
            if (!CanBePulledByMagnet)
                return;

            if (!isMagneticallyControlled)
                BeginMagneticPull();

            point.y = transform.position.y;
            float speed = pullSpeed / Mathf.Max(0.5f, magneticMass);
            Vector3 next = Vector3.MoveTowards(transform.position, point, speed * deltaTime);
            MoveBy(next - transform.position);
        }

        public void EnterMagneticOrbit()
        {
            if (!IsAlive)
                return;

            SetMarkState(MagneticMarkState.Magnetized);
            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isStunned = true;
            isMagneticallyControlled = true;
            StopMoving();
        }

        public void TickMagneticOrbit(Vector3 orbitPosition, float deltaTime)
        {
            if (!IsAlive || !isMagneticallyControlled)
                return;

            orbitPosition.y = transform.position.y;
            float t = 1f - Mathf.Exp(-magneticPullSnapSharpness * deltaTime);
            Vector3 next = Vector3.Lerp(transform.position, orbitPosition, t);
            MoveBy(next - transform.position);
        }

        public void CancelMagneticPull()
        {
            if (!isMagneticallyControlled)
                return;

            isMagneticallyControlled = false;
            isStunned = false;
            ReleaseAnchor();
            StartIdleMovement();
        }

        public void AnchorMagneticHold()
        {
            if (!IsAlive)
                return;

            StopMoving();

            if (playerCombat != null)
            {
                Vector3 lookPosition = playerCombat.transform.position;
                lookPosition.y = transform.position.y;
                Vector3 direction = lookPosition - transform.position;
                if (direction.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(direction);
            }

            if (!isAnchorHeld)
            {
                isAnchorHeld = true;
                OnAnchored.Invoke(this);
            }
        }

        void ReleaseAnchor()
        {
            if (!isAnchorHeld)
                return;

            isAnchorHeld = false;
            OnAnchorReleased.Invoke(this);
        }

        public void RejectMagneticPull(Vector3 center, float speed)
        {
            if (!IsAlive)
                return;

            StopBehaviorCoroutine();
            Vector3 direction = transform.position - center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            isStunned = true;
            isMagneticallyControlled = false;
            ReleaseAnchor();
            behaviorCoroutine = StartCoroutine(MagneticPushRoutine(direction.normalized, speed * 0.12f, 0.12f, stunDuration * 0.5f));
        }

        public void MagnetRepel(Vector3 direction, float speed, int impactDamage, int recoilDamage = -1)
        {
            if (!IsAlive)
                return;

            StopBehaviorCoroutine();
            HideCounterCue();
            isExecutingLinearCharge = false;
            SetEnemyCollisionsIgnored(false);
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            SetMarkState(MagneticMarkState.Normal);
            isStunned = true;
            isMagneticallyControlled = true;
            isMagnetRepelProjectile = true;
            ReleaseAnchor();

            int resolvedRecoil = recoilDamage < 0 ? impactDamage : recoilDamage;
            behaviorCoroutine = StartCoroutine(MagneticRepelRoutine(direction.normalized, speed, impactDamage, resolvedRecoil));
        }

        public void ReceiveMagneticImpact(int damage, Vector3 sourcePosition, float impactKnockbackDistance, bool clearsMagnetized)
        {
            if (!IsAlive)
                return;

            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isExecutingLinearCharge = false;
            SetEnemyCollisionsIgnored(false);
            isRetreating = false;
            isLockedTarget = false;
            isStunned = true;
            isMagneticallyControlled = false;
            ReleaseAnchor();
            StopMoving();

            float multiplier = markState == MagneticMarkState.Marked || markState == MagneticMarkState.Magnetized
                ? markedProjectileDamageMultiplier
                : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
            lastDamageMethod = ConsumePendingMethod(KillMethod.Repel);
            combatHealth.ApplyDamage(finalDamage);

            if (clearsMagnetized && markState == MagneticMarkState.Magnetized)
                SetMarkState(MagneticMarkState.Normal);

            OnDamaged.Invoke(this);

            if (!combatHealth.IsAlive)
            {
                Die();
                return;
            }

            if (animator != null)
                animator.SetTrigger(HitHash);

            Vector3 direction = transform.position - sourcePosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            behaviorCoroutine = StartCoroutine(HitReactionRoutine(direction.normalized, impactKnockbackDistance));
        }

        IEnumerator AttackRoutine()
        {
            isPreparingAttack = true;
            ShowCounterCue();
            yield return new WaitForSeconds(prepareAttackTime);

            isPreparingAttack = false;
            isAttacking = true;
            attackHitApplied = false;
            moveMode = MoveMode.Approach;

            // Short safety window: if the player slipped out during the windup,
            // close the gap for up to ~1.2s and then commit the swing. Anything
            // longer makes the attack feel sluggish (e.g. the MetalEnemy "thinks
            // about it" forever bug).
            float approachTimer = 0f;
            const float maxAttackCloseTime = 1.2f;
            while (IsAlive && playerCombat != null && DistanceToPlayer() > attackRange && approachTimer < maxAttackCloseTime)
            {
                approachTimer += Time.deltaTime;
                yield return null;
            }

            StopMoving();

            bool inRange = playerCombat != null && DistanceToPlayer() <= attackRange * hitRangeTolerance;

            if (inRange && animator != null)
                animator.SetTrigger(AirPunchHash);

            if (inRange)
            {
                yield return new WaitForSeconds(attackHitDelay);
                TryApplyAttackHit();
            }

            yield return new WaitForSeconds(attackRecovery);

            isAttacking = false;
            HideCounterCue();
            behaviorCoroutine = null;
        }

        IEnumerator LinearChargeRoutine()
        {
            isPreparingAttack = true;
            moveMode = MoveMode.None;
            StopMoving();
            ShowCounterCue();
            ShowChargeTelegraph();

            float prepTimer = 0f;
            while (prepTimer < prepareAttackTime && IsAlive)
            {
                prepTimer += Time.deltaTime;
                moveMode = MoveMode.None;
                FacePlayer();
                yield return null;
            }

            HideChargeTelegraph();
            isPreparingAttack = false;
            isAttacking = true;
            isExecutingLinearCharge = true;
            attackHitApplied = false;
            moveMode = MoveMode.None;

            Vector3 chargeDirection = transform.forward;
            if (playerCombat != null)
            {
                Vector3 toPlayer = playerCombat.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                    chargeDirection = toPlayer.normalized;
            }

            if (animator != null)
                animator.SetTrigger(AirPunchHash);

            SetEnemyCollisionsIgnored(true);

            float dashTimer = 0f;
            float hitRadiusSqr = chargeHitRadius * chargeHitRadius;
            while (dashTimer < chargeDuration && IsAlive)
            {
                dashTimer += Time.deltaTime;
                CollisionFlags flags = MoveBy(chargeDirection * chargeSpeed * ExternalSpeedMultiplier * Time.deltaTime);

                if (!attackHitApplied && playerCombat != null)
                {
                    Vector3 delta = playerCombat.transform.position - transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= hitRadiusSqr)
                        TryApplyAttackHit();
                }

                if ((flags & CollisionFlags.Sides) != 0)
                {
                    TryApplyAttackHit();
                    break;
                }

                yield return null;
            }

            if (!attackHitApplied)
                TryApplyAttackHit();

            isExecutingLinearCharge = false;
            SetEnemyCollisionsIgnored(false);

            yield return new WaitForSeconds(attackRecovery);

            isAttacking = false;
            HideCounterCue();
            behaviorCoroutine = null;
        }

        IEnumerator RangedAttackRoutine()
        {
            isPreparingAttack = true;
            ShowCounterCue();
            yield return new WaitForSeconds(prepareAttackTime);

            isPreparingAttack = false;
            isAttacking = true;

            if (spitterDrone != null && IsAlive)
                spitterDrone.FireProjectile();

            // Wait for the spitter to finish its burst
            if (spitterDrone != null)
            {
                float safety = 0f;
                while (spitterDrone.IsFiring && IsAlive && safety < 4f)
                {
                    safety += Time.deltaTime;
                    yield return null;
                }
            }

            yield return new WaitForSeconds(attackRecovery);

            isAttacking = false;
            HideCounterCue();
            behaviorCoroutine = null;
        }

        IEnumerator RetreatRoutine()
        {
            isRetreating = true;
            moveMode = MoveMode.Retreat;
            yield return new WaitUntil(() => playerCombat == null || DistanceToPlayer() >= retreatDistance || !IsAlive);
            isRetreating = false;
            StopMoving();
            StartIdleMovement();
            behaviorCoroutine = null;
        }

        IEnumerator HitReactionRoutine(Vector3 direction, float distance)
        {
            float elapsed = 0f;
            Vector3 totalOffset = direction * distance;
            Vector3 applied = Vector3.zero;

            while (elapsed < knockbackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / knockbackDuration);
                Vector3 next = Vector3.Lerp(Vector3.zero, totalOffset, t);
                MoveBy(next - applied);
                applied = next;
                yield return null;
            }

            yield return StunRoutine(stunDuration);
        }

        IEnumerator MagneticPushRoutine(Vector3 direction, float distance, float duration, float finalStunDuration)
        {
            float elapsed = 0f;
            Vector3 totalOffset = direction * distance;
            Vector3 applied = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 next = Vector3.Lerp(Vector3.zero, totalOffset, t);
                MoveBy(next - applied);
                applied = next;
                yield return null;
            }

            yield return StunRoutine(finalStunDuration);
        }

        IEnumerator MagneticRepelRoutine(Vector3 direction, float speed, int impactDamage, int recoilDamage)
        {
            float elapsed = 0f;
            HashSet<ArkhamEnemy> hitEnemies = new HashSet<ArkhamEnemy>();

            if (debugMagnetProjectileCollisions)
                Debug.Log($"[MagnetRepel] {name} launched dir={direction} speed={speed:F2} impactDmg={impactDamage} recoilDmg={recoilDamage} hitRadius={magnetizedProjectileRadius:F2}", this);

            while (elapsed < magnetizedRepelDuration && IsAlive)
            {
                elapsed += Time.deltaTime;
                lastArenaWallHitNormal = Vector3.zero;
                Vector3 prevPosition = transform.position;
                CollisionFlags collision = MoveBy(direction * speed * Time.deltaTime);
                DamageEnemiesTouchedByMagneticProjectile(hitEnemies, impactDamage, recoilDamage, prevPosition);
                if (!IsAlive)
                    yield break;

                if (HitArenaWall(collision) || EscapedArenaBounds())
                {
                    Vector3 wallNormal = lastArenaWallHitNormal.sqrMagnitude > 0.001f
                        ? lastArenaWallHitNormal
                        : arenaSystem != null
                            ? arenaSystem.GetNearestWallNormal(transform.position)
                            : -direction;

                    ApplyArenaWallSlam(wallNormal, speed);
                    if (!IsAlive)
                        yield break;

                    break;
                }

                yield return null;
            }

            isMagnetRepelProjectile = false;
            isMagneticallyControlled = false;
            yield return StunRoutine(stunDuration);
        }

        IEnumerator StunRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            isStunned = false;
            StartIdleMovement();
            behaviorCoroutine = null;
        }

        void StartIdleMovement()
        {
            if (!IsAlive || !isActiveAndEnabled || isLockedTarget || isStunned || isMagneticallyControlled)
                return;

            if (movementCoroutine != null)
                StopCoroutine(movementCoroutine);

            movementCoroutine = StartCoroutine(IdleMovementRoutine());
        }

        IEnumerator IdleMovementRoutine()
        {
            while (IsAlive && !isLockedTarget && !isStunned && !isPreparingAttack && !isAttacking && !isRetreating)
            {
                // Reserve enemies (over capacity in the player's close-combat ring)
                // orbit at forcedKeepDistanceTarget until a slot opens up. This is
                // what stops 8 Scraplings from glomming onto the player at once.
                if (forcedKeepDistance)
                {
                    float reserveDist = DistanceToPlayer();
                    if (reserveDist < forcedKeepDistanceTarget - 0.6f)
                        moveMode = MoveMode.Retreat;
                    else if (reserveDist > forcedKeepDistanceTarget + 1.5f)
                        moveMode = MoveMode.Approach;
                    else
                        moveMode = Random.value > 0.5f ? MoveMode.StrafeLeft : MoveMode.StrafeRight;

                    yield return new WaitForSeconds(Random.Range(0.25f, 0.55f));
                    continue;
                }

                // Ranged enemy: maintain ideal distance like a linear charger
                if (spitterDrone != null)
                {
                    float idealDist = spitterDrone.GetIdealDistance();
                    float idealTol = spitterDrone.GetIdealTolerance();
                    float distance = DistanceToPlayer();
                    if (distance > idealDist + idealTol)
                        moveMode = MoveMode.Approach;
                    else if (distance < idealDist - idealTol)
                        moveMode = MoveMode.Retreat;
                    else
                        moveMode = Random.value > 0.5f ? MoveMode.StrafeLeft : MoveMode.StrafeRight;
                }
                else if (useLinearCharge)
                {
                    float distance = DistanceToPlayer();
                    if (distance > chargeIdealDistance + chargeIdealTolerance)
                        moveMode = MoveMode.Approach;
                    else if (distance < chargeIdealDistance - chargeIdealTolerance && distance > attackRange * 0.85f)
                        moveMode = MoveMode.Retreat;
                    else
                        moveMode = Random.value > 0.5f ? MoveMode.StrafeLeft : MoveMode.StrafeRight;
                }
                else if (disableStrafe)
                {
                    moveMode = MoveMode.Approach;
                }
                else
                {
                    if (DistanceToPlayer() > attackRange * 1.5f)
                    {
                        int random = Random.Range(0, 10);
                        if (random < 6)
                            moveMode = MoveMode.Approach;
                        else
                            moveMode = Random.value > 0.5f ? MoveMode.StrafeLeft : MoveMode.StrafeRight;
                    }
                    else
                    {
                        moveMode = Random.value > 0.5f ? MoveMode.StrafeLeft : MoveMode.StrafeRight;
                    }
                }

                yield return new WaitForSeconds(
                    useLinearCharge || spitterDrone != null
                        ? Random.Range(0.18f, 0.32f)
                        : Random.Range(0.7f, 1.2f));
            }

            movementCoroutine = null;
        }

        void FacePlayer()
        {
            if (playerCombat == null || !IsAlive)
                return;

            Vector3 lookPosition = playerCombat.transform.position;
            lookPosition.y = transform.position.y;

            Vector3 direction = lookPosition - transform.position;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
        }

        void Move()
        {
            if (!IsAlive || playerCombat == null || isLockedTarget || isStunned || isMagneticallyControlled)
            {
                AnimateMove(0f, false, 0f);
                return;
            }

            Vector3 direction = Vector3.zero;
            float speed = 0f;
            bool strafing = false;
            float strafeDirection = 0f;

            Vector3 toPlayer = playerCombat.transform.position - transform.position;
            toPlayer.y = 0f;
            Vector3 playerDirection = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
            Vector3 perpendicular = Quaternion.AngleAxis(90f, Vector3.up) * playerDirection;

            switch (moveMode)
            {
                case MoveMode.StrafeLeft:
                    direction = -perpendicular;
                    speed = strafeSpeed;
                    strafing = true;
                    strafeDirection = -1f;
                    break;
                case MoveMode.StrafeRight:
                    direction = perpendicular;
                    speed = strafeSpeed;
                    strafing = true;
                    strafeDirection = 1f;
                    break;
                case MoveMode.Approach:
                    direction = useNavMeshPathing && navPath.HasValidPath
                        ? navPath.GetSteerDirection(transform.position, playerDirection)
                        : playerDirection;
                    speed = approachSpeed;
                    break;
                case MoveMode.Retreat:
                    direction = -playerDirection;
                    speed = retreatSpeed;
                    break;
            }

            if (direction.sqrMagnitude > 0.01f)
            {
                if (manager != null)
                {
                    // Wider separation + a tangential nudge: instead of pushing
                    // straight away from a neighbor (which causes head-on jams
                    // when two enemies want the same slot), we mix in a sideways
                    // component so they ring around each other and form an arc.
                    Vector3 separation = Vector3.zero;
                    int neighbors = 0;
                    const float separationRadius = 2.6f;
                    const float sqrRadius = separationRadius * separationRadius;

                    for (int i = 0; i < manager.Enemies.Count; i++)
                    {
                        var other = manager.Enemies[i];
                        if (other == null || !other.IsAlive || other == this)
                            continue;

                        Vector3 diff = transform.position - other.transform.position;
                        diff.y = 0f;
                        float sqrDist = diff.sqrMagnitude;

                        if (sqrDist > 0.01f && sqrDist < sqrRadius)
                        {
                            float dist = Mathf.Sqrt(sqrDist);
                            float falloff = 1f - (dist / separationRadius);
                            Vector3 radial = diff / dist;
                            // Tangential component perpendicular to the radial — sign
                            // is stable per pair (hash-based) so two enemies don't
                            // oscillate trying to pass each other.
                            int sign = (other.GetInstanceID() < GetInstanceID()) ? 1 : -1;
                            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x) * sign;
                            separation += (radial + tangent * 0.45f) * falloff;
                            neighbors++;
                        }
                    }

                    if (neighbors > 0)
                    {
                        direction += (separation / neighbors) * 1.8f;
                        direction.Normalize();
                    }
                }

                MoveBy(direction * speed * ExternalSpeedMultiplier * Time.deltaTime);
            }

            AnimateMove((speed * ExternalSpeedMultiplier) / approachSpeed, strafing, strafeDirection);
        }

        void AnimateMove(float magnitude, bool strafing, float strafeDirection)
        {
            if (animator == null)
                return;

            // Keep the run animation playing by default while the enemy is alive
            // and not hard-stopped (stun, death, lock). This guarantees the
            // locomotion blend tree never drops to a static idle pose between
            // moveMode transitions.
            if (IsAlive && !isStunned && !isLockedTarget && !isMagneticallyControlled)
                magnitude = Mathf.Max(magnitude, 0.4f);

            animator.SetFloat(InputMagnitudeHash, magnitude, 0.15f, Time.deltaTime);
            animator.SetBool(StrafeHash, strafing);
            animator.SetFloat(StrafeDirectionHash, strafeDirection, 0.15f, Time.deltaTime);
        }

        void StopMoving()
        {
            moveMode = MoveMode.None;
            AnimateMove(0f, false, 0f);
        }

        void StopBehaviorCoroutine()
        {
            if (behaviorCoroutine != null)
                StopCoroutine(behaviorCoroutine);

            behaviorCoroutine = null;

            if (movementCoroutine != null)
                StopCoroutine(movementCoroutine);

            movementCoroutine = null;

            if (isExecutingLinearCharge)
            {
                isExecutingLinearCharge = false;
                SetEnemyCollisionsIgnored(false);
            }
        }

        void DecayMagneticMark()
        {
            if (isMagneticallyControlled || markState == MagneticMarkState.Normal || markState == MagneticMarkState.Stunned)
                return;

            if (GetTimeSinceLastMark() < markDecayTime)
                return;

            if (markState == MagneticMarkState.Magnetized)
                SetMarkState(MagneticMarkState.Marked);
            else
                SetMarkState(MagneticMarkState.Normal);
        }

        void DamageEnemiesTouchedByMagneticProjectile(HashSet<ArkhamEnemy> hitEnemies, int impactDamage, int recoilDamage, Vector3 previousPosition)
        {
            if (manager == null)
                return;

            Vector3 segStart = previousPosition;
            Vector3 segEnd = transform.position;
            segStart.y = 0f;
            segEnd.y = 0f;

            float projectileBodyRadius = BodyRadius;

            if (debugMagnetProjectileCollisions)
            {
                Vector3 drawA = previousPosition + Vector3.up * 1f;
                Vector3 drawB = transform.position + Vector3.up * 1f;
                Debug.DrawLine(drawA, drawB, Color.cyan, 1.5f, false);
            }

            IReadOnlyList<ArkhamEnemy> enemies = manager.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || enemy == this || !enemy.IsAlive || hitEnemies.Contains(enemy))
                    continue;

                Vector3 target = enemy.transform.position;
                target.y = 0f;

                float combinedRadius = magnetizedProjectileRadius + projectileBodyRadius + enemy.BodyRadius;
                float distSqr = SqrDistanceFromPointToSegment(target, segStart, segEnd);

                if (distSqr > combinedRadius * combinedRadius)
                {
                    if (debugMagnetProjectileCollisions)
                    {
                        float dist = Mathf.Sqrt(distSqr);
                        if (dist <= combinedRadius * 1.5f)
                            Debug.Log($"[MagnetRepel] {name} MISS {enemy.name} segDist={dist:F2} threshold={combinedRadius:F2}", this);
                    }
                    continue;
                }

                hitEnemies.Add(enemy);
                Vector3 contactPoint = enemy.transform.position;

                if (debugMagnetProjectileCollisions)
                {
                    float dist = Mathf.Sqrt(distSqr);
                    Debug.Log($"[MagnetRepel] {name} HIT {enemy.name} segDist={dist:F2} threshold={combinedRadius:F2} impactDmg={impactDamage} recoilDmg={recoilDamage}", this);
                    Debug.DrawLine(transform.position + Vector3.up * 1f, contactPoint + Vector3.up * 1f, Color.red, 1.5f, false);
                }

                enemy.TagNextDamageMethod(KillMethod.EnemyRepel);
                enemy.ReceiveMagneticImpact(impactDamage, transform.position, knockbackDistance * 1.5f, false);
                ApplyProjectileRecoil(recoilDamage, contactPoint);

                if (!IsAlive)
                    return;
            }
        }

        static float SqrDistanceFromPointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 1e-6f)
                return (point - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / abSqr);
            Vector3 projection = a + ab * t;
            return (point - projection).sqrMagnitude;
        }

        void ApplyProjectileRecoil(int damage, Vector3 contactPoint)
        {
            if (!IsAlive || combatHealth == null || damage <= 0)
                return;

            lastDamageMethod = KillMethod.EnemyRepel;
            combatHealth.ApplyDamage(damage);
            OnDamaged.Invoke(this);

            if (!combatHealth.IsAlive)
            {
                Die();
                return;
            }

            if (animator != null)
                animator.SetTrigger(HitHash);
        }

        CollisionFlags MoveBy(Vector3 displacement)
        {
            if (characterController != null && characterController.enabled)
                return characterController.Move(displacement);
            else
            {
                transform.position += displacement;
                return CollisionFlags.None;
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!isMagnetRepelProjectile || hit == null || hit.collider == null)
                return;

            if (!IsArenaWallCollider(hit.collider) || Mathf.Abs(hit.normal.y) > 0.35f)
                return;

            lastArenaWallHitNormal = hit.normal;
        }

        bool HitArenaWall(CollisionFlags collision)
        {
            return isMagnetRepelProjectile
                && (collision & CollisionFlags.Sides) != 0
                && lastArenaWallHitNormal.sqrMagnitude > 0.001f;
        }

        bool EscapedArenaBounds()
        {
            if (!isMagnetRepelProjectile || arenaSystem == null || arenaSystem.IsInsideArena(transform.position))
                return false;

            lastArenaWallHitNormal = arenaSystem.GetNearestWallNormal(transform.position);
            transform.position = arenaSystem.ClampToArena(transform.position);
            return true;
        }

        bool IsArenaWallCollider(Collider target)
        {
            int arenaWallLayer = LayerMask.NameToLayer("ArenaWall");
            return arenaWallLayer >= 0 && target.gameObject.layer == arenaWallLayer;
        }

        void ApplyArenaWallSlam(Vector3 wallNormal, float impactSpeed)
        {
            isMagnetRepelProjectile = false;

            if (arenaSystem == null)
                arenaSystem = FindFirstObjectByType<ArenaSystem>();

            int damage = arenaSystem != null
                ? arenaSystem.CalculateWallSlamDamage(impactSpeed)
                : 2 + Mathf.FloorToInt(Mathf.Max(0f, impactSpeed) / 5f);

            ReceiveArenaWallSlam(damage, wallNormal);
            arenaSystem?.ReportWallSlam(gameObject, wallNormal, impactSpeed, damage);
        }

        public void ReceiveArenaWallSlam(int damage, Vector3 wallNormal)
        {
            if (!IsAlive)
                return;

            lastDamageMethod = KillMethod.WallSlam;
            combatHealth.ApplyDamage(Mathf.Max(0, damage));
            OnDamaged.Invoke(this);

            if (!combatHealth.IsAlive)
            {
                Die();
                return;
            }

            if (animator != null)
                animator.SetTrigger(HitHash);

            Vector3 bounce = wallNormal;
            bounce.y = 0f;
            if (bounce.sqrMagnitude > 0.001f)
                MoveBy(bounce.normalized * wallSlamBounceDistance);
        }

        void Die()
        {
            isDead = true;
            HideCounterCue();
            ReleaseAnchor();
            SetEnemyCollisionsIgnored(false);
            UpdateMagnetizedIndicator();
            StopBehaviorCoroutine();
            StopMoving();

            // Release player if dying while grappling
            if (grappler != null && grappler.IsGrappling)
                grappler.InterruptGrapple();

            OnDeath.Invoke(this);

            if (animator != null)
                animator.SetTrigger(DeathHash);

            if (characterController != null)
                characterController.enabled = false;

            enabled = false;

            if (destroyOnDeath)
                Pool.Despawn(gameObject, deathDespawnDelay);
        }

        void ResetRuntimeState()
        {
            EnsureEvents();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (combatHealth == null)
                combatHealth = GetComponent<CombatHealth>();

            if (combatHealth == null)
                combatHealth = gameObject.AddComponent<CombatHealth>();

            if (arenaSystem == null)
                arenaSystem = FindFirstObjectByType<ArenaSystem>();

            spitterDrone = GetComponent<SpitterDroneBehavior>();
            grappler = GetComponent<GrapplerBehavior>();

            StopBehaviorCoroutine();
            isDead = false;
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isLockedTarget = false;
            isStunned = false;
            isMagnetized = false;
            isMagneticallyControlled = false;
            isAnchorHeld = false;
            isMagnetRepelProjectile = false;
            isExecutingLinearCharge = false;
            attackHitApplied = false;
            lastArenaWallHitNormal = Vector3.zero;
            lastMarkTime = -999f;
            spawnTime = Time.time;
            moveMode = MoveMode.None;
            magneticMarks = 0;
            markState = MagneticMarkState.Normal;
            lastDamageMethod = KillMethod.Unknown;
            pendingDamageMethod = KillMethod.Unknown;

            navPath.Reset();
            navPath.Randomize();

            combatHealth.Configure(maxHealth, true);

            if (characterController != null)
                characterController.enabled = true;

            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            EnsureHealthBar();
            EnsureMagnetizedIndicator();
            HideCounterCue();
            ReleaseAnchor();
            UpdateMagnetizedIndicator();
        }

        float DistanceToPlayer()
        {
            if (playerCombat == null)
                return float.PositiveInfinity;

            Vector3 delta = playerCombat.transform.position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        /// <summary>
        /// Deprecated — the pre-counter cue moved to the player's head
        /// (CounterSenseIndicator). Kept as a no-op so legacy callers
        /// (e.g. GrapplerBehavior) continue to compile and run. The
        /// player-side indicator already picks up isPreparingAttack.
        /// </summary>
        public void ShowCounterCue()
        {
            // Intentionally empty: counter telegraph now lives on the player.
        }

        public void HideCounterCue()
        {
            HideChargeTelegraph();
        }

        void ShowChargeTelegraph()
        {
            if (chargeTelegraph != null)
                chargeTelegraph.SetActive(true);
        }

        void HideChargeTelegraph()
        {
            if (chargeTelegraph != null)
                chargeTelegraph.SetActive(false);
        }

        void EnsureMagnetizedIndicator()
        {
            if (!autoCreateMagnetizedIndicator || magnetizedIndicator != null)
                return;

            GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cue.name = "Magnetized Cue";
            cue.transform.SetParent(transform, false);
            cue.transform.localPosition = new Vector3(0f, magnetizedIndicatorHeight, 0f);
            cue.transform.localScale = new Vector3(0.72f, 0.018f, 0.72f);

            Collider cueCollider = cue.GetComponent<Collider>();
            if (cueCollider != null)
                DestroyLocalObject(cueCollider);

            Renderer renderer = cue.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateCueMaterial(magnetizedIndicatorColor);

            magnetizedIndicator = cue;
            magnetizedIndicator.SetActive(false);
        }

        void EnsureHealthBar()
        {
            if (!autoCreateHealthBar || healthBar != null)
                return;

            GameObject barObject = new GameObject("Enemy Health Bar");
            barObject.transform.SetParent(transform, false);
            healthBar = barObject.AddComponent<WorldSpaceHealthBar>();
            healthBar.Configure(combatHealth, healthBarHeight);
        }

        void UpdateMagnetizedIndicator()
        {
            if (magnetizedIndicator != null)
                magnetizedIndicator.SetActive(IsAlive && IsMagneticPullTarget);
        }

        static Material CreateCueMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            return new Material(shader)
            {
                color = color
            };
        }

        static void DestroyLocalObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
