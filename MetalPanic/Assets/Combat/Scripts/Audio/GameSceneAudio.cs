using UnityEngine;

namespace MagnetPanic.Combat.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameSceneAudio : MonoBehaviour
    {
        [Header("Ambient Music (looping)")]
        [SerializeField] AudioClip ambientMusic;
        [SerializeField] [Range(0f, 1f)] float musicVolume = 0.5f;

        [Header("SFX")]
        [SerializeField] AudioClip enemySpawnClip;
        [SerializeField] [Range(0f, 1f)] float spawnVolume = 0.8f;

        [SerializeField] AudioClip waveCompleteClip;
        [SerializeField] [Range(0f, 1f)] float waveCompleteVolume = 1f;

        AudioSource musicSource;

        void Awake()
        {
            musicSource = GetComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            if (ambientMusic != null)
                musicSource.clip = ambientMusic;
        }

        void Start()
        {
            if (musicSource.clip != null && !musicSource.isPlaying)
                musicSource.Play();

            WaveDirector director = FindFirstObjectByType<WaveDirector>();
            if (director != null)
            {
                director.OnEnemySpawned.AddListener(OnEnemySpawned);
                director.OnWaveCleared.AddListener(OnWaveCleared);
            }
        }

        void OnDestroy()
        {
            WaveDirector director = FindFirstObjectByType<WaveDirector>();
            if (director != null)
            {
                director.OnEnemySpawned.RemoveListener(OnEnemySpawned);
                director.OnWaveCleared.RemoveListener(OnWaveCleared);
            }
        }

        void OnEnemySpawned(ArkhamEnemy _, ArenaDoorId __)
        {
            if (enemySpawnClip != null)
                AudioSource.PlayClipAtPoint(enemySpawnClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, spawnVolume);
        }

        void OnWaveCleared(int _)
        {
            if (waveCompleteClip != null)
                AudioSource.PlayClipAtPoint(waveCompleteClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, waveCompleteVolume);
        }
    }
}
