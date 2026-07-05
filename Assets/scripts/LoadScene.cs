using UnityEngine;
using UnityEngine.SceneManagement; // Added this to make SceneManager work

public class LoadScene : MonoBehaviour
{
    // Made public so it can be called by a UI Button click event
    public void PlayGame()
    {
        // Loads the scene at index 0 in your Build Settings asynchronously
        SceneManager.LoadSceneAsync(0);
    }
}