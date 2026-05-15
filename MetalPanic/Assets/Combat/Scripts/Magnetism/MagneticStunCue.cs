using UnityEngine;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(ArkhamEnemy))]
    public sealed class MagneticStunCue : MonoBehaviour
    {
        [SerializeField] ArkhamEnemy enemy;

        [Header("Cue")]
        [Tooltip("VFX prefab spawned over this enemy's head while the magnetic anchor is held. Mirrors the counter-stun stars used by ArkhamEnemy.")]
        [SerializeField] GameObject cuePrefab;
        [SerializeField] float cueHeight = 2.45f;
        [Tooltip("Seconds the VFX stays alive after spawning. Matches the anchor stun length. Use 0 or less to keep it alive until the anchor is released.")]
        [SerializeField] float cueLifetime = 1f;

        GameObject cueInstance;

        void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<ArkhamEnemy>();
        }

        void OnEnable()
        {
            if (enemy == null)
                return;

            enemy.OnAnchored.AddListener(HandleAnchored);
            enemy.OnAnchorReleased.AddListener(HandleAnchorReleased);
        }

        void OnDisable()
        {
            if (enemy != null)
            {
                enemy.OnAnchored.RemoveListener(HandleAnchored);
                enemy.OnAnchorReleased.RemoveListener(HandleAnchorReleased);
            }

            DespawnCue();
        }

        void HandleAnchored(ArkhamEnemy _)
        {
            SpawnCue();
        }

        void HandleAnchorReleased(ArkhamEnemy _)
        {
            DespawnCue();
        }

        void SpawnCue()
        {
            if (cuePrefab == null || cueInstance != null)
                return;

            Vector3 worldPos = transform.position + Vector3.up * cueHeight;
            cueInstance = Instantiate(cuePrefab, worldPos, Quaternion.identity, transform);
            cueInstance.name = cuePrefab.name + " (AnchorCue)";
            cueInstance.SetActive(true);

            if (cueLifetime > 0f)
                Destroy(cueInstance, cueLifetime);
        }

        void DespawnCue()
        {
            if (cueInstance == null)
                return;

            Destroy(cueInstance);
            cueInstance = null;
        }
    }
}
