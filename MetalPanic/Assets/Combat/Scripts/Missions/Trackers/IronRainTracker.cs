using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Kill N enemies with repelled scrap." Strikes do not count; magnetized
    /// enemy projectiles (EnemyRepel) and wall slams do not count either —
    /// only the direct scrap repel.
    /// </summary>
    public sealed class IronRainTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        public override MissionId Id => MissionId.IronRain;

        void Awake()
        {
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();
        }

        protected override void OnBegin()
        {
            if (scoring != null)
                scoring.OnKillReported.AddListener(HandleKill);
        }

        protected override void OnEnd()
        {
            if (scoring != null)
                scoring.OnKillReported.RemoveListener(HandleKill);
        }

        void HandleKill(KillContext ctx)
        {
            if (ctx.Method == KillMethod.Repel)
                AddProgress(1);
        }
    }
}
