using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// One-shot trap powerup. Plants a single <see cref="MagneticMineBehaviour"/>
    /// at the player's current position. Duration on the effect is short — it
    /// just bookends "MISSION → MINE PLANTED!" feedback. The mine itself lives
    /// on its own clock (arm → trigger or timeout).
    /// </summary>
    public sealed class MagneticMineEffect : IPowerupEffect
    {
        // Short — just the banner / window where the controller considers this
        // effect "active". The mine entity itself manages its real lifecycle.
        const float ActivationWindow = 0.6f;

        PowerupController controller;
        MagneticMineBehaviour activeMine;

        public PowerupId Id => PowerupId.MagneticMine;
        public float Duration => ActivationWindow;
        public bool HasPersistentEntity => activeMine != null;

        public void Activate(PowerupContext ctx)
        {
            controller = ctx.Controller;

            // E6: only one mine in arena. If we still own one, destroy it
            // before planting the new one.
            if (activeMine != null)
            {
                activeMine.DespawnSilently();
                activeMine = null;
            }

            activeMine = MagneticMineBehaviour.Spawn(
                ctx.ActivationPosition,
                ctx.EnemyManager,
                controller.MineAoERadius,
                controller.MineDamage,
                controller.MineMagnetizeMarks,
                controller.MineTriggerRadius,
                controller.MineArmTime,
                controller.MineTimeout,
                controller.MineColor);
        }

        public void Tick(float unscaledDelta)
        {
            // The mine self-manages — nothing to do per-frame from the effect.
        }

        public void Deactivate(PowerupContext ctx, bool runEnded)
        {
            // E2 — clean up the mine if the run is ending. Otherwise it
            // outlives the effect's activation window.
            if (runEnded && activeMine != null)
            {
                activeMine.DespawnSilently();
            }
            activeMine = null;
            controller = null;
        }
    }
}
