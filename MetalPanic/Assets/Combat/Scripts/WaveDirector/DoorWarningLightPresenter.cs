using System.Collections;
using UnityEngine;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Minimal presentation hook for the vertical slice: blinks the light assigned
    /// on an ArenaDoor whenever WaveDirector announces that door.
    /// </summary>
    public sealed class DoorWarningLightPresenter : MonoBehaviour
    {
        [SerializeField] WaveDirector waveDirector;
        [SerializeField] ArenaSystem arena;
        [SerializeField, Min(0.05f)] float fallbackDuration = 0.75f;
        [SerializeField, Min(0.03f)] float blinkInterval = 0.12f;
        [SerializeField, Min(0f)] float warningIntensity = 4f;

        Coroutine pulseRoutine;

        void OnEnable()
        {
            Resolve();
            if (waveDirector != null)
                waveDirector.OnDoorWarning.AddListener(HandleDoorWarning);
        }

        void OnDisable()
        {
            if (waveDirector != null)
                waveDirector.OnDoorWarning.RemoveListener(HandleDoorWarning);
        }

        public void SetReferences(WaveDirector director, ArenaSystem arenaRef)
        {
            if (waveDirector != null)
                waveDirector.OnDoorWarning.RemoveListener(HandleDoorWarning);

            waveDirector = director;
            arena = arenaRef;

            if (isActiveAndEnabled && waveDirector != null)
                waveDirector.OnDoorWarning.AddListener(HandleDoorWarning);
        }

        void HandleDoorWarning(ArenaDoorId doorId)
        {
            Resolve();
            if (arena == null || !arena.TryGetDoor(doorId, out ArenaDoor door) || door.WarningLight == null)
                return;

            if (pulseRoutine != null)
                StopCoroutine(pulseRoutine);

            float duration = waveDirector != null && waveDirector.Config != null
                ? waveDirector.Config.doorWarningTime
                : fallbackDuration;

            pulseRoutine = StartCoroutine(Pulse(door.WarningLight, Mathf.Max(0.05f, duration)));
        }

        IEnumerator Pulse(Light light, float duration)
        {
            float originalIntensity = light.intensity;
            bool originalEnabled = light.enabled;
            float elapsed = 0f;

            while (elapsed < duration && light != null)
            {
                light.enabled = !light.enabled;
                light.intensity = warningIntensity;
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }

            if (light != null)
            {
                light.intensity = originalIntensity;
                light.enabled = originalEnabled;
            }

            pulseRoutine = null;
        }

        void Resolve()
        {
            if (waveDirector == null)
                waveDirector = FindFirstObjectByType<WaveDirector>();

            if (arena == null)
                arena = waveDirector != null ? waveDirector.Arena : FindFirstObjectByType<ArenaSystem>();
        }
    }
}
