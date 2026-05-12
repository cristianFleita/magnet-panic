using System;
using System.Collections.Generic;
using System.IO;
using MagnetPanic.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MagnetPanic.Combat.Editor
{
    public static class RunVerticalSliceSetup
    {
        const string PrefabsFolder = "Assets/Prefabs";
        const string PickupsFolder = "Assets/Prefabs/Pickups";
        const string MaterialsFolder = "Assets/Combat/Generated/Materials";
        const string GeneratedFolder = "Assets/Combat/Generated";
        const string SceneFolder = "Assets/Scenes";

        const string ArenaPrefabPath = PrefabsFolder + "/Arena.prefab";
        const string RunPrefabPath = PrefabsFolder + "/Run.prefab";
        const string HealingPickupPrefabPath = PickupsFolder + "/HealingPickup.prefab";
        const string WaveDirectorConfigPath = GeneratedFolder + "/WaveDirectorConfig.asset";
        const string RunScenePath = SceneFolder + "/RunVerticalSlice.unity";

        const string PlayerPrefabPath = PrefabsFolder + "/MainCharacter.prefab";
        const string ScraplingPrefabPath = PrefabsFolder + "/Enemies/Combat/Scrapling.prefab";
        const string MetalEnemyPrefabPath = PrefabsFolder + "/Enemies/Combat/MetalEnemy.prefab";
        const string RunnerBotPrefabPath = PrefabsFolder + "/Enemies/Combat/RunnerBot.prefab";
        const string HeavyBotPrefabPath = PrefabsFolder + "/Enemies/Combat/HeavyBot.prefab";

        const string LightScrapPrefabPath = PrefabsFolder + "/Attractables/LightScrap_Attractable.prefab";
        const string PlatePrefabPath = PrefabsFolder + "/Attractables/Plate_Attractable.prefab";
        const string MinePrefabPath = PrefabsFolder + "/Attractables/Mine_Attractable.prefab";
        const string HeavyScrapPrefabPath = PrefabsFolder + "/Attractables/Heavy_Attractable.prefab";

        const string ScraplingDefinitionPath = GeneratedFolder + "/EnemyDefinitions/Scrapling.asset";
        const string MetalEnemyDefinitionPath = GeneratedFolder + "/EnemyDefinitions/MetalEnemy.asset";
        const string RunnerBotDefinitionPath = GeneratedFolder + "/EnemyDefinitions/RunnerBot.asset";
        const string HeavyBotDefinitionPath = GeneratedFolder + "/EnemyDefinitions/HeavyBot.asset";

        [MenuItem("Tools/Magnet Panic/Vertical Slice/Create Run Vertical Slice")]
        public static void CreateRunVerticalSlice()
        {
            EnsureProjectLayers();
            EnsureFolder(PrefabsFolder);
            EnsureFolder(PickupsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(GeneratedFolder);
            EnsureFolder(SceneFolder);

            GameObject healingPickupPrefab = CreateOrUpdateHealingPickupPrefab();
            GameObject arenaPrefab = CreateOrUpdateArenaPrefab();
            WaveDirectorConfig config = CreateOrUpdateWaveDirectorConfig(healingPickupPrefab);

            HardenRuntimePrefabs();
            DeleteGeneratedRunPrefab();
            CreateOrUpdateRunScene(arenaPrefab, config);
            AddSceneToBuildSettings(RunScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Run Vertical Slice",
                    "Created an editable RunVerticalSlice scene with Arena, player, camera, EnemyManager and Run Configuration wired in-scene.",
                    "Play");
            }
        }

        public static void ValidateRunVerticalSlice()
        {
            List<string> errors = new List<string>();

            GameObject arenaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            GameObject healingPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealingPickupPrefabPath);
            WaveDirectorConfig config = AssetDatabase.LoadAssetAtPath<WaveDirectorConfig>(WaveDirectorConfigPath);
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RunScenePath);

            if (arenaPrefab == null) errors.Add("Missing Arena.prefab");
            if (healingPickupPrefab == null) errors.Add("Missing HealingPickup.prefab");
            if (config == null) errors.Add("Missing WaveDirectorConfig.asset");
            if (scene == null) errors.Add("Missing RunVerticalSlice.unity");

            if (config != null)
            {
                if (config.acts == null || config.acts.Count != 5)
                    errors.Add("WaveDirectorConfig must contain 5 acts.");
                if (config.scrapTypes == null || config.scrapTypes.Count != 4)
                    errors.Add("WaveDirectorConfig must contain 4 scrap types.");
                if (config.healingPickupPrefab == null)
                    errors.Add("WaveDirectorConfig missing healingPickupPrefab.");
            }

            if (arenaPrefab != null)
                ValidateArenaPrefab(errors);
            if (scene != null)
                ValidateRunScene(errors, config);
            if (healingPickupPrefab != null)
                ValidateHealingPrefab(errors);

            if (errors.Count > 0)
                throw new Exception("Run vertical slice validation failed:\n- " + string.Join("\n- ", errors));

            Debug.Log("[RunVerticalSliceSetup] Validation passed: editable scene, Arena prefab, Healing pickup and config references are present.");
        }

        static void ValidateArenaPrefab(List<string> errors)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(ArenaPrefabPath);
            try
            {
                ArenaSystem arena = contents.GetComponentInChildren<ArenaSystem>(true);
                if (arena == null)
                {
                    errors.Add("Arena.prefab missing ArenaSystem.");
                    return;
                }

                arena.RebuildSpawnCache();
                if (arena.Doors.Count != 4)
                    errors.Add("Arena.prefab must contain exactly 4 ArenaDoor components.");
                if (arena.PickupSpawns.Count != 4)
                    errors.Add("Arena.prefab must contain exactly 4 pickup pads.");

                for (int i = 0; i < arena.Doors.Count; i++)
                {
                    ArenaDoor door = arena.Doors[i];
                    if (door == null)
                    {
                        errors.Add("Arena.prefab has a null door reference.");
                        continue;
                    }

                    if (door.WarningLight == null)
                        errors.Add("Door " + door.DoorId + " missing warning light.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void ValidateRunScene(List<string> errors, WaveDirectorConfig config)
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            try
            {
                ArenaSystem arena = Object.FindFirstObjectByType<ArenaSystem>();
                ArkhamCombatController player = Object.FindFirstObjectByType<ArkhamCombatController>();
                ArkhamEnemyManager enemyManager = Object.FindFirstObjectByType<ArkhamEnemyManager>();
                Camera camera = Camera.main;
                WaveDirector waveDirector = Object.FindFirstObjectByType<WaveDirector>();
                ScrapDirector scrapDirector = Object.FindFirstObjectByType<ScrapDirector>();
                HealingDirector healingDirector = Object.FindFirstObjectByType<HealingDirector>();
                RunController runController = Object.FindFirstObjectByType<RunController>();
                DoorWarningLightPresenter warningPresenter = Object.FindFirstObjectByType<DoorWarningLightPresenter>();
                WaveDirectorDebugHud debugHud = Object.FindFirstObjectByType<WaveDirectorDebugHud>();
                RunBootstrap bootstrap = Object.FindFirstObjectByType<RunBootstrap>();

                if (arena == null) errors.Add("RunVerticalSlice scene missing ArenaSystem instance.");
                if (player == null) errors.Add("RunVerticalSlice scene missing player instance.");
                if (enemyManager == null) errors.Add("RunVerticalSlice scene missing EnemyManager.");
                if (camera == null) errors.Add("RunVerticalSlice scene missing MainCamera.");
                if (waveDirector == null) errors.Add("RunVerticalSlice scene missing WaveDirector.");
                if (scrapDirector == null) errors.Add("RunVerticalSlice scene missing ScrapDirector.");
                if (healingDirector == null) errors.Add("RunVerticalSlice scene missing HealingDirector.");
                if (runController == null) errors.Add("RunVerticalSlice scene missing RunController.");
                if (warningPresenter == null) errors.Add("RunVerticalSlice scene missing DoorWarningLightPresenter.");
                if (debugHud == null) errors.Add("RunVerticalSlice scene missing WaveDirectorDebugHud.");
                if (bootstrap != null) errors.Add("RunVerticalSlice should not depend on RunBootstrap.");

                if (waveDirector != null)
                {
                    SerializedObject waveSo = new SerializedObject(waveDirector);
                    if (waveSo.FindProperty("config")?.objectReferenceValue != config)
                        errors.Add("Scene WaveDirector config is not wired.");
                    if (waveSo.FindProperty("arena")?.objectReferenceValue == null)
                        errors.Add("Scene WaveDirector arena reference is not wired.");
                    if (waveSo.FindProperty("enemyManager")?.objectReferenceValue == null)
                        errors.Add("Scene WaveDirector enemyManager reference is not wired.");
                    if (waveSo.FindProperty("player")?.objectReferenceValue == null)
                        errors.Add("Scene WaveDirector player reference is not wired.");
                    if (waveSo.FindProperty("startRunOnEnable")?.boolValue != true)
                        errors.Add("Scene WaveDirector startRunOnEnable must be true.");
                }
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.path != scene.path && !string.IsNullOrEmpty(previousScene.path))
                    EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);
            }
        }

        static void ValidateHealingPrefab(List<string> errors)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(HealingPickupPrefabPath);
            try
            {
                if (contents.GetComponent<HealingPickup>() == null)
                    errors.Add("HealingPickup.prefab missing HealingPickup component.");

                Collider pickupCollider = contents.GetComponent<Collider>();
                if (pickupCollider == null)
                    errors.Add("HealingPickup.prefab missing Collider.");
                else if (!pickupCollider.isTrigger)
                    errors.Add("HealingPickup collider must be a trigger.");

                Rigidbody body = contents.GetComponent<Rigidbody>();
                if (body == null)
                    errors.Add("HealingPickup.prefab missing Rigidbody.");
                else if (!body.isKinematic || body.useGravity)
                    errors.Add("HealingPickup Rigidbody must be kinematic with gravity disabled.");

                if (contents.GetComponent<PoolIdentity>() == null)
                    errors.Add("HealingPickup.prefab missing PoolIdentity.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static GameObject CreateOrUpdateHealingPickupPrefab()
        {
            Material material = CreateMaterial("MP_Healing_Green", new Color(0.18f, 0.92f, 0.42f));
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "HealingPickup";

            try
            {
                SetLayerRecursive(root, LayerMask.NameToLayer("Pickup"));
                root.transform.localScale = new Vector3(0.55f, 0.18f, 0.55f);
                AssignMaterial(root, material);

                Collider pickupCollider = root.GetComponent<Collider>();
                pickupCollider.isTrigger = true;

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                HealingPickup pickup = root.AddComponent<HealingPickup>();
                pickup.Configure(2);
                root.AddComponent<PoolIdentity>();

                GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = "Glow";
                Object.DestroyImmediate(glow.GetComponent<Collider>());
                glow.transform.SetParent(root.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                glow.transform.localScale = Vector3.one * 0.45f;
                AssignMaterial(glow, material);

                PrefabUtility.SaveAsPrefabAsset(root, HealingPickupPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return LoadRequired<GameObject>(HealingPickupPrefabPath);
        }

        static GameObject CreateOrUpdateArenaPrefab()
        {
            Material floorMaterial = CreateMaterial("MP_Arena_Floor_Grey", new Color(0.35f, 0.37f, 0.39f));
            Material wallMaterial = CreateMaterial("MP_Arena_Wall_Grey", new Color(0.22f, 0.24f, 0.27f));
            Material doorMaterial = CreateMaterial("MP_Arena_Door_Orange", new Color(0.95f, 0.42f, 0.12f));
            Material padMaterial = CreateMaterial("MP_PickupPad_Blue", new Color(0.12f, 0.55f, 0.92f));
            Material centerMaterial = CreateMaterial("MP_Arena_Core_Cyan", new Color(0.1f, 0.85f, 0.95f));

            GameObject root = new GameObject("Arena");
            try
            {
                ArenaSystem arena = root.AddComponent<ArenaSystem>();
                arena.ConfigurePlayableBounds(Vector3.zero, new Vector3(36f, 2f, 28f));
                arena.ConfigurePlayerStart(Vector3.zero);
                arena.ConfigureWallSlamDamage(2, 5f);

                SerializedObject arenaSo = new SerializedObject(arena);
                SetBool(arenaSo, "createDefaultMapSpawnsWhenEmpty", false);
                SetFloat(arenaSo, "defaultScrapWallInset", 1f);
                SetFloat(arenaSo, "defaultScrapDoorMinDistance", 3f);
                arenaSo.ApplyModifiedPropertiesWithoutUndo();

                Transform visuals = new GameObject("Visuals").transform;
                visuals.SetParent(root.transform, false);
                Transform colliders = new GameObject("Colliders").transform;
                colliders.SetParent(root.transform, false);
                Transform doors = new GameObject("Doors").transform;
                doors.SetParent(root.transform, false);
                Transform pickupPads = new GameObject("PickupPads").transform;
                pickupPads.SetParent(root.transform, false);

                CreateColliderCube(colliders, "Ground", new Vector3(0f, -0.25f, 0f), new Vector3(36f, 0.5f, 28f), floorMaterial, "Ground");
                CreateWallSegments(colliders, wallMaterial);
                CreateCenterLandmark(visuals, centerMaterial);

                CreateDoor(doors, ArenaDoorId.North, new Vector3(0f, 0f, 14f), Vector3.back, doorMaterial);
                CreateDoor(doors, ArenaDoorId.South, new Vector3(0f, 0f, -14f), Vector3.forward, doorMaterial);
                CreateDoor(doors, ArenaDoorId.East, new Vector3(18f, 0f, 0f), Vector3.left, doorMaterial);
                CreateDoor(doors, ArenaDoorId.West, new Vector3(-18f, 0f, 0f), Vector3.right, doorMaterial);

                CreatePickupPad(pickupPads, "Pickup_NW", new Vector3(-12f, 0f, 8f), padMaterial);
                CreatePickupPad(pickupPads, "Pickup_NE", new Vector3(12f, 0f, 8f), padMaterial);
                CreatePickupPad(pickupPads, "Pickup_SW", new Vector3(-12f, 0f, -8f), padMaterial);
                CreatePickupPad(pickupPads, "Pickup_SE", new Vector3(12f, 0f, -8f), padMaterial);

                arena.RebuildSpawnCache();
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return LoadRequired<GameObject>(ArenaPrefabPath);
        }

        static WaveDirectorConfig CreateOrUpdateWaveDirectorConfig(GameObject healingPickupPrefab)
        {
            WaveDirectorConfig config = AssetDatabase.LoadAssetAtPath<WaveDirectorConfig>(WaveDirectorConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<WaveDirectorConfig>();
                AssetDatabase.CreateAsset(config, WaveDirectorConfigPath);
            }

            config.maxEnemiesAlive = 18;
            config.reinforcementThreshold = 2;
            config.baseRest = 2f;
            config.minRest = 0.55f;
            config.restReductionPerAct = 0.25f;
            config.warmupDuration = 2.5f;
            config.doorWarningTime = 0.75f;
            config.spawnIntervalRange = new Vector2(0.2f, 0.45f);
            config.doorPlayerMinDistance = 6f;
            config.maxSameDoorStreak = 2;
            config.underusedDoorBonus = 4f;
            config.sameDoorPenalty = 3f;
            config.playerCampingDoorPenalty = 6f;
            config.waveBudgetGrowth = 0.5f;

            config.acts = new List<WaveDirectorActConfig>
            {
                Act("Act 1 - Onboarding", 0f, 3, 5, 1, 2, 2f,
                    Enemy("Scrapling", ScraplingPrefabPath, ScraplingDefinitionPath, 1, 5f)),
                Act("Act 2 - Pressure", 90f, 5, 9, 2, 2, 1.8f,
                    Enemy("Scrapling", ScraplingPrefabPath, ScraplingDefinitionPath, 1, 4f),
                    Enemy("MetalEnemy", MetalEnemyPrefabPath, MetalEnemyDefinitionPath, 2, 2f)),
                Act("Act 3 - Mix", 210f, 8, 14, 2, 3, 1.6f,
                    Enemy("Scrapling", ScraplingPrefabPath, ScraplingDefinitionPath, 1, 4f),
                    Enemy("MetalEnemy", MetalEnemyPrefabPath, MetalEnemyDefinitionPath, 2, 3f),
                    Enemy("RunnerBot", RunnerBotPrefabPath, RunnerBotDefinitionPath, 3, 2f)),
                Act("Act 4 - Heavy Arrival", 360f, 12, 20, 3, 4, 1.3f,
                    Enemy("Scrapling", ScraplingPrefabPath, ScraplingDefinitionPath, 1, 3f),
                    Enemy("MetalEnemy", MetalEnemyPrefabPath, MetalEnemyDefinitionPath, 2, 3f),
                    Enemy("RunnerBot", RunnerBotPrefabPath, RunnerBotDefinitionPath, 3, 3f),
                    Enemy("HeavyBot", HeavyBotPrefabPath, HeavyBotDefinitionPath, 4, 1.4f)),
                Act("Act 5 - Storm", 540f, 16, 28, 4, 4, 1.1f,
                    Enemy("Scrapling", ScraplingPrefabPath, ScraplingDefinitionPath, 1, 3f),
                    Enemy("MetalEnemy", MetalEnemyPrefabPath, MetalEnemyDefinitionPath, 2, 3f),
                    Enemy("RunnerBot", RunnerBotPrefabPath, RunnerBotDefinitionPath, 3, 3f),
                    Enemy("HeavyBot", HeavyBotPrefabPath, HeavyBotDefinitionPath, 4, 2f))
            };

            config.scrapTypes = new List<ScrapTypeEntry>
            {
                Scrap("LightScrap", LightScrapPrefabPath, MagneticObjectType.LightScrap, 3f, 3f, 2f, 2f, 2f),
                Scrap("Plate", PlatePrefabPath, MagneticObjectType.Plate, 0.3f, 1f, 1.5f, 1.5f, 1.5f),
                Scrap("Mine", MinePrefabPath, MagneticObjectType.Mine, 0f, 0.3f, 0.6f, 0.8f, 0.8f),
                Scrap("Heavy", HeavyScrapPrefabPath, MagneticObjectType.Heavy, 0f, 0f, 0.3f, 0.6f, 0.8f)
            };

            config.scrapNearMinRadius = 4f;
            config.scrapNearMaxRadius = 10f;
            config.scrapPlayerMinDistance = 2.5f;
            config.scrapDoorMinDistance = 3f;
            config.scrapEnemyMinDistance = 1.5f;
            config.scrapWallInset = 1f;
            config.scrapMaxActive = 14;
            config.scrapAmmoFloorNearPlayer = 4;
            config.scrapAmmoFloorBonusPerAct = 1;
            config.scrapRefillCadence = 0.6f;
            config.scrapBurstPerRefill = 3;
            config.scrapSamplingAttempts = 16;

            config.healingPickupPrefab = healingPickupPrefab;
            config.healingSpawnCooldown = 30f;
            config.healingHpThreshold = 0.5f;
            config.healingMaxActive = 1;
            config.healingFirstSpawnDelay = 15f;

            EditorUtility.SetDirty(config);
            return config;
        }

        static void CreateOrUpdateRunScene(GameObject arenaPrefab, WaveDirectorConfig config)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject arenaObject = (GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab, scene);
            arenaObject.name = "Arena";
            arenaObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ArenaSystem arena = arenaObject.GetComponentInChildren<ArenaSystem>();
            if (arena != null)
                arena.RebuildSpawnCache();

            GameObject enemyManagerObject = new GameObject("EnemyManager");
            ArkhamEnemyManager enemyManager = enemyManagerObject.AddComponent<ArkhamEnemyManager>();

            GameObject playerPrefab = LoadRequired<GameObject>(PlayerPrefabPath);
            GameObject playerObject = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            playerObject.name = "MainCharacter";
            playerObject.transform.SetPositionAndRotation(arena != null ? arena.PlayerStart : Vector3.zero, Quaternion.identity);

            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            cameraObject.AddComponent<AudioListener>();
            ArkhamSimpleCameraFollow cameraFollow = cameraObject.AddComponent<ArkhamSimpleCameraFollow>();
            cameraFollow.Configure(playerObject.transform, new Vector3(0f, 14f, -8f));

            ConfigureScenePlayer(playerObject, camera, cameraFollow, enemyManager);

            GameObject runConfiguration = new GameObject("Run Configuration");
            WaveDirector waveDirector = runConfiguration.AddComponent<WaveDirector>();
            ScrapDirector scrapDirector = runConfiguration.AddComponent<ScrapDirector>();
            HealingDirector healingDirector = runConfiguration.AddComponent<HealingDirector>();
            RunController runController = runConfiguration.AddComponent<RunController>();
            DoorWarningLightPresenter warningPresenter = runConfiguration.AddComponent<DoorWarningLightPresenter>();
            WaveDirectorDebugHud debugHud = runConfiguration.AddComponent<WaveDirectorDebugHud>();

            CombatHealth playerHealth = playerObject.GetComponentInChildren<CombatHealth>();

            SerializedObject waveSo = new SerializedObject(waveDirector);
            SetObject(waveSo, "config", config);
            SetObject(waveSo, "arena", arena);
            SetObject(waveSo, "enemyManager", enemyManager);
            SetObject(waveSo, "player", playerObject.transform);
            SetBool(waveSo, "startRunOnEnable", true);
            waveSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject scrapSo = new SerializedObject(scrapDirector);
            SetObject(scrapSo, "waveDirector", waveDirector);
            SetObject(scrapSo, "config", config);
            SetObject(scrapSo, "arena", arena);
            SetObject(scrapSo, "player", playerObject.transform);
            SetLayerMask(scrapSo, "groundMask", LayerMask.GetMask("Ground"));
            SetFloat(scrapSo, "scrapGroundClearance", 0.03f);
            SetFloat(scrapSo, "groundProbeHeight", 8f);
            SetFloat(scrapSo, "groundProbeDistance", 20f);
            scrapSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject healingSo = new SerializedObject(healingDirector);
            SetObject(healingSo, "waveDirector", waveDirector);
            SetObject(healingSo, "config", config);
            SetObject(healingSo, "arena", arena);
            SetObject(healingSo, "player", playerObject.transform);
            SetObject(healingSo, "playerHealth", playerHealth);
            healingSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject runControllerSo = new SerializedObject(runController);
            SetObject(runControllerSo, "waveDirector", waveDirector);
            SetObject(runControllerSo, "scrapDirector", scrapDirector);
            SetObject(runControllerSo, "healingDirector", healingDirector);
            SetObject(runControllerSo, "playerHealth", playerHealth);
            SetBool(runControllerSo, "resolveFromBootstrap", false);
            runControllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject warningSo = new SerializedObject(warningPresenter);
            SetObject(warningSo, "waveDirector", waveDirector);
            SetObject(warningSo, "arena", arena);
            SetFloat(warningSo, "fallbackDuration", config.doorWarningTime);
            warningSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject hudSo = new SerializedObject(debugHud);
            SetBool(hudSo, "showOnStart", true);
            SetObject(hudSo, "waveDirector", waveDirector);
            SetObject(hudSo, "scrapDirector", scrapDirector);
            SetObject(hudSo, "healingDirector", healingDirector);
            SetObject(hudSo, "enemyManager", enemyManager);
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, RunScenePath);
        }

        static void ConfigureScenePlayer(
            GameObject playerObject,
            Camera camera,
            ArkhamSimpleCameraFollow cameraFollow,
            ArkhamEnemyManager enemyManager)
        {
            Animator animator = playerObject.GetComponentInChildren<Animator>();
            ArkhamPlayerMotor motor = playerObject.GetComponent<ArkhamPlayerMotor>();
            ArkhamTargetScanner scanner = playerObject.GetComponent<ArkhamTargetScanner>();
            ArkhamCombatController combat = playerObject.GetComponent<ArkhamCombatController>();
            MagnetismController magnetism = playerObject.GetComponent<MagnetismController>();
            OverloadController overload = playerObject.GetComponent<OverloadController>();
            CombatHealth health = playerObject.GetComponentInChildren<CombatHealth>();
            GameInputProvider inputProvider = GameInputProvider.EnsureOn(playerObject);

            inputProvider.Configure(camera);

            UnityEngine.InputSystem.PlayerInput playerInput = playerObject.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
                playerInput.camera = camera;

            if (motor != null)
                motor.Configure(camera, animator, 5f);

            Transform hitPoint = playerObject.transform.Find("Hit Point");
            if (hitPoint == null)
            {
                GameObject hitPointObject = new GameObject("Hit Point");
                hitPointObject.transform.SetParent(playerObject.transform, false);
                hitPointObject.transform.localPosition = new Vector3(0f, 0.9f, 0.75f);
                hitPoint = hitPointObject.transform;
            }

            if (combat != null)
                combat.Configure(enemyManager, scanner, motor, animator, cameraFollow, hitPoint);

            if (magnetism != null)
                magnetism.Configure(camera, enemyManager, motor);

            if (overload != null)
                overload.Configure(magnetism, motor, cameraFollow, enemyManager);

            DamageNumberSpawner[] damageSpawners = playerObject.GetComponentsInChildren<DamageNumberSpawner>(true);
            for (int i = 0; i < damageSpawners.Length; i++)
            {
                SerializedObject spawnerSo = new SerializedObject(damageSpawners[i]);
                SetObject(spawnerSo, "health", health);
                SetObject(spawnerSo, "cameraOverride", camera);
                spawnerSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void CreateWallSegments(Transform parent, Material wallMaterial)
        {
            float halfX = 18f;
            float halfZ = 14f;
            float doorHalf = 3.25f;
            float wallThickness = 0.5f;
            float wallHeight = 3f;

            float horizontalWidth = halfX - doorHalf;
            float horizontalCenterX = (doorHalf + halfX) * 0.5f;
            CreateColliderCube(parent, "Wall_North_East", new Vector3(horizontalCenterX, wallHeight * 0.5f, halfZ + wallThickness * 0.5f), new Vector3(horizontalWidth, wallHeight, wallThickness), wallMaterial, "ArenaWall");
            CreateColliderCube(parent, "Wall_North_West", new Vector3(-horizontalCenterX, wallHeight * 0.5f, halfZ + wallThickness * 0.5f), new Vector3(horizontalWidth, wallHeight, wallThickness), wallMaterial, "ArenaWall");
            CreateColliderCube(parent, "Wall_South_East", new Vector3(horizontalCenterX, wallHeight * 0.5f, -halfZ - wallThickness * 0.5f), new Vector3(horizontalWidth, wallHeight, wallThickness), wallMaterial, "ArenaWall");
            CreateColliderCube(parent, "Wall_South_West", new Vector3(-horizontalCenterX, wallHeight * 0.5f, -halfZ - wallThickness * 0.5f), new Vector3(horizontalWidth, wallHeight, wallThickness), wallMaterial, "ArenaWall");

            float verticalLength = halfZ - doorHalf;
            float verticalCenterZ = (doorHalf + halfZ) * 0.5f;
            CreateColliderCube(parent, "Wall_East_North", new Vector3(halfX + wallThickness * 0.5f, wallHeight * 0.5f, verticalCenterZ), new Vector3(wallThickness, wallHeight, verticalLength), wallMaterial, "ArenaWall");
            CreateColliderCube(parent, "Wall_East_South", new Vector3(halfX + wallThickness * 0.5f, wallHeight * 0.5f, -verticalCenterZ), new Vector3(wallThickness, wallHeight, verticalLength), wallMaterial, "ArenaWall");
            CreateColliderCube(parent, "Wall_West_North", new Vector3(-halfX - wallThickness * 0.5f, wallHeight * 0.5f, verticalCenterZ), new Vector3(wallThickness, wallHeight, verticalLength), wallMaterial, "ArenaWall");
            CreateColliderCube(parent, "Wall_West_South", new Vector3(-halfX - wallThickness * 0.5f, wallHeight * 0.5f, -verticalCenterZ), new Vector3(wallThickness, wallHeight, verticalLength), wallMaterial, "ArenaWall");
        }

        static void CreateCenterLandmark(Transform parent, Material material)
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            core.name = "Core_Landmark";
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(parent, false);
            core.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            core.transform.localScale = new Vector3(1.4f, 0.08f, 1.4f);
            AssignMaterial(core, material);
        }

        static void CreateDoor(Transform parent, ArenaDoorId id, Vector3 localPosition, Vector3 facingLocal, Material material)
        {
            GameObject doorObject = new GameObject("Door_" + id);
            doorObject.transform.SetParent(parent, false);
            doorObject.transform.localPosition = localPosition;

            ArenaDoor door = doorObject.AddComponent<ArenaDoor>();
            door.Configure(id, facingLocal, 1.5f, 1.5f);

            Transform exit = new GameObject("Exit").transform;
            exit.SetParent(doorObject.transform, false);
            exit.localPosition = facingLocal.normalized * 1.35f;

            Transform queue = new GameObject("Queue").transform;
            queue.SetParent(doorObject.transform, false);
            queue.localPosition = -facingLocal.normalized * 2f;

            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.transform.SetParent(doorObject.transform, false);
            frame.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            bool eastWest = id == ArenaDoorId.East || id == ArenaDoorId.West;
            frame.transform.localScale = eastWest ? new Vector3(0.32f, 2.3f, 5.8f) : new Vector3(5.8f, 2.3f, 0.32f);
            AssignMaterial(frame, material);

            Light warning = new GameObject("WarningLight").AddComponent<Light>();
            warning.transform.SetParent(doorObject.transform, false);
            warning.transform.localPosition = facingLocal.normalized * 0.7f + Vector3.up * 2.3f;
            warning.type = LightType.Point;
            warning.color = new Color(1f, 0.22f, 0.05f);
            warning.range = 8f;
            warning.intensity = 0f;
            warning.enabled = false;

            SerializedObject doorSo = new SerializedObject(door);
            SetObject(doorSo, "exit", exit);
            SetObject(doorSo, "queue", queue);
            SetObject(doorSo, "warningLight", warning);
            doorSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreatePickupPad(Transform parent, string name, Vector3 localPosition, Material material)
        {
            GameObject pad = new GameObject(name);
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = localPosition;
            SetLayerRecursive(pad, LayerMask.NameToLayer("Pickup"));

            ArenaSpawnPoint point = pad.AddComponent<ArenaSpawnPoint>();
            point.Configure(ArenaSpawnCategory.Pickup, 1f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "PadVisual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(pad.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            visual.transform.localScale = new Vector3(1.2f, 0.04f, 1.2f);
            AssignMaterial(visual, material);
        }

        static void CreateColliderCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, string layerName)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            SetLayerRecursive(cube, LayerMask.NameToLayer(layerName));
            AssignMaterial(cube, material);
        }

        static WaveDirectorActConfig Act(
            string name,
            float startTime,
            int baseBudget,
            int maxBudget,
            int doorsMin,
            int doorsMax,
            float restBase,
            params EnemySpawnEntry[] enemies)
        {
            WaveDirectorActConfig act = new WaveDirectorActConfig
            {
                actName = name,
                startTime = startTime,
                baseBudget = baseBudget,
                maxBudget = maxBudget,
                activeDoorsMin = doorsMin,
                activeDoorsMax = doorsMax,
                restBase = restBase,
                enemyPool = new List<EnemySpawnEntry>(enemies)
            };
            return act;
        }

        static EnemySpawnEntry Enemy(string label, string prefabPath, string definitionPath, int threatCost, float weight)
        {
            return new EnemySpawnEntry
            {
                label = label,
                prefab = LoadRequired<GameObject>(prefabPath),
                definition = LoadRequired<EnemyDefinition>(definitionPath),
                threatCost = threatCost,
                weight = weight
            };
        }

        static ScrapTypeEntry Scrap(string label, string prefabPath, MagneticObjectType type, params float[] weights)
        {
            return new ScrapTypeEntry
            {
                label = label,
                prefab = LoadRequired<GameObject>(prefabPath),
                type = type,
                weightPerAct = new List<float>(weights)
            };
        }

        static void HardenRuntimePrefabs()
        {
            SetPrefabLayer(PlayerPrefabPath, "Player", addPoolIdentity: false);

            SetPrefabLayer(ScraplingPrefabPath, "Enemy", addPoolIdentity: true);
            SetPrefabLayer(MetalEnemyPrefabPath, "Enemy", addPoolIdentity: true);
            SetPrefabLayer(RunnerBotPrefabPath, "Enemy", addPoolIdentity: true);
            SetPrefabLayer(HeavyBotPrefabPath, "Enemy", addPoolIdentity: true);

            SetPrefabLayer(LightScrapPrefabPath, "Attractable", addPoolIdentity: true);
            SetPrefabLayer(PlatePrefabPath, "Attractable", addPoolIdentity: true);
            SetPrefabLayer(MinePrefabPath, "Attractable", addPoolIdentity: true);
            SetPrefabLayer(HeavyScrapPrefabPath, "Attractable", addPoolIdentity: true);
            SetPrefabLayer(HealingPickupPrefabPath, "Pickup", addPoolIdentity: true);
        }

        static void DeleteGeneratedRunPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RunPrefabPath) != null)
                AssetDatabase.DeleteAsset(RunPrefabPath);
        }

        static void SetPrefabLayer(string path, string layerName, bool addPoolIdentity)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SetLayerRecursive(contents, LayerMask.NameToLayer(layerName));
                if (addPoolIdentity && contents.GetComponent<PoolIdentity>() == null)
                    contents.AddComponent<PoolIdentity>();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureProjectLayers()
        {
            EnsureLayer("Player", 6);
            EnsureLayer("Enemy", 7);
            EnsureLayer("Ground", 8);
            EnsureLayer("ArenaWall", 9);
            EnsureLayer("Attractable", 10);
            EnsureLayer("Pickup", 11);
        }

        static void EnsureLayer(string layerName, int preferredIndex)
        {
            if (LayerMask.NameToLayer(layerName) >= 0)
                return;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return;

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || layers.arraySize < 32)
                return;

            SerializedProperty preferred = layers.GetArrayElementAtIndex(preferredIndex);
            if (preferred != null && string.IsNullOrEmpty(preferred.stringValue))
            {
                preferred.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer == null || !string.IsNullOrEmpty(layer.stringValue))
                    continue;

                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        static Material CreateMaterial(string name, Color color)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                if (existing.HasProperty("_BaseColor"))
                    existing.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException("Required Unity asset not found.", path);
            return asset;
        }

        static void AssignMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        static void SetLayerRecursive(GameObject target, int layer)
        {
            if (target == null || layer < 0)
                return;

            target.layer = layer;
            for (int i = 0; i < target.transform.childCount; i++)
                SetLayerRecursive(target.transform.GetChild(i).gameObject, layer);
        }

        static void SetObject(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.objectReferenceValue = value;
        }

        static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.boolValue = value;
        }

        static void SetFloat(SerializedObject so, string property, float value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.floatValue = value;
        }

        static void SetLayerMask(SerializedObject so, string property, int value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
                return;

            SerializedProperty bits = p.FindPropertyRelative("m_Bits");
            if (bits != null)
                bits.intValue = value;
            else
                p.intValue = value;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
