using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    public sealed class ScrapDirector : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] WaveDirectorConfig config;
        [SerializeField] ArenaSystem arena;
        [SerializeField] Transform player;

        [Header("Placement")]
        [SerializeField] LayerMask groundMask;
        [SerializeField, Min(0f)] float scrapGroundClearance = 0.03f;
        [SerializeField, Min(0.5f)] float groundProbeHeight = 8f;
        [SerializeField, Min(0.5f)] float groundProbeDistance = 20f;

        readonly List<GameObject> activeScrap = new List<GameObject>(32);
        readonly List<Vector3> enemyPositions = new List<Vector3>(32);
        readonly List<ScrapTypeEntry> scrapScratch = new List<ScrapTypeEntry>(8);

        float refillTimer;

        public IReadOnlyList<GameObject> ActiveScrap => activeScrap;

        void Awake()
        {
            ResolveDependencies();
        }

        void OnEnable()
        {
            ResolveDependencies();
        }

        void Update()
        {
            if (config == null || arena == null || player == null)
                ResolveDependencies();

            if (config == null || arena == null || player == null)
                return;

            PruneDespawned();

            refillTimer -= Time.deltaTime;
            if (refillTimer > 0f)
                return;

            refillTimer = config.scrapRefillCadence;

            int active = activeScrap.Count;
            if (active >= config.scrapMaxActive)
                return;

            int floor = config.scrapAmmoFloorNearPlayer + GetActIndex() * config.scrapAmmoFloorBonusPerAct;
            int nearby = CountScrapNearPlayer();
            if (nearby >= floor)
                return;

            int spawnTarget = Mathf.Min(
                config.scrapBurstPerRefill,
                config.scrapMaxActive - active,
                floor - nearby);

            for (int i = 0; i < spawnTarget; i++)
                TrySpawnOnePiece();
        }

        public void SpawnBurst(int count)
        {
            if (config == null || arena == null || player == null)
                return;

            int actualCount = Mathf.Min(count, config.scrapMaxActive - activeScrap.Count);
            for (int i = 0; i < actualCount; i++)
                TrySpawnOnePiece();
        }

        public void SetReferences(
            WaveDirector director,
            WaveDirectorConfig nextConfig,
            ArenaSystem arenaRef,
            Transform playerRef)
        {
            if (director != null)
                waveDirector = director;
            if (nextConfig != null)
                config = nextConfig;
            if (arenaRef != null)
                arena = arenaRef;
            if (playerRef != null)
                player = playerRef;
        }

        void TrySpawnOnePiece()
        {
            ScrapTypeEntry entry = PickScrapType();
            if (entry == null || entry.prefab == null)
                return;

            enemyPositions.Clear();
            if (waveDirector != null && waveDirector.EnemyManager != null)
            {
                IReadOnlyList<ArkhamEnemy> enemies = waveDirector.EnemyManager.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    ArkhamEnemy enemy = enemies[i];
                    if (enemy != null && enemy.IsAlive)
                        enemyPositions.Add(enemy.transform.position);
                }
            }

            ScrapSpawnQuery query = new ScrapSpawnQuery
            {
                PlayerPosition = player.position,
                MinRadius = config.scrapNearMinRadius,
                MaxRadius = config.scrapNearMaxRadius,
                PlayerMinDistance = config.scrapPlayerMinDistance,
                DoorMinDistance = config.scrapDoorMinDistance,
                EnemyMinDistance = config.scrapEnemyMinDistance,
                WallInset = config.scrapWallInset,
                Attempts = config.scrapSamplingAttempts
            };

            arena.TryFindScrapSpawnPoint(query, enemyPositions, out Vector3 position);
            position = SnapToGround(position, entry.prefab);

            GameObject instance = Pool.Spawn(entry.prefab, position, Quaternion.identity);
            if (instance != null)
                activeScrap.Add(instance);
        }

        Vector3 SnapToGround(Vector3 position, GameObject prefab)
        {
            float floorOffset = EstimatePrefabFloorOffset(prefab) + scrapGroundClearance;
            Vector3 origin = position + Vector3.up * groundProbeHeight;
            int mask = groundMask.value != 0 ? groundMask.value : LayerMask.GetMask("Ground");
            if (mask == 0)
                mask = ~0;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeDistance, mask, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + floorOffset;
                return position;
            }

            Bounds bounds = arena != null ? arena.PlayableBounds : new Bounds(Vector3.zero, Vector3.one);
            position.y = bounds.center.y + floorOffset;
            return position;
        }

        float EstimatePrefabFloorOffset(GameObject prefab)
        {
            if (prefab == null)
                return 0.25f;

            Collider prefabCollider = prefab.GetComponent<Collider>();
            if (prefabCollider == null)
                return 0.25f;

            Vector3 scale = prefab.transform.lossyScale;
            if (prefabCollider is BoxCollider box)
            {
                float bottom = (box.center.y - box.size.y * 0.5f) * Mathf.Abs(scale.y);
                return Mathf.Max(0f, -bottom);
            }

            if (prefabCollider is SphereCollider sphere)
            {
                float radius = sphere.radius * MaxAbs(scale);
                float bottom = sphere.center.y * Mathf.Abs(scale.y) - radius;
                return Mathf.Max(0f, -bottom);
            }

            if (prefabCollider is CapsuleCollider capsule)
            {
                float radiusScale = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                    : capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z))
                    : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                float heightScale = capsule.direction == 0 ? Mathf.Abs(scale.x)
                    : capsule.direction == 1 ? Mathf.Abs(scale.y)
                    : Mathf.Abs(scale.z);
                float halfHeight = Mathf.Max(capsule.radius * radiusScale, capsule.height * heightScale * 0.5f);
                float center = capsule.center.y * Mathf.Abs(scale.y);
                return Mathf.Max(0f, -(center - halfHeight));
            }

            return Mathf.Max(0.05f, prefabCollider.bounds.extents.y);
        }

        static float MaxAbs(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        ScrapTypeEntry PickScrapType()
        {
            int actIndex = GetActIndex();
            scrapScratch.Clear();
            float totalWeight = 0f;

            for (int i = 0; i < config.scrapTypes.Count; i++)
            {
                ScrapTypeEntry entry = config.scrapTypes[i];
                if (entry == null || entry.prefab == null)
                    continue;

                float weight = config.GetScrapWeight(entry, actIndex);
                if (weight <= 0f)
                    continue;

                scrapScratch.Add(entry);
                totalWeight += weight;
            }

            if (scrapScratch.Count == 0)
                return null;

            float pick = Random.value * totalWeight;
            for (int i = 0; i < scrapScratch.Count; i++)
            {
                float w = config.GetScrapWeight(scrapScratch[i], actIndex);
                pick -= w;
                if (pick <= 0f)
                    return scrapScratch[i];
            }

            return scrapScratch[scrapScratch.Count - 1];
        }

        int CountScrapNearPlayer()
        {
            float radiusSqr = config.scrapNearMaxRadius * config.scrapNearMaxRadius;
            Vector3 playerPos = player.position;
            int count = 0;

            for (int i = 0; i < activeScrap.Count; i++)
            {
                GameObject instance = activeScrap[i];
                if (instance == null || !instance.activeInHierarchy)
                    continue;

                Vector3 delta = instance.transform.position - playerPos;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                    count++;
            }

            return count;
        }

        void PruneDespawned()
        {
            for (int i = activeScrap.Count - 1; i >= 0; i--)
            {
                GameObject instance = activeScrap[i];
                if (instance == null || !instance.activeInHierarchy)
                    activeScrap.RemoveAt(i);
            }
        }

        int GetActIndex()
        {
            return waveDirector != null ? waveDirector.CurrentActIndex : 0;
        }

        void ResolveDependencies()
        {
            if (waveDirector == null)
                waveDirector = FindFirstObjectByType<WaveDirector>();

            if (waveDirector != null)
            {
                if (config == null)
                    config = waveDirector.Config;
                if (arena == null)
                    arena = waveDirector.Arena;
                if (player == null && waveDirector.Player != null)
                    player = waveDirector.Player;
            }

            if (arena == null)
                arena = FindFirstObjectByType<ArenaSystem>();

            if (player == null)
            {
                ArkhamCombatController combat = FindFirstObjectByType<ArkhamCombatController>();
                if (combat != null)
                    player = combat.transform;
            }
        }
    }
}
