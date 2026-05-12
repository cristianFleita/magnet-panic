using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Wires the player's death event to stop the wave director and exposes
    /// a single hook (OnRunEnded) for game-over UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] ScrapDirector scrapDirector;
        [SerializeField] HealingDirector healingDirector;
        [SerializeField] CombatHealth playerHealth;

        [Header("Behavior")]
        [SerializeField] bool resolveFromBootstrap = true;
        [SerializeField] bool freezeTimeOnEnd = false;

        public UnityEvent<CombatHealth> OnRunEnded = new UnityEvent<CombatHealth>();

        bool runEnded;
        float runStartTime;

        public float RunDuration => runEnded
            ? runEndTime - runStartTime
            : Time.time - runStartTime;

        public bool RunEnded => runEnded;
        float runEndTime;

        void Start()
        {
            if (resolveFromBootstrap)
                Resolve();

            if (playerHealth != null)
                playerHealth.OnDeath.AddListener(HandlePlayerDeath);

            runStartTime = Time.time;
        }

        void OnDestroy()
        {
            if (playerHealth != null)
                playerHealth.OnDeath.RemoveListener(HandlePlayerDeath);
        }

        public void BindPlayerHealth(CombatHealth health)
        {
            if (playerHealth != null)
                playerHealth.OnDeath.RemoveListener(HandlePlayerDeath);

            playerHealth = health;

            if (playerHealth != null)
                playerHealth.OnDeath.AddListener(HandlePlayerDeath);
        }

        void Resolve()
        {
            if (waveDirector == null)
                waveDirector = GetComponent<WaveDirector>();
            if (waveDirector == null)
                waveDirector = FindFirstObjectByType<WaveDirector>();

            if (scrapDirector == null)
                scrapDirector = GetComponent<ScrapDirector>();
            if (scrapDirector == null)
                scrapDirector = FindFirstObjectByType<ScrapDirector>();

            if (healingDirector == null)
                healingDirector = GetComponent<HealingDirector>();
            if (healingDirector == null)
                healingDirector = FindFirstObjectByType<HealingDirector>();

            if (playerHealth == null)
            {
                RunBootstrap bootstrap = GetComponent<RunBootstrap>();
                Transform player = bootstrap != null ? bootstrap.Player : null;
                if (player == null)
                {
                    ArkhamCombatController combat = FindFirstObjectByType<ArkhamCombatController>();
                    if (combat != null)
                        player = combat.transform;
                }

                if (player != null)
                    playerHealth = player.GetComponentInChildren<CombatHealth>();
            }
        }

        void HandlePlayerDeath(CombatHealth health)
        {
            if (runEnded)
                return;

            runEnded = true;
            runEndTime = Time.time;

            if (waveDirector != null)
                waveDirector.StopRun();

            if (scrapDirector != null)
                scrapDirector.enabled = false;
            if (healingDirector != null)
                healingDirector.enabled = false;

            OnRunEnded.Invoke(health);

            if (freezeTimeOnEnd)
                Time.timeScale = 0f;
        }
    }
}
