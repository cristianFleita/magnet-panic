using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MagnetPanic.Combat.Upgrades
{
    /// <summary>
    /// Magnetic Slide (double-tap WASD) + Magnetic Slam (slamKey) for the player.
    /// Unlocked at runtime by the UpgradeSystem. Reads the keyboard directly so
    /// it doesn't have to touch the project's InputSystem_Actions asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementAbilityController : MonoBehaviour
    {
        enum SlideDir { None, Forward, Back, Left, Right }

        [Header("References")]
        [SerializeField] ArkhamPlayerMotor motor;
        [SerializeField] ArkhamCombatController combat;
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] Camera cameraOverride;
        [SerializeField] ArkhamSimpleCameraFollow cameraRig;
        [SerializeField] Animator animator;

        [Header("Slide")]
        [SerializeField] float slideDistance = 6f;
        [SerializeField] float slideDuration = 0.22f;
        [SerializeField] float slideIFrameDuration = 0.2f;
        [SerializeField] int slideContactDamage = 2;
        [SerializeField] float slideKnockbackDistance = 1.2f;
        [SerializeField] float slideContactRadius = 1.2f;
        [SerializeField] float doubleTapWindow = 0.25f;
        [SerializeField] float baseSlideCooldown = 0f;
        [SerializeField] float wallRaycastInset = 0.4f;
        [SerializeField] LayerMask wallLayers = ~0;

        [Header("Slam")]
        [SerializeField] Key slamKey = Key.G;
        [SerializeField] float slamSearchRadius = 5f;
        [SerializeField] float slamAirTime = 0.4f;
        [SerializeField] float slamAoeRadius = 4f;
        [SerializeField] int slamDamage = 5;
        [SerializeField] float slamKnockdownDuration = 1.5f;
        [SerializeField] float slamKnockbackDistance = 1.6f;
        [SerializeField] float slamCooldown = 0f;
        [SerializeField] float slamJumpHeight = 2.2f;

        [Header("VFX")]
        [SerializeField, Tooltip("Spawned at the player's feet during slide.")]
        GameObject slideVfxPrefab;
        [SerializeField] float slideVfxLifetime = 1.5f;
        [SerializeField, Tooltip("Spawned at the player's feet on slam takeoff and landing.")]
        GameObject slamVfxPrefab;
        [SerializeField] float slamVfxLifetime = 1.5f;

        [Header("Events")]
        public UnityEvent OnSlideTriggered = new UnityEvent();
        public UnityEvent OnSlamTriggered = new UnityEvent();
        public UnityEvent OnSlamLanded = new UnityEvent();

        bool slideUnlocked;
        bool slamUnlocked;
        float slideCooldownBonus;
        float lastTapTime;
        SlideDir lastTapDir = SlideDir.None;
        float nextSlideReadyTime;
        float nextSlamReadyTime;
        bool isSliding;
        bool isSlamming;
        CharacterController controller;

        static readonly Collider[] OverlapBuffer = new Collider[32];
        static readonly int SlideHash = Animator.StringToHash("Slide");
        static readonly int SlamHash = Animator.StringToHash("Slam");

        public bool SlideUnlocked => slideUnlocked;
        public bool SlamUnlocked => slamUnlocked;
        public float SlideCooldownRemaining => Mathf.Max(0f, nextSlideReadyTime - Time.time);
        public float SlamCooldownRemaining => Mathf.Max(0f, nextSlamReadyTime - Time.time);
        public float SlideCooldown => Mathf.Max(0f, baseSlideCooldown - slideCooldownBonus);
        public float SlamCooldown => slamCooldown;

        public void UnlockSlide() => slideUnlocked = true;
        public void UnlockSlam() => slamUnlocked = true;
        public void AddSlideCooldownReduction(float seconds) => slideCooldownBonus += Mathf.Max(0f, seconds);

        void Awake()
        {
            if (motor == null) motor = GetComponent<ArkhamPlayerMotor>();
            if (combat == null) combat = GetComponent<ArkhamCombatController>();
            if (enemyManager == null) enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();
            if (cameraOverride == null) cameraOverride = Camera.main;
            if (cameraRig == null) cameraRig = FindFirstObjectByType<ArkhamSimpleCameraFollow>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (Time.timeScale <= 0.01f || Keyboard.current == null)
                return;

            if (slideUnlocked && !isSliding && !isSlamming)
                PollSlideInput();

            if (slamUnlocked && !isSlamming && !isSliding)
                PollSlamInput();
        }

        void PollSlideInput()
        {
            if (Time.time < nextSlideReadyTime)
                return;

            SlideDir pressed = ReadFreshlyPressedSlideDir();
            if (pressed == SlideDir.None)
                return;

            if (lastTapDir == pressed && Time.time - lastTapTime <= doubleTapWindow)
            {
                StartSlide(pressed);
                lastTapDir = SlideDir.None;
                lastTapTime = -10f;
                return;
            }

            lastTapDir = pressed;
            lastTapTime = Time.time;
        }

        SlideDir ReadFreshlyPressedSlideDir()
        {
            Keyboard kb = Keyboard.current;
            if (TryPress(kb.wKey, kb.upArrowKey)) return SlideDir.Forward;
            if (TryPress(kb.sKey, kb.downArrowKey)) return SlideDir.Back;
            if (TryPress(kb.aKey, kb.leftArrowKey)) return SlideDir.Left;
            if (TryPress(kb.dKey, kb.rightArrowKey)) return SlideDir.Right;
            return SlideDir.None;
        }

        static bool TryPress(KeyControl a, KeyControl b)
        {
            return (a != null && a.wasPressedThisFrame) || (b != null && b.wasPressedThisFrame);
        }

        void PollSlamInput()
        {
            if (Time.time < nextSlamReadyTime || Keyboard.current == null)
                return;

            KeyControl key = Keyboard.current[slamKey];
            if (key != null && key.wasPressedThisFrame)
                StartSlam();
        }

        void StartSlide(SlideDir dir)
        {
            Vector3 worldDir = ResolveSlideDirection(dir);
            if (worldDir.sqrMagnitude < 0.01f)
                return;

            nextSlideReadyTime = Time.time + SlideCooldown;
            StartCoroutine(SlideRoutine(worldDir.normalized));
        }

        Vector3 ResolveSlideDirection(SlideDir dir)
        {
            Camera cam = cameraOverride != null ? cameraOverride : Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            forward.Normalize();
            right.Normalize();

            switch (dir)
            {
                case SlideDir.Forward: return forward;
                case SlideDir.Back: return -forward;
                case SlideDir.Left: return -right;
                case SlideDir.Right: return right;
                default: return Vector3.zero;
            }
        }

        IEnumerator SlideRoutine(Vector3 worldDir)
        {
            isSliding = true;
            if (motor != null) motor.SetMovementLocked(true, true);
            if (combat != null) combat.ExternalInvulnerability = true;
            OnSlideTriggered.Invoke();

            Vector3 start = transform.position;
            float maxDist = ResolveSlideDistance(start, worldDir);
            Vector3 destination = start + worldDir * maxDist;

            transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up);

            if (animator != null) animator.SetTrigger(SlideHash);
            SpawnVfx(slideVfxPrefab, slideVfxLifetime);

            float elapsed = 0f;
            float iframeEnd = Time.time + slideIFrameDuration;
            System.Collections.Generic.HashSet<ArkhamEnemy> alreadyHit = new System.Collections.Generic.HashSet<ArkhamEnemy>();

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                Vector3 next = Vector3.Lerp(start, destination, SmoothStep(t));
                Vector3 delta = next - transform.position;
                delta.y = 0f;

                if (controller != null && controller.enabled)
                    controller.Move(delta);
                else
                    transform.position += delta;

                ApplySlideContact(alreadyHit);

                if (combat != null && Time.time > iframeEnd)
                    combat.ExternalInvulnerability = false;

                yield return null;
            }

            if (combat != null) combat.ExternalInvulnerability = false;
            SpawnVfx(slideVfxPrefab, slideVfxLifetime);
            if (motor != null && combat != null && combat.IsAlive) motor.SetMovementLocked(false);
            isSliding = false;
        }

        float ResolveSlideDistance(Vector3 origin, Vector3 dir)
        {
            float requested = slideDistance;
            if (Physics.Raycast(origin + Vector3.up * 1f, dir, out RaycastHit hit, requested + wallRaycastInset, wallLayers, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(0.5f, hit.distance - wallRaycastInset);
            }
            return requested;
        }

        void ApplySlideContact(System.Collections.Generic.HashSet<ArkhamEnemy> alreadyHit)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, slideContactRadius, OverlapBuffer);
            for (int i = 0; i < count; i++)
            {
                ArkhamEnemy enemy = OverlapBuffer[i].GetComponentInParent<ArkhamEnemy>();
                if (enemy == null || !enemy.IsAlive || alreadyHit.Contains(enemy))
                    continue;
                alreadyHit.Add(enemy);
                enemy.ReceiveMagneticImpact(slideContactDamage, transform.position, slideKnockbackDistance, false);
            }
        }

        void StartSlam()
        {
            nextSlamReadyTime = Time.time + slamCooldown;
            StartCoroutine(SlamRoutine());
        }

        IEnumerator SlamRoutine()
        {
            isSlamming = true;
            if (motor != null) motor.SetMovementLocked(true, true);
            if (combat != null) combat.ExternalInvulnerability = true;
            OnSlamTriggered.Invoke();

            if (animator != null) animator.SetTrigger(SlamHash);

            ArkhamEnemy target = FindNearestEnemy(transform.position, slamSearchRadius);
            Vector3 landing = target != null ? target.transform.position : transform.position;
            Vector3 start = transform.position;

            float elapsed = 0f;
            while (elapsed < slamAirTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slamAirTime);

                Vector3 horizontal = Vector3.Lerp(start, landing, SmoothStep(t));
                float lift = Mathf.Sin(t * Mathf.PI) * slamJumpHeight;
                Vector3 next = new Vector3(horizontal.x, start.y + lift, horizontal.z);
                Vector3 delta = next - transform.position;

                if (controller != null && controller.enabled)
                    controller.Move(delta);
                else
                    transform.position += delta;

                yield return null;
            }

            // Snap to exact landing position at ground height so VFX spawns on the floor
            Vector3 groundedLanding = new Vector3(landing.x, start.y, landing.z);
            Vector3 snapDelta = groundedLanding - transform.position;
            if (controller != null && controller.enabled)
                controller.Move(snapDelta);
            else
                transform.position = groundedLanding;

            ApplySlamImpact(transform.position);
            cameraRig?.Shake(0.34f, 0.28f);
            SpawnVfx(slamVfxPrefab, slamVfxLifetime);
            OnSlamLanded.Invoke();

            if (combat != null) combat.ExternalInvulnerability = false;
            if (motor != null && combat != null && combat.IsAlive) motor.SetMovementLocked(false);
            isSlamming = false;
        }

        void ApplySlamImpact(Vector3 origin)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, slamAoeRadius, OverlapBuffer);
            for (int i = 0; i < count; i++)
            {
                ArkhamEnemy enemy = OverlapBuffer[i].GetComponentInParent<ArkhamEnemy>();
                if (enemy == null || !enemy.IsAlive)
                    continue;

                enemy.ReceiveMagneticImpact(slamDamage, origin, slamKnockbackDistance, true);
                enemy.ForceStun(slamKnockdownDuration);
            }
        }

        ArkhamEnemy FindNearestEnemy(Vector3 origin, float radius)
        {
            if (enemyManager == null)
                return null;

            ArkhamEnemy best = null;
            float bestSqr = radius * radius;
            var roster = enemyManager.Enemies;
            for (int i = 0; i < roster.Count; i++)
            {
                ArkhamEnemy e = roster[i];
                if (e == null || !e.IsAlive)
                    continue;
                Vector3 d = e.transform.position - origin;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }

        static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        void SpawnVfx(GameObject prefab, float lifetime)
        {
            if (prefab == null) return;
            Vector3 pos = new Vector3(transform.position.x, 0.4f, transform.position.z);
            Quaternion rot = transform.rotation;
            GameObject instance = Instantiate(prefab, pos, rot);
            instance.name = prefab.name + " (Runtime)";
            if (lifetime > 0f) Destroy(instance, lifetime);
        }
    }
}
