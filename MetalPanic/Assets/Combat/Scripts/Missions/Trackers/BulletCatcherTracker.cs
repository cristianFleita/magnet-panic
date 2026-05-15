using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Attract N enemy projectiles into orbit." Magnetism's
    /// <see cref="MagnetismController.OnObjectOrbited"/> fires whenever any
    /// magnetic object enters orbit. We filter to objects that also carry an
    /// <see cref="EnemyProjectile"/> component.
    /// </summary>
    public sealed class BulletCatcherTracker : MissionTrackerBase
    {
        [SerializeField] MagnetismController magnetism;

        public override MissionId Id => MissionId.BulletCatcher;

        void Awake()
        {
            if (magnetism == null)
                magnetism = FindFirstObjectByType<MagnetismController>();
        }

        protected override void OnBegin()
        {
            if (magnetism != null)
                magnetism.OnObjectOrbited.AddListener(HandleOrbited);
        }

        protected override void OnEnd()
        {
            if (magnetism != null)
                magnetism.OnObjectOrbited.RemoveListener(HandleOrbited);
        }

        void HandleOrbited(MagneticObject obj)
        {
            if (obj == null)
                return;
            if (obj.GetComponent<EnemyProjectile>() == null)
                return;
            AddProgress(1);
        }
    }
}
