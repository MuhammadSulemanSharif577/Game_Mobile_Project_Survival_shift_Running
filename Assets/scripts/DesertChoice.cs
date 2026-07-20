using UnityEngine;
using UnityEngine.SceneManagement; // Added this to make SceneManager work

public class DesertChose : MonoBehaviour
{
    // Made public so it can be called by a UI Button click event
    public void choseDesert()
    {
        // Loads the Desert gameplay scene asynchronously.
        SceneManager.LoadSceneAsync("DesertEnvirnment");
    }
}
