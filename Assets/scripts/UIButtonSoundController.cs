using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Routes every Unity UI Button in the scene through one editable AudioSource.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class UIButtonSoundController : MonoBehaviour
{
    private static UIButtonSoundController activeController;
    private AudioSource buttonAudio;

    private void Awake()
    {
        buttonAudio = GetComponent<AudioSource>();
        buttonAudio.playOnAwake = false;
        buttonAudio.loop = false;
        buttonAudio.spatialBlend = 0f;

        // Keep the sound alive when a navigation button changes scenes on the
        // same frame that it is clicked, otherwise the click can be cut short.
        if (activeController != null && activeController != this)
        {
            if (buttonAudio.clip != null)
                activeController.buttonAudio.clip = buttonAudio.clip;

            Destroy(gameObject);
            return;
        }

        activeController = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookSceneButtons();
    }

    private void OnDestroy()
    {
        if (activeController != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        activeController = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookSceneButtons();
    }

    private void HookSceneButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            button.onClick.RemoveListener(PlayButtonSound);
            button.onClick.AddListener(PlayButtonSound);
        }
    }

    public void PlayButtonSound()
    {
        if (buttonAudio == null || buttonAudio.clip == null || buttonAudio.mute)
            return;

        buttonAudio.PlayOneShot(buttonAudio.clip);
    }
}
