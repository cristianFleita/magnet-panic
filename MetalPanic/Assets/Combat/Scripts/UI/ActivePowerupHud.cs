using MagnetPanic.Combat.Powerups;
using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Drives the "ability slot" in the top HUD row to mirror the active
    /// powerup: icon letter, display name and countdown timer. Hides the slot
    /// when no powerup is running. Binds the shared HUD UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ActivePowerupHud : MonoBehaviour
    {
        [SerializeField] PowerupController controller;

        [Header("Style")]
        [SerializeField] Color slowTimeColor = new Color(0.45f, 0.78f, 1f, 1f);
        [SerializeField] Color overloadPulseColor = new Color(1f, 0.36f, 0.20f, 1f);
        [SerializeField] Color magneticMineColor = new Color(1f, 0.72f, 0.18f, 1f);
        [SerializeField] Color idleColor = new Color(0.40f, 0.46f, 0.55f, 1f);
        [SerializeField, Tooltip("Hide the slot entirely while no powerup is active.")]
        bool hideWhenIdle = true;

        UIDocument document;
        VisualElement slot;
        VisualElement iconBg;
        Label iconText;
        Label titleLabel;
        Label valueLabel;

        PowerupId currentId;
        float remaining;

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (controller == null)
                controller = FindFirstObjectByType<PowerupController>();
        }

        void OnEnable()
        {
            Build();
            Bind();
            RefreshIdle();
        }

        void OnDisable()
        {
            Unbind();
        }

        public void Bind(PowerupController target)
        {
            Unbind();
            controller = target;
            Bind();
            RefreshIdle();
        }

        void Bind()
        {
            if (controller == null)
                return;
            controller.OnPowerupActivated.AddListener(HandleActivated);
            controller.OnPowerupDeactivated.AddListener(HandleDeactivated);
            controller.OnPowerupTimerChanged.AddListener(HandleTimerChanged);
        }

        void Unbind()
        {
            if (controller == null)
                return;
            controller.OnPowerupActivated.RemoveListener(HandleActivated);
            controller.OnPowerupDeactivated.RemoveListener(HandleDeactivated);
            controller.OnPowerupTimerChanged.RemoveListener(HandleTimerChanged);
        }

        void Build()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            slot = root.Q<VisualElement>("ability-slot");
            iconBg = root.Q<VisualElement>("ability-icon");
            iconText = root.Q<Label>("ability-icon-text");
            titleLabel = root.Q<Label>("ability-label");
            valueLabel = root.Q<Label>("ability-value");
        }

        void HandleActivated(PowerupId id)
        {
            currentId = id;
            remaining = ResolveDuration(id);
            ApplyVisual(id);
            if (valueLabel != null)
                valueLabel.text = FormatRemaining(remaining);
            ShowSlot(true);
        }

        void HandleDeactivated(PowerupId _)
        {
            currentId = PowerupId.None;
            remaining = 0f;
            RefreshIdle();
        }

        void HandleTimerChanged(PowerupId id, float secondsRemaining)
        {
            if (id == PowerupId.None)
            {
                currentId = PowerupId.None;
                remaining = 0f;
                RefreshIdle();
                return;
            }

            currentId = id;
            remaining = Mathf.Max(0f, secondsRemaining);
            if (valueLabel != null)
                valueLabel.text = FormatRemaining(remaining);
        }

        void RefreshIdle()
        {
            if (currentId != PowerupId.None)
                return;

            if (titleLabel != null)
                titleLabel.text = "POWERUP";
            if (iconText != null)
                iconText.text = "—";
            if (valueLabel != null)
                valueLabel.text = "--";
            if (iconBg != null)
                iconBg.style.backgroundColor = idleColor;

            ShowSlot(!hideWhenIdle);
        }

        void ShowSlot(bool visible)
        {
            if (slot == null)
                return;
            slot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ApplyVisual(PowerupId id)
        {
            Color accent = ResolveColor(id);
            string letter = ResolveIconLetter(id);
            string title = ResolveDisplayName(id);

            if (iconBg != null)
                iconBg.style.backgroundColor = accent;
            if (iconText != null)
                iconText.text = letter;
            if (titleLabel != null)
                titleLabel.text = title;
        }

        float ResolveDuration(PowerupId id)
        {
            if (controller == null)
                return 0f;
            return id switch
            {
                PowerupId.SlowTime => controller.SlowTimeDuration,
                PowerupId.OverloadPulse => controller.PulseDuration,
                PowerupId.MagneticMine => controller.MineTimeout,
                _ => 0f,
            };
        }

        Color ResolveColor(PowerupId id) => id switch
        {
            PowerupId.SlowTime => slowTimeColor,
            PowerupId.OverloadPulse => overloadPulseColor,
            PowerupId.MagneticMine => magneticMineColor,
            _ => idleColor,
        };

        static string ResolveIconLetter(PowerupId id) => id switch
        {
            PowerupId.SlowTime => "S",
            PowerupId.OverloadPulse => "O",
            PowerupId.MagneticMine => "M",
            _ => "—",
        };

        static string ResolveDisplayName(PowerupId id) => id switch
        {
            PowerupId.SlowTime => "SLOW TIME",
            PowerupId.OverloadPulse => "OVERLOAD PULSE",
            PowerupId.MagneticMine => "MAGNETIC MINE",
            _ => "POWERUP",
        };

        static string FormatRemaining(float seconds)
        {
            if (seconds <= 0f)
                return "0.0s";
            return seconds.ToString("0.0") + "s";
        }
    }
}
