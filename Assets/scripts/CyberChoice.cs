using UnityEngine;
using UnityEngine.SceneManagement; // Added this to make SceneManager work

public class CyberChose : MonoBehaviour
{
    // Made public so it can be called by a UI Button click event
    public void ChoseCyber()
    {
        SceneManager.LoadSceneAsync("CyberPunkCity");
    }
}
