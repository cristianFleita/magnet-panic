using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(ArkhamPlayerMotor))]
    public sealed class ArkhamCombatController : MonoBehaviour
    {
        static readonly int GroundPunchHash = Animator.StringToHash("GroundPunch");
        static readonly int DodgeHash = Animator.StringToHash("Dodge");
        static readonly int HitHash = Animator.StringToHash("Hit");

        [Header("References")]
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] ArkhamTargetScanner targetScanner;
        [SerializeField] ArkhamPlayerMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] Transform hitPoint;
        [SerializeField] ArkhamSimpleCameraFollow cameraRig;

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

        [Header("Events")]
        public UnityEvent<ArkhamEnemy> OnTrajectory = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnHit = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnCounterAttack = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnDamaged = new UnityEvent<ArkhamEnemy>();

        ArkhamEnemy lockedTarget;
        CharacterController characterController;
        Coroutine attackCoroutine;
        Coroutine damageCoroutine;
        int attackIndex;
        float nextCounterTime;
        bool hitAppliedThisAttack;
        bool currentAttackIsCounter;

        public bool isAttackingEnemy { get; private set; }
        public bool isCountering { get; private set; }
        public ArkhamEnemy LockedTarget => lockedTarget;

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

            characterController = GetComponent<CharacterController>();
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
        }

        void EnsureEvents()
        {
            OnTrajectory ??= new UnityEvent<ArkhamEnemy>();
            OnHit ??= new UnityEvent<ArkhamEnemy>();
            OnCounterAttack ??= new UnityEvent<ArkhamEnemy>();
            OnDamaged ??= new UnityEvent<ArkhamEnemy>();
        }

        public void OnAttack(InputValue value)
        {
            if (value.isPressed)
                AttackCheck();
        }

        public void OnAttack()
        {
            AttackCheck();
        }

        public void OnCounter(InputValue value)
        {
            if (value.isPressed)
                CounterCheck();
        }

        public void OnCounter()
        {
            CounterCheck();
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed)
                CounterCheck();
        }

        public void AttackCheck()
        {
            if (isAttackingEnemy)
                return;

            ArkhamEnemy target = targetScanner != null
                ? targetScanner.FindTarget(enemyManager, transform.position, motor.WorldMoveDirection)
                : null;

            if (target == null)
            {
                PlayWhiffAttack();
                return;
            }

            StartAttack(target, false);
        }

        public void CounterCheck()
        {
            if (isCountering || isAttackingEnemy || Time.time < nextCounterTime || enemyManager == null)
                return;

            ArkhamEnemy target = enemyManager.ClosestCounterableEnemy(transform.position);
            if (target == null)
                return;

            if (attackCoroutine != null)
                StopCoroutine(attackCoroutine);

            attackCoroutine = StartCoroutine(CounterRoutine(target));
        }

        public void HitEvent()
        {
            ApplyHit();
        }

        public void ReceiveDamage(ArkhamEnemy source)
        {
            if (isCountering || isAttackingEnemy)
                return;

            if (damageCoroutine != null)
                StopCoroutine(damageCoroutine);

            damageCoroutine = StartCoroutine(DamageRoutine(source));
        }

        void StartAttack(ArkhamEnemy target, bool counterAttack)
        {
            if (attackCoroutine != null)
                StopCoroutine(attackCoroutine);

            attackCoroutine = StartCoroutine(AttackRoutine(target, counterAttack));
        }

        IEnumerator AttackRoutine(ArkhamEnemy target, bool counterAttack)
        {
            lockedTarget = target;
            hitAppliedThisAttack = false;
            currentAttackIsCounter = counterAttack;
            isAttackingEnemy = true;
            motor.SetMovementLocked(true);

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

            if (lockedTarget != null)
                lockedTarget.LockAsTarget(false);

            lockedTarget = null;
            currentAttackIsCounter = false;
            isAttackingEnemy = false;
            motor.SetMovementLocked(false);
            attackCoroutine = null;
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

            yield return new WaitForSeconds(counterDodgeDuration);

            isCountering = false;
            yield return AttackRoutine(target, true);
        }

        IEnumerator DamageRoutine(ArkhamEnemy source)
        {
            motor.SetMovementLocked(true, true);
            OnDamaged.Invoke(source);

            if (animator != null)
                animator.SetTrigger(HitHash);

            cameraRig?.Shake(0.2f, 0.2f);
            yield return new WaitForSeconds(0.42f);

            motor.SetMovementLocked(false);
            damageCoroutine = null;
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

            if (animator != null)
                animator.SetTrigger(GroundPunchHash);

            yield return new WaitForSeconds(0.2f);
            isAttackingEnemy = false;
            motor.SetMovementLocked(false);
            attackCoroutine = null;
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

        static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
