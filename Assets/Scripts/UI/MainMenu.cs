using UnityEngine;
using UnityEngine.SceneManagement;

// The title screen (scene MainMenu): Game Start loads the maze, Settings shows its panel (empty
// for now), Quit closes the game. The buttons are hooked up in the scene.
public class MainMenu : MonoBehaviour
{
    public string gameScene = "Underground";
    public GameObject settingsPanel;

    public AudioClip music;               // (unused: the music is MenuMusic, begun on the start screen)
    [Range(0f, 1f)] public float musicVolume = 0.6f;

    void Start()
    {
        // the menu scene's camera was made without ears: nothing is heard without an AudioListener
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            var cam = Camera.main != null ? Camera.main.gameObject : gameObject;
            cam.AddComponent<AudioListener>();
        }
        // the tune that began on the start screen carries on; it starts here only when the menu is
        // reached some other way (back from the maze)
        MenuMusic.Play(1f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void StartGame()
    {
        RunStats.StartRun();
        SceneManager.LoadScene(gameScene);
    }

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
