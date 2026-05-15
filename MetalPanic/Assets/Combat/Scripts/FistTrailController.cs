using UnityEngine;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Spawns persistent <see cref="TrailRenderer"/> children on the player's hand bones
    /// and emits a quick streak each time <see cref="ArkhamCombatController.OnTrajectory"/>
    /// fires. Modeled after the trail on LightScrap_Attractable so the strike reads as a
    /// magnetised arc instead of a plain swing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FistTrailController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ArkhamCombatController combat;
        [SerializeField] Animator animator;

        [Header("Bones")]
        [SerializeField] HumanBodyBones leftBone = HumanBodyBones.LeftHand;
        [SerializeField] HumanBodyBones rightBone = HumanBodyBones.RightHand;
        [SerializeField, Tooltip("Optional override transform. When set, takes priority over the humanoid bone lookup.")]
        Transform leftHandOverride;
        [SerializeField] Transform rightHandOverride;

        [Header("Trail Appearance")]
        [SerializeField] Material trailMaterial;
        [SerializeField] Color trailColor = new Color(0.18039216f, 0.85882354f, 1f, 1f);
        [SerializeField, Min(0.01f)] float trailTime = 0.18f;
        [SerializeField, Min(0f)] float trailStartWidth = 0.18f;
        [SerializeField, Min(0f)] float trailEndWidth = 0f;
        [SerializeField, Min(0f)] float trailMinVertexDistance = 0.04f;

        [Header("Timing")]
        [SerializeField, Min(0.01f), Tooltip("Total seconds the trail keeps emitting after OnTrajectory fires. Should cover the lunge plus a small linger.")]
        float emissionDuration = 0.34f;

        TrailRenderer leftTrail;
        TrailRenderer rightTrail;
        float emissionUntil;

        void Reset()
        {
            combat = GetComponent<ArkhamCombatController>();
            animator = GetComponent<Animator>();
        }

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (combat == null) combat = GetComponent<ArkhamCombatController>();

            leftTrail = CreateTrail(ResolveBone(leftHandOverride, leftBone), "Fist Trail (L)");
            rightTrail = CreateTrail(ResolveBone(rightHandOverride, rightBone), "Fist Trail (R)");
        }

        void OnEnable()
        {
            if (combat != null)
                combat.OnTrajectory.AddListener(HandleTrajectory);
        }

        void OnDisable()
        {
            if (combat != null)
                combat.OnTrajectory.RemoveListener(HandleTrajectory);

            emissionUntil = 0f;
            SetEmitting(false);
        }

        void Update()
        {
            if (emissionUntil > 0f && Time.time >= emissionUntil)
            {
                emissionUntil = 0f;
                SetEmitting(false);
            }
        }

        void HandleTrajectory(ArkhamEnemy _)
        {
            emissionUntil = Time.time + emissionDuration;
            SetEmitting(true);
        }

        void SetEmitting(bool on)
        {
            if (leftTrail != null) leftTrail.emitting = on;
            if (rightTrail != null) rightTrail.emitting = on;
        }

        Transform ResolveBone(Transform overrideTransform, HumanBodyBones bone)
        {
            if (overrideTransform != null)
                return overrideTransform;
            if (animator == null || !animator.isHuman)
                return null;
            return animator.GetBoneTransform(bone);
        }

        TrailRenderer CreateTrail(Transform bone, string name)
        {
            if (bone == null) return null;

            GameObject go = new GameObject(name);
            go.transform.SetParent(bone, false);

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailStartWidth;
            trail.endWidth = trailEndWidth;
            trail.minVertexDistance = trailMinVertexDistance;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.emitting = false;
            trail.autodestruct = false;
            trail.receiveShadows = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (trailMaterial != null)
            {
                trail.material = trailMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    trail.material = new Material(shader);
            }

            trail.startColor = trailColor;
            Color fade = trailColor;
            fade.a = 0f;
            trail.endColor = fade;

            return trail;
        }
    }
}
