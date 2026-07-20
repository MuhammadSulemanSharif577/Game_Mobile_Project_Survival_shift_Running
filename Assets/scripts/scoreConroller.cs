using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the score for one run, the persistent best distance, and the coin totals.
/// The existing component name is intentionally kept so current scene references continue to work.
/// </summary>
public class scoreController : MonoBehaviour
{
    public const string HighScoreKey = "HighScoreMeters";
    public const string HighestCoinsKey = "HighestCoinsCollected";

    public static int CoinCount;
    public static int CurrentScore { get; private set; }
    public static int HighestScore => PlayerPrefs.GetInt(HighScoreKey, 0);
    public static int HighestCoins => PlayerPrefs.GetInt(HighestCoinsKey, 0);
    public static int TotalCoins => HighestCoins;

    [Header("HUD")]
    [SerializeField] private GameObject textBox;
    [SerializeField] private TMP_Text scoreTextBox;
    [SerializeField] private GameObject crownIcon;
    [SerializeField] private Sprite crownSprite;

    [Header("New Record Feedback")]
    [SerializeField] private AudioClip highScoreClip;
    [SerializeField, Min(0.1f)] private float crownPopDuration = 2.5f;
    [SerializeField, Min(1f)] private float crownPopScale = 1.2f;
    [SerializeField, Min(1)] private int crownPopCount = 5;

    [Header("Distance Speed Progression")]
    [SerializeField, Min(1f)] private float speedMultiplierAt300m = 1.1f;
    [SerializeField, Min(1f)] private float speedMultiplierAt800m = 1.2f;
    [SerializeField, Min(1f)] private float speedMultiplierAt1100m = 1.3f;

    [SerializeField] private int internalCoinCount;

    private TMP_Text coinTextBox;
    private IRunnerController player;
    private int scoreToBeat;
    private bool recordCelebrated;
    private Vector3 crownBaseScale;
    private AudioSource highScoreAudio;

    private void Start()
    {
        CoinCount = 0;
        CurrentScore = 0;
        scoreToBeat = HighestScore;

        coinTextBox = textBox != null ? textBox.GetComponent<TMP_Text>() : null;
        ResolveScoreText();
        ResolveCrown();

        player = RunnerControllerLocator.Find();
        RefreshHud();
    }

    private void Update()
    {
        internalCoinCount = CoinCount;

        if (player == null)
        {
            player = RunnerControllerLocator.Find();
            if (player == null)
            {
                UpdateCoinText();
                return;
            }
        }

        int distanceInMeters = Mathf.Max(0, Mathf.FloorToInt(player.RunnerTransform.position.z - player.StartingZ));
        if (distanceInMeters != CurrentScore)
        {
            CurrentScore = distanceInMeters;
            UpdateScoreText();
            UpdateHighScore();
        }

        player.SetScoreSpeedMultiplier(GetSpeedMultiplier(CurrentScore));
        UpdateCoinText();
    }

    public static void AddCoin()
    {
        CoinCount++;
    }

    public static void SaveCoinRecord()
    {
        if (CoinCount <= HighestCoins)
            return;

        PlayerPrefs.SetInt(HighestCoinsKey, CoinCount);
        PlayerPrefs.Save();
    }

    private void UpdateHighScore()
    {
        if (CurrentScore > HighestScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, CurrentScore);
            PlayerPrefs.Save();
        }

        // Compare against the score that existed at the start of this run. This triggers only once.
        if (!recordCelebrated && CurrentScore > scoreToBeat)
        {
            recordCelebrated = true;
            ShowNewRecordFeedback();
        }
    }

    private float GetSpeedMultiplier(int score)
    {
        if (score >= 1100) return speedMultiplierAt1100m;
        if (score >= 800) return speedMultiplierAt800m;
        if (score >= 300) return speedMultiplierAt300m;
        return 1f;
    }

    private void ResolveScoreText()
    {
        if (scoreTextBox != null && scoreTextBox != coinTextBox)
            return;

        // Use the authored HUD text in each gameplay scene. Do not create or
        // reposition UI at runtime, so the Canvas layout remains exactly as designed.
        foreach (TMP_Text candidate in FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == coinTextBox)
                continue;

            if (candidate.name == "desertScoreMade" || candidate.name == "ScoreText")
            {
                scoreTextBox = candidate;
                return;
            }
        }
    }

    private void ResolveCrown()
    {
        if (crownIcon == null && crownSprite != null && scoreTextBox != null)
        {
            GameObject crownObject = new GameObject("CrownIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            crownObject.transform.SetParent(scoreTextBox.transform.parent, false);

            Image crownImage = crownObject.GetComponent<Image>();
            crownImage.sprite = crownSprite;
            crownImage.preserveAspect = true;
            crownImage.raycastTarget = false;

            RectTransform scoreRect = scoreTextBox.rectTransform;
            RectTransform crownRect = crownObject.GetComponent<RectTransform>();
            crownRect.anchorMin = scoreRect.anchorMin;
            crownRect.anchorMax = scoreRect.anchorMax;
            crownRect.pivot = scoreRect.pivot;
            crownRect.anchoredPosition = scoreRect.anchoredPosition + new Vector2(-125f, 0f);
            crownRect.sizeDelta = new Vector2(72f, 72f);
            crownIcon = crownObject;
        }

        if (crownIcon == null)
        {
            return;
        }

        crownBaseScale = crownIcon.transform.localScale;
        // It stays hidden until this run beats the saved score.
        crownIcon.SetActive(false);
    }

    private void ShowNewRecordFeedback()
    {
        if (crownIcon != null)
        {
            crownIcon.SetActive(true);
            StartCoroutine(PopCrown());
        }

        if (highScoreClip != null)
        {
            if (highScoreAudio == null)
            {
                highScoreAudio = GetComponent<AudioSource>();
                if (highScoreAudio == null)
                {
                    highScoreAudio = gameObject.AddComponent<AudioSource>();
                }
                highScoreAudio.playOnAwake = false;
            }

            highScoreAudio.PlayOneShot(highScoreClip);
        }
    }

    private IEnumerator PopCrown()
    {
        float elapsed = 0f;
        while (elapsed < crownPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = (Mathf.Sin(elapsed * crownPopCount * Mathf.PI * 2f / crownPopDuration) + 1f) * 0.5f;
            crownIcon.transform.localScale = crownBaseScale * Mathf.Lerp(1f, crownPopScale, pulse);
            yield return null;
        }

        if (crownIcon != null)
        {
            crownIcon.transform.localScale = crownBaseScale;
        }
    }

    private void RefreshHud()
    {
        UpdateScoreText();
        UpdateCoinText();
    }

    private void UpdateScoreText()
    {
        if (scoreTextBox != null)
        {
            scoreTextBox.text = $"{CurrentScore}m";
        }
    }

    private void UpdateCoinText()
    {
        if (coinTextBox != null)
        {
            coinTextBox.text = CoinCount.ToString();
        }
    }
}
