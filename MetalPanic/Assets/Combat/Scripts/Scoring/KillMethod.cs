namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// How a kill was scored. Drives the style multiplier in <see cref="ScoringConfig"/>.
    /// Boss / mission / wave / counter rewards are awarded through dedicated
    /// <see cref="ScoringRuntime"/> entry points, not by extending this enum.
    /// </summary>
    public enum KillMethod
    {
        Unknown = 0,
        Strike = 1,
        Repel = 2,
        EnemyRepel = 3,
        WallSlam = 4,
        Overload = 5,
    }
}
