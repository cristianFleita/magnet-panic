using UnityEngine;
using UnityEngine.Rendering;

namespace MagnetPanic.Combat
{
    public sealed class LineRendererStrikeTargetPresenter : StrikeTargetPresenter
    {
        [Header("Line")]
        [SerializeField] LineRenderer line;
        [SerializeField, Range(8, 96)] int segments = 40;
        [SerializeField] float radius = 0.95f;
        [SerializeField] float heightOffset = 0.05f;
        [SerializeField] float width = 0.07f;

        [Header("Color")]
        [SerializeField] Color normalColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] Color counterColor = new Color(1f, 0.78f, 0.18f, 0.95f);

        [Header("Motion")]
        [SerializeField] float positionLerpSharpness = 22f;
        [SerializeField] float colorLerpSharpness = 18f;
        [SerializeField] float normalPulseHz = 1.4f;
        [SerializeField] float counterPulseHz = 4f;
        [SerializeField] float normalPulseAmount = 0.08f;
        [SerializeField] float counterPulseAmount = 0.16f;

        Transform tracked;
        StrikeTargetState currentState = StrikeTargetState.Hidden;
        Vector3 smoothedPosition;
        Color smoothedColor;
        bool initialized;

        public static LineRendererStrikeTargetPresenter Create(
            Transform parent,
            float radius,
            float heightOffset,
            float width,
            Color normalColor,
            Color counterColor)
        {
            GameObject host = new GameObject("Strike Target Indicator");
            host.transform.SetParent(parent, false);
            host.layer = parent.gameObject.layer;

            LineRenderer lr = host.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 0;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off;
            lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.material = new Material(ResolveShader());
            lr.enabled = false;

            LineRendererStrikeTargetPresenter presenter = host.AddComponent<LineRendererStrikeTargetPresenter>();
            presenter.line = lr;
            presenter.radius = radius;
            presenter.heightOffset = heightOffset;
            presenter.width = width;
            presenter.normalColor = normalColor;
            presenter.counterColor = counterColor;
            return presenter;
        }

        public override void Show(ArkhamEnemy target, StrikeTargetState state)
        {
            if (target == null || line == null)
            {
                Hide();
                return;
            }

            tracked = target.transform;
            currentState = state;

            if (!line.enabled)
                line.enabled = true;

            if (!initialized)
            {
                smoothedPosition = TargetPositionFor(tracked);
                smoothedColor = ColorFor(state);
                initialized = true;
            }
        }

        public override void Hide()
        {
            tracked = null;
            currentState = StrikeTargetState.Hidden;
            initialized = false;
            if (line != null)
                line.enabled = false;
        }

        void Awake()
        {
            if (line == null)
                line = GetComponent<LineRenderer>();
        }

        void LateUpdate()
        {
            if (line == null || currentState == StrikeTargetState.Hidden || tracked == null)
                return;

            float dt = Time.deltaTime;
            float posK = 1f - Mathf.Exp(-positionLerpSharpness * dt);
            float colK = 1f - Mathf.Exp(-colorLerpSharpness * dt);

            smoothedPosition = Vector3.Lerp(smoothedPosition, TargetPositionFor(tracked), posK);
            smoothedColor = Color.Lerp(smoothedColor, ColorFor(currentState), colK);

            float pulseHz = currentState == StrikeTargetState.Counterable ? counterPulseHz : normalPulseHz;
            float pulseAmount = currentState == StrikeTargetState.Counterable ? counterPulseAmount : normalPulseAmount;
            float pulse = 1f + pulseAmount * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);

            DrawRing(smoothedPosition, radius * pulse, smoothedColor);
        }

        Vector3 TargetPositionFor(Transform t)
        {
            Vector3 p = t.position;
            p.y += heightOffset;
            return p;
        }

        Color ColorFor(StrikeTargetState state)
        {
            return state == StrikeTargetState.Counterable ? counterColor : normalColor;
        }

        void DrawRing(Vector3 center, float r, Color color)
        {
            int count = Mathf.Max(8, segments) + 1;
            if (line.positionCount != count)
                line.positionCount = count;

            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = width;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / segments;
                float a = t * Mathf.PI * 2f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }

            line.startColor = color;
            line.endColor = color;
        }

        static Shader ResolveShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            return shader;
        }
    }
}
