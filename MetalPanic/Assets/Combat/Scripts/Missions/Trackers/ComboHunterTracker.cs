using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Reach combo ×N." Snapshots the live combo from
    /// <see cref="ScoringRuntime"/> and never decreases — we want the player
    /// to be credited for the highest combo achieved during the mission, not
    /// the current one.
    /// </summary>
    public sealed class ComboHunterTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        public override MissionId Id => MissionId.ComboHunter;

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
            scoring.OnComboChanged.AddListener(HandleCombo);
            // Seed with current combo so the player isn't penalised for an
            // already-running streak.
            HandleCombo(scoring.ComboCount, scoring.ComboTimeRemaining);
        }

        protected override void OnEnd()
        {
            if (scoring == null)
                return;
            scoring.OnComboChanged.RemoveListener(HandleCombo);
        }

        void HandleCombo(int count, float _)
        {
            if (State == null)
                return;
            if (count > State.Progress)
                SetProgress(count);
        }
    }
}
