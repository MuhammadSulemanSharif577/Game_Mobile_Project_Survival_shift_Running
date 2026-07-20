using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
/// <summary>Persists and applies the player's audio, UI-placement, and graphics preferences.</summary>
public class GameSettings : MonoBehaviour
{
    private const string MusicEnabledKey = "SettingsMusicEnabled";
    private const string GunPositionKey = "SettingsGunPosition";
    private const string QualityKey = "SettingsQuality";

    [SerializeField] private Toggle audioToggle;
    [SerializeField] private TMP_Dropdown gunPositionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    private void Awake()
    {
        ApplySavedSettings();
    }

    private void Start()
    {
        ConfigureSettingsScreen();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSceneApplier()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySavedSettings();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySavedSettings();
    }

    public static void ApplySavedSettings()
    {
        ApplyQuality(PlayerPrefs.GetInt(QualityKey, 1));
        ApplyMusic(PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1);
        ApplyGunPosition(PlayerPrefs.GetInt(GunPositionKey, 2));
    }

    private void ConfigureSettingsScreen()
    {
        ResolveAudioToggle();

        if (audioToggle != null)
        {
            audioToggle.SetIsOnWithoutNotify(IsMusicEnabled);
            audioToggle.onValueChanged.RemoveListener(SetMusicEnabled);
            audioToggle.onValueChanged.AddListener(SetMusicEnabled);
        }

        ResolveDropdowns();
        ConfigureDropdown(gunPositionDropdown, new[] { "Top Right", "Middle Right", "Bottom Right" },
            PlayerPrefs.GetInt(GunPositionKey, 2), SetGunPosition);

        ConfigureDropdown(qualityDropdown, new[] { "Low", "Medium", "High" },
            PlayerPrefs.GetInt(QualityKey, 1), SetQuality);
    }

    private void ResolveAudioToggle()
    {
        if (audioToggle != null) return;

        foreach (Toggle toggle in GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.name.ToLowerInvariant().Contains("audio"))
            {
                audioToggle = toggle;
                return;
            }
        }
    }

    private void ResolveDropdowns()
    {
        TMP_Dropdown[] dropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            string name = dropdown.name.ToLowerInvariant();
            if (gunPositionDropdown == null && (name.Contains("gun") || name.Contains("position")))
            {
                gunPositionDropdown = dropdown;
            }
            else if (qualityDropdown == null && (name.Contains("quality") || name.Contains("graphic")))
            {
                qualityDropdown = dropdown;
            }
        }

        // If the authored dropdown objects use generic names, use their order in the canvas.
        if (gunPositionDropdown == null && dropdowns.Length > 0) gunPositionDropdown = dropdowns[0];
        if (qualityDropdown == null)
        {
            foreach (TMP_Dropdown dropdown in dropdowns)
            {
                if (dropdown != gunPositionDropdown)
                {
                    qualityDropdown = dropdown;
                    break;
                }
            }
        }
    }

    private static bool IsMusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;

    private void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMusic(enabled);
    }

    private void SetGunPosition(int position)
    {
        PlayerPrefs.SetInt(GunPositionKey, Mathf.Clamp(position, 0, 2));
        PlayerPrefs.Save();
        ApplyGunPosition(position);
    }
    private void SetQuality(int quality)
    {
        int clampedQuality = Mathf.Clamp(quality, 0, 2);

        PlayerPrefs.SetInt(QualityKey, clampedQuality);
        PlayerPrefs.Save();

        ApplyQuality(clampedQuality);
    }

    private static void ApplyMusic(bool enabled)
    {
        ApplyMusicSource(GameObject.Find("MenuBGM"), enabled);
        ApplyMusicSource(GameObject.Find("BGM"), enabled);
    }

    private static void ApplyMusicSource(GameObject musicObject, bool enabled)
    {
        if (musicObject == null) return;

        AudioSource bgm = musicObject.GetComponent<AudioSource>();
        if (bgm == null) return;

        // BGM must always play at the clip's authored speed. This also repairs
        // scenes that were previously saved with a reduced AudioSource pitch.
        bgm.pitch = 1f;
        bgm.mute = !enabled;
        if (enabled && !bgm.isPlaying && bgm.clip != null) bgm.Play();
    }

    private static void ApplyGunPosition(int position)
    {
        foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name != "shooting" && rect.name != "CyberShooting") continue;

            float verticalAnchor = position == 0 ? 1f : position == 1 ? 0.5f : 0f;
            rect.anchorMin = new Vector2(1f, verticalAnchor);
            rect.anchorMax = new Vector2(1f, verticalAnchor);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-36f, position == 0 ? -110f : position == 1 ? 0f : 110f);
        }
    }

    private static void ApplyQuality(int preset)
    {
        preset = Mathf.Clamp(preset, 0, 2);

        if (QualitySettings.names.Length <= preset)
        {
            Debug.LogWarning(
                $"[GameSettings] Quality level {preset} does not exist."
            );
            return;
        }

        QualitySettings.SetQualityLevel(preset, true);

        switch (preset)
        {
            case 0:
                ApplyLowGraphics();
                break;

            case 1:
                ApplyMediumGraphics();
                break;

            case 2:
                ApplyHighGraphics();
                break;
        }

        Debug.Log(
            $"Graphics quality changed to: {QualitySettings.names[preset]}, " +
            $"Target FPS: {Application.targetFrameRate}"
        );
    }




    private static void ApplyLowGraphics()
    {
        Application.targetFrameRate = 30;

        QualitySettings.vSyncCount = 0;
        QualitySettings.lodBias = 0.6f;
        QualitySettings.maximumLODLevel = 1;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
    }

    private static void ApplyMediumGraphics()
    {
        Application.targetFrameRate = 45;

        QualitySettings.vSyncCount = 0;
        QualitySettings.lodBias = 1.1f;
        QualitySettings.maximumLODLevel = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
    }

    private static void ApplyHighGraphics()
    {
        Application.targetFrameRate = 60;

        QualitySettings.vSyncCount = 0;
        QualitySettings.lodBias = 2f;
        QualitySettings.maximumLODLevel = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.softParticles = true;
    }






    private void ConfigureDropdown(TMP_Dropdown dropdown, string[] options, int value, UnityEngine.Events.UnityAction<int> listener)
    {
        if (dropdown == null)
        {
            Debug.LogWarning("[GameSettings] A Settings dropdown is missing. Save the authored dropdown objects in Settings.unity, then assign them to GameSettings if needed.");
            return;
        }
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Length - 1));
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(listener);
        dropdown.RefreshShownValue();
    }

}
