using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] GameObject counterIndicator;
        [SerializeField] ParticleSystem counterParticle = null;
        [SerializeField] GameObject chargeTelegraph;

        [Header("Stats")]
        [SerializeField] int maxHealth = 3;
        [SerializeField] int magneticMarksToMagnetize = 2;
        [SerializeField] float stunDuration = 0.45f;
        [SerializeField] float knockbackDistance = 0.55f;
        [SerializeField] float knockbackDuration = 0.16f;
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
        [SerializeField] float magnetizedProjectileRadius = 0.8f;
        [SerializeField] float markedProjectileDamageMultiplier = 1.2f;
        [SerializeField] float wallSlamBounceDistance = 0.18f;
        [SerializeField] float counterPulseDistance = 1.1f;
        [SerializeField] bool alwaysPullableByMagnet;
        [SerializeField] GameObject magnetizedIndicator;
        [SerializeField] bool autoCreateMagnetizedIndicator = true;
        [SerializeField] float magnetizedIndicatorHeight = 2.15f;
        [SerializeField] Color magnetizedIndicatorColor = new Color(1f, 0.82f, 0.16f, 0.85f);

        [Header("Movement")]
        [SerializeField] float strafeSpeed = 1.25f;
        [SerializeField] float approachSpeed = 5f;
        [SerializeField] float retreatSpeed = 2.25f;
        [SerializeField] float retreatDistance = 4.25f;
        [SerializeField] bool disableStrafe;

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

        [Header("Events")]
        public UnityEvent<ArkhamEnemy> OnDamaged = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnDeath = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnMagnetized = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnCountered = new UnityEvent<ArkhamEnemy>();

        int magneticMarks;
        bool isPreparingAttack;
        bool isAttacking;
        bool isRetreating;
        bool isLockedTarget;
        bool isStunned;
        bool isDead;
        bool isMagnetized;
        bool isMagneticallyControlled;
        bool attackHitApplied;
        float lastMarkTime = -999f;
        MoveMode moveMode;
        Coroutine behaviorCoroutine;
        Coroutine movementCoroutine;
        ArenaSystem arenaSystem;
        bool isMagnetRepelProjectile;
        Vector3 lastArenaWallHitNormal;

        public bool IsAlive => !isDead && isActiveAndEnabled && combatHealth != null && combatHealth.IsAlive;
        public bool IsAttackable => IsAlive && !isLockedTarget;
        public bool IsCounterable => IsAlive && (isPreparingAttack || isAttacking);
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

        public bool CanDirectorSelect =>
            IsAlive &&
            !isLockedTarget &&
            !isStunned &&
            !isMagneticallyControlled &&
            !isPreparingAttack &&
            !isAttacking &&
            !isRetreating;

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

            if (definition != null)
                ApplyDefinitionFields(definition);

            combatHealth.Configure(maxHealth, true);
            EnsureHealthBar();
            EnsureMagnetizedIndicator();
            UpdateMagnetizedIndicator();
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
            isDead = true;
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isLockedTarget = false;
            isStunned = false;
            isMagnetized = false;
            isMagneticallyControlled = false;
            isMagnetRepelProjectile = false;
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
            Move();
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
            if (indicator != null)
                counterIndicator = indicator;
            if (combatHealth == null)
                combatHealth = GetComponent<CombatHealth>();
            EnsureHealthBar();
            EnsureMagnetizedIndicator();
            UpdateMagnetizedIndicator();
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

            StopBehaviorCoroutine();
            behaviorCoroutine = StartCoroutine(useLinearCharge ? LinearChargeRoutine() : AttackRoutine());
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

            playerCombat = attacker;
            OnCountered.Invoke(this);
            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isStunned = true;
            isMagneticallyControlled = false;
            SetMarkState(MagneticMarkState.Magnetized);
            StopMoving();

            Vector3 direction = transform.position - attacker.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            behaviorCoroutine = StartCoroutine(MagneticPushRoutine(direction.normalized, counterPulseDistance, 0.12f, stunDuration));
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
            isRetreating = false;
            isStunned = true;
            isLockedTarget = false;
            StopMoving();

            ApplyMark(1);
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

            float allowedRange = isMagnetRepelProjectile
                ? float.PositiveInfinity
                : useLinearCharge
                    ? Mathf.Max(chargeHitRadius, attackRange * 0.5f) * hitRangeTolerance
                    : attackRange * hitRangeTolerance;

            if (DistanceToPlayer() > allowedRange)
                return false;

            attackHitApplied = true;
            playerCombat.ReceiveDamage(this);
            return true;
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
            StartIdleMovement();
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
            behaviorCoroutine = StartCoroutine(MagneticPushRoutine(direction.normalized, speed * 0.12f, 0.12f, stunDuration * 0.5f));
        }

        public void MagnetRepel(Vector3 direction, float speed, int impactDamage)
        {
            if (!IsAlive)
                return;

            StopBehaviorCoroutine();
            HideCounterCue();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            SetMarkState(MagneticMarkState.Normal);
            isStunned = true;
            isMagneticallyControlled = true;
            isMagnetRepelProjectile = true;
            behaviorCoroutine = StartCoroutine(MagneticRepelRoutine(direction.normalized, speed, impactDamage));
        }

        public void ReceiveMagneticImpact(int damage, Vector3 sourcePosition, float impactKnockbackDistance, bool clearsMagnetized)
        {
            if (!IsAlive)
                return;

            StopBehaviorCoroutine();
            HideCounterCue();
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isLockedTarget = false;
            isStunned = true;
            isMagneticallyControlled = false;
            StopMoving();

            float multiplier = markState == MagneticMarkState.Marked || markState == MagneticMarkState.Magnetized
                ? markedProjectileDamageMultiplier
                : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
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

            float approachTimer = 0f;
            while (IsAlive && playerCombat != null && DistanceToPlayer() > attackRange && approachTimer < 2.2f)
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
            ShowCounterCue();
            ShowChargeTelegraph();

            float prepTimer = 0f;
            while (prepTimer < prepareAttackTime && IsAlive)
            {
                prepTimer += Time.deltaTime;
                FacePlayer();
                yield return null;
            }

            HideChargeTelegraph();
            isPreparingAttack = false;
            isAttacking = true;
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

            float dashTimer = 0f;
            float hitRadiusSqr = chargeHitRadius * chargeHitRadius;
            while (dashTimer < chargeDuration && IsAlive)
            {
                dashTimer += Time.deltaTime;
                CollisionFlags flags = MoveBy(chargeDirection * chargeSpeed * Time.deltaTime);

                if (!attackHitApplied && playerCombat != null)
                {
                    Vector3 delta = playerCombat.transform.position - transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= hitRadiusSqr)
                        TryApplyAttackHit();
                }

                if ((flags & CollisionFlags.Sides) != 0)
                    break;

                yield return null;
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

        IEnumerator MagneticRepelRoutine(Vector3 direction, float speed, int impactDamage)
        {
            float elapsed = 0f;
            HashSet<ArkhamEnemy> hitEnemies = new HashSet<ArkhamEnemy>();

            while (elapsed < magnetizedRepelDuration && IsAlive)
            {
                elapsed += Time.deltaTime;
                lastArenaWallHitNormal = Vector3.zero;
                CollisionFlags collision = MoveBy(direction * speed * Time.deltaTime);
                DamageEnemiesTouchedByMagneticProjectile(hitEnemies, impactDamage);

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
                if (disableStrafe)
                {
                    moveMode = DistanceToPlayer() > attackRange * 1.5f ? MoveMode.Approach : MoveMode.None;
                }
                else
                {
                    int random = Random.Range(0, 3);
                    moveMode = random switch
                    {
                        0 => MoveMode.None,
                        1 => MoveMode.StrafeLeft,
                        _ => MoveMode.StrafeRight
                    };
                }

                yield return new WaitForSeconds(Random.Range(0.7f, 1.2f));
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
                    direction = playerDirection;
                    speed = approachSpeed;
                    break;
                case MoveMode.Retreat:
                    direction = -playerDirection;
                    speed = retreatSpeed;
                    break;
            }

            if (direction.sqrMagnitude > 0.01f)
                MoveBy(direction * speed * Time.deltaTime);

            AnimateMove(speed / approachSpeed, strafing, strafeDirection);
        }

        void AnimateMove(float magnitude, bool strafing, float strafeDirection)
        {
            if (animator == null)
                return;

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

        void DamageEnemiesTouchedByMagneticProjectile(HashSet<ArkhamEnemy> hitEnemies, int impactDamage)
        {
            if (manager == null)
                return;

            IReadOnlyList<ArkhamEnemy> enemies = manager.Enemies;
            float radiusSqr = magnetizedProjectileRadius * magnetizedProjectileRadius;

            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || enemy == this || !enemy.IsAlive || hitEnemies.Contains(enemy))
                    continue;

                Vector3 delta = enemy.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr)
                    continue;

                hitEnemies.Add(enemy);
                enemy.ReceiveMagneticImpact(impactDamage, transform.position, knockbackDistance * 1.5f, false);
            }
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
            UpdateMagnetizedIndicator();
            StopBehaviorCoroutine();
            StopMoving();
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

            StopBehaviorCoroutine();
            isDead = false;
            isPreparingAttack = false;
            isAttacking = false;
            isRetreating = false;
            isLockedTarget = false;
            isStunned = false;
            isMagnetized = false;
            isMagneticallyControlled = false;
            isMagnetRepelProjectile = false;
            attackHitApplied = false;
            lastArenaWallHitNormal = Vector3.zero;
            lastMarkTime = -999f;
            moveMode = MoveMode.None;
            magneticMarks = 0;
            markState = MagneticMarkState.Normal;

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

        void ShowCounterCue()
        {
            if (counterIndicator != null)
                counterIndicator.SetActive(true);

            if (counterParticle != null)
                counterParticle.Play(true);
        }

        void HideCounterCue()
        {
            if (counterIndicator != null)
                counterIndicator.SetActive(false);

            if (counterParticle != null)
            {
                counterParticle.Clear(true);
                counterParticle.Stop(true);
            }

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
