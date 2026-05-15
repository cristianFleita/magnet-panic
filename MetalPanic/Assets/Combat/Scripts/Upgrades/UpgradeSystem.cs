using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MagnetPanic.Combat.Scoring;

namespace MagnetPanic.Combat.Upgrades
{
    /// <summary>
    /// Glue between ScoringRuntime (level ups) and gameplay systems. Owns the
    /// Choice-of-3 algorithm, applies modifiers, and presents the upgrade UI
    /// through <see cref="UpgradeChoiceHud"/>. Implements
    /// <see cref="ILevelUpUpgradeBroker"/> so ScoringRuntime can drive it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeSystem : MonoBehaviour, ILevelUpUpgradeBroker
    {
        [Header("Catalog")]
        [SerializeField] UpgradeCatalog catalog;

        [Header("Gameplay refs")]
        [SerializeField] MagnetismController magnetism;
        [SerializeField] ArkhamCombatController combat;
        [SerializeField] ArkhamPlayerMotor motor;
        [SerializeField] MovementAbilityController movementAbility;
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] GameInputProvider inputProvider;

        [Header("UI")]
        [SerializeField] UpgradeChoiceHud choiceHud;

        [Header("Scoring")]
        [SerializeField] ScoringRuntime scoringRuntime;
        [SerializeField] bool registerOnStart = true;

        [Header("Magnetic Chain (combat)")]
        [SerializeField] float chainRadius = 3f;

        [Header("Events")]
        public UnityEvent<UpgradeData, int> OnUpgradeApplied = new UnityEvent<UpgradeData, int>(); // (upgrade, newStackCount)
        public UnityEvent OnUpgradeOfferShown = new UnityEvent();
        public UnityEvent OnUpgradeOfferHidden = new UnityEvent();

        readonly Dictionary<UpgradeId, int> stacks = new Dictionary<UpgradeId, int>();
        readonly List<UpgradeData> offerScratch = new List<UpgradeData>(8);
        readonly List<UpgradeData> branchScratch = new List<UpgradeData>(4);

        // Magnetic Chain state — driven by stacks of Chain upgrade.
        float chainProbability;

        Action pendingResolve;
        bool offerActive;

        public IReadOnlyDictionary<UpgradeId, int> Stacks => stacks;
        public int GetStacks(UpgradeId id) => stacks.TryGetValue(id, out int v) ? v : 0;
        public float ChainProbability => chainProbability;
        public float ChainRadius => chainRadius;

        void Awake()
        {
            if (magnetism == null) magnetism = GetComponent<MagnetismController>();
            if (combat == null) combat = GetComponent<ArkhamCombatController>();
            if (motor == null) motor = GetComponent<ArkhamPlayerMotor>();
            if (movementAbility == null) movementAbility = GetComponent<MovementAbilityController>();
            if (enemyManager == null) enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();
            if (waveDirector == null) waveDirector = FindFirstObjectByType<WaveDirector>();
            if (inputProvider == null) inputProvider = GameInputProvider.EnsureOn(gameObject);
            if (scoringRuntime == null) scoringRuntime = ScoringRuntime.Instance;
        }

        void OnEnable()
        {
            if (combat != null)
                combat.OnHit.AddListener(HandleStrikeHit);
        }

        void OnDisable()
        {
            if (combat != null)
                combat.OnHit.RemoveListener(HandleStrikeHit);
        }

        void Start()
        {
            if (registerOnStart)
                RegisterWithScoring();
        }

        public void RegisterWithScoring()
        {
            if (scoringRuntime == null)
                scoringRuntime = ScoringRuntime.Instance;
            if (scoringRuntime != null)
                scoringRuntime.SetUpgradeBroker(this);
        }

        // ---------- ILevelUpUpgradeBroker ----------

        public void RequestUpgradeOffer(int newLevel, Action onUpgradeResolved)
        {
            if (offerActive)
            {
                // Should not happen — ScoringRuntime won't double-trigger — but be safe.
                onUpgradeResolved?.Invoke();
                return;
            }

            pendingResolve = onUpgradeResolved;
            offerActive = true;

            BuildOffer(offerScratch);

            // Edge case E1 — pool fully exhausted: grant skip bonus, resume.
            if (offerScratch.Count == 0)
            {
                ResolveOffer(null);
                return;
            }

            Time.timeScale = 0f;
            if (inputProvider != null)
                inputProvider.SetState(GameInputState.UI);

            if (choiceHud != null)
            {
                choiceHud.Show(offerScratch, newLevel, OnPlayerChose);
                OnUpgradeOfferShown.Invoke();
            }
            else
            {
                // No HUD wired — auto-pick first for robustness.
                Debug.LogWarning("[UpgradeSystem] No UpgradeChoiceHud configured — auto-picking first offer.", this);
                ResolveOffer(offerScratch[0]);
            }
        }

        void OnPlayerChose(UpgradeData chosen)
        {
            ResolveOffer(chosen);
        }

        void ResolveOffer(UpgradeData chosen)
        {
            if (!offerActive)
                return;

            if (chosen != null)
                ApplyUpgrade(chosen);

            if (choiceHud != null)
            {
                choiceHud.Hide();
                OnUpgradeOfferHidden.Invoke();
            }

            if (inputProvider != null)
                inputProvider.SetState(GameInputState.Gameplay);

            Time.timeScale = 1f;
            offerActive = false;

            Action resolve = pendingResolve;
            pendingResolve = null;
            resolve?.Invoke();
        }

        // ---------- Offer generation ----------

        void BuildOffer(List<UpgradeData> result)
        {
            result.Clear();
            if (catalog == null || catalog.upgrades == null)
                return;

            // Group available upgrades by branch.
            Dictionary<UpgradeBranch, List<UpgradeData>> byBranch = new Dictionary<UpgradeBranch, List<UpgradeData>>();
            for (int i = 0; i < catalog.upgrades.Count; i++)
            {
                UpgradeData u = catalog.upgrades[i];
                if (u == null || u.id == UpgradeId.None)
                    continue;
                if (GetStacks(u.id) >= u.maxStacks)
                    continue;
                if (u.prerequisite != UpgradeId.None && GetStacks(u.prerequisite) <= 0)
                    continue;

                if (!byBranch.TryGetValue(u.branch, out List<UpgradeData> bucket))
                {
                    bucket = new List<UpgradeData>(4);
                    byBranch[u.branch] = bucket;
                }
                bucket.Add(u);
            }

            // Sample up to 3 — try to draw from distinct branches first.
            List<UpgradeBranch> branches = new List<UpgradeBranch>(byBranch.Keys);
            Shuffle(branches);

            for (int i = 0; i < branches.Count && result.Count < 3; i++)
            {
                List<UpgradeData> bucket = byBranch[branches[i]];
                UpgradeData pick = bucket[UnityEngine.Random.Range(0, bucket.Count)];
                result.Add(pick);
                bucket.Remove(pick);
            }

            // Fill remaining slots by duplicating branches (never the same upgrade).
            while (result.Count < 3)
            {
                branchScratch.Clear();
                foreach (var pair in byBranch)
                    if (pair.Value.Count > 0)
                        branchScratch.Add(pair.Value[UnityEngine.Random.Range(0, pair.Value.Count)]);

                if (branchScratch.Count == 0)
                    break;

                UpgradeData pick = branchScratch[UnityEngine.Random.Range(0, branchScratch.Count)];
                byBranch[pick.branch].Remove(pick);
                if (byBranch[pick.branch].Count == 0)
                    byBranch.Remove(pick.branch);
                result.Add(pick);
            }
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---------- Apply ----------

        public void ApplyUpgrade(UpgradeData u)
        {
            if (u == null || u.id == UpgradeId.None)
                return;

            int prevStacks = GetStacks(u.id);
            int newStacks = Mathf.Min(prevStacks + 1, u.maxStacks);
            stacks[u.id] = newStacks;
            bool isFirstStack = prevStacks == 0;

            switch (u.id)
            {
                case UpgradeId.MagneticReach:
                    if (magnetism != null) magnetism.PullRangeBonus += 1.5f;
                    break;

                case UpgradeId.QuickCoil:
                    if (magnetism != null) magnetism.PullSpeedMult += 0.3f;
                    break;

                case UpgradeId.MagneticChain:
                    chainProbability += isFirstStack ? 0.3f : 0.2f;
                    chainProbability = Mathf.Clamp01(chainProbability);
                    break;

                case UpgradeId.ScrapCannon:
                    if (magnetism != null) magnetism.RepelDamageBonus += 1;
                    break;

                case UpgradeId.Railgun:
                    if (magnetism != null)
                    {
                        magnetism.RepelSpeedMult += 0.3f;
                        magnetism.RepelPiercingBonus += 1;
                    }
                    break;

                case UpgradeId.DeepPockets:
                    if (magnetism != null) magnetism.MaxCapacityBonus += 3;
                    break;

                case UpgradeId.HeavyLifter:
                    // Spawn unlock — flagged for wave director to consume.
                    // (Wrecking Core prefab pendiente; ver design doc Q3.)
                    Debug.Log("[UpgradeSystem] Heavy Lifter unlocked (Wrecking Core spawn flagged).", this);
                    break;

                case UpgradeId.MagneticSlide:
                    if (movementAbility != null)
                    {
                        movementAbility.UnlockSlide();
                        if (!isFirstStack)
                            movementAbility.AddSlideCooldownReduction(1f);
                    }
                    break;

                case UpgradeId.MagneticSlam:
                    if (movementAbility != null)
                        movementAbility.UnlockSlam();
                    break;

                case UpgradeId.IronStride:
                    if (magnetism != null)
                        magnetism.ChargeMobilityPenaltyMult *= 0.5f;
                    break;
            }

            OnUpgradeApplied.Invoke(u, newStacks);
        }

        // ---------- Magnetic Chain hook ----------

        void HandleStrikeHit(ArkhamEnemy struckEnemy)
        {
            if (struckEnemy == null || chainProbability <= 0f || enemyManager == null)
                return;
            if (struckEnemy.MarkState != MagneticMarkState.Magnetized)
                return;

            if (UnityEngine.Random.value > chainProbability)
                return;

            ArkhamEnemy nearest = FindNearestEnemyExcept(struckEnemy.transform.position, chainRadius, struckEnemy);
            if (nearest == null)
                return;

            nearest.ApplyMark(1);
        }

        ArkhamEnemy FindNearestEnemyExcept(Vector3 origin, float radius, ArkhamEnemy excluded)
        {
            ArkhamEnemy best = null;
            float bestSqr = radius * radius;
            var roster = enemyManager.Enemies;
            for (int i = 0; i < roster.Count; i++)
            {
                ArkhamEnemy e = roster[i];
                if (e == null || e == excluded || !e.IsAlive)
                    continue;
                Vector3 d = e.transform.position - origin;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }
    }
}
