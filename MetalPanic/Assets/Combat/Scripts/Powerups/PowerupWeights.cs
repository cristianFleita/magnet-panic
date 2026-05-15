using System;
using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Per-mission weighted distribution for which powerup is granted on
    /// completion. Stored on the <see cref="Missions.MissionDefinition"/> and
    /// passed verbatim into the broker. Weights are normalized at roll time —
    /// they don't need to sum to 1.
    /// </summary>
    [Serializable]
    public struct PowerupWeights
    {
        [Min(0f)] public float slowTime;
        [Min(0f)] public float overloadPulse;
        [Min(0f)] public float magneticMine;

        public PowerupWeights(float slow, float pulse, float mine)
        {
            slowTime = Mathf.Max(0f, slow);
            overloadPulse = Mathf.Max(0f, pulse);
            magneticMine = Mathf.Max(0f, mine);
        }

        public static PowerupWeights Uniform => new PowerupWeights(1f, 1f, 1f);

        public float Total => Mathf.Max(0f, slowTime) + Mathf.Max(0f, overloadPulse) + Mathf.Max(0f, magneticMine);

        /// <summary>
        /// Returns a powerup chosen by weighted roll. Falls back to uniform when
        /// every weight is zero so a mission with no table still rewards.
        /// </summary>
        public PowerupId Roll()
        {
            float total = Total;
            if (total <= 0f)
                return UniformPick();

            float roll = UnityEngine.Random.value * total;
            float acc = slowTime;
            if (roll < acc)
                return PowerupId.SlowTime;
            acc += overloadPulse;
            if (roll < acc)
                return PowerupId.OverloadPulse;
            return PowerupId.MagneticMine;
        }

        static PowerupId UniformPick()
        {
            int pick = UnityEngine.Random.Range(0, 3);
            return pick switch
            {
                0 => PowerupId.SlowTime,
                1 => PowerupId.OverloadPulse,
                _ => PowerupId.MagneticMine,
            };
        }
    }
}
