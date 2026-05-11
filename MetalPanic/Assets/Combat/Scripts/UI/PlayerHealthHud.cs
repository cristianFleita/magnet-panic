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
            Build();

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

        void Build()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            root.Clear();
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;

            VisualElement panel = new VisualElement();
            panel.name = "player-health-panel";
            panel.style.position = Position.Absolute;
            panel.style.left = Length.Percent(3);
            panel.style.top = Length.Percent(3);
            panel.style.width = Length.Percent(28);
            panel.style.minWidth = 180;
            panel.style.maxWidth = 360;
            panel.style.height = 42;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 7;
            panel.style.paddingBottom = 7;
            panel.style.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 0.72f);
            root.Add(panel);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            panel.Add(row);

            Label titleLabel = new Label(title);
            titleLabel.style.width = 34;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.88f, 0.96f, 1f, 1f);
            row.Add(titleLabel);

            VisualElement track = new VisualElement();
            track.style.flexGrow = 1;
            track.style.height = 16;
            track.style.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            row.Add(track);

            fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = 0;
            fill.style.top = 0;
            fill.style.bottom = 0;
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = new Color(0.16f, 0.92f, 0.48f, 1f);
            track.Add(fill);

            valueLabel = new Label();
            valueLabel.style.width = 54;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            valueLabel.style.color = new Color(0.88f, 0.96f, 1f, 1f);
            row.Add(valueLabel);
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
