using System;
using UnityEngine;
using UnityEngine.UI;

namespace MagnetPanic.Combat
{
    public struct PopupOptions
    {
        public float scaleMultiplier;
        public float lifetimeMultiplier;
        public float fontSizeMultiplier;
        public Vector3 drift;

        public static PopupOptions Default => new PopupOptions
        {
            scaleMultiplier = 1f,
            lifetimeMultiplier = 1f,
            fontSizeMultiplier = 1f,
            drift = Vector3.up,
        };
    }

    public sealed class DamagePopup : MonoBehaviour
    {
        [Header("Base Animation")]
        [SerializeField] float lifetime = 0.85f;
        [SerializeField] float riseDistance = 1.25f;
        [SerializeField] AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] AnimationCurve scaleCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.15f, 1.15f),
            new Keyframe(0.45f, 1f),
            new Keyframe(1f, 0.85f));

        [Header("Base Style")]
        [SerializeField] int baseFontSize = 64;
        [SerializeField] FontStyle fontStyle = FontStyle.Bold;
        [SerializeField] Vector2 canvasSize = new Vector2(240f, 96f);
        [SerializeField] float worldScale = 0.01f;
        [SerializeField] Color outlineColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] Vector2 outlineDistance = new Vector2(2f, -2f);

        public event Action<DamagePopup> OnExpired;

        Canvas canvas;
        Text label;
        Camera billboardCamera;
        Vector3 origin;
        Vector3 driftDirection = Vector3.up;
        Color baseColor = Color.white;
        float activeLifetime;
        float activeScaleMultiplier = 1f;
        float elapsed;
        bool running;

        public bool IsRunning => running;

        void Awake()
        {
            EnsureUi();
        }

        public void Show(Vector3 worldPosition, string text, Color color, Camera camera)
        {
            Show(worldPosition, text, color, camera, PopupOptions.Default);
        }

        public void Show(Vector3 worldPosition, string text, Color color, Camera camera, in PopupOptions options)
        {
            EnsureUi();

            origin = worldPosition;
            baseColor = color;
            billboardCamera = camera != null ? camera : Camera.main;

            activeLifetime = Mathf.Max(0.05f, lifetime * SafeMultiplier(options.lifetimeMultiplier));
            activeScaleMultiplier = SafeMultiplier(options.scaleMultiplier);
            driftDirection = options.drift.sqrMagnitude > 0.0001f ? options.drift.normalized : Vector3.up;

            float fontMultiplier = SafeMultiplier(options.fontSizeMultiplier);

            if (label != null)
            {
                label.text = text;
                label.color = baseColor;
                label.fontSize = Mathf.Max(1, Mathf.RoundToInt(baseFontSize * fontMultiplier));
            }

            transform.position = origin;
            transform.localScale = Vector3.one * worldScale * activeScaleMultiplier;
            elapsed = 0f;
            running = true;
            gameObject.SetActive(true);
        }

        public void Cancel()
        {
            running = false;
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!running)
                return;

            elapsed += Time.deltaTime;
            float t = activeLifetime > 0f ? Mathf.Clamp01(elapsed / activeLifetime) : 1f;

            float drift = riseCurve.Evaluate(t) * riseDistance;
            transform.position = origin + driftDirection * drift;

            float scale = scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * worldScale * activeScaleMultiplier * scale;

            if (billboardCamera != null)
                transform.rotation = billboardCamera.transform.rotation;

            if (label != null)
            {
                Color c = baseColor;
                c.a = baseColor.a * fadeCurve.Evaluate(t);
                label.color = c;
            }

            if (t >= 1f)
            {
                running = false;
                OnExpired?.Invoke(this);
            }
        }

        static float SafeMultiplier(float value)
        {
            return value > 0.0001f ? value : 1f;
        }

        void EnsureUi()
        {
            if (canvas != null)
                return;

            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = canvasSize;
            transform.localScale = Vector3.one * worldScale;

            Transform existing = transform.Find("Label");
            GameObject labelObject;
            if (existing != null)
            {
                labelObject = existing.gameObject;
            }
            else
            {
                labelObject = new GameObject("Label");
                labelObject.transform.SetParent(transform, false);
            }

            label = labelObject.GetComponent<Text>();
            if (label == null)
                label = labelObject.AddComponent<Text>();

            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontSize = baseFontSize;
            label.fontStyle = fontStyle;
            label.raycastTarget = false;

            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Outline outline = labelObject.GetComponent<Outline>();
            if (outline == null)
                outline = labelObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = outlineDistance;
        }
    }
}
