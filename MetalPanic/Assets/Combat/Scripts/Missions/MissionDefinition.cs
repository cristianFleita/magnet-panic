using UnityEngine;

namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Static configuration for a single mission. Designer-tunable in the
    /// Inspector — the live state (timer, progress, completion) lives in
    /// <see cref="MissionRuntimeState"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Magnet Panic/Mission Definition", fileName = "Mission_NewMission")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public MissionId id = MissionId.Unknown;
        public string displayName = "Mission";
        [TextArea(1, 3)] public string objective = "Do the thing";

        [Header("Timing")]
        [Min(5f)] public float durationSeconds = 35f;

        [Header("Target")]
        [Tooltip("Progress value at which the mission is considered complete (e.g. 5 kills, ×5 combo).")]
        [Min(1)] public int targetCount = 3;

        [Header("Reward")]
        [Min(0)] public int xpReward = 50;
        [Min(0)] public int healReward;
        public bool grantsPowerup;
    }
}
