using UnityEngine;

namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Base class for mission progress observers. <see cref="MissionSystem"/>
    /// discovers every tracker in the scene at boot and routes activation to
    /// the one whose <see cref="Id"/> matches the freshly-started mission.
    /// </summary>
    public abstract class MissionTrackerBase : MonoBehaviour
    {
        protected MissionSystem System;
        protected MissionRuntimeState State;
        protected bool isTracking;

        public abstract MissionId Id { get; }

        public void Attach(MissionSystem system)
        {
            System = system;
        }

        public void BeginTracking(MissionRuntimeState state)
        {
            State = state;
            isTracking = true;
            OnBegin();
        }

        public void EndTracking()
        {
            if (!isTracking)
                return;
            isTracking = false;
            OnEnd();
            State = null;
        }

        /// <summary>Bump progress by <paramref name="amount"/> and notify the system.</summary>
        protected void AddProgress(int amount)
        {
            if (!isTracking || State == null || amount <= 0)
                return;

            State.Progress = Mathf.Clamp(State.Progress + amount, 0, int.MaxValue);
            System?.NotifyProgressChanged();

            if (State.Progress >= State.Target)
                System?.CompleteActiveMission();
        }

        /// <summary>Overwrite progress with an absolute value (e.g. current combo).</summary>
        protected void SetProgress(int value)
        {
            if (!isTracking || State == null)
                return;

            int clamped = Mathf.Clamp(value, 0, int.MaxValue);
            if (clamped == State.Progress)
                return;

            State.Progress = clamped;
            System?.NotifyProgressChanged();

            if (State.Progress >= State.Target)
                System?.CompleteActiveMission();
        }

        /// <summary>Subscribe to upstream events. Called when the mission becomes active.</summary>
        protected abstract void OnBegin();

        /// <summary>Unsubscribe from upstream events. Called when the mission ends (any state).</summary>
        protected abstract void OnEnd();
    }
}
