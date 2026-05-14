namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Pluggable contract for the (future) powerup system. The
    /// <see cref="MissionSystem"/> calls this when a completed mission's
    /// <see cref="MissionDefinition.grantsPowerup"/> flag is true. If no
    /// broker is wired, that part of the reward is silently skipped.
    /// </summary>
    public interface IPowerupBroker
    {
        void GrantRandomPowerup();
    }
}
