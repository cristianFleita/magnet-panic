using System;
using UnityEngine;

namespace MagnetPanic.Combat.Upgrades
{
    [Serializable]
    public sealed class UpgradeData
    {
        public UpgradeId id = UpgradeId.None;
        public UpgradeBranch branch = UpgradeBranch.Magnetism;
        public string displayName = "";
        [TextArea(2, 4)] public string description = "";
        [Min(1)] public int maxStacks = 1;
        public UpgradeId prerequisite = UpgradeId.None;
        public Color accentColor = new Color(0.18f, 0.85f, 1f, 1f);
    }
}
