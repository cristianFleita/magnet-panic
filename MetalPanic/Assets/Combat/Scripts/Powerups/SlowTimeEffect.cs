using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// "Slow Time" without touching <c>Time.timeScale</c> — instead halves the
    /// <see cref="ArkhamEnemy.ExternalSpeedMultiplier"/> on every live enemy
    /// for the effect's duration. Player, magnetic forces, animation and
    /// physics all keep running at full speed; only enemy locomotion gets
    /// gated. Newly-spawned enemies during the window are picked up on the
    /// next Tick.
    /// </summary>
    public sealed class SlowTimeEffect : IPowerupEffect
    {
        const float SlowMultiplier = 0.5f;

        PowerupController controller;
        ArkhamEnemyManager enemyManager;
        readonly HashSet<ArkhamEnemy> affected = new HashSet<ArkhamEnemy>();
        bool active;

        public PowerupId Id => PowerupId.SlowTime;
        public float Duration => controller != null ? controller.SlowTimeDuration : 8f;
        public bool HasPersistentEntity => false;

        public void Activate(PowerupContext ctx)
        {
            controller = ctx.Controller;
            enemyManager = ctx.EnemyManager;
            affected.Clear();
            active = true;

            ApplySlowToCurrentEnemies();
        }

        public void Tick(float unscaledDelta)
        {
            // Wave director may spawn new enemies during the slow window. Sweep
            // the live list every tick so they snap to half-speed too.
            if (!active)
                return;

            ApplySlowToCurrentEnemies();
        }

        public void Deactivate(PowerupContext ctx, bool runEnded)
        {
            if (!active)
                return;
            active = false;

            foreach (ArkhamEnemy enemy in affected)
            {
                if (enemy == null)
                    continue;
                // Restore to 1 unconditionally — only this effect writes to
                // ExternalSpeedMultiplier today, so there's no stack to unwind.
                enemy.ExternalSpeedMultiplier = 1f;
            }
            affected.Clear();
            controller = null;
            enemyManager = null;
        }

        void ApplySlowToCurrentEnemies()
        {
            if (enemyManager == null)
                return;

            IReadOnlyList<ArkhamEnemy> enemies = enemyManager.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;
                if (affected.Add(enemy))
                    enemy.ExternalSpeedMultiplier = SlowMultiplier;
            }
        }
    }
}
