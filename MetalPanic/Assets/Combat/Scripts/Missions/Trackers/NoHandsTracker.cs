using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Kill N enemies using only repel." Per the GDD (E3), strikes during the
    /// mission don't fail it — they simply don't count. We accept Repel,
    /// EnemyRepel, and WallSlam (which is the consequence of a successful
    /// magnet throw) as "hands-off" kills.
    /// </summary>
    public sealed class NoHandsTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        public override MissionId Id => MissionId.NoHands;

        void Awake()
        {
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();
        }

        protected override void OnBegin()
        {
            if (scoring == null)
                return;
            scoring.OnKillReported.AddListener(HandleKill);
        }

        protected override void OnEnd()
        {
            if (scoring == null)
                return;
            scoring.OnKillReported.RemoveListener(HandleKill);
        }

        void HandleKill(KillContext ctx)
        {
            switch (ctx.Method)
            {
                case KillMethod.Repel:
                case KillMethod.EnemyRepel:
                case KillMethod.WallSlam:
                    AddProgress(1);
                    break;
            }
        }
    }
}
