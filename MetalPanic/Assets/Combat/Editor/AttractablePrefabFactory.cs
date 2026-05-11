using System.IO;
using MagnetPanic.Combat;
using UnityEditor;
using UnityEngine;

namespace MagnetPanic.Combat.Editor
{
    public static class AttractablePrefabFactory
    {
        public const string AttractablesFolder = "Assets/Prefabs/Attractables";
        public const string MaterialFolder = "Assets/Combat/Generated/Materials";

        const string ModelRootName = "Model";
        const string PlaceholderName = "Placeholder";
        const string VfxRootName = "VFX";
        const string OrbitParticleName = "OrbitGlow";
        const string ImpactParticleName = "ImpactBurst";
        const string TrailName = "Trail";

        [MenuItem("Tools/Magnet Panic/Arkham Combat/Build Attractable Prefabs")]
        public static void BuildAllAttractablePrefabs()
        {
            EnsureFolder(AttractablesFolder);
            EnsureFolder(MaterialFolder);

            BuildPrefab(
                MagneticObjectType.LightScrap,
                "LightScrap_Attractable",
                CreateMaterial("MP_Scrap_Steel", new Color(0.52f, 0.6f, 0.64f)),
                PrimitiveType.Cube,
                new Vector3(0.42f, 0.28f, 0.42f),
                useSphereCollider: false);

            BuildPrefab(
                MagneticObjectType.Plate,
                "Plate_Attractable",
                CreateMaterial("MP_Plate_Teal", new Color(0.15f, 0.55f, 0.6f)),
                PrimitiveType.Cube,
                new Vector3(1.1f, 0.16f, 0.7f),
                useSphereCollider: false);

            BuildPrefab(
                MagneticObjectType.Mine,
                "Mine_Attractable",
                CreateMaterial("MP_Mine_Orange", new Color(1f, 0.42f, 0.08f)),
                PrimitiveType.Sphere,
                Vector3.one * 0.62f,
                useSphereCollider: true);

            BuildPrefab(
                MagneticObjectType.Heavy,
                "Heavy_Attractable",
                CreateMaterial("MP_Heavy_Dark", new Color(0.18f, 0.2f, 0.22f)),
                PrimitiveType.Cube,
                Vector3.one * 0.9f,
                useSphereCollider: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Attractable Prefabs",
                "Built LightScrap, Plate, Mine and Heavy prefabs at " + AttractablesFolder + "\n\n" +
                "To swap a 3D model:\n" +
                "  1. Open the prefab\n" +
                "  2. Delete the 'Placeholder' child under 'Model'\n" +
                "  3. Drag your mesh under 'Model' (keep local position 0,0,0)\n\n" +
                "Colliders, Rigidbody and VFX wiring live on the root and stay intact.",
                "Nice");
        }

        static void BuildPrefab(
            MagneticObjectType type,
            string prefabName,
            Material material,
            PrimitiveType placeholderPrimitive,
            Vector3 placeholderScale,
            bool useSphereCollider)
        {
            string prefabPath = AttractablesFolder + "/" + prefabName + ".prefab";

            GameObject root = new GameObject(prefabName);
            try
            {
                root.transform.position = Vector3.zero;

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.mass = Mathf.Max(1f, placeholderScale.magnitude);
                body.linearDamping = 0.05f;
                body.angularDamping = 0.5f;

                if (useSphereCollider)
                {
                    SphereCollider sphere = root.AddComponent<SphereCollider>();
                    sphere.radius = Mathf.Max(0.1f, placeholderScale.x * 0.5f);
                }
                else
                {
                    BoxCollider box = root.AddComponent<BoxCollider>();
                    box.size = placeholderScale;
                }

                Transform modelRoot = new GameObject(ModelRootName).transform;
                modelRoot.SetParent(root.transform, false);

                GameObject placeholder = GameObject.CreatePrimitive(placeholderPrimitive);
                placeholder.name = PlaceholderName;
                Object.DestroyImmediate(placeholder.GetComponent<Collider>());
                placeholder.transform.SetParent(modelRoot, false);
                placeholder.transform.localPosition = Vector3.zero;
                placeholder.transform.localScale = placeholderScale;
                AssignMaterial(placeholder, material);

                Transform vfxRoot = new GameObject(VfxRootName).transform;
                vfxRoot.SetParent(root.transform, false);

                ParticleSystem orbitParticle = CreateOrbitParticle(vfxRoot, material.color);
                ParticleSystem impactParticle = CreateImpactParticle(vfxRoot, material.color);
                TrailRenderer trail = CreateTrail(vfxRoot, material.color);

                MagneticObject magnetic = root.AddComponent<MagneticObject>();
                magnetic.ConfigurePrototype(type, ~0);
                AssignMagneticReferences(magnetic, modelRoot, orbitParticle, impactParticle, trail);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static ParticleSystem CreateOrbitParticle(Transform parent, Color color)
        {
            GameObject particleObject = new GameObject(OrbitParticleName);
            particleObject.transform.SetParent(parent, false);

            ParticleSystem particle = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.duration = 1.5f;
            main.loop = true;
            main.startLifetime = 0.4f;
            main.startSize = 0.18f;
            main.startSpeed = 0.4f;
            main.startColor = new Color(color.r, color.g, color.b, 0.65f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = 12f;

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particle;
        }

        static ParticleSystem CreateImpactParticle(Transform parent, Color color)
        {
            GameObject particleObject = new GameObject(ImpactParticleName);
            particleObject.transform.SetParent(parent, false);

            ParticleSystem particle = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSize = 0.22f;
            main.startSpeed = 4f;
            main.startColor = new Color(color.r, color.g, color.b, 0.95f);
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particle;
        }

        static TrailRenderer CreateTrail(Transform parent, Color color)
        {
            GameObject trailObject = new GameObject(TrailName);
            trailObject.transform.SetParent(parent, false);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.minVertexDistance = 0.05f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0.0f;
            trail.emitting = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader) { color = new Color(color.r, color.g, color.b, 0.85f) };
            trail.material = material;
            return trail;
        }

        static void AssignMagneticReferences(
            MagneticObject magnetic,
            Transform modelRoot,
            ParticleSystem orbitParticle,
            ParticleSystem impactParticle,
            TrailRenderer trail)
        {
            SerializedObject serialized = new SerializedObject(magnetic);
            SerializedProperty modelRootProperty = serialized.FindProperty("modelRoot");
            SerializedProperty orbitProperty = serialized.FindProperty("orbitParticle");
            SerializedProperty impactProperty = serialized.FindProperty("impactParticle");
            SerializedProperty trailProperty = serialized.FindProperty("trail");

            if (modelRootProperty != null)
                modelRootProperty.objectReferenceValue = modelRoot;
            if (orbitProperty != null)
                orbitProperty.objectReferenceValue = orbitParticle;
            if (impactProperty != null)
                impactProperty.objectReferenceValue = impactParticle;
            if (trailProperty != null)
                trailProperty.objectReferenceValue = trail;

            serialized.ApplyModifiedPropertiesWithoutUndo();
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
