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
        [SerializeField, Tooltip("Hard cap on simultaneous attackers, Spider-Man style threat-token pool. 3 is the sweet spot for arena-scale combat.")]
        int maxSimultaneousAttackers = 3;
        [SerializeField, Tooltip("Alive enemies required before a second attacker is sent.")]
        int secondAttackerThreshold = 3;
        [SerializeField, Tooltip("Alive enemies required before a third attacker is sent.")]
        int thirdAttackerThreshold = 6;
        [SerializeField, Tooltip("Stagger window between consecutive attackers so their telegraphs don't overlap. (min, max) seconds.")]
        Vector2 attackerStaggerRange = new Vector2(0.18f, 0.45f);
        [SerializeField, Tooltip("Delay reduction per director cycle tick, for ramping pressure over time.")]
        float delayReductionPerMinute = 0.06f;
        [SerializeField, Tooltip("Minimum delay floor to avoid overwhelming the player.")]
        float minDelay = 0.35f;
        [SerializeField, Tooltip("Extra delay applied when the player is mid-combo or low HP (Spider-Man fairness rules).")]
        float fairnessExtraDelay = 0.25f;
        [SerializeField, Tooltip("Player HP fraction below which the director eases off.")]
        [Range(0f, 1f)] float lowHpFairnessThreshold = 0.25f;

        Coroutine attackDirectorCoroutine;
        float directorStartTime;
        ArkhamCombatController cachedPlayer;

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

        /// <summary>
        /// True when any enemy that is BOTH telegraphing an attack AND counterable
        /// (excludes HeavyBot) is inside the radius. Used by the player's
        /// CounterSenseIndicator to drive the "magnetic sense" cue.
        /// </summary>
        public bool HasCounterTargetInRadius(Vector3 position, float radius)
        {
            float radiusSqr = radius * radius;
            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsCounterTarget)
                    continue;

                if ((enemy.transform.position - position).sqrMagnitude <= radiusSqr)
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
                // Honor canBeCountered: HeavyBot windups must be dodged, not parried.
                if (enemy == null || !enemy.IsCounterTarget)
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
            float delay = Mathf.Max(minDelay, baseDelay - reduction);

            // Spider-Man fairness: ease off while the player is comboing or low HP.
            ArkhamCombatController player = ResolvePlayer();
            if (player != null && player.IsAlive)
            {
                if (player.isAttackingEnemy || player.isCountering)
                    delay += fairnessExtraDelay;
                else if (player.Health != null && player.Health.MaxHealth > 0)
                {
                    float hpFrac = (float)player.Health.CurrentHealth / player.Health.MaxHealth;
                    if (hpFrac <= lowHpFairnessThreshold)
                        delay += fairnessExtraDelay * 0.5f;
                }
            }

            return delay;
        }

        int TargetSimultaneousAttackers(int aliveCount)
        {
            int target = 1;
            if (aliveCount >= secondAttackerThreshold)
                target = 2;
            if (aliveCount >= thirdAttackerThreshold)
                target = 3;
            return Mathf.Clamp(target, 1, Mathf.Max(1, maxSimultaneousAttackers));
        }

        ArkhamCombatController ResolvePlayer()
        {
            if (cachedPlayer != null)
                return cachedPlayer;

            cachedPlayer = FindFirstObjectByType<ArkhamCombatController>();
            return cachedPlayer;
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

                int aliveCount = AliveEnemyCount();
                int targetAttackers = TargetSimultaneousAttackers(aliveCount);

                // Primary attacker (biased away from the most recent one for variety)
                ArkhamEnemy primary = RandomAvailableEnemy(previousEnemy);
                if (primary == null)
                    primary = RandomAvailableEnemy(null);
                if (primary == null)
                    continue;

                primary.BeginAttack();
                activeAttackers.Add(primary);

                // Fill up to N simultaneous attackers, with a stagger between each so
                // their windups land at slightly different beats. Spider-Man avoids the
                // "wall of enemies" feeling by spacing telegraphs ~0.2-0.5s apart.
                for (int slot = activeAttackers.Count; slot < targetAttackers; slot++)
                {
                    float stagger = Random.Range(attackerStaggerRange.x, attackerStaggerRange.y);
                    yield return new WaitForSeconds(stagger);

                    ArkhamEnemy next = RandomAvailableEnemyExcluding(activeAttackers);
                    if (next == null || !next.CanDirectorSelect)
                        continue;

                    next.BeginAttack();
                    activeAttackers.Add(next);
                }

                // Wait for all active attackers to finish their attack window.
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

                // Retreat surviving attackers so they reset to idle orbit
                for (int i = 0; i < activeAttackers.Count; i++)
                {
                    ArkhamEnemy a = activeAttackers[i];
                    if (a != null && a.IsAlive)
                        a.BeginRetreat();
                }

                previousEnemy = primary;
            }
        }
    }
}

