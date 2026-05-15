using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Magnetize N enemies." There is no manager-level event for newly
    /// magnetized enemies, so we poll the live roster each frame and count
    /// unique instance IDs that transitioned to <see cref="MagneticMarkState.Magnetized"/>
    /// during the mission window.
    /// </summary>
    public sealed class MagnetMaestroTracker : MissionTrackerBase
    {
        [SerializeField] ArkhamEnemyManager enemyManager;
        readonly HashSet<int> credited = new HashSet<int>();

        public override MissionId Id => MissionId.MagnetMaestro;

        void Awake()
        {
            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();
        }

        protected override void OnBegin()
        {
            credited.Clear();
        }

        protected override void OnEnd()
        {
            credited.Clear();
        }

        void Update()
        {
            if (!isTracking || enemyManager == null)
                return;

            IReadOnlyList<ArkhamEnemy> roster = enemyManager.Enemies;
            for (int i = 0; i < roster.Count; i++)
            {
                ArkhamEnemy enemy = roster[i];
                if (enemy == null)
                    continue;
                if (enemy.MarkState != MagneticMarkState.Magnetized)
                    continue;

                int instanceId = enemy.GetInstanceID();
                if (credited.Add(instanceId))
                    AddProgress(1);
            }
        }
    }
}
