using MagnetPanic.Combat.Upgrades;
using UnityEngine;

namespace MagnetPanic.Combat.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerSfx : MonoBehaviour
    {
        [Header("Pull")]
        [SerializeField] AudioClip pullClip;
        [SerializeField] [Range(0f, 1f)] float pullVolume = 1f;

        [Header("Repel")]
        [SerializeField] AudioClip repelClip;
        [SerializeField] [Range(0f, 1f)] float repelVolume = 1f;

        [Header("Overload")]
        [SerializeField] AudioClip overloadClip;
        [SerializeField] [Range(0f, 1f)] float overloadVolume = 1f;

        [Header("Level Up (upgrade picked)")]
        [SerializeField] AudioClip levelUpClip;
        [SerializeField] [Range(0f, 1f)] float levelUpVolume = 1f;

        AudioSource source;

        void Awake() => source = GetComponent<AudioSource>();

        void Start()
        {
            MagnetismController mag = GetComponent<MagnetismController>();
            if (mag != null)
            {
                mag.OnPullStarted.AddListener(PlayPull);
                mag.OnRepelFired.AddListener(PlayRepel);
            }

            OverloadController ol = GetComponent<OverloadController>();
            if (ol != null)
                ol.OnOverloadExploded.AddListener(PlayOverload);

            UpgradeSystem up = GetComponent<UpgradeSystem>();
            if (up != null)
                up.OnUpgradeApplied.AddListener(PlayLevelUp);
        }

        void OnDestroy()
        {
            MagnetismController mag = GetComponent<MagnetismController>();
            if (mag != null)
            {
                mag.OnPullStarted.RemoveListener(PlayPull);
                mag.OnRepelFired.RemoveListener(PlayRepel);
            }

            OverloadController ol = GetComponent<OverloadController>();
            if (ol != null)
                ol.OnOverloadExploded.RemoveListener(PlayOverload);

            UpgradeSystem up = GetComponent<UpgradeSystem>();
            if (up != null)
                up.OnUpgradeApplied.RemoveListener(PlayLevelUp);
        }

        void PlayPull() { if (pullClip != null) source.PlayOneShot(pullClip, pullVolume); }
        void PlayRepel(bool _) { if (repelClip != null) source.PlayOneShot(repelClip, repelVolume); }
        void PlayOverload(Vector3 _, float __, int ___) { if (overloadClip != null) source.PlayOneShot(overloadClip, overloadVolume); }
        void PlayLevelUp(UpgradeData _, int __) { if (levelUpClip != null) source.PlayOneShot(levelUpClip, levelUpVolume); }
    }
}
