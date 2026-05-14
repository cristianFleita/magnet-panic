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
        [SerializeField, Tooltip("When alive enemies >= this threshold, the director may send a second simultaneous attacker.")]
        int simultaneousAttackThreshold = 5;
        [SerializeField, Tooltip("Delay reduction per director cycle tick, for ramping pressure over time.")]
        float delayReductionPerMinute = 0.06f;
        [SerializeField, Tooltip("Minimum delay floor to avoid overwhelming the player.")]
        float minDelay = 0.35f;

        Coroutine attackDirectorCoroutine;
        float directorStartTime;

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

            directorStartTime = Time.time;
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

            ArkhamCombatController player = FindFirstObjectByType<ArkhamCombatController>();
            if (player != null)
            {
                available.Sort((a, b) => 
                {
                    float distA = (a.transform.position - player.transform.position).sqrMagnitude;
                    float distB = (b.transform.position - player.transform.position).sqrMagnitude;
                    return distA.CompareTo(distB);
                });
            }

            // Bias towards the closest available enemies
            int maxIndex = Mathf.Min(available.Count, Mathf.Max(2, available.Count / 2));
            return available[Random.Range(0, maxIndex)];
        }

        ArkhamEnemy RandomAvailableEnemyExcluding(List<ArkhamEnemy> excluded)
        {
            List<ArkhamEnemy> available = new List<ArkhamEnemy>();

            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy != null && enemy.CanDirectorSelect && !excluded.Contains(enemy))
                    available.Add(enemy);
            }

            if (available.Count == 0)
                return null;

            ArkhamCombatController player = FindFirstObjectByType<ArkhamCombatController>();
            if (player != null)
            {
                available.Sort((a, b) => 
                {
                    float distA = (a.transform.position - player.transform.position).sqrMagnitude;
                    float distB = (b.transform.position - player.transform.position).sqrMagnitude;
                    return distA.CompareTo(distB);
                });
            }

            // Bias towards the closest available enemies
            int maxIndex = Mathf.Min(available.Count, Mathf.Max(2, available.Count / 2));
            return available[Random.Range(0, maxIndex)];
        }

        float GetScaledDelay()
        {
            float elapsed = Time.time - directorStartTime;
            float minutesElapsed = elapsed / 60f;
            float baseDelay = Random.Range(attackDelayRange.x, attackDelayRange.y);
            float reduction = minutesElapsed * delayReductionPerMinute;
            return Mathf.Max(minDelay, baseDelay - reduction);
        }

        IEnumerator AttackDirector()
        {
            ArkhamEnemy previousEnemy = null;
            WaitForSeconds retryDelay = new WaitForSeconds(0.25f);
            List<ArkhamEnemy> activeAttackers = new List<ArkhamEnemy>(4);

            while (enabled)
            {
                if (AliveEnemyCount() == 0)
                {
                    yield return retryDelay;
                    continue;
                }

                yield return new WaitForSeconds(GetScaledDelay());

                activeAttackers.Clear();

                // Primary attacker
                ArkhamEnemy attackingEnemy = RandomAvailableEnemy(previousEnemy);
                if (attackingEnemy == null)
                    attackingEnemy = RandomAvailableEnemy(null);

                if (attackingEnemy == null)
                    continue;

                attackingEnemy.BeginAttack();
                activeAttackers.Add(attackingEnemy);

                        // Simultaneous second attacker at higher enemy counts (normal pressure)
                int aliveCount = AliveEnemyCount();
                if (aliveCount >= simultaneousAttackThreshold)
                {
                    ArkhamEnemy secondAttacker = RandomAvailableEnemyExcluding(activeAttackers);
                    if (secondAttacker != null)
                    {
                        // Small stagger so both attacks don't land simultaneously
                        yield return new WaitForSeconds(Random.Range(0.2f, 0.45f));
                        if (secondAttacker.CanDirectorSelect)
                        {
                            secondAttacker.BeginAttack();
                            activeAttackers.Add(secondAttacker);
                        }
                    }
                }

                // Wait for all active attackers to finish
                yield return new WaitUntil(() =>
                {
                    for (int i = activeAttackers.Count - 1; i >= 0; i--)
                    {
                        ArkhamEnemy a = activeAttackers[i];
                        if (a == null || !a.IsAlive)
                        {
                            activeAttackers.RemoveAt(i);
                            continue;
                        }
                        if (a.IsCounterable || a.IsAttacking)
                            return false;
                    }
                    return true;
                });

                // Retreat surviving attackers
                for (int i = 0; i < activeAttackers.Count; i++)
                {
                    ArkhamEnemy a = activeAttackers[i];
                    if (a != null && a.IsAlive)
                        a.BeginRetreat();
                }

                previousEnemy = attackingEnemy;
            }
        }
    }
}

