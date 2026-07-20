using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Animates the saved player statistics on the Analytics scene.</summary>
public class AnalyticsDisplay : MonoBehaviour
{
    [Header("Existing Analytics labels")]
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private TMP_Text timeTakenText;
    [SerializeField] private TMP_Text obstaclesDestroyedText;

    [Header("Count-up feedback")]
    [SerializeField, Min(0.1f)] private float countDuration = 1.25f;
    [SerializeField] private AudioSource countUpAudio;

    private void Awake()
    {
        ResolveLabels();
        countUpAudio = GetComponent<AudioSource>();
        if (countUpAudio == null)
        {
            countUpAudio = gameObject.AddComponent<AudioSource>();
        }

        countUpAudio.playOnAwake = false;
        countUpAudio.spatialBlend = 0f;
    }

    private void Start()
    {
        StartCoroutine(AnimateStatistics());
    }

    private IEnumerator AnimateStatistics()
    {
        int highScore = scoreController.HighestScore;
        int elapsedSeconds = AnalyticsStats.LastRunTimeSeconds;
        int destroyed = AnalyticsStats.LastRunObstaclesDestroyed;

        SetValues(0, 0, 0);

        if (countUpAudio != null && countUpAudio.clip != null &&
            (highScore > 0 || elapsedSeconds > 0 || destroyed > 0))
        {
            countUpAudio.Play();
        }

        float elapsed = 0f;
        while (elapsed < countDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / countDuration));
            SetValues(
                Mathf.RoundToInt(highScore * progress),
                Mathf.RoundToInt(elapsedSeconds * progress),
                Mathf.RoundToInt(destroyed * progress));
            yield return null;
        }

        SetValues(highScore, elapsedSeconds, destroyed);
    }

    private void SetValues(int highScore, int elapsedSeconds, int destroyed)
    {
        if (highScoreText != null) highScoreText.text = $"{highScore} m";
        if (timeTakenText != null) timeTakenText.text = FormatTime(elapsedSeconds);
        if (obstaclesDestroyedText != null) obstaclesDestroyedText.text = destroyed.ToString();
    }

    private void ResolveLabels()
    {
        if (highScoreText == null) highScoreText = FindText("HighScoreText");
        if (timeTakenText == null) timeTakenText = FindText("TimeTakenText");
        if (obstaclesDestroyedText == null) obstaclesDestroyedText = FindText("ObstacleDestroyedNumberText");
    }

    private TMP_Text FindText(string objectName)
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == objectName) return text;
        }

        return null;
    }

    private static string FormatTime(int totalSeconds)
    {
        return string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
    }
}
