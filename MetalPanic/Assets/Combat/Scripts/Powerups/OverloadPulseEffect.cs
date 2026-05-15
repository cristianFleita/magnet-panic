using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Emits a radial pulse around the player every
    /// <see cref="PowerupController.PulseInterval"/> seconds. Each pulse uses
    /// <see cref="ArkhamEnemy.ReceiveMagneticImpact"/> so the damage / knockback
    /// path is shared with the existing magnet repel — keeps reactions consistent.
    /// </summary>
    public sealed class OverloadPulseEffect : IPowerupEffect
    {
        PowerupController controller;
        Transform player;
        ArkhamEnemyManager enemyManager;
        float nextPulseAt;
        float elapsed;
        bool active;

        public PowerupId Id => PowerupId.OverloadPulse;
        public float Duration => controller != null ? controller.PulseDuration : 10f;
        public bool HasPersistentEntity => false;

        public void Activate(PowerupContext ctx)
        {
            controller = ctx.Controller;
            player = ctx.Player;
            enemyManager = ctx.EnemyManager;
            elapsed = 0f;
            nextPulseAt = 0f; // first pulse on activation
            active = true;
        }

        public void Tick(float unscaledDelta)
        {
            if (!active || controller == null)
                return;

            elapsed += unscaledDelta;
            if (elapsed < nextPulseAt)
                return;

            EmitPulse();
            nextPulseAt = elapsed + Mathf.Max(0.1f, controller.PulseInterval);
        }

        public void Deactivate(PowerupContext ctx, bool runEnded)
        {
            active = false;
            controller = null;
            player = null;
            enemyManager = null;
        }

        void EmitPulse()
        {
            if (player == null || enemyManager == null)
                return;

            Vector3 origin = player.position;
            SpawnPulseVfx(origin);
            float radiusSqr = controller.PulseRadius * controller.PulseRadius;
            int damage = Mathf.Max(0, controller.PulseDamage);
            float knockback = controller.PulseKnockbackDistance;

            IReadOnlyList<ArkhamEnemy> enemies = enemyManager.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                Vector3 delta = enemy.transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr)
                    continue;

                if (damage > 0)
                    enemy.ReceiveMagneticImpact(damage, origin, knockback, clearsMagnetized: false);
                else
                    enemy.RejectMagneticPull(origin, knockback * 4f);
            }

            DebugDrawRing(origin, controller.PulseRadius);
        }

        void SpawnPulseVfx(Vector3 origin)
        {
            GameObject prefab = controller != null ? controller.PulseVfxPrefab : null;
            if (prefab == null)
                return;

            // World-space one-shot: don't parent to the player, otherwise the
            // VFX follows the player as they move during its lifetime. The
            // particle systems' own stop action handles cleanup.
            GameObject instance = Object.Instantiate(prefab, origin, Quaternion.identity);

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float lifetime = 1.5f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;
                ps.Play(true);
                lifetime = Mathf.Max(lifetime, ps.main.duration + ps.main.startLifetime.constantMax);
            }

            Object.Destroy(instance, lifetime);
        }

        static void DebugDrawRing(Vector3 origin, float radius)
        {
#if UNITY_EDITOR
            const int segments = 32;
            Color color = new Color(0.18f, 0.86f, 1f, 0.85f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector3 p0 = origin + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = origin + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Debug.DrawLine(p0, p1, color, 0.3f);
            }
#endif
        }
    }
}
