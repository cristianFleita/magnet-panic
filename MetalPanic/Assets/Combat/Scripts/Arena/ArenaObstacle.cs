using UnityEngine;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Marks a scene prop as a solid arena obstacle. Guarantees a non-trigger collider so
    /// CharacterController-based agents (enemies, player) are physically blocked, and exposes
    /// the footprint to ArenaSystem so spawn sampling avoids it.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class ArenaObstacle : MonoBehaviour
    {
        public enum ShapeMode
        {
            AutoFromBounds,
            Capsule,
            Box,
            UseExistingColliders
        }

        [Header("Collider")]
        [Tooltip("How to guarantee a solid (non-trigger) collider on this obstacle.")]
        [SerializeField] ShapeMode shapeMode = ShapeMode.AutoFromBounds;
        [Tooltip("If true, forces any existing collider under this object to be non-trigger so the CharacterController can collide with it.")]
        [SerializeField] bool forceExistingNonTrigger = true;
        [Tooltip("Extra inset on auto-generated colliders so the visual mesh isn't pierced.")]
        [SerializeField, Min(0f)] float autoShapeInset = 0.05f;

        [Header("Layer")]
        [Tooltip("If true, assigns the configured blocking layer to this object so it matches the rest of the static arena geometry.")]
        [SerializeField] bool overrideLayer = true;
        [SerializeField] string blockingLayerName = "ArenaWall";

        [Header("Spawn Avoidance")]
        [Tooltip("Approximate XZ radius used by ArenaSystem to forbid spawning inside this obstacle. 0 = auto from collider bounds.")]
        [SerializeField, Min(0f)] float spawnAvoidanceRadius = 0f;

        Collider generatedCollider;
        ArenaSystem registeredArena;

        public float SpawnAvoidanceRadius => spawnAvoidanceRadius > 0f ? spawnAvoidanceRadius : AutoFootprintRadius();
        public Vector3 FootprintCenter => transform.position;

        void Awake()
        {
            EnsureSolidCollider();
        }

        void OnEnable()
        {
            EnsureSolidCollider();
            if (!Application.isPlaying)
                return;

            registeredArena = FindArenaSystem();
            if (registeredArena != null)
                registeredArena.RegisterObstacle(this);
        }

        void OnDisable()
        {
            if (registeredArena != null)
            {
                registeredArena.UnregisterObstacle(this);
                registeredArena = null;
            }
        }

        void OnValidate()
        {
            // In the editor, refresh the collider so toggling shapeMode does the right thing.
            if (!Application.isPlaying)
                EnsureSolidCollider();
        }

        void EnsureSolidCollider()
        {
            ApplyBlockingLayer();

            if (shapeMode == ShapeMode.UseExistingColliders)
            {
                if (forceExistingNonTrigger)
                    DisableTriggersOnExistingColliders();
                return;
            }

            if (forceExistingNonTrigger)
                DisableTriggersOnExistingColliders();

            if (HasUsableSolidCollider())
                return;

            // Need to add a generated collider. Wipe any previously generated one to avoid duplicates.
            if (generatedCollider != null)
                DestroyImmediateSafe(generatedCollider);

            switch (shapeMode)
            {
                case ShapeMode.Capsule:
                    generatedCollider = CreateCapsuleFromBounds();
                    break;
                case ShapeMode.Box:
                    generatedCollider = CreateBoxFromBounds();
                    break;
                default:
                    generatedCollider = ChooseAutoShape();
                    break;
            }
        }

        void ApplyBlockingLayer()
        {
            if (!overrideLayer || string.IsNullOrEmpty(blockingLayerName))
                return;

            int layer = LayerMask.NameToLayer(blockingLayerName);
            if (layer < 0)
                return;

            if (gameObject.layer != layer)
                gameObject.layer = layer;
        }

        void DisableTriggersOnExistingColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || c == generatedCollider)
                    continue;
                if (c.isTrigger)
                    c.isTrigger = false;
            }
        }

        bool HasUsableSolidCollider()
        {
            Collider[] colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || c.isTrigger)
                    continue;
                return true;
            }

            // Also accept solid colliders on direct children (a common Kenney layout).
            Collider[] childColliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider c = childColliders[i];
                if (c == null || c.isTrigger || c.gameObject == gameObject)
                    continue;
                return true;
            }

            return false;
        }

        Collider ChooseAutoShape()
        {
            Bounds bounds = RendererBoundsLocal();
            if (bounds.size == Vector3.zero)
                return CreateBoxFromBounds();

            float xz = Mathf.Max(bounds.size.x, bounds.size.z);
            // Round-ish props (dome, tank, reactor) get a capsule. Boxy props get a box.
            float ratio = Mathf.Min(bounds.size.x, bounds.size.z) / Mathf.Max(0.001f, xz);
            return ratio > 0.7f ? CreateCapsuleFromBounds() : CreateBoxFromBounds();
        }

        BoxCollider CreateBoxFromBounds()
        {
            Bounds bounds = RendererBoundsLocal();
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
            if (bounds.size == Vector3.zero)
            {
                box.center = Vector3.zero;
                box.size = Vector3.one;
                return box;
            }
            box.center = bounds.center;
            Vector3 size = bounds.size;
            size.x = Mathf.Max(0.1f, size.x - autoShapeInset);
            size.z = Mathf.Max(0.1f, size.z - autoShapeInset);
            size.y = Mathf.Max(0.1f, size.y);
            box.size = size;
            return box;
        }

        CapsuleCollider CreateCapsuleFromBounds()
        {
            Bounds bounds = RendererBoundsLocal();
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            capsule.direction = 1; // Y axis
            if (bounds.size == Vector3.zero)
            {
                capsule.center = Vector3.zero;
                capsule.radius = 0.5f;
                capsule.height = 2f;
                return capsule;
            }
            capsule.center = bounds.center;
            float radius = Mathf.Max(0.1f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f - autoShapeInset);
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2f, bounds.size.y);
            return capsule;
        }

        Bounds RendererBoundsLocal()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            Bounds worldBounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer)
                    continue;
                if (!initialized)
                {
                    worldBounds = r.bounds;
                    initialized = true;
                }
                else
                {
                    worldBounds.Encapsulate(r.bounds);
                }
            }

            if (!initialized)
                return new Bounds(Vector3.zero, Vector3.zero);

            // Convert world bounds into the local space of this transform so collider center/size stay stable when rotated.
            Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
            Vector3 lossy = transform.lossyScale;
            Vector3 invScale = new Vector3(
                lossy.x != 0f ? 1f / Mathf.Abs(lossy.x) : 0f,
                lossy.y != 0f ? 1f / Mathf.Abs(lossy.y) : 0f,
                lossy.z != 0f ? 1f / Mathf.Abs(lossy.z) : 0f);
            Vector3 localSize = Vector3.Scale(worldBounds.size, invScale);
            return new Bounds(localCenter, localSize);
        }

        float AutoFootprintRadius()
        {
            Bounds bounds = RendererBoundsLocal();
            if (bounds.size == Vector3.zero)
                return 1f;
            return Mathf.Max(0.5f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f);
        }

        ArenaSystem FindArenaSystem()
        {
            ArenaSystem inParent = GetComponentInParent<ArenaSystem>();
            if (inParent != null)
                return inParent;
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<ArenaSystem>();
#else
            return Object.FindObjectOfType<ArenaSystem>();
#endif
        }

        static void DestroyImmediateSafe(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        void OnDrawGizmosSelected()
        {
            float r = SpawnAvoidanceRadius;
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(FootprintCenter, r);
        }
    }
}
