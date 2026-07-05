using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Reference")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText;

    [Header("Audio")]
    [SerializeField] private AudioSource gameOverAudio;

    private void Awake()
    {
        // Singleton pattern to access the manager easily
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerGameOverSequence()
    {
        // Use a small delay before showing the menu so the player has time to collapse
        Invoke(nameof(ShowGameOverUI), 1.2f);
    }

    private void ShowGameOverUI()
    {
        // 1. Activate the screen panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // 2. Play the overall Game Over sound design tracking
        if (gameOverAudio != null)
        {
            gameOverAudio.Play();
        }

        // 3. Fetch and display the score 
        // Note: Capitalization matches your ScoreController reference from earlier
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Final Score: {scoreController.CoinCount}";
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
}