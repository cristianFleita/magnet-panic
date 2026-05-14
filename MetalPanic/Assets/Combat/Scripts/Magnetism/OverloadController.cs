using MagnetPanic.Combat.Scoring;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    public enum OverloadState
    {
        Normal,
        Critical,
        GracePeriod,
        Overload,
        Recovery
    }

    [RequireComponent(typeof(MagnetismController))]
    public sealed class OverloadController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] MagnetismController magnetism;
        [SerializeField] ArkhamPlayerMotor motor;
        [SerializeField] ArkhamSimpleCameraFollow cameraRig;
        [SerializeField] ArkhamEnemyManager enemyManager;

        [Header("Thresholds")]
        [Range(0.5f, 0.95f)]
        [SerializeField] float criticalThreshold = 0.7f;

        [Header("Grace Period")]
        [SerializeField] float overloadGracePeriod = 1.5f;

        [Header("Explosion")]
        [SerializeField] int baseOverloadDamage = 4;
        [SerializeField] float damagePerCharge = 2f;
        [SerializeField] float overloadRadius = 6f;
        [SerializeField] float overloadKnockbackDistance = 2.5f;
        [SerializeField] LayerMask targetLayers = ~0;

        [Header("Recovery")]
        [SerializeField] float overloadCooldown = 2f;
        [SerializeField] float overloadStunDuration = 0.5f;

        [Header("Camera")]
        [SerializeField] float overloadShakeAmplitude = 0.3f;
        [SerializeField] float overloadShakeDuration = 0.3f;

        [Header("Debug")]
        [SerializeField] bool debugLogs = true;
        [SerializeField] bool showRuntimeRing = true;
        [SerializeField] Color ringColor = new Color(1f, 0.25f, 0.1f, 0.9f);
        [SerializeField] float ringDuration = 0.45f;
        [SerializeField] int ringSegments = 48;
        [SerializeField] float ringHeight = 0.08f;
        [SerializeField] float ringWidth = 0.12f;

        [Header("Events (HUD/VFX/SFX)")]
        public UnityEvent OnEnteredCritical = new UnityEvent();
        public UnityEvent OnExitedCritical = new UnityEvent();
        public UnityEvent OnEnteredGrace = new UnityEvent();
        public UnityEvent OnExitedGrace = new UnityEvent();
        public UnityEvent<Vector3, float, int> OnOverloadExploded = new UnityEvent<Vector3, float, int>();
        public UnityEvent OnRecoveryStarted = new UnityEvent();
        public UnityEvent OnRecoveryEnded = new UnityEvent();
        public UnityEvent<OverloadState> OnStateChanged = new UnityEvent<OverloadState>();
        public UnityEvent<float> OnGraceTimerChanged = new UnityEvent<float>();

        readonly Collider[] overlapBuffer = new Collider[64];

        OverloadState state = OverloadState.Normal;
        float graceTimer;
        float recoveryTimer;
        LineRenderer ring;
        float ringTimer;

        public OverloadState State => state;
        public float GraceTimeRemaining => graceTimer;
        public float GraceTimeNormalized => overloadGracePeriod > 0f ? Mathf.Clamp01(graceTimer / overloadGracePeriod) : 0f;
        public float CriticalThreshold => criticalThreshold;
        public float OverloadRadius => overloadRadius;

        void Reset()
        {
            magnetism = GetComponent<MagnetismController>();
            motor = GetComponent<ArkhamPlayerMotor>();
        }

        void Awake()
        {
            if (magnetism == null)
                magnetism = GetComponent<MagnetismController>();
            if (motor == null)
                motor = GetComponent<ArkhamPlayerMotor>();
            if (cameraRig == null)
                cameraRig = FindFirstObjectByType<ArkhamSimpleCameraFollow>();
            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();

            graceTimer = overloadGracePeriod;
        }

        public void Configure(
            MagnetismController magneticController,
            ArkhamPlayerMotor playerMotor,
            ArkhamSimpleCameraFollow followCamera,
            ArkhamEnemyManager manager)
        {
            if (magneticController != null)
                magnetism = magneticController;
            if (playerMotor != null)
                motor = playerMotor;
            if (followCamera != null)
                cameraRig = followCamera;
            if (manager != null)
                enemyManager = manager;
        }

        void Update()
        {
            TickRing();

            if (magnetism == null || magnetism.MaxCapacity <= 0f)
                return;

            float ratio = magnetism.CurrentCharge / magnetism.MaxCapacity;

            switch (state)
            {
                case OverloadState.Normal:
                    if (ratio >= 1f)
                        EnterGracePeriod();
                    else if (ratio >= criticalThreshold)
                        EnterCritical();
                    break;

                case OverloadState.Critical:
                    if (ratio >= 1f)
                        EnterGracePeriod();
                    else if (ratio < criticalThreshold)
                        EnterNormal();
                    break;

                case OverloadState.GracePeriod:
                    if (ratio < 1f)
                    {
                        ExitGraceToCharge(ratio);
                        break;
                    }

                    graceTimer -= Time.deltaTime;
                    OnGraceTimerChanged.Invoke(graceTimer);
                    if (graceTimer <= 0f)
                        TriggerOverload();
                    break;

                case OverloadState.Recovery:
                    recoveryTimer -= Time.deltaTime;
                    if (recoveryTimer <= 0f)
                        EndRecovery();
                    break;
            }
        }

        void EnterNormal()
        {
            ChangeState(OverloadState.Normal);
            OnExitedCritical.Invoke();
            Log("Overload → Normal");
        }

        void EnterCritical()
        {
            if (state == OverloadState.Critical)
                return;
            ChangeState(OverloadState.Critical);
            OnEnteredCritical.Invoke();
            Log($"Overload → Critical (charge {magnetism.CurrentCharge:0.0}/{magnetism.MaxCapacity:0})");
        }

        void EnterGracePeriod()
        {
            if (state == OverloadState.GracePeriod)
                return;

            graceTimer = overloadGracePeriod;
            magnetism.SetPullEnabled(false);
            magnetism.CancelAttracting();
            ChangeState(OverloadState.GracePeriod);
            OnEnteredGrace.Invoke();
            OnGraceTimerChanged.Invoke(graceTimer);
            Log($"Overload → GracePeriod ({overloadGracePeriod:0.00}s para repeler)");
        }

        void ExitGraceToCharge(float ratio)
        {
            magnetism.SetPullEnabled(true);
            graceTimer = overloadGracePeriod;
            OnExitedGrace.Invoke();

            if (ratio >= criticalThreshold)
            {
                ChangeState(OverloadState.Critical);
                OnEnteredCritical.Invoke();
                Log("Overload cancelado → Critical (crisis evitada)");
            }
            else
            {
                ChangeState(OverloadState.Normal);
                OnExitedCritical.Invoke();
                Log("Overload cancelado → Normal (crisis evitada)");
            }
        }

        void TriggerOverload()
        {
            ChangeState(OverloadState.Overload);
            magnetism.SetPullEnabled(false);
            magnetism.SetRepelEnabled(false);

            float chargeAtOverload = magnetism.CurrentCharge;
            int damage = ComputeOverloadDamage(chargeAtOverload);
            Vector3 origin = transform.position;

            int hits = ApplyAoeDamage(origin, damage);

            magnetism.ForcedEject();

            if (motor != null)
                motor.SetMovementLocked(true, true);

            if (cameraRig != null)
                cameraRig.Shake(overloadShakeAmplitude, overloadShakeDuration);

            OnOverloadExploded.Invoke(origin, overloadRadius, hits);
            SpawnRing(origin);
            Log($"💥 OVERLOAD: damage={damage}, hits={hits}, radius={overloadRadius:0.0}m, charge={chargeAtOverload:0.0}");

            recoveryTimer = Mathf.Max(overloadCooldown, overloadStunDuration);
            ChangeState(OverloadState.Recovery);
            OnRecoveryStarted.Invoke();

            Invoke(nameof(ReleaseMovementLock), overloadStunDuration);
        }

        void ReleaseMovementLock()
        {
            if (motor != null)
                motor.SetMovementLocked(false);
        }

        void EndRecovery()
        {
            if (motor != null)
                motor.SetMovementLocked(false);

            magnetism.SetPullEnabled(true);
            magnetism.SetRepelEnabled(true);

            OnRecoveryEnded.Invoke();
            Log("Recovery terminado → Pull/Repel habilitados");

            float ratio = magnetism.MaxCapacity > 0f ? magnetism.CurrentCharge / magnetism.MaxCapacity : 0f;
            if (ratio >= criticalThreshold)
            {
                ChangeState(OverloadState.Critical);
                OnEnteredCritical.Invoke();
            }
            else
            {
                ChangeState(OverloadState.Normal);
            }
        }

        int ComputeOverloadDamage(float chargeAtOverload)
        {
            float perCharge = damagePerCharge > 0f ? damagePerCharge : 1f;
            return baseOverloadDamage + Mathf.FloorToInt(chargeAtOverload / perCharge);
        }

        int ApplyAoeDamage(Vector3 origin, int damage)
        {
            int hits = 0;
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                overloadRadius,
                overlapBuffer,
                targetLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                ArkhamEnemy enemy = overlapBuffer[i].GetComponentInParent<ArkhamEnemy>();
                if (enemy == null || !enemy.IsAlive)
                    continue;

                float distance = Vector3.Distance(origin, enemy.transform.position);
                float distanceFactor = Mathf.Clamp01(1f - distance / overloadRadius);
                float knockback = overloadKnockbackDistance * distanceFactor;

                enemy.TagNextDamageMethod(KillMethod.Overload);
                enemy.ReceiveMagneticImpact(damage, origin, knockback, true);
                hits++;
            }

            return hits;
        }

        void ChangeState(OverloadState next)
        {
            if (state == next)
                return;
            state = next;
            OnStateChanged.Invoke(state);
        }

        void Log(string message)
        {
            if (!debugLogs)
                return;
            Debug.Log($"[Overload] {message}", this);
        }

        void EnsureRing()
        {
            if (ring != null)
                return;

            GameObject ringObject = new GameObject("Overload Ring");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.numCapVertices = 0;
            ring.numCornerVertices = 0;
            ring.widthMultiplier = ringWidth;
            ring.positionCount = Mathf.Max(8, ringSegments);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            ring.material = new Material(shader) { color = ringColor };
            ring.startColor = ringColor;
            ring.endColor = ringColor;
            ring.enabled = false;
        }

        void SpawnRing(Vector3 origin)
        {
            if (!showRuntimeRing)
                return;

            EnsureRing();

            int segments = ring.positionCount;
            float step = (Mathf.PI * 2f) / segments;
            Vector3 ground = new Vector3(origin.x, origin.y + ringHeight, origin.z);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * step;
                Vector3 point = ground + new Vector3(Mathf.Cos(angle) * overloadRadius, 0f, Mathf.Sin(angle) * overloadRadius);
                ring.SetPosition(i, point);
            }

            ring.startColor = ringColor;
            ring.endColor = ringColor;
            ring.enabled = true;
            ringTimer = ringDuration;
        }

        void TickRing()
        {
            if (ring == null || !ring.enabled || ringTimer <= 0f)
                return;

            ringTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(ringTimer / Mathf.Max(0.0001f, ringDuration));
            Color faded = ringColor;
            faded.a = ringColor.a * t;
            ring.startColor = faded;
            ring.endColor = faded;

            if (ringTimer <= 0f)
                ring.enabled = false;
        }

        void OnDrawGizmos()
        {
            Color color = state == OverloadState.Overload || state == OverloadState.GracePeriod
                ? new Color(1f, 0.25f, 0.1f, 0.55f)
                : new Color(1f, 0.25f, 0.1f, 0.25f);
            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position, overloadRadius);
        }
    }
}
