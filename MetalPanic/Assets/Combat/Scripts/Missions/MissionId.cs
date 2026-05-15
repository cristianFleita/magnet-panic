namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Identifier for the full mission catalog (12 entries, 3 tiers). Numeric
    /// values are *stable* and serialized inside <see cref="MissionDefinition"/>
    /// assets — do not reorder, only append.
    ///
    /// Tier groupings are declared on the <see cref="MissionDefinition"/> asset,
    /// not by the numeric range, so the values can stay in insertion order.
    /// </summary>
    public enum MissionId
    {
        Unknown = 0,

        // Original MVP (tiers 1 & 2)
        ComboHunter = 1,
        Counterstorm = 2,
        ScrapCollector = 3,
        WallSlam = 4,
        NoHands = 5,

        // Expanded catalog
        IronRain = 6,
        MagnetMaestro = 7,
        ChainReaction = 8,
        OverloadArtist = 9,
        WreckingBall = 10,
        BulletCatcher = 11,
        LivingAmmo = 12,
    }
}
