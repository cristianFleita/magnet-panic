using System.Collections;
using UnityEngine;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Self-contained one-shot melee strike trail. On spawn it creates a child TrailRenderer,
    /// sweeps it along a short crescent arc in local space (forward = +Z), then destroys itself
    /// once the trail has had time to fade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeTrailVfx : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] Material trailMaterial;
        [SerializeField] Color trailColor = new Color(0.18039216f, 0.85882354f, 1f, 1f);
        [SerializeField, Tooltip("Width at the head of the trail (where new vertices are laid down).")]
        [Min(0.001f)] float startWidth = 0.025f;
        [SerializeField, Tooltip("Width at the tail of the trail (oldest vertices, just before they vanish).")]
        [Min(0f)] float endWidth = 0.025f;
        [SerializeField, Tooltip("Peak width at the middle of the trail (sword-slash bulge). Set equal to start/end to disable the bulge.")]
        [Min(0.001f)] float midWidth = 0.045f;
        [SerializeField, Tooltip("Global multiplier applied on top of the width curve, à la Jammo's widthMultiplier.")]
        [Min(0.001f)] float widthMultiplier = 1f;
        [SerializeField, Min(0.02f)] float trailTime = 0.3f;
        [SerializeField, Tooltip("Minimum distance between trail vertices. Higher values = smoother, less jittery trail.")]
        [Min(0.001f)] float minVertexDistance = 0.1f;

        [Header("Arc (world-space fallback only)")]
        [SerializeField, Min(0.1f)] float arcLength = 1.8f;
        [SerializeField] float arcHeight = 0.35f;
        [SerializeField, Min(0.02f)] float arcDuration = 0.22f;
        [SerializeField, Tooltip("Push the arc forward in local +Z so it cuts through the target.")]
        float forwardOffset = 0.25f;

        void OnEnable()
        {
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            GameObject head = new GameObject("TrailHead");
            head.transform.SetParent(transform, false);

            TrailRenderer trail = head.AddComponent<TrailRenderer>();
            trail.material = trailMaterial;
            trail.time = trailTime;
            trail.minVertexDistance = minVertexDistance;
            trail.widthMultiplier = widthMultiplier;
            trail.widthCurve = BuildArcWidthCurve(startWidth, midWidth, endWidth);
            trail.numCornerVertices = 0;
            trail.numCapVertices = 0;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(trailColor, 0f),
                    new GradientColorKey(trailColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(trailColor.a, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            trail.colorGradient = gradient;

            // When this VFX is parented to a bone/transform (e.g. the player's fist), let the
            // parent's motion shape the trail naturally — skip the artificial in-place arc sweep.
            bool followParent = transform.parent != null;

            if (followParent)
            {
                head.transform.localPosition = Vector3.zero;
                trail.Clear();
                trail.emitting = true;

                yield return new WaitForSeconds(arcDuration);

                trail.emitting = false;
                yield return new WaitForSeconds(trailTime + 0.05f);

                Destroy(gameObject);
                yield break;
            }

            Vector3 start = new Vector3(-arcLength * 0.5f, arcHeight, forwardOffset);
            Vector3 end = new Vector3(arcLength * 0.5f, -arcHeight, forwardOffset);

            head.transform.localPosition = start;
            trail.Clear();
            trail.emitting = true;

            float elapsed = 0f;
            while (elapsed < arcDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / arcDuration);
                float eased = 1f - (1f - t) * (1f - t);
                head.transform.localPosition = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            head.transform.localPosition = end;
            trail.emitting = false;

            yield return new WaitForSeconds(trailTime + 0.05f);

            Destroy(gameObject);
        }

        static AnimationCurve BuildArcWidthCurve(float start, float mid, float end)
        {
            // Sword-slash profile: thin -> thick -> thin, mirroring the Jammo trail's bulge.
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(0.5f, mid),
                new Keyframe(1f, end));
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
