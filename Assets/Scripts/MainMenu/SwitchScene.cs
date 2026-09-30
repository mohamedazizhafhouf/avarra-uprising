using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchScene : MonoBehaviour
{
    [Tooltip("Name of the scene to load (must be added to Build Settings)")]
    public string sceneName;

    // Call this from the Button's OnClick event
    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
       
    }


}
