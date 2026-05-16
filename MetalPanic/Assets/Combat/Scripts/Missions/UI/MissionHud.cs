using MagnetPanic.Combat.Powerups;
using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat.Missions.UI
{
    /// <summary>
    /// Compact mission card with name, objective, progress text and a
    /// horizontal time-remaining track. Binds the live HUD UXML document so it
    /// can share one UIDocument with the rest of the run HUD.
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
        Label rewardLabel;
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
            root.pickingMode = PickingMode.Ignore;

            card = root.Q<VisualElement>("mission-card");
            nameLabel = root.Q<Label>("mission-title");
            objectiveLabel = root.Q<Label>("mission-description");
            progressLabel = root.Q<Label>("mission-progress");
            rewardLabel = root.Q<Label>("mission-reward");
            timerFill = root.Q<VisualElement>("mission-progress-fill");
            bannerLabel = root.Q<Label>("mission-banner-label");

            if (card != null)
            {
                card.style.backgroundColor = background;
                card.style.borderLeftColor = accentActive;
            }

            if (progressLabel != null)
                progressLabel.style.color = accentActive;
            if (timerFill != null)
                timerFill.style.backgroundColor = accentActive;
        }

        void ShowCard(MissionDefinition def, Color accent)
        {
            if (card == null)
                return;
            card.style.display = DisplayStyle.Flex;
            card.style.borderLeftColor = accent;
            if (progressLabel != null)
                progressLabel.style.color = accent;
            if (timerFill != null)
                timerFill.style.backgroundColor = accent;
            if (nameLabel != null)
                nameLabel.text = def != null ? def.displayName.ToUpper() : "MISSION";
            if (objectiveLabel != null)
                objectiveLabel.text = def != null ? def.objective : "";
        }

        void HideCard()
        {
            if (card != null)
                card.style.display = DisplayStyle.None;
            if (bannerLabel != null)
                bannerLabel.style.display = DisplayStyle.None;
            if (rewardLabel != null)
                rewardLabel.style.display = DisplayStyle.None;
        }

        void HandleStarted(MissionDefinition def)
        {
            ShowCard(def, accentActive);
            UpdateReward(missions != null ? missions.Current : null, def);
            HandleProgress(missions != null ? missions.Current : null);
        }

        void UpdateReward(MissionRuntimeState state, MissionDefinition def)
        {
            if (rewardLabel == null)
                return;

            if (def == null || !def.grantsPowerup || state == null || state.RewardPowerup == PowerupId.None)
            {
                rewardLabel.style.display = DisplayStyle.None;
                return;
            }

            rewardLabel.text = "REWARD: " + FormatPowerupName(state.RewardPowerup);
            rewardLabel.style.display = DisplayStyle.Flex;
        }

        static string FormatPowerupName(PowerupId id) => id switch
        {
            PowerupId.SlowTime => "Slow Time",
            PowerupId.OverloadPulse => "Overload Pulse",
            PowerupId.MagneticMine => "Magnetic Mine",
            _ => id.ToString(),
        };

        void HandleProgress(MissionRuntimeState state)
        {
            if (state == null || card == null)
                return;

            if (progressLabel != null)
                progressLabel.text = state.Progress + " / " + state.Target;

            float pct = state.TimeRemaining01 * 100f;
            if (timerFill != null)
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
            if (progressLabel != null)
                progressLabel.style.color = color;
            if (timerFill != null)
                timerFill.style.backgroundColor = color;
            // Card will be hidden once the next cooldown ends and a new
            // mission starts (HandleStarted). Until then we leave the
            // completed/expired summary on screen as feedback.
        }
    }
}
