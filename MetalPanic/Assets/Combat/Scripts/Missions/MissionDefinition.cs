using MagnetPanic.Combat.Powerups;
using UnityEngine;

namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Static configuration for a single mission. Designer-tunable in the
    /// Inspector — the live state (timer, progress, completion) lives in
    /// <see cref="MissionRuntimeState"/>.
    ///
    /// Each mission carries its own weighted distribution for which powerup
    /// the player gets on completion (see GDD §Mission Regla 4). The
    /// distribution is normalised at roll time, so tweaking it in the editor
    /// doesn't require summing to 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Magnet Panic/Mission Definition", fileName = "Mission_NewMission")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public MissionId id = MissionId.Unknown;
        public string displayName = "Mission";
        [TextArea(1, 3)] public string objective = "Do the thing";

        [Header("Difficulty Tier")]
        [Tooltip("1 = Fundamentals (Act 1+). 2 = Mastery (Act 2+). 3 = Style (Act 3+).")]
        [Range(1, 3)] public int tier = 1;

        [Tooltip("Mission only enters the pool if this condition matches the run state. Most missions can stay Always.")]
        public MissionGate gate = MissionGate.Always;

        [Header("Timing")]
        [Min(5f)] public float durationSeconds = 35f;

        [Header("Target")]
        [Tooltip("Progress value at which the mission is considered complete (e.g. 5 kills, ×5 combo).")]
        [Min(1)] public int targetCount = 3;

        [Header("Reward")]
        [Min(0)] public int xpReward = 50;
        [Min(0)] public int healReward;

        [Header("Powerup Reward")]
        [Tooltip("If true, completing this mission triggers a weighted powerup roll using the values below.")]
        public bool grantsPowerup = true;

        [Tooltip("Relative weights for the powerup roll. Don't need to sum to 1 — they're normalised at roll time. Zero in every field falls back to a uniform pick.")]
        public PowerupWeights powerupWeights = PowerupWeights.Uniform;
    }

    /// <summary>
    /// Soft availability gate for missions that depend on enemy types or run
    /// progression beyond plain tier order. The catalog filters the pool by
    /// this flag before each pick.
    /// </summary>
    public enum MissionGate
    {
        Always = 0,
        /// <summary>Needs a Spitter Drone reachable in the arena (Act 3+ contents).</summary>
        RequiresSpitterDrone = 1,
        /// <summary>Needs the player to be in / capable of overload.</summary>
        RequiresOverloadAccess = 2,
    }
}
