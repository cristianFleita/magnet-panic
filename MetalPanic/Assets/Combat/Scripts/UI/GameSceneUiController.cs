using MagnetPanic.Combat.Scoring;
using MagnetPanic.Combat.Upgrades;
using MagnetPanic.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        [SerializeField] ArkhamSimpleCameraFollow cameraController;
        [SerializeField] UpgradeSystem upgradeSystem;

        [Header("Navigation")]
        [SerializeField] string retrySceneName = "GameScene";
        [SerializeField] string mainMenuSceneName = "MainMenu";
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
        bool upgradeOfferActive;
        float previousTimeScale = 1f;

        void Awake()
        {
            PlayerIdentity.NewRun();
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            ActivateOverlayDocuments();
            ShowDocument(hudDocument);
            EnsureHudBinders();
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

            // Ignore pause input while the level-up upgrade picker is open —
            // otherwise ESC stacks the pause menu behind the choice panel and
            // both modal UIs fight for input (see GDD upgrade-system §Reglas 1).
            if (upgradeOfferActive)
                return;

            if (PausePressedThisFrame())
                SetPaused(!paused);
        }

        public void Resume()
        {
            Debug.Log("[GameSceneUiController] Resume button clicked.", this);
            SetPaused(false);
        }

        public void Retry()
        {
            Debug.Log("[GameSceneUiController] Retry button clicked.", this);
            LoadScene(retrySceneName);
        }

        public void MainMenu()
        {
            Debug.Log("[GameSceneUiController] Main Menu / Quit button clicked.", this);
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
            if (cameraController == null)
                cameraController = FindFirstObjectByType<ArkhamSimpleCameraFollow>();
            if (upgradeSystem == null)
                upgradeSystem = FindFirstObjectByType<UpgradeSystem>();
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
            if (upgradeSystem != null)
            {
                upgradeSystem.OnUpgradeOfferShown.AddListener(HandleUpgradeOfferShown);
                upgradeSystem.OnUpgradeOfferHidden.AddListener(HandleUpgradeOfferHidden);
            }
        }

        void UnbindRunEvents()
        {
            if (runController != null)
                runController.OnRunEnded.RemoveListener(HandleRunEnded);
            if (scoring != null)
                scoring.OnRunEnded.RemoveListener(HandleScoringRunEnded);
            if (upgradeSystem != null)
            {
                upgradeSystem.OnUpgradeOfferShown.RemoveListener(HandleUpgradeOfferShown);
                upgradeSystem.OnUpgradeOfferHidden.RemoveListener(HandleUpgradeOfferHidden);
            }
        }

        void HandleUpgradeOfferShown() => upgradeOfferActive = true;
        void HandleUpgradeOfferHidden() => upgradeOfferActive = false;

        void CacheHudElements()
        {
            VisualElement root = hudDocument != null ? hudDocument.rootVisualElement : null;
            UiDocumentQuery.TryGetLabel(root, "time-value", out timeLabel);
        }

        // Ensures the HUD UIDocument GameObject has the per-section binders
        // attached. Lets the scene work with just the HudDocument + this
        // controller — no extra inspector wiring required for the new sections.
        void EnsureHudBinders()
        {
            if (hudDocument == null)
                return;

            GameObject host = hudDocument.gameObject;
            if (host.GetComponent<ActivePowerupHud>() == null)
                host.AddComponent<ActivePowerupHud>();
            if (host.GetComponent<OverloadMeterHud>() == null)
                host.AddComponent<OverloadMeterHud>();
            if (host.GetComponent<UpgradeStripHud>() == null)
                host.AddComponent<UpgradeStripHud>();
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
            Debug.Log($"[GameSceneUiController] SetPaused -> {paused}", this);

            if (paused)
            {
                if (freezeTimeWhilePaused)
                {
                    previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                    Time.timeScale = 0f;
                }

                if (inputProvider != null)
                    inputProvider.SetState(GameInputState.UI);

                if (cameraController != null)
                    cameraController.SetLookEnabled(false);
                else
                    ReleaseCursor();

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

                if (cameraController != null)
                    cameraController.SetLookEnabled(true);
            }
        }

        static void ReleaseCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        void BindPauseButtons()
        {
            VisualElement root = pauseDocument != null ? pauseDocument.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogWarning("[GameSceneUiController] Pause document root is null while binding buttons.", this);
                return;
            }

            if (UiDocumentQuery.TryGetButton(root, "resume-button", out Button resume))
            {
                if (resumeButton != null && resumeButton != resume)
                    resumeButton.clicked -= Resume;

                resumeButton = resume;
                resumeButton.clicked -= Resume;
                resumeButton.clicked += Resume;
            }
            else
            {
                Debug.LogWarning("[GameSceneUiController] resume-button not found in pause document.", this);
            }

            if (UiDocumentQuery.TryGetButton(root, "quit-button", out Button quit))
            {
                if (quitButton != null && quitButton != quit)
                    quitButton.clicked -= MainMenu;

                quitButton = quit;
                quitButton.clicked -= MainMenu;
                quitButton.clicked += MainMenu;
            }
            else
            {
                Debug.LogWarning("[GameSceneUiController] quit-button not found in pause document.", this);
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
            Debug.Log("[GameOver] ShowGameOver invoked.", this);

            if (paused)
            {
                paused = false;
                UnbindPauseButtons();
                HideDocument(pauseDocument);
            }

            // Switch input to UI map (same pattern as pause) so UI Toolkit clicks aren't
            // shadowed by gameplay action map. SetInputEnabled(false) leaves PlayerInput
            // on the Player map, which intermittently blocks pointer events on the panel.
            if (inputProvider != null)
                inputProvider.SetState(GameInputState.UI);

            if (cameraController != null)
                cameraController.SetLookEnabled(false);
            else
                ReleaseCursor();

            EnsureUiInputAlive();

            if (freezeTimeOnGameOver)
            {
                previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }

            long scoreValue = stats != null ? stats.FinalScore : scoring != null ? scoring.Score : 0L;
            float survivalSeconds = stats != null
                ? stats.SurvivalTimeSeconds
                : runController != null ? runController.RunDuration : 0f;
            string playerName = PlayerIdentity.CurrentName;
            Debug.Log($"[GameOver] score={scoreValue} survival={survivalSeconds:F1}s player={playerName}", this);

            ActivateDocumentHost(gameOverTopFiveDocument);
            ActivateDocumentHost(gameOverNoTopFiveDocument);
            HideDocument(gameOverTopFiveDocument);
            HideDocument(gameOverNoTopFiveDocument);

            LeaderboardController client = topFiveLeaderboard != null ? topFiveLeaderboard : noTopFiveLeaderboard;
            if (client == null)
            {
                Debug.LogWarning("[GameOver] No LeaderboardController assigned; falling back to top-five layout without backend.", this);
                PresentLayout(gameOverTopFiveDocument, scoreValue, survivalSeconds);
                return;
            }

            Debug.Log("[GameOver] Fetching leaderboard...", this);
            client.FetchTopFive(entries => RouteByLeaderboard(entries, playerName, scoreValue, survivalSeconds));
        }

        static void EnsureUiInputAlive()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject host = new GameObject("EventSystem");
                eventSystem = host.AddComponent<EventSystem>();
                host.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[GameOver] Created missing EventSystem for UI input.");
                return;
            }

            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[GameOver] Added InputSystemUIInputModule to existing EventSystem.");
            }

            // Kick the module so pointer bindings reattach cleanly.
            module.enabled = false;
            module.enabled = true;
        }

        void RouteByLeaderboard(LeaderboardController.LeaderboardEntry[] entries, string playerName, long scoreValue, float survivalSeconds)
        {
            if (entries == null)
            {
                Debug.LogWarning("[GameOver] Leaderboard fetch failed; showing top-five layout without submission.", this);
                PresentLayout(gameOverTopFiveDocument, scoreValue, survivalSeconds);
                return;
            }

            bool madeTopFive = WouldEnterTopFive(entries, scoreValue);
            long cutoff = entries.Length >= LeaderboardController.TopCount ? entries[LeaderboardController.TopCount - 1].score : 0L;
            Debug.Log($"[GameOver] Leaderboard ready. entries={entries.Length} cutoff={cutoff} ourScore={scoreValue} madeTopFive={madeTopFive}", this);

            UIDocument chosen = madeTopFive ? gameOverTopFiveDocument : gameOverNoTopFiveDocument;
            UIDocument other = madeTopFive ? gameOverNoTopFiveDocument : gameOverTopFiveDocument;

            HideDocument(other);
            PresentLayout(chosen, scoreValue, survivalSeconds);

            if (madeTopFive)
            {
                if (topFiveLeaderboard != null)
                {
                    LeaderboardController.LeaderboardEntry[] optimistic = OptimisticInsert(entries, playerName, scoreValue);
                    topFiveLeaderboard.RenderTopFive(optimistic, playerName, scoreValue);
                    Debug.Log("[GameOver] Rendered top-five layout optimistically. Submitting score...", this);
                    BindGameOverButtons(chosen);

                    topFiveLeaderboard.SubmitScore(playerName, scoreValue, updated =>
                    {
                        if (!gameOver)
                        {
                            Debug.Log("[GameOver] Submit returned after gameOver was reset; ignoring.", this);
                            return;
                        }

                        LeaderboardController.LeaderboardEntry[] toRender = updated != null ? updated : optimistic;
                        Debug.Log($"[GameOver] Submit complete. server-entries={(updated != null ? updated.Length : -1)}. Re-rendering.", this);
                        topFiveLeaderboard.RenderTopFive(toRender, playerName, scoreValue);
                        BindGameOverButtons(chosen);
                    });
                }
                else
                {
                    Debug.LogWarning("[GameOver] topFiveLeaderboard not assigned.", this);
                }
            }
            else
            {
                if (noTopFiveLeaderboard != null)
                {
                    noTopFiveLeaderboard.RenderMissedTopFive(entries, playerName, scoreValue);
                    Debug.Log("[GameOver] Rendered missed-top-five layout.", this);
                    BindGameOverButtons(chosen);
                }
                else
                {
                    Debug.LogWarning("[GameOver] noTopFiveLeaderboard not assigned.", this);
                }
            }
        }

        void PresentLayout(UIDocument document, long scoreValue, float survivalSeconds)
        {
            ActivateDocumentHost(document);
            RefreshDocumentPanel(document);
            ShowDocument(document);
            UpdateGameOverLabels(document, scoreValue, survivalSeconds);
            BindGameOverButtons(document);
            EnsurePanelPickable(document);
            Debug.Log($"[GameOver] Presented layout '{(document != null ? document.name : "null")}'.", this);
        }

        static void EnsurePanelPickable(UIDocument document)
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Position;
            // Make sure the root sits above other docs on the same panel.
            root.BringToFront();
        }

        static void RefreshDocumentPanel(UIDocument document)
        {
            if (document == null)
                return;

            PanelSettings settings = document.panelSettings;
            if (settings == null)
                return;

            // Re-assigning panelSettings forces UIDocument to rebuild the panel hierarchy,
            // which re-attaches the runtime panel to InputSystemUIInputModule reliably.
            document.panelSettings = null;
            document.panelSettings = settings;
        }

        static bool WouldEnterTopFive(LeaderboardController.LeaderboardEntry[] entries, long score)
        {
            if (entries.Length < LeaderboardController.TopCount)
                return true;
            return score > entries[LeaderboardController.TopCount - 1].score;
        }

        static LeaderboardController.LeaderboardEntry[] OptimisticInsert(
            LeaderboardController.LeaderboardEntry[] entries, string playerName, long playerScore)
        {
            int sourceCount = entries != null ? entries.Length : 0;
            int total = sourceCount + 1;
            LeaderboardController.LeaderboardEntry[] merged = new LeaderboardController.LeaderboardEntry[total];
            for (int i = 0; i < sourceCount; i++)
                merged[i] = entries[i];
            merged[sourceCount] = new LeaderboardController.LeaderboardEntry(0, playerName, playerScore);

            System.Array.Sort(merged, (a, b) => b.score.CompareTo(a.score));

            int resultCount = Mathf.Min(merged.Length, LeaderboardController.TopCount);
            LeaderboardController.LeaderboardEntry[] result = new LeaderboardController.LeaderboardEntry[resultCount];
            for (int i = 0; i < resultCount; i++)
                result[i] = new LeaderboardController.LeaderboardEntry(i + 1, merged[i].name, merged[i].score);
            return result;
        }

        void UpdateGameOverLabels(UIDocument document, long scoreValue, float survivalSeconds)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;

            if (UiDocumentQuery.TryGetLabel(root, "final-score-value", out Label finalScore))
                finalScore.text = scoreValue.ToString("N0") + " PTS";
            if (UiDocumentQuery.TryGetLabel(root, "survival-time-value", out Label survival))
                survival.text = FormatTime(survivalSeconds) + " MIN";
        }

        void BindGameOverButtons(UIDocument document)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogWarning("[GameOver] BindGameOverButtons: document root is null.", this);
                return;
            }

            if (UiDocumentQuery.TryGetButton(root, "retry-button", out Button retry))
            {
                if (retryButton != null && retryButton != retry)
                    retryButton.clicked -= Retry;

                retryButton = retry;
                retryButton.clicked -= Retry;
                retryButton.clicked += Retry;
                Debug.Log("[GameOver] retry-button bound.", this);
            }
            else
            {
                Debug.LogWarning("[GameOver] retry-button NOT FOUND in document.", this);
            }

            if (UiDocumentQuery.TryGetButton(root, "main-menu-button", out Button menu))
            {
                if (mainMenuButton != null && mainMenuButton != menu)
                    mainMenuButton.clicked -= MainMenu;

                mainMenuButton = menu;
                mainMenuButton.clicked -= MainMenu;
                mainMenuButton.clicked += MainMenu;
                Debug.Log("[GameOver] main-menu-button bound.", this);
            }
            else
            {
                Debug.LogWarning("[GameOver] main-menu-button NOT FOUND in document.", this);
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
            ReleaseCursor();

            if (inputProvider != null)
                inputProvider.SetState(GameInputState.Disabled);

            Pool.ReleaseAll();
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
