using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat.Scoring.UI
{
    /// <summary>
    /// UI Toolkit HUD for the Scoring &amp; XP system. Renders the XP bar, level,
    /// combo counter (with a draining ring) and the running score. Build is
    /// done in code to keep the prototype free of UXML/USS dependencies — swap
    /// for assets later when the visual design lands.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class XpHud : MonoBehaviour
    {
        [SerializeField] ScoringRuntime scoring;
        [SerializeField, Tooltip("Hide the combo counter when ComboCount <= 1.")] bool hideComboAtRest = true;

        [Header("Style")]
        [SerializeField] Color barBackground = new Color(0.10f, 0.11f, 0.13f, 0.92f);
        [SerializeField] Color xpFillColor = new Color(0.96f, 0.78f, 0.18f, 1f);
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
            root.Clear();
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;

            BuildXpPanel(root);
            BuildComboPanel(root);
            BuildScorePanel(root);
            BuildLevelUpFlash(root);
        }

        void BuildXpPanel(VisualElement parent)
        {
            VisualElement panel = new VisualElement { name = "xp-panel" };
            panel.style.position = Position.Absolute;
            panel.style.left = Length.Percent(3);
            panel.style.top = 60;
            panel.style.width = Length.Percent(34);
            panel.style.minWidth = 220;
            panel.style.maxWidth = 420;
            panel.style.height = 30;
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignItems = Align.Center;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 4;
            panel.style.paddingBottom = 4;
            panel.style.backgroundColor = barBackground;
            parent.Add(panel);

            levelLabel = new Label("Lv.1");
            levelLabel.style.width = 48;
            levelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            levelLabel.style.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            panel.Add(levelLabel);

            VisualElement track = new VisualElement { name = "xp-track" };
            track.style.flexGrow = 1;
            track.style.height = 12;
            track.style.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 0.95f);
            panel.Add(track);

            xpFill = new VisualElement { name = "xp-fill" };
            xpFill.style.position = Position.Absolute;
            xpFill.style.left = 0;
            xpFill.style.top = 0;
            xpFill.style.bottom = 0;
            xpFill.style.width = Length.Percent(0);
            xpFill.style.backgroundColor = xpFillColor;
            track.Add(xpFill);

            xpValueLabel = new Label("0 / 50");
            xpValueLabel.style.width = 86;
            xpValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            xpValueLabel.style.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            xpValueLabel.style.fontSize = 12;
            panel.Add(xpValueLabel);
        }

        void BuildComboPanel(VisualElement parent)
        {
            comboPanel = new VisualElement { name = "combo-panel" };
            comboPanel.style.position = Position.Absolute;
            comboPanel.style.right = Length.Percent(3);
            comboPanel.style.top = Length.Percent(18);
            comboPanel.style.width = 110;
            comboPanel.style.height = 60;
            comboPanel.style.alignItems = Align.Center;
            comboPanel.style.justifyContent = Justify.Center;
            comboPanel.style.display = DisplayStyle.None;
            parent.Add(comboPanel);

            VisualElement ringTrack = new VisualElement { name = "combo-ring" };
            ringTrack.style.position = Position.Absolute;
            ringTrack.style.left = 0;
            ringTrack.style.right = 0;
            ringTrack.style.bottom = 0;
            ringTrack.style.height = 4;
            ringTrack.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.6f);
            comboPanel.Add(ringTrack);

            comboRingFill = new VisualElement { name = "combo-ring-fill" };
            comboRingFill.style.position = Position.Absolute;
            comboRingFill.style.left = 0;
            comboRingFill.style.top = 0;
            comboRingFill.style.bottom = 0;
            comboRingFill.style.width = Length.Percent(100);
            comboRingFill.style.backgroundColor = new Color(1f, 0.78f, 0.22f, 1f);
            ringTrack.Add(comboRingFill);

            comboLabel = new Label("x1");
            comboLabel.style.fontSize = 36;
            comboLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            comboLabel.style.color = comboColorLow;
            comboPanel.Add(comboLabel);
        }

        void BuildScorePanel(VisualElement parent)
        {
            VisualElement panel = new VisualElement { name = "score-panel" };
            panel.style.position = Position.Absolute;
            panel.style.right = Length.Percent(3);
            panel.style.top = Length.Percent(3);
            panel.style.minWidth = 120;
            panel.style.height = 34;
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.FlexEnd;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.backgroundColor = barBackground;
            parent.Add(panel);

            Label title = new Label("SCORE");
            title.style.color = new Color(0.78f, 0.82f, 0.88f, 1f);
            title.style.fontSize = 11;
            title.style.marginRight = 8;
            panel.Add(title);

            scoreLabel = new Label("0");
            scoreLabel.style.color = new Color(1f, 0.94f, 0.78f, 1f);
            scoreLabel.style.fontSize = 20;
            scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(scoreLabel);
        }

        void BuildLevelUpFlash(VisualElement parent)
        {
            levelUpFlash = new Label("LEVEL UP!");
            levelUpFlash.style.position = Position.Absolute;
            levelUpFlash.style.left = 0;
            levelUpFlash.style.right = 0;
            levelUpFlash.style.top = Length.Percent(28);
            levelUpFlash.style.unityTextAlign = TextAnchor.MiddleCenter;
            levelUpFlash.style.fontSize = 64;
            levelUpFlash.style.unityFontStyleAndWeight = FontStyle.Bold;
            levelUpFlash.style.color = new Color(1f, 0.86f, 0.28f, 0f);
            levelUpFlash.style.display = DisplayStyle.None;
            parent.Add(levelUpFlash);
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
            xpValueLabel.text = currentInLevel + " / " + toNext;
            levelLabel.text = "Lv." + level;
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

            comboLabel.text = "x" + count;

            float t = Mathf.Clamp01((count - 1) / (float)Mathf.Max(1, comboColorRamp - 1));
            comboLabel.style.color = Color.Lerp(comboColorLow, comboColorHigh, t);

            float window = scoring != null && scoring.Config != null ? scoring.Config.comboWindowSeconds : 1f;
            float pct = window > 0f ? Mathf.Clamp01(timeRemaining / window) * 100f : 0f;
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
