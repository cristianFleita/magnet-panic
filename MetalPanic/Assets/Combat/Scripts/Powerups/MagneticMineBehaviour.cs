using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Runtime-spawned mine. Owns its own arm timer, trigger, timeout and
    /// detonation AoE — once it's in the scene it lives on its own life cycle
    /// regardless of whether the spawning effect was cancelled (see GDD AC-7 /
    /// E2 — the effect kills the mine only on player death).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagneticMineBehaviour : MonoBehaviour
    {
        const float DiscThickness = 0.08f;
        const float DiscRadius = 0.55f;

        float aoeRadius = 10f;
        int damage = 4;
        int magnetizeMarks = 2;
        float triggerRadius = 0.6f;
        float armTime = 0.5f;
        float timeout = 30f;
        Color color = new Color(0.18f, 0.85f, 1f, 1f);

        SphereCollider triggerCollider;
        Renderer discRenderer;
        MaterialPropertyBlock propertyBlock;

        float lifeAge;
        bool armed;
        bool detonated;
        ArkhamEnemyManager enemyManager;

        public bool Detonated => detonated;

        public void Configure(
            ArkhamEnemyManager enemyManagerRef,
            float aoeRadiusValue,
            int damageValue,
            int marksValue,
            float triggerRadiusValue,
            float armTimeValue,
            float timeoutValue,
            Color colorValue)
        {
            enemyManager = enemyManagerRef;
            aoeRadius = Mathf.Max(0.5f, aoeRadiusValue);
            damage = Mathf.Max(0, damageValue);
            magnetizeMarks = Mathf.Max(1, marksValue);
            triggerRadius = Mathf.Max(0.1f, triggerRadiusValue);
            armTime = Mathf.Max(0f, armTimeValue);
            timeout = Mathf.Max(1f, timeoutValue);
            color = colorValue;

            BuildVisual();
            BuildTrigger();
            ApplyColor(0.35f);
        }

        public static MagneticMineBehaviour Spawn(
            Vector3 worldPosition,
            ArkhamEnemyManager enemyManager,
            float aoeRadius,
            int damage,
            int marks,
            float triggerRadius,
            float armTime,
            float timeout,
            Color color)
        {
            // Snap to ground; tolerates not finding a hit (mine just floats).
            Vector3 spawnPosition = worldPosition;
            if (Physics.Raycast(worldPosition + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                spawnPosition = hit.point + Vector3.up * 0.02f;

            GameObject go = new GameObject("MagneticMine");
            go.transform.position = spawnPosition;
            MagneticMineBehaviour mine = go.AddComponent<MagneticMineBehaviour>();
            mine.Configure(enemyManager, aoeRadius, damage, marks, triggerRadius, armTime, timeout, color);
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
                float t = armTime <= 0f ? 1f : Mathf.Clamp01(lifeAge / armTime);
                ApplyColor(Mathf.Lerp(0.25f, 0.85f, t));
                if (lifeAge >= armTime)
                {
                    armed = true;
                    if (triggerCollider != null)
                        triggerCollider.enabled = true;
                    ApplyColor(0.95f);
                }
                return;
            }

            // Pulse the disc once it's armed.
            float pulse = 0.7f + Mathf.PingPong(lifeAge * 2.4f, 0.3f);
            ApplyColor(pulse);

            if (lifeAge >= timeout)
                Detonate();
        }

        void OnTriggerEnter(Collider other)
        {
            if (detonated || !armed)
                return;

            ArkhamEnemy enemy = other.GetComponentInParent<ArkhamEnemy>();
            if (enemy == null || !enemy.IsAlive)
                return;

            Detonate();
        }

        void Detonate()
        {
            if (detonated)
                return;
            detonated = true;

            Vector3 origin = transform.position;
            if (enemyManager != null)
            {
                IReadOnlyList<ArkhamEnemy> enemies = enemyManager.Enemies;
                float radiusSqr = aoeRadius * aoeRadius;
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
            Color ring = color;
            ring.a = 0.9f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector3 p0 = origin + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * aoeRadius;
                Vector3 p1 = origin + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * aoeRadius;
                Debug.DrawLine(p0, p1, ring, 0.6f);
            }
#endif

            Destroy(gameObject);
        }

        void BuildVisual()
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(transform, false);
            disc.transform.localScale = new Vector3(DiscRadius * 2f, DiscThickness, DiscRadius * 2f);
            disc.transform.localPosition = new Vector3(0f, DiscThickness, 0f);

            Collider discCollider = disc.GetComponent<Collider>();
            if (discCollider != null)
                Destroy(discCollider);

            discRenderer = disc.GetComponent<Renderer>();
            if (discRenderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                discRenderer.sharedMaterial = new Material(shader) { color = color };
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        void BuildTrigger()
        {
            triggerCollider = gameObject.AddComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = triggerRadius;
            triggerCollider.center = new Vector3(0f, 0.2f, 0f);
            triggerCollider.enabled = false; // armed in Update
        }

        void ApplyColor(float intensity)
        {
            if (discRenderer == null || propertyBlock == null)
                return;

            Color c = color;
            c.r *= intensity;
            c.g *= intensity;
            c.b *= intensity;
            propertyBlock.SetColor("_BaseColor", c);
            propertyBlock.SetColor("_Color", c);
            discRenderer.SetPropertyBlock(propertyBlock);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.25f);
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.75f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
