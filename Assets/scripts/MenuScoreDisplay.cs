using TMPro;
using UnityEngine;

/// <summary>Loads the persistent best distance and coin bank into the existing menu score container.</summary>
public class MenuScoreDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private GameObject crownIcon;

    private void Start()
    {
        ResolveReferences();
        RefreshDisplay();
    }

    public void ResetSavedScoreAndCoins()
    {
        PlayerPrefs.SetInt(scoreController.HighScoreKey, 0);
        PlayerPrefs.SetInt(scoreController.HighestCoinsKey, 0);
        PlayerPrefs.Save();

        // Keep the static run coin value consistent if this is invoked while
        // testing with gameplay objects still present in the Editor.
        scoreController.CoinCount = 0;
        RefreshDisplay();
    }

    private void ResolveReferences()
    {
        if (scoreText == null)
        {
            GameObject scoreObject = GameObject.Find("ScoreText");
            scoreText = scoreObject != null ? scoreObject.GetComponent<TMP_Text>() : null;
        }

        if (coinText == null)
        {
            GameObject coinObject = GameObject.Find("CoinCollectedText");
            coinText = coinObject != null ? coinObject.GetComponent<TMP_Text>() : null;
        }

        if (crownIcon == null)
        {
            crownIcon = GameObject.Find("CrownIcon");
        }
    }

    private void RefreshDisplay()
    {
        int bestScore = scoreController.HighestScore;
        if (scoreText != null)
        {
            scoreText.text = $"{bestScore}m";
        }

        if (coinText != null)
        {
            coinText.text = $"{scoreController.HighestCoins}";
        }

        // The menu crown appears only after a real record exists; a new game still starts with it hidden.
        if (crownIcon != null)
        {
            crownIcon.SetActive(bestScore > 0);
        }
    }
}
