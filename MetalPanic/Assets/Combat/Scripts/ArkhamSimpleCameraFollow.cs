using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Bridge over a Cinemachine FreeLook-style rig (CinemachineCamera + CinemachineOrbitalFollow with ThreeRing orbits).
    /// Preserves the public surface the rest of the combat code expects (Yaw / Shake / Configure / SetLookEnabled).
    /// </summary>
    public sealed class ArkhamSimpleCameraFollow : MonoBehaviour
    {
        [Header("Cinemachine refs")]
        [SerializeField] CinemachineCamera vcam;
        [SerializeField] CinemachineOrbitalFollow orbital;
        [SerializeField] CinemachineInputAxisController inputController;
        [SerializeField] CinemachineImpulseSource impulseSource;

        [Header("Shake")]
        [SerializeField] Vector3 shakeDirectionBias = new Vector3(1f, 0.35f, 1f);
        [SerializeField] float shakeImpulseScale = 1f;

        [Header("Cursor")]
        [SerializeField] bool lockCursorOnAwake = true;
        [SerializeField] bool relockOnClick = true;

        bool lookEnabled = true;

        public Transform Target => vcam != null ? vcam.Follow : null;
        public float Yaw => orbital != null ? orbital.HorizontalAxis.Value : 0f;
        public float YOrbit => orbital != null ? orbital.VerticalAxis.Value : 0.5f;

        void Reset()
        {
            TryAutoWire();
        }

        void Awake()
        {
            if (vcam == null || orbital == null || inputController == null || impulseSource == null)
                TryAutoWire();

            ApplyCursorLock();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!lookEnabled) return;
            if (focus) ApplyCursorLock();
            else ReleaseCursor();
        }

        void Update()
        {
            if (relockOnClick && lockCursorOnAwake && lookEnabled
                && Cursor.lockState != CursorLockMode.Locked
                && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ApplyCursorLock();
            }
        }

        public void Configure(Transform followTarget, Vector3 _legacyOffsetIgnored)
        {
            if (vcam == null) return;

            vcam.Follow = followTarget;
            vcam.LookAt = followTarget;
        }

        public void SetLookEnabled(bool enabled)
        {
            lookEnabled = enabled;
            if (inputController != null) inputController.enabled = enabled;
            if (enabled) ApplyCursorLock();
            else ReleaseCursor();
        }

        public void Shake(float amplitude, float duration)
        {
            if (impulseSource == null) return;

            Vector3 velocity = Vector3.Scale(Random.insideUnitSphere, shakeDirectionBias).normalized
                               * (amplitude * shakeImpulseScale);

            impulseSource.GenerateImpulseWithVelocity(velocity);
        }

        void TryAutoWire()
        {
            if (vcam == null) vcam = GetComponent<CinemachineCamera>();
            if (orbital == null) orbital = GetComponent<CinemachineOrbitalFollow>();
            if (inputController == null) inputController = GetComponent<CinemachineInputAxisController>();
            if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        void ApplyCursorLock()
        {
            if (!lockCursorOnAwake) return;
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
