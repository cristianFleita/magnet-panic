using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArkhamSimpleCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform target;
        [SerializeField] float pivotHeight = 1.55f;

        [Header("Rig")]
        [SerializeField] float distance = 4.8f;
        [SerializeField] Vector2 shoulderOffset = new Vector2(0.55f, 0.15f);
        [SerializeField] float followSharpness = 14f;
        [SerializeField] float rotationSharpness = 22f;

        [Header("Look — Mouse")]
        [SerializeField] float mouseYawSensitivity = 0.16f;
        [SerializeField] float mousePitchSensitivity = 0.12f;

        [Header("Look — Gamepad")]
        [SerializeField] float gamepadYawSensitivity = 160f;
        [SerializeField] float gamepadPitchSensitivity = 120f;
        [SerializeField, Range(0f, 0.5f)] float gamepadDeadzone = 0.15f;

        [Header("Look — Constraints")]
        [SerializeField] bool invertY = false;
        [SerializeField] float minPitch = -20f;
        [SerializeField] float maxPitch = 55f;
        [SerializeField] float initialYaw = 0f;
        [SerializeField] float initialPitch = 16f;

        [Header("Cursor")]
        [SerializeField] bool lockCursorOnAwake = true;
        [SerializeField] bool relockOnClick = true;

        [Header("Shake")]
        [SerializeField, Range(0f, 1f)] float shakeYAttenuation = 0.35f;

        Coroutine shakeCoroutine;
        Vector3 shakeOffset;
        float yaw;
        float pitch;
        bool lookEnabled = true;
        bool initialized;

        public Transform Target => target;
        public float Yaw => yaw;
        public float Pitch => pitch;

        void Awake()
        {
            yaw = initialYaw;
            pitch = initialPitch;
            ApplyCursorLock();
        }

        void OnValidate()
        {
            followSharpness = Mathf.Max(0.01f, followSharpness);
            rotationSharpness = Mathf.Max(0.01f, rotationSharpness);
            distance = Mathf.Max(0.5f, distance);
            pivotHeight = Mathf.Max(0f, pivotHeight);
            minPitch = Mathf.Clamp(minPitch, -89f, 89f);
            maxPitch = Mathf.Clamp(maxPitch, minPitch, 89f);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus)
                ApplyCursorLock();
            else
                ReleaseCursor();
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            if (lookEnabled)
                UpdateLook();

            HandleClickRelock();

            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 localOffset = new Vector3(shoulderOffset.x, shoulderOffset.y, -distance);
            Vector3 desired = pivot + orbit * localOffset + shakeOffset;

            if (!initialized)
            {
                transform.position = desired;
                transform.rotation = Quaternion.LookRotation(pivot - desired);
                initialized = true;
                return;
            }

            transform.position = Vector3.Lerp(
                transform.position,
                desired,
                1f - Mathf.Exp(-followSharpness * Time.deltaTime));

            Vector3 lookDir = pivot - transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }
        }

        void UpdateLook()
        {
            float yawDelta = 0f;
            float pitchDelta = 0f;

            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                yawDelta += mouseDelta.x * mouseYawSensitivity;
                pitchDelta += mouseDelta.y * mousePitchSensitivity;
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                if (stick.sqrMagnitude > gamepadDeadzone * gamepadDeadzone)
                {
                    yawDelta += stick.x * gamepadYawSensitivity * Time.deltaTime;
                    pitchDelta += stick.y * gamepadPitchSensitivity * Time.deltaTime;
                }
            }

            yaw += yawDelta;
            pitch += invertY ? pitchDelta : -pitchDelta;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        void HandleClickRelock()
        {
            if (!relockOnClick || !lockCursorOnAwake)
                return;

            if (Cursor.lockState == CursorLockMode.Locked)
                return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                ApplyCursorLock();
        }

        public void Configure(Transform followTarget, Vector3 cameraOffset)
        {
            target = followTarget;

            if (cameraOffset.sqrMagnitude > 0.0001f)
            {
                distance = Mathf.Max(1f, new Vector2(cameraOffset.x, cameraOffset.z).magnitude);
                pivotHeight = Mathf.Max(0f, cameraOffset.y * 0.2f + 1.2f);
            }

            initialized = false;
            ApplyCursorLock();
        }

        public void SetLookEnabled(bool enabled)
        {
            lookEnabled = enabled;
            if (!enabled)
                ReleaseCursor();
            else
                ApplyCursorLock();
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

        void ApplyCursorLock()
        {
            if (!lockCursorOnAwake)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
