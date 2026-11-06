using UnityEngine;
using UnityEngine.SceneManagement;

// The title screen (scene MainMenu): Game Start loads the maze, Settings shows its panel (empty
// for now), Quit closes the game. The buttons are hooked up in the scene.
public class MainMenu : MonoBehaviour
{
    public string gameScene = "Underground";
    public GameObject settingsPanel;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void StartGame() => SceneManager.LoadScene(gameScene);

    public void OpenSettings() => settingsPanel.SetActive(true);

    public void CloseSettings() => settingsPanel.SetActive(false);

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
