using UnityEngine;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Bridges the player's <see cref="MagnetismController"/> events to two VFX prefabs:
    /// AttractVfx (looping, on while pull is held) and RepelVfx (one-shot, on release).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagnetismVfxBinder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] MagnetismController magnetism;
        [SerializeField, Tooltip("Anchor used to position spawned VFX. Defaults to this transform.")]
        Transform attachPoint;

        [Header("Pull VFX (looping)")]
        [SerializeField] GameObject attractVfxPrefab;
        [SerializeField] Vector3 attractLocalOffset = Vector3.zero;

        [Header("Repel VFX (one-shot)")]
        [SerializeField] GameObject repelVfxPrefab;
        [SerializeField] Vector3 repelLocalOffset = Vector3.zero;
        [SerializeField, Tooltip("Detach the repel instance from the player so it stays where it was fired.")]
        bool repelInWorldSpace = true;
        [SerializeField, Tooltip("Auto-destroy the spawned repel VFX after this many seconds. 0 to keep it alive.")]
        float repelLifetime = 2.5f;
        [SerializeField, Tooltip("Only fire repel VFX when the player actually had a payload to release.")]
        bool repelOnlyWhenLoaded = false;

        GameObject attractInstance;
        ParticleSystem[] attractParticles;

        void Reset()
        {
            magnetism = GetComponent<MagnetismController>();
        }

        void Awake()
        {
            if (magnetism == null)
                magnetism = GetComponent<MagnetismController>();

            if (attachPoint == null)
                attachPoint = transform;

            EnsureAttractInstance();
        }

        void OnEnable()
        {
            if (magnetism == null)
                return;

            magnetism.OnPullStarted.AddListener(HandlePullStarted);
            magnetism.OnPullStopped.AddListener(HandlePullStopped);
            magnetism.OnRepelFired.AddListener(HandleRepelFired);
        }

        void OnDisable()
        {
            if (magnetism != null)
            {
                magnetism.OnPullStarted.RemoveListener(HandlePullStarted);
                magnetism.OnPullStopped.RemoveListener(HandlePullStopped);
                magnetism.OnRepelFired.RemoveListener(HandleRepelFired);
            }

            StopAttract();
        }

        void EnsureAttractInstance()
        {
            if (attractVfxPrefab == null || attractInstance != null)
                return;

            attractInstance = Instantiate(attractVfxPrefab, attachPoint);
            attractInstance.transform.localPosition = attractLocalOffset;
            attractInstance.transform.localRotation = Quaternion.identity;
            attractInstance.name = attractVfxPrefab.name + " (Runtime)";
            attractParticles = attractInstance.GetComponentsInChildren<ParticleSystem>(true);
            attractInstance.SetActive(false);
        }

        void HandlePullStarted()
        {
            if (attractInstance == null)
                EnsureAttractInstance();

            if (attractInstance == null)
                return;

            attractInstance.SetActive(true);
            if (attractParticles != null)
            {
                for (int i = 0; i < attractParticles.Length; i++)
                {
                    ParticleSystem ps = attractParticles[i];
                    if (ps == null)
                        continue;
                    ps.Clear(true);
                    ps.Play(true);
                }
            }
        }

        void HandlePullStopped()
        {
            StopAttract();
        }

        void StopAttract()
        {
            if (attractInstance == null)
                return;

            if (attractParticles != null)
            {
                for (int i = 0; i < attractParticles.Length; i++)
                {
                    ParticleSystem ps = attractParticles[i];
                    if (ps == null)
                        continue;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            attractInstance.SetActive(false);
        }

        void HandleRepelFired(bool emptyRelease)
        {
            if (repelVfxPrefab == null)
                return;

            if (repelOnlyWhenLoaded && emptyRelease)
                return;

            Vector3 worldPos = attachPoint.TransformPoint(repelLocalOffset);
            Quaternion worldRot = attachPoint.rotation;

            GameObject instance;
            if (repelInWorldSpace)
            {
                instance = Instantiate(repelVfxPrefab, worldPos, worldRot);
            }
            else
            {
                instance = Instantiate(repelVfxPrefab, attachPoint);
                instance.transform.localPosition = repelLocalOffset;
                instance.transform.localRotation = Quaternion.identity;
            }

            instance.name = repelVfxPrefab.name + " (Runtime)";

            if (repelLifetime > 0f)
                Destroy(instance, repelLifetime);
        }
    }
}
