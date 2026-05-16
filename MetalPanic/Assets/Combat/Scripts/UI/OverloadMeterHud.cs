using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Drives the bottom-center "OVERLOAD" meter — fills with current magnetic
    /// charge, swaps tint at the critical threshold, flashes a grace-period
    /// countdown when the player is about to overload, and shows a recovery
    /// label until pull/repel are re-enabled. Binds the shared HUD UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OverloadMeterHud : MonoBehaviour
    {
        [SerializeField] MagnetismController magnetism;
        [SerializeField] OverloadController overload;
        [SerializeField, Tooltip("Hide the panel entirely while charge is zero and we're not in overload.")]
        bool hideWhenEmpty = true;

        const string WarningClass = "overload-warning";
        const string CriticalClass = "overload-critical";

        UIDocument document;
        VisualElement anchor;
        VisualElement fill;
        Label valueLabel;
        Label stateLabel;

        float currentCharge;
        float maxCapacity = 1f;
        OverloadState state = OverloadState.Normal;
        float graceTimer;

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (magnetism == null)
                magnetism = FindFirstObjectByType<MagnetismController>();
            if (overload == null)
                overload = FindFirstObjectByType<OverloadController>();
        }

        void OnEnable()
        {
            Build();
            Bind();
            RefreshFromState();
        }

        void OnDisable()
        {
            Unbind();
        }

        public void Bind(MagnetismController magnet, OverloadController overloadCtrl)
        {
            Unbind();
            magnetism = magnet;
            overload = overloadCtrl;
            Bind();
            RefreshFromState();
        }

        void Bind()
        {
            if (magnetism != null)
                magnetism.OnChargeChanged.AddListener(HandleChargeChanged);

            if (overload != null)
            {
                overload.OnStateChanged.AddListener(HandleStateChanged);
                overload.OnGraceTimerChanged.AddListener(HandleGraceChanged);
            }
        }

        void Unbind()
        {
            if (magnetism != null)
                magnetism.OnChargeChanged.RemoveListener(HandleChargeChanged);

            if (overload != null)
            {
                overload.OnStateChanged.RemoveListener(HandleStateChanged);
                overload.OnGraceTimerChanged.RemoveListener(HandleGraceChanged);
            }
        }

        void Build()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            anchor = root.Q<VisualElement>("overload-meter");
            fill = root.Q<VisualElement>("overload-fill");
            valueLabel = root.Q<Label>("overload-value");
            stateLabel = root.Q<Label>("overload-state-label");
        }

        void RefreshFromState()
        {
            if (magnetism != null)
                HandleChargeChanged(magnetism.CurrentCharge, magnetism.MaxCapacity);
            if (overload != null)
                HandleStateChanged(overload.State);
            ApplyVisibility();
        }

        void HandleChargeChanged(float charge, float capacity)
        {
            currentCharge = charge;
            maxCapacity = Mathf.Max(0.0001f, capacity);
            UpdateFill();
            ApplyVisibility();
        }

        void HandleStateChanged(OverloadState newState)
        {
            state = newState;
            UpdateStateLabel();
            UpdateColorClass();
            ApplyVisibility();
        }

        void HandleGraceChanged(float secondsRemaining)
        {
            graceTimer = Mathf.Max(0f, secondsRemaining);
            UpdateStateLabel();
        }

        void UpdateFill()
        {
            if (fill == null)
                return;
            float ratio = Mathf.Clamp01(currentCharge / maxCapacity);
            fill.style.width = Length.Percent(ratio * 100f);

            if (valueLabel != null)
                valueLabel.text = Mathf.RoundToInt(ratio * 100f) + "%";
        }

        void UpdateColorClass()
        {
            if (anchor == null)
                return;

            anchor.RemoveFromClassList(WarningClass);
            anchor.RemoveFromClassList(CriticalClass);

            switch (state)
            {
                case OverloadState.Critical:
                    anchor.AddToClassList(WarningClass);
                    break;
                case OverloadState.GracePeriod:
                case OverloadState.Overload:
                case OverloadState.Recovery:
                    anchor.AddToClassList(CriticalClass);
                    break;
            }
        }

        void UpdateStateLabel()
        {
            if (stateLabel == null)
                return;

            stateLabel.text = state switch
            {
                OverloadState.Normal => "STABLE",
                OverloadState.Critical => "CRITICAL",
                OverloadState.GracePeriod => "REPEL NOW " + graceTimer.ToString("0.0") + "s",
                OverloadState.Overload => "OVERLOAD!",
                OverloadState.Recovery => "RECOVERING",
                _ => "",
            };
        }

        void ApplyVisibility()
        {
            if (anchor == null)
                return;

            bool visible = !hideWhenEmpty
                || state != OverloadState.Normal
                || currentCharge > 0f;

            anchor.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
