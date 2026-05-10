using UnityEngine;
using UnityEngine.InputSystem;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArkhamPlayerMotor : MonoBehaviour
    {
        static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");

        [Header("References")]
        [SerializeField] Animator animator;
        [SerializeField] Camera cameraOverride;

        [Header("Movement")]
        [SerializeField] float movementSpeed = 5f;
        [SerializeField] float sprintMultiplier = 1.25f;
        [SerializeField] float rotationSharpness = 14f;
        [SerializeField] float gravity = -25f;
        [SerializeField] float groundedStickForce = -2f;

        CharacterController controller;
        Vector2 moveAxis;
        Vector3 worldMoveDirection;
        float verticalVelocity;
        float acceleration = 1f;
        bool movementLocked;
        bool sprinting;

        public Vector2 MoveAxis => moveAxis;
        public Vector3 WorldMoveDirection => worldMoveDirection;
        public bool IsMovementLocked => movementLocked;

        public float Acceleration
        {
            get => acceleration;
            set => acceleration = Mathf.Max(0f, value);
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (cameraOverride == null)
                cameraOverride = Camera.main;
        }

        void Update()
        {
            UpdateWorldMoveDirection();
            UpdateHorizontalMovement();
            UpdateGravity();
        }

        public void Configure(Camera sceneCamera, Animator targetAnimator, float speed)
        {
            cameraOverride = sceneCamera;
            animator = targetAnimator;
            movementSpeed = speed;
        }

        public void SetMovementLocked(bool locked, bool clearInput = false)
        {
            movementLocked = locked;

            if (clearInput)
                moveAxis = Vector2.zero;
        }

        public void MoveController(Vector3 displacement)
        {
            controller.Move(displacement);
        }

        public void OnMove(InputValue value)
        {
            moveAxis = value.Get<Vector2>();
        }

        public void OnSprint(InputValue value)
        {
            sprinting = value.isPressed;
        }

        void UpdateWorldMoveDirection()
        {
            Camera sceneCamera = cameraOverride != null ? cameraOverride : Camera.main;

            Vector3 forward = sceneCamera != null ? sceneCamera.transform.forward : Vector3.forward;
            Vector3 right = sceneCamera != null ? sceneCamera.transform.right : Vector3.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            worldMoveDirection = forward * moveAxis.y + right * moveAxis.x;

            if (worldMoveDirection.sqrMagnitude > 1f)
                worldMoveDirection.Normalize();
        }

        void UpdateHorizontalMovement()
        {
            float inputMagnitude = Mathf.Clamp01(moveAxis.sqrMagnitude);
            float animatedMagnitude = movementLocked ? 0f : inputMagnitude * acceleration;

            if (animator != null)
                animator.SetFloat(InputMagnitudeHash, animatedMagnitude, 0.1f, Time.deltaTime);

            if (movementLocked || inputMagnitude <= 0.01f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(worldMoveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

            float speed = movementSpeed * acceleration * (sprinting ? sprintMultiplier : 1f);
            controller.Move(worldMoveDirection * speed * Time.deltaTime);
        }

        void UpdateGravity()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundedStickForce;

            verticalVelocity += gravity * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }
    }
}
