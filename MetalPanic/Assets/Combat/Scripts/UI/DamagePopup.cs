using System;
using UnityEngine;
using UnityEngine.UI;

namespace MagnetPanic.Combat
{
    public sealed class DamagePopup : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] float lifetime = 0.85f;
        [SerializeField] float riseDistance = 1.25f;
        [SerializeField] AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] AnimationCurve scaleCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.15f, 1.15f),
            new Keyframe(0.45f, 1f),
            new Keyframe(1f, 0.85f));

        [Header("Style")]
        [SerializeField] int fontSize = 64;
        [SerializeField] FontStyle fontStyle = FontStyle.Bold;
        [SerializeField] Vector2 canvasSize = new Vector2(220f, 90f);
        [SerializeField] float worldScale = 0.01f;
        [SerializeField] Color outlineColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] Vector2 outlineDistance = new Vector2(2f, -2f);

        public event Action<DamagePopup> OnExpired;

        Canvas canvas;
        Text label;
        Camera billboardCamera;
        Vector3 origin;
        Color baseColor = Color.white;
        float elapsed;
        bool running;

        public bool IsRunning => running;

        void Awake()
        {
            EnsureUi();
        }

        public void Show(Vector3 worldPosition, int amount, Color color, Camera camera)
        {
            EnsureUi();

            origin = worldPosition;
            baseColor = color;
            billboardCamera = camera != null ? camera : Camera.main;

            if (label != null)
            {
                label.text = amount.ToString();
                label.color = baseColor;
            }

            transform.position = origin;
            transform.localScale = Vector3.one * worldScale;
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
            float t = lifetime > 0f ? Mathf.Clamp01(elapsed / lifetime) : 1f;

            float rise = riseCurve.Evaluate(t) * riseDistance;
            transform.position = origin + Vector3.up * rise;

            float scale = scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * worldScale * scale;

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
            label.fontSize = fontSize;
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
