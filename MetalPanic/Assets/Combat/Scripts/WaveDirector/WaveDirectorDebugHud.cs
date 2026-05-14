using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Minimal IMGUI overlay for tuning waves: act/wave/alive/scrap/healing cooldown.
    /// Toggle with F3 in play mode. Strip from production builds via the toggle flag.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveDirectorDebugHud : MonoBehaviour
    {
        [SerializeField] bool showOnStart = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] Key toggleKey = Key.F3;
#else
        [SerializeField] KeyCode toggleKey = KeyCode.F3;
#endif
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] ScrapDirector scrapDirector;
        [SerializeField] HealingDirector healingDirector;
        [SerializeField] ArkhamEnemyManager enemyManager;

        GUIStyle style;
        bool visible;

        void Start()
        {
            visible = showOnStart;
            Resolve();
        }

        void Update()
        {
            if (WasTogglePressed())
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13,
                    padding = new RectOffset(8, 8, 6, 6)
                };
                style.normal.textColor = Color.white;
            }

            Resolve();

            string act = waveDirector != null ? waveDirector.CurrentActIndex.ToString() : "-";
            string wave = waveDirector != null ? waveDirector.CurrentWaveIndex.ToString() : "-";
            string state = waveDirector != null ? waveDirector.State.ToString() : "-";
            string clock = waveDirector != null ? waveDirector.RunClock.ToString("0.0") + "s" : "-";
            int alive = enemyManager != null ? enemyManager.AliveEnemyCount() : 0;
            int scrap = scrapDirector != null ? scrapDirector.ActiveScrap.Count : 0;
            int heals = healingDirector != null ? healingDirector.ActivePickups.Count : 0;

            string text =
                $"[{ToggleKeyLabel()}] WaveDirector Debug\n" +
                $"State: {state}\n" +
                $"Clock: {clock}\n" +
                $"Act:   {act}\n" +
                $"Wave:  {wave}\n" +
                $"Alive: {alive}\n" +
                $"Scrap: {scrap}\n" +
                $"Heals: {heals}";

            GUI.Box(new Rect(12, 12, 220, 150), text, style);
        }

        bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[toggleKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(toggleKey);
#endif
        }

        string ToggleKeyLabel()
        {
#if ENABLE_INPUT_SYSTEM
            return toggleKey.ToString();
#else
            return toggleKey.ToString();
#endif
        }

        void Resolve()
        {
            if (waveDirector == null)
                waveDirector = FindFirstObjectByType<WaveDirector>();
            if (scrapDirector == null)
                scrapDirector = FindFirstObjectByType<ScrapDirector>();
            if (healingDirector == null)
                healingDirector = FindFirstObjectByType<HealingDirector>();
            if (enemyManager == null)
                enemyManager = FindFirstObjectByType<ArkhamEnemyManager>();
        }
    }
}
