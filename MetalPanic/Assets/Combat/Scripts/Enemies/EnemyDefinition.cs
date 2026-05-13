using UnityEngine;

namespace MagnetPanic.Combat
{
    public enum EnemyArchetype
    {
        Scrapling,
        MetalEnemy,
        RunnerBot,
        HeavyBot,
        Custom
    }

    [CreateAssetMenu(menuName = "Magnet Panic/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public EnemyArchetype archetype = EnemyArchetype.Scrapling;
        public string displayName = "Scrapling";

        [Header("Health")]
        public int maxHealth = 3;

        [Header("Magnetism")]
        public bool alwaysPullableByMagnet;
        public int magneticMarksToMagnetize = 2;
        public float magneticMass = 3f;

        [Header("Movement")]
        public float approachSpeed = 5f;
        public float strafeSpeed = 1.25f;
        public float retreatSpeed = 2.25f;
        public float retreatDistance = 4.25f;
        [Tooltip("When true the enemy never strafes — only approaches or retreats.")]
        public bool disableStrafe;

        [Header("Attack")]
        public float prepareAttackTime = 0.35f;
        public float attackRange = 1.8f;
        public float attackHitDelay = 0.2f;
        public float attackRecovery = 0.55f;
        public float knockbackDistance = 0.55f;
        public float knockbackDuration = 0.16f;

        [Header("Linear charge (Runner Bot)")]
        [Tooltip("When true, the attack routine locks direction at prep end and dashes in a straight line.")]
        public bool useLinearCharge;
        public float chargeSpeed = 11f;
        public float chargeDuration = 0.55f;
        public float chargeHitRadius = 1.1f;
        [Tooltip("Damage dealt when the linear charge connects.")]
        public int chargeDamage = 2;
        [Tooltip("When true, the charge knocks the player down (Hit_Knockback state).")]
        public bool chargeCausesKnockdown = true;
        [Tooltip("Ideal idle distance from the player for linear-charge enemies (sweet spot for a tackle).")]
        public float chargeIdealDistance = 5f;
        [Tooltip("Tolerance around chargeIdealDistance before the bot decides to approach or retreat.")]
        public float chargeIdealTolerance = 1.2f;
    }
}
