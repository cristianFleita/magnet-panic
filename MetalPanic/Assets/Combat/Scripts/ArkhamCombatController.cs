using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(ArkhamPlayerMotor))]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class ArkhamCombatController : MonoBehaviour
    {
        static readonly int GroundPunchHash = Animator.StringToHash("GroundPunch");
        static readonly int DodgeHash = Animator.StringToHash("Dodge");
        static readonly int HitHash = Animator.StringToHash("Hit");
        const string KnockbackStateName = "Armature|Hit_Knockback";
        const string IdleStateName = "Armature|Idle_Loop";

        [Header("References")]
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] ArkhamTargetScanner targetScanner;
        [SerializeField] ArkhamPlayerMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] Transform hitPoint;
        [SerializeField] ArkhamSimpleCameraFollow cameraRig;
        [SerializeField] GameInputProvider inputProvider;

        [Header("Health")]
        [SerializeField] CombatHealth health;
        [SerializeField] int maxHealth = 6;
        [SerializeField] int damagePerEnemyHit = 1;

        [Header("Attack")]
        [SerializeField] int strikeDamage = 1;
        [SerializeField] float attackCooldown = 0.38f;
        [SerializeField] float attackLungeDuration = 0.22f;
        [SerializeField] float attackImpactDelay = 0.16f;
        [SerializeField] float targetOffset = 0.95f;
        [SerializeField] string[] attackTriggers = { "AirKick", "AirKick2", "AirPunch", "AirKick3" };
        [SerializeField] bool useAnimationEventsForHits = false;

        [Header("Sticky Target")]
        [SerializeField, Tooltip("Idle seconds before the sticky lock expires (only when NOT attacking/comboing).")] float stickyTargetTimeout = 3f;
        [SerializeField] float stickyYawBreakThreshold = 25f;

        [Header("Combo")]
        [SerializeField] float comboWindow = 0.55f;
        [SerializeField] float comboFinisherRecovery = 0.55f;
        [SerializeField] float comboLungeMultiplier = 1.15f;

        [Header("Counter")]
        [SerializeField] float counterCooldown = 0.65f;
        [SerializeField] float counterDodgeDuration = 0.16f;
        [SerializeField] float counterRadius = 4f;

        [Header("Dodge")]
        [SerializeField] float dodgeDistance = 2.35f;
        [SerializeField] float dodgeDuration = 0.28f;
        [SerializeField] float dodgeCooldown = 0.45f;

        [Header("Knockdown")]
        [SerializeField, Tooltip("How long the player stays locked when knocked down by a charge.")]
        float knockdownDuration = 1.1f;

        [Header("Grapple")]
        [SerializeField, Tooltip("Brief immunity after escaping a grapple.")]
        float grappleEscapeImmunity = 0.5f;

        [Header("Events")]
        public UnityEvent<ArkhamEnemy> OnTrajectory = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnHit = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnCounterAttack = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnDamaged = new UnityEvent<ArkhamEnemy>();
        public UnityEvent OnDeath = new UnityEvent();
        public UnityEvent<GrapplerBehavior> OnGrappled = new UnityEvent<GrapplerBehavior>();
        public UnityEvent<bool> OnGrappleEnd = new UnityEvent<bool>();

        ArkhamEnemy lockedTarget;
        ArkhamEnemy lastHitEnemy;
        CharacterController characterController;
        Coroutine attackCoroutine;
        Coroutine damageCoroutine;
        Coroutine dodgeCoroutine;
        int comboIndex;
        float comboWindowEndTime;
        float lastHitTime;
        float lastHitYaw;
        float nextCounterTime;
        float nextDodgeTime;
        bool hitAppliedThisAttack;
        bool currentAttackIsCounter;

        public bool isAttackingEnemy { get; private set; }
        public bool isCountering { get; private set; }
        public bool isDodging { get; private set; }
        public bool ExternalInvulnerability { get; set; }
        public ArkhamEnemy LockedTarget => lockedTarget;
        public ArkhamEnemy LastHitEnemy => PeekStickyTarget();
        public CombatHealth Health => health;
        public bool IsAlive => health == null || health.IsAlive;
        public float CounterRadius => counterRadius;
        public ArkhamEnemyManager EnemyManager => enemyManager;
        public ArkhamTargetScanner TargetScanner => targetScanner;
        public Vector3 PreferredStrikeDirection => ResolveStrikeDirection();

        void Awake()
        {
            EnsureEvents();

            if (motor == null)
                motor = GetComponent<ArkhamPlayerMotor>();

            if (targetScanner == null)
                targetScanner = GetComponent<ArkhamTargetScanner>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();

            if (cameraRig == null)
                cameraRig = FindFirstObjectByType<ArkhamSimpleCameraFollow>();

            if (health == null)
                health = GetComponent<CombatHealth>();

            if (health == null)
                health = gameObject.AddComponent<CombatHealth>();

            if (inputProvider == null)
                inputProvider = GameInputProvider.EnsureOn(gameObject);

            health.Configure(maxHealth, true);
            characterController = GetComponent<CharacterController>();

            StrikeTargetIndicator.EnsureOn(gameObject, this);
        }

        void OnValidate()
        {
            dodgeDistance = Mathf.Max(0f, dodgeDistance);
            dodgeDuration = Mathf.Max(0.01f, dodgeDuration);
            dodgeCooldown = Mathf.Max(0f, dodgeCooldown);
        }

        void Update()
        {
            DecayStickyTarget();
            PumpInput();
        }

        public void Configure(
            ArkhamEnemyManager manager,
            ArkhamTargetScanner scanner,
            ArkhamPlayerMotor playerMotor,
            Animator targetAnimator,
            ArkhamSimpleCameraFollow followCamera,
            Transform punchPoint)
        {
            EnsureEvents();

            enemyManager = manager;
            targetScanner = scanner;
            motor = playerMotor;
            animator = targetAnimator;
            cameraRig = followCamera;
            hitPoint = punchPoint;

            if (health == null)
                health = GetComponent<CombatHealth>();

            if (inputProvider == null)
                inputProvider = GameInputProvider.EnsureOn(gameObject);

            if (characterController == null)
                characterController = GetComponent<CharacterController>();
        }

        void EnsureEvents()
        {
            OnTrajectory ??= new UnityEvent<ArkhamEnemy>();
            OnHit ??= new UnityEvent<ArkhamEnemy>();
            OnCounterAttack ??= new UnityEvent<ArkhamEnemy>();
            OnDamaged ??= new UnityEvent<ArkhamEnemy>();
            OnDeath ??= new UnityEvent();
        }

        public void AttackCheck()
        {
            if (!IsAlive || isAttackingEnemy || isDodging || isCountering)
                return;

            if (TryStartCounterFromStrike())
                return;

            // 1. Try sticky target first (last-hit enemy stays locked)
            ArkhamEnemy target = PeekStickyTarget();

            // 2. Fall back to scanner
            if (target == null && targetScanner != null)
                target = targetScanner.FindTarget(enemyManager, transform.position, ResolveStrikeDirection());

            if (target == null)
            {
                ResetCombo();
                PlayWhiffAttack();
                return;
            }

            StartAttack(target, false);
        }

        bool TryStartCounterFromStrike()
        {
            if (isCountering || enemyManager == null || Time.time < nextCounterTime)
                return false;

            ArkhamEnemy counterTarget = enemyManager.ClosestCounterableEnemy(transform.position, counterRadius);
            if (counterTarget == null)
                return false;

            if (attackCoroutine != null)
                StopCoroutine(attackCoroutine);

            attackCoroutine = StartCoroutine(CounterRoutine(counterTarget));
            return true;
        }

        public void DodgeCheck()
        {
            if (!CanAcceptDodgeInput())
                return;

            StartDodge(ResolveDodgeDirection());
        }

        public void HitEvent()
        {
            ApplyHit();
        }

        public void ReceiveDamage(ArkhamEnemy source)
        {
            ReceiveDamage(source, damagePerEnemyHit, false);
        }

        public void ReceiveDamage(ArkhamEnemy source, int damage, bool knockdown)
        {
            if (!IsAlive || isCountering || isAttackingEnemy || isDodging || ExternalInvulnerability || health == null)
                return;

            // If grappled, still take damage (that's the point of the grapple)
            int amount = Mathf.Max(1, damage);
            if (!health.ApplyDamage(amount))
                return;

            if (!health.IsAlive)
            {
                OnDamaged.Invoke(source);
                Die();
                return;
            }

            if (damageCoroutine != null)
                StopCoroutine(damageCoroutine);

            damageCoroutine = StartCoroutine(DamageRoutine(source, knockdown));
        }

        public bool Heal(int amount)
        {
            return health != null && health.Heal(amount);
        }

        void PumpInput()
        {
            if (inputProvider == null)
                return;

            if (CanAcceptDodgeInput() && inputProvider.ConsumeBuffered(GameInputIntent.Dodge))
            {
                DodgeCheck();
                return;
            }

            if (CanAcceptStrikeInput() && inputProvider.ConsumeBuffered(GameInputIntent.Strike))
                AttackCheck();
        }

        bool CanAcceptStrikeInput()
        {
            return IsAlive && !isAttackingEnemy && !isDodging;
        }

        bool CanAcceptDodgeInput()
        {
            return IsAlive
                && !isDodging
                && !isCountering
                && !isAttackingEnemy
                && damageCoroutine == null
                && Time.time >= nextDodgeTime;
        }

        void StartAttack(ArkhamEnemy target, bool counterAttack)
        {
            if (attackCoroutine != null)
                StopCoroutine(attackCoroutine);

            attackCoroutine = StartCoroutine(AttackRoutine(target, counterAttack));
        }

        void StartDodge(Vector3 direction)
        {
            if (dodgeCoroutine != null)
                StopCoroutine(dodgeCoroutine);

            dodgeCoroutine = StartCoroutine(DodgeRoutine(direction));
        }

        IEnumerator AttackRoutine(ArkhamEnemy target, bool counterAttack)
        {
            lockedTarget = target;
            hitAppliedThisAttack = false;
            currentAttackIsCounter = counterAttack;
            isAttackingEnemy = true;
            motor.SetMovementLocked(true);

            // Refresh yaw baseline each combo hit so micro-drift doesn't break lock
            if (target == lastHitEnemy && cameraRig != null)
                lastHitYaw = cameraRig.Yaw;

            try
            {
                string trigger = NextComboTrigger(counterAttack);
                if (animator != null)
                    animator.SetTrigger(trigger);

                Debug.Log($"[Combo] Hit {comboIndex}/{(attackTriggers != null ? attackTriggers.Length : 0)} trigger='{trigger}' target={target?.name} isFinisher={(attackTriggers != null && comboIndex >= attackTriggers.Length)}");

                if (target != null)
                {
                    target.LockAsTarget(true);
                    OnTrajectory.Invoke(target);

                    // Combo chain gets slightly faster lunges
                    float lungeDur = comboIndex > 1
                        ? attackLungeDuration / comboLungeMultiplier
                        : attackLungeDuration;
                    yield return MoveTowardTarget(target, lungeDur);
                }

                yield return new WaitForSeconds(attackImpactDelay);

                if (!useAnimationEventsForHits)
                    ApplyHit();

                // Finisher has longer recovery
                bool isFinisher = attackTriggers != null && comboIndex >= attackTriggers.Length;
                float recovery = isFinisher
                    ? attackCooldown + comboFinisherRecovery
                    : attackCooldown;

                yield return new WaitForSeconds(recovery);

                if (!isFinisher && !counterAttack)
                {
                    comboWindowEndTime = Time.time + comboWindow;
                }
                else
                {
                    Debug.Log($"[Combo] Chain COMPLETE (finisher={isFinisher}) — resetting");
                    ResetCombo();
                }
            }
            finally
            {
                if (lockedTarget != null)
                    lockedTarget.LockAsTarget(false);

                lockedTarget = null;
                currentAttackIsCounter = false;
                isAttackingEnemy = false;
                if (IsAlive)
                    motor.SetMovementLocked(false);
                attackCoroutine = null;
            }
        }

        IEnumerator CounterRoutine(ArkhamEnemy target)
        {
            nextCounterTime = Time.time + counterCooldown;
            lockedTarget = target;
            isCountering = true;
            motor.SetMovementLocked(true);
            OnCounterAttack.Invoke(target);
            target.CounteredBy(this);

            if (animator != null)
                animator.SetTrigger(DodgeHash);

            cameraRig?.Shake(0.15f, 0.16f);

            try
            {
                yield return new WaitForSeconds(counterDodgeDuration);
            }
            finally
            {
                isCountering = false;
            }

            yield return AttackRoutine(target, true);
        }

        IEnumerator DodgeRoutine(Vector3 direction)
        {
            nextDodgeTime = Time.time + dodgeCooldown;
            isDodging = true;
            motor.SetMovementLocked(true);

            try
            {
                if (direction.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(direction.normalized);

                if (animator != null)
                    animator.SetTrigger(DodgeHash);

                Vector3 start = transform.position;
                Vector3 destination = start + direction.normalized * dodgeDistance;
                float elapsed = 0f;

                while (elapsed < dodgeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dodgeDuration);
                    Vector3 next = Vector3.Lerp(start, destination, SmoothStep(t));
                    Vector3 delta = next - transform.position;
                    delta.y = 0f;

                    if (characterController != null && characterController.enabled)
                        characterController.Move(delta);
                    else
                        transform.position += delta;

                    yield return null;
                }
            }
            finally
            {
                isDodging = false;
                if (IsAlive)
                    motor.SetMovementLocked(false);
                dodgeCoroutine = null;
            }
        }

        IEnumerator DamageRoutine(ArkhamEnemy source, bool knockdown)
        {
            motor.SetMovementLocked(true, true);
            OnDamaged.Invoke(source);

            try
            {
                if (animator != null)
                {
                    if (knockdown)
                        animator.CrossFade(KnockbackStateName, 0.05f, 0);
                    else
                        animator.SetTrigger(HitHash);
                }

                cameraRig?.Shake(knockdown ? 0.32f : 0.2f, knockdown ? 0.28f : 0.2f);
                yield return new WaitForSeconds(knockdown ? Mathf.Max(0.4f, knockdownDuration) : 0.42f);

                if (knockdown && animator != null)
                    animator.CrossFade(IdleStateName, 0.15f, 0);
            }
            finally
            {
                if (IsAlive)
                    motor.SetMovementLocked(false);
                damageCoroutine = null;
            }
        }

        void Die()
        {
            if (attackCoroutine != null)
                StopCoroutine(attackCoroutine);

            if (damageCoroutine != null)
                StopCoroutine(damageCoroutine);

            if (dodgeCoroutine != null)
                StopCoroutine(dodgeCoroutine);

            isAttackingEnemy = false;
            isCountering = false;
            isDodging = false;
            lockedTarget = null;
            motor.SetMovementLocked(true, true);

            if (animator != null)
                animator.SetTrigger(HitHash);

            cameraRig?.Shake(0.26f, 0.24f);
            OnDeath.Invoke();
        }

        IEnumerator MoveTowardTarget(ArkhamEnemy target, float duration)
        {
            if (target == null || characterController == null)
                yield break;

            Vector3 destination = TargetOffset(target.transform);
            Vector3 start = transform.position;
            float elapsed = 0f;

            Vector3 lookPosition = target.transform.position;
            lookPosition.y = transform.position.y;
            Vector3 lookDirection = lookPosition - transform.position;
            if (lookDirection.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDirection);

            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 next = Vector3.Lerp(start, destination, SmoothStep(t));
                Vector3 delta = next - transform.position;
                characterController.Move(delta);
                yield return null;
            }
        }

        void ApplyHit()
        {
            if (hitAppliedThisAttack || lockedTarget == null || !lockedTarget.IsAlive)
                return;

            hitAppliedThisAttack = true;

            int damageToApply = strikeDamage;

            if (!currentAttackIsCounter && attackTriggers != null && attackTriggers.Length > 0)
            {
                if (comboIndex >= attackTriggers.Length)
                    damageToApply = 3; // Finisher
                else if (comboIndex == attackTriggers.Length - 1)
                    damageToApply = 2; // Hit before finisher
            }

            lockedTarget.TakeStrike(this, damageToApply, currentAttackIsCounter);
            OnHit.Invoke(lockedTarget);

            // Record sticky target for combo continuity
            lastHitEnemy = lockedTarget;

            lastHitTime = Time.time;
            if (cameraRig != null) lastHitYaw = cameraRig.Yaw;

            cameraRig?.Shake(0.11f + Mathf.Min(0.25f, Vector3.Distance(transform.position, lockedTarget.transform.position) * 0.02f), 0.14f);
        }

        void PlayWhiffAttack()
        {
            if (attackCoroutine != null)
                StopCoroutine(attackCoroutine);

            attackCoroutine = StartCoroutine(WhiffRoutine());
        }

        IEnumerator WhiffRoutine()
        {
            isAttackingEnemy = true;
            motor.SetMovementLocked(true);

            try
            {
                if (animator != null)
                    animator.SetTrigger(GroundPunchHash);

                yield return new WaitForSeconds(0.2f);
            }
            finally
            {
                isAttackingEnemy = false;
                if (IsAlive)
                    motor.SetMovementLocked(false);
                attackCoroutine = null;
            }
        }

        string NextComboTrigger(bool counterAttack)
        {
            if (attackTriggers == null || attackTriggers.Length == 0)
                return "AirPunch";

            if (counterAttack)
            {
                ResetCombo();
                return "AirPunch";
            }

            // Reset combo if outside the chain window
            if (Time.time > comboWindowEndTime && comboIndex > 0)
            {
                Debug.Log($"[Combo] Window EXPIRED — resetting (was idx {comboIndex})");
                ResetCombo();
            }

            int idx = Mathf.Clamp(comboIndex, 0, attackTriggers.Length - 1);
            comboIndex = idx + 1;
            return attackTriggers[idx];
        }

        void ResetCombo()
        {
            comboIndex = 0;
            comboWindowEndTime = 0f;
        }

        /// <summary>
        /// Pure read — returns the sticky target if still valid, never clears the field.
        /// The ONLY way to intentionally break the lock is moving the camera past the
        /// yaw threshold. Timeout is handled separately by DecayStickyTarget.
        /// </summary>
        ArkhamEnemy PeekStickyTarget()
        {
            if (lastHitEnemy == null || !lastHitEnemy.IsAlive)
                return null;

            // Camera movement is the deliberate unlock mechanism
            if (cameraRig != null && Mathf.Abs(Mathf.DeltaAngle(lastHitYaw, cameraRig.Yaw)) > stickyYawBreakThreshold)
                return null;

            float maxRange = targetScanner != null ? targetScanner.MaximumDistance : 13f;
            if (Vector3.Distance(transform.position, lastHitEnemy.transform.position) > maxRange)
                return null;

            return lastHitEnemy;
        }

        /// <summary>
        /// Called once per Update — clears the sticky reference when it expires.
        /// Never clears during active combat or combo window so rapid hits stay locked.
        /// </summary>
        void DecayStickyTarget()
        {
            if (lastHitEnemy == null)
                return;

            // Always clear dead enemies
            if (!lastHitEnemy.IsAlive)
            {
                lastHitEnemy = null;
                return;
            }

            // Don't expire while attacking or inside the combo window
            if (isAttackingEnemy || isCountering || Time.time < comboWindowEndTime)
                return;

            // Idle timeout — player stopped comboing
            if (Time.time - lastHitTime > stickyTargetTimeout)
                lastHitEnemy = null;
        }

        Vector3 TargetOffset(Transform target)
        {
            Vector3 position = target.position;
            return Vector3.MoveTowards(position, transform.position, targetOffset);
        }

        Vector3 ResolveStrikeDirection()
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f)
                return fwd.normalized;

            if (motor != null)
            {
                Vector3 move = motor.WorldMoveDirection;
                move.y = 0f;
                if (move.sqrMagnitude > 0.01f)
                    return move.normalized;
            }

            return Vector3.forward;
        }

        Vector3 ResolveDodgeDirection()
        {
            Vector3 direction = motor != null ? motor.WorldMoveDirection : Vector3.zero;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.01f && inputProvider != null)
            {
                direction = inputProvider.AimWorldDirection;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.01f)
                direction = transform.forward;

            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        }

        static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
