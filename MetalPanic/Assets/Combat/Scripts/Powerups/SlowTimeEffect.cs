using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Drops <c>Time.timeScale</c> to <see cref="PowerupController.SlowTimeScale"/>
    /// and boosts the player's <see cref="ArkhamPlayerMotor.ExternalSpeedMultiplier"/>
    /// by the reciprocal — net effect is "everyone except the player is slow."
    ///
    /// E1: the upgrade screen sets timeScale=0; this effect re-applies the slow
    /// value as soon as the screen closes (its timer also pauses while
    /// unscaledDelta keeps ticking the controller's own update — handled in the
    /// controller, see <c>Time.unscaledDeltaTime</c> wiring).
    /// </summary>
    public sealed class SlowTimeEffect : IPowerupEffect
    {
        PowerupController controller;
        ArkhamPlayerMotor motor;
        float restoredTimeScale = 1f;
        float restoredFixedDelta;
        float configuredFixedDelta;
        bool applied;

        public PowerupId Id => PowerupId.SlowTime;
        public float Duration => controller != null ? controller.SlowTimeDuration : 8f;
        public bool HasPersistentEntity => false;

        public void Activate(PowerupContext ctx)
        {
            controller = ctx.Controller;
            motor = ctx.Motor;

            restoredTimeScale = Time.timeScale;
            restoredFixedDelta = Time.fixedDeltaTime;
            float scale = Mathf.Clamp(controller.SlowTimeScale, 0.1f, 1f);

            Time.timeScale = scale;
            // Keep physics step density similar so collisions / overlap checks
            // don't go chunky while we're slow-mo.
            configuredFixedDelta = restoredFixedDelta * scale;
            Time.fixedDeltaTime = configuredFixedDelta;

            if (motor != null)
                motor.ExternalSpeedMultiplier = 1f / Mathf.Max(0.01f, scale);

            applied = true;
        }

        public void Tick(float unscaledDelta)
        {
            // E1: an external system (e.g. upgrade screen) may set timeScale=0.
            // While paused do nothing — when it returns to a non-zero value but
            // not our slow value, snap it back so slow-mo resumes cleanly.
            if (!applied)
                return;

            float current = Time.timeScale;
            if (current <= 0f)
                return;

            float scale = Mathf.Clamp(controller.SlowTimeScale, 0.1f, 1f);
            if (!Mathf.Approximately(current, scale))
            {
                Time.timeScale = scale;
                Time.fixedDeltaTime = configuredFixedDelta;
            }
        }

        public void Deactivate(PowerupContext ctx, bool runEnded)
        {
            if (!applied)
                return;
            applied = false;

            Time.timeScale = runEnded ? 1f : restoredTimeScale;
            Time.fixedDeltaTime = restoredFixedDelta;
            if (motor != null)
                motor.ExternalSpeedMultiplier = 1f;
        }
    }
}
