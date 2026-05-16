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

        [Header("Hit (melee connects)")]
        [SerializeField] AudioClip hitClip;
        [SerializeField] [Range(0f, 1f)] float hitVolume = 1f;

        [Header("Level Up (upgrade picked)")]
        [SerializeField] AudioClip levelUpClip;
        [SerializeField] [Range(0f, 1f)] float levelUpVolume = 1f;

        AudioSource sfxSource;
        AudioSource pullSource;

        void Awake()
        {
            sfxSource = GetComponent<AudioSource>();
            pullSource = gameObject.AddComponent<AudioSource>();
            pullSource.playOnAwake = false;
            pullSource.loop = false;
        }

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

            ArkhamCombatController combat = GetComponent<ArkhamCombatController>();
            if (combat != null)
                combat.OnHit.AddListener(PlayHit);
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

            ArkhamCombatController combat = GetComponent<ArkhamCombatController>();
            if (combat != null)
                combat.OnHit.RemoveListener(PlayHit);
        }

        void PlayPull()
        {
            if (pullClip == null) return;
            if (!pullSource.isPlaying)
            {
                pullSource.clip = pullClip;
                pullSource.volume = pullVolume;
                pullSource.Play();
            }
        }

        void PlayRepel(bool _)
        {
            pullSource.Stop();
            if (repelClip != null) sfxSource.PlayOneShot(repelClip, repelVolume);
        }

        void PlayHit(ArkhamEnemy _) { if (hitClip != null) sfxSource.PlayOneShot(hitClip, hitVolume); }
        void PlayOverload(Vector3 _, float __, int ___) { if (overloadClip != null) sfxSource.PlayOneShot(overloadClip, overloadVolume); }
        void PlayLevelUp(UpgradeData _, int __) { if (levelUpClip != null) sfxSource.PlayOneShot(levelUpClip, levelUpVolume); }
    }
}
