using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Simple serialized UI button navigation that does not rely on build indices.</summary>
public class SceneNavigation : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void LoadScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
