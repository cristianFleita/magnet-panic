using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    [DisallowMultipleComponent]
    public sealed class ArenaSystem : MonoBehaviour
    {
        [Serializable]
        public sealed class WallSlamUnityEvent : UnityEvent<GameObject, Vector3, int, float>
        {
        }

        [Header("Bounds")]
        [SerializeField] Vector3 localPlayableCenter = new Vector3(-26.5f, 4.4f, 2.5f);
        [SerializeField] Vector3 localPlayableSize = new Vector3(30f, 8f, 58f);

        [Header("Spawn Selection")]
        [SerializeField, Min(0f)] float minSpawnDistanceFromPlayer = 8f;
        [SerializeField, Min(0.1f)] float spawnPointOverlapRadius = 1f;
        [SerializeField, Min(0f)] float offscreenBonus = 1.5f;
        [SerializeField] Camera spawnWeightCamera = null;

        [Header("Map Defaults")]
        [SerializeField] bool createDefaultMapSpawnsWhenEmpty = true;

        [Header("Wall Slam")]
        [SerializeField, Min(0)] int baseSlamDamage = 2;
        [SerializeField, Min(0.1f)] float speedDamageRatio = 5f;
        [SerializeField] WallSlamUnityEvent onWallSlam = new WallSlamUnityEvent();

        readonly List<ArenaSpawnPoint> enemySpawns = new List<ArenaSpawnPoint>();
        readonly List<ArenaSpawnPoint> scrapSpawns = new List<ArenaSpawnPoint>();
        readonly List<ArenaSpawnPoint> pickupSpawns = new List<ArenaSpawnPoint>();
        readonly List<ArenaSpawnPoint> spawnScratch = new List<ArenaSpawnPoint>();

        public event Action<GameObject, Vector3, int, float> WallSlammed;

        public Bounds PlayableBounds
        {
            get
            {
                Vector3 center = transform.TransformPoint(localPlayableCenter);
                Vector3 scale = Abs(transform.lossyScale);
                Vector3 size = new Vector3(
                    localPlayableSize.x * scale.x,
                    localPlayableSize.y * scale.y,
                    localPlayableSize.z * scale.z);

                return new Bounds(center, size);
            }
        }

        public WallSlamUnityEvent OnWallSlam => onWallSlam;
        public float MinSpawnDistanceFromPlayer => minSpawnDistanceFromPlayer;

        void Awake()
        {
            EnsureDefaultSpawnPoints();
            RebuildSpawnCache();
        }

        void OnValidate()
        {
            localPlayableSize.x = Mathf.Max(1f, localPlayableSize.x);
            localPlayableSize.y = Mathf.Max(0.1f, localPlayableSize.y);
            localPlayableSize.z = Mathf.Max(1f, localPlayableSize.z);
            spawnPointOverlapRadius = Mathf.Max(0.1f, spawnPointOverlapRadius);
            speedDamageRatio = Mathf.Max(0.1f, speedDamageRatio);
        }

        public void ConfigurePlayableBounds(Vector3 localCenter, Vector3 localSize)
        {
            localPlayableCenter = localCenter;
            localPlayableSize = new Vector3(
                Mathf.Max(1f, localSize.x),
                Mathf.Max(0.1f, localSize.y),
                Mathf.Max(1f, localSize.z));
        }

        public void ConfigureWallSlamDamage(int baseDamage, float speedRatio)
        {
            baseSlamDamage = Mathf.Max(0, baseDamage);
            speedDamageRatio = Mathf.Max(0.1f, speedRatio);
        }

        public void RebuildSpawnCache()
        {
            enemySpawns.Clear();
            scrapSpawns.Clear();
            pickupSpawns.Clear();

            ArenaSpawnPoint[] points = GetComponentsInChildren<ArenaSpawnPoint>(true);
            for (int i = 0; i < points.Length; i++)
            {
                ArenaSpawnPoint point = points[i];
                if (point == null)
                    continue;

                switch (point.Category)
                {
                    case ArenaSpawnCategory.Enemy:
                        enemySpawns.Add(point);
                        break;
                    case ArenaSpawnCategory.Scrap:
                        scrapSpawns.Add(point);
                        break;
                    case ArenaSpawnCategory.Pickup:
                        pickupSpawns.Add(point);
                        break;
                }
            }
        }

        public bool IsInsideArena(Vector3 position)
        {
            Bounds bounds = PlayableBounds;
            return position.x >= bounds.min.x
                && position.x <= bounds.max.x
                && position.z >= bounds.min.z
                && position.z <= bounds.max.z;
        }

        public Vector3 ClampToArena(Vector3 position)
        {
            Bounds bounds = PlayableBounds;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            return position;
        }

        public Vector3 GetRandomSpawnPoint(ArenaSpawnCategory category)
        {
            List<ArenaSpawnPoint> points = SpawnsFor(category);
            if (points.Count == 0)
                return RandomPointInsideArena();

            return points[UnityEngine.Random.Range(0, points.Count)].Position;
        }

        public Vector3 GetSpawnPointAwayFromPlayer(ArenaSpawnCategory category, Vector3 playerPosition, float minDistance)
        {
            float requiredDistance = Mathf.Max(0f, minDistance);
            List<ArenaSpawnPoint> points = SpawnsFor(category);
            spawnScratch.Clear();

            for (int i = 0; i < points.Count; i++)
            {
                ArenaSpawnPoint point = points[i];
                if (point == null)
                    continue;

                Vector3 delta = point.Position - playerPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < requiredDistance * requiredDistance)
                    continue;

                if (IsSpawnOccupied(point))
                    continue;

                spawnScratch.Add(point);
            }

            if (spawnScratch.Count > 0)
                return WeightedSpawnFromScratch(playerPosition).Position;

            return FallbackPointAwayFromPlayer(playerPosition, requiredDistance);
        }

        public Vector3 GetSpawnPointAwayFromPlayer(ArenaSpawnCategory category, Vector3 playerPosition)
        {
            return GetSpawnPointAwayFromPlayer(category, playerPosition, minSpawnDistanceFromPlayer);
        }

        public int CalculateWallSlamDamage(float impactSpeed)
        {
            return baseSlamDamage + Mathf.FloorToInt(Mathf.Max(0f, impactSpeed) / speedDamageRatio);
        }

        public void ReportWallSlam(GameObject target, Vector3 wallNormal, float impactSpeed)
        {
            ReportWallSlam(target, wallNormal, impactSpeed, CalculateWallSlamDamage(impactSpeed));
        }

        public void ReportWallSlam(GameObject target, Vector3 wallNormal, float impactSpeed, int damage)
        {
            Vector3 normal = wallNormal.sqrMagnitude > 0.001f ? wallNormal.normalized : GetNearestWallNormal(target != null ? target.transform.position : transform.position);
            int finalDamage = Mathf.Max(0, damage);
            onWallSlam.Invoke(target, normal, finalDamage, Mathf.Max(0f, impactSpeed));
            WallSlammed?.Invoke(target, normal, finalDamage, Mathf.Max(0f, impactSpeed));
        }

        public Vector3 GetNearestWallNormal(Vector3 position)
        {
            Bounds bounds = PlayableBounds;
            float left = Mathf.Abs(position.x - bounds.min.x);
            float right = Mathf.Abs(bounds.max.x - position.x);
            float back = Mathf.Abs(position.z - bounds.min.z);
            float front = Mathf.Abs(bounds.max.z - position.z);

            float best = left;
            Vector3 normal = Vector3.right;

            if (right < best)
            {
                best = right;
                normal = Vector3.left;
            }

            if (back < best)
            {
                best = back;
                normal = Vector3.forward;
            }

            if (front < best)
                normal = Vector3.back;

            return normal;
        }

        List<ArenaSpawnPoint> SpawnsFor(ArenaSpawnCategory category)
        {
            return category switch
            {
                ArenaSpawnCategory.Enemy => enemySpawns,
                ArenaSpawnCategory.Scrap => scrapSpawns,
                ArenaSpawnCategory.Pickup => pickupSpawns,
                _ => enemySpawns
            };
        }

        ArenaSpawnPoint WeightedSpawnFromScratch(Vector3 playerPosition)
        {
            float totalWeight = 0f;
            for (int i = 0; i < spawnScratch.Count; i++)
                totalWeight += SpawnWeight(spawnScratch[i], playerPosition);

            float pick = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < spawnScratch.Count; i++)
            {
                pick -= SpawnWeight(spawnScratch[i], playerPosition);
                if (pick <= 0f)
                    return spawnScratch[i];
            }

            return spawnScratch[spawnScratch.Count - 1];
        }

        float SpawnWeight(ArenaSpawnPoint point, Vector3 playerPosition)
        {
            Vector3 delta = point.Position - playerPosition;
            delta.y = 0f;
            float weight = Mathf.Max(0.1f, delta.magnitude);
            if (IsOffscreen(point.Position))
                weight *= 1f + offscreenBonus;

            return weight;
        }

        bool IsOffscreen(Vector3 position)
        {
            Camera sceneCamera = spawnWeightCamera != null ? spawnWeightCamera : Camera.main;
            if (sceneCamera == null)
                return false;

            Vector3 viewport = sceneCamera.WorldToViewportPoint(position);
            return viewport.z <= 0f
                || viewport.x < 0f
                || viewport.x > 1f
                || viewport.y < 0f
                || viewport.y > 1f;
        }

        bool IsSpawnOccupied(ArenaSpawnPoint point)
        {
            float radius = Mathf.Max(spawnPointOverlapRadius, point.OccupancyRadius);
            Collider[] hits = Physics.OverlapSphere(point.Position, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.GetComponentInParent<ArenaSpawnPoint>() == point)
                    continue;

                return true;
            }

            return false;
        }

        Vector3 FallbackPointAwayFromPlayer(Vector3 playerPosition, float minDistance)
        {
            const int attempts = 24;
            Vector3 best = ClampToArena(playerPosition);
            float bestDistanceSqr = -1f;

            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate = RandomPointInsideArena();
                Vector3 delta = candidate - playerPosition;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr >= minDistance * minDistance)
                    return candidate;

                if (distanceSqr > bestDistanceSqr)
                {
                    best = candidate;
                    bestDistanceSqr = distanceSqr;
                }
            }

            return best;
        }

        Vector3 RandomPointInsideArena()
        {
            Bounds bounds = PlayableBounds;
            return new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y,
                UnityEngine.Random.Range(bounds.min.z, bounds.max.z));
        }

        void EnsureDefaultSpawnPoints()
        {
            if (!createDefaultMapSpawnsWhenEmpty || GetComponentInChildren<ArenaSpawnPoint>(true) != null)
                return;

            Transform root = new GameObject("Generated Arena Spawns").transform;
            root.SetParent(transform, false);

            CreateDefaultSpawns(root, "EnemySpawns", ArenaSpawnCategory.Enemy, new[]
            {
                new Vector3(-39f, 4.4f, -11f),
                new Vector3(-14f, 4.4f, -11f),
                new Vector3(-39f, 4.4f, 16f),
                new Vector3(-14f, 4.4f, 16f),
                new Vector3(-27f, 4.4f, -21f),
                new Vector3(-27f, 4.4f, 24f),
                new Vector3(-40f, 4.4f, 2.5f),
                new Vector3(-13f, 4.4f, 2.5f)
            });

            CreateDefaultSpawns(root, "ScrapSpawns", ArenaSpawnCategory.Scrap, new[]
            {
                new Vector3(-34f, 4.4f, -7f),
                new Vector3(-28f, 4.4f, -8f),
                new Vector3(-21f, 4.4f, -6f),
                new Vector3(-35f, 4.4f, 2f),
                new Vector3(-28f, 4.4f, 0f),
                new Vector3(-20f, 4.4f, 3f),
                new Vector3(-34f, 4.4f, 10f),
                new Vector3(-27f, 4.4f, 12f),
                new Vector3(-20f, 4.4f, 10f),
                new Vector3(-31f, 4.4f, 18f),
                new Vector3(-24f, 4.4f, -15f),
                new Vector3(-17f, 4.4f, -1f)
            });

            CreateDefaultSpawns(root, "PickupSpawns", ArenaSpawnCategory.Pickup, new[]
            {
                new Vector3(-36f, 4.4f, -8f),
                new Vector3(-18f, 4.4f, -8f),
                new Vector3(-36f, 4.4f, 13f),
                new Vector3(-18f, 4.4f, 13f)
            });
        }

        static void CreateDefaultSpawns(Transform parent, string groupName, ArenaSpawnCategory category, Vector3[] localPositions)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(parent, false);

            for (int i = 0; i < localPositions.Length; i++)
            {
                GameObject pointObject = new GameObject(category + " Spawn " + (i + 1).ToString("00"));
                pointObject.transform.SetParent(group, false);
                pointObject.transform.localPosition = localPositions[i];
                ArenaSpawnPoint point = pointObject.AddComponent<ArenaSpawnPoint>();
                point.Configure(category);
            }
        }

        static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        void OnDrawGizmosSelected()
        {
            Bounds bounds = PlayableBounds;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.85f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
