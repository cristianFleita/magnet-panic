using UnityEngine;
using UnityEngine.Rendering;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Spawns a ring of small stars/quads that orbit the target's head.
    /// Used when an enemy is countered and stunned.
    /// All meshes are built at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CounterStunVfx : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField, Tooltip("Material for the orbiting stars/sparks.")]
        Material starMaterial;

        [Header("Look")]
        [SerializeField] Color starColor = new Color(1f, 0.85f, 0.2f, 1f); // Yellow/gold
        [SerializeField, Min(1)] int starCount = 3;
        [SerializeField, Min(0f)] float starSize = 0.25f;
        [SerializeField, Min(0f)] float orbitRadius = 0.45f;
        [SerializeField] float orbitSpeed = 180f; // degrees per second
        [SerializeField] float bobSpeed = 6f;
        [SerializeField] float bobAmount = 0.08f;

        Transform[] stars;
        Renderer[] renderers;
        float currentAngle;
        float bobTime;

        static Mesh _quadMesh;

        void Awake()
        {
            stars = new Transform[starCount];
            renderers = new Renderer[starCount];
            Mesh quad = GetQuadMesh();

            for (int i = 0; i < starCount; i++)
            {
                Transform t = BuildQuad($"Star_{i}", quad, starMaterial, starColor);
                stars[i] = t;
                renderers[i] = t.GetComponent<Renderer>();
            }
        }

        void Update()
        {
            currentAngle += orbitSpeed * Time.deltaTime;
            bobTime += bobSpeed * Time.deltaTime;
            
            float bobOffset = Mathf.Sin(bobTime) * bobAmount;

            Camera cam = Camera.main;

            for (int i = 0; i < starCount; i++)
            {
                float angle = currentAngle + (i * 360f / starCount);
                float rad = angle * Mathf.Deg2Rad;

                Vector3 localPos = new Vector3(
                    Mathf.Cos(rad) * orbitRadius,
                    bobOffset,
                    Mathf.Sin(rad) * orbitRadius
                );

                stars[i].localPosition = localPos;
                
                // Billboard to face camera
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - stars[i].position;
                    if (toCam.sqrMagnitude > 0.0001f)
                    {
                        stars[i].rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                    }
                }
                else
                {
                    stars[i].localRotation = Quaternion.identity;
                }
                
                stars[i].localScale = new Vector3(starSize, starSize, starSize);
            }
        }

        Transform BuildQuad(string name, Mesh quad, Material material, Color tint)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = quad;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            mr.SetPropertyBlock(block);

            return go.transform;
        }

        static Mesh GetQuadMesh()
        {
            if (_quadMesh != null) return _quadMesh;

            _quadMesh = new Mesh { name = "CounterStun_Quad" };
            _quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
            };
            _quadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            _quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quadMesh.RecalculateNormals();
            _quadMesh.RecalculateBounds();
            return _quadMesh;
        }
    }
}
