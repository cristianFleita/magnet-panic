using System;

namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// Pluggable contract between <see cref="ScoringRuntime"/> and the (future)
    /// upgrade picker. ScoringRuntime stays oblivious to how upgrades are
    /// presented: it calls <see cref="RequestUpgradeOffer"/>, hands over a
    /// completion callback, and waits for the broker to invoke it.
    /// </summary>
    public interface ILevelUpUpgradeBroker
    {
        /// <summary>
        /// Called when the player levels up. The broker should present its
        /// upgrade UI and then invoke <paramref name="onUpgradeResolved"/>
        /// exactly once. ScoringRuntime resumes gameplay on that call.
        /// </summary>
        void RequestUpgradeOffer(int newLevel, Action onUpgradeResolved);
    }
}
