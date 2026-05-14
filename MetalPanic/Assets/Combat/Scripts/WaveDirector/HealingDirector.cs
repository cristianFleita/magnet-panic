using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    public sealed class HealingDirector : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] WaveDirectorConfig config;
        [SerializeField] ArenaSystem arena;
        [SerializeField] Transform player;
        [SerializeField] CombatHealth playerHealth;

        readonly List<GameObject> activePickups = new List<GameObject>(2);

        float cooldownTimer;

        public IReadOnlyList<GameObject> ActivePickups => activePickups;

        void Awake()
        {
            ResolveDependencies();
            cooldownTimer = config != null ? config.healingFirstSpawnDelay : 15f;
        }

        void OnEnable()
        {
            ResolveDependencies();
        }

        void Update()
        {
            if (config == null || arena == null || playerHealth == null)
                ResolveDependencies();

            if (config == null || arena == null || playerHealth == null)
                return;

            PrunePickups();

            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f)
                return;

            if (activePickups.Count >= config.healingMaxActive)
                return;

            if (!ShouldSpawnNow())
                return;

            if (!arena.TryGetPickupSpawn(player != null ? player.position : Vector3.zero, out ArenaSpawnPoint pad))
                return;

            GameObject instance = Pool.Spawn(config.healingPickupPrefab, pad.Position, Quaternion.identity);
            if (instance != null)
            {
                activePickups.Add(instance);
                cooldownTimer = config.healingSpawnCooldown;
            }
        }

        public void SetReferences(
            WaveDirector director,
            WaveDirectorConfig nextConfig,
            ArenaSystem arenaRef,
            Transform playerRef,
            CombatHealth healthRef)
        {
            if (director != null)
                waveDirector = director;
            if (nextConfig != null)
                config = nextConfig;
            if (arenaRef != null)
                arena = arenaRef;
            if (playerRef != null)
                player = playerRef;
            if (healthRef != null)
                playerHealth = healthRef;

            if (config != null && cooldownTimer <= 0f)
                cooldownTimer = config.healingFirstSpawnDelay;
        }

        bool ShouldSpawnNow()
        {
            if (playerHealth == null || !playerHealth.IsAlive)
                return false;

            if (playerHealth.IsFull)
                return false;

            float hpFraction = playerHealth.MaxHealth > 0
                ? (float)playerHealth.CurrentHealth / playerHealth.MaxHealth
                : 0f;

            return hpFraction <= config.healingHpThreshold;
        }

        void PrunePickups()
        {
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                GameObject instance = activePickups[i];
                if (instance == null || !instance.activeInHierarchy)
                    activePickups.RemoveAt(i);
            }
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
                if (player == null)
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

            if (playerHealth == null && player != null)
                playerHealth = player.GetComponentInChildren<CombatHealth>();
        }
    }
}
