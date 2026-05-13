using UnityEngine;

namespace MagnetPanic.Combat
{
    public enum EnemyArchetype
    {
        Scrapling,
        MetalEnemy,
        RunnerBot,
        HeavyBot,
        SpitterDrone,
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

        [Header("Ranged (Spitter Drone)")]
        [Tooltip("When true, this enemy uses ranged attacks via SpitterDroneBehavior instead of melee.")]
        public bool useRangedAttack;
        [Tooltip("Prefab for the projectile fired by ranged enemies.")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 10f;
        public int projectileDamage = 1;
        [Tooltip("Number of projectiles per burst.")]
        public int burstCount = 1;
        [Tooltip("Delay between burst shots.")]
        public float burstInterval = 0.25f;
        [Tooltip("Aim inaccuracy in degrees — keeps ranged enemies fair.")]
        public float aimInaccuracy = 5f;
        [Tooltip("Preferred distance from player for ranged enemies.")]
        public float rangedIdealDistance = 7f;
        [Tooltip("Tolerance around rangedIdealDistance.")]
        public float rangedIdealTolerance = 1.5f;

        [Header("Grappler")]
        [Tooltip("When true, this enemy can grab the player and hold them while allies attack.")]
        public bool canGrapple;
        [Tooltip("Chance (0-1) of attempting grapple when selected by the Attack Director.")]
        [Range(0f, 1f)] public float grappleChance = 0.35f;
        [Tooltip("Number of Q presses required for the player to escape.")]
        public int grappleMashesRequired = 1;
        [Tooltip("Maximum number of hits the grappler can deal before auto-releasing.")]
        public int grappleMaxHits = 2;
        [Tooltip("Maximum hold time before auto-release (seconds).")]
        public float grappleMaxHoldTime = 3.5f;
        [Tooltip("Damage per tick while the player is grappled.")]
        public int grappleDamagePerTick = 1;
        [Tooltip("Seconds between damage ticks during grapple.")]
        public float grappleDamageInterval = 1.2f;
        [Tooltip("Stun duration applied to the grappler when the player escapes.")]
        public float grappleEscapeStunDuration = 0.8f;
    }
}
