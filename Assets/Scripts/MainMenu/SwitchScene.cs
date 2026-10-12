using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchScene : MonoBehaviour
{
    [Tooltip("Name of the scene to load (must be added to Build Settings)")]
    public string sceneName;


    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }



    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
       
    }


}
