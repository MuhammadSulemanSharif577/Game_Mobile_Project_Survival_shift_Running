using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GamePauseController : MonoBehaviour
{
    private GameObject pausePanel;
    private Button pauseButton;
    private Button resumeButton;
    private Button quitButton;

    private bool isPaused = false;
    private GameObject countdownTextObj;
    private TextMeshProUGUI countdownText;

    void Start()
    {
        // Resolve references dynamically under Canvas (transform is Canvas)
        Transform panelTrans = transform.Find("GamePausePanel");
        if (panelTrans != null)
        {
            pausePanel = panelTrans.gameObject;
            
            Transform resumeTrans = panelTrans.Find("ResumeButton");
            if (resumeTrans != null) resumeButton = resumeTrans.GetComponent<Button>();

            Transform quitTrans = panelTrans.Find("QuitToMenuButton");
            if (quitTrans != null) quitButton = quitTrans.GetComponent<Button>();
        }

        Transform pauseBtnTrans = transform.Find("Pause");
        if (pauseBtnTrans != null)
        {
            pauseButton = pauseBtnTrans.GetComponent<Button>();
        }

        // Set up click listeners
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(PauseGame);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitToMenu);
        }

        // Hide pause menu initially
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;

        // Verify player is not already dead (don't pause during game over)
        IRunnerController player = RunnerControllerLocator.Find();
        if (player != null && player.IsDead) return;

        isPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null) pausePanel.SetActive(true);

        // Hide all gameplay HUD elements (including Pause button)
        SetHUDActive(false);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        StartCoroutine(ResumeSequence());
    }

    private IEnumerator ResumeSequence()
    {
        // 1. Hide the Pause Panel immediately
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        // 2. Create and configure the countdown text dynamically
        CreateCountdownUI();

        // 3. Count down 3, 2, 1 in unscaled realtime
        for (int i = 3; i >= 1; i--)
        {
            countdownText.text = i.ToString();
            
            // Pulse animation: start large and scale down to normal
            float timer = 0f;
            while (timer < 1.0f)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / 1.0f);
                
                // Scale from 2.5f to 1.0f with an overshoot bounce
                float scale = Mathf.Lerp(2.5f, 1.0f, progress);
                countdownText.transform.localScale = new Vector3(scale, scale, 1f);
                
                yield return null;
            }
        }

        // 4. Destroy the countdown UI
        if (countdownTextObj != null)
        {
            Destroy(countdownTextObj);
        }

        // 5. Restore time and HUD (including Pause button)
        isPaused = false;

        PlayerMove desertPlayer = FindAnyObjectByType<PlayerMove>();
        if (desertPlayer != null)
        {
            desertPlayer.RestoreAnimatorAfterPause();
        }

        CyberPlayerMove cyberPlayer = FindAnyObjectByType<CyberPlayerMove>();
        if (cyberPlayer != null)
        {
            cyberPlayer.RestoreAnimatorAfterPause();
        }

        Time.timeScale = 1f;
        SetHUDActive(true);
    }

    private void CreateCountdownUI()
    {
        countdownTextObj = new GameObject("CountdownText");
        countdownTextObj.transform.SetParent(transform, false);

        RectTransform rt = countdownTextObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(400f, 200f);

        countdownText = countdownTextObj.AddComponent<TextMeshProUGUI>();
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.fontSize = 110f;
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.color = new Color(1f, 0.85f, 0f); // Sleek gold color
        
        // Add drop shadow effect
        countdownText.outlineColor = Color.black;
        countdownText.outlineWidth = 0.25f;
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
    }

    public void SetHUDActive(bool active)
    {
        string[] hudNames = new string[] {
            "leftMove", "RightMove", "slide", "jump", "Pause",
            // Keep both the original names and the renamed Desert HUD names supported.
            "StatBar", "CoinPanel", "ScoreText", "CoinCollected", "RunScoreText",
            "CrownIcon", "CoinIcon", "Bottle", "GameOverBulletsPanel",
            "CyberBulletsPanel", "OnGameCyberBulletsPanel", "Bullets", "CyberBullets",
            "shooting", "CyberShooting"
        };

        PlayerGunController gc = FindFirstObjectByType<PlayerGunController>();

        foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in hudNames)
            {
                if (child.name == name)
                {
                    bool isWeaponHud = name == "GameOverBulletsPanel" || name == "CyberBulletsPanel" ||
                                       name == "OnGameCyberBulletsPanel" || name == "Bullets" ||
                                       name == "CyberBullets" || name == "shooting" || name == "CyberShooting";
                    child.gameObject.SetActive(active && (!isWeaponHud || (gc != null && gc.HasGun)));
                    break;
                }
            }
        }

        // Also toggle the dynamic shoot button if player currently has gun
        if (gc != null && gc.HasGun)
        {
            Transform shootTrans = transform.Find("shooting");
            if (shootTrans != null)
            {
                shootTrans.gameObject.SetActive(active);
            }
        }
    }
}
