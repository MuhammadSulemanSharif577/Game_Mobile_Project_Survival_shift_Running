using UnityEngine;
using UnityEngine.SceneManagement; // Added this to make SceneManager work

public class LoadMenu : MonoBehaviour
{
    // Made public so it can be called by a UI Button click event
    public void MainMenuLoad()
    {
        // Loads the Choose Environment scene asynchronously
        SceneManager.LoadSceneAsync("Menu");
    }
}