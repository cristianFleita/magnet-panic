using MagnetPanic.Combat.Powerups;

namespace MagnetPanic.Combat.Missions
{
    public enum MissionState
    {
        Idle,
        Cooldown,
        Active,
        Completing,
        Expired,
    }

    /// <summary>
    /// Live state for the currently-active mission. The system mutates this
    /// instance in-place to keep allocations off the hot path; observers
    /// (trackers, HUD) read it through <see cref="MissionSystem.Current"/>.
    /// </summary>
    public sealed class MissionRuntimeState
    {
        public MissionDefinition Definition;
        public int Progress;
        public float TimeRemaining;
        public MissionState State;
        /// <summary>
        /// Powerup rolled at mission start. Surfaced in the HUD so the player
        /// knows what's on the line before they finish the objective. Granted
        /// verbatim on completion — re-rolling at completion is intentionally
        /// avoided (no surprise rewards).
        /// </summary>
        public PowerupId RewardPowerup;

        public int Target => Definition != null ? Definition.targetCount : 0;
        public float Duration => Definition != null ? Definition.durationSeconds : 0f;
        public float Progress01
        {
            get
            {
                if (Definition == null || Definition.targetCount <= 0)
                    return 0f;
                return UnityEngine.Mathf.Clamp01(Progress / (float)Definition.targetCount);
            }
        }
        public float TimeRemaining01
        {
            get
            {
                if (Definition == null || Definition.durationSeconds <= 0f)
                    return 0f;
                return UnityEngine.Mathf.Clamp01(TimeRemaining / Definition.durationSeconds);
            }
        }

        public bool IsActive => State == MissionState.Active;
        public bool IsComplete => Progress >= Target && Target > 0;

        public void Reset()
        {
            Definition = null;
            Progress = 0;
            TimeRemaining = 0f;
            State = MissionState.Idle;
            RewardPowerup = PowerupId.None;
        }
    }
}
