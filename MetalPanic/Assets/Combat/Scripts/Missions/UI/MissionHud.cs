using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat.Missions.UI
{
    /// <summary>
    /// Compact mission card with name, objective, progress text and a
    /// horizontal time-remaining track. Slides in on mission start, flashes
    /// green on complete, dims red on expire. Built in code so it has no
    /// asset dependencies — drop on the same UIDocument as the rest of the HUD
    /// or a dedicated one.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionHud : MonoBehaviour
    {
        [SerializeField] MissionSystem missions;

        [Header("Style")]
        [SerializeField] Color background = new Color(0.04f, 0.05f, 0.07f, 0.85f);
        [SerializeField] Color accentActive = new Color(1f, 0.78f, 0.22f, 1f);
        [SerializeField] Color accentComplete = new Color(0.27f, 0.93f, 0.45f, 1f);
        [SerializeField] Color accentExpired = new Color(0.92f, 0.32f, 0.32f, 1f);

        UIDocument document;
        VisualElement card;
        Label nameLabel;
        Label objectiveLabel;
        Label progressLabel;
        VisualElement timerFill;
        Label bannerLabel;
        float bannerTimer;
        const float BannerDuration = 0.6f;

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (missions == null)
                missions = MissionSystem.Instance != null
                    ? MissionSystem.Instance
                    : FindFirstObjectByType<MissionSystem>();
        }

        void OnEnable()
        {
            Build();
            Bind();
            HideCard();
        }

        void OnDisable()
        {
            Unbind();
        }

        void Update()
        {
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.unscaledDeltaTime;
                if (bannerTimer <= 0f && bannerLabel != null)
                    bannerLabel.style.display = DisplayStyle.None;
            }
        }

        public void Bind(MissionSystem system)
        {
            Unbind();
            missions = system;
            Bind();
        }

        void Bind()
        {
            if (missions == null)
                return;
            missions.OnMissionStarted.AddListener(HandleStarted);
            missions.OnMissionProgress.AddListener(HandleProgress);
            missions.OnMissionCompleted.AddListener(HandleCompleted);
            missions.OnMissionExpired.AddListener(HandleExpired);
            missions.OnCooldownStarted.AddListener(HandleCooldown);
        }

        void Unbind()
        {
            if (missions == null)
                return;
            missions.OnMissionStarted.RemoveListener(HandleStarted);
            missions.OnMissionProgress.RemoveListener(HandleProgress);
            missions.OnMissionCompleted.RemoveListener(HandleCompleted);
            missions.OnMissionExpired.RemoveListener(HandleExpired);
            missions.OnCooldownStarted.RemoveListener(HandleCooldown);
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

            card = new VisualElement { name = "mission-card" };
            card.style.position = Position.Absolute;
            card.style.right = Length.Percent(3);
            card.style.top = 110;
            card.style.width = 240;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.backgroundColor = background;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = accentActive;
            root.Add(card);

            VisualElement headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            card.Add(headerRow);

            nameLabel = new Label("MISSION");
            nameLabel.style.color = new Color(0.95f, 0.95f, 0.98f, 1f);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.fontSize = 14;
            headerRow.Add(nameLabel);

            progressLabel = new Label("0 / 0");
            progressLabel.style.color = accentActive;
            progressLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            progressLabel.style.fontSize = 14;
            headerRow.Add(progressLabel);

            objectiveLabel = new Label("");
            objectiveLabel.style.color = new Color(0.82f, 0.86f, 0.92f, 1f);
            objectiveLabel.style.fontSize = 11;
            objectiveLabel.style.marginTop = 2;
            objectiveLabel.style.marginBottom = 6;
            objectiveLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(objectiveLabel);

            VisualElement timerTrack = new VisualElement { name = "timer-track" };
            timerTrack.style.height = 4;
            timerTrack.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            card.Add(timerTrack);

            timerFill = new VisualElement { name = "timer-fill" };
            timerFill.style.position = Position.Absolute;
            timerFill.style.left = 0;
            timerFill.style.top = 0;
            timerFill.style.bottom = 0;
            timerFill.style.width = Length.Percent(100);
            timerFill.style.backgroundColor = accentActive;
            timerTrack.Add(timerFill);

            bannerLabel = new Label("MISSION COMPLETE");
            bannerLabel.style.position = Position.Absolute;
            bannerLabel.style.right = Length.Percent(3);
            bannerLabel.style.top = 90;
            bannerLabel.style.minWidth = 240;
            bannerLabel.style.fontSize = 18;
            bannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            bannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            bannerLabel.style.color = accentComplete;
            bannerLabel.style.display = DisplayStyle.None;
            root.Add(bannerLabel);
        }

        void ShowCard(MissionDefinition def, Color accent)
        {
            if (card == null)
                return;
            card.style.display = DisplayStyle.Flex;
            card.style.borderLeftColor = accent;
            progressLabel.style.color = accent;
            timerFill.style.backgroundColor = accent;
            nameLabel.text = def != null ? def.displayName.ToUpper() : "MISSION";
            objectiveLabel.text = def != null ? def.objective : "";
        }

        void HideCard()
        {
            if (card != null)
                card.style.display = DisplayStyle.None;
            if (bannerLabel != null)
                bannerLabel.style.display = DisplayStyle.None;
        }

        void HandleStarted(MissionDefinition def)
        {
            ShowCard(def, accentActive);
            HandleProgress(missions != null ? missions.Current : null);
        }

        void HandleProgress(MissionRuntimeState state)
        {
            if (state == null || card == null)
                return;
            progressLabel.text = state.Progress + " / " + state.Target;
            float pct = state.TimeRemaining01 * 100f;
            timerFill.style.width = Length.Percent(pct);
        }

        void HandleCompleted(MissionDefinition def)
        {
            ShowBanner("MISSION COMPLETE", accentComplete);
            FlashCard(accentComplete);
        }

        void HandleExpired(MissionDefinition def)
        {
            ShowBanner("MISSION EXPIRED", accentExpired);
            FlashCard(accentExpired);
        }

        void HandleCooldown(float _)
        {
            HideCard();
        }

        void ShowBanner(string text, Color color)
        {
            if (bannerLabel == null)
                return;
            bannerLabel.text = text;
            bannerLabel.style.color = color;
            bannerLabel.style.display = DisplayStyle.Flex;
            bannerTimer = BannerDuration;
        }

        void FlashCard(Color color)
        {
            if (card == null)
                return;
            card.style.borderLeftColor = color;
            progressLabel.style.color = color;
            timerFill.style.backgroundColor = color;
            // Card will be hidden once the next cooldown ends and a new
            // mission starts (HandleStarted). Until then we leave the
            // completed/expired summary on screen as feedback.
        }
    }
}
