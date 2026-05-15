using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// One-shot melee hit burst inspired by Mix and Jam's HitParticle:
    /// a textured ring billboarded toward the camera that scales up and fades,
    /// plus N small "spark" quads sitting on its perimeter that follow the expansion.
    /// All meshes are built at runtime so the prefab carries no scene references.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeImpactVfx : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField, Tooltip("Material with the cyan ring sprite (e.g. M_VFX_Melee_Hit_Ring).")]
        Material ringMaterial;
        [SerializeField, Tooltip("Material for the small spark quads. Falls back to ringMaterial when null.")]
        Material sparkMaterial;

        [Header("Look")]
        [SerializeField] Color ringColor = new Color(0.18039216f, 0.85882354f, 1f, 1f);
        [SerializeField] Color sparkColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField, Tooltip("Face the spawn-time rotation (e.g. attacker direction) instead of billboarding to the camera.")]
        bool useSpawnRotation = false;

        [Header("Ring")]
        [SerializeField, Min(0f)] float ringStartScale = 0.4f;
        [SerializeField, Min(0f)] float ringEndScale = 2.2f;
        [SerializeField, Min(0.05f)] float ringDuration = 0.32f;
        [SerializeField, Range(0f, 1f)] float ringFadeStart = 0.4f;

        [Header("Sparks")]
        [SerializeField, Min(0)] int sparkCount = 6;
        [SerializeField, Min(0f)] float sparkSize = 0.22f;
        [SerializeField, Range(0f, 1f)] float sparkRadiusFactor = 0.46f;
        [SerializeField, Tooltip("Degrees added to each spark's angular position over the burst (visual swirl).")]
        float sparkSpinDegrees = 35f;
        [SerializeField, Tooltip("Initial angular offset of the spark ring in degrees.")]
        float sparkAngleOffset = 0f;

        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] float selfDestructAfter = 0.6f;

        static Mesh _quadMesh;

        void OnEnable()
        {
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            Camera cam = Camera.main;
            Mesh quad = GetQuadMesh();

            Transform ring = BuildQuad("ImpactRing", quad, ringMaterial, ringColor);
            Transform[] sparks = new Transform[sparkCount];
            Renderer[] sparkRenderers = new Renderer[sparkCount];
            MaterialPropertyBlock sparkBlock = new MaterialPropertyBlock();
            Material resolvedSparkMat = sparkMaterial != null ? sparkMaterial : ringMaterial;

            for (int i = 0; i < sparkCount; i++)
            {
                Transform t = BuildQuad($"Spark_{i}", quad, resolvedSparkMat, sparkColor);
                sparks[i] = t;
                sparkRenderers[i] = t.GetComponent<Renderer>();
            }

            Quaternion baseRotation = useSpawnRotation
                ? transform.rotation
                : ResolveFaceCameraRotation(cam);

            transform.rotation = baseRotation;

            MaterialPropertyBlock ringBlock = new MaterialPropertyBlock();
            Renderer ringRenderer = ring.GetComponent<Renderer>();

            float elapsed = 0f;
            while (elapsed < ringDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ringDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                float scale = Mathf.Lerp(ringStartScale, ringEndScale, eased);
                float ringAlpha = t < ringFadeStart
                    ? 1f
                    : Mathf.Lerp(1f, 0f, Mathf.InverseLerp(ringFadeStart, 1f, t));

                ring.localScale = new Vector3(scale, scale, scale);
                Color rc = ringColor;
                rc.a *= ringAlpha;
                ringRenderer.GetPropertyBlock(ringBlock);
                ringBlock.SetColor("_BaseColor", rc);
                ringBlock.SetColor("_Color", rc);
                ringRenderer.SetPropertyBlock(ringBlock);

                float sparkR = scale * 0.5f * sparkRadiusFactor * 2f;
                float spinRad = (sparkSpinDegrees * Mathf.Deg2Rad) * eased;
                float baseAng = sparkAngleOffset * Mathf.Deg2Rad + spinRad;

                float sparkScale = sparkSize * (1f - 0.55f * t);
                float sparkAlpha = Mathf.Lerp(1f, 0f, t);
                Color sc = sparkColor;
                sc.a *= sparkAlpha;

                for (int i = 0; i < sparkCount; i++)
                {
                    float ang = baseAng + i * Mathf.PI * 2f / sparkCount;
                    Vector3 local = new Vector3(Mathf.Cos(ang) * sparkR, Mathf.Sin(ang) * sparkR, 0f);
                    sparks[i].localPosition = local;
                    sparks[i].localScale = new Vector3(sparkScale, sparkScale, sparkScale);

                    sparkRenderers[i].GetPropertyBlock(sparkBlock);
                    sparkBlock.SetColor("_BaseColor", sc);
                    sparkBlock.SetColor("_Color", sc);
                    sparkRenderers[i].SetPropertyBlock(sparkBlock);
                }

                yield return null;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, selfDestructAfter - ringDuration));
            Destroy(gameObject);
        }

        Quaternion ResolveFaceCameraRotation(Camera cam)
        {
            if (cam == null)
                return transform.rotation;

            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude < 0.0001f)
                return transform.rotation;

            return Quaternion.LookRotation(-toCam.normalized, Vector3.up);
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
            if (_quadMesh != null)
                return _quadMesh;

            _quadMesh = new Mesh { name = "MeleeImpactVfx_Quad" };
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
