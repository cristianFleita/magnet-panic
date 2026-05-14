using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    public sealed class WallSlamTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        public override MissionId Id => MissionId.WallSlam;

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
            if (ctx.Method == KillMethod.WallSlam)
                AddProgress(1);
        }
    }
}
