namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// Snapshot of a run sent on <see cref="ScoringRuntime.OnRunEnded"/>. Plain
    /// class so the leaderboard / death screen / host bridge can hold a reference
    /// without struct copies.
    /// </summary>
    public sealed class RunStats
    {
        public long TotalXpEarned;
        public int Kills;
        public int MaxComboReached;
        public int LevelReached;
        public float SurvivalTimeSeconds;
        public int CountersLanded;
        public int BossesKilled;
        public int WavesCleared;
        public int MissionsCompleted;
        public long FinalScore;

        public void Reset()
        {
            TotalXpEarned = 0;
            Kills = 0;
            MaxComboReached = 0;
            LevelReached = 1;
            SurvivalTimeSeconds = 0f;
            CountersLanded = 0;
            BossesKilled = 0;
            WavesCleared = 0;
            MissionsCompleted = 0;
            FinalScore = 0;
        }
    }
}
