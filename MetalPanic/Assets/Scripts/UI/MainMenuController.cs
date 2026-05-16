using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MagnetPanic.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string PlayRunButtonName = "play-run-button";
        public const string ControlsGuideButtonName = "controls-guide-button";
        public const string BackToMenuButtonName = "back-to-menu-button";

        [SerializeField] UIDocument mainMenuDocument;
        [SerializeField] UIDocument guideDocument;
        [SerializeField] GameObject guideView;
        [SerializeField] string gameSceneName = "GameScene";
        [SerializeField] bool showMenuOnEnable = true;

        Button playRunButton;
        Button controlsGuideButton;
        Button backToMenuButton;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            EnsureCursorUsable();
            EnsureUiInputAlive();
            RefreshDocumentPanel(mainMenuDocument);
            RefreshDocumentPanel(guideDocument);

            if (showMenuOnEnable)
                ShowMenu();
            else
                BindMenuButtons();
        }

        static void EnsureCursorUsable()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        static void EnsureUiInputAlive()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject host = new GameObject("EventSystem");
                eventSystem = host.AddComponent<EventSystem>();
                host.AddComponent<InputSystemUIInputModule>();
                return;
            }

            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            module.enabled = false;
            module.enabled = true;
        }

        static void RefreshDocumentPanel(UIDocument document)
        {
            if (document == null)
                return;

            PanelSettings settings = document.panelSettings;
            if (settings == null)
                return;

            document.panelSettings = null;
            document.panelSettings = settings;
        }

        void OnDisable()
        {
            UnbindMenuButtons();
            UnbindGuideButtons();
        }

        public void ShowMenu()
        {
            UnbindGuideButtons();

            SetDocumentVisible(mainMenuDocument, true);

            if (guideView != null)
                guideView.SetActive(false);

            BindMenuButtons();
        }

        public void ShowGuide()
        {
            UnbindMenuButtons();
            SetDocumentVisible(mainMenuDocument, false);

            if (guideView != null)
                guideView.SetActive(true);

            ResolveReferences();
            BindGuideButtons();
        }

        public void LoadRunScene()
        {
            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogWarning("[MainMenuController] Game scene name is empty.", this);
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        void ResolveReferences()
        {
            if (mainMenuDocument == null)
                mainMenuDocument = GetComponent<UIDocument>();

            if (guideView == null)
                guideView = GameObject.Find("MainMenu_Guide");

            if (guideDocument == null && guideView != null)
                guideDocument = guideView.GetComponent<UIDocument>();
        }

        void BindMenuButtons()
        {
            VisualElement root = mainMenuDocument != null ? mainMenuDocument.rootVisualElement : null;

            if (UiDocumentQuery.TryGetButton(root, PlayRunButtonName, out Button play))
            {
                if (playRunButton != play)
                    UnbindPlayRunButton();

                playRunButton = play;
                playRunButton.clicked -= LoadRunScene;
                playRunButton.clicked += LoadRunScene;
            }

            if (UiDocumentQuery.TryGetButton(root, ControlsGuideButtonName, out Button guide))
            {
                if (controlsGuideButton != guide)
                    UnbindControlsGuideButton();

                controlsGuideButton = guide;
                controlsGuideButton.clicked -= ShowGuide;
                controlsGuideButton.clicked += ShowGuide;
            }
        }

        void BindGuideButtons()
        {
            VisualElement root = guideDocument != null ? guideDocument.rootVisualElement : null;

            if (!UiDocumentQuery.TryGetButton(root, BackToMenuButtonName, out Button back))
                return;

            if (backToMenuButton != back)
                UnbindBackToMenuButton();

            backToMenuButton = back;
            backToMenuButton.clicked -= ShowMenu;
            backToMenuButton.clicked += ShowMenu;
        }

        void UnbindMenuButtons()
        {
            UnbindPlayRunButton();
            UnbindControlsGuideButton();
        }

        void UnbindGuideButtons()
        {
            UnbindBackToMenuButton();
        }

        void UnbindPlayRunButton()
        {
            if (playRunButton != null)
                playRunButton.clicked -= LoadRunScene;

            playRunButton = null;
        }

        void UnbindControlsGuideButton()
        {
            if (controlsGuideButton != null)
                controlsGuideButton.clicked -= ShowGuide;

            controlsGuideButton = null;
        }

        void UnbindBackToMenuButton()
        {
            if (backToMenuButton != null)
                backToMenuButton.clicked -= ShowMenu;

            backToMenuButton = null;
        }

        static void SetDocumentVisible(UIDocument document, bool visible)
        {
            if (document == null || document.rootVisualElement == null)
                return;

            document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
