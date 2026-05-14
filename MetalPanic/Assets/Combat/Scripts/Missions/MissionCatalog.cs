using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Missions
{
    /// <summary>
    /// Ordered pool of missions the <see cref="MissionSystem"/> picks from.
    /// Future "difficulty by act" tuning can either add per-act sublists or
    /// tag definitions with metadata — keep this MVP flat.
    /// </summary>
    [CreateAssetMenu(menuName = "Magnet Panic/Mission Catalog", fileName = "MissionCatalog")]
    public sealed class MissionCatalog : ScriptableObject
    {
        [SerializeField] List<MissionDefinition> missions = new List<MissionDefinition>();

        public int Count => missions != null ? missions.Count : 0;
        public IReadOnlyList<MissionDefinition> All => missions;

        public MissionDefinition PickRandom(MissionDefinition exclude)
        {
            if (missions == null || missions.Count == 0)
                return null;
            if (missions.Count == 1)
                return missions[0];

            int attempts = 0;
            while (attempts++ < 8)
            {
                MissionDefinition pick = missions[Random.Range(0, missions.Count)];
                if (pick != null && pick != exclude)
                    return pick;
            }

            // Fallback: linear scan for the first non-excluded entry.
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i] != null && missions[i] != exclude)
                    return missions[i];
            }

            return missions[0];
        }
    }
}
