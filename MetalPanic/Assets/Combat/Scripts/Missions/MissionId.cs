namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Identifier for the MVP mission catalog. Adding a mission means: new id
    /// here, new <see cref="MissionDefinition"/> asset, and a tracker
    /// MonoBehaviour that returns this id from <see cref="MissionTrackerBase.Id"/>.
    /// </summary>
    public enum MissionId
    {
        Unknown = 0,
        ComboHunter = 1,
        Counterstorm = 2,
        ScrapCollector = 3,
        WallSlam = 4,
        NoHands = 5,
    }
}
