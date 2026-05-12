using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        public string label = "Enemy";
        public GameObject prefab;
        public EnemyDefinition definition;
        [Min(1)] public int threatCost = 1;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public sealed class WaveDirectorActConfig
    {
        public string actName = "Act 1";
        [Min(0f)] public float startTime = 0f;
        [Min(1)] public int baseBudget = 4;
        [Min(1)] public int maxBudget = 6;
        [Min(1)] public int activeDoorsMin = 1;
        [Min(1)] public int activeDoorsMax = 2;
        [Min(0f)] public float restBase = 2f;
        public List<EnemySpawnEntry> enemyPool = new List<EnemySpawnEntry>();
    }

    [Serializable]
    public sealed class ScrapTypeEntry
    {
        public string label = "LightScrap";
        public GameObject prefab;
        public MagneticObjectType type = MagneticObjectType.LightScrap;
        [Tooltip("Weights per act index (0..N-1). Missing entries fall back to lastWeight.")]
        public List<float> weightPerAct = new List<float> { 1f };
    }

    [CreateAssetMenu(menuName = "Magnet Panic/Wave Director Config", fileName = "WaveDirectorConfig")]
    public sealed class WaveDirectorConfig : ScriptableObject
    {
        [Header("Director Core")]
        [Min(1)] public int maxEnemiesAlive = 18;
        [Min(0)] public int reinforcementThreshold = 2;
        [Min(0f)] public float baseRest = 2f;
        [Min(0f)] public float minRest = 0.55f;
        [Min(0f)] public float restReductionPerAct = 0.25f;
        [Min(0f)] public float warmupDuration = 2.5f;
        [Min(0f)] public float doorWarningTime = 0.75f;
        public Vector2 spawnIntervalRange = new Vector2(0.2f, 0.45f);
        [Min(0f)] public float doorPlayerMinDistance = 6f;
        [Min(1)] public int maxSameDoorStreak = 2;
        [Min(0f)] public float underusedDoorBonus = 4f;
        [Min(0f)] public float sameDoorPenalty = 3f;
        [Min(0f)] public float playerCampingDoorPenalty = 6f;
        [Min(0f)] public float waveBudgetGrowth = 0.5f;

        [Header("Acts")]
        public List<WaveDirectorActConfig> acts = new List<WaveDirectorActConfig>();

        [Header("Scrap")]
        public List<ScrapTypeEntry> scrapTypes = new List<ScrapTypeEntry>();
        [Min(0f)] public float scrapNearMinRadius = 4f;
        [Min(0f)] public float scrapNearMaxRadius = 10f;
        [Min(0f)] public float scrapPlayerMinDistance = 2.5f;
        [Min(0f)] public float scrapDoorMinDistance = 3f;
        [Min(0f)] public float scrapEnemyMinDistance = 1.5f;
        [Min(0f)] public float scrapWallInset = 1f;
        [Min(0)] public int scrapMaxActive = 14;
        [Min(0)] public int scrapAmmoFloorNearPlayer = 4;
        [Min(0)] public int scrapAmmoFloorBonusPerAct = 1;
        [Min(0.05f)] public float scrapRefillCadence = 0.6f;
        [Min(1)] public int scrapBurstPerRefill = 3;
        [Min(1)] public int scrapSamplingAttempts = 16;

        [Header("Healing")]
        public GameObject healingPickupPrefab;
        [Min(0f)] public float healingSpawnCooldown = 30f;
        [Range(0f, 1f)] public float healingHpThreshold = 0.5f;
        [Min(1)] public int healingMaxActive = 1;
        [Min(0f)] public float healingFirstSpawnDelay = 15f;

        public WaveDirectorActConfig GetAct(int actIndex)
        {
            if (acts == null || acts.Count == 0)
                return null;

            int index = Mathf.Clamp(actIndex, 0, acts.Count - 1);
            return acts[index];
        }

        public int ActCount => acts != null ? acts.Count : 0;

        public float GetScrapWeight(ScrapTypeEntry entry, int actIndex)
        {
            if (entry == null || entry.weightPerAct == null || entry.weightPerAct.Count == 0)
                return 0f;

            int clamped = Mathf.Clamp(actIndex, 0, entry.weightPerAct.Count - 1);
            return Mathf.Max(0f, entry.weightPerAct[clamped]);
        }
    }
}
