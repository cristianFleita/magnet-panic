using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    public enum WaveDirectorState
    {
        Idle,
        Bootstrapping,
        Warmup,
        WaveActive,
        Rest,
        ActTransition,
        Stopped
    }

    public sealed class WaveDirector : MonoBehaviour
    {
        const int MaxDoorHistory = 6;

        [Header("Config")]
        [SerializeField] WaveDirectorConfig config;

        [Header("References")]
        [SerializeField] ArenaSystem arena;
        [SerializeField] ArkhamEnemyManager enemyManager;
        [SerializeField] Transform player;

        [Header("Behavior")]
        [SerializeField] bool startRunOnEnable = false;

        [Header("Events")]
        public UnityEvent<int> OnActStarted = new UnityEvent<int>();
        public UnityEvent<int> OnWaveStarted = new UnityEvent<int>();
        public UnityEvent<int> OnWaveCleared = new UnityEvent<int>();
        public UnityEvent<ArenaDoorId> OnDoorWarning = new UnityEvent<ArenaDoorId>();
        public UnityEvent<ArkhamEnemy, ArenaDoorId> OnEnemySpawned = new UnityEvent<ArkhamEnemy, ArenaDoorId>();
        public UnityEvent OnRunStopped = new UnityEvent();

        readonly List<ArenaDoorId> recentDoorHistory = new List<ArenaDoorId>(MaxDoorHistory);
        readonly List<ArenaDoor> doorScratch = new List<ArenaDoor>(8);
        readonly List<EnemySpawnEntry> enemyScratch = new List<EnemySpawnEntry>(8);

        WaveDirectorState state = WaveDirectorState.Idle;
        Coroutine runLoop;
        int actIndex;
        int waveIndex;
        float runClock;
        int sameDoorStreak;
        ArenaDoorId? lastDoor;

        public WaveDirectorState State => state;
        public int CurrentActIndex => actIndex;
        public int CurrentWaveIndex => waveIndex;
        public float RunClock => runClock;
        public ArkhamEnemyManager EnemyManager => enemyManager;
        public ArenaSystem Arena => arena;
        public WaveDirectorConfig Config => config;
        public Transform Player => player;

        public int AliveEnemyCount => enemyManager != null ? enemyManager.AliveEnemyCount() : 0;

        Coroutine autoStartRoutine;

        void Start()
        {
            if (startRunOnEnable && config != null)
                autoStartRoutine = StartCoroutine(AutoStartAfterSceneInit());
        }

        IEnumerator AutoStartAfterSceneInit()
        {
            yield return null;
            autoStartRoutine = null;

            if (isActiveAndEnabled && runLoop == null && config != null)
                StartRun();
        }

        void Update()
        {
            if (state == WaveDirectorState.WaveActive || state == WaveDirectorState.Rest || state == WaveDirectorState.ActTransition)
                runClock += Time.deltaTime;
        }

        public void SetConfig(WaveDirectorConfig nextConfig)
        {
            if (nextConfig == null)
                return;

            config = nextConfig;
        }

        public void SetReferences(ArenaSystem arenaRef, ArkhamEnemyManager managerRef, Transform playerRef)
        {
            if (arenaRef != null) arena = arenaRef;
            if (managerRef != null) enemyManager = managerRef;
            if (playerRef != null) player = playerRef;
        }

        void OnDisable()
        {
            if (autoStartRoutine != null)
            {
                StopCoroutine(autoStartRoutine);
                autoStartRoutine = null;
            }

            StopRun();
        }

        public void StartRun()
        {
            if (runLoop != null)
                StopCoroutine(runLoop);

            runLoop = StartCoroutine(RunCoroutine());
        }

        public void StopRun()
        {
            if (runLoop != null)
            {
                StopCoroutine(runLoop);
                runLoop = null;
            }

            if (state != WaveDirectorState.Stopped)
            {
                state = WaveDirectorState.Stopped;
                OnRunStopped.Invoke();
            }
        }

        IEnumerator RunCoroutine()
        {
            state = WaveDirectorState.Bootstrapping;
            if (!ResolveDependencies())
            {
                Debug.LogWarning("WaveDirector: missing dependencies, halting run.", this);
                state = WaveDirectorState.Stopped;
                yield break;
            }

            state = WaveDirectorState.Warmup;
            yield return new WaitForSeconds(config.warmupDuration);

            actIndex = 0;
            waveIndex = 0;
            runClock = 0f;
            sameDoorStreak = 0;
            lastDoor = null;
            recentDoorHistory.Clear();

            OnActStarted.Invoke(actIndex);

            while (true)
            {
                MaybeAdvanceAct();

                WaveDirectorActConfig actConfig = config.GetAct(actIndex);
                if (actConfig == null)
                {
                    Debug.LogWarning("WaveDirector: no acts configured.", this);
                    yield break;
                }

                state = WaveDirectorState.WaveActive;
                OnWaveStarted.Invoke(waveIndex);

                yield return StartCoroutine(RunWave(actConfig));

                OnWaveCleared.Invoke(waveIndex);
                waveIndex++;

                state = WaveDirectorState.Rest;
                yield return StartCoroutine(WaitForRestAndReinforcement(actConfig));
            }
        }

        bool ResolveDependencies()
        {
            if (config == null)
            {
                Debug.LogError("WaveDirector: missing WaveDirectorConfig.", this);
                return false;
            }

            if (arena == null)
                arena = FindFirstObjectByType<ArenaSystem>();
            if (arena != null)
                arena.RebuildSpawnCache();

            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();

            if (player == null)
            {
                ArkhamCombatController combat = FindFirstObjectByType<ArkhamCombatController>();
                if (combat != null)
                    player = combat.transform;
            }

            if (arena == null)
            {
                Debug.LogWarning("WaveDirector: missing ArenaSystem dependency.", this);
                return false;
            }

            if (enemyManager == null)
            {
                Debug.LogWarning("WaveDirector: missing ArkhamEnemyManager dependency.", this);
                return false;
            }

            if (player == null)
            {
                Debug.LogWarning("WaveDirector: missing player dependency.", this);
                return false;
            }

            if (arena.Doors.Count == 0)
            {
                Debug.LogWarning("WaveDirector: ArenaSystem has no doors cached.", arena);
                return false;
            }

            return true;
        }

        void MaybeAdvanceAct()
        {
            int target = actIndex;
            for (int i = actIndex + 1; i < config.ActCount; i++)
            {
                WaveDirectorActConfig candidate = config.acts[i];
                if (candidate != null && runClock >= candidate.startTime)
                    target = i;
            }

            if (target != actIndex)
            {
                actIndex = target;
                state = WaveDirectorState.ActTransition;
                OnActStarted.Invoke(actIndex);
            }
        }

        IEnumerator RunWave(WaveDirectorActConfig actConfig)
        {
            int budget = ComputeWaveBudget(actConfig);
            int doorCount = UnityEngine.Random.Range(actConfig.activeDoorsMin, actConfig.activeDoorsMax + 1);
            doorCount = Mathf.Clamp(doorCount, 1, arena.Doors.Count);

            doorScratch.Clear();
            for (int i = 0; i < doorCount; i++)
            {
                ArenaDoor door = PickDoor(doorScratch);
                if (door != null)
                    doorScratch.Add(door);
            }

            if (doorScratch.Count == 0)
                yield break;

            DistributeBudgetAcrossDoors(actConfig, budget, doorScratch, out List<int> perDoorBudgets);

            // Door warnings (parallel)
            for (int i = 0; i < doorScratch.Count; i++)
                OnDoorWarning.Invoke(doorScratch[i].DoorId);

            yield return new WaitForSeconds(config.doorWarningTime);

            // Per-door spawn queue
            for (int i = 0; i < doorScratch.Count; i++)
            {
                StartCoroutine(SpawnQueueForDoor(actConfig, doorScratch[i], perDoorBudgets[i]));
            }

            // Wait until either all enemies are alive cap or perDoorBudgets fully consumed.
            // Wait one frame; the wave is considered "active" until reinforcement kicks in.
            yield return null;
        }

        IEnumerator SpawnQueueForDoor(WaveDirectorActConfig actConfig, ArenaDoor door, int budgetForDoor)
        {
            int remaining = budgetForDoor;
            float minInterval = Mathf.Min(config.spawnIntervalRange.x, config.spawnIntervalRange.y);
            float maxInterval = Mathf.Max(config.spawnIntervalRange.x, config.spawnIntervalRange.y);

            while (remaining > 0)
            {
                if (state == WaveDirectorState.Stopped)
                    yield break;

                if (AliveEnemyCount >= config.maxEnemiesAlive)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                EnemySpawnEntry entry = PickEnemyEntry(actConfig, remaining);
                if (entry == null || entry.prefab == null)
                {
                    // Cannot fit any enemy in remaining budget.
                    yield break;
                }

                SpawnEnemy(entry, door);
                remaining -= entry.threatCost;

                yield return new WaitForSeconds(UnityEngine.Random.Range(minInterval, maxInterval));
            }
        }

        void SpawnEnemy(EnemySpawnEntry entry, ArenaDoor door)
        {
            Vector3 facing = door.FacingDirection;
            Quaternion rotation = facing.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(facing, Vector3.up)
                : Quaternion.identity;

            GameObject instance = Pool.Spawn(entry.prefab, door.ExitPosition, rotation);
            if (instance == null)
                return;

            ArkhamEnemy enemy = instance.GetComponent<ArkhamEnemy>();
            if (enemy == null)
            {
                Debug.LogWarning("WaveDirector: spawned enemy prefab has no ArkhamEnemy component.", entry.prefab);
                Pool.Despawn(instance);
                return;
            }

            if (entry.definition != null)
                enemy.Apply(entry.definition);

            enemyManager?.Register(enemy);
            OnEnemySpawned.Invoke(enemy, door.DoorId);
        }

        int ComputeWaveBudget(WaveDirectorActConfig actConfig)
        {
            return ComputeWaveBudget(actConfig, waveIndex, config.waveBudgetGrowth);
        }

        public static int ComputeWaveBudget(WaveDirectorActConfig actConfig, int waveIndex, float waveBudgetGrowth)
        {
            if (actConfig == null)
                return 0;

            int growth = Mathf.FloorToInt(Mathf.Max(0, waveIndex) * Mathf.Max(0f, waveBudgetGrowth));
            int raw = actConfig.baseBudget + growth;
            return Mathf.Clamp(raw, actConfig.baseBudget, actConfig.maxBudget);
        }

        void DistributeBudgetAcrossDoors(WaveDirectorActConfig actConfig, int totalBudget, List<ArenaDoor> doors, out List<int> perDoor)
        {
            perDoor = new List<int>(doors.Count);
            int baseShare = totalBudget / Mathf.Max(1, doors.Count);
            int remainder = totalBudget - baseShare * doors.Count;
            for (int i = 0; i < doors.Count; i++)
                perDoor.Add(baseShare + (i < remainder ? 1 : 0));
        }

        ArenaDoor PickDoor(List<ArenaDoor> excluded)
        {
            IReadOnlyList<ArenaDoor> all = arena.Doors;
            if (all.Count == 0)
                return null;

            float bestWeight = float.NegativeInfinity;
            ArenaDoor best = null;
            Vector3 playerPos = player.position;

            for (int i = 0; i < all.Count; i++)
            {
                ArenaDoor door = all[i];
                if (door == null || !door.IsEnabled)
                    continue;
                if (Contains(excluded, door))
                    continue;

                float weight = ScoreDoor(door, playerPos);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = door;
                }
            }

            if (best != null)
            {
                if (lastDoor.HasValue && best.DoorId == lastDoor.Value)
                    sameDoorStreak++;
                else
                    sameDoorStreak = 1;

                lastDoor = best.DoorId;
                recentDoorHistory.Insert(0, best.DoorId);
                if (recentDoorHistory.Count > MaxDoorHistory)
                    recentDoorHistory.RemoveAt(recentDoorHistory.Count - 1);
            }

            return best;
        }

        float ScoreDoor(ArenaDoor door, Vector3 playerPos)
        {
            Vector3 delta = door.ExitPosition - playerPos;
            delta.y = 0f;
            float distance = delta.magnitude;

            float weight = distance;

            // Bonus for doors not used recently.
            int recencyIndex = recentDoorHistory.IndexOf(door.DoorId);
            if (recencyIndex < 0)
                weight += config.underusedDoorBonus;

            // Penalty for same-door repetition past streak cap.
            if (lastDoor.HasValue && door.DoorId == lastDoor.Value && sameDoorStreak >= config.maxSameDoorStreak)
                weight -= config.sameDoorPenalty;

            // Penalty when player camps directly on top of the door.
            if (distance < config.doorPlayerMinDistance)
                weight -= config.playerCampingDoorPenalty;

            return weight;
        }

        EnemySpawnEntry PickEnemyEntry(WaveDirectorActConfig actConfig, int remainingBudget)
        {
            enemyScratch.Clear();
            float totalWeight = 0f;
            for (int i = 0; i < actConfig.enemyPool.Count; i++)
            {
                EnemySpawnEntry entry = actConfig.enemyPool[i];
                if (entry == null || entry.prefab == null)
                    continue;
                if (entry.threatCost > remainingBudget)
                    continue;
                if (entry.weight <= 0f)
                    continue;

                enemyScratch.Add(entry);
                totalWeight += entry.weight;
            }

            if (enemyScratch.Count == 0)
                return null;

            float pick = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < enemyScratch.Count; i++)
            {
                pick -= enemyScratch[i].weight;
                if (pick <= 0f)
                    return enemyScratch[i];
            }

            return enemyScratch[enemyScratch.Count - 1];
        }

        IEnumerator WaitForRestAndReinforcement(WaveDirectorActConfig actConfig)
        {
            float restTarget = Mathf.Max(config.minRest, actConfig.restBase - actIndex * config.restReductionPerAct);
            float restTimer = 0f;

            while (true)
            {
                restTimer += Time.deltaTime;

                bool restDone = restTimer >= restTarget;
                bool fewEnemies = AliveEnemyCount <= config.reinforcementThreshold;
                if (restDone && fewEnemies)
                    yield break;

                yield return null;
            }
        }

        static bool Contains(List<ArenaDoor> list, ArenaDoor candidate)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == candidate)
                    return true;
            }

            return false;
        }
    }
}
