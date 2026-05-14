using UnityEngine;

namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// Per-kill payload produced by combat systems and consumed by
    /// <see cref="ScoringRuntime"/>. Designed for value-type pass-by-copy on
    /// hot paths — no allocations.
    /// </summary>
    public readonly struct KillContext
    {
        public readonly ArkhamEnemy Enemy;
        public readonly KillMethod Method;
        public readonly bool IsBoss;
        public readonly Vector3 Position;

        public KillContext(ArkhamEnemy enemy, KillMethod method, bool isBoss, Vector3 position)
        {
            Enemy = enemy;
            Method = method;
            IsBoss = isBoss;
            Position = position;
        }
    }

    /// <summary>
    /// Resolved XP/score event emitted after <see cref="ScoringRuntime"/>
    /// applies multipliers and combo math. Consumed by HUD popups and
    /// downstream systems (missions, achievements).
    /// </summary>
    public readonly struct XpAwardEvent
    {
        public readonly KillMethod Method;
        public readonly int RawXp;
        public readonly int FinalXp;
        public readonly int ComboCount;
        public readonly float StyleMultiplier;
        public readonly float ComboMultiplier;
        public readonly Vector3 Position;

        public XpAwardEvent(KillMethod method, int rawXp, int finalXp, int comboCount, float styleMultiplier, float comboMultiplier, Vector3 position)
        {
            Method = method;
            RawXp = rawXp;
            FinalXp = finalXp;
            ComboCount = comboCount;
            StyleMultiplier = styleMultiplier;
            ComboMultiplier = comboMultiplier;
            Position = position;
        }
    }
}
