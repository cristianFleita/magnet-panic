using UnityEngine;

namespace MagnetPanic.Combat.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip music;
        [SerializeField] [Range(0f, 1f)] float volume = 0.6f;

        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.volume = volume;
            // Set clip early so playOnAwake fires the right track.
            if (music != null)
                source.clip = music;
        }

        void Start()
        {
            if (source.clip == null) return;
            if (!source.isPlaying)
                source.Play();
        }
    }
}
