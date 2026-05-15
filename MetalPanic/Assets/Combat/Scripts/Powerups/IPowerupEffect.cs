namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Implemented by every powerup. Lifecycle is exactly
    /// <c>Activate → Tick* → Deactivate</c>. Effects that spawn long-lived
    /// entities (e.g. Magnetic Mine) hand ownership off to the spawned object;
    /// Deactivate only reverts modifiers applied to the player / world.
    /// </summary>
    public interface IPowerupEffect
    {
        PowerupId Id { get; }
        float Duration { get; }

        /// <summary>True while a separate spawned entity owned by this effect is alive.</summary>
        bool HasPersistentEntity { get; }

        void Activate(PowerupContext ctx);
        void Tick(float unscaledDelta);
        void Deactivate(PowerupContext ctx, bool runEnded);
    }
}
