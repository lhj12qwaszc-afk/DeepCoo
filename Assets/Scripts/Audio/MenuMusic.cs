using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The menu music (Menu_Music), one tune that carries on from the start screen's title into the
// main menu without starting over. It lives outside the scenes; any scene that isn't the start
// screen or the main menu (the maze) fades it out.
public class MenuMusic : MonoBehaviour
{
    public const float Volume = 0.6f;
    static readonly string[] MenuScenes = { "Start", "MainMenu" };

    static MenuMusic instance;
    AudioSource source;

    // starts the tune (fading in over fadeSeconds), or lets it carry on if it is already playing
    public static void Play(float fadeSeconds = 0f)
    {
        if (instance != null)
            return;
        var clip = Sfx.Bank != null ? Sfx.Bank.menuMusic : null;
        if (clip == null)
            return;
        var go = new GameObject("Menu Music");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MenuMusic>();
        var s = instance.source = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        s.volume = fadeSeconds > 0f ? 0f : Volume;
        s.Play();
        if (fadeSeconds > 0f)
            instance.StartCoroutine(instance.FadeTo(Volume, fadeSeconds, false));
    }

    public static void FadeOut(float seconds = 1.2f)
    {
        if (instance != null)
            instance.StartCoroutine(instance.FadeTo(0f, seconds, true));
    }

    IEnumerator FadeTo(float target, float seconds, bool thenStop)
    {
        float from = source.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(from, target, t / seconds);
            yield return null;
        }
        source.volume = target;
        if (thenStop)
        {
            if (instance == this) instance = null;
            Destroy(gameObject);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (System.Array.IndexOf(MenuScenes, scene.name) < 0)
                FadeOut();   // into the maze: the menu tune goes
        };
    }
}
