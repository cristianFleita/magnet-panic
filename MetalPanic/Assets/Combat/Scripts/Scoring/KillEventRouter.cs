using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// Glue between the combat side (ArkhamEnemy / WaveDirector) and
    /// <see cref="ScoringRuntime"/>. Subscribes to every spawned enemy and
    /// forwards their deaths and successful counters as score events.
    ///
    /// Place this anywhere in the scene next to the ScoringRuntime — it
    /// auto-resolves the rest of its references.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillEventRouter : MonoBehaviour
    {
        [Header("References (auto-resolved if empty)")]
        [SerializeField] ScoringRuntime scoring;
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] ArkhamEnemyManager enemyManager;

        readonly HashSet<ArkhamEnemy> tracked = new HashSet<ArkhamEnemy>();

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();

            if (waveDirector != null)
            {
                waveDirector.OnEnemySpawned.AddListener(HandleEnemySpawned);
                waveDirector.OnWaveCleared.AddListener(HandleWaveCleared);
            }

            if (enemyManager != null)
            {
                IReadOnlyList<ArkhamEnemy> existing = enemyManager.Enemies;
                for (int i = 0; i < existing.Count; i++)
                    TrackEnemy(existing[i]);
            }
        }

        void OnDisable()
        {
            if (waveDirector != null)
            {
                waveDirector.OnEnemySpawned.RemoveListener(HandleEnemySpawned);
                waveDirector.OnWaveCleared.RemoveListener(HandleWaveCleared);
            }

            foreach (ArkhamEnemy enemy in tracked)
            {
                if (enemy == null)
                    continue;
                enemy.OnDeath.RemoveListener(HandleEnemyDeath);
                enemy.OnCountered.RemoveListener(HandleEnemyCountered);
            }

            tracked.Clear();
        }

        void ResolveReferences()
        {
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();

            if (waveDirector == null)
                waveDirector = FindFirstObjectByType<WaveDirector>();

            if (enemyManager == null)
                enemyManager = waveDirector != null
                    ? waveDirector.EnemyManager
                    : FindFirstObjectByType<ArkhamEnemyManager>();
        }

        void HandleEnemySpawned(ArkhamEnemy enemy, ArenaDoorId door)
        {
            TrackEnemy(enemy);
        }

        void TrackEnemy(ArkhamEnemy enemy)
        {
            if (enemy == null || !tracked.Add(enemy))
                return;
            enemy.OnDeath.AddListener(HandleEnemyDeath);
            enemy.OnCountered.AddListener(HandleEnemyCountered);
        }

        void HandleEnemyDeath(ArkhamEnemy enemy)
        {
            if (scoring == null || enemy == null)
                return;

            scoring.ReportKill(enemy, enemy.LastDamageMethod, enemy.IsBoss);
            // ArkhamEnemy reuses pooled instances; drop the listener so a
            // recycled enemy doesn't double-fire on its next death.
            enemy.OnDeath.RemoveListener(HandleEnemyDeath);
            enemy.OnCountered.RemoveListener(HandleEnemyCountered);
            tracked.Remove(enemy);
        }

        void HandleEnemyCountered(ArkhamEnemy enemy)
        {
            if (scoring == null)
                return;
            scoring.ReportCounter(enemy);
        }

        void HandleWaveCleared(int waveNumber)
        {
            if (scoring == null)
                return;
            scoring.ReportWaveCleared(waveNumber);
        }
    }
}
