using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    public struct ArenaSpawnValidation
    {
        public float PlayerMinDistance;
        public float DoorMinDistance;
        public float WallInset;
        public float OccupancyRadius;
        public LayerMask BlockingMask;
    }

    public struct ScrapSpawnQuery
    {
        public Vector3 PlayerPosition;
        public float MinRadius;
        public float MaxRadius;
        public float PlayerMinDistance;
        public float DoorMinDistance;
        public float EnemyMinDistance;
        public float WallInset;
        public int Attempts;
    }

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
        [SerializeField] Vector3 localPlayerStart = new Vector3(-26.5f, 4.4f, 2.5f);

        [Header("Spawn Selection")]
        [SerializeField, Min(0f)] float minSpawnDistanceFromPlayer = 8f;
        [SerializeField, Min(0.1f)] float spawnPointOverlapRadius = 1f;
        [SerializeField, Min(0f)] float offscreenBonus = 1.5f;
        [SerializeField] Camera spawnWeightCamera = null;

        [Header("Map Defaults")]
        [SerializeField] bool createDefaultMapSpawnsWhenEmpty = true;

        [Header("Scrap Sampling Defaults")]
        [SerializeField, Min(0f)] float defaultScrapWallInset = 1.5f;
        [SerializeField, Min(0f)] float defaultScrapDoorMinDistance = 4f;

        [Header("Wall Slam")]
        [SerializeField, Min(0)] int baseSlamDamage = 2;
        [SerializeField, Min(0.1f)] float speedDamageRatio = 5f;
        [SerializeField] WallSlamUnityEvent onWallSlam = new WallSlamUnityEvent();

        readonly List<ArenaSpawnPoint> enemySpawns = new List<ArenaSpawnPoint>();
        readonly List<ArenaSpawnPoint> scrapSpawns = new List<ArenaSpawnPoint>();
        readonly List<ArenaSpawnPoint> pickupSpawns = new List<ArenaSpawnPoint>();
        readonly List<ArenaSpawnPoint> spawnScratch = new List<ArenaSpawnPoint>();
        readonly List<ArenaDoor> doors = new List<ArenaDoor>();
        static readonly IReadOnlyList<Vector3> EmptyEnemyPositions = Array.Empty<Vector3>();

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

        public Vector3 PlayerStart => transform.TransformPoint(localPlayerStart);
        public WallSlamUnityEvent OnWallSlam => onWallSlam;
        public float MinSpawnDistanceFromPlayer => minSpawnDistanceFromPlayer;
        public IReadOnlyList<ArenaDoor> Doors => doors;
        public IReadOnlyList<ArenaSpawnPoint> PickupSpawns => pickupSpawns;

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

        public void ConfigurePlayerStart(Vector3 localStart)
        {
            localPlayerStart = localStart;
        }

        public void ConfigureWallSlamDamage(int baseDamage, float speedRatio)
        {
            baseSlamDamage = Mathf.Max(0, baseDamage);
            speedDamageRatio = Mathf.Max(0.1f, speedRatio);
        }

        public void RebuildSpawnCache()
        {
            EnsureDefaultSpawnPoints();

            enemySpawns.Clear();
            scrapSpawns.Clear();
            pickupSpawns.Clear();
            doors.Clear();

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

            ArenaDoor[] doorComponents = GetComponentsInChildren<ArenaDoor>(true);
            for (int i = 0; i < doorComponents.Length; i++)
            {
                ArenaDoor door = doorComponents[i];
                if (door == null || !door.IsEnabled || !door.gameObject.activeInHierarchy)
                    continue;
                doors.Add(door);
            }
        }

        // ----- Bounds -----

        public bool IsInsideArena(Vector3 position)
        {
            Bounds bounds = PlayableBounds;
            return position.x >= bounds.min.x
                && position.x <= bounds.max.x
                && position.z >= bounds.min.z
                && position.z <= bounds.max.z;
        }

        public bool IsInsidePlayableArea(Vector3 position, float wallInset)
        {
            Bounds bounds = PlayableBounds;
            float inset = Mathf.Max(0f, wallInset);
            return position.x >= bounds.min.x + inset
                && position.x <= bounds.max.x - inset
                && position.z >= bounds.min.z + inset
                && position.z <= bounds.max.z - inset;
        }

        public Vector3 ClampToArena(Vector3 position)
        {
            Bounds bounds = PlayableBounds;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            return position;
        }

        public Vector3 ClampToPlayableArea(Vector3 position, float wallInset)
        {
            Bounds bounds = PlayableBounds;
            float inset = Mathf.Max(0f, wallInset);
            float minX = bounds.min.x + inset;
            float maxX = bounds.max.x - inset;
            float minZ = bounds.min.z + inset;
            float maxZ = bounds.max.z - inset;
            if (minX > maxX) minX = maxX = bounds.center.x;
            if (minZ > maxZ) minZ = maxZ = bounds.center.z;
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.z = Mathf.Clamp(position.z, minZ, maxZ);
            return position;
        }

        public Vector3 RandomPointInsideArena(float wallInset = 0f)
        {
            Bounds bounds = PlayableBounds;
            float inset = Mathf.Max(0f, wallInset);
            float minX = bounds.min.x + inset;
            float maxX = bounds.max.x - inset;
            float minZ = bounds.min.z + inset;
            float maxZ = bounds.max.z - inset;
            if (minX > maxX) { minX = bounds.center.x; maxX = bounds.center.x; }
            if (minZ > maxZ) { minZ = bounds.center.z; maxZ = bounds.center.z; }
            return new Vector3(
                UnityEngine.Random.Range(minX, maxX),
                bounds.center.y,
                UnityEngine.Random.Range(minZ, maxZ));
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

        // ----- Doors -----

        public bool TryGetDoor(ArenaDoorId id, out ArenaDoor door)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                ArenaDoor candidate = doors[i];
                if (candidate != null && candidate.DoorId == id)
                {
                    door = candidate;
                    return true;
                }
            }

            door = null;
            return false;
        }

        public ArenaDoor GetDoor(ArenaDoorId id)
        {
            TryGetDoor(id, out ArenaDoor door);
            return door;
        }

        public ArenaDoor GetFarthestDoorFrom(Vector3 position)
        {
            ArenaDoor best = null;
            float bestDistSqr = -1f;
            for (int i = 0; i < doors.Count; i++)
            {
                ArenaDoor door = doors[i];
                if (door == null || !door.IsEnabled)
                    continue;

                Vector3 delta = door.ExitPosition - position;
                delta.y = 0f;
                float distSqr = delta.sqrMagnitude;
                if (distSqr > bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = door;
                }
            }

            return best;
        }

        public float DistanceToNearestDoor(Vector3 position)
        {
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < doors.Count; i++)
            {
                ArenaDoor door = doors[i];
                if (door == null)
                    continue;

                Vector3 delta = door.ExitPosition - position;
                delta.y = 0f;
                float distSqr = delta.sqrMagnitude;
                if (distSqr < bestSqr)
                    bestSqr = distSqr;
            }

            return float.IsPositiveInfinity(bestSqr) ? float.PositiveInfinity : Mathf.Sqrt(bestSqr);
        }

        public bool IsNearDoor(Vector3 position, float radius)
        {
            float r = Mathf.Max(0f, radius);
            float rSqr = r * r;
            for (int i = 0; i < doors.Count; i++)
            {
                ArenaDoor door = doors[i];
                if (door == null)
                    continue;

                Vector3 delta = door.ExitPosition - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= rSqr)
                    return true;
            }

            return false;
        }

        // ----- Spawn validation -----

        public bool IsValidSpawnPosition(Vector3 position, Vector3 playerPosition, ArenaSpawnValidation validation)
        {
            if (!IsInsidePlayableArea(position, validation.WallInset))
                return false;

            Vector3 toPlayer = position - playerPosition;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < validation.PlayerMinDistance * validation.PlayerMinDistance)
                return false;

            if (validation.DoorMinDistance > 0f && IsNearDoor(position, validation.DoorMinDistance))
                return false;

            if (validation.OccupancyRadius > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(position, validation.OccupancyRadius, validation.BlockingMask.value != 0 ? validation.BlockingMask.value : ~0, QueryTriggerInteraction.Ignore);
                if (hits.Length > 0)
                    return false;
            }

            return true;
        }

        // ----- Scrap sampling -----

        public bool TryFindScrapSpawnPoint(ScrapSpawnQuery query, IReadOnlyList<Vector3> enemyPositions, out Vector3 position)
        {
            int attempts = query.Attempts > 0 ? query.Attempts : 16;
            float minRadius = Mathf.Max(0f, query.MinRadius);
            float maxRadius = Mathf.Max(minRadius + 0.1f, query.MaxRadius);
            float playerMinSqr = query.PlayerMinDistance * query.PlayerMinDistance;
            float doorMin = Mathf.Max(0f, query.DoorMinDistance);
            float enemyMinSqr = query.EnemyMinDistance * query.EnemyMinDistance;
            float wallInset = query.WallInset > 0f ? query.WallInset : defaultScrapWallInset;
            IReadOnlyList<Vector3> enemies = enemyPositions ?? EmptyEnemyPositions;

            for (int i = 0; i < attempts; i++)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minRadius, maxRadius, UnityEngine.Random.value);
                Vector3 candidate = new Vector3(
                    query.PlayerPosition.x + Mathf.Cos(angle) * radius,
                    PlayableBounds.center.y,
                    query.PlayerPosition.z + Mathf.Sin(angle) * radius);

                if (!IsInsidePlayableArea(candidate, wallInset))
                    continue;

                Vector3 toPlayer = candidate - query.PlayerPosition;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < playerMinSqr)
                    continue;

                if (doorMin > 0f && IsNearDoor(candidate, doorMin))
                    continue;

                if (enemyMinSqr > 0f && IsTooCloseToAny(candidate, enemies, enemyMinSqr))
                    continue;

                position = candidate;
                return true;
            }

            // Fallback: clamp ring midpoint inside the area, away from player.
            Vector3 fallback = query.PlayerPosition + new Vector3(0f, 0f, Mathf.Max(query.PlayerMinDistance, minRadius + 1f));
            fallback = ClampToPlayableArea(fallback, wallInset);
            position = fallback;
            return false;
        }

        static bool IsTooCloseToAny(Vector3 candidate, IReadOnlyList<Vector3> others, float minDistanceSqr)
        {
            for (int i = 0; i < others.Count; i++)
            {
                Vector3 delta = others[i] - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistanceSqr)
                    return true;
            }

            return false;
        }

        // ----- Pickup pads -----

        public bool TryGetPickupSpawn(Vector3 playerPosition, out ArenaSpawnPoint point)
        {
            spawnScratch.Clear();
            for (int i = 0; i < pickupSpawns.Count; i++)
            {
                ArenaSpawnPoint pad = pickupSpawns[i];
                if (pad == null)
                    continue;

                if (IsSpawnOccupied(pad))
                    continue;

                spawnScratch.Add(pad);
            }

            if (spawnScratch.Count == 0)
            {
                point = null;
                return false;
            }

            point = WeightedSpawnFromScratch(playerPosition);
            return point != null;
        }

        // ----- Legacy enemy spawn API (kept for back-compat) -----

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

        // ----- Wall slam -----

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

        // ----- Internal helpers -----

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
                if (IsStaticArenaCollider(hit))
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

        // ----- Default spawn point generation -----

        void EnsureDefaultSpawnPoints()
        {
            if (!createDefaultMapSpawnsWhenEmpty)
                return;

            bool hasDoors = GetComponentInChildren<ArenaDoor>(true) != null;
            bool hasPickupSpawns = false;
            ArenaSpawnPoint[] spawnPoints = GetComponentsInChildren<ArenaSpawnPoint>(true);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null && spawnPoints[i].Category == ArenaSpawnCategory.Pickup)
                {
                    hasPickupSpawns = true;
                    break;
                }
            }

            if (hasDoors && hasPickupSpawns)
                return;

            Transform root = transform.Find("Generated Arena Spawns");
            if (root == null)
            {
                root = new GameObject("Generated Arena Spawns").transform;
                root.SetParent(transform, false);
            }

            if (!hasDoors)
                CreateDefaultDoors(root);
            if (!hasPickupSpawns)
                CreateDefaultPickupPads(root);
        }

        void CreateDefaultDoors(Transform parent)
        {
            Transform group = new GameObject("Doors").transform;
            group.SetParent(parent, false);

            Vector3 localCenter = localPlayableCenter;
            float halfX = localPlayableSize.x * 0.5f;
            float halfZ = localPlayableSize.z * 0.5f;
            float y = localCenter.y;

            CreateDoor(group, ArenaDoorId.North, new Vector3(localCenter.x, y, localCenter.z + halfZ), Vector3.back);
            CreateDoor(group, ArenaDoorId.East, new Vector3(localCenter.x + halfX, y, localCenter.z), Vector3.left);
            CreateDoor(group, ArenaDoorId.West, new Vector3(localCenter.x - halfX, y, localCenter.z), Vector3.right);
        }

        void CreateDefaultPickupPads(Transform parent)
        {
            Transform group = new GameObject("PickupPads").transform;
            group.SetParent(parent, false);

            Vector3 localCenter = localPlayableCenter;
            float quarterX = localPlayableSize.x * 0.33f;
            float quarterZ = localPlayableSize.z * 0.28f;
            float y = localCenter.y;

            Vector3[] padPositions =
            {
                new Vector3(localCenter.x - quarterX, y, localCenter.z + quarterZ),
                new Vector3(localCenter.x + quarterX, y, localCenter.z + quarterZ),
                new Vector3(localCenter.x - quarterX, y, localCenter.z - quarterZ),
                new Vector3(localCenter.x + quarterX, y, localCenter.z - quarterZ)
            };

            string[] padNames = { "Pickup_NW", "Pickup_NE", "Pickup_SW", "Pickup_SE" };

            for (int i = 0; i < padPositions.Length; i++)
            {
                GameObject pad = new GameObject(padNames[i]);
                pad.transform.SetParent(group, false);
                pad.transform.localPosition = padPositions[i];
                ArenaSpawnPoint point = pad.AddComponent<ArenaSpawnPoint>();
                point.Configure(ArenaSpawnCategory.Pickup);
            }
        }

        void CreateDoor(Transform parent, ArenaDoorId id, Vector3 localPosition, Vector3 facingLocal)
        {
            GameObject obj = new GameObject("Door_" + id);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            ArenaDoor door = obj.AddComponent<ArenaDoor>();
            door.Configure(id, facingLocal);
        }

        static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        static bool IsStaticArenaCollider(Collider hit)
        {
            int layer = hit.gameObject.layer;
            return layer == LayerMask.NameToLayer("Ground")
                || layer == LayerMask.NameToLayer("ArenaWall");
        }

        void OnDrawGizmosSelected()
        {
            Bounds bounds = PlayableBounds;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.85f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.9f);
            Gizmos.DrawWireSphere(PlayerStart, 0.6f);

            for (int i = 0; i < pickupSpawns.Count; i++)
            {
                ArenaSpawnPoint pad = pickupSpawns[i];
                if (pad == null)
                    continue;
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.85f);
                Gizmos.DrawWireSphere(pad.Position, 0.8f);
            }
        }
    }
}
