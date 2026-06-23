using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SceneAudioSource : MonoBehaviour
{
    [SerializeField] private AudioChannelType channelType = AudioChannelType.BackgroundMusic;
    [SerializeField, Min(0f)] private float baseVolume = 1f;

    private AudioSource cachedAudioSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAutoRegistration()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAutoRegistration()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RegisterSceneAudioSources(SceneManager.GetActiveScene());
    }

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

    internal void SetConfiguredValues(AudioChannelType configuredChannelType, float configuredBaseVolume)
    {
        channelType = configuredChannelType;
        baseVolume = Mathf.Clamp01(configuredBaseVolume);
        ApplyConfiguredVolume();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RegisterSceneAudioSources(scene);
    }

    private static void RegisterSceneAudioSources(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            AudioSource[] audioSources = rootObjects[i].GetComponentsInChildren<AudioSource>(true);
            for (int j = 0; j < audioSources.Length; j++)
            {
                AutoConfigureAudioSource(audioSources[j]);
            }
        }
    }

    private static void AutoConfigureAudioSource(AudioSource audioSource)
    {
        if (audioSource == null
            || !audioSource.playOnAwake
            || !audioSource.loop
            || audioSource.GetComponent<SceneAudioSource>() != null)
        {
            return;
        }

        float sourceVolume = audioSource.volume;
        SceneAudioSource sceneAudioSource = audioSource.gameObject.AddComponent<SceneAudioSource>();
        sceneAudioSource.SetConfiguredValues(AudioChannelType.BackgroundMusic, sourceVolume);
    }
}
