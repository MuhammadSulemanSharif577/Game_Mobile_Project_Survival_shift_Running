using UnityEngine;
using UnityEngine.SceneManagement; // Added this to make SceneManager work

public class WindowChoice : MonoBehaviour
{
    // Made public so it can be called by a UI Button click event
    public void ChoseWindow()
    {
        // Loads the Choose Environment scene asynchronously
        SceneManager.LoadSceneAsync("ChoseEnviirnment");
    }
}