using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Keeps menu music continuous between menu scenes and stops it elsewhere.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class MenuBackgroundMusic : MonoBehaviour
{
    private const string MusicEnabledKey = "SettingsMusicEnabled";

    private static readonly HashSet<string> MenuSceneNames = new HashSet<string>
    {
        "Menu",
        "ChoseEnviirnment",
        "Settings",
        "Analytics"
    };

    private static MenuBackgroundMusic activeMusic;
    private AudioSource musicSource;

    private void Awake()
    {
        musicSource = GetComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.pitch = 1f;

        if (!MenuSceneNames.Contains(gameObject.scene.name))
        {
            musicSource.Stop();
            Destroy(gameObject);
            return;
        }

        if (activeMusic != null && activeMusic != this)
        {
            // This scene owns a duplicate source. Keep the already-playing one
            // so its playback position is not reset during menu navigation.
            if (activeMusic.musicSource.clip == null && musicSource.clip != null)
            {
                activeMusic.musicSource.clip = musicSource.clip;
                activeMusic.PlayIfEnabled();
            }

            musicSource.Stop();
            Destroy(gameObject);
            return;
        }

        activeMusic = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayIfEnabled();
    }

    private void OnDestroy()
    {
        if (activeMusic != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        activeMusic = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (MenuSceneNames.Contains(scene.name))
        {
            PlayIfEnabled();
            return;
        }

        musicSource.Stop();
        Destroy(gameObject);
    }

    private void PlayIfEnabled()
    {
        bool enabled = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
        musicSource.mute = !enabled;

        if (enabled && musicSource.clip != null && !musicSource.isPlaying)
            musicSource.Play();
    }
}
