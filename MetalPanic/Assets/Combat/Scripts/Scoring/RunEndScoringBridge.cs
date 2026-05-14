using UnityEngine;

namespace MagnetPanic.Combat.Scoring
{
    /// <summary>
    /// Optional bridge that calls <see cref="ScoringRuntime.EndRun"/> when
    /// <see cref="RunController.OnRunEnded"/> fires. Kept separate so the
    /// scoring system has no compile-time dependency on the run controller —
    /// drop this component into the scene only if you want the run-end hookup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunEndScoringBridge : MonoBehaviour
    {
        [SerializeField] RunController runController;
        [SerializeField] ScoringRuntime scoring;

        void Awake()
        {
            if (runController == null)
                runController = FindFirstObjectByType<RunController>();
            if (scoring == null)
                scoring = ScoringRuntime.Instance != null
                    ? ScoringRuntime.Instance
                    : FindFirstObjectByType<ScoringRuntime>();
        }

        void OnEnable()
        {
            if (runController != null)
                runController.OnRunEnded.AddListener(HandleRunEnded);
        }

        void OnDisable()
        {
            if (runController != null)
                runController.OnRunEnded.RemoveListener(HandleRunEnded);
        }

        void HandleRunEnded(CombatHealth _)
        {
            if (scoring != null)
                scoring.EndRun();
        }
    }
}
