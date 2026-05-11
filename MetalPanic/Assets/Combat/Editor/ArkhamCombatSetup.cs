using System.IO;
using MagnetPanic.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat.Editor
{
    public static class ArkhamCombatSetup
    {
        const string GeneratedFolder = "Assets/Combat/Generated";
        const string MaterialFolder = GeneratedFolder + "/Materials";
        const string AnimatorFolder = GeneratedFolder + "/Animators";
        const string UIFolder = GeneratedFolder + "/UI";
        const string PlayerControllerPath = AnimatorFolder + "/ArkhamPrototypePlayer.controller";
        const string EnemyControllerPath = AnimatorFolder + "/ArkhamPrototypeEnemy.controller";
        const string PanelSettingsPath = UIFolder + "/MP_RuntimePanelSettings.asset";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string MapPrefabPath = "Assets/Prefabs/Map.prefab";

        [MenuItem("Tools/Magnet Panic/Arkham Combat/Create Prototype Animators")]
        public static void CreatePrototypeAnimators()
        {
            EnsureFolder(GeneratedFolder);
            EnsureFolder(AnimatorFolder);

            CreatePlayerController();
            CreateEnemyController();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Magnet Panic/Arkham Combat/Create Demo In Current Scene")]
        public static void CreateDemoInCurrentScene()
        {
            CreatePrototypeAnimators();
            EnsureFolder(MaterialFolder);

            Material playerMaterial = CreateMaterial("MP_Player_Blue", new Color(0.1f, 0.38f, 0.95f));
            Material enemyMaterial = CreateMaterial("MP_Enemy_Red", new Color(0.9f, 0.18f, 0.1f));
            Material metalEnemyMaterial = CreateMaterial("MP_Metal_Enemy_Cyan", new Color(0.28f, 0.72f, 0.92f));
            Material magnetizedMaterial = CreateMaterial("MP_Magnetized_Yellow", new Color(1f, 0.82f, 0.16f));
            Material arenaMaterial = CreateMaterial("MP_Arena_Grey", new Color(0.42f, 0.44f, 0.46f));
            Material counterMaterial = CreateMaterial("MP_Counter_Cyan", new Color(0.25f, 1f, 1f));
            Material scrapMaterial = CreateMaterial("MP_Scrap_Steel", new Color(0.52f, 0.6f, 0.64f));
            Material plateMaterial = CreateMaterial("MP_Plate_Teal", new Color(0.15f, 0.55f, 0.6f));
            Material mineMaterial = CreateMaterial("MP_Mine_Orange", new Color(1f, 0.42f, 0.08f));
            Material heavyMaterial = CreateMaterial("MP_Heavy_Dark", new Color(0.18f, 0.2f, 0.22f));
            Material healingMaterial = CreateMaterial("MP_Healing_Green", new Color(0.18f, 0.92f, 0.42f));

            GameObject root = new GameObject("Arkham Combat Demo");
            Undo.RegisterCreatedObjectUndo(root, "Create Arkham Combat Demo");

            CreateArena(root.transform, arenaMaterial);
            CreateMagneticScrap(root.transform, scrapMaterial, plateMaterial, mineMaterial, heavyMaterial);
            CreateHealingPickups(root.transform, healingMaterial);

            GameObject cameraObject = new GameObject("Combat Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Combat Camera");
            cameraObject.transform.SetParent(root.transform);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            cameraObject.AddComponent<AudioListener>();
            ArkhamSimpleCameraFollow cameraFollow = cameraObject.AddComponent<ArkhamSimpleCameraFollow>();

            GameObject player = CreatePlayer(root.transform, camera, cameraFollow, playerMaterial);
            cameraFollow.Configure(player.transform, new Vector3(0f, 10f, -8f));

            GameObject enemyRoot = new GameObject("Enemy Manager");
            Undo.RegisterCreatedObjectUndo(enemyRoot, "Create Enemy Manager");
            enemyRoot.transform.SetParent(root.transform);
            ArkhamEnemyManager enemyManager = enemyRoot.AddComponent<ArkhamEnemyManager>();

            ArkhamCombatController playerCombat = player.GetComponent<ArkhamCombatController>();
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 5f, 1f, Mathf.Sin(angle) * 5f);
                CreateEnemy(enemyRoot.transform, enemyManager, playerCombat, position, enemyMaterial, magnetizedMaterial, counterMaterial);
            }

            CreateEnemy(
                enemyRoot.transform,
                enemyManager,
                playerCombat,
                new Vector3(0f, 1f, 3.4f),
                metalEnemyMaterial,
                magnetizedMaterial,
                counterMaterial,
                "Metal Enemy",
                true,
                2.2f);

            playerCombat.Configure(
                enemyManager,
                player.GetComponent<ArkhamTargetScanner>(),
                player.GetComponent<ArkhamPlayerMotor>(),
                player.GetComponent<Animator>(),
                cameraFollow,
                player.transform.Find("Hit Point"));

            player.GetComponent<MagnetismController>()?.Configure(camera, enemyManager, player.GetComponent<ArkhamPlayerMotor>());

            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog(
                "Arkham Combat Demo",
                "Created a URP-safe prototype setup. Press Play, use WASD to move, left click to Pull, left click again to Repel, right click to strike, and Space to counter.",
                "Nice");
        }

        [MenuItem("Tools/Magnet Panic/Arkham Combat/Spawn Metal Enemy In Current Scene")]
        public static void SpawnMetalEnemyInCurrentScene()
        {
            CreatePrototypeAnimators();
            EnsureFolder(MaterialFolder);

            ArkhamEnemyManager enemyManager = Object.FindFirstObjectByType<ArkhamEnemyManager>();
            ArkhamCombatController playerCombat = Object.FindFirstObjectByType<ArkhamCombatController>();

            if (enemyManager == null)
            {
                GameObject enemyRoot = new GameObject("Enemy Manager");
                Undo.RegisterCreatedObjectUndo(enemyRoot, "Create Enemy Manager");
                enemyManager = enemyRoot.AddComponent<ArkhamEnemyManager>();
            }

            Material metalEnemyMaterial = CreateMaterial("MP_Metal_Enemy_Cyan", new Color(0.28f, 0.72f, 0.92f));
            Material magnetizedMaterial = CreateMaterial("MP_Magnetized_Yellow", new Color(1f, 0.82f, 0.16f));
            Material counterMaterial = CreateMaterial("MP_Counter_Cyan", new Color(0.25f, 1f, 1f));

            Vector3 position = playerCombat != null
                ? playerCombat.transform.position + playerCombat.transform.forward * 3.4f
                : new Vector3(0f, 0f, 3.4f);
            position.y = 1f;

            GameObject metalEnemy = CreateEnemy(
                enemyManager.transform,
                enemyManager,
                playerCombat,
                position,
                metalEnemyMaterial,
                magnetizedMaterial,
                counterMaterial,
                "Metal Enemy",
                true,
                2.2f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = metalEnemy;
            EditorUtility.DisplayDialog(
                "Metal Enemy",
                "Spawned a metal enemy. Press Play and left click Pull to attract it immediately.",
                "Nice");
        }

        [MenuItem("Tools/Magnet Panic/Arkham Combat/Spawn Healing Pickups In Current Scene")]
        public static void SpawnHealingPickupsInCurrentScene()
        {
            EnsureFolder(MaterialFolder);
            Material healingMaterial = CreateMaterial("MP_Healing_Green", new Color(0.18f, 0.92f, 0.42f));

            Transform parent = Object.FindFirstObjectByType<ArkhamCombatController>()?.transform.parent;
            if (parent == null)
            {
                GameObject root = new GameObject("Healing Pickups");
                Undo.RegisterCreatedObjectUndo(root, "Create Healing Pickups Root");
                parent = root.transform;
            }

            CreateHealingPickups(parent, healingMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Healing Pickups",
                "Spawned prototype healing pickups. They heal the player through CombatHealth.",
                "Nice");
        }

        static GameObject CreatePlayer(Transform parent, Camera sceneCamera, ArkhamSimpleCameraFollow cameraFollow, Material material)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(player, "Create Combat Player");
            player.name = "Arkham Combat Player";
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(0f, 1f, 0f);
            AssignMaterial(player, material);
            Object.DestroyImmediate(player.GetComponent<Collider>());

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.zero;
            controller.height = 2f;
            controller.radius = 0.45f;

            Animator animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);

            CombatHealth health = player.AddComponent<CombatHealth>();
            ArkhamPlayerMotor motor = player.AddComponent<ArkhamPlayerMotor>();
            ArkhamTargetScanner scanner = player.AddComponent<ArkhamTargetScanner>();
            ArkhamCombatController combat = player.AddComponent<ArkhamCombatController>();
            MagnetismController magnetism = player.AddComponent<MagnetismController>();
            GameInputProvider inputProvider = player.AddComponent<GameInputProvider>();

            GameObject hitPoint = new GameObject("Hit Point");
            hitPoint.transform.SetParent(player.transform);
            hitPoint.transform.localPosition = new Vector3(0f, 0.9f, 0.75f);

            PlayerInput input = player.AddComponent<PlayerInput>();
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions != null)
            {
                input.actions = actions;
                input.defaultActionMap = "Player";
                input.notificationBehavior = PlayerNotifications.SendMessages;
            }

            health.Configure(6, true);
            inputProvider.Configure(sceneCamera);
            motor.Configure(sceneCamera, animator, 5f);
            combat.Configure(null, scanner, motor, animator, cameraFollow, hitPoint.transform);
            magnetism.Configure(sceneCamera, null, motor);
            CreatePlayerHud(player.transform, health);

            return player;
        }

        static GameObject CreateEnemy(
            Transform parent,
            ArkhamEnemyManager manager,
            ArkhamCombatController playerCombat,
            Vector3 position,
            Material enemyMaterial,
            Material magnetizedMaterial,
            Material counterMaterial,
            string enemyName = "Arkham Enemy",
            bool alwaysPullableByMagnet = false,
            float magneticMass = 3f)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(enemy, "Create Combat Enemy");
            enemy.name = enemyName;
            enemy.transform.SetParent(parent);
            enemy.transform.position = position;
            AssignMaterial(enemy, enemyMaterial);
            Object.DestroyImmediate(enemy.GetComponent<Collider>());

            CharacterController controller = enemy.AddComponent<CharacterController>();
            controller.center = Vector3.zero;
            controller.height = 2f;
            controller.radius = 0.45f;

            Animator animator = enemy.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyControllerPath);

            CombatHealth health = enemy.AddComponent<CombatHealth>();
            health.Configure(3, true);

            GameObject counterCue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(counterCue, "Create Counter Cue");
            counterCue.name = "Counter Cue";
            counterCue.transform.SetParent(enemy.transform);
            counterCue.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            counterCue.transform.localScale = Vector3.one * 0.28f;
            AssignMaterial(counterCue, counterMaterial);
            Object.DestroyImmediate(counterCue.GetComponent<Collider>());
            counterCue.SetActive(false);

            ArkhamEnemy arkhamEnemy = enemy.AddComponent<ArkhamEnemy>();
            arkhamEnemy.Configure(manager, playerCombat, animator, counterCue);
            arkhamEnemy.ConfigureMagneticProfile(alwaysPullableByMagnet, magneticMass);
            arkhamEnemy.OnMagnetized.AddListener(target => AssignMaterial(target.gameObject, magnetizedMaterial));

            manager.Register(arkhamEnemy);
            return enemy;
        }

        static void CreatePlayerHud(Transform player, CombatHealth health)
        {
            GameObject hudObject = new GameObject("Player Health HUD");
            Undo.RegisterCreatedObjectUndo(hudObject, "Create Player Health HUD");
            hudObject.transform.SetParent(player, false);

            UIDocument document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = GetOrCreatePanelSettings();

            PlayerHealthHud hud = hudObject.AddComponent<PlayerHealthHud>();
            hud.Configure(health);
        }

        static void CreateArena(Transform parent, Material material)
        {
            GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
            if (mapPrefab != null)
            {
                GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, parent);
                Undo.RegisterCreatedObjectUndo(map, "Create Map Arena");
                map.name = "Map";
                map.transform.localPosition = new Vector3(26.29f, -4.38f, -7.2f);
                map.transform.localRotation = Quaternion.identity;
                map.transform.localScale = Vector3.one;

                if (map.GetComponent<ArenaSystem>() == null)
                    map.AddComponent<ArenaSystem>();

                if (map.GetComponent<ArenaMapColliderBuilder>() == null)
                    map.AddComponent<ArenaMapColliderBuilder>();

                return;
            }

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(floor, "Create Combat Arena");
            floor.name = "Prototype Arena";
            floor.transform.SetParent(parent);
            floor.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            AssignMaterial(floor, material);
            ArenaSystem arena = floor.AddComponent<ArenaSystem>();
            arena.ConfigurePlayableBounds(Vector3.zero, new Vector3(16f, 2f, 16f));
        }

        static void CreateMagneticScrap(
            Transform parent,
            Material scrapMaterial,
            Material plateMaterial,
            Material mineMaterial,
            Material heavyMaterial)
        {
            CreateMagneticObject(parent, "Light Scrap A", MagneticObjectType.LightScrap, new Vector3(-3.5f, 0.45f, 2.2f), new Vector3(0.42f, 0.28f, 0.72f), PrimitiveType.Cube, scrapMaterial);
            CreateMagneticObject(parent, "Light Scrap B", MagneticObjectType.LightScrap, new Vector3(3.2f, 0.45f, -1.8f), new Vector3(0.35f, 0.35f, 0.62f), PrimitiveType.Cube, scrapMaterial);
            CreateMagneticObject(parent, "Light Scrap C", MagneticObjectType.LightScrap, new Vector3(1.2f, 0.45f, 4.2f), new Vector3(0.5f, 0.22f, 0.5f), PrimitiveType.Cube, scrapMaterial);
            CreateMagneticObject(parent, "Metal Plate A", MagneticObjectType.Plate, new Vector3(-5f, 0.45f, -1.4f), new Vector3(1.2f, 0.16f, 0.7f), PrimitiveType.Cube, plateMaterial);
            CreateMagneticObject(parent, "Metal Plate B", MagneticObjectType.Plate, new Vector3(4.8f, 0.45f, 2.7f), new Vector3(1.2f, 0.16f, 0.7f), PrimitiveType.Cube, plateMaterial);
            CreateMagneticObject(parent, "Magnetic Mine", MagneticObjectType.Mine, new Vector3(-1.1f, 0.45f, -4.3f), new Vector3(0.62f, 0.62f, 0.62f), PrimitiveType.Sphere, mineMaterial);
            CreateMagneticObject(parent, "Heavy Scrap", MagneticObjectType.Heavy, new Vector3(5.5f, 0.55f, -4.5f), new Vector3(0.9f, 0.9f, 0.9f), PrimitiveType.Cube, heavyMaterial);
        }

        static void CreateHealingPickups(Transform parent, Material healingMaterial)
        {
            CreateHealingPickup(parent, "Healing Pickup A", new Vector3(-2.2f, 0.55f, -2.6f), healingMaterial, 2);
            CreateHealingPickup(parent, "Healing Pickup B", new Vector3(2.4f, 0.55f, 2.1f), healingMaterial, 2);
        }

        static void CreateHealingPickup(Transform parent, string name, Vector3 position, Material material, int healAmount)
        {
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(pickup, "Create Healing Pickup");
            pickup.name = name;
            pickup.transform.SetParent(parent);
            pickup.transform.position = position;
            pickup.transform.localScale = Vector3.one * 0.42f;
            AssignMaterial(pickup, material);

            Collider pickupCollider = pickup.GetComponent<Collider>();
            pickupCollider.isTrigger = true;

            HealingPickup healingPickup = pickup.AddComponent<HealingPickup>();
            healingPickup.Configure(healAmount);
        }

        static void CreateMagneticObject(
            Transform parent,
            string name,
            MagneticObjectType type,
            Vector3 position,
            Vector3 scale,
            PrimitiveType primitive,
            Material material)
        {
            GameObject magneticObject = GameObject.CreatePrimitive(primitive);
            Undo.RegisterCreatedObjectUndo(magneticObject, "Create Magnetic Object");
            magneticObject.name = name;
            magneticObject.transform.SetParent(parent);
            magneticObject.transform.position = position;
            magneticObject.transform.localScale = scale;
            AssignMaterial(magneticObject, material);

            Rigidbody body = magneticObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = Mathf.Max(1f, scale.magnitude);
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            MagneticObject magnet = magneticObject.AddComponent<MagneticObject>();
            LayerMask allLayers = ~0;
            magnet.ConfigurePrototype(type, allLayers);
        }

        static void CreatePlayerController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller != null)
                return;

            controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);

            AddFloat(controller, "InputMagnitude");
            AddTrigger(controller, "GroundPunch");
            AddTrigger(controller, "AirKick");
            AddTrigger(controller, "AirKick2");
            AddTrigger(controller, "AirPunch");
            AddTrigger(controller, "AirKick3");
            AddTrigger(controller, "Dodge");
            AddTrigger(controller, "Hit");

            AnimatorState idle = controller.layers[0].stateMachine.AddState("Idle");
            idle.motion = CreateClip("Player_Idle", false, 1f, 1f);
            controller.layers[0].stateMachine.defaultState = idle;

            AddTriggeredState(controller, "GroundPunch", "GroundPunch", CreateClip("Player_GroundPunch", true, 1.1f, 0.88f));
            AddTriggeredState(controller, "AirKick", "AirKick", CreateClip("Player_AirKick", true, 0.82f, 1.16f));
            AddTriggeredState(controller, "AirKick2", "AirKick2", CreateClip("Player_AirKick2", true, 1.18f, 0.9f));
            AddTriggeredState(controller, "AirPunch", "AirPunch", CreateClip("Player_AirPunch", true, 0.9f, 1.2f));
            AddTriggeredState(controller, "AirKick3", "AirKick3", CreateClip("Player_AirKick3", true, 1.15f, 0.85f));
            AddTriggeredState(controller, "Dodge", "Dodge", CreateClip("Player_Dodge", true, 1.05f, 0.78f));
            AddTriggeredState(controller, "Hit", "Hit", CreateClip("Player_Hit", true, 1.22f, 0.78f));
        }

        static void CreateEnemyController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            if (controller != null)
                return;

            controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);

            AddFloat(controller, "InputMagnitude");
            AddFloat(controller, "StrafeDirection");
            AddBool(controller, "Strafe");
            AddTrigger(controller, "Hit");
            AddTrigger(controller, "Death");
            AddTrigger(controller, "AirPunch");

            AnimatorState idle = controller.layers[0].stateMachine.AddState("Idle");
            idle.motion = CreateClip("Enemy_Idle", false, 1f, 1f);
            controller.layers[0].stateMachine.defaultState = idle;

            AddTriggeredState(controller, "AirPunch", "AirPunch", CreateClip("Enemy_AirPunch", true, 0.85f, 1.12f));
            AddTriggeredState(controller, "Hit", "Hit", CreateClip("Enemy_Hit", true, 1.22f, 0.82f));
            AddTriggeredState(controller, "Death", "Death", CreateClip("Enemy_Death", false, 0.8f, 0.35f));
        }

        static void AddTriggeredState(AnimatorController controller, string stateName, string triggerName, Motion motion)
        {
            AnimatorState state = controller.layers[0].stateMachine.AddState(stateName);
            state.motion = motion;

            AnimatorStateTransition transition = controller.layers[0].stateMachine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0.03f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            AnimatorStateTransition exit = state.AddExitTransition();
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.05f;
        }

        static AnimationClip CreateClip(string name, bool oneShot, float midScale, float endScale)
        {
            string clipPath = AnimatorFolder + "/" + name + ".anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
                return existing;

            AnimationClip clip = new AnimationClip
            {
                name = name,
                frameRate = 30f,
                wrapMode = oneShot ? WrapMode.Once : WrapMode.Loop
            };

            AnimationCurve scaleX = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.12f, midScale),
                new Keyframe(0.32f, endScale),
                new Keyframe(0.48f, 1f));

            AnimationCurve scaleY = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.12f, Mathf.Max(0.35f, 2f - midScale)),
                new Keyframe(0.32f, Mathf.Max(0.35f, 2f - endScale)),
                new Keyframe(0.48f, 1f));

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.x"), scaleX);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.y"), scaleY);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.z"), scaleX);

            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        static Material CreateMaterial(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader)
            {
                color = color
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static PanelSettings GetOrCreatePanelSettings()
        {
            EnsureFolder(UIFolder);

            PanelSettings existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
                return existing;

            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            return panelSettings;
        }

        static void AssignMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        static void AddTrigger(AnimatorController controller, string name)
        {
            if (!HasParameter(controller, name))
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        static void AddFloat(AnimatorController controller, string name)
        {
            if (!HasParameter(controller, name))
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        static void AddBool(AnimatorController controller, string name)
        {
            if (!HasParameter(controller, name))
                controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        }

        static bool HasParameter(AnimatorController controller, string name)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name)
                    return true;
            }

            return false;
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
