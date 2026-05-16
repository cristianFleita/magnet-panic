using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(ArkhamPlayerMotor))]
    public sealed class MagnetismController : MonoBehaviour
    {
        static readonly int PullHash = Animator.StringToHash("Pull");
        static readonly int RepelHash = Animator.StringToHash("Repel");
        static readonly int PullingHash = Animator.StringToHash("Pulling");

        [Header("References")]
        [SerializeField] Animator animator;
        [SerializeField] Camera cameraOverride;
        [SerializeField] ArkhamPlayerMotor motor;
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] Transform orbitCenter;
        [SerializeField] GameInputProvider inputProvider;

        [Header("Pull")]
        [SerializeField] float pullRadius = 5f;
        [SerializeField] float pullSpeed = 12f;
        [SerializeField] float enemyPullSpeed = 8f;
        [SerializeField] LayerMask attractableLayers = ~0;

        [Header("Orbit")]
        [SerializeField] float orbitRadius = 2.2f;
        [SerializeField] float orbitHeight = 0.95f;
        [SerializeField] float orbitAngularSpeed = 210f;
        [SerializeField] float maxCapacity = 8f;
        [SerializeField] float overflowHeadroom = 0.5f;
        [SerializeField] float orbitRejectSpeed = 7f;
        [SerializeField] float chargePenaltyAtFull = 0.2f;

        [Header("Repel")]
        [SerializeField] float repelConeAngle = 50f;
        [SerializeField, Tooltip("Seconds of forced downtime between repels. 0 = no cooldown. Recharge gate lives on the OverloadController instead.")]
        float repelCooldown = 0f;
        [SerializeField] float repelSpeed = 16f;
        [SerializeField] int magnetizedEnemyDamage = 5;
        [SerializeField, Tooltip("Damage the repelled enemy itself takes each time it collides with another enemy mid-flight (mirrors wall-slam recoil).")]
        int magnetizedEnemyRecoilDamage = 5;
        [SerializeField, Tooltip("Search radius (m) used to home every repelled projectile toward the nearest enemy. 0 disables auto-aim and falls back to the cone spread.")]
        float repelAutoAimRadius = 15f;
        [SerializeField, Tooltip("When repelling a magnetized enemy, also snap toward arena walls within this radius if no enemy is closer (lets the recoil/wall-slam combo work). 0 disables wall auto-aim.")]
        float magnetizedRepelWallAimRadius = 5f;

        [Header("Enemy Pull")]
        [SerializeField] float heldEnemyDistance = 1.9f;
        [SerializeField, Tooltip("Once a magnetized enemy gets within this distance of the hold point, it locks in place and stops following the player. Enables the Pull → Strike → Repel combo.")]
        float enemyAnchorLockDistance = 0.4f;
        [SerializeField, Tooltip("Seconds the player has to fire Repel after starting a pull. If no repel fires within this window the enemy demagnetizes and walks off.")]
        float magnetizedHoldTimeout = 3f;
        [SerializeField, Tooltip("Maximum distance a pulled magnetized enemy may be from the player. If the player runs away (or the enemy is dragged past this radius) it demagnetizes and is released.")]
        float magnetizedMaxDistance = 5f;

        [Header("Aim Assist")]
        [SerializeField] bool autoCreateAimIndicator = true;
        [SerializeField] LineRenderer aimLine;
        [SerializeField] Transform aimTip;
        [SerializeField] float aimIndicatorLength = 5f;
        [SerializeField] float aimIndicatorHeight = 0.08f;
        [SerializeField] float aimLineWidth = 0.08f;
        [SerializeField] Color aimIndicatorColor = new Color(0.2f, 0.85f, 1f, 0.85f);

        [Header("Events")]
        public UnityEvent OnPullStarted = new UnityEvent();
        public UnityEvent OnPullStopped = new UnityEvent();
        public UnityEvent<bool> OnRepelFired = new UnityEvent<bool>();
        public UnityEvent<float, float> OnChargeChanged = new UnityEvent<float, float>();
        public UnityEvent<MagneticObject> OnObjectOrbited = new UnityEvent<MagneticObject>();
        public UnityEvent<ArkhamEnemy> OnEnemyOrbited = new UnityEvent<ArkhamEnemy>();
        public UnityEvent<ArkhamEnemy> OnEnemyPulled = new UnityEvent<ArkhamEnemy>();

        readonly Collider[] overlapBuffer = new Collider[64];
        readonly List<MagneticObject> attractingObjects = new List<MagneticObject>();
        readonly List<MagneticObject> orbitingObjects = new List<MagneticObject>();
        readonly List<ArkhamEnemy> pulledEnemies = new List<ArkhamEnemy>();
        readonly List<PulledEnemyHold> pulledEnemyHolds = new List<PulledEnemyHold>();

        struct PulledEnemyHold
        {
            public Vector3 ApproachDirection;
            public Vector3 AnchorPosition;
            public bool IsAnchored;
            public float PullStartTime;
        }

        float currentCharge;
        float nextRepelTime;
        float orbitAngle;
        bool pullHeld;
        bool pullActive;
        bool pullEnabled = true;
        bool repelEnabled = true;
        ArenaSystem cachedArena;

        // Upgrade modifiers — set by UpgradeSystem. Defaults are no-op.
        float pullRangeBonus;
        float pullSpeedMult = 1f;
        float repelSpeedMult = 1f;
        int repelDamageBonus;
        int repelPiercingBonus;
        int maxCapacityBonus;
        float chargeMobilityPenaltyMult = 1f;

        public float CurrentCharge => currentCharge;
        public float MaxCapacity => EffectiveMaxCapacity;
        public float EffectiveMaxCapacity => Mathf.Max(1f, maxCapacity + maxCapacityBonus);
        public float EffectivePullRadius => Mathf.Max(0f, pullRadius + pullRangeBonus);
        public bool IsPulling => pullActive;
        public bool HasOrbitingPayload => orbitingObjects.Count > 0 || pulledEnemies.Count > 0;
        public int OrbitingCount => orbitingObjects.Count + pulledEnemies.Count;
        public bool PullEnabled => pullEnabled;
        public bool RepelEnabled => repelEnabled;

        public float PullRangeBonus
        {
            get => pullRangeBonus;
            set => pullRangeBonus = Mathf.Max(0f, value);
        }

        public float PullSpeedMult
        {
            get => pullSpeedMult;
            set => pullSpeedMult = Mathf.Max(0.1f, value);
        }

        public float RepelSpeedMult
        {
            get => repelSpeedMult;
            set => repelSpeedMult = Mathf.Max(0.1f, value);
        }

        public int RepelDamageBonus
        {
            get => repelDamageBonus;
            set => repelDamageBonus = Mathf.Max(0, value);
        }

        public int RepelPiercingBonus
        {
            get => repelPiercingBonus;
            set => repelPiercingBonus = Mathf.Max(0, value);
        }

        public int MaxCapacityBonus
        {
            get => maxCapacityBonus;
            set => maxCapacityBonus = Mathf.Max(0, value);
        }

        public float ChargeMobilityPenaltyMult
        {
            get => chargeMobilityPenaltyMult;
            set => chargeMobilityPenaltyMult = Mathf.Clamp(value, 0f, 4f);
        }

        public void SetPullEnabled(bool enabled)
        {
            pullEnabled = enabled;
            if (!pullEnabled)
            {
                pullHeld = false;
                CancelAttractingPayload();
                if (pullActive)
                {
                    pullActive = false;
                    StopPullAnimation();
                    OnPullStopped.Invoke();
                }
            }
        }

        public void SetRepelEnabled(bool enabled)
        {
            repelEnabled = enabled;
        }

        public void CancelAttracting()
        {
            CancelAttractingPayload();
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<ArkhamPlayerMotor>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (cameraOverride == null)
                cameraOverride = Camera.main;

            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();

            if (orbitCenter == null)
                orbitCenter = transform;

            if (inputProvider == null)
                inputProvider = GameInputProvider.EnsureOn(gameObject);

            EnsureAimIndicator();
        }

        void Update()
        {
            PumpInput();

            if (pullHeld && Time.time >= nextRepelTime)
                TickPull();

            TickOrbit();
            UpdateAimIndicator();
            ApplyMovementPenalty();
        }

        public void Configure(Camera sceneCamera, ArkhamEnemyManager manager, ArkhamPlayerMotor playerMotor)
        {
            cameraOverride = sceneCamera;
            enemyManager = manager;
            motor = playerMotor;
            orbitCenter = transform;
            if (inputProvider == null)
                inputProvider = GameInputProvider.EnsureOn(gameObject);

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            EnsureAimIndicator();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                CancelPullWithoutRepel();
        }

        public void StartPull()
        {
            if (Time.time < nextRepelTime || pullActive || !pullEnabled)
                return;

            pullHeld = true;
            pullActive = true;
            PlayPullAnimation();
            OnPullStarted.Invoke();
        }

        public void ReleasePull()
        {
            bool hadInput = pullHeld || pullActive || HasOrbitingPayload;
            pullHeld = false;

            if (!hadInput || Time.time < nextRepelTime || !repelEnabled)
                return;

            RepelOrbitingPayload();
            PlayRepelAnimation();
            CancelAttractingPayload();
            pullActive = false;
            nextRepelTime = Time.time + repelCooldown;
            OnPullStopped.Invoke();
        }

        public void ForcedEject()
        {
            Vector3 center = OrbitPosition();

            for (int i = orbitingObjects.Count - 1; i >= 0; i--)
            {
                MagneticObject magneticObject = orbitingObjects[i];
                if (magneticObject == null)
                    continue;

                Vector3 direction = magneticObject.transform.position - center;
                magneticObject.ForcedEject(direction, orbitRejectSpeed * 1.4f);
            }

            for (int i = pulledEnemies.Count - 1; i >= 0; i--)
            {
                ArkhamEnemy enemy = pulledEnemies[i];
                if (enemy == null)
                    continue;

                Vector3 direction = enemy.transform.position - center;
                enemy.RejectMagneticPull(center, orbitRejectSpeed * 1.2f);
            }

            orbitingObjects.Clear();
            pulledEnemies.Clear();
            pulledEnemyHolds.Clear();
            SetCharge(0f);
            pullActive = false;
            pullHeld = false;
            StopPullAnimation();
            nextRepelTime = Time.time + repelCooldown;
            UpdateAimIndicator();
        }

        void TogglePullRepel()
        {
            if (pullActive || HasOrbitingPayload)
            {
                ReleasePull();
                return;
            }

            StartPull();
        }

        void PumpInput()
        {
            if (inputProvider == null || Time.time < nextRepelTime)
                return;

            if (inputProvider.ConsumeBuffered(GameInputIntent.PullToggle))
                TogglePullRepel();
        }

        void TickPull()
        {
            if (!pullActive)
            {
                pullActive = true;
                PlayPullAnimation();
                OnPullStarted.Invoke();
            }

            Vector3 center = OrbitPosition();
            CollectMagneticObjects(center);
            CollectMagnetizedEnemies();
            PullObjects(center);
            PullEnemies();
        }

        void CollectMagneticObjects(Vector3 center)
        {
            int count = Physics.OverlapSphereNonAlloc(
                center,
                EffectivePullRadius,
                overlapBuffer,
                attractableLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                MagneticObject magneticObject = overlapBuffer[i].GetComponentInParent<MagneticObject>();
                if (magneticObject == null || !magneticObject.CanEnterOrbit || orbitingObjects.Contains(magneticObject) || attractingObjects.Contains(magneticObject))
                    continue;

                magneticObject.BeginAttract(orbitCenter);
                attractingObjects.Add(magneticObject);
            }
        }

        void CollectMagnetizedEnemies()
        {
            if (enemyManager == null)
                return;

            IReadOnlyList<ArkhamEnemy> enemies = enemyManager.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || !enemy.CanBePulledByMagnet || pulledEnemies.Contains(enemy))
                    continue;

                Vector3 delta = enemy.transform.position - transform.position;
                delta.y = 0f;
                float effectiveRadius = EffectivePullRadius;
                if (delta.sqrMagnitude > effectiveRadius * effectiveRadius)
                    continue;

                enemy.BeginMagneticPull();
                pulledEnemies.Add(enemy);
                pulledEnemyHolds.Add(new PulledEnemyHold
                {
                    ApproachDirection = CaptureEnemyAnchor(enemy),
                    AnchorPosition = Vector3.zero,
                    IsAnchored = false,
                    PullStartTime = Time.time,
                });
                OnEnemyPulled.Invoke(enemy);
                OnEnemyOrbited.Invoke(enemy);
            }
        }

        Vector3 CaptureEnemyAnchor(ArkhamEnemy enemy)
        {
            Vector3 delta = enemy.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f)
            {
                Vector3 fallback = transform.forward;
                fallback.y = 0f;
                return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
            }
            return delta.normalized;
        }

        void PullObjects(Vector3 center)
        {
            for (int i = attractingObjects.Count - 1; i >= 0; i--)
            {
                MagneticObject magneticObject = attractingObjects[i];
                if (magneticObject == null || !magneticObject.isActiveAndEnabled)
                {
                    attractingObjects.RemoveAt(i);
                    continue;
                }

                if (magneticObject.MagneticState != MagneticObjectState.Attracting)
                {
                    attractingObjects.RemoveAt(i);
                    continue;
                }

                Vector3 ringTarget = NearestRingTarget(center, magneticObject.transform.position);
                magneticObject.TickAttract(ringTarget, pullSpeed * pullSpeedMult, Time.deltaTime);

                if (!magneticObject.IsCloseEnoughForOrbit(center, orbitRadius))
                    continue;

                attractingObjects.RemoveAt(i);
                orbitingObjects.Add(magneticObject);
                AddCharge(magneticObject.MagneticMass);
                magneticObject.EnterOrbit(orbitCenter);
                OnObjectOrbited.Invoke(magneticObject);
            }
        }

        void PullEnemies()
        {
            for (int i = pulledEnemies.Count - 1; i >= 0; i--)
            {
                ArkhamEnemy enemy = pulledEnemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    RemovePulledEnemyAt(i);
                    continue;
                }

                if (!enemy.CanBePulledByMagnet)
                {
                    enemy.CancelMagneticPull();
                    RemovePulledEnemyAt(i);
                    continue;
                }

                PulledEnemyHold hold = pulledEnemyHolds[i];

                if (ShouldDemagnetizePulledEnemy(enemy, hold))
                {
                    DemagnetizeAndRelease(enemy, i);
                    continue;
                }

                if (hold.IsAnchored)
                    continue;

                Vector3 holdPoint = transform.position + hold.ApproachDirection * heldEnemyDistance;
                enemy.MagnetPullTowards(holdPoint, enemyPullSpeed * pullSpeedMult, Time.deltaTime);

                Vector3 delta = enemy.transform.position - holdPoint;
                delta.y = 0f;
                if (delta.sqrMagnitude <= enemyAnchorLockDistance * enemyAnchorLockDistance)
                {
                    hold.IsAnchored = true;
                    hold.AnchorPosition = enemy.transform.position;
                    pulledEnemyHolds[i] = hold;
                    enemy.AnchorMagneticHold();
                }
            }
        }

        void RemovePulledEnemyAt(int index)
        {
            pulledEnemies.RemoveAt(index);
            if (index < pulledEnemyHolds.Count)
                pulledEnemyHolds.RemoveAt(index);
        }

        bool ShouldDemagnetizePulledEnemy(ArkhamEnemy enemy, PulledEnemyHold hold)
        {
            if (magnetizedHoldTimeout > 0f && Time.time - hold.PullStartTime >= magnetizedHoldTimeout)
                return true;

            if (magnetizedMaxDistance > 0f)
            {
                Vector3 delta = enemy.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > magnetizedMaxDistance * magnetizedMaxDistance)
                    return true;
            }

            return false;
        }

        void DemagnetizeAndRelease(ArkhamEnemy enemy, int index)
        {
            enemy.SetMarkState(MagneticMarkState.Normal);
            enemy.CancelMagneticPull();
            RemovePulledEnemyAt(index);
        }

        void TickOrbit()
        {
            int total = orbitingObjects.Count;
            if (total == 0)
                return;

            orbitAngle += orbitAngularSpeed * Time.deltaTime;
            Vector3 center = OrbitPosition();
            int slot = 0;

            for (int i = orbitingObjects.Count - 1; i >= 0; i--)
            {
                MagneticObject magneticObject = orbitingObjects[i];
                if (magneticObject == null || !magneticObject.isActiveAndEnabled || magneticObject.MagneticState != MagneticObjectState.InOrbit)
                {
                    orbitingObjects.RemoveAt(i);
                    continue;
                }

                magneticObject.TickOrbit(OrbitSlot(center, total, slot), Time.deltaTime);
                slot++;
            }
        }

        void RepelOrbitingPayload()
        {
            int total = orbitingObjects.Count + pulledEnemies.Count;
            bool empty = total == 0;
            Vector3 manualAim = AimDirection();
            Vector3 primaryAim = ResolvePrimaryAutoAim(manualAim);
            int slot = 0;
            FaceRepelDirection(primaryAim);

            float effectiveRepelSpeed = repelSpeed * repelSpeedMult;
            int effectiveDamageBonus = repelDamageBonus;
            int effectivePierceBonus = repelPiercingBonus;

            for (int i = orbitingObjects.Count - 1; i >= 0; i--)
            {
                MagneticObject magneticObject = orbitingObjects[i];
                if (magneticObject == null)
                    continue;

                Vector3 coneDir = DirectionInCone(primaryAim, slot, total);
                Vector3 launchDir = ResolveProjectileAutoAim(magneticObject.transform.position, coneDir);
                magneticObject.Repel(launchDir, effectiveRepelSpeed, effectiveDamageBonus, effectivePierceBonus);
                slot++;
            }

            for (int i = pulledEnemies.Count - 1; i >= 0; i--)
            {
                ArkhamEnemy enemy = pulledEnemies[i];
                if (enemy == null)
                    continue;

                Vector3 coneDir = DirectionInCone(primaryAim, slot, total);
                Vector3 launchDir = ResolveMagnetizedRepelDirection(enemy, coneDir);
                enemy.MagnetRepel(launchDir, effectiveRepelSpeed * 0.78f, magnetizedEnemyDamage + effectiveDamageBonus, magnetizedEnemyRecoilDamage);
                slot++;
            }

            orbitingObjects.Clear();
            pulledEnemies.Clear();
            pulledEnemyHolds.Clear();
            SetCharge(0f);
            OnRepelFired.Invoke(empty);
            UpdateAimIndicator();
        }

        void CancelAttractingPayload()
        {
            for (int i = attractingObjects.Count - 1; i >= 0; i--)
            {
                MagneticObject magneticObject = attractingObjects[i];
                if (magneticObject != null)
                    magneticObject.StopAttracting();
            }
            attractingObjects.Clear();
        }

        void CancelPullWithoutRepel()
        {
            bool wasPulling = pullActive || pullHeld;
            pullHeld = false;
            pullActive = false;
            CancelAttractingPayload();

            if (wasPulling)
            {
                StopPullAnimation();
                OnPullStopped.Invoke();
            }
        }

        void PlayPullAnimation()
        {
            if (animator == null)
                return;

            animator.ResetTrigger(RepelHash);
            animator.SetBool(PullingHash, true);
            animator.SetTrigger(PullHash);
        }

        void PlayRepelAnimation()
        {
            if (animator == null)
                return;

            animator.ResetTrigger(PullHash);
            animator.SetBool(PullingHash, false);
            animator.SetTrigger(RepelHash);
        }

        void StopPullAnimation()
        {
            if (animator != null)
                animator.SetBool(PullingHash, false);
        }

        Vector3 OrbitPosition()
        {
            Vector3 center = orbitCenter != null ? orbitCenter.position : transform.position;
            center.y = transform.position.y + orbitHeight;
            return center;
        }

        Vector3 NearestRingTarget(Vector3 center, Vector3 fromWorldPosition)
        {
            Vector3 delta = fromWorldPosition - center;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f)
                delta = transform.forward;
            return center + delta.normalized * orbitRadius;
        }

        Vector3 OrbitSlot(Vector3 center, int total, int slot)
        {
            float angle = orbitAngle + (360f / Mathf.Max(1, total)) * slot;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * orbitRadius;
            return center + offset;
        }

        Vector3 AimDirection()
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f)
                return fwd.normalized;

            if (motor != null && motor.WorldMoveDirection.sqrMagnitude > 0.01f)
                return motor.WorldMoveDirection.normalized;

            return Vector3.forward;
        }

        void FaceRepelDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        Vector3 DirectionInCone(Vector3 aim, int slot, int total)
        {
            if (total <= 1)
                return aim.normalized;

            float t = total <= 1 ? 0.5f : slot / (float)(total - 1);
            float offset = Mathf.Lerp(-repelConeAngle * 0.5f, repelConeAngle * 0.5f, t);
            return (Quaternion.AngleAxis(offset, Vector3.up) * aim).normalized;
        }

        void AddCharge(float amount)
        {
            SetCharge(currentCharge + amount);
        }

        void SetCharge(float value)
        {
            float capacity = EffectiveMaxCapacity;
            float ceiling = capacity * (1f + Mathf.Max(0f, overflowHeadroom));
            currentCharge = Mathf.Clamp(value, 0f, ceiling);
            OnChargeChanged.Invoke(currentCharge, capacity);
        }

        void ApplyMovementPenalty()
        {
            if (motor == null)
                return;

            float capacity = EffectiveMaxCapacity;
            float ratio = capacity > 0f ? Mathf.Clamp01(currentCharge / capacity) : 0f;
            float effectivePenalty = chargePenaltyAtFull * chargeMobilityPenaltyMult;
            motor.Acceleration = Mathf.Max(0.25f, 1f - ratio * effectivePenalty);
        }

        void EnsureAimIndicator()
        {
            if (!autoCreateAimIndicator)
                return;

            Material indicatorMaterial = CreateIndicatorMaterial(aimIndicatorColor);

            if (aimLine == null)
            {
                GameObject lineObject = new GameObject("Repel Aim Line");
                lineObject.transform.SetParent(transform, false);
                aimLine = lineObject.AddComponent<LineRenderer>();
                aimLine.useWorldSpace = true;
                aimLine.positionCount = 2;
                aimLine.widthMultiplier = aimLineWidth;
                aimLine.numCapVertices = 4;
                aimLine.material = indicatorMaterial;
                aimLine.enabled = false;
            }

            if (aimTip == null)
            {
                GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tip.name = "Repel Aim Tip";
                tip.transform.SetParent(transform, false);
                tip.transform.localScale = new Vector3(0.28f, 0.04f, 0.55f);
                Collider tipCollider = tip.GetComponent<Collider>();
                if (tipCollider != null)
                    DestroyLocalObject(tipCollider);

                Renderer renderer = tip.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = indicatorMaterial;

                aimTip = tip.transform;
                aimTip.gameObject.SetActive(false);
            }
        }

        void UpdateAimIndicator()
        {
            bool visible = pullActive || HasOrbitingPayload;
            if (aimLine != null)
                aimLine.enabled = visible;

            if (aimTip != null)
                aimTip.gameObject.SetActive(visible);

            if (!visible)
                return;

            Vector3 aim = AimDirection();
            Vector3 start = transform.position + Vector3.up * aimIndicatorHeight;
            Vector3 end = start + aim * aimIndicatorLength;

            if (aimLine != null)
            {
                aimLine.SetPosition(0, start);
                aimLine.SetPosition(1, end);
            }

            if (aimTip != null)
            {
                aimTip.position = end;
                aimTip.rotation = Quaternion.LookRotation(aim, Vector3.up);
            }
        }

        static Material CreateIndicatorMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader)
            {
                color = color
            };

            return material;
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

        Vector3 ResolvePrimaryAutoAim(Vector3 fallback)
        {
            return ResolveProjectileAutoAim(transform.position, fallback);
        }

        Vector3 ResolveProjectileAutoAim(Vector3 origin, Vector3 fallback)
        {
            if (repelAutoAimRadius <= 0f)
                return fallback;

            if (TryFindNearestEnemyDirection(origin, repelAutoAimRadius, null, out Vector3 dir))
                return dir;

            return fallback;
        }

        Vector3 ResolveMagnetizedRepelDirection(ArkhamEnemy projectile, Vector3 fallback)
        {
            if (projectile == null)
                return fallback;

            Vector3 origin = projectile.transform.position;
            float enemyRadius = repelAutoAimRadius;
            float wallRadius = magnetizedRepelWallAimRadius;
            float searchRadius = Mathf.Max(enemyRadius, wallRadius);
            if (searchRadius <= 0f)
                return fallback;

            float bestDistSqr = searchRadius * searchRadius;
            Vector3 bestTarget = Vector3.zero;
            bool found = false;

            if (enemyRadius > 0f && TryFindNearestEnemyPoint(origin, enemyRadius, projectile, out Vector3 enemyPoint, out float enemyDistSqr))
            {
                bestDistSqr = enemyDistSqr;
                bestTarget = enemyPoint;
                found = true;
            }

            if (wallRadius > 0f)
            {
                ArenaSystem arena = ResolveArenaSystem();
                if (arena != null)
                {
                    Vector3 wallPoint = NearestArenaWallPoint(origin, arena.PlayableBounds);
                    Vector3 delta = wallPoint - origin;
                    delta.y = 0f;
                    float distSqr = delta.sqrMagnitude;
                    float wallMaxSqr = wallRadius * wallRadius;
                    if (distSqr > 0.04f && distSqr <= wallMaxSqr && distSqr <= bestDistSqr)
                    {
                        bestDistSqr = distSqr;
                        bestTarget = wallPoint;
                        found = true;
                    }
                }
            }

            if (!found)
                return fallback;

            Vector3 toTarget = bestTarget - origin;
            toTarget.y = 0f;
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : fallback;
        }

        bool TryFindNearestEnemyDirection(Vector3 origin, float radius, ArkhamEnemy exclude, out Vector3 direction)
        {
            if (TryFindNearestEnemyPoint(origin, radius, exclude, out Vector3 point, out _))
            {
                Vector3 delta = point - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    direction = delta.normalized;
                    return true;
                }
            }

            direction = Vector3.zero;
            return false;
        }

        bool TryFindNearestEnemyPoint(Vector3 origin, float radius, ArkhamEnemy exclude, out Vector3 point, out float distanceSqr)
        {
            point = Vector3.zero;
            distanceSqr = float.MaxValue;
            if (enemyManager == null || radius <= 0f)
                return false;

            float bestDistSqr = radius * radius;
            bool found = false;

            IReadOnlyList<ArkhamEnemy> roster = enemyManager.Enemies;
            for (int i = 0; i < roster.Count; i++)
            {
                ArkhamEnemy other = roster[i];
                if (other == null || other == exclude || !other.IsAlive)
                    continue;
                if (pulledEnemies.Contains(other))
                    continue;

                Vector3 delta = other.transform.position - origin;
                delta.y = 0f;
                float distSqr = delta.sqrMagnitude;
                if (distSqr < 0.04f || distSqr > bestDistSqr)
                    continue;

                bestDistSqr = distSqr;
                point = other.transform.position;
                found = true;
            }

            distanceSqr = bestDistSqr;
            return found;
        }

        ArenaSystem ResolveArenaSystem()
        {
            if (cachedArena == null)
                cachedArena = FindFirstObjectByType<ArenaSystem>();
            return cachedArena;
        }

        static Vector3 NearestArenaWallPoint(Vector3 position, Bounds bounds)
        {
            float dxMin = Mathf.Abs(position.x - bounds.min.x);
            float dxMax = Mathf.Abs(bounds.max.x - position.x);
            float dzMin = Mathf.Abs(position.z - bounds.min.z);
            float dzMax = Mathf.Abs(bounds.max.z - position.z);

            float min = dxMin;
            Vector3 point = new Vector3(bounds.min.x, position.y, position.z);

            if (dxMax < min)
            {
                min = dxMax;
                point = new Vector3(bounds.max.x, position.y, position.z);
            }
            if (dzMin < min)
            {
                min = dzMin;
                point = new Vector3(position.x, position.y, bounds.min.z);
            }
            if (dzMax < min)
            {
                point = new Vector3(position.x, position.y, bounds.max.z);
            }

            return point;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? OrbitPosition() : transform.position + Vector3.up * orbitHeight;
            Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.18f);
            Gizmos.DrawWireSphere(center, pullRadius);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(center, orbitRadius);

            Vector3 aim = Application.isPlaying ? AimDirection() : transform.forward;
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.35f);
            Gizmos.DrawRay(center, Quaternion.AngleAxis(-repelConeAngle * 0.5f, Vector3.up) * aim * pullRadius);
            Gizmos.DrawRay(center, Quaternion.AngleAxis(repelConeAngle * 0.5f, Vector3.up) * aim * pullRadius);
        }
    }
}
