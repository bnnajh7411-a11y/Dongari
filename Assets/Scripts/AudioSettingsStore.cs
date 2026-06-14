using System;
using UnityEngine;

public enum AudioChannelType
{
    BackgroundMusic,
    SoundEffect
}

public static class AudioSettingsStore
{
    private const string BackgroundMusicVolumePrefKey = "AudioSettings.BackgroundMusicVolume";
    private const string SoundEffectVolumePrefKey = "AudioSettings.SoundEffectVolume";
    private const float DefaultVolume = 1f;

    public static event Action VolumesChanged;

    public static float BackgroundMusicVolume => GetVolume(BackgroundMusicVolumePrefKey);

    public static float SoundEffectVolume => GetVolume(SoundEffectVolumePrefKey);

    public static float GetVolume(AudioChannelType channelType)
    {
        return channelType == AudioChannelType.BackgroundMusic
            ? BackgroundMusicVolume
            : SoundEffectVolume;
    }

    public static void SetBackgroundMusicVolume(float volume)
    {
        SetVolume(BackgroundMusicVolumePrefKey, volume);
    }

    public static void SetSoundEffectVolume(float volume)
    {
        SetVolume(SoundEffectVolumePrefKey, volume);
    }

    private static float GetVolume(string prefKey)
    {
        return PlayerPrefs.GetFloat(prefKey, DefaultVolume);
    }

    private static void SetVolume(string prefKey, float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(prefKey, clampedVolume);
        PlayerPrefs.Save();
        VolumesChanged?.Invoke();
    }
}
