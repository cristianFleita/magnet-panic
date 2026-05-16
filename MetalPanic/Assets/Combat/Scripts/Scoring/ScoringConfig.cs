using UnityEngine;

namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// All tuning knobs for the Scoring &amp; XP system in one ScriptableObject so the
    /// designer can iterate without recompiling. Defaults match
    /// design/gdd/scoring-xp-system.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Magnet Panic/Scoring Config", fileName = "ScoringConfig")]
    public sealed class ScoringConfig : ScriptableObject
    {
        [Header("Kill XP")]
        [Min(1)] public int baseKillXp = 10;
        [Min(1f)] public float repelKillMultiplier = 1.5f;
        [Min(1f)] public float wallSlamMultiplier = 2f;
        [Min(1f)] public float overloadMultiplier = 2f;
        [Min(1f)] public float enemyRepelMultiplier = 2.5f;
        [Min(1)] public int counterXp = 15;
        [Min(1)] public int bossKillXp = 100;
        [Tooltip("XP awarded when a wave is cleared: wavesClearedBaseXp * (waveNumber + 1)")]
        [Min(0)] public int wavesClearedBaseXp = 5;
        [Tooltip("Extra flat XP added per additional kill within the same multi-kill window.")]
        [Min(0)] public int multiKillBonusXp = 5;
        [Tooltip("Seconds within which simultaneous kills count toward a multi-kill bonus.")]
        [Min(0.05f)] public float multiKillWindowSeconds = 1f;

        [Header("Combo")]
        [Tooltip("Seconds of inactivity before the combo resets.")]
        [Min(0.25f)] public float comboWindowSeconds = 3f;
        [Tooltip("Bonus multiplier added per combo step above 1 (capped by maxComboMultiplier).")]
        [Min(0f)] public float comboScalingPerStep = 0.1f;
        [Min(1f)] public float maxComboMultiplier = 3f;

        [Header("Leveling")]
        [Min(10)] public int baseXpPerLevel = 50;
        [Min(0)] public int xpGrowthPerLevel = 15;

        [Header("Final score")]
        [Tooltip("Legacy tuning field retained for serialized configs. Survival time no longer awards score.")]
        [Min(0f)] public float survivalScorePerSecond = 2f;
        [Tooltip("Score points awarded per point of the highest combo reached.")]
        [Min(0f)] public float styleScorePerComboPoint = 10f;

        [Header("Behaviour")]
        [Tooltip("If true, ScoringRuntime sets Time.timeScale = 0 while waiting for an upgrade pick.")]
        public bool pauseOnLevelUp = true;
        [Tooltip("If true, all kills that don't carry an explicit method are treated as Strike kills.")]
        public bool defaultUntaggedKillsToStrike = true;

        public float GetStyleMultiplier(KillMethod method)
        {
            switch (method)
            {
                case KillMethod.Repel: return repelKillMultiplier;
                case KillMethod.WallSlam: return wallSlamMultiplier;
                case KillMethod.Overload: return overloadMultiplier;
                case KillMethod.EnemyRepel: return enemyRepelMultiplier;
                default: return 1f;
            }
        }

        public int GetXpForNextLevel(int currentLevel)
        {
            int lvl = Mathf.Max(1, currentLevel);
            return baseXpPerLevel + (lvl - 1) * xpGrowthPerLevel;
        }
    }
}
