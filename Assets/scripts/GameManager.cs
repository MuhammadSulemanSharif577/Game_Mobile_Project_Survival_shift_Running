using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Reference")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text finalCoinsText;

    [Header("Audio")]
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField] private AudioSource gameOverAudio;

    private float runStartedAt;
    private bool gameOverTriggered;

    private void Awake()
    {
        // Singleton pattern to access the manager easily
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // A newly loaded gameplay scene always begins a fresh measured run.
        Time.timeScale = 1f;
        runStartedAt = Time.time;
        AnalyticsStats.BeginRun();
        ConfigureGameplayAudioAtStartup();
    }

    public void TriggerGameOverSequence()
    {
        if (gameOverTriggered) return;
        gameOverTriggered = true;

        // Snapshot the values before the delayed game-over panel and time freeze occur.
        AnalyticsStats.RecordCompletedRun(Mathf.RoundToInt(Time.time - runStartedAt));
        scoreController.SaveCoinRecord();

        // Use a small delay before showing the menu so the player has time to collapse
        Invoke(nameof(ShowGameOverUI), 1.2f);
    }

    private void ShowGameOverUI()
    {
        // 1. Hide gameplay HUD
        GamePauseController pauseController = FindFirstObjectByType<GamePauseController>();
        if (pauseController != null)
        {
            pauseController.SetHUDActive(false);
        }

        PlayerGunController gunController = FindFirstObjectByType<PlayerGunController>();
        if (gunController != null)
        {
            gunController.HideWeaponUI();
        }

        // 2. Activate the screen panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // 2b. Play the overall Game Over sound design tracking
        if (gameOverAudio == null && gameOverClip != null)
        {
            gameOverAudio = gameObject.GetComponent<AudioSource>();
            if (gameOverAudio == null)
            {
                gameOverAudio = gameObject.AddComponent<AudioSource>();
            }
            gameOverAudio.clip = gameOverClip;
            gameOverAudio.playOnAwake = false;
            gameOverAudio.loop = false;
        }

        if (gameOverAudio != null)
        {
            gameOverAudio.Play();
        }

        // 3. Stop background music (BGM GameObject)
        GameObject bgmObj = GameObject.Find("BGM");
        if (bgmObj != null)
        {
            AudioSource bgmAudio = bgmObj.GetComponent<AudioSource>();
            if (bgmAudio != null)
            {
                bgmAudio.Stop();
            }
        }

        // 4. Display both distance score and coins for this run.
        if (finalScoreText != null)
        {
            finalScoreText.text = $"{scoreController.CurrentScore}m";
        }

        if (finalCoinsText != null)
        {
            finalCoinsText.text = $"{scoreController.CoinCount}";
        }

        // 4. Freeze game time so nothing moves behind the menu
        Time.timeScale = 0f;
    }

    // Call this from a UI Button on your Game Over screen to restart
    public void RestartGame()
    {
        Time.timeScale = 1f; // Always reset time scale!
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static void ConfigureGameplayAudioAtStartup()
    {
        foreach (AudioSource source in FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (source.gameObject.name == "BGM")
            {
                source.playOnAwake = true;
                source.loop = true;
                source.pitch = 1f;
                continue;
            }

            // ButtonSFX can be carrying a navigation click across the scene load.
            if (source.gameObject.name == "ButtonSFX")
                continue;

            source.playOnAwake = false;
            source.loop = false;
            source.Stop();
        }
    }
}
