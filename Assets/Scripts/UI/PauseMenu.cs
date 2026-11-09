using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ESC in the game opens this: the game stops (monster, battery, sounds) and the mouse is free.
// Continue (or ESC again) goes back, Settings opens the same settings panel as the main menu
// (ESC closes it again), Main Menu ends the run, Quit closes the game. While a note, the padlock,
// the birdcage or the inventory is up, ESC belongs to them instead.
// Lives on the game scene's "PauseMenu" canvas, built by DeepCoo > Build Pause Menu.
public class PauseMenu : MonoBehaviour
{
    public GameObject root;            // the dimmed screen with the buttons
    public GameObject settingsPanel;
    public string mainMenuScene = "MainMenu";

    public static bool IsOpen { get; private set; }

    FirstPersonController fps;
    bool wasInputLocked;
    bool busyLastFrame;                // a close-up was open: its ESC closes it, not opens this

    void Awake()
    {
        IsOpen = false;
        root.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // after every other script's Update, so the player's own ESC handling (freeing the mouse)
    // can't undo Resume's locking it again
    void LateUpdate()
    {
        bool busy = busyLastFrame;
        busyLastFrame = Interactable.CloseUpOpen;
        var k = Keyboard.current;
        if (k == null || !k.escapeKey.wasPressedThisFrame)
            return;
        if (IsOpen)
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
                CloseSettings();
            else
                Resume();
            return;
        }
        // a close-up, or the game already stopped (the escape screen), keeps ESC for itself
        if (busy || Interactable.CloseUpOpen || Time.timeScale == 0f)
            return;
        Open();
    }

    void Open()
    {
        if (fps == null) fps = FindFirstObjectByType<FirstPersonController>();
        IsOpen = true;
        root.SetActive(true);
        // hidden: the slats (drawn on top of everything otherwise) are drawn here, behind the menu
        var interactor = fps != null ? fps.GetComponent<PlayerInteractor>() : null;
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        ShowSlats(interactor != null ? interactor.HiddenIn : null);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (fps != null)
        {
            wasInputLocked = fps.inputLocked;
            fps.inputLocked = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    RectTransform slatLayer;

    void ShowSlats(HidingSpot spot)
    {
        if (slatLayer != null)
            Destroy(slatLayer.gameObject);
        slatLayer = null;
        if (spot == null)
            return;
        slatLayer = new GameObject("Slats", typeof(RectTransform)).GetComponent<RectTransform>();
        slatLayer.SetParent(transform, false);
        slatLayer.SetSiblingIndex(0);   // behind the dimmed screen and the menu
        slatLayer.anchorMin = Vector2.zero;
        slatLayer.anchorMax = Vector2.one;
        slatLayer.offsetMin = slatLayer.offsetMax = Vector2.zero;
        foreach (var r in PlayerInteractor.SlatRects(spot.kind))
        {
            var bar = new GameObject("Slat", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = (RectTransform)bar.transform;
            rt.SetParent(slatLayer, false);
            rt.anchorMin = new Vector2(r.xMin, 1f - r.yMax);   // GUI rects run top-down
            rt.anchorMax = new Vector2(r.xMax, 1f - r.yMin);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = bar.GetComponent<UnityEngine.UI.Image>();
            image.color = PlayerInteractor.SlatColor;
            image.raycastTarget = false;
        }
    }

    public void Resume()
    {
        ShowSlats(null);
        IsOpen = false;
        root.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (fps != null) fps.inputLocked = wasInputLocked;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OpenSettings() => settingsPanel.SetActive(true);

    public void CloseSettings() => settingsPanel.SetActive(false);

    public void ToMainMenu()
    {
        Leave();
        SceneManager.LoadScene(mainMenuScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        Leave();
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Leave()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void OnDestroy()
    {
        if (IsOpen) Leave();
    }
}
