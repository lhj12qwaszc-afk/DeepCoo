using UnityEditor;
using UnityEditor.SceneManagement;

// Pressing Play in the Editor always starts from the start screen (then the main menu), like the built game does,
// whichever scene is open. Turn it off with DeepCoo > Play From Main Menu to test a scene directly.
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    const string MenuScene = "Assets/Scenes/Start.unity";   // the start screen, which leads to the main menu
    const string MenuPath = "DeepCoo/Play From Main Menu";
    const string PrefKey = "DeepCoo.PlayFromMainMenu";

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static PlayFromMainMenu()
    {
        // wait until the asset database is ready before loading the scene asset
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene)
            : null;
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
}
