using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Ranged enemy behavior: the Spitter Drone.
    /// Keeps distance from the player, strafes, and fires metallic projectiles
    /// that can be attracted and repelled back. Extends ArkhamEnemy's base AI
    /// by overriding the attack cycle with a ranged projectile launch.
    ///
    /// Design goals (from GDD §8.4):
    ///   - Fires metallic projectiles
    ///   - Projectiles can be attracted and returned
    ///   - Forces the player to use Pull defensively (orbital scrap blocks shots)
    ///   - Creates dynamic pressure at range while melee enemies push close
    ///
    /// This component works alongside ArkhamEnemy. The ArkhamEnemy handles
    /// the core state machine (strafe/approach/retreat, marks, magnetism, health).
    /// This component hooks into BeginAttack to fire projectiles instead of
    /// melee hits.
    /// </summary>
    public sealed class SpitterDroneBehavior : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ArkhamEnemy enemy;
        [SerializeField] ArkhamCombatController playerCombat;
        [SerializeField] Transform firePoint;

        [Header("Projectile")]
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] float projectileSpeed = 10f;
        [SerializeField] int projectileDamage = 1;
        [SerializeField] int burstCount = 1;
        [SerializeField] float burstInterval = 0.25f;
        [SerializeField] float aimInaccuracy = 5f;

        [Header("Firing Behavior")]
        [SerializeField] float idealDistance = 7f;
        [SerializeField] float idealTolerance = 1.5f;
        [SerializeField] float fireReadyDelay = 0.4f;
        [SerializeField] float postFireRecovery = 0.6f;

        [Header("Events")]
        public UnityEvent<SpitterDroneBehavior> OnFired = new UnityEvent<SpitterDroneBehavior>();

        Coroutine fireRoutine;
        bool isFiring;

        void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<ArkhamEnemy>();

            if (playerCombat == null)
                playerCombat = FindFirstObjectByType<ArkhamCombatController>();

            if (firePoint == null)
            {
                Transform fp = transform.Find("FirePoint");
                if (fp != null)
                    firePoint = fp;
                else
                    firePoint = transform;
            }
        }

        void OnEnable()
        {
            if (enemy == null)
                enemy = GetComponent<ArkhamEnemy>();

            if (playerCombat == null)
                playerCombat = FindFirstObjectByType<ArkhamCombatController>();
        }

        /// <summary>
        /// Called by a patched ArkhamEnemy.BeginAttack or by the Attack Director
        /// when this enemy is selected for attack. The ArkhamEnemy should detect
        /// the SpitterDroneBehavior component and delegate to FireProjectile()
        /// instead of running AttackRoutine.
        /// </summary>
        public void FireProjectile()
        {
            if (isFiring || enemy == null || !enemy.IsAlive)
                return;

            if (fireRoutine != null)
                StopCoroutine(fireRoutine);

            fireRoutine = StartCoroutine(FireRoutine());
        }

        IEnumerator FireRoutine()
        {
            isFiring = true;

            // Show counter cue via the enemy's normal system
            // The enemy should show counter cue during prepare
            yield return new WaitForSeconds(fireReadyDelay);

            if (!enemy.IsAlive || playerCombat == null)
            {
                isFiring = false;
                fireRoutine = null;
                yield break;
            }

            for (int i = 0; i < burstCount; i++)
            {
                if (!enemy.IsAlive || playerCombat == null)
                    break;

                SpawnProjectile();

                if (i < burstCount - 1)
                    yield return new WaitForSeconds(burstInterval);
            }

            OnFired.Invoke(this);

            yield return new WaitForSeconds(postFireRecovery);
            isFiring = false;
            fireRoutine = null;
        }

        void SpawnProjectile()
        {
            if (projectilePrefab == null || playerCombat == null)
                return;

            Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.2f;
            Vector3 target = playerCombat.transform.position + Vector3.up * 0.8f;
            Vector3 direction = (target - origin).normalized;

            // Apply slight inaccuracy for fairness
            if (aimInaccuracy > 0f)
            {
                float yaw = Random.Range(-aimInaccuracy, aimInaccuracy);
                direction = Quaternion.AngleAxis(yaw, Vector3.up) * direction;
            }

            direction.y = 0f;
            direction = direction.normalized;

            GameObject instance = Pool.Spawn(projectilePrefab, origin, Quaternion.LookRotation(direction));
            if (instance == null)
                return;

            EnemyProjectile projectile = instance.GetComponent<EnemyProjectile>();
            if (projectile != null)
                projectile.Fire(direction, playerCombat, projectileSpeed);
        }

        /// <summary>
        /// Returns the ideal distance this drone wants to keep from the player.
        /// Used by ArkhamEnemy's idle movement to position correctly.
        /// </summary>
        public float GetIdealDistance() => idealDistance;
        public float GetIdealTolerance() => idealTolerance;
        public bool IsFiring => isFiring;
    }
}
