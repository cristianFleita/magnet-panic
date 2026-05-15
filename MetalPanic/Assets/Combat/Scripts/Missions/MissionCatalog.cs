using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Pool of mission definitions the <see cref="MissionSystem"/> picks from.
    /// Selection is tier-gated by the current act and (Act 3+) tier-weighted
    /// to bias the player toward high-tier challenges late in a run.
    /// </summary>
    [CreateAssetMenu(menuName = "Magnet Panic/Mission Catalog", fileName = "MissionCatalog")]
    public sealed class MissionCatalog : ScriptableObject
    {
        [SerializeField] List<MissionDefinition> missions = new List<MissionDefinition>();

        readonly List<MissionDefinition> filterBuffer = new List<MissionDefinition>(16);

        public int Count => missions != null ? missions.Count : 0;
        public IReadOnlyList<MissionDefinition> All => missions;

        /// <summary>
        /// Pick the next mission excluding the most recent and any mission whose
        /// tier exceeds <paramref name="maxTier"/> or whose gate isn't satisfied.
        ///
        /// When <paramref name="useTierBias"/> is true (Act 3+ per GDD Regla 5)
        /// each definition is weighted by its <see cref="MissionDefinition.tier"/>
        /// so tier-3 missions appear ~3× as often as tier-1.
        /// </summary>
        public MissionDefinition PickNext(
            MissionDefinition exclude,
            int maxTier,
            bool useTierBias,
            IMissionGateResolver gateResolver)
        {
            if (missions == null || missions.Count == 0)
                return null;

            filterBuffer.Clear();
            for (int i = 0; i < missions.Count; i++)
            {
                MissionDefinition def = missions[i];
                if (def == null)
                    continue;
                if (def == exclude && missions.Count > 1)
                    continue;
                if (def.tier > maxTier)
                    continue;
                if (gateResolver != null && !gateResolver.IsGateSatisfied(def.gate))
                    continue;
                filterBuffer.Add(def);
            }

            if (filterBuffer.Count == 0)
            {
                // Fallback: relax exclude if nothing else fits.
                for (int i = 0; i < missions.Count; i++)
                {
                    MissionDefinition def = missions[i];
                    if (def == null)
                        continue;
                    if (def.tier > maxTier)
                        continue;
                    if (gateResolver != null && !gateResolver.IsGateSatisfied(def.gate))
                        continue;
                    filterBuffer.Add(def);
                }
            }

            if (filterBuffer.Count == 0)
                return null;
            if (filterBuffer.Count == 1)
                return filterBuffer[0];

            if (!useTierBias)
                return filterBuffer[Random.Range(0, filterBuffer.Count)];

            int totalWeight = 0;
            for (int i = 0; i < filterBuffer.Count; i++)
                totalWeight += Mathf.Max(1, filterBuffer[i].tier);

            int roll = Random.Range(0, totalWeight);
            int acc = 0;
            for (int i = 0; i < filterBuffer.Count; i++)
            {
                acc += Mathf.Max(1, filterBuffer[i].tier);
                if (roll < acc)
                    return filterBuffer[i];
            }

            return filterBuffer[filterBuffer.Count - 1];
        }
    }

    /// <summary>
    /// Implemented by the mission system itself so the catalog can ask whether
    /// a contextual gate (e.g. spitter drone available) is satisfied without
    /// importing scene-level dependencies.
    /// </summary>
    public interface IMissionGateResolver
    {
        bool IsGateSatisfied(MissionGate gate);
    }
}
