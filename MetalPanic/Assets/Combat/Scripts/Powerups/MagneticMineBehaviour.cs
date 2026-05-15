using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Runtime-spawned mine entity. The mine prefab owns its own SphereCollider
    /// — the collider's radius doubles as both the trigger range and the AoE
    /// damage radius (an enemy that touches the trigger is, by definition,
    /// already inside the explosion).
    ///
    /// Lifecycle: spawn → arm (delay) → wait for any live enemy to enter → on
    /// enter, instantiate the explosion VFX prefab at the mine position and
    /// damage / magnetize every enemy still inside the radius, then destroy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class MagneticMineBehaviour : MonoBehaviour
    {
        int damage = 4;
        int magnetizeMarks = 2;
        float armTime = 0.5f;
        float timeout = 30f;
        GameObject explosionVfxPrefab;

        SphereCollider triggerCollider;
        float lifeAge;
        bool armed;
        bool detonated;
        ArkhamEnemyManager enemyManager;

        public bool Detonated => detonated;
        public float AoeRadius => triggerCollider != null ? triggerCollider.radius * MaxLossyScale() : 0f;

        public void Configure(
            ArkhamEnemyManager enemyManagerRef,
            int damageValue,
            int marksValue,
            float armTimeValue,
            float timeoutValue,
            GameObject explosionVfxPrefabRef)
        {
            enemyManager = enemyManagerRef;
            damage = Mathf.Max(0, damageValue);
            magnetizeMarks = Mathf.Max(1, marksValue);
            armTime = Mathf.Max(0f, armTimeValue);
            timeout = Mathf.Max(1f, timeoutValue);
            explosionVfxPrefab = explosionVfxPrefabRef;

            triggerCollider = GetComponent<SphereCollider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
                triggerCollider.enabled = false; // armed in Update
            }
        }

        public static MagneticMineBehaviour Spawn(
            GameObject minePrefab,
            Vector3 worldPosition,
            ArkhamEnemyManager enemyManager,
            int damage,
            int marks,
            float armTime,
            float timeout,
            GameObject explosionVfxPrefab)
        {
            if (minePrefab == null)
            {
                Debug.LogWarning("[MagneticMine] No mine prefab assigned on PowerupController — mine will not spawn.");
                return null;
            }

            // Snap to ground; tolerates not finding a hit (mine just floats).
            Vector3 spawnPosition = worldPosition;
            if (Physics.Raycast(worldPosition + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                spawnPosition = hit.point + Vector3.up * 0.02f;

            GameObject instance = Object.Instantiate(minePrefab, spawnPosition, Quaternion.identity);
            MagneticMineBehaviour mine = instance.GetComponent<MagneticMineBehaviour>();
            if (mine == null)
            {
                Debug.LogWarning($"[MagneticMine] Prefab '{minePrefab.name}' has no MagneticMineBehaviour component.");
                Object.Destroy(instance);
                return null;
            }

            mine.Configure(enemyManager, damage, marks, armTime, timeout, explosionVfxPrefab);
            return mine;
        }

        public void DespawnSilently()
        {
            if (detonated)
                return;
            detonated = true;
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        void Update()
        {
            if (detonated)
                return;

            lifeAge += Time.deltaTime;

            if (!armed)
            {
                if (lifeAge >= armTime)
                {
                    armed = true;
                    if (triggerCollider != null)
                        triggerCollider.enabled = true;
                }
                return;
            }

            if (lifeAge >= timeout)
                Detonate(transform.position);
        }

        void OnTriggerEnter(Collider other)
        {
            if (detonated || !armed)
                return;

            ArkhamEnemy enemy = other.GetComponentInParent<ArkhamEnemy>();
            if (enemy == null || !enemy.IsAlive)
                return;

            Detonate(transform.position);
        }

        void Detonate(Vector3 origin)
        {
            if (detonated)
                return;
            detonated = true;

            SpawnExplosionVfx(origin);

            float radius = AoeRadius;
            if (enemyManager != null && radius > 0f)
            {
                IReadOnlyList<ArkhamEnemy> enemies = enemyManager.Enemies;
                float radiusSqr = radius * radius;
                for (int i = 0; i < enemies.Count; i++)
                {
                    ArkhamEnemy enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive)
                        continue;

                    Vector3 delta = enemy.transform.position - origin;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > radiusSqr)
                        continue;

                    // Magnetize first so the kill is correctly attributed if
                    // the damage finishes the enemy.
                    enemy.ApplyMark(magnetizeMarks);
                    if (damage > 0)
                        enemy.ReceiveMagneticImpact(damage, origin, 1.2f, clearsMagnetized: false);
                }
            }

#if UNITY_EDITOR
            const int segments = 48;
            Color ring = new Color(0.18f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector3 p0 = origin + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = origin + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Debug.DrawLine(p0, p1, ring, 0.6f);
            }
#endif

            Destroy(gameObject);
        }

        void SpawnExplosionVfx(Vector3 origin)
        {
            if (explosionVfxPrefab == null)
                return;

            GameObject instance = Object.Instantiate(explosionVfxPrefab, origin, Quaternion.identity);
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float lifetime = 1.5f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;
                ps.Play(true);
                lifetime = Mathf.Max(lifetime, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            Object.Destroy(instance, lifetime);
        }

        float MaxLossyScale()
        {
            Vector3 s = transform.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        void OnDrawGizmosSelected()
        {
            if (triggerCollider == null)
                triggerCollider = GetComponent<SphereCollider>();
            float r = triggerCollider != null ? triggerCollider.radius * MaxLossyScale() : 0f;
            Gizmos.color = new Color(0.18f, 0.85f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
}
