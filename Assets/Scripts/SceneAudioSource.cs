using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SceneAudioSource : MonoBehaviour
{
    [SerializeField] private AudioChannelType channelType = AudioChannelType.BackgroundMusic;
    [SerializeField, Min(0f)] private float baseVolume = 1f;

    private AudioSource cachedAudioSource;

    private void Awake()
    {
        cachedAudioSource = GetComponent<AudioSource>();
        ApplyConfiguredVolume();
    }

    private void OnEnable()
    {
        AudioSettingsStore.VolumesChanged += HandleVolumesChanged;
        ApplyConfiguredVolume();
    }

    private void OnDisable()
    {
        AudioSettingsStore.VolumesChanged -= HandleVolumesChanged;
    }

    private void HandleVolumesChanged()
    {
        ApplyConfiguredVolume();
    }

    private void ApplyConfiguredVolume()
    {
        if (cachedAudioSource == null)
        {
            cachedAudioSource = GetComponent<AudioSource>();
        }

        if (cachedAudioSource == null)
        {
            return;
        }

        cachedAudioSource.volume = Mathf.Clamp01(baseVolume) * AudioSettingsStore.GetVolume(channelType);
    }
}
