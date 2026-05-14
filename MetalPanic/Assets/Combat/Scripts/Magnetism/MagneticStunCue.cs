using UnityEngine;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(ArkhamEnemy))]
    public sealed class MagneticStunCue : MonoBehaviour
    {
        [SerializeField] ArkhamEnemy enemy;

        [Header("Cue")]
        [SerializeField, Tooltip("Optional pre-authored cue (mesh, particle, etc). If null, a red sphere primitive is generated at runtime.")]
        GameObject cueOverride;
        [SerializeField] bool autoCreateCue = true;
        [SerializeField] float cueHeight = 2.45f;
        [SerializeField] Vector3 cueScale = new Vector3(0.42f, 0.42f, 0.42f);
        [SerializeField] Color cueColor = new Color(1f, 0.18f, 0.18f, 0.9f);

        GameObject cueInstance;

        void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<ArkhamEnemy>();

            EnsureCue();
            HideCue();
        }

        void OnEnable()
        {
            if (enemy == null)
                return;

            enemy.OnAnchored.AddListener(HandleAnchored);
            enemy.OnAnchorReleased.AddListener(HandleAnchorReleased);
        }

        void OnDisable()
        {
            if (enemy == null)
                return;

            enemy.OnAnchored.RemoveListener(HandleAnchored);
            enemy.OnAnchorReleased.RemoveListener(HandleAnchorReleased);
            HideCue();
        }

        void HandleAnchored(ArkhamEnemy _)
        {
            ShowCue();
        }

        void HandleAnchorReleased(ArkhamEnemy _)
        {
            HideCue();
        }

        void EnsureCue()
        {
            if (cueInstance != null)
                return;

            if (cueOverride != null)
            {
                cueInstance = cueOverride;
                return;
            }

            if (!autoCreateCue)
                return;

            GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cue.name = "Stun Cue";
            cue.transform.SetParent(transform, false);
            cue.transform.localPosition = new Vector3(0f, cueHeight, 0f);
            cue.transform.localScale = cueScale;

            Collider cueCollider = cue.GetComponent<Collider>();
            if (cueCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(cueCollider);
                else
                    DestroyImmediate(cueCollider);
            }

            Renderer renderer = cue.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Standard");

                renderer.sharedMaterial = new Material(shader)
                {
                    color = cueColor
                };
            }

            cueInstance = cue;
        }

        void ShowCue()
        {
            if (cueInstance == null)
                EnsureCue();

            if (cueInstance != null)
                cueInstance.SetActive(true);
        }

        void HideCue()
        {
            if (cueInstance != null)
                cueInstance.SetActive(false);
        }
    }
}
