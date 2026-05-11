using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(ArkhamPlayerMotor))]
    public sealed class MagnetismController : MonoBehaviour
    {
        [Header("References")]
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
        [SerializeField] float orbitRejectSpeed = 7f;
        [SerializeField] float chargePenaltyAtFull = 0.2f;

        [Header("Repel")]
        [SerializeField] float repelConeAngle = 50f;
        [SerializeField] float repelCooldown = 0.25f;
        [SerializeField] float repelSpeed = 16f;
        [SerializeField] int magnetizedEnemyDamage = 5;

        [Header("Enemy Pull")]
        [SerializeField] float heldEnemyDistance = 1.9f;
        [SerializeField] float heldEnemySpacing = 0.9f;

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

        float currentCharge;
        float nextRepelTime;
        float orbitAngle;
        bool pullHeld;
        bool pullActive;
        bool pullEnabled = true;
        bool repelEnabled = true;

        public float CurrentCharge => currentCharge;
        public float MaxCapacity => maxCapacity;
        public bool IsPulling => pullActive;
        public bool HasOrbitingPayload => orbitingObjects.Count > 0 || pulledEnemies.Count > 0;
        public bool PullEnabled => pullEnabled;
        public bool RepelEnabled => repelEnabled;

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
            OnPullStarted.Invoke();
        }

        public void ReleasePull()
        {
            bool hadInput = pullHeld || pullActive || HasOrbitingPayload;
            pullHeld = false;

            if (!hadInput || Time.time < nextRepelTime || !repelEnabled)
                return;

            RepelOrbitingPayload();
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
            SetCharge(0f);
            pullActive = false;
            pullHeld = false;
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
                pullRadius,
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
                if (delta.sqrMagnitude > pullRadius * pullRadius)
                    continue;

                enemy.BeginMagneticPull();
                pulledEnemies.Add(enemy);
                OnEnemyPulled.Invoke(enemy);
                OnEnemyOrbited.Invoke(enemy);
            }
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

                magneticObject.TickAttract(center, pullSpeed, Time.deltaTime);

                if (!magneticObject.IsCloseEnoughForOrbit(center, orbitRadius))
                    continue;

                if (CanFit(magneticObject.MagneticMass))
                {
                    attractingObjects.RemoveAt(i);
                    orbitingObjects.Add(magneticObject);
                    AddCharge(magneticObject.MagneticMass);
                    magneticObject.EnterOrbit(orbitCenter);
                    OnObjectOrbited.Invoke(magneticObject);
                }
                else
                {
                    attractingObjects.RemoveAt(i);
                    magneticObject.RejectFromOrbit(center, orbitRejectSpeed);
                }
            }
        }

        void PullEnemies()
        {
            Vector3 aim = AimDirection();
            for (int i = pulledEnemies.Count - 1; i >= 0; i--)
            {
                ArkhamEnemy enemy = pulledEnemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    pulledEnemies.RemoveAt(i);
                    continue;
                }

                if (!enemy.CanBePulledByMagnet)
                {
                    enemy.CancelMagneticPull();
                    pulledEnemies.RemoveAt(i);
                    continue;
                }

                enemy.MagnetPullTowards(EnemyHoldPosition(aim, i, pulledEnemies.Count), enemyPullSpeed, Time.deltaTime);
            }
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
            Vector3 aim = AimDirection();
            int slot = 0;

            for (int i = orbitingObjects.Count - 1; i >= 0; i--)
            {
                MagneticObject magneticObject = orbitingObjects[i];
                if (magneticObject == null)
                    continue;

                magneticObject.Repel(DirectionInCone(aim, slot, total), repelSpeed);
                slot++;
            }

            for (int i = pulledEnemies.Count - 1; i >= 0; i--)
            {
                ArkhamEnemy enemy = pulledEnemies[i];
                if (enemy == null)
                    continue;

                enemy.MagnetRepel(DirectionInCone(aim, slot, total), repelSpeed * 0.78f, magnetizedEnemyDamage);
                slot++;
            }

            orbitingObjects.Clear();
            pulledEnemies.Clear();
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
                OnPullStopped.Invoke();
        }

        Vector3 OrbitPosition()
        {
            Vector3 center = orbitCenter != null ? orbitCenter.position : transform.position;
            center.y = transform.position.y + orbitHeight;
            return center;
        }

        Vector3 OrbitSlot(Vector3 center, int total, int slot)
        {
            float angle = orbitAngle + (360f / Mathf.Max(1, total)) * slot;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * orbitRadius;
            return center + offset;
        }

        Vector3 EnemyHoldPosition(Vector3 aim, int slot, int total)
        {
            Vector3 center = transform.position + aim.normalized * heldEnemyDistance;
            Vector3 right = Quaternion.AngleAxis(90f, Vector3.up) * aim.normalized;
            float offset = slot - (Mathf.Max(1, total) - 1f) * 0.5f;
            center += right * offset * heldEnemySpacing;
            center.y = transform.position.y;
            return center;
        }

        Vector3 AimDirection()
        {
            if (inputProvider != null && inputProvider.AimWorldDirection.sqrMagnitude > 0.01f)
                return inputProvider.AimWorldDirection.normalized;

            if (motor != null && motor.WorldMoveDirection.sqrMagnitude > 0.01f)
                return motor.WorldMoveDirection.normalized;

            return transform.forward;
        }

        Vector3 DirectionInCone(Vector3 aim, int slot, int total)
        {
            if (total <= 1)
                return aim.normalized;

            float t = total <= 1 ? 0.5f : slot / (float)(total - 1);
            float offset = Mathf.Lerp(-repelConeAngle * 0.5f, repelConeAngle * 0.5f, t);
            return (Quaternion.AngleAxis(offset, Vector3.up) * aim).normalized;
        }

        bool CanFit(float mass)
        {
            return currentCharge + mass <= maxCapacity + 0.001f;
        }

        void AddCharge(float amount)
        {
            SetCharge(currentCharge + amount);
        }

        void SetCharge(float value)
        {
            currentCharge = Mathf.Clamp(value, 0f, maxCapacity);
            OnChargeChanged.Invoke(currentCharge, maxCapacity);
        }

        void ApplyMovementPenalty()
        {
            if (motor == null)
                return;

            float ratio = maxCapacity > 0f ? Mathf.Clamp01(currentCharge / maxCapacity) : 0f;
            motor.Acceleration = Mathf.Max(0.25f, 1f - ratio * chargePenaltyAtFull);
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
