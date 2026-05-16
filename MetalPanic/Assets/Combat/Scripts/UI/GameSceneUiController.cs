using MagnetPanic.Combat.Scoring;
using MagnetPanic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat
{
    [DisallowMultipleComponent]
    public sealed class GameSceneUiController : MonoBehaviour
    {
        [Header("Documents")]
        [SerializeField] UIDocument hudDocument;
        [SerializeField] UIDocument pauseDocument;
        [SerializeField] UIDocument gameOverTopFiveDocument;
        [SerializeField] UIDocument gameOverNoTopFiveDocument;

        [Header("Runtime")]
        [SerializeField] RunController runController;
        [SerializeField] ScoringRuntime scoring;
        [SerializeField] GameInputProvider inputProvider;

        [Header("Navigation")]
        [SerializeField] string retrySceneName = "GameScene";
        [SerializeField] string mainMenuSceneName = "MainMenu";
        [SerializeField] string leaderboardPlayerName = "PLAYER";
        [SerializeField] bool showTopFiveLayout = true;
        [SerializeField] bool freezeTimeWhilePaused = true;
        [SerializeField] bool freezeTimeOnGameOver = true;
        [SerializeField] LeaderboardController topFiveLeaderboard;
        [SerializeField] LeaderboardController noTopFiveLeaderboard;

        Button resumeButton;
        Button quitButton;
        Button retryButton;
        Button mainMenuButton;
        Label timeLabel;
        bool paused;
        bool gameOver;
        float previousTimeScale = 1f;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            ActivateOverlayDocuments();
            ShowDocument(hudDocument);
            CacheHudElements();
            HideDocument(pauseDocument);
            HideDocument(gameOverTopFiveDocument);
            HideDocument(gameOverNoTopFiveDocument);
            BindPauseButtons();
            BindRunEvents();
        }

        void OnDisable()
        {
            UnbindRunEvents();
            UnbindPauseButtons();
            UnbindGameOverButtons();

            if (paused && !gameOver && freezeTimeWhilePaused)
                Time.timeScale = previousTimeScale;
        }

        void Update()
        {
            UpdateHudTimer();

            if (gameOver)
                return;

            if (PausePressedThisFrame())
                SetPaused(!paused);
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void Retry()
        {
            LoadScene(retrySceneName);
        }

        public void MainMenu()
        {
            LoadScene(mainMenuSceneName);
        }

        void ResolveReferences()
        {
            if (runController == null)
                runController = FindFirstObjectByType<RunController>();
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null ? ScoringRuntime.Instance : FindFirstObjectByType<ScoringRuntime>();
            if (inputProvider == null)
                inputProvider = FindFirstObjectByType<GameInputProvider>();
        }

        void ActivateOverlayDocuments()
        {
            ActivateDocumentHost(pauseDocument);
            ActivateDocumentHost(gameOverTopFiveDocument);
            ActivateDocumentHost(gameOverNoTopFiveDocument);
        }

        void BindRunEvents()
        {
            if (runController != null)
                runController.OnRunEnded.AddListener(HandleRunEnded);
            if (scoring != null)
                scoring.OnRunEnded.AddListener(HandleScoringRunEnded);
        }

        void UnbindRunEvents()
        {
            if (runController != null)
                runController.OnRunEnded.RemoveListener(HandleRunEnded);
            if (scoring != null)
                scoring.OnRunEnded.RemoveListener(HandleScoringRunEnded);
        }

        void CacheHudElements()
        {
            VisualElement root = hudDocument != null ? hudDocument.rootVisualElement : null;
            UiDocumentQuery.TryGetLabel(root, "time-value", out timeLabel);
        }

        void UpdateHudTimer()
        {
            if (timeLabel == null || gameOver)
                return;

            float seconds = runController != null ? runController.RunDuration : 0f;
            timeLabel.text = FormatTime(seconds);
        }

        bool PausePressedThisFrame()
        {
            if (inputProvider != null && inputProvider.PausePressed)
                return true;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        void SetPaused(bool shouldPause)
        {
            if (gameOver || paused == shouldPause)
                return;

            paused = shouldPause;

            if (paused)
            {
                if (freezeTimeWhilePaused)
                {
                    previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                    Time.timeScale = 0f;
                }

                if (inputProvider != null)
                    inputProvider.SetState(GameInputState.UI);

                ShowDocument(pauseDocument);
                BindPauseButtons();
            }
            else
            {
                HideDocument(pauseDocument);

                if (freezeTimeWhilePaused)
                    Time.timeScale = previousTimeScale;

                if (inputProvider != null)
                    inputProvider.SetState(GameInputState.Gameplay);
            }
        }

        void BindPauseButtons()
        {
            VisualElement root = pauseDocument != null ? pauseDocument.rootVisualElement : null;

            if (UiDocumentQuery.TryGetButton(root, "resume-button", out Button resume))
            {
                resumeButton = resume;
                resumeButton.clicked -= Resume;
                resumeButton.clicked += Resume;
            }

            if (UiDocumentQuery.TryGetButton(root, "quit-button", out Button quit))
            {
                quitButton = quit;
                quitButton.clicked -= MainMenu;
                quitButton.clicked += MainMenu;
            }
        }

        void UnbindPauseButtons()
        {
            if (resumeButton != null)
                resumeButton.clicked -= Resume;
            if (quitButton != null)
                quitButton.clicked -= MainMenu;

            resumeButton = null;
            quitButton = null;
        }

        void HandleRunEnded(CombatHealth _)
        {
            if (scoring == null)
                ShowGameOver(null);
        }

        void HandleScoringRunEnded(RunStats stats)
        {
            ShowGameOver(stats);
        }

        void ShowGameOver(RunStats stats)
        {
            if (gameOver)
                return;

            gameOver = true;

            if (paused)
            {
                paused = false;
                UnbindPauseButtons();
                HideDocument(pauseDocument);
            }

            if (inputProvider != null)
                inputProvider.SetInputEnabled(false);

            if (freezeTimeOnGameOver)
            {
                previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }

            UIDocument document = showTopFiveLayout ? gameOverTopFiveDocument : gameOverNoTopFiveDocument;
            UIDocument other = showTopFiveLayout ? gameOverNoTopFiveDocument : gameOverTopFiveDocument;
            HideDocument(other);
            ShowDocument(document);
            UpdateGameOverLabels(document, stats);
            SubmitLeaderboardScore(document, stats);
            BindGameOverButtons(document);
        }

        void UpdateGameOverLabels(UIDocument document, RunStats stats)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            long scoreValue = stats != null ? stats.FinalScore : scoring != null ? scoring.Score : 0;
            float survivalSeconds = stats != null
                ? stats.SurvivalTimeSeconds
                : runController != null ? runController.RunDuration : 0f;

            if (UiDocumentQuery.TryGetLabel(root, "final-score-value", out Label finalScore))
                finalScore.text = scoreValue.ToString("N0") + " PTS";
            if (UiDocumentQuery.TryGetLabel(root, "survival-time-value", out Label survival))
                survival.text = FormatTime(survivalSeconds) + " MIN";
        }

        void SubmitLeaderboardScore(UIDocument document, RunStats stats)
        {
            LeaderboardController leaderboard = showTopFiveLayout ? topFiveLeaderboard : noTopFiveLeaderboard;
            if (leaderboard == null && document != null)
                leaderboard = document.GetComponent<LeaderboardController>();

            if (leaderboard == null)
                return;

            long scoreValue = stats != null ? stats.FinalScore : scoring != null ? scoring.Score : 0;
            if (scoreValue > 0)
                leaderboard.SubmitScore(leaderboardPlayerName, scoreValue);
            else
                leaderboard.Refresh();
        }

        void BindGameOverButtons(UIDocument document)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;

            if (UiDocumentQuery.TryGetButton(root, "retry-button", out Button retry))
            {
                if (retryButton != null && retryButton != retry)
                    retryButton.clicked -= Retry;

                retryButton = retry;
                retryButton.clicked -= Retry;
                retryButton.clicked += Retry;
            }

            if (UiDocumentQuery.TryGetButton(root, "main-menu-button", out Button menu))
            {
                if (mainMenuButton != null && mainMenuButton != menu)
                    mainMenuButton.clicked -= MainMenu;

                mainMenuButton = menu;
                mainMenuButton.clicked -= MainMenu;
                mainMenuButton.clicked += MainMenu;
            }
        }

        void UnbindGameOverButtons()
        {
            if (retryButton != null)
                retryButton.clicked -= Retry;
            if (mainMenuButton != null)
                mainMenuButton.clicked -= MainMenu;

            retryButton = null;
            mainMenuButton = null;
        }

        static void ShowDocument(UIDocument document)
        {
            if (document == null)
                return;

            ActivateDocumentHost(document);

            if (document.rootVisualElement != null)
                document.rootVisualElement.style.display = DisplayStyle.Flex;
        }

        static void HideDocument(UIDocument document)
        {
            if (document == null)
                return;

            if (document.rootVisualElement != null)
                document.rootVisualElement.style.display = DisplayStyle.None;
        }

        static void ActivateDocumentHost(UIDocument document)
        {
            if (document != null && !document.gameObject.activeSelf)
                document.gameObject.SetActive(true);
        }

        static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainder = totalSeconds % 60;
            return minutes.ToString("00") + ":" + remainder.ToString("00");
        }

        void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[GameSceneUiController] Scene name is empty.", this);
                return;
            }

            Time.timeScale = 1f;
            Pool.ReleaseAll();
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
