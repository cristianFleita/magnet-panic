using UnityEngine;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Bridges <see cref="ArkhamCombatController"/> events to one-shot melee VFX:
    /// DashVfx fires when a strike telegraphs/lunges toward a target (OnTrajectory),
    /// MeleeVfx fires when the strike actually connects (OnHit).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatVfxBinder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ArkhamCombatController combat;
        [SerializeField, Tooltip("Anchor used to position spawned VFX. Defaults to this transform.")]
        Transform attachPoint;

        [Header("Dash VFX (lunge / telegraph)")]
        [SerializeField] GameObject dashVfxPrefab;
        [SerializeField] Vector3 dashLocalOffset = Vector3.zero;
        [SerializeField, Tooltip("Detach from the player so the dust stays where the lunge started.")]
        bool dashInWorldSpace = true;
        [SerializeField, Tooltip("Rotate the spawned dash VFX to face the lunge target.")]
        bool dashFaceTarget = true;
        [SerializeField, Tooltip("Auto-destroy the spawned dash VFX after this many seconds. 0 to keep it alive.")]
        float dashLifetime = 1.5f;

        [Header("Melee Swing VFX (player-side trail)")]
        [SerializeField] GameObject meleeVfxPrefab;
        [SerializeField, Tooltip("Optional bone/transform the trail is parented to (e.g. right-hand bone). When set, overrides the attachPoint and meleeSpawnAtTarget so the trail follows the bone motion naturally.")]
        Transform meleeAttachBone;
        [SerializeField, Tooltip("Spawn at the struck enemy's transform instead of the player anchor. Ignored when meleeAttachBone is set.")]
        bool meleeSpawnAtTarget = false;
        [SerializeField] Vector3 meleeLocalOffset = Vector3.zero;
        [SerializeField, Tooltip("Detach from the player so the swing stays in world space. Forced off when meleeAttachBone is set.")]
        bool meleeInWorldSpace = true;
        [SerializeField, Tooltip("Rotate the spawned melee VFX to face the struck enemy.")]
        bool meleeFaceTarget = true;
        [SerializeField, Tooltip("Auto-destroy the spawned melee VFX after this many seconds. 0 to keep it alive.")]
        float meleeLifetime = 1.5f;

        [Header("Impact VFX (enemy-side burst)")]
        [SerializeField, Tooltip("One-shot effect spawned on the struck enemy when the strike connects.")]
        GameObject impactVfxPrefab;
        [SerializeField, Tooltip("Local offset applied at the enemy transform (e.g. chest height).")]
        Vector3 impactLocalOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Tooltip("Detach from the enemy so the burst stays put even if they get knocked around.")]
        bool impactInWorldSpace = true;
        [SerializeField, Tooltip("Rotate the spawned impact VFX to face back toward the player.")]
        bool impactFaceAttacker = true;
        [SerializeField, Tooltip("Auto-destroy the spawned impact VFX after this many seconds. 0 to keep it alive.")]
        float impactLifetime = 1.0f;

        void Reset()
        {
            combat = GetComponent<ArkhamCombatController>();
        }

        void Awake()
        {
            if (combat == null)
                combat = GetComponent<ArkhamCombatController>();

            if (attachPoint == null)
                attachPoint = transform;
        }

        void OnEnable()
        {
            if (combat == null)
                return;

            combat.OnTrajectory.AddListener(HandleTrajectory);
            combat.OnHit.AddListener(HandleHit);
        }

        void OnDisable()
        {
            if (combat == null)
                return;

            combat.OnTrajectory.RemoveListener(HandleTrajectory);
            combat.OnHit.RemoveListener(HandleHit);
        }

        void HandleTrajectory(ArkhamEnemy target)
        {
            Quaternion rotation = attachPoint.rotation;
            if (dashFaceTarget && target != null)
            {
                Vector3 toTarget = target.transform.position - attachPoint.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                    rotation = Quaternion.LookRotation(toTarget.normalized);
            }

            SpawnOneShot(dashVfxPrefab, attachPoint, dashLocalOffset, rotation, dashInWorldSpace, dashLifetime);
        }

        void HandleHit(ArkhamEnemy target)
        {
            bool useBone = meleeAttachBone != null;
            Transform meleeAnchor = useBone
                ? meleeAttachBone
                : (meleeSpawnAtTarget && target != null ? target.transform : attachPoint);
            bool meleeUseWorldSpace = !useBone && meleeInWorldSpace;

            Quaternion meleeRotation = meleeAnchor.rotation;
            if (!useBone && meleeFaceTarget && target != null)
            {
                Vector3 toTarget = target.transform.position - attachPoint.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                    meleeRotation = Quaternion.LookRotation(toTarget.normalized);
            }

            SpawnOneShot(meleeVfxPrefab, meleeAnchor, meleeLocalOffset, meleeRotation, meleeUseWorldSpace, meleeLifetime);

            if (impactVfxPrefab != null && target != null)
            {
                Transform impactAnchor = target.transform;
                Quaternion impactRotation = impactAnchor.rotation;
                if (impactFaceAttacker)
                {
                    Vector3 toAttacker = attachPoint.position - target.transform.position;
                    toAttacker.y = 0f;
                    if (toAttacker.sqrMagnitude > 0.0001f)
                        impactRotation = Quaternion.LookRotation(toAttacker.normalized);
                }

                SpawnOneShot(impactVfxPrefab, impactAnchor, impactLocalOffset, impactRotation, impactInWorldSpace, impactLifetime);
            }
        }

        static void SpawnOneShot(GameObject prefab, Transform anchor, Vector3 localOffset, Quaternion worldRotation, bool worldSpace, float lifetime)
        {
            if (prefab == null || anchor == null)
                return;

            GameObject instance;
            if (worldSpace)
            {
                Vector3 worldPos = anchor.TransformPoint(localOffset);
                instance = Instantiate(prefab, worldPos, worldRotation);
            }
            else
            {
                instance = Instantiate(prefab, anchor);
                instance.transform.localPosition = localOffset;
                instance.transform.localRotation = Quaternion.identity;
            }

            instance.name = prefab.name + " (Runtime)";

            if (lifetime > 0f)
                Destroy(instance, lifetime);
        }
    }
}
