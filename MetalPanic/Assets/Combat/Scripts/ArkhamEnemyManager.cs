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
        [SerializeField, Tooltip("Hard cap on simultaneous attackers. 1 = strict queue (one-at-a-time), 2 = late-game double-team. Keep low for arena combat.")]
        int maxSimultaneousAttackers = 2;
        [SerializeField, Tooltip("Minutes of combat before the second attacker slot opens. Until then the director enforces a strict 1-at-a-time queue so the player can learn the patterns.")]
        float secondAttackerAfterMinutes = 1.5f;
        [SerializeField, Tooltip("Alive enemies required before the director even considers a second attacker (defends against double-teaming a near-empty arena).")]
        int secondAttackerMinAlive = 4;
        [SerializeField, Tooltip("Stagger window between consecutive attackers so their telegraphs don't overlap. (min, max) seconds.")]
        Vector2 attackerStaggerRange = new Vector2(0.25f, 0.55f);
        [SerializeField, Tooltip("Delay reduction per director cycle tick, for ramping pressure over time.")]
        float delayReductionPerMinute = 0.06f;
        [SerializeField, Tooltip("Minimum delay floor to avoid overwhelming the player.")]
        float minDelay = 0.35f;
        [SerializeField, Tooltip("Extra delay applied when the player is mid-combo or low HP (Spider-Man fairness rules).")]
        float fairnessExtraDelay = 0.25f;
        [SerializeField, Tooltip("Player HP fraction below which the director eases off.")]
        [Range(0f, 1f)] float lowHpFairnessThreshold = 0.25f;

        [Header("Engagement Slots")]
        [SerializeField, Tooltip("Maximum enemies allowed inside the close-combat ring around the player. Extras orbit as reserves until one drops out.")]
        int closeEngagementSlots = 3;
        [SerializeField, Tooltip("Radius of the close-combat ring. Enemies within this distance of the player count as 'engaged'.")]
        float closeEngagementRadius = 4.5f;
        [SerializeField, Tooltip("Distance reserve enemies orbit at while waiting for a slot. Should be > closeEngagementRadius.")]
        float reserveOrbitDistance = 6.5f;
        [SerializeField, Tooltip("How often (seconds) to re-rank engaged vs reserve enemies. Cheap, no need to run every frame.")]
        float engagementUpdateInterval = 0.2f;

        Coroutine attackDirectorCoroutine;
        float directorStartTime;
        ArkhamCombatController cachedPlayer;
        readonly List<ArkhamEnemy> engagementBuffer = new List<ArkhamEnemy>(16);
        float nextEngagementUpdate;

        public int CloseEngagementSlots => closeEngagementSlots;
        public float CloseEngagementRadius => closeEngagementRadius;
        public float ReserveOrbitDistance => reserveOrbitDistance;

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

        void Update()
        {
            if (Time.time < nextEngagementUpdate)
                return;

            nextEngagementUpdate = Time.time + Mathf.Max(0.05f, engagementUpdateInterval);
            UpdateEngagementSlots();
        }

        /// <summary>
        /// Spider-Man "circle of combat": rank alive enemies by distance to the
        /// player and only let the closest N stay engaged in melee. The rest are
        /// flagged as reserves and pushed out to <see cref="reserveOrbitDistance"/>
        /// so they orbit instead of stacking on top of the engaged trio. As soon
        /// as one engaged enemy dies, retreats, or is knocked away, the next
        /// closest reserve takes its slot on the next tick.
        ///
        /// Attacking / pre-attacking enemies keep their slot regardless of
        /// distance — interrupting an in-flight charge would feel buggy.
        /// </summary>
        void UpdateEngagementSlots()
        {
            ArkhamCombatController player = ResolvePlayer();
            if (player == null)
                return;

            Vector3 playerPos = player.transform.position;

            engagementBuffer.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy e = enemies[i];
                if (e == null || !e.IsAlive)
                    continue;
                engagementBuffer.Add(e);
            }

            engagementBuffer.Sort((a, b) =>
            {
                float da = (a.transform.position - playerPos).sqrMagnitude;
                float db = (b.transform.position - playerPos).sqrMagnitude;
                return da.CompareTo(db);
            });

            int slotsTaken = 0;
            for (int i = 0; i < engagementBuffer.Count; i++)
            {
                ArkhamEnemy e = engagementBuffer[i];

                // Attackers keep their slot (mid-attack reservation).
                bool isCommitted = e.IsAttacking || e.isPreparingAttack;
                bool wantsSlot = isCommitted || slotsTaken < closeEngagementSlots;

                if (wantsSlot)
                {
                    e.SetForcedKeepDistance(false, 0f);
                    if (!isCommitted)
                        slotsTaken++;
                }
                else
                {
                    e.SetForcedKeepDistance(true, reserveOrbitDistance);
                }
            }
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
        /// True when any enemy that is BOTH counterable AND posing an IMMINENT
        /// threat (in melee range mid-windup, runner mid-dash, shooter firing)
        /// is inside the radius. Used by the player's CounterSenseIndicator so
        /// the magnetic sense only fires when the player actually needs to react.
        /// HeavyBot is excluded by canBeCountered, ranged shooters trigger it
        /// when their projectile is leaving the barrel.
        /// </summary>
        public bool HasCounterTargetInRadius(Vector3 position, float radius)
        {
            float radiusSqr = radius * radius;
            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsImminentCounterThreat)
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
            if (aliveCount <= 0)
                return 0;

            // Default: strict 1-at-a-time queue. The player learns patterns
            // first; only after enough time in the run do we open the second
            // slot. This makes the opening minute readable instead of a brawl.
            int target = 1;

            float minutesElapsed = (Time.time - directorStartTime) / 60f;
            bool secondSlotUnlocked =
                minutesElapsed >= secondAttackerAfterMinutes
                && aliveCount >= secondAttackerMinAlive;

            if (secondSlotUnlocked)
                target = 2;

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

