using System.IO;
using MagnetPanic.Combat;
using UnityEditor;
using UnityEngine;

namespace MagnetPanic.Combat.Editor
{
    /// <summary>
    /// Builds the archetype enemy prefabs described in design/gdd/enemy-system.md.
    /// Each prefab has a swappable Model child — delete the Placeholder and drag in
    /// a 3D model under Model (keep local position 0,0,0). Components on the root
    /// (ArkhamEnemy, CharacterController, CombatHealth, definition) stay intact.
    /// </summary>
    public static class EnemyPrefabFactory
    {
        public const string EnemiesFolder = "Assets/Prefabs/Enemies/Combat";
        public const string DefinitionFolder = "Assets/Combat/Generated/EnemyDefinitions";
        public const string MaterialFolder = "Assets/Combat/Generated/Materials";

        const string ModelRootName = "Model";
        const string PlaceholderName = "Placeholder";
        const string VfxRootName = "VFX";
        const string CounterCueName = "CounterCue";
        const string ChargeTelegraphName = "ChargeTelegraph";
        const string HitPointName = "Hit Point";
        const string FirePointName = "FirePoint";
        const string ProjectilePrefabName = "EnemyProjectile";

        [MenuItem("Tools/Magnet Panic/Arkham Combat/Build Enemy Prefabs")]
        public static void BuildAllEnemyPrefabs()
        {
            EnsureFolder(EnemiesFolder);
            EnsureFolder(DefinitionFolder);
            EnsureFolder(MaterialFolder);

            Material scraplingMat = CreateMaterial("MP_Enemy_Red", new Color(0.9f, 0.18f, 0.1f));
            Material metalMat = CreateMaterial("MP_Metal_Enemy_Cyan", new Color(0.28f, 0.72f, 0.92f));
            Material runnerMat = CreateMaterial("MP_Runner_Orange", new Color(1f, 0.55f, 0.12f));
            Material heavyMat = CreateMaterial("MP_Heavy_Dark", new Color(0.18f, 0.2f, 0.22f));
            Material spitterMat = CreateMaterial("MP_Spitter_Purple", new Color(0.62f, 0.22f, 0.85f));
            Material counterMat = CreateMaterial("MP_Counter_Cyan", new Color(0.25f, 1f, 1f));
            Material telegraphMat = CreateMaterial("MP_Telegraph_Red", new Color(1f, 0.15f, 0.12f, 0.65f));
            Material projectileMat = CreateMaterial("MP_Projectile_Yellow", new Color(1f, 0.85f, 0.15f));

            // Build the projectile prefab first — SpitterDrone needs a reference to it
            GameObject projectilePrefab = BuildProjectilePrefab(projectileMat);

            BuildEnemy(
                "Scrapling",
                ScraplingDefinition(),
                scraplingMat,
                counterMat,
                telegraphMat,
                new Vector3(0.9f, 1.0f, 0.9f));

            BuildEnemy(
                "MetalEnemy",
                MetalEnemyDefinition(),
                metalMat,
                counterMat,
                telegraphMat,
                new Vector3(1.0f, 1.0f, 1.0f));

            BuildEnemy(
                "RunnerBot",
                RunnerBotDefinition(),
                runnerMat,
                counterMat,
                telegraphMat,
                new Vector3(0.85f, 1.0f, 0.85f));

            BuildEnemy(
                "HeavyBot",
                HeavyBotDefinition(),
                heavyMat,
                counterMat,
                telegraphMat,
                new Vector3(1.25f, 1.15f, 1.25f));

            EnemyDefinition spitterDef = SpitterDroneDefinition(projectilePrefab);
            BuildSpitterDrone(
                "SpitterDrone",
                spitterDef,
                spitterMat,
                counterMat,
                telegraphMat,
                projectilePrefab,
                new Vector3(0.8f, 0.9f, 0.8f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Enemy Prefabs",
                "Built Scrapling, MetalEnemy, RunnerBot, HeavyBot and SpitterDrone at " + EnemiesFolder + "\n\n" +
                "To swap a 3D model:\n" +
                "  1. Open the prefab\n" +
                "  2. Delete the 'Placeholder' child under 'Model'\n" +
                "  3. Drag your mesh under 'Model' (keep local position 0,0,0)\n\n" +
                "Tuning lives on the EnemyDefinition asset under " + DefinitionFolder + "\n" +
                "Changes to the definition apply on Awake.",
                "Nice");
        }

        static EnemyDefinition ScraplingDefinition()
        {
            EnemyDefinition def = GetOrCreateDefinition("Scrapling");
            def.archetype = EnemyArchetype.Scrapling;
            def.displayName = "Scrapling";
            def.maxHealth = 3;
            def.alwaysPullableByMagnet = false;
            def.magneticMarksToMagnetize = 2;
            def.magneticMass = 3f;
            def.approachSpeed = 5f;
            def.strafeSpeed = 1.25f;
            def.retreatSpeed = 2.25f;
            def.retreatDistance = 4.25f;
            def.disableStrafe = false;
            def.prepareAttackTime = 0.35f;
            def.attackRange = 1.8f;
            def.attackHitDelay = 0.2f;
            def.attackRecovery = 0.55f;
            def.knockbackDistance = 0.55f;
            def.knockbackDuration = 0.16f;
            def.useLinearCharge = false;
            EditorUtility.SetDirty(def);
            return def;
        }

        static EnemyDefinition MetalEnemyDefinition()
        {
            EnemyDefinition def = GetOrCreateDefinition("MetalEnemy");
            def.archetype = EnemyArchetype.MetalEnemy;
            def.displayName = "Metal Enemy";
            def.maxHealth = 5;
            def.alwaysPullableByMagnet = true;
            def.magneticMarksToMagnetize = 2;
            def.magneticMass = 2.2f;
            def.approachSpeed = 4f;
            def.strafeSpeed = 1f;
            def.retreatSpeed = 2f;
            def.retreatDistance = 4.25f;
            def.disableStrafe = false;
            def.prepareAttackTime = 0.4f;
            def.attackRange = 1.8f;
            def.attackHitDelay = 0.22f;
            def.attackRecovery = 0.6f;
            def.knockbackDistance = 0.55f;
            def.knockbackDuration = 0.16f;
            def.useLinearCharge = false;
            EditorUtility.SetDirty(def);
            return def;
        }

        static EnemyDefinition RunnerBotDefinition()
        {
            EnemyDefinition def = GetOrCreateDefinition("RunnerBot");
            def.archetype = EnemyArchetype.RunnerBot;
            def.displayName = "Runner Bot";
            def.maxHealth = 4;
            def.alwaysPullableByMagnet = false;
            def.magneticMarksToMagnetize = 2;
            def.magneticMass = 4f;
            def.approachSpeed = 7f;
            def.strafeSpeed = 0f;
            def.retreatSpeed = 3f;
            def.retreatDistance = 5.5f;
            def.disableStrafe = true;
            def.prepareAttackTime = 0.65f;
            def.attackRange = 6f;
            def.attackHitDelay = 0f;
            def.attackRecovery = 0.5f;
            def.knockbackDistance = 0.7f;
            def.knockbackDuration = 0.18f;
            def.useLinearCharge = true;
            def.chargeSpeed = 12f;
            def.chargeDuration = 0.55f;
            def.chargeHitRadius = 1.2f;
            EditorUtility.SetDirty(def);
            return def;
        }

        static EnemyDefinition HeavyBotDefinition()
        {
            EnemyDefinition def = GetOrCreateDefinition("HeavyBot");
            def.archetype = EnemyArchetype.HeavyBot;
            def.displayName = "Heavy Bot";
            def.maxHealth = 8;
            def.alwaysPullableByMagnet = false;
            def.magneticMarksToMagnetize = 3;
            def.magneticMass = 5f;
            def.approachSpeed = 3f;
            def.strafeSpeed = 0.8f;
            def.retreatSpeed = 1.6f;
            def.retreatDistance = 3.8f;
            def.disableStrafe = false;
            def.prepareAttackTime = 0.6f;
            def.attackRange = 2.2f;
            def.attackHitDelay = 0.28f;
            def.attackRecovery = 0.8f;
            def.knockbackDistance = 1.1f;
            def.knockbackDuration = 0.22f;
            def.useLinearCharge = false;
            def.useRangedAttack = false;
            def.canThrowScraps = true;
            def.scrapThrowChance = 0.4f;
            def.scrapThrowSpeed = 10f;
            EditorUtility.SetDirty(def);
            return def;
        }

        static EnemyDefinition SpitterDroneDefinition(GameObject projectilePrefab)
        {
            EnemyDefinition def = GetOrCreateDefinition("SpitterDrone");
            def.archetype = EnemyArchetype.SpitterDrone;
            def.displayName = "Spitter Drone";
            def.maxHealth = 4;
            def.alwaysPullableByMagnet = false;
            def.magneticMarksToMagnetize = 2;
            def.magneticMass = 2.5f;
            def.approachSpeed = 3.5f;
            def.strafeSpeed = 1.4f;
            def.retreatSpeed = 3f;
            def.retreatDistance = 6f;
            def.disableStrafe = false;
            def.prepareAttackTime = 0.5f;
            def.attackRange = 12f;
            def.attackHitDelay = 0f;
            def.attackRecovery = 0.65f;
            def.knockbackDistance = 0.55f;
            def.knockbackDuration = 0.16f;
            def.useLinearCharge = false;
            def.useRangedAttack = true;
            def.projectilePrefab = projectilePrefab;
            def.projectileSpeed = 10f;
            def.projectileDamage = 1;
            def.burstCount = 2;
            def.burstInterval = 0.3f;
            def.aimInaccuracy = 6f;
            def.rangedIdealDistance = 7f;
            def.rangedIdealTolerance = 1.5f;
            def.canThrowScraps = false;
            EditorUtility.SetDirty(def);
            return def;
        }

        static EnemyDefinition GetOrCreateDefinition(string archetypeName)
        {
            string path = DefinitionFolder + "/" + archetypeName + ".asset";
            EnemyDefinition existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null)
                return existing;

            EnemyDefinition asset = ScriptableObject.CreateInstance<EnemyDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void BuildEnemy(
            string prefabName,
            EnemyDefinition definition,
            Material bodyMaterial,
            Material counterMaterial,
            Material telegraphMaterial,
            Vector3 placeholderScale)
        {
            string prefabPath = EnemiesFolder + "/" + prefabName + ".prefab";

            GameObject root = new GameObject(prefabName);
            try
            {
                root.transform.position = Vector3.zero;

                CharacterController controller = root.AddComponent<CharacterController>();
                controller.center = new Vector3(0f, 1f, 0f);
                controller.height = 2f;
                controller.radius = 0.45f;
                controller.slopeLimit = 60f;
                controller.stepOffset = 0.3f;

                Animator animator = root.AddComponent<Animator>();
                animator.applyRootMotion = false;
                RuntimeAnimatorController controllerAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Combat/Generated/Animators/ArkhamPrototypeEnemy.controller");
                if (controllerAsset != null)
                    animator.runtimeAnimatorController = controllerAsset;

                CombatHealth health = root.AddComponent<CombatHealth>();
                health.Configure(definition.maxHealth, true);

                Transform modelRoot = new GameObject(ModelRootName).transform;
                modelRoot.SetParent(root.transform, false);

                GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                placeholder.name = PlaceholderName;
                Object.DestroyImmediate(placeholder.GetComponent<Collider>());
                placeholder.transform.SetParent(modelRoot, false);
                placeholder.transform.localPosition = new Vector3(0f, 1f, 0f);
                placeholder.transform.localScale = placeholderScale;
                AssignMaterial(placeholder, bodyMaterial);

                Transform vfxRoot = new GameObject(VfxRootName).transform;
                vfxRoot.SetParent(root.transform, false);

                GameObject counterCue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                counterCue.name = CounterCueName;
                Object.DestroyImmediate(counterCue.GetComponent<Collider>());
                counterCue.transform.SetParent(vfxRoot, false);
                counterCue.transform.localPosition = new Vector3(0f, 2.55f, 0f);
                counterCue.transform.localScale = Vector3.one * 0.28f;
                AssignMaterial(counterCue, counterMaterial);
                counterCue.SetActive(false);

                GameObject chargeTelegraph = null;
                if (definition.useLinearCharge)
                {
                    chargeTelegraph = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    chargeTelegraph.name = ChargeTelegraphName;
                    Object.DestroyImmediate(chargeTelegraph.GetComponent<Collider>());
                    chargeTelegraph.transform.SetParent(vfxRoot, false);
                    // lay flat on the ground in front of the enemy
                    chargeTelegraph.transform.localPosition = new Vector3(0f, 0.02f, 3f);
                    chargeTelegraph.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    chargeTelegraph.transform.localScale = new Vector3(0.9f, 6f, 1f);
                    AssignMaterial(chargeTelegraph, telegraphMaterial);
                    chargeTelegraph.SetActive(false);
                }

                Transform hitPoint = new GameObject(HitPointName).transform;
                hitPoint.SetParent(root.transform, false);
                hitPoint.localPosition = new Vector3(0f, 1.6f, 0.9f);

                ArkhamEnemy enemy = root.AddComponent<ArkhamEnemy>();
                WireEnemyReferences(enemy, definition, animator, controller, health, counterCue, chargeTelegraph);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void WireEnemyReferences(
            ArkhamEnemy enemy,
            EnemyDefinition definition,
            Animator animator,
            CharacterController controller,
            CombatHealth health,
            GameObject counterCue,
            GameObject chargeTelegraph)
        {
            SerializedObject so = new SerializedObject(enemy);

            SetObject(so, "definition", definition);
            SetObject(so, "animator", animator);
            SetObject(so, "characterController", controller);
            SetObject(so, "combatHealth", health);
            SetObject(so, "counterIndicator", counterCue);
            SetObject(so, "chargeTelegraph", chargeTelegraph);

            // Mirror definition values into the serialized fields so the prefab
            // ships with sane defaults visible in the inspector.
            SetInt(so, "maxHealth", definition.maxHealth);
            SetBool(so, "alwaysPullableByMagnet", definition.alwaysPullableByMagnet);
            SetInt(so, "magneticMarksToMagnetize", definition.magneticMarksToMagnetize);
            SetFloat(so, "magneticMass", definition.magneticMass);
            SetFloat(so, "approachSpeed", definition.approachSpeed);
            SetFloat(so, "strafeSpeed", definition.strafeSpeed);
            SetFloat(so, "retreatSpeed", definition.retreatSpeed);
            SetFloat(so, "retreatDistance", definition.retreatDistance);
            SetBool(so, "disableStrafe", definition.disableStrafe);
            SetFloat(so, "prepareAttackTime", definition.prepareAttackTime);
            SetFloat(so, "attackRange", definition.attackRange);
            SetFloat(so, "attackHitDelay", definition.attackHitDelay);
            SetFloat(so, "attackRecovery", definition.attackRecovery);
            SetFloat(so, "knockbackDistance", definition.knockbackDistance);
            SetFloat(so, "knockbackDuration", definition.knockbackDuration);
            SetBool(so, "useLinearCharge", definition.useLinearCharge);
            SetFloat(so, "chargeSpeed", definition.chargeSpeed);
            SetFloat(so, "chargeDuration", definition.chargeDuration);
            SetFloat(so, "chargeHitRadius", definition.chargeHitRadius);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetObject(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.objectReferenceValue = value;
        }

        static void SetInt(SerializedObject so, string property, int value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.intValue = value;
        }

        static void SetFloat(SerializedObject so, string property, float value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.floatValue = value;
        }

        static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.boolValue = value;
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

            Material material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void AssignMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        static GameObject BuildProjectilePrefab(Material projectileMaterial)
        {
            string prefabPath = EnemiesFolder + "/" + ProjectilePrefabName + ".prefab";

            // Return existing if already built
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
                return existing;

            GameObject root = new GameObject(ProjectilePrefabName);
            try
            {
                root.transform.position = Vector3.zero;

                // Model root for swappable visuals
                Transform modelRoot = new GameObject(ModelRootName).transform;
                modelRoot.SetParent(root.transform, false);

                // Placeholder sphere for the projectile
                GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                placeholder.name = PlaceholderName;
                Object.DestroyImmediate(placeholder.GetComponent<Collider>());
                placeholder.transform.SetParent(modelRoot, false);
                placeholder.transform.localScale = Vector3.one * 0.35f;
                AssignMaterial(placeholder, projectileMaterial);

                // Trigger collider for detection
                SphereCollider col = root.AddComponent<SphereCollider>();
                col.radius = 0.2f;
                col.isTrigger = true;

                // EnemyProjectile component
                EnemyProjectile projectile = root.AddComponent<EnemyProjectile>();
                SerializedObject so = new SerializedObject(projectile);
                SetObject(so, "modelRoot", modelRoot);
                SetFloat(so, "speed", 10f);
                SetFloat(so, "lifetime", 4f);
                SetFloat(so, "hitRadius", 0.5f);
                SetInt(so, "damage", 1);
                so.ApplyModifiedPropertiesWithoutUndo();

                // MagneticObject so the player can attract and repel it
                MagneticObject magneticObj = root.AddComponent<MagneticObject>();
                SerializedObject moSo = new SerializedObject(magneticObj);
                SetObject(moSo, "modelRoot", modelRoot);
                moSo.ApplyModifiedPropertiesWithoutUndo();
                magneticObj.ConfigurePrototype(MagneticObjectType.LightScrap, ~0);

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void BuildSpitterDrone(
            string prefabName,
            EnemyDefinition definition,
            Material bodyMaterial,
            Material counterMaterial,
            Material telegraphMaterial,
            GameObject projectilePrefab,
            Vector3 placeholderScale)
        {
            string prefabPath = EnemiesFolder + "/" + prefabName + ".prefab";

            GameObject root = new GameObject(prefabName);
            try
            {
                root.transform.position = Vector3.zero;

                CharacterController controller = root.AddComponent<CharacterController>();
                controller.center = new Vector3(0f, 1f, 0f);
                controller.height = 2f;
                controller.radius = 0.45f;
                controller.slopeLimit = 60f;
                controller.stepOffset = 0.3f;

                Animator animator = root.AddComponent<Animator>();
                animator.applyRootMotion = false;
                RuntimeAnimatorController controllerAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Combat/Generated/Animators/ArkhamPrototypeEnemy.controller");
                if (controllerAsset != null)
                    animator.runtimeAnimatorController = controllerAsset;

                CombatHealth health = root.AddComponent<CombatHealth>();
                health.Configure(definition.maxHealth, true);

                Transform modelRoot = new GameObject(ModelRootName).transform;
                modelRoot.SetParent(root.transform, false);

                GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                placeholder.name = PlaceholderName;
                Object.DestroyImmediate(placeholder.GetComponent<Collider>());
                placeholder.transform.SetParent(modelRoot, false);
                placeholder.transform.localPosition = new Vector3(0f, 1f, 0f);
                placeholder.transform.localScale = placeholderScale;
                AssignMaterial(placeholder, bodyMaterial);

                Transform vfxRoot = new GameObject(VfxRootName).transform;
                vfxRoot.SetParent(root.transform, false);

                GameObject counterCue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                counterCue.name = CounterCueName;
                Object.DestroyImmediate(counterCue.GetComponent<Collider>());
                counterCue.transform.SetParent(vfxRoot, false);
                counterCue.transform.localPosition = new Vector3(0f, 2.55f, 0f);
                counterCue.transform.localScale = Vector3.one * 0.28f;
                AssignMaterial(counterCue, counterMaterial);
                counterCue.SetActive(false);

                // Fire point for projectile origin
                Transform firePoint = new GameObject(FirePointName).transform;
                firePoint.SetParent(root.transform, false);
                firePoint.localPosition = new Vector3(0f, 1.2f, 0.7f);

                Transform hitPoint = new GameObject(HitPointName).transform;
                hitPoint.SetParent(root.transform, false);
                hitPoint.localPosition = new Vector3(0f, 1.6f, 0.9f);

                ArkhamEnemy enemy = root.AddComponent<ArkhamEnemy>();
                WireEnemyReferences(enemy, definition, animator, controller, health, counterCue, null);

                // SpitterDroneBehavior component
                SpitterDroneBehavior spitter = root.AddComponent<SpitterDroneBehavior>();
                SerializedObject spitterSo = new SerializedObject(spitter);
                SetObject(spitterSo, "enemy", enemy);
                SetObject(spitterSo, "firePoint", firePoint);
                SetObject(spitterSo, "projectilePrefab", projectilePrefab);
                SetFloat(spitterSo, "projectileSpeed", definition.projectileSpeed);
                SetInt(spitterSo, "projectileDamage", definition.projectileDamage);
                SetInt(spitterSo, "burstCount", definition.burstCount);
                SetFloat(spitterSo, "burstInterval", definition.burstInterval);
                SetFloat(spitterSo, "aimInaccuracy", definition.aimInaccuracy);
                SetFloat(spitterSo, "idealDistance", definition.rangedIdealDistance);
                SetFloat(spitterSo, "idealTolerance", definition.rangedIdealTolerance);
                spitterSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
