using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    public sealed class CounterstormTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        public override MissionId Id => MissionId.Counterstorm;

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
            scoring.OnCounterAwarded.AddListener(HandleCounter);
        }

        protected override void OnEnd()
        {
            if (scoring == null)
                return;
            scoring.OnCounterAwarded.RemoveListener(HandleCounter);
        }

        void HandleCounter(ArkhamEnemy _)
        {
            AddProgress(1);
        }
    }
}
