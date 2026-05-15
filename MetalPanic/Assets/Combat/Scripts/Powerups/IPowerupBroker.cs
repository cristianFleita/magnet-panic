namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Bridge between the mission system and the powerup runtime. The mission
    /// system holds no opinions about *which* powerup to grant — it forwards
    /// the weights table from the completed mission and lets this broker roll.
    /// </summary>
    public interface IPowerupBroker
    {
        /// <summary>Roll a powerup using the supplied weights and activate it immediately.</summary>
        void GrantPowerup(PowerupWeights weights);

        /// <summary>Roll a powerup using uniform weights — fallback used by old mission tooling.</summary>
        void GrantRandomPowerup();

        /// <summary>Activate a pre-rolled powerup. Used when the mission system
        /// rolls at mission start (so the HUD can show what's at stake) and
        /// only fires the grant on completion.</summary>
        void GrantSpecificPowerup(PowerupId id);
    }
}
