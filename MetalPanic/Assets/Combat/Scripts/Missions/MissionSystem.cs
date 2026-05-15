using System.Collections.Generic;
using MagnetPanic.Combat.Powerups;
using MagnetPanic.Combat.Scoring;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// State machine that picks, runs, and rewards missions one at a time.
    /// Cooldown → Active → (Completing | Expired) → Cooldown.
    ///
    /// Holds zero gameplay logic about *what* a mission tracks — that lives in
    /// <see cref="MissionTrackerBase"/> subclasses. The system just owns the
    /// timer, target check, and reward payout, and routes events to the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionSystem : MonoBehaviour, IMissionGateResolver
    {
        public static MissionSystem Instance { get; private set; }

        [Header("Config")]
        [SerializeField] MissionCatalog catalog;
        [Tooltip("Random cooldown between missions, in seconds.")]
        [SerializeField] Vector2 cooldownRange = new Vector2(15f, 20f);
        [Tooltip("Seconds the HUD spends showing the 'MISSION COMPLETE' / 'EXPIRED' card before starting cooldown.")]
        [SerializeField, Min(0.1f)] float completionDisplaySeconds = 0.6f;
        [Tooltip("Auto-start the first mission after this delay when the run begins.")]
        [SerializeField, Min(0f)] float firstMissionDelay = 8f;

        [Header("References (auto-resolved)")]
        [SerializeField] ScoringRuntime scoring;
        [SerializeField] CombatHealth playerHealth;
        [SerializeField] MonoBehaviour powerupBrokerComponent;
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] OverloadController overload;

        [Header("Tier Gating")]
        [Tooltip("Run clock (s) at which Act 2 missions become available.")]
        [SerializeField, Min(0f)] float act2UnlockSeconds = 90f;
        [Tooltip("Run clock (s) at which Act 3 missions become available.")]
        [SerializeField, Min(0f)] float act3UnlockSeconds = 210f;

        [Header("Behaviour")]
        [SerializeField] bool autoStart = true;
        [Tooltip("If true, the mission timer is frozen while Time.timeScale == 0 (e.g. during upgrade picks).")]
        [SerializeField] bool pauseWithTimeScale = true;

        [Header("Events")]
        public UnityEvent<MissionDefinition> OnMissionStarted = new UnityEvent<MissionDefinition>();
        public UnityEvent<MissionRuntimeState> OnMissionProgress = new UnityEvent<MissionRuntimeState>();
        public UnityEvent<MissionDefinition> OnMissionCompleted = new UnityEvent<MissionDefinition>();
        public UnityEvent<MissionDefinition> OnMissionExpired = new UnityEvent<MissionDefinition>();
        public UnityEvent<float> OnCooldownStarted = new UnityEvent<float>();

        readonly MissionRuntimeState current = new MissionRuntimeState();
        readonly Dictionary<MissionId, MissionTrackerBase> trackers = new Dictionary<MissionId, MissionTrackerBase>();

        IPowerupBroker powerupBroker;
        MissionDefinition lastMission;
        MissionTrackerBase activeTracker;
        float cooldownTimer;
        float completionTimer;
        bool runActive;

        public MissionRuntimeState Current => current;
        public bool HasActiveMission => current.State == MissionState.Active;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            ResolveReferences();
            DiscoverTrackers();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            if (scoring != null)
                scoring.OnRunEnded.AddListener(HandleRunEnded);
        }

        void OnDisable()
        {
            if (scoring != null)
                scoring.OnRunEnded.RemoveListener(HandleRunEnded);
        }

        void Start()
        {
            if (autoStart)
                BeginRun();
        }

        void Update()
        {
            if (!runActive)
                return;

            float dt = pauseWithTimeScale ? Time.deltaTime : Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            switch (current.State)
            {
                case MissionState.Cooldown:
                    cooldownTimer -= dt;
                    if (cooldownTimer <= 0f)
                        StartNextMission();
                    break;
                case MissionState.Active:
                    current.TimeRemaining -= dt;
                    if (current.TimeRemaining <= 0f)
                        ExpireActiveMission();
                    else
                        OnMissionProgress.Invoke(current);
                    break;
                case MissionState.Completing:
                case MissionState.Expired:
                    completionTimer -= dt;
                    if (completionTimer <= 0f)
                        BeginCooldown();
                    break;
            }
        }

        public void BeginRun()
        {
            if (catalog == null || catalog.Count == 0)
            {
                Debug.LogWarning("[MissionSystem] No catalog assigned or empty. Disabling.", this);
                enabled = false;
                return;
            }

            runActive = true;
            cooldownTimer = Mathf.Max(0f, firstMissionDelay);
            current.Reset();
            current.State = MissionState.Cooldown;
            OnCooldownStarted.Invoke(cooldownTimer);
        }

        public void StopRun()
        {
            runActive = false;
            if (activeTracker != null)
            {
                activeTracker.EndTracking();
                activeTracker = null;
            }
            current.Reset();
        }

        /// <summary>Force-skip the current cooldown / mission. Useful for tooling.</summary>
        public void ForceStartNextMission()
        {
            cooldownTimer = 0f;
            if (current.State == MissionState.Cooldown)
                StartNextMission();
        }

        public void RegisterTracker(MissionTrackerBase tracker)
        {
            if (tracker == null)
                return;
            tracker.Attach(this);
            trackers[tracker.Id] = tracker;
        }

        public void NotifyProgressChanged()
        {
            if (!HasActiveMission)
                return;
            OnMissionProgress.Invoke(current);
        }

        public void CompleteActiveMission()
        {
            if (!HasActiveMission)
                return;

            MissionDefinition def = current.Definition;
            current.State = MissionState.Completing;
            current.Progress = Mathf.Max(current.Progress, current.Target);
            completionTimer = completionDisplaySeconds;

            if (activeTracker != null)
            {
                activeTracker.EndTracking();
                activeTracker = null;
            }

            ApplyRewards(def);
            OnMissionCompleted.Invoke(def);
        }

        void ExpireActiveMission()
        {
            MissionDefinition def = current.Definition;
            current.State = MissionState.Expired;
            current.TimeRemaining = 0f;
            completionTimer = completionDisplaySeconds;

            if (activeTracker != null)
            {
                activeTracker.EndTracking();
                activeTracker = null;
            }

            OnMissionExpired.Invoke(def);
        }

        void BeginCooldown()
        {
            cooldownTimer = Random.Range(cooldownRange.x, cooldownRange.y);
            current.Reset();
            current.State = MissionState.Cooldown;
            OnCooldownStarted.Invoke(cooldownTimer);
        }

        void StartNextMission()
        {
            int act = ResolveCurrentAct();
            int maxTier = ResolveMaxTier(act);
            bool tierBias = act >= 3;
            MissionDefinition next = catalog.PickNext(lastMission, maxTier, tierBias, this);
            if (next == null)
            {
                BeginCooldown();
                return;
            }

            lastMission = next;
            current.Definition = next;
            current.Progress = 0;
            current.TimeRemaining = next.durationSeconds;
            current.State = MissionState.Active;
            current.RewardPowerup = RollMissionReward(next);

            if (trackers.TryGetValue(next.id, out MissionTrackerBase tracker) && tracker != null)
            {
                activeTracker = tracker;
                tracker.BeginTracking(current);
            }
            else
            {
                Debug.LogWarning($"[MissionSystem] No tracker registered for {next.id}. Mission will only expire on timer.", this);
            }

            OnMissionStarted.Invoke(next);
            OnMissionProgress.Invoke(current);
        }

        void ApplyRewards(MissionDefinition def)
        {
            if (def == null)
                return;

            if (def.xpReward > 0 && scoring != null)
                scoring.ReportMissionCompleted(def.xpReward);

            if (def.healReward > 0 && playerHealth != null)
                playerHealth.Heal(def.healReward);

            if (def.grantsPowerup && powerupBroker != null)
            {
                PowerupId reward = current.RewardPowerup != PowerupId.None
                    ? current.RewardPowerup
                    : def.powerupWeights.Roll();
                powerupBroker.GrantSpecificPowerup(reward);
            }
        }

        PowerupId RollMissionReward(MissionDefinition def)
        {
            if (def == null || !def.grantsPowerup)
                return PowerupId.None;

            PowerupId rolled = def.powerupWeights.Total > 0f
                ? def.powerupWeights.Roll()
                : PowerupWeights.Uniform.Roll();

            // Diagnostic log — user reported never seeing OverloadPulse despite
            // most missions weighting it 0.2–0.5. Log every roll for missions
            // that *could* yield OverloadPulse so we can confirm the
            // distribution at runtime.
            if (def.powerupWeights.overloadPulse > 0f)
            {
                Debug.Log(
                    $"[MissionSystem] '{def.displayName}' rolled reward = {rolled} | weights: slow={def.powerupWeights.slowTime}, pulse={def.powerupWeights.overloadPulse}, mine={def.powerupWeights.magneticMine}",
                    this);
            }

            return rolled;
        }

        int ResolveCurrentAct()
        {
            if (waveDirector != null)
            {
                // WaveDirector tracks actIndex zero-based but exposes
                // CurrentActIndex; map to 1-based for tier logic.
                return Mathf.Max(1, waveDirector.CurrentActIndex + 1);
            }

            // Fallback: derive from ScoringRuntime survival time so the catalog
            // still opens up even when no wave director is wired (e.g. sandbox
            // scene).
            if (scoring != null)
            {
                float t = scoring.Stats != null ? scoring.Stats.SurvivalTimeSeconds : 0f;
                if (t >= act3UnlockSeconds)
                    return 3;
                if (t >= act2UnlockSeconds)
                    return 2;
            }
            return 1;
        }

        static int ResolveMaxTier(int act) => act switch
        {
            <= 1 => 1,
            2 => 2,
            _ => 3,
        };

        public bool IsGateSatisfied(MissionGate gate)
        {
            switch (gate)
            {
                case MissionGate.Always:
                    return true;
                case MissionGate.RequiresSpitterDrone:
                    return HasLiveSpitterDrone();
                case MissionGate.RequiresOverloadAccess:
                    return overload != null;
                default:
                    return true;
            }
        }

        bool HasLiveSpitterDrone()
        {
            if (enemyManager == null)
                return false;
            IReadOnlyList<ArkhamEnemy> enemies = enemyManager.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy != null && enemy.IsAlive && enemy.GetComponent<SpitterDroneBehavior>() != null)
                    return true;
            }
            return false;
        }

        void HandleRunEnded(RunStats _)
        {
            StopRun();
        }

        void ResolveReferences()
        {
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();

            if (playerHealth == null)
            {
                ArkhamCombatController combat = FindFirstObjectByType<ArkhamCombatController>();
                if (combat != null)
                    playerHealth = combat.GetComponent<CombatHealth>();
            }

            powerupBroker = powerupBrokerComponent as IPowerupBroker;
            if (powerupBrokerComponent != null && powerupBroker == null)
                Debug.LogWarning($"[MissionSystem] '{powerupBrokerComponent.name}' does not implement IPowerupBroker.", this);
            if (powerupBroker == null)
            {
                PowerupController found = FindFirstObjectByType<PowerupController>();
                if (found != null)
                {
                    powerupBroker = found;
                    powerupBrokerComponent = found;
                }
            }

            if (waveDirector == null)
                waveDirector = FindFirstObjectByType<WaveDirector>();
            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();
            if (overload == null)
                overload = FindFirstObjectByType<OverloadController>();
        }

        void DiscoverTrackers()
        {
            MissionTrackerBase[] found = FindObjectsByType<MissionTrackerBase>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
                RegisterTracker(found[i]);
        }
    }
}
