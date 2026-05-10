using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArkhamEnemy : MonoBehaviour
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

        [Header("References")]
        [SerializeField] ArkhamEnemyManager manager;
        [SerializeField] ArkhamCombatController playerCombat;
        [SerializeField] Animator animator;
        [SerializeField] CharacterController characterController;
        [SerializeField] GameObject counterIndicator;
        [SerializeField] ParticleSystem counterParticle = null;

        [Header("Stats")]
        [SerializeField] int maxHealth = 3;
        [SerializeField] int magneticMarksToMagnetize = 2;
        [SerializeField] float stunDuration = 0.45f;
        [SerializeField] float knockbackDistance = 0.55f;
        [SerializeField] float knockbackDuration = 0.16f;

        [Header("Movement")]
        [SerializeField] float strafeSpeed = 1.25f;
        [SerializeField] float approachSpeed = 5f;
        [SerializeField] float retreatSpeed = 2.25f;
        [SerializeField] float retreatDistance = 4.25f;

        [Header("Attack")]
        [SerializeField] float prepareAttackTime = 0.35f;
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float attackHitDelay = 0.2f;
        [SerializeField] float attackRecovery = 0.55f;

        [Header("Events")]
        public UnityEvent<ArkhamEnemy> OnDamaged = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnDeath = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnMagnetized = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnCountered = new UnityEvent<ArkhamEnemy>();

        int health;
        int magneticMarks;
        bool isPreparingAttack;
        bool isAttacking;
        bool isRetreating;
        bool isLockedTarget;
        bool isStunned;
        bool isDead;
        bool isMagnetized;
        bool attackHitApplied;
        MoveMode moveMode;
        Coroutine behaviorCoroutine;
        Coroutine movementCoroutine;

        public bool IsAlive => !isDead && isActiveAndEnabled && health > 0;
        public bool IsAttackable => IsAlive && !isLockedTarget;
        public bool IsCounterable => IsAlive && (isPreparingAttack || isAttacking);
        public bool IsAttacking => isAttacking;
        public bool IsMagnetized => isMagnetized;
        public int MagneticMarks => magneticMarks;

        public bool CanDirectorSelect =>
            IsAlive &&
            !isLockedTarget &&
            !isStunned &&
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

            health = maxHealth;
        }

        void OnEnable()
        {
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

        void Update()
        {
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
            animator = targetAnimator;
            counterIndicator = indicator;
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
            behaviorCoroutine = StartCoroutine(AttackRoutine());
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
            StopMoving();

            behaviorCoroutine = StartCoroutine(StunRoutine(stunDuration));
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

            ApplyMagneticMark();
            health -= Mathf.Max(1, damage);
            OnDamaged.Invoke(this);

            if (health <= 0)
            {
                Die();
                return;
            }

            if (animator != null)
                animator.SetTrigger(HitHash);

            Vector3 direction = (transform.position - attacker.transform.position).normalized;
            direction.y = 0f;
            behaviorCoroutine = StartCoroutine(HitReactionRoutine(direction));
        }

        public void HitEvent()
        {
            if (playerCombat == null || attackHitApplied)
                return;

            attackHitApplied = true;
            playerCombat.ReceiveDamage(this);
        }

        void ApplyMagneticMark()
        {
            if (isMagnetized)
                return;

            magneticMarks++;

            if (magneticMarks >= magneticMarksToMagnetize)
            {
                isMagnetized = true;
                OnMagnetized.Invoke(this);
            }
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

            if (animator != null)
                animator.SetTrigger(AirPunchHash);

            yield return new WaitForSeconds(attackHitDelay);

            if (!attackHitApplied && playerCombat != null)
            {
                attackHitApplied = true;
                playerCombat.ReceiveDamage(this);
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

        IEnumerator HitReactionRoutine(Vector3 direction)
        {
            float elapsed = 0f;
            Vector3 totalOffset = direction * knockbackDistance;
            Vector3 applied = Vector3.zero;

            while (elapsed < knockbackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / knockbackDuration);
                Vector3 next = Vector3.Lerp(Vector3.zero, totalOffset, t);
                characterController.Move(next - applied);
                applied = next;
                yield return null;
            }

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
            if (!IsAlive || !isActiveAndEnabled || isLockedTarget || isStunned)
                return;

            if (movementCoroutine != null)
                StopCoroutine(movementCoroutine);

            movementCoroutine = StartCoroutine(IdleMovementRoutine());
        }

        IEnumerator IdleMovementRoutine()
        {
            while (IsAlive && !isLockedTarget && !isStunned && !isPreparingAttack && !isAttacking && !isRetreating)
            {
                int random = Random.Range(0, 3);
                moveMode = random switch
                {
                    0 => MoveMode.None,
                    1 => MoveMode.StrafeLeft,
                    _ => MoveMode.StrafeRight
                };

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
            if (!IsAlive || playerCombat == null || isLockedTarget || isStunned)
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
                characterController.Move(direction * speed * Time.deltaTime);

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

        void Die()
        {
            isDead = true;
            HideCounterCue();
            StopBehaviorCoroutine();
            StopMoving();
            OnDeath.Invoke(this);

            if (animator != null)
                animator.SetTrigger(DeathHash);

            if (characterController != null)
                characterController.enabled = false;

            enabled = false;
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
        }
    }
}
