using UnityEngine;

namespace MagnetPanic.Combat.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip[] clips;
        [SerializeField] [Range(0f, 1f)] float volume = 1f;

        AudioSource source;

        void Awake() => source = GetComponent<AudioSource>();

        // Wire this to any UnityEvent in the Inspector.
        public void Play()
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) source.PlayOneShot(clip, volume);
        }

        // Overloads that match common UnityEvent signatures so they appear in Inspector.
        public void Play(Object _) => Play();
        public void Play(int _) => Play();
        public void Play(bool _) => Play();
    }
}
