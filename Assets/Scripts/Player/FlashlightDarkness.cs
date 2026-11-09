using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the maze dark whether the flashlight is on or off: the faint ambient light, the dim
// directional light and the reflections all stay at DarkShare of what the scene sets, so only
// the flashlight's own beam lights things up. Hidden in a locker, wardrobe or bin (where the
// flashlight goes off) the eyes adjust: it slowly brightens to HiddenShare so the view through
// the slats can be made out, and is dark again the moment the player comes out. Added to the player at run time;
// the scene's own lighting values are restored when the player goes away.
public class FlashlightDarkness : MonoBehaviour
{
    const float DarkShare = 0.3f;     // how much of the scene's light is left (smaller = darker)
    const float HiddenShare = 1.1f;   // ...while hidden (the eyes have adjusted)
    const float AdjustSpeed = 2.5f;   // share per second the eyes adjust by

    FirstPersonController fps;
    Light sun;
    Color ambient;
    float sunIntensity, reflections;
    float level = DarkShare;
    float appliedFactor = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null && player.GetComponent<FlashlightDarkness>() == null)
            player.gameObject.AddComponent<FlashlightDarkness>();
    }

    void Start()
    {
        fps = GetComponent<FirstPersonController>();
        ambient = RenderSettings.ambientLight;
        reflections = RenderSettings.reflectionIntensity;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional)
                sun = l;
        if (sun != null)
            sunIntensity = sun.intensity;
        Apply(level);
        // (the things in the player's hands don't vanish into this dark: the flashlight and
        // tracker materials glow faintly in their own colours)
    }

    void Update()
    {
        float target = fps != null && fps.IsHidden ? HiddenShare : DarkShare;
        if (!Mathf.Approximately(appliedFactor, GameSettings.BrightnessFactor))
            Apply(level);   // the brightness setting was changed (pause menu)
        if (Mathf.Approximately(level, target))
            return;
        // eyes adjust slowly once hidden; coming out it's dark again at once
        level = target < level ? target : Mathf.MoveTowards(level, target, AdjustSpeed * Time.deltaTime);
        Apply(level);
    }

    void Apply(float k)
    {
        appliedFactor = GameSettings.BrightnessFactor;
        k *= appliedFactor;   // the player's brightness setting
        RenderSettings.ambientLight = ambient * k;
        RenderSettings.reflectionIntensity = reflections * k;
        if (sun != null)
            sun.intensity = sunIntensity * k;
    }

    void OnDestroy()
    {
        RenderSettings.ambientLight = ambient;
        RenderSettings.reflectionIntensity = reflections;
        if (sun != null)
            sun.intensity = sunIntensity;
    }
}
