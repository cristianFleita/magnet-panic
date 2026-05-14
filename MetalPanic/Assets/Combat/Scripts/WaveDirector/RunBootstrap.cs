using UnityEngine;
using UnityEngine.InputSystem;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Spawns arena, player, enemy manager and directors at run start so the
    /// scene only needs to contain this bootstrap component.
    /// </summary>
    public sealed class RunBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] GameObject arenaPrefab;
        [SerializeField] GameObject playerPrefab;
        [SerializeField] GameObject enemyManagerPrefab;
        [SerializeField] GameObject cameraPrefab;

        [Header("Configuration")]
        [SerializeField] WaveDirectorConfig waveDirectorConfig;

        [Header("Behavior")]
        [SerializeField] bool autoStart = true;
        [SerializeField] bool reuseExistingScene = true;

        ArenaSystem arenaSystem;
        ArkhamEnemyManager enemyManager;
        Transform playerTransform;
        Camera sceneCamera;
        ArkhamSimpleCameraFollow cameraFollow;
        WaveDirector waveDirector;
        ScrapDirector scrapDirector;
        HealingDirector healingDirector;
        RunController runController;

        public ArenaSystem Arena => arenaSystem;
        public ArkhamEnemyManager EnemyManager => enemyManager;
        public Transform Player => playerTransform;
        public WaveDirector WaveDirector => waveDirector;

        void Start()
        {
            if (autoStart)
                Bootstrap();
        }

        public void Bootstrap()
        {
            arenaSystem = EnsureArena();
            playerTransform = EnsurePlayer();
            enemyManager = EnsureEnemyManager();
            sceneCamera = EnsureCamera();
            ConfigurePlayerForRun();
            EnsureDirectors();
        }

        ArenaSystem EnsureArena()
        {
            ArenaSystem existing = reuseExistingScene ? FindFirstObjectByType<ArenaSystem>() : null;
            if (existing != null)
                return existing;

            if (arenaPrefab == null)
            {
                Debug.LogError("RunBootstrap: missing arena prefab.", this);
                return null;
            }

            GameObject instance = Instantiate(arenaPrefab);
            instance.name = arenaPrefab.name;
            return instance.GetComponentInChildren<ArenaSystem>();
        }

        Transform EnsurePlayer()
        {
            ArkhamCombatController existing = reuseExistingScene ? FindFirstObjectByType<ArkhamCombatController>() : null;
            if (existing != null)
                return existing.transform;

            if (playerPrefab == null)
            {
                Debug.LogError("RunBootstrap: missing player prefab.", this);
                return null;
            }

            Vector3 spawnPosition = arenaSystem != null ? arenaSystem.PlayerStart : Vector3.zero;
            GameObject instance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            instance.name = playerPrefab.name;
            return instance.transform;
        }

        ArkhamEnemyManager EnsureEnemyManager()
        {
            ArkhamEnemyManager existing = reuseExistingScene ? FindFirstObjectByType<ArkhamEnemyManager>() : null;
            if (existing != null)
                return existing;

            if (enemyManagerPrefab != null)
            {
                GameObject instance = Instantiate(enemyManagerPrefab);
                instance.name = enemyManagerPrefab.name;
                return instance.GetComponentInChildren<ArkhamEnemyManager>();
            }

            GameObject managerObject = new GameObject("EnemyManager");
            return managerObject.AddComponent<ArkhamEnemyManager>();
        }

        Camera EnsureCamera()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera == null && cameraPrefab != null)
            {
                GameObject instance = Instantiate(cameraPrefab);
                instance.name = cameraPrefab.name;
                mainCamera = instance.GetComponentInChildren<Camera>();
            }

            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("MainCamera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            if (!mainCamera.CompareTag("MainCamera"))
                mainCamera.tag = "MainCamera";

            cameraFollow = mainCamera.GetComponent<ArkhamSimpleCameraFollow>();
            if (cameraFollow == null)
                cameraFollow = mainCamera.gameObject.AddComponent<ArkhamSimpleCameraFollow>();

            if (playerTransform != null)
                cameraFollow.Configure(playerTransform, new Vector3(0f, 14f, -8f));

            return mainCamera;
        }

        void ConfigurePlayerForRun()
        {
            if (playerTransform == null)
                return;

            GameObject playerObject = playerTransform.gameObject;
            Animator animator = playerObject.GetComponentInChildren<Animator>();
            ArkhamPlayerMotor motor = playerObject.GetComponent<ArkhamPlayerMotor>();
            ArkhamTargetScanner scanner = playerObject.GetComponent<ArkhamTargetScanner>();
            ArkhamCombatController combat = playerObject.GetComponent<ArkhamCombatController>();
            MagnetismController magnetism = playerObject.GetComponent<MagnetismController>();
            OverloadController overload = playerObject.GetComponent<OverloadController>();
            GameInputProvider inputProvider = GameInputProvider.EnsureOn(playerObject);

            if (sceneCamera != null)
            {
                inputProvider.Configure(sceneCamera);

                PlayerInput playerInput = playerObject.GetComponent<PlayerInput>();
                if (playerInput != null)
                    playerInput.camera = sceneCamera;
            }

            if (motor != null)
                motor.Configure(sceneCamera, animator, 5f);

            Transform hitPoint = playerTransform.Find("Hit Point");
            if (hitPoint == null)
            {
                GameObject hitPointObject = new GameObject("Hit Point");
                hitPointObject.transform.SetParent(playerTransform, false);
                hitPointObject.transform.localPosition = new Vector3(0f, 0.9f, 0.75f);
                hitPoint = hitPointObject.transform;
            }

            if (combat != null)
                combat.Configure(enemyManager, scanner, motor, animator, cameraFollow, hitPoint);

            if (magnetism != null)
                magnetism.Configure(sceneCamera, enemyManager, motor);

            if (overload != null)
                overload.Configure(magnetism, motor, cameraFollow, enemyManager);
        }

        void EnsureDirectors()
        {
            waveDirector = GetComponent<WaveDirector>();
            if (waveDirector == null)
                waveDirector = gameObject.AddComponent<WaveDirector>();

            waveDirector.SetConfig(waveDirectorConfig);
            waveDirector.SetReferences(arenaSystem, enemyManager, playerTransform);

            scrapDirector = GetComponent<ScrapDirector>();
            if (scrapDirector == null)
                scrapDirector = gameObject.AddComponent<ScrapDirector>();
            scrapDirector.SetReferences(waveDirector, waveDirectorConfig, arenaSystem, playerTransform);

            healingDirector = GetComponent<HealingDirector>();
            if (healingDirector == null)
                healingDirector = gameObject.AddComponent<HealingDirector>();
            CombatHealth playerHealth = playerTransform != null ? playerTransform.GetComponentInChildren<CombatHealth>() : null;
            healingDirector.SetReferences(waveDirector, waveDirectorConfig, arenaSystem, playerTransform, playerHealth);

            DoorWarningLightPresenter doorWarningPresenter = GetComponent<DoorWarningLightPresenter>();
            if (doorWarningPresenter != null)
                doorWarningPresenter.SetReferences(waveDirector, arenaSystem);

            runController = GetComponent<RunController>();
            if (runController != null && playerTransform != null)
            {
                if (playerHealth != null)
                    runController.BindPlayerHealth(playerHealth);
            }

            // Start the run only after everything is wired.
            waveDirector.StartRun();
        }
    }
}
