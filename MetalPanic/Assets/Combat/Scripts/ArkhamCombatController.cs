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

        [Header("Events")]
        public UnityEvent<ArkhamEnemy> OnTrajectory = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnHit = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnCounterAttack = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnDamaged = new UnityEvent<ArkhamEnemy>();
        public UnityEvent OnDeath = new UnityEvent();

        ArkhamEnemy lockedTarget;
        CharacterController characterController;
        Coroutine attackCoroutine;
        Coroutine damageCoroutine;
        Coroutine dodgeCoroutine;
        int attackIndex = -1;
        float nextCounterTime;
        float nextDodgeTime;
        bool hitAppliedThisAttack;
        bool currentAttackIsCounter;

        public bool isAttackingEnemy { get; private set; }
        public bool isCountering { get; private set; }
        public bool isDodging { get; private set; }
        public ArkhamEnemy LockedTarget => lockedTarget;
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

            ArkhamEnemy target = targetScanner != null
                ? targetScanner.FindTarget(enemyManager, transform.position, ResolveStrikeDirection())
                : null;

            if (target == null)
            {
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
            if (!IsAlive || isCountering || isAttackingEnemy || isDodging || health == null)
                return;

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

            try
            {
                string trigger = NextAttackTrigger(counterAttack);
                if (animator != null)
                    animator.SetTrigger(trigger);

                if (target != null)
                {
                    target.LockAsTarget(true);
                    OnTrajectory.Invoke(target);
                    yield return MoveTowardTarget(target, attackLungeDuration);
                }

                yield return new WaitForSeconds(attackImpactDelay);

                if (!useAnimationEventsForHits)
                    ApplyHit();

                yield return new WaitForSeconds(attackCooldown);
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
            lockedTarget.TakeStrike(this, strikeDamage, currentAttackIsCounter);
            OnHit.Invoke(lockedTarget);
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

        string NextAttackTrigger(bool counterAttack)
        {
            if (attackTriggers == null || attackTriggers.Length == 0)
                return "AirPunch";

            if (counterAttack)
                return "AirPunch";

            attackIndex = (attackIndex + 1) % attackTriggers.Length;
            return attackTriggers[attackIndex];
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
