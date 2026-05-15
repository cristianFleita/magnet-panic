using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Multi-kill ≥N with a single magnetized-enemy projectile."
    ///
    /// Implementation: when an <see cref="KillMethod.EnemyRepel"/> kill lands we
    /// open a short window (<see cref="windowSeconds"/>). Any further EnemyRepel
    /// kills inside that window are credited to the same chain. We never
    /// AddProgress(1) — we wait for the window to close and report the chain
    /// size only if it meets the mission target (so a sequence of unrelated
    /// 1-kill chains doesn't trivially complete the mission).
    /// </summary>
    public sealed class ChainReactionTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        [SerializeField, Min(0.1f)] float windowSeconds = 0.6f;

        int chainKills;
        float windowExpires;
        bool windowOpen;

        public override MissionId Id => MissionId.ChainReaction;

        void Awake()
        {
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();
        }

        protected override void OnBegin()
        {
            chainKills = 0;
            windowOpen = false;
            if (scoring != null)
                scoring.OnKillReported.AddListener(HandleKill);
        }

        protected override void OnEnd()
        {
            if (scoring != null)
                scoring.OnKillReported.RemoveListener(HandleKill);
        }

        void Update()
        {
            if (!isTracking || !windowOpen || State == null)
                return;
            if (Time.time < windowExpires)
                return;

            // Window closed — credit the chain.
            int target = State.Target;
            if (chainKills >= target)
                SetProgress(target);

            chainKills = 0;
            windowOpen = false;
        }

        void HandleKill(KillContext ctx)
        {
            if (ctx.Method != KillMethod.EnemyRepel)
                return;

            if (!windowOpen)
            {
                windowOpen = true;
                chainKills = 1;
            }
            else
            {
                chainKills++;
            }
            windowExpires = Time.time + windowSeconds;
        }
    }
}
