using System;
using UnityEngine;

namespace MagnetPanic.Combat
{
    [Serializable]
    public sealed class HealthChangeStyle
    {
        public bool enabled = true;
        public Color color = Color.white;
        public string prefix = "";
        public string suffix = "";

        [Header("Sizing")]
        public float scaleMultiplier = 1f;
        public float fontSizeMultiplier = 1f;
        public float lifetimeMultiplier = 1f;

        [Header("Motion")]
        public Vector3 drift = new Vector3(0f, 1f, 0f);

        [Header("Filtering")]
        public int minimumAmount = 1;

        [Header("Critical Overlay")]
        public bool criticalEnabled = false;
        public int criticalThreshold = 4;
        public Color criticalColor = new Color(1f, 0.78f, 0.18f, 1f);
        public string criticalPrefix = "";
        public string criticalSuffix = "!";
        public float criticalScaleMultiplier = 1.35f;
        public float criticalFontMultiplier = 1.15f;

        public bool IsCritical(int amount)
        {
            return criticalEnabled && amount >= criticalThreshold;
        }
    }

    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] CombatHealth health;
        [SerializeField] Camera cameraOverride;

        [Header("Placement")]
        [SerializeField] Vector3 worldOffset = new Vector3(0f, 2.35f, 0f);
        [SerializeField] float horizontalJitter = 0.35f;
        [SerializeField] float verticalJitter = 0.12f;
        [SerializeField] float depthJitter = 0.12f;

        [Header("Damage")]
        [SerializeField] HealthChangeStyle damageStyle = new HealthChangeStyle
        {
            enabled = true,
            color = new Color(1f, 0.32f, 0.32f, 1f),
            prefix = "-",
            criticalEnabled = true,
        };

        [Header("Heal")]
        [SerializeField] HealthChangeStyle healStyle = new HealthChangeStyle
        {
            enabled = false,
            color = new Color(0.36f, 0.9f, 0.42f, 1f),
            prefix = "+",
            scaleMultiplier = 0.85f,
            fontSizeMultiplier = 0.9f,
        };

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
            if (health == null)
                return;

            health.OnDamageApplied.AddListener(HandleDamage);
            health.OnHealApplied.AddListener(HandleHeal);
        }

        void OnDisable()
        {
            if (health == null)
                return;

            health.OnDamageApplied.RemoveListener(HandleDamage);
            health.OnHealApplied.RemoveListener(HandleHeal);
        }

        public void Configure(CombatHealth target)
        {
            if (health != null)
            {
                health.OnDamageApplied.RemoveListener(HandleDamage);
                health.OnHealApplied.RemoveListener(HandleHeal);
            }

            health = target;

            if (isActiveAndEnabled && health != null)
            {
                health.OnDamageApplied.AddListener(HandleDamage);
                health.OnHealApplied.AddListener(HandleHeal);
            }
        }

        void HandleDamage(CombatHealth source, int amount)
        {
            Spawn(damageStyle, amount);
        }

        void HandleHeal(CombatHealth source, int amount)
        {
            Spawn(healStyle, amount);
        }

        void Spawn(HealthChangeStyle style, int amount)
        {
            if (style == null || !style.enabled)
                return;
            if (amount < style.minimumAmount)
                return;

            DamagePopupPool pool = DamagePopupPool.EnsureInstance();
            DamagePopup popup = pool.Get();
            if (popup == null)
                return;

            bool isCritical = style.IsCritical(amount);

            string prefix = isCritical ? style.criticalPrefix : style.prefix;
            string suffix = isCritical ? style.criticalSuffix : style.suffix;
            string text = string.Concat(prefix, amount.ToString(), suffix);

            Color color = isCritical ? style.criticalColor : style.color;

            PopupOptions options = new PopupOptions
            {
                scaleMultiplier = style.scaleMultiplier * (isCritical ? style.criticalScaleMultiplier : 1f),
                fontSizeMultiplier = style.fontSizeMultiplier * (isCritical ? style.criticalFontMultiplier : 1f),
                lifetimeMultiplier = style.lifetimeMultiplier,
                drift = style.drift,
            };

            Vector3 jitter = new Vector3(
                UnityEngine.Random.Range(-horizontalJitter, horizontalJitter),
                UnityEngine.Random.Range(-verticalJitter, verticalJitter),
                UnityEngine.Random.Range(-depthJitter, depthJitter));

            Vector3 spawn = transform.position + worldOffset + jitter;
            Camera cam = cameraOverride != null ? cameraOverride : Camera.main;

            popup.Show(spawn, text, color, cam, options);
        }
    }
}
