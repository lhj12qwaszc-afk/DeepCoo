using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Makes the screen feel wrong as the monster gets closer: a dark red vignette closes in,
// colours drain, film grain and colour fringing creep in, the view trembles and the
// flashlight stutters. Starts at farRange, peaks at nearRange, and is stronger while chased.
// Distance is straight-line, so the dread can be felt through walls.
// Runs early so Update can take last frame's shake back off before the controller looks around;
// LateUpdate still comes after every Update, so the flicker lands on top of the battery's level.
[DefaultExecutionOrder(-200)]
public class MonsterProximityFX : MonoBehaviour
{
    public Transform cameraRoot;
    public Light flashlight;
    public float farRange = 18f;
    public float nearRange = 5f;
    public float chaseBoost = 0.35f;
    public float maxShake = 0.6f;   // degrees

    MonsterWander monster;
    FirstPersonController player;
    Vignette vignette;
    ChromaticAberration chromatic;
    FilmGrain grain;
    ColorAdjustments color;
    float level;
    float flickerTimer;
    float flickerFactor = 1f;
    Quaternion lastShake = Quaternion.identity;

    void Start()
    {
        monster = FindFirstObjectByType<MonsterWander>();
        player = GetComponent<FirstPersonController>();

        var cam = cameraRoot.GetComponent<Camera>();
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.25f, 0f, 0f));
        vignette.smoothness.Override(0.6f);
        chromatic = profile.Add<ChromaticAberration>(true);
        grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3);
        color = profile.Add<ColorAdjustments>(true);

        var volume = new GameObject("MonsterProximityVolume").AddComponent<Volume>();
        volume.transform.SetParent(transform, false);
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        Apply(0f);
    }

    void Update()
    {
        cameraRoot.localRotation *= Quaternion.Inverse(lastShake);
        lastShake = Quaternion.identity;
    }

    void LateUpdate()
    {
        float target = 0f;
        if (monster != null)
        {
            var offset = monster.transform.position - transform.position;
            offset.y = 0f;
            float t = Mathf.InverseLerp(farRange, nearRange, offset.magnitude);
            target = t * t * (3f - 2f * t);
            if (monster.IsChasing && target > 0f)
                target = Mathf.Clamp01(target + chaseBoost);
        }
        // creeps in slowly, fades out a little faster once it's gone
        level = Mathf.MoveTowards(level, target, Time.deltaTime * (target > level ? 0.6f : 0.9f));
        Apply(level);
        Shake(level);
        Flicker(level);
    }

    void Apply(float k)
    {
        // a slow pulse, like a pounding heart, once it gets strong
        float pulse = k > 0.5f ? Mathf.Sin(Time.time * 7f) * 0.04f * k : 0f;
        vignette.intensity.Override(Mathf.Lerp(0f, 0.55f, k) + pulse);
        chromatic.intensity.Override(Mathf.Lerp(0f, 0.7f, k));
        grain.intensity.Override(Mathf.Lerp(0f, 0.75f, k));
        color.saturation.Override(Mathf.Lerp(0f, -55f, k));
        color.postExposure.Override(Mathf.Lerp(0f, -0.4f, k));
    }

    void Shake(float k)
    {
        if (k <= 0.01f || player.IsHidden && k < 0.3f)
            return;
        float s = maxShake * k * k;
        float t = Time.time * 13f;
        lastShake = Quaternion.Euler((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * s * 2f,
                                     (Mathf.PerlinNoise(0.7f, t) - 0.5f) * s * 2f, 0f);
        cameraRoot.localRotation *= lastShake;
    }

    // Runs after FlashlightBattery has set the brightness, and cuts it out in short bursts.
    void Flicker(float k)
    {
        if (flashlight == null || !flashlight.enabled || k < 0.2f)
            return;
        flickerTimer -= Time.deltaTime;
        if (flickerTimer <= 0f)
        {
            bool drop = Random.value < 0.5f * k;
            flickerFactor = drop ? Random.Range(0f, 0.35f) : 1f;
            flickerTimer = drop ? Random.Range(0.03f, 0.1f) : Random.Range(0.1f, 0.6f) / k;
        }
        flashlight.intensity *= flickerFactor;
    }
}
