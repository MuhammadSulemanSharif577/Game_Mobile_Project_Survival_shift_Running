using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class SceneLoadBinder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Listen for future scene loads
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Bind the current scene (since AfterSceneLoad runs after the initial scene loads)
        BindCurrentScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindCurrentScene();
    }

    private static void BindCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        
        if (sceneName == "Menu")
        {
            // Bind Play button
            GameObject playButtonObj = GameObject.Find("Play");
            if (playButtonObj != null)
            {
                Button playBtn = playButtonObj.GetComponent<Button>();
                if (playBtn != null)
                {
                    playBtn.onClick.RemoveAllListeners();
                    playBtn.onClick.AddListener(() => {
                        SceneManager.LoadSceneAsync("ChoseEnviirnment");
                    });
                    Debug.Log("[SceneLoadBinder] Programmatically bound Play button to load ChoseEnviirnment.");
                }
            }

            // Bind Options button
            GameObject optionsButtonObj = GameObject.Find("Options");
            if (optionsButtonObj != null)
            {
                Button optionsBtn = optionsButtonObj.GetComponent<Button>();
                if (optionsBtn != null)
                {
                    optionsBtn.onClick.RemoveAllListeners();
                    optionsBtn.onClick.AddListener(() => {
                        SceneManager.LoadSceneAsync("ChoseEnviirnment");
                    });
                    Debug.Log("[SceneLoadBinder] Programmatically bound Options button to load ChoseEnviirnment.");
                }
            }

            // Bind the Analytics button created on the menu to its dedicated screen.
            GameObject analyticsButtonObj = GameObject.Find("Analytics");
            if (analyticsButtonObj != null)
            {
                Button analyticsButton = analyticsButtonObj.GetComponent<Button>();
                if (analyticsButton != null)
                {
                    analyticsButton.onClick.RemoveAllListeners();
                    analyticsButton.onClick.AddListener(() => SceneManager.LoadSceneAsync("Analytics"));
                    Debug.Log("[SceneLoadBinder] Programmatically bound Analytics button to load Analytics.");
                }
            }

            GameObject settingsButtonObj = GameObject.Find("Settings");
            if (settingsButtonObj != null)
            {
                Button settingsButton = settingsButtonObj.GetComponent<Button>();
                if (settingsButton != null)
                {
                    settingsButton.onClick.RemoveAllListeners();
                    settingsButton.onClick.AddListener(() => SceneManager.LoadScene("Settings"));
                    Debug.Log("[SceneLoadBinder] Programmatically bound Settings button to load Settings.");
                }
            }
        }
        else if (sceneName == "ChoseEnviirnment")
        {
            // Bind the button visibly labelled Desert.
            GameObject desertBtnObj = GameObject.Find("ChooseDesert");
            if (desertBtnObj != null)
            {
                Button desertBtn = desertBtnObj.GetComponent<Button>();
                if (desertBtn != null)
                {
                    desertBtn.onClick.RemoveAllListeners();
                    desertBtn.onClick.AddListener(() => {
                        SceneManager.LoadSceneAsync("DesertEnvirnment");
                    });
                    Debug.Log("[SceneLoadBinder] Programmatically bound ChooseDesert button to load DesertEnvirnment.");
                }
            }

            // Bind the button visibly labelled Cyberpunk.
            GameObject cyberBtnObj = GameObject.Find("ChooseCyberPunk");
            if (cyberBtnObj != null)
            {
                Button cyberBtn = cyberBtnObj.GetComponent<Button>();
                if (cyberBtn != null)
                {
                    cyberBtn.onClick.RemoveAllListeners();
                    cyberBtn.onClick.AddListener(() => {
                        SceneManager.LoadSceneAsync("CyberPunkCity");
                    });
                    Debug.Log("[SceneLoadBinder] Programmatically bound ChooseCyberPunk button to load CyberPunkCity.");
                }
            }
        }
        else if (sceneName == "DesertEnvirnment" || sceneName == "CyberPunkCity")
        {
            // These controls live in pause/game-over panels, so find inactive buttons too.
            BindBackToMenuButtons();
        }
        else if (sceneName == "Settings")
        {
            GameObject backButtonObj = GameObject.Find("BackToMenuButton");
            if (backButtonObj != null)
            {
                Button backButton = backButtonObj.GetComponent<Button>();
                if (backButton != null)
                {
                    backButton.onClick.RemoveAllListeners();
                    backButton.onClick.AddListener(OpenMenu);
                }
            }
        }
    }

    private static void BindBackToMenuButtons()
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.name != "MainMenu" && button.name != "QuitToMenuButton")
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OpenMenu);
            Debug.Log($"[SceneLoadBinder] Bound {button.name} to load Menu.");
        }
    }

    private static void OpenMenu()
    {
        // Game-over freezes time; restore it before entering the menu.
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync("Menu");
    }
}
