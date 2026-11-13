using UnityEngine;

// The player's choices from the settings panel, kept between runs (PlayerPrefs).
//   Brightness      0..1, 0.5 = as designed. Scales how much light is left in the dark maze.
//   Volume          0..1, every sound in the game (AudioListener).
//   MicOn           whether the microphone is listened to at all.
//   MicSensitivity  0..1, higher = quieter sounds already reach the monster.
public static class GameSettings
{
    const string BrightnessKey = "Settings.Brightness";
    const string VolumeKey = "Settings.Volume";
    const string MicOnKey = "Settings.MicOn";
    const string MicSensitivityKey = "Settings.MicSensitivity";

    public const float DefaultBrightness = 0.5f, DefaultVolume = 1f, DefaultMicSensitivity = 0.5f;
    public const bool DefaultMicOn = true;

    public static float Brightness
    {
        get => PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness);
        set { PlayerPrefs.SetFloat(BrightnessKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); BrightnessFactor = Factor(); }
    }

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
        set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = Volume; }
    }

    public static bool MicOn
    {
        get => PlayerPrefs.GetInt(MicOnKey, DefaultMicOn ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(MicOnKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static float MicSensitivity
    {
        get => PlayerPrefs.GetFloat(MicSensitivityKey, DefaultMicSensitivity);
        set { PlayerPrefs.SetFloat(MicSensitivityKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    // multiplier for the maze's leftover light: 0.5x at the darkest, 1x in the middle, 2x at the brightest
    public static float BrightnessFactor { get; private set; } = 1f;
    static float Factor() => Factor(Brightness);
    static float Factor(float brightness) => Mathf.Pow(2f, (brightness - 0.5f) * 2f);

    // the quietest sound (dB) the monster still hears: -35 dB at low sensitivity, -55 dB at high
    public static float MicQuietDb => Mathf.Lerp(-35f, -55f, sensitivityPreview ?? MicSensitivity);

    // While the settings panel is open its dials are tried out at once, before Save
    // (EndPreview puts the saved values back).
    static float? sensitivityPreview;
    public static void PreviewBrightness(float brightness) => BrightnessFactor = Factor(brightness);
    public static void PreviewSensitivity(float sensitivity) => sensitivityPreview = sensitivity;
    public static void EndPreview()
    {
        sensitivityPreview = null;
        BrightnessFactor = Factor();
        AudioListener.volume = Volume;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnStart()
    {
        AudioListener.volume = Volume;
        BrightnessFactor = Factor();
    }
}
