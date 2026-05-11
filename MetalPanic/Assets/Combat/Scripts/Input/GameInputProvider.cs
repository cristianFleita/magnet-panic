using UnityEngine;
using UnityEngine.InputSystem;

namespace MagnetPanic.Combat
{
    [DisallowMultipleComponent]
    public sealed class GameInputProvider : MonoBehaviour
    {
        const string PlayerActionMap = "Player";
        const string UiActionMap = "UI";

        [Header("References")]
        [SerializeField] PlayerInput playerInput;
        [SerializeField] Camera cameraOverride;

        [Header("Buffering")]
        [SerializeField, Range(0.05f, 0.3f)] float bufferWindow = 0.15f;

        [Header("Aim")]
        [SerializeField, Range(0.05f, 0.3f)] float gamepadAimDeadzone = 0.15f;

        readonly GameInputBuffer combatBuffer = new GameInputBuffer();

        GameInputState state = GameInputState.Gameplay;
        Vector2 moveAxis;
        Vector2 aimScreenPosition;
        Vector2 stickAim;
        Vector3 aimWorldDirection = Vector3.forward;
        bool inputEnabled = true;
        bool pullTogglePressed;
        bool strikePressed;
        bool counterPressed;
        bool sprintPressed;
        bool upgrade1Pressed;
        bool upgrade2Pressed;
        bool upgrade3Pressed;
        bool pausePressed;

        public float BufferWindow
        {
            get => bufferWindow;
            set => bufferWindow = Mathf.Clamp(value, 0.05f, 0.3f);
        }

        public GameInputState State => state;
        public Vector2 MoveAxis => GameplayEnabled ? moveAxis : Vector2.zero;
        public Vector2 AimScreenPosition => aimScreenPosition;
        public Vector3 AimWorldDirection => aimWorldDirection;
        public Camera SceneCamera => cameraOverride;
        public bool PullTogglePressed => GameplayEnabled && pullTogglePressed;
        public bool StrikePressed => GameplayEnabled && strikePressed;
        public bool CounterPressed => GameplayEnabled && counterPressed;
        public bool SprintPressed => GameplayEnabled && sprintPressed;
        public bool Upgrade1Pressed => UiEnabled && upgrade1Pressed;
        public bool Upgrade2Pressed => UiEnabled && upgrade2Pressed;
        public bool Upgrade3Pressed => UiEnabled && upgrade3Pressed;
        public bool PausePressed => inputEnabled && state != GameInputState.Disabled && pausePressed;

        bool GameplayEnabled => inputEnabled && state == GameInputState.Gameplay;
        bool UiEnabled => inputEnabled && state == GameInputState.UI;

        public static GameInputProvider EnsureOn(GameObject owner)
        {
            GameInputProvider provider = owner.GetComponent<GameInputProvider>();
            return provider != null ? provider : owner.AddComponent<GameInputProvider>();
        }

        void Awake()
        {
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            if (cameraOverride == null)
                cameraOverride = Camera.main;

            if (aimWorldDirection.sqrMagnitude <= 0.01f)
                aimWorldDirection = transform.forward.sqrMagnitude > 0.01f ? transform.forward : Vector3.forward;
        }

        void Update()
        {
            UpdateAim();
        }

        void LateUpdate()
        {
            ClearFramePresses();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ResetInputState();
        }

        public void Configure(Camera sceneCamera)
        {
            cameraOverride = sceneCamera;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
                ResetInputState();
        }

        public void SetState(GameInputState nextState)
        {
            state = nextState;
            ClearFramePresses();

            if (playerInput == null)
                return;

            if (state == GameInputState.Disabled)
            {
                playerInput.DeactivateInput();
                return;
            }

            if (!playerInput.inputIsActive)
                playerInput.ActivateInput();

            switch (state)
            {
                case GameInputState.Gameplay:
                    playerInput.SwitchCurrentActionMap(PlayerActionMap);
                    break;
                case GameInputState.UI:
                    playerInput.SwitchCurrentActionMap(UiActionMap);
                    break;
            }
        }

        public bool ConsumeBuffered(GameInputIntent intent)
        {
            if (!GameplayEnabled || !IsBufferedCombatIntent(intent))
                return false;

            return combatBuffer.Consume(intent, Time.time, bufferWindow);
        }

        public void OnMove(InputValue value)
        {
            moveAxis = value.Get<Vector2>();
        }

        public void OnAim(InputValue value)
        {
            stickAim = value.Get<Vector2>();
        }

        public void OnLook(InputValue value)
        {
            OnAim(value);
        }

        public void OnSprint(InputValue value)
        {
            SetSprintPressed(value.isPressed);
        }

        public void SetSprintPressed(bool pressed)
        {
            sprintPressed = pressed;
        }

        public void OnPullToggle(InputValue value)
        {
            if (value.isPressed)
                RegisterCombatPress(GameInputIntent.PullToggle);
        }

        public void OnPullToggle()
        {
            RegisterCombatPress(GameInputIntent.PullToggle);
        }

        public void OnPull(InputValue value)
        {
            OnPullToggle(value);
        }

        public void OnPull()
        {
            OnPullToggle();
        }

        public void OnStrike(InputValue value)
        {
            if (value.isPressed)
                RegisterCombatPress(GameInputIntent.Strike);
        }

        public void OnStrike()
        {
            RegisterCombatPress(GameInputIntent.Strike);
        }

        public void OnAttack(InputValue value)
        {
            OnStrike(value);
        }

        public void OnAttack()
        {
            OnStrike();
        }

        public void OnCounter(InputValue value)
        {
            if (value.isPressed)
                RegisterCombatPress(GameInputIntent.Counter);
        }

        public void OnCounter()
        {
            RegisterCombatPress(GameInputIntent.Counter);
        }

        public void OnJump(InputValue value)
        {
            OnCounter(value);
        }

        public void OnJump()
        {
            OnCounter();
        }

        public void OnDodge(InputValue value)
        {
            OnCounter(value);
        }

        public void OnDodge()
        {
            OnCounter();
        }

        public void OnUpgrade1(InputValue value)
        {
            if (value.isPressed)
                RegisterUiPress(GameInputIntent.Upgrade1);
        }

        public void OnUpgrade1()
        {
            RegisterUiPress(GameInputIntent.Upgrade1);
        }

        public void OnPrevious(InputValue value)
        {
            OnUpgrade1(value);
        }

        public void OnUpgrade2(InputValue value)
        {
            if (value.isPressed)
                RegisterUiPress(GameInputIntent.Upgrade2);
        }

        public void OnUpgrade2()
        {
            RegisterUiPress(GameInputIntent.Upgrade2);
        }

        public void OnNext(InputValue value)
        {
            OnUpgrade2(value);
        }

        public void OnUpgrade3(InputValue value)
        {
            if (value.isPressed)
                RegisterUiPress(GameInputIntent.Upgrade3);
        }

        public void OnUpgrade3()
        {
            RegisterUiPress(GameInputIntent.Upgrade3);
        }

        public void OnPause(InputValue value)
        {
            if (value.isPressed)
                RegisterPausePress();
        }

        public void OnPause()
        {
            RegisterPausePress();
        }

        public void OnCancel(InputValue value)
        {
            OnPause(value);
        }

        void RegisterCombatPress(GameInputIntent intent)
        {
            if (!GameplayEnabled)
                return;

            if (intent == GameInputIntent.Counter)
            {
                counterPressed = true;
                strikePressed = false;
                combatBuffer.Clear(GameInputIntent.Strike);
            }
            else if (intent == GameInputIntent.Strike)
            {
                if (counterPressed)
                    return;

                strikePressed = true;
            }
            else if (intent == GameInputIntent.PullToggle)
            {
                pullTogglePressed = true;
            }

            combatBuffer.Record(intent, Time.time);
        }

        void RegisterUiPress(GameInputIntent intent)
        {
            if (!UiEnabled)
                return;

            switch (intent)
            {
                case GameInputIntent.Upgrade1:
                    upgrade1Pressed = true;
                    break;
                case GameInputIntent.Upgrade2:
                    upgrade2Pressed = true;
                    break;
                case GameInputIntent.Upgrade3:
                    upgrade3Pressed = true;
                    break;
            }
        }

        void RegisterPausePress()
        {
            if (!inputEnabled || state == GameInputState.Disabled)
                return;

            pausePressed = true;
        }

        void UpdateAim()
        {
            Camera sceneCamera = cameraOverride != null ? cameraOverride : Camera.main;
            if (sceneCamera == null)
            {
                UpdateAimFromStick(null);
                return;
            }

            if (Mouse.current != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                if (IsInsideScreen(pointer))
                {
                    aimScreenPosition = pointer;
                    Ray ray = sceneCamera.ScreenPointToRay(pointer);
                    Plane ground = new Plane(Vector3.up, transform.position);
                    if (ground.Raycast(ray, out float distance))
                    {
                        Vector3 hit = ray.GetPoint(distance);
                        Vector3 direction = hit - transform.position;
                        direction.y = 0f;
                        if (direction.sqrMagnitude > 0.01f)
                        {
                            aimWorldDirection = direction.normalized;
                            return;
                        }
                    }
                }
            }

            UpdateAimFromStick(sceneCamera);
        }

        void UpdateAimFromStick(Camera sceneCamera)
        {
            if (stickAim.sqrMagnitude > 1.21f)
                return;

            if (stickAim.sqrMagnitude <= gamepadAimDeadzone * gamepadAimDeadzone)
                return;

            Vector3 forward = sceneCamera != null ? sceneCamera.transform.forward : Vector3.forward;
            Vector3 right = sceneCamera != null ? sceneCamera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 direction = right * stickAim.x + forward * stickAim.y;
            if (direction.sqrMagnitude > 0.01f)
                aimWorldDirection = direction.normalized;
        }

        static bool IsInsideScreen(Vector2 position)
        {
            return position.x >= 0f
                && position.y >= 0f
                && position.x <= Screen.width
                && position.y <= Screen.height;
        }

        void ResetInputState()
        {
            moveAxis = Vector2.zero;
            stickAim = Vector2.zero;
            sprintPressed = false;
            combatBuffer.Reset();
            ClearFramePresses();
        }

        void ClearFramePresses()
        {
            pullTogglePressed = false;
            strikePressed = false;
            counterPressed = false;
            upgrade1Pressed = false;
            upgrade2Pressed = false;
            upgrade3Pressed = false;
            pausePressed = false;
        }

        static bool IsBufferedCombatIntent(GameInputIntent intent)
        {
            return intent == GameInputIntent.PullToggle
                || intent == GameInputIntent.Strike
                || intent == GameInputIntent.Counter;
        }
    }
}
