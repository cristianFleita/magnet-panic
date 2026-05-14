using System;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// Central authority for Scrap XP, combo, level, and run score. Designed as
    /// the single integration point for combat (push kills in), the upgrade
    /// system (subscribe to OnLevelUp / plug an <see cref="ILevelUpUpgradeBroker"/>),
    /// the mission system (push mission XP in), and the HUD (subscribe to events).
    ///
    /// Lives for the duration of a scene. Singleton-style access via
    /// <see cref="Instance"/> for callers that don't have an inspector reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoringRuntime : MonoBehaviour
    {
        public static ScoringRuntime Instance { get; private set; }

        [Header("Config")]
        [SerializeField] ScoringConfig config;

        [Header("Behaviour")]
        [Tooltip("Start the run timer in Start(). Disable if the run boots from a separate state machine.")]
        [SerializeField] bool autoStartRun = true;

        [Header("Upgrade broker (optional)")]
        [Tooltip("MonoBehaviour implementing ILevelUpUpgradeBroker. If left empty, level ups resolve instantly.")]
        [SerializeField] MonoBehaviour upgradeBrokerComponent;

        [Header("Events")]
        public UnityEvent<int, int, int> OnXpChanged = new UnityEvent<int, int, int>(); // currentXpInLevel, xpToNextLevel, currentLevel
        public UnityEvent<int> OnLevelUp = new UnityEvent<int>();                       // new level
        public UnityEvent<int, float> OnComboChanged = new UnityEvent<int, float>();    // count, timeRemaining
        public UnityEvent OnComboReset = new UnityEvent();
        public UnityEvent<XpAwardEvent> OnXpAwarded = new UnityEvent<XpAwardEvent>();   // for popups
        public UnityEvent<long> OnScoreChanged = new UnityEvent<long>();
        public UnityEvent<RunStats> OnRunEnded = new UnityEvent<RunStats>();

        ILevelUpUpgradeBroker upgradeBroker;
        readonly RunStats stats = new RunStats();

        int currentLevel = 1;
        int currentXpInLevel;
        int xpToNextLevel;
        int comboCount;
        float comboTimer;
        float multiKillWindowTimer;
        int multiKillExtraThisWindow;
        float runStartUnscaledTime;
        bool runActive;
        bool levelUpPending;
        long score;
        long lastBroadcastScore = -1;

        public ScoringConfig Config => config;
        public int CurrentLevel => currentLevel;
        public int CurrentXpInLevel => currentXpInLevel;
        public int XpToNextLevel => xpToNextLevel;
        public float LevelProgress01 => xpToNextLevel <= 0 ? 0f : Mathf.Clamp01(currentXpInLevel / (float)xpToNextLevel);
        public int ComboCount => comboCount;
        public float ComboTimeRemaining => Mathf.Max(0f, comboTimer);
        public long Score => score;
        public bool RunActive => runActive;
        public RunStats Stats => stats;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            if (config == null)
            {
                Debug.LogWarning("[ScoringRuntime] No ScoringConfig assigned. Creating a default at runtime.", this);
                config = ScriptableObject.CreateInstance<ScoringConfig>();
            }

            upgradeBroker = upgradeBrokerComponent as ILevelUpUpgradeBroker;
            if (upgradeBrokerComponent != null && upgradeBroker == null)
                Debug.LogWarning($"[ScoringRuntime] Assigned broker '{upgradeBrokerComponent.name}' does not implement ILevelUpUpgradeBroker.", this);

            ResetRunState();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (autoStartRun)
                BeginRun();
        }

        void Update()
        {
            if (!runActive)
                return;

            // Survival timer uses unscaled time so it doesn't tick during the
            // upgrade-pause but still reflects the player's investment.
            stats.SurvivalTimeSeconds = Time.unscaledTime - runStartUnscaledTime;
            RecomputeAndBroadcastScore();

            if (comboTimer > 0f)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                    ResetCombo(announce: true);
                else
                    OnComboChanged.Invoke(comboCount, comboTimer);
            }

            if (multiKillWindowTimer > 0f)
            {
                multiKillWindowTimer -= Time.deltaTime;
                if (multiKillWindowTimer <= 0f)
                    multiKillExtraThisWindow = 0;
            }
        }

        public void SetUpgradeBroker(ILevelUpUpgradeBroker broker)
        {
            upgradeBroker = broker;
        }

        public void BeginRun()
        {
            ResetRunState();
            runActive = true;
            runStartUnscaledTime = Time.unscaledTime;
            OnXpChanged.Invoke(currentXpInLevel, xpToNextLevel, currentLevel);
            lastBroadcastScore = -1;
            RecomputeAndBroadcastScore();
            OnComboChanged.Invoke(comboCount, comboTimer);
        }

        /// <summary>
        /// Ends the run, computes final score, and fires <see cref="OnRunEnded"/>.
        /// Idempotent — additional calls are no-ops.
        /// </summary>
        public void EndRun()
        {
            if (!runActive)
                return;

            runActive = false;
            stats.LevelReached = currentLevel;
            stats.SurvivalTimeSeconds = Time.unscaledTime - runStartUnscaledTime;
            score = ComputeFinalScore();
            stats.FinalScore = score;
            lastBroadcastScore = score;
            OnScoreChanged.Invoke(score);
            OnRunEnded.Invoke(stats);
        }

        // ---------- Combat entry points ----------

        /// <summary>
        /// Standard kill report. <paramref name="context"/> carries the method,
        /// boss flag and source position so the HUD can spawn floating popups.
        /// </summary>
        public void ReportKill(KillContext context)
        {
            if (!runActive)
                return;

            stats.Kills++;

            KillMethod method = context.Method;
            if (method == KillMethod.Unknown && config.defaultUntaggedKillsToStrike)
                method = KillMethod.Strike;

            int baseXp = context.IsBoss ? config.bossKillXp : config.baseKillXp;
            float styleMultiplier = context.IsBoss ? 1f : config.GetStyleMultiplier(method);

            // Combo math runs before multi-kill so the bonus stacks on top of the multiplier.
            IncrementCombo();
            float comboMultiplier = GetComboMultiplier(comboCount);

            int multiKillBonus = 0;
            if (multiKillWindowTimer > 0f)
            {
                multiKillExtraThisWindow++;
                multiKillBonus = multiKillExtraThisWindow * config.multiKillBonusXp;
            }
            multiKillWindowTimer = config.multiKillWindowSeconds;

            int rawXp = baseXp;
            int finalXp = Mathf.RoundToInt(rawXp * styleMultiplier * comboMultiplier) + multiKillBonus;
            finalXp = Mathf.Max(1, finalXp);

            if (context.IsBoss)
                stats.BossesKilled++;

            ApplyXp(finalXp);

            OnXpAwarded.Invoke(new XpAwardEvent(method, rawXp, finalXp, comboCount, styleMultiplier, comboMultiplier, context.Position));
        }

        /// <summary>Convenience wrapper for the most common path.</summary>
        public void ReportKill(ArkhamEnemy enemy, KillMethod method, bool isBoss = false)
        {
            Vector3 position = enemy != null ? enemy.transform.position : transform.position;
            ReportKill(new KillContext(enemy, method, isBoss, position));
        }

        /// <summary>
        /// Successful counter (no kill). Awards flat XP and keeps the combo alive
        /// without consuming the multi-kill window.
        /// </summary>
        public void ReportCounter(ArkhamEnemy enemy)
        {
            if (!runActive)
                return;

            stats.CountersLanded++;
            IncrementCombo();

            int xp = config.counterXp;
            ApplyXp(xp);

            Vector3 position = enemy != null ? enemy.transform.position : transform.position;
            OnXpAwarded.Invoke(new XpAwardEvent(KillMethod.Strike, xp, xp, comboCount, 1f, GetComboMultiplier(comboCount), position));
        }

        /// <summary>
        /// Fired by the wave director when a wave is fully cleared.
        /// </summary>
        public void ReportWaveCleared(int waveNumber)
        {
            if (!runActive)
                return;

            stats.WavesCleared++;
            int xp = Mathf.Max(0, config.wavesClearedBaseXp * (waveNumber + 1));
            if (xp > 0)
                ApplyXp(xp);
        }

        /// <summary>
        /// Forward-compatible hook for the future mission-system. Missions
        /// publish their reward via this entry point so the scoring system
        /// stays decoupled from mission internals.
        /// </summary>
        public void ReportMissionCompleted(int xpReward)
        {
            if (!runActive)
                return;

            stats.MissionsCompleted++;
            if (xpReward > 0)
                ApplyXp(xpReward);
        }

        /// <summary>
        /// Escape hatch for systems (e.g. "recicla chatarra") that need to grant
        /// XP without registering a kill. Doesn't touch combo.
        /// </summary>
        public void ReportRawXp(int amount)
        {
            if (!runActive || amount <= 0)
                return;
            ApplyXp(amount);
        }

        // ---------- Internal ----------

        void ApplyXp(int amount)
        {
            if (amount <= 0)
                return;

            stats.TotalXpEarned += amount;
            currentXpInLevel += amount;

            while (currentXpInLevel >= xpToNextLevel)
            {
                int rollover = currentXpInLevel - xpToNextLevel;
                currentXpInLevel = rollover;
                TriggerLevelUp();
            }

            OnXpChanged.Invoke(currentXpInLevel, xpToNextLevel, currentLevel);
            RecomputeAndBroadcastScore();
        }

        void TriggerLevelUp()
        {
            currentLevel++;
            xpToNextLevel = config.GetXpForNextLevel(currentLevel);
            stats.LevelReached = currentLevel;

            OnLevelUp.Invoke(currentLevel);

            if (upgradeBroker != null)
            {
                levelUpPending = true;
                if (config.pauseOnLevelUp)
                    Time.timeScale = 0f;

                int levelAtRequest = currentLevel;
                upgradeBroker.RequestUpgradeOffer(levelAtRequest, ResolveUpgradeOffer);
            }
        }

        void ResolveUpgradeOffer()
        {
            if (!levelUpPending)
                return;
            levelUpPending = false;
            if (config.pauseOnLevelUp)
                Time.timeScale = 1f;
        }

        void IncrementCombo()
        {
            comboCount++;
            comboTimer = config.comboWindowSeconds;

            if (comboCount > stats.MaxComboReached)
                stats.MaxComboReached = comboCount;

            OnComboChanged.Invoke(comboCount, comboTimer);
        }

        void ResetCombo(bool announce)
        {
            if (comboCount == 0 && !announce)
                return;
            comboCount = 0;
            comboTimer = 0f;
            OnComboChanged.Invoke(0, 0f);
            if (announce)
                OnComboReset.Invoke();
        }

        float GetComboMultiplier(int count)
        {
            if (count <= 1)
                return 1f;
            float mult = 1f + (count - 1) * config.comboScalingPerStep;
            return Mathf.Min(mult, config.maxComboMultiplier);
        }

        long ComputeFinalScore()
        {
            long survival = (long)Mathf.Floor(stats.SurvivalTimeSeconds * config.survivalScorePerSecond);
            long style = (long)Mathf.Floor(stats.MaxComboReached * config.styleScorePerComboPoint);
            return stats.TotalXpEarned + survival + style;
        }

        void RecomputeAndBroadcastScore()
        {
            score = ComputeFinalScore();
            if (score == lastBroadcastScore)
                return;
            lastBroadcastScore = score;
            OnScoreChanged.Invoke(score);
        }

        void ResetRunState()
        {
            stats.Reset();
            currentLevel = 1;
            currentXpInLevel = 0;
            xpToNextLevel = config.GetXpForNextLevel(currentLevel);
            comboCount = 0;
            comboTimer = 0f;
            multiKillWindowTimer = 0f;
            multiKillExtraThisWindow = 0;
            score = 0;
            levelUpPending = false;
            runStartUnscaledTime = Time.unscaledTime;
            stats.LevelReached = currentLevel;
        }
    }
}
