using UnityEngine;

namespace MagnetPanic.Combat.Missions.Trackers
{
    /// <summary>
    /// "Reach critical overload and release the explosion (without dying)."
    /// Listens to <see cref="OverloadController.OnOverloadExploded"/>. Death
    /// during the post-explosion grace window is handled upstream by
    /// <see cref="MissionSystem"/> (mission aborts on player death — E2).
    /// </summary>
    public sealed class OverloadArtistTracker : MissionTrackerBase
    {
        [SerializeField] OverloadController overload;
        public override MissionId Id => MissionId.OverloadArtist;

        void Awake()
        {
            if (overload == null)
                overload = FindFirstObjectByType<OverloadController>();
        }

        protected override void OnBegin()
        {
            if (overload != null)
                overload.OnOverloadExploded.AddListener(HandleExplosion);
        }

        protected override void OnEnd()
        {
            if (overload != null)
                overload.OnOverloadExploded.RemoveListener(HandleExplosion);
        }

        void HandleExplosion(Vector3 origin, float radius, int hits)
        {
            AddProgress(1);
        }
    }
}
