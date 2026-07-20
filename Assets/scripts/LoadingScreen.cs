using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class LoadingScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform loadingBar;
    [SerializeField] private RectTransform loadingDot;
    [SerializeField] private TMP_Text percentageText;

    [Header("Loading Settings")]
    [SerializeField] private float loadingTime = 5f;

    [Tooltip("Distance between the centers of two dots.")]
    [SerializeField] private float dotSpacing = 10f;

    private void Start()
    {
        StartCoroutine(FillLoadingBar());
    }

    IEnumerator FillLoadingBar()
    {
        // Remove previously created dots
        for (int i = loadingBar.childCount - 1; i >= 0; i--)
        {
            if (loadingBar.GetChild(i) != loadingDot)
                Destroy(loadingBar.GetChild(i).gameObject);
        }

        // Keep template active while cloning
        loadingDot.gameObject.SetActive(true);

        float barWidth = loadingBar.rect.width;

        int totalDots = Mathf.CeilToInt(barWidth / dotSpacing);

        float startX = -barWidth / 2f + dotSpacing / 2f;

        float delay = loadingTime / totalDots;

        if (percentageText != null)
            percentageText.text = "0%";

        // Hide original template
        loadingDot.anchoredPosition = new Vector2(-5000, loadingDot.anchoredPosition.y);

        for (int i = 0; i < totalDots; i++)
        {
            RectTransform dot = Instantiate(loadingDot, loadingBar);

            dot.gameObject.SetActive(true);

            dot.localScale = loadingDot.localScale;
            dot.localRotation = Quaternion.identity;

            dot.anchorMin = loadingDot.anchorMin;
            dot.anchorMax = loadingDot.anchorMax;
            dot.pivot = loadingDot.pivot;

            dot.anchoredPosition = new Vector2(
                startX + (i * dotSpacing),
                0f
            );

            float progress = (float)(i + 1) / totalDots;

            if (percentageText != null)
                percentageText.text = Mathf.RoundToInt(progress * 100f) + "%";

            yield return new WaitForSeconds(delay);
        }

        if (percentageText != null)
            percentageText.text = "100%";

        yield return new WaitForSeconds(0.5f);

        SceneManager.LoadScene("Menu");
    }
}