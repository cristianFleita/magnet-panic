using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Use a magnetized enemy as a projectile and kill another enemy with it."
    /// <see cref="KillMethod.EnemyRepel"/> is awarded exactly when one enemy's
    /// magnet-repel projectile fatally impacts another, so we just count those.
    /// </summary>
    public sealed class LivingAmmoTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        public override MissionId Id => MissionId.LivingAmmo;

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
            if (ctx.Method == KillMethod.EnemyRepel)
                AddProgress(1);
        }
    }
}
