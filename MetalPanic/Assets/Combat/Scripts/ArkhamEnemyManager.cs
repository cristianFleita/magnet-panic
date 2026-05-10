using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    public sealed class ArkhamEnemyManager : MonoBehaviour
    {
        readonly List<ArkhamEnemy> enemies = new List<ArkhamEnemy>();

        [Header("Attack Director")]
        [SerializeField] bool startAttackDirectorOnPlay = true;
        [SerializeField] Vector2 attackDelayRange = new Vector2(0.65f, 1.5f);

        Coroutine attackDirectorCoroutine;

        public IReadOnlyList<ArkhamEnemy> Enemies => enemies;

        void Awake()
        {
            ArkhamEnemy[] childEnemies = GetComponentsInChildren<ArkhamEnemy>(true);
            for (int i = 0; i < childEnemies.Length; i++)
                Register(childEnemies[i]);
        }

        void OnEnable()
        {
            if (startAttackDirectorOnPlay)
                StartAttackDirector();
        }

        void OnDisable()
        {
            if (attackDirectorCoroutine != null)
                StopCoroutine(attackDirectorCoroutine);

            attackDirectorCoroutine = null;
        }

        public void Register(ArkhamEnemy enemy)
        {
            if (enemy == null || enemies.Contains(enemy))
                return;

            enemies.Add(enemy);
            enemy.SetManager(this);
        }

        public void Unregister(ArkhamEnemy enemy)
        {
            enemies.Remove(enemy);
        }

        public void StartAttackDirector()
        {
            if (attackDirectorCoroutine != null)
                StopCoroutine(attackDirectorCoroutine);

            attackDirectorCoroutine = StartCoroutine(AttackDirector());
        }

        public int AliveEnemyCount()
        {
            int count = 0;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i] == null)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                if (enemies[i].IsAlive)
                    count++;
            }

            return count;
        }

        public bool AnyEnemyIsPreparingAttack()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsCounterable)
                    return true;
            }

            return false;
        }

        public ArkhamEnemy ClosestCounterableEnemy(Vector3 position, float maxDistance = float.PositiveInfinity)
        {
            ArkhamEnemy closest = null;
            float closestDistance = float.PositiveInfinity;
            float maxDistanceSqr = maxDistance * maxDistance;

            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsCounterable)
                    continue;

                float distance = Vector3.SqrMagnitude(enemy.transform.position - position);
                if (distance > maxDistanceSqr)
                    continue;

                if (distance < closestDistance)
                {
                    closest = enemy;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        ArkhamEnemy RandomAvailableEnemy(ArkhamEnemy excluded)
        {
            List<ArkhamEnemy> available = new List<ArkhamEnemy>();

            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy != null && enemy != excluded && enemy.CanDirectorSelect)
                    available.Add(enemy);
            }

            if (available.Count == 0)
                return null;

            return available[Random.Range(0, available.Count)];
        }

        IEnumerator AttackDirector()
        {
            ArkhamEnemy previousEnemy = null;
            WaitForSeconds retryDelay = new WaitForSeconds(0.25f);

            while (enabled)
            {
                if (AliveEnemyCount() == 0)
                {
                    yield return retryDelay;
                    continue;
                }

                yield return new WaitForSeconds(Random.Range(attackDelayRange.x, attackDelayRange.y));

                ArkhamEnemy attackingEnemy = RandomAvailableEnemy(previousEnemy);
                if (attackingEnemy == null)
                    attackingEnemy = RandomAvailableEnemy(null);

                if (attackingEnemy == null)
                    continue;

                attackingEnemy.BeginAttack();

                yield return new WaitUntil(() =>
                    attackingEnemy == null ||
                    !attackingEnemy.IsAlive ||
                    (!attackingEnemy.IsCounterable && !attackingEnemy.IsAttacking));

                if (attackingEnemy != null && attackingEnemy.IsAlive)
                    attackingEnemy.BeginRetreat();

                previousEnemy = attackingEnemy;
            }
        }
    }
}
