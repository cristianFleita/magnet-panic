using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    public sealed class ArkhamTargetScanner : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] float maximumDistance = 13f;
        [SerializeField] float directionalAngle = 70f;
        [SerializeField] float nearestTargetWeight = 0.35f;

        public float MaximumDistance => maximumDistance;

        public ArkhamEnemy FindTarget(
            ArkhamEnemyManager manager,
            Vector3 origin,
            Vector3 preferredDirection,
            ArkhamEnemy excluded = null)
        {
            if (manager == null)
                return null;

            IReadOnlyList<ArkhamEnemy> enemies = manager.Enemies;
            ArkhamEnemy best = null;
            float bestScore = float.NegativeInfinity;
            bool hasDirection = preferredDirection.sqrMagnitude > 0.05f;

            for (int i = 0; i < enemies.Count; i++)
            {
                ArkhamEnemy enemy = enemies[i];
                if (enemy == null || enemy == excluded || !enemy.IsAttackable)
                    continue;

                Vector3 toEnemy = enemy.transform.position - origin;
                toEnemy.y = 0f;

                float distance = toEnemy.magnitude;
                if (distance > maximumDistance || distance <= 0.01f)
                    continue;

                float score = -distance * nearestTargetWeight;

                if (hasDirection)
                {
                    float angle = Vector3.Angle(preferredDirection, toEnemy);
                    if (angle > directionalAngle)
                        continue;

                    score += 1f - (angle / directionalAngle);
                }

                if (score > bestScore)
                {
                    best = enemy;
                    bestScore = score;
                }
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, maximumDistance);
        }
    }
}
