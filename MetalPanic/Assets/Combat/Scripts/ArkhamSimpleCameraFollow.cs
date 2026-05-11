using System.Collections;
using UnityEngine;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArkhamSimpleCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 10f, -8f);
        [SerializeField] float followSharpness = 8f;
        [SerializeField] float lookAhead = 0.5f;
        [SerializeField] float lookAtHeightOffset = 0.8f;
        [SerializeField, Range(0f, 1f)] float shakeYAttenuation = 0.35f;

        Coroutine shakeCoroutine;
        Vector3 shakeOffset;

        void OnValidate()
        {
            followSharpness = Mathf.Max(0.01f, followSharpness);
            lookAtHeightOffset = Mathf.Max(0f, lookAtHeightOffset);
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + offset + target.forward * lookAhead + shakeOffset;
            transform.position = Vector3.Lerp(
                transform.position,
                desired,
                1f - Mathf.Exp(-followSharpness * Time.deltaTime));

            Vector3 lookTarget = target.position + Vector3.up * lookAtHeightOffset;
            LookAtTarget(lookTarget);
        }

        public void Configure(Transform followTarget, Vector3 cameraOffset)
        {
            target = followTarget;
            offset = cameraOffset;
            transform.position = target.position + offset;
            LookAtTarget(target.position + Vector3.up * lookAtHeightOffset);
        }

        public void Shake(float amplitude, float duration)
        {
            if (!isActiveAndEnabled)
                return;

            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeCoroutine = StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damping = 1f - Mathf.Clamp01(elapsed / duration);
                shakeOffset = Random.insideUnitSphere * (amplitude * damping);
                shakeOffset.y *= shakeYAttenuation;
                yield return null;
            }

            shakeOffset = Vector3.zero;
            shakeCoroutine = null;
        }

        void LookAtTarget(Vector3 lookTarget)
        {
            Vector3 direction = lookTarget - transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
