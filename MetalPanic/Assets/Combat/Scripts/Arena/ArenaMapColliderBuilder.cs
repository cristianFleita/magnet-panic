using UnityEngine;

namespace MagnetPanic.Combat
{
    [DisallowMultipleComponent]
    public sealed class ArenaMapColliderBuilder : MonoBehaviour
    {
        const string GeneratedRootName = "Generated Arena Colliders";
        const string ArenaWallLayerName = "ArenaWall";
        const string GroundLayerName = "Ground";

        [SerializeField] bool buildOnAwake = true;
        [SerializeField] bool addMeshColliders = true;
        [SerializeField] bool addGroundProxies = true;
        [SerializeField, Min(0.02f)] float groundProxyHeight = 0.12f;
        [SerializeField, Min(0f)] float groundInset = 0.2f;

        void Awake()
        {
            if (buildOnAwake)
                BuildNow();
        }

        public void BuildNow()
        {
            Transform generatedRoot = ResetGeneratedRoot();
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null || IsInsideGeneratedRoot(filter.transform))
                    continue;

                if (addMeshColliders)
                    EnsureMeshCollider(filter);

                if (addGroundProxies && IsGroundCandidate(filter))
                    CreateGroundProxy(generatedRoot, filter);
            }
        }

        Transform ResetGeneratedRoot()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
                DestroyLocalObject(existing.gameObject);

            GameObject root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        void EnsureMeshCollider(MeshFilter filter)
        {
            Collider existing = filter.GetComponent<Collider>();
            if (existing == null)
            {
                MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                meshCollider.convex = false;
            }

            filter.gameObject.layer = LayerOrDefault(ArenaWallLayerName);
        }

        void CreateGroundProxy(Transform parent, MeshFilter filter)
        {
            Bounds bounds = RendererBounds(filter);
            if (bounds.size.x <= 0.01f || bounds.size.z <= 0.01f)
                return;

            GameObject proxy = new GameObject("Ground - " + filter.gameObject.name);
            proxy.layer = LayerOrDefault(GroundLayerName);
            proxy.transform.SetParent(parent, true);
            proxy.transform.position = new Vector3(bounds.center.x, bounds.min.y + groundProxyHeight * 0.5f, bounds.center.z);
            proxy.transform.rotation = Quaternion.identity;

            BoxCollider collider = proxy.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                Mathf.Max(0.1f, bounds.size.x - groundInset),
                groundProxyHeight,
                Mathf.Max(0.1f, bounds.size.z - groundInset));
        }

        static Bounds RendererBounds(MeshFilter filter)
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer != null)
                return renderer.bounds;

            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 min = filter.transform.TransformPoint(meshBounds.min);
            Vector3 max = filter.transform.TransformPoint(meshBounds.max);
            Bounds bounds = new Bounds(min, Vector3.zero);
            bounds.Encapsulate(max);
            return bounds;
        }

        static bool IsGroundCandidate(MeshFilter filter)
        {
            string objectName = filter.gameObject.name.ToLowerInvariant();
            return objectName.Contains("room")
                || objectName.Contains("floor")
                || objectName.Contains("platform");
        }

        static bool IsInsideGeneratedRoot(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == GeneratedRootName)
                    return true;

                current = current.parent;
            }

            return false;
        }

        static int LayerOrDefault(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? layer : 0;
        }

        static void DestroyLocalObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
