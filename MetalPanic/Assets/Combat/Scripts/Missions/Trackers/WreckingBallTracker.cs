using MagnetPanic.Combat.Scoring;
using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Kill N enemies with a single repel action." Reading `OnRepelFired`
    /// gives us the timestamp of each repel; we then count Repel-method kills
    /// landing inside the next short window and credit the chain if it meets
    /// the mission target.
    /// </summary>
    public sealed class WreckingBallTracker : MissionTrackerBase
    {
        [SerializeField] ScoringRuntime scoring;
        [SerializeField] MagnetismController magnetism;
        [SerializeField, Min(0.1f)] float windowSeconds = 0.55f;

        int killsThisRepel;
        float windowExpires;
        bool windowOpen;

        public override MissionId Id => MissionId.WreckingBall;

        void Awake()
        {
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();
            if (magnetism == null)
                magnetism = FindFirstObjectByType<MagnetismController>();
        }

        protected override void OnBegin()
        {
            killsThisRepel = 0;
            windowOpen = false;
            if (scoring != null)
                scoring.OnKillReported.AddListener(HandleKill);
            if (magnetism != null)
                magnetism.OnRepelFired.AddListener(HandleRepelFired);
        }

        protected override void OnEnd()
        {
            if (scoring != null)
                scoring.OnKillReported.RemoveListener(HandleKill);
            if (magnetism != null)
                magnetism.OnRepelFired.RemoveListener(HandleRepelFired);
        }

        void Update()
        {
            if (!isTracking || !windowOpen || State == null)
                return;
            if (Time.time < windowExpires)
                return;

            int target = State.Target;
            if (killsThisRepel >= target)
                SetProgress(target);

            killsThisRepel = 0;
            windowOpen = false;
        }

        void HandleRepelFired(bool emptyRepel)
        {
            if (emptyRepel)
                return;
            windowOpen = true;
            killsThisRepel = 0;
            windowExpires = Time.time + windowSeconds;
        }

        void HandleKill(KillContext ctx)
        {
            if (!windowOpen)
                return;
            if (ctx.Method == KillMethod.Repel || ctx.Method == KillMethod.WallSlam)
                killsThisRepel++;
        }
    }
}
