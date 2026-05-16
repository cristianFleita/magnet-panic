using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [SerializeField] CombatHealth health;
        [SerializeField] string title = "HP";

        UIDocument document;
        VisualElement fill;
        Label valueLabel;

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (health == null)
                health = FindFirstObjectByType<ArkhamCombatController>()?.GetComponent<CombatHealth>();
        }

        void OnEnable()
        {
            BindDocumentElements();

            if (health != null)
                health.OnHealthChanged.AddListener(Refresh);

            Refresh(health);
        }

        void OnDisable()
        {
            if (health != null)
                health.OnHealthChanged.RemoveListener(Refresh);
        }

        public void Configure(CombatHealth target)
        {
            if (health != null)
                health.OnHealthChanged.RemoveListener(Refresh);

            health = target;

            if (isActiveAndEnabled && health != null)
                health.OnHealthChanged.AddListener(Refresh);

            Refresh(health);
        }

        void BindDocumentElements()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            fill = root.Q<VisualElement>("hp-fill");
            valueLabel = root.Q<Label>("hp-value");
        }

        void Refresh(CombatHealth target)
        {
            if (fill == null || valueLabel == null)
                return;

            int current = target != null ? target.CurrentHealth : 0;
            int max = target != null ? target.MaxHealth : 1;
            float normalized = target != null ? target.Normalized : 0f;

            fill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
            valueLabel.text = current + "/" + max;
        }
    }
}
