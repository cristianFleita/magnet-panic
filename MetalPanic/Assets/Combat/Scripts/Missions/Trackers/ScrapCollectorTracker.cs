using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Orbit N pieces simultaneously." MagnetismController doesn't fire an
    /// orbit-count event, so we poll its <see cref="MagnetismController.OrbitingCount"/>
    /// once per frame and snapshot the maximum reached during the mission.
    /// </summary>
    public sealed class ScrapCollectorTracker : MissionTrackerBase
    {
        [SerializeField] MagnetismController magnetism;
        public override MissionId Id => MissionId.ScrapCollector;

        void Awake()
        {
            if (magnetism == null)
                magnetism = FindFirstObjectByType<MagnetismController>();
        }

        void Update()
        {
            if (!isTracking || magnetism == null || State == null)
                return;

            int count = magnetism.OrbitingCount;
            if (count > State.Progress)
                SetProgress(count);
        }

        protected override void OnBegin() { }
        protected override void OnEnd() { }
    }
}
