using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the maze dark whether the flashlight is on or off: the faint ambient light, the dim
// directional light and the reflections all stay at DarkShare of what the scene sets, so only
// the flashlight's own beam lights things up. Added to the player at run time; the scene's own
// lighting values are restored when the player goes away.
public class FlashlightDarkness : MonoBehaviour
{
    const float DarkShare = 0.2f;    // how much of the scene's light is left (smaller = darker)

    Light sun;
    Color ambient;
    float sunIntensity, reflections;

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
        ambient = RenderSettings.ambientLight;
        reflections = RenderSettings.reflectionIntensity;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional)
                sun = l;
        if (sun != null)
            sunIntensity = sun.intensity;
        Apply(DarkShare);
        // (the things in the player's hands don't vanish into this dark: the flashlight and
        // tracker materials glow faintly in their own colours, Emission 0.3)
    }

    void Apply(float k)
    {
        RenderSettings.ambientLight = ambient * k;
        RenderSettings.reflectionIntensity = reflections * k;
        if (sun != null)
            sun.intensity = sunIntensity * k;
    }

    void OnDestroy() => Apply(1f);
}
