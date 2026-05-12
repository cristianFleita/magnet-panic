using UnityEngine;

namespace MagnetPanic.Combat
{
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] CombatHealth health;
        [SerializeField] Camera cameraOverride;

        [Header("Placement")]
        [SerializeField] Vector3 worldOffset = new Vector3(0f, 2.35f, 0f);
        [SerializeField] float horizontalJitter = 0.35f;
        [SerializeField] float verticalJitter = 0.12f;

        [Header("Style")]
        [SerializeField] Color color = Color.white;
        [SerializeField] bool enableCritical = true;
        [SerializeField] int criticalThreshold = 4;
        [SerializeField] Color criticalColor = new Color(1f, 0.78f, 0.18f, 1f);

        [Header("Filters")]
        [SerializeField] int minimumAmount = 1;

        void Reset()
        {
            health = GetComponentInParent<CombatHealth>();
        }

        void Awake()
        {
            if (health == null)
                health = GetComponentInParent<CombatHealth>();
            if (cameraOverride == null)
                cameraOverride = Camera.main;
        }

        void OnEnable()
        {
            if (health != null)
                health.OnDamageApplied.AddListener(HandleDamage);
        }

        void OnDisable()
        {
            if (health != null)
                health.OnDamageApplied.RemoveListener(HandleDamage);
        }

        public void Configure(CombatHealth target)
        {
            if (health != null)
                health.OnDamageApplied.RemoveListener(HandleDamage);

            health = target;

            if (isActiveAndEnabled && health != null)
                health.OnDamageApplied.AddListener(HandleDamage);
        }

        void HandleDamage(CombatHealth source, int amount)
        {
            if (amount < minimumAmount)
                return;

            DamagePopupPool pool = DamagePopupPool.EnsureInstance();
            DamagePopup popup = pool.Get();
            if (popup == null)
                return;

            Vector3 jitter = new Vector3(
                Random.Range(-horizontalJitter, horizontalJitter),
                Random.Range(-verticalJitter, verticalJitter),
                Random.Range(-horizontalJitter, horizontalJitter) * 0.35f);

            Vector3 spawn = transform.position + worldOffset + jitter;
            Color tint = (enableCritical && amount >= criticalThreshold) ? criticalColor : color;
            Camera cam = cameraOverride != null ? cameraOverride : Camera.main;

            popup.Show(spawn, -amount, tint, cam);
        }
    }
}
