using UnityEngine;
using UnityEngine.UI;

namespace MagnetPanic.Combat
{
    public sealed class WorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField] CombatHealth health;
        [SerializeField] Camera cameraOverride = null;
        [SerializeField] Vector3 localOffset = new Vector3(0f, 2.35f, 0f);
        [SerializeField] Vector2 size = new Vector2(86f, 9f);
        [SerializeField] Color backgroundColor = new Color(0.02f, 0.025f, 0.03f, 0.72f);
        [SerializeField] Color fillColor = new Color(0.2f, 0.95f, 0.46f, 0.95f);
        [SerializeField] bool hideWhenFull = false;

        RectTransform fillRect;
        Canvas canvas;

        void Awake()
        {
            if (health == null)
                health = GetComponentInParent<CombatHealth>();

            if (cameraOverride == null)
                cameraOverride = Camera.main;

            EnsureUi();
        }

        void OnEnable()
        {
            if (health != null)
                health.OnHealthChanged.AddListener(Refresh);

            Refresh(health);
        }

        void OnDisable()
        {
            if (health != null)
                health.OnHealthChanged.RemoveListener(Refresh);
        }

        void LateUpdate()
        {
            if (canvas == null || cameraOverride == null)
                return;

            canvas.transform.rotation = cameraOverride.transform.rotation;
        }

        public void Configure(CombatHealth target, float height)
        {
            if (health != null)
                health.OnHealthChanged.RemoveListener(Refresh);

            health = target;
            localOffset = new Vector3(0f, height, 0f);

            EnsureUi();
            if (canvas != null)
                canvas.transform.localPosition = localOffset;

            if (isActiveAndEnabled && health != null)
                health.OnHealthChanged.AddListener(Refresh);

            Refresh(health);
        }

        void EnsureUi()
        {
            if (canvas != null)
                return;

            Transform existing = transform.Find("Health Bar Canvas");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            GameObject canvasObject = new GameObject("Health Bar Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = localOffset;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = size;

            GameObject background = new GameObject("Background");
            background.transform.SetParent(canvasObject.transform, false);
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = backgroundColor;
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(background.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColor;
            fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);
        }

        void Refresh(CombatHealth target)
        {
            if (fillRect == null)
                return;

            float normalized = target != null ? target.Normalized : 0f;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);

            if (canvas != null)
                canvas.enabled = target != null && target.IsAlive && (!hideWhenFull || !target.IsFull);
        }
    }
}
