using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat.Scoring.UI
{
    /// <summary>
    /// UI Toolkit HUD for the Scoring &amp; XP system. Binds the live HUD UXML
    /// document and updates the XP bar, level, combo counter and score labels.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class XpHud : MonoBehaviour
    {
        [SerializeField] ScoringRuntime scoring;
        [SerializeField, Tooltip("Hide the combo counter when ComboCount <= 1.")] bool hideComboAtRest = true;

        [Header("Style")]
        [SerializeField] Color comboColorLow = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] Color comboColorHigh = new Color(1f, 0.55f, 0.18f, 1f);
        [SerializeField, Min(2)] int comboColorRamp = 10;

        UIDocument document;
        VisualElement root;
        VisualElement xpFill;
        Label levelLabel;
        Label xpValueLabel;
        VisualElement comboPanel;
        Label comboLabel;
        VisualElement comboRingFill;
        Label scoreLabel;
        Label levelUpFlash;

        float flashTimer;
        const float FlashDuration = 1.1f;

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();
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

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.unscaledDeltaTime;
                if (levelUpFlash != null)
                {
                    float a = Mathf.Clamp01(flashTimer / FlashDuration);
                    Color c = levelUpFlash.style.color.value;
                    levelUpFlash.style.color = new Color(c.r, c.g, c.b, a);
                    if (flashTimer <= 0f)
                        levelUpFlash.style.display = DisplayStyle.None;
                }
            }
        }

        public void Bind(ScoringRuntime runtime)
        {
            Unbind();
            scoring = runtime;
            Bind();
            RefreshFromState();
        }

        void Bind()
        {
            if (scoring == null)
                return;
            scoring.OnXpChanged.AddListener(HandleXpChanged);
            scoring.OnLevelUp.AddListener(HandleLevelUp);
            scoring.OnComboChanged.AddListener(HandleComboChanged);
            scoring.OnComboReset.AddListener(HandleComboReset);
            scoring.OnScoreChanged.AddListener(HandleScoreChanged);
        }

        void Unbind()
        {
            if (scoring == null)
                return;
            scoring.OnXpChanged.RemoveListener(HandleXpChanged);
            scoring.OnLevelUp.RemoveListener(HandleLevelUp);
            scoring.OnComboChanged.RemoveListener(HandleComboChanged);
            scoring.OnComboReset.RemoveListener(HandleComboReset);
            scoring.OnScoreChanged.RemoveListener(HandleScoreChanged);
        }

        void Build()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            xpFill = root.Q<VisualElement>("xp-fill");
            levelLabel = root.Q<Label>("level-value");
            xpValueLabel = root.Q<Label>("xp-value");
            comboPanel = root.Q<VisualElement>("combo-panel");
            comboLabel = root.Q<Label>("combo-value");
            comboRingFill = root.Q<VisualElement>("combo-ring-fill");
            scoreLabel = root.Q<Label>("score-value");
            levelUpFlash = root.Q<Label>("level-up-flash");
        }

        void RefreshFromState()
        {
            if (scoring == null)
                return;
            HandleXpChanged(scoring.CurrentXpInLevel, scoring.XpToNextLevel, scoring.CurrentLevel);
            HandleComboChanged(scoring.ComboCount, scoring.ComboTimeRemaining);
            HandleScoreChanged(scoring.Score);
        }

        void HandleXpChanged(int currentInLevel, int toNext, int level)
        {
            if (xpFill == null)
                return;
            float pct = toNext > 0 ? Mathf.Clamp01(currentInLevel / (float)toNext) * 100f : 0f;
            xpFill.style.width = Length.Percent(pct);

            if (xpValueLabel != null)
                xpValueLabel.text = currentInLevel + "/" + toNext;
            if (levelLabel != null)
                levelLabel.text = "LV." + level;
        }

        void HandleLevelUp(int level)
        {
            if (levelUpFlash == null)
                return;
            levelUpFlash.text = "LEVEL UP!  Lv." + level;
            levelUpFlash.style.display = DisplayStyle.Flex;
            flashTimer = FlashDuration;
        }

        void HandleComboChanged(int count, float timeRemaining)
        {
            if (comboPanel == null)
                return;

            bool visible = count > (hideComboAtRest ? 1 : 0);
            comboPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (!visible)
                return;

            if (comboLabel != null)
            {
                comboLabel.text = "x" + count;

                float t = Mathf.Clamp01((count - 1) / (float)Mathf.Max(1, comboColorRamp - 1));
                comboLabel.style.color = Color.Lerp(comboColorLow, comboColorHigh, t);
            }

            float window = scoring != null && scoring.Config != null ? scoring.Config.comboWindowSeconds : 1f;
            float pct = window > 0f ? Mathf.Clamp01(timeRemaining / window) * 100f : 0f;
            if (comboRingFill != null)
                comboRingFill.style.width = Length.Percent(pct);
        }

        void HandleComboReset()
        {
            if (comboPanel != null)
                comboPanel.style.display = DisplayStyle.None;
        }

        void HandleScoreChanged(long value)
        {
            if (scoreLabel == null)
                return;
            scoreLabel.text = value.ToString("N0");
        }
    }
}
