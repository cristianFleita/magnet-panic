using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Upgrades
{
    [CreateAssetMenu(menuName = "Magnet Panic/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        public List<UpgradeData> upgrades = new List<UpgradeData>();

        public UpgradeData Find(UpgradeId id)
        {
            if (id == UpgradeId.None || upgrades == null)
                return null;

            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] != null && upgrades[i].id == id)
                    return upgrades[i];
            }
            return null;
        }
    }
}
