using UnityEngine;
using UnityEngine.Rendering;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Spider-sense style VFX.
    /// Spawns radial lines in an arc over the player's head that billboard to the camera and pulse.
    /// Replaces the old ParticleSystem-based SenseVfx.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SenseVfxProcedural : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField, Min(1)] int lineCount = 9;
        [SerializeField, Range(10f, 360f)] float arcAngle = 140f; // degrees
        [SerializeField, Min(0f)] float innerRadius = 0.35f;
        [SerializeField, Min(0f)] float outerRadius = 0.95f;
        [SerializeField, Min(0f)] float lineThickness = 0.12f;

        [Header("Animation")]
        [SerializeField] float pulseSpeed = 15f;
        [SerializeField] float pulseScaleAmp = 0.15f;
        [SerializeField] float pulseAlphaAmp = 0.35f;

        Transform[] lines;
        Renderer[] renderers;
        MaterialPropertyBlock block;

        static Mesh _quadMesh;

        void Awake()
        {
            lines = new Transform[lineCount];
            renderers = new Renderer[lineCount];
            block = new MaterialPropertyBlock();
            Mesh quad = GetQuadMesh();

            for (int i = 0; i < lineCount; i++)
            {
                Transform t = BuildQuad($"SenseLine_{i}", quad, lineMaterial, lineColor);
                lines[i] = t;
                renderers[i] = t.GetComponent<Renderer>();
            }
        }

        void Update()
        {
            Camera cam = Camera.main;
            float time = Time.time * pulseSpeed;
            float scaleMult = 1f + Mathf.Sin(time) * pulseScaleAmp;
            float alphaMult = 1f - pulseAlphaAmp + Mathf.Sin(time) * pulseAlphaAmp;

            Color c = lineColor;
            c.a *= alphaMult;

            float startAngle = -arcAngle / 2f;
            float step = lineCount > 1 ? arcAngle / (lineCount - 1) : 0f;

            for (int i = 0; i < lineCount; i++)
            {
                float angle = startAngle + i * step;
                float rad = angle * Mathf.Deg2Rad;

                float length = (outerRadius - innerRadius) * scaleMult;
                float midRadius = innerRadius + length / 2f;

                // Position offset in the screen's X-Y plane
                Vector3 screenOffset = new Vector3(
                    Mathf.Sin(rad) * midRadius,
                    Mathf.Cos(rad) * midRadius,
                    0f
                );

                lines[i].localScale = new Vector3(lineThickness, length, 1f);

                // Billboard perfectly parallel to the screen
                if (cam != null)
                {
                    Quaternion camRot = cam.transform.rotation;
                    
                    // Apply offset in camera space so the arc doesn't rotate with the player's body
                    lines[i].position = transform.position + (camRot * screenOffset);
                    
                    // Z rotation to point outward in screen plane
                    lines[i].rotation = camRot * Quaternion.Euler(0, 0, -angle);
                }
                else
                {
                    lines[i].localPosition = screenOffset;
                    lines[i].localRotation = Quaternion.Euler(0, 0, -angle);
                }

                renderers[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                renderers[i].SetPropertyBlock(block);
            }
        }

        Transform BuildQuad(string name, Mesh quad, Material material, Color tint)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = quad;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;

            MaterialPropertyBlock b = new MaterialPropertyBlock();
            mr.GetPropertyBlock(b);
            b.SetColor("_BaseColor", tint);
            b.SetColor("_Color", tint);
            mr.SetPropertyBlock(b);

            return go.transform;
        }

        static Mesh GetQuadMesh()
        {
            if (_quadMesh != null) return _quadMesh;

            _quadMesh = new Mesh { name = "Sense_Quad" };
            // Centered quad
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
